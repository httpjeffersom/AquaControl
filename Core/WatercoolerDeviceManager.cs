using WatercoolerTemp.Core.Devices;
using WatercoolerTemp.Core.RiseModeAuraIce;

namespace WatercoolerTemp.Core;

/// <summary>
/// Coordena os water coolers suportados. Cada dispositivo tem sua própria
/// sessão, transporte e protocolo; o gerenciador apenas descobre, abre,
/// inicia, remove e encaminha os eventos para a interface.
///
/// O app foi desenhado para um único water cooler por PC: o gerenciador
/// identifica qual família está presente (Pichau Aqua ou Rise Mode Aura Ice) e
/// controla somente ela. Um PC com os dois conectados não é um cenário
/// suportado; se acontecer, o primeiro encontrado fica ativo e o outro é
/// ignorado até o primeiro sair.
/// </summary>
public sealed class WatercoolerDeviceManager : IAsyncDisposable
{
    private const int UpdateIntervalMs = 1000;

    private readonly object gate = new();
    private readonly Dictionary<string, ActiveDevice> activeDevices =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim reconcileGate = new(1, 1);

    // O LibreHardwareMonitor mantém estado estático (OpCode/Ring0); abrir dois
    // "Computer" no mesmo processo não é seguro. Todos os drivers compartilham
    // este único leitor.
    private readonly CpuTemperatureReader sharedTemperatureReader = new();

    private bool isDisposed;
    private bool monitoringRequested = true;
    private string? primaryKey;

    public event Action<int>? TemperatureRead;
    public event Action<int?>? CpuUsageRead;
    public event Action<int, byte[]>? TemperatureSent;
    public event Action? TemperatureUnavailable;
    public event Action<Exception>? Error;
    public event Action? DevicesChanged;

    public bool IsConnected
    {
        get
        {
            lock (gate)
                return activeDevices.Count > 0;
        }
    }

    public bool IsMonitoring
    {
        get
        {
            lock (gate)
                return monitoringRequested && activeDevices.Values.Any(device => device.Loop is not null);
        }
    }

    /// <summary>
    /// Procura dispositivos novos, remove os que sumiram e abre os que ainda
    /// não estão ativos. Não lança exceção por ausência de hardware.
    /// </summary>
    public async Task ReconcileAsync()
    {
        if (isDisposed)
            return;

        await reconcileGate.WaitAsync().ConfigureAwait(false);
        try
        {
            List<DeviceDescriptor> discovered;
            try
            {
                discovered = await Task.Run(Discover).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                AppLogger.Error("Falha ao procurar water coolers", exception);
                return;
            }

            bool changed = false;

            List<ActiveDevice> removed;
            lock (gate)
            {
                removed = activeDevices.Values
                    .Where(device => !discovered.Any(candidate => string.Equals(
                        candidate.Key,
                        device.Descriptor.Key,
                        StringComparison.OrdinalIgnoreCase)))
                    .ToList();
            }

            foreach (ActiveDevice device in removed)
            {
                await RemoveDeviceAsync(device).ConfigureAwait(false);
                changed = true;
            }

            foreach (DeviceDescriptor descriptor in discovered)
            {
                bool hasActiveDevice;
                lock (gate)
                    hasActiveDevice = activeDevices.Count > 0;

                // Um PC tem um water cooler só. Depois que um está ativo, os
                // demais candidatos (cenário teórico de dois conectados) não
                // são abertos: o segundo só entra se o primeiro sair.
                if (hasActiveDevice)
                    break;

                IWatercoolerDeviceSession session;
                try
                {
                    session = descriptor.CreateSession();
                }
                catch (Exception exception)
                {
                    AppLogger.Error($"Falha ao criar sessão para {descriptor.DisplayName}", exception);
                    Error?.Invoke(exception);
                    continue;
                }

                var active = new ActiveDevice(descriptor, session);
                Subscribe(active);

                try
                {
                    await Task.Run(session.Open).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    AppLogger.Error($"Falha ao abrir {descriptor.DisplayName}", exception);
                    session.Dispose();
                    Error?.Invoke(exception);
                    continue;
                }

                lock (gate)
                {
                    activeDevices[descriptor.Key] = active;
                    primaryKey ??= descriptor.Key;
                }

                if (monitoringRequested)
                    StartLoop(active);

                AppLogger.Info($"{descriptor.DisplayName} conectado ({descriptor.Key}).");
                changed = true;
            }

            if (changed)
                DevicesChanged?.Invoke();
        }
        finally
        {
            reconcileGate.Release();
        }
    }

    public void StartMonitoring()
    {
        if (isDisposed)
            return;

        List<ActiveDevice> snapshot;
        lock (gate)
        {
            monitoringRequested = true;
            snapshot = activeDevices.Values.ToList();
            if (primaryKey is null && snapshot.Count > 0)
                primaryKey = snapshot[0].Descriptor.Key;
        }

        foreach (ActiveDevice device in snapshot)
            StartLoop(device);

        DevicesChanged?.Invoke();
    }

    public async Task StopMonitoringAsync()
    {
        await reconcileGate.WaitAsync().ConfigureAwait(false);
        try
        {
            List<ActiveDevice> snapshot;
            lock (gate)
            {
                monitoringRequested = false;
                snapshot = activeDevices.Values.ToList();
            }

            foreach (ActiveDevice device in snapshot)
                await StopLoopAsync(device).ConfigureAwait(false);
        }
        finally
        {
            reconcileGate.Release();
        }

        DevicesChanged?.Invoke();
    }

    public async ValueTask DisposeAsync()
    {
        if (isDisposed)
            return;

        isDisposed = true;
        await reconcileGate.WaitAsync().ConfigureAwait(false);
        try
        {
            List<ActiveDevice> snapshot;
            lock (gate)
            {
                monitoringRequested = false;
                snapshot = activeDevices.Values.ToList();
            }

            foreach (ActiveDevice device in snapshot)
                await RemoveDeviceAsync(device).ConfigureAwait(false);

            sharedTemperatureReader.Dispose();

            lock (gate)
            {
                activeDevices.Clear();
                primaryKey = null;
            }
        }
        finally
        {
            reconcileGate.Release();
        }
    }

    private List<DeviceDescriptor> Discover()
    {
        var devices = new List<DeviceDescriptor>();

        try
        {
            string portName = Aqua240XPortDiscovery.FindPort();
            devices.Add(new DeviceDescriptor(
                $"pichau-aqua-serial:{portName}",
                "PICHAU AQUA 240X",
                () => new PichauAqua240XSession(portName, sharedTemperatureReader, UpdateIntervalMs)));
        }
        catch (InvalidOperationException)
        {
            // Ausência do Pichau ou mais de uma porta compatível. O suporte
            // existente trata esses casos como "não conectar"; o Rise Mode
            // continua sendo procurado normalmente.
        }
        catch (Exception exception)
        {
            AppLogger.Error("Falha ao procurar o Pichau Aqua", exception);
        }

        foreach (RiseModeAuraIceDeviceInfo info in RiseModeAuraIceDeviceDiscovery.FindDevices())
        {
            RiseModeAuraIceDeviceInfo captured = info;
            devices.Add(new DeviceDescriptor(
                $"rise-mode-aura-ice:{captured.DevicePath}",
                RiseModeAuraIceDeviceInfo.DisplayName,
                () => new RiseModeAuraIceSession(captured, sharedTemperatureReader, UpdateIntervalMs)));
        }

        return devices;
    }

    private void Subscribe(ActiveDevice device)
    {
        string key = device.Descriptor.Key;
        device.Session.TemperatureRead += temperature => ForwardTemperatureRead(key, temperature);
        device.Session.CpuUsageRead += usage => ForwardCpuUsage(key, usage);
        device.Session.TemperatureUnavailable += () => ForwardTemperatureUnavailable(key);
        device.Session.TemperatureSent += (temperature, packet) =>
            TemperatureSent?.Invoke(temperature, packet);
        device.Session.Error += exception => _ = HandleSessionErrorAsync(key, exception);
    }

    private void ForwardTemperatureRead(string key, int temperature)
    {
        lock (gate)
        {
            if (!string.Equals(primaryKey, key, StringComparison.OrdinalIgnoreCase))
                return;
        }

        TemperatureRead?.Invoke(temperature);
    }

    private void ForwardCpuUsage(string key, int? usage)
    {
        lock (gate)
        {
            if (!string.Equals(primaryKey, key, StringComparison.OrdinalIgnoreCase))
                return;
        }

        CpuUsageRead?.Invoke(usage);
    }

    private void ForwardTemperatureUnavailable(string key)
    {
        lock (gate)
        {
            if (!string.Equals(primaryKey, key, StringComparison.OrdinalIgnoreCase))
                return;
        }

        TemperatureUnavailable?.Invoke();
    }

    private async Task HandleSessionErrorAsync(string key, Exception exception)
    {
        Error?.Invoke(exception);

        await reconcileGate.WaitAsync().ConfigureAwait(false);
        try
        {
            ActiveDevice? device;
            lock (gate)
                activeDevices.TryGetValue(key, out device);

            if (device is null)
                return;

            await RemoveDeviceAsync(device).ConfigureAwait(false);
            DevicesChanged?.Invoke();
        }
        finally
        {
            reconcileGate.Release();
        }
    }

    private void StartLoop(ActiveDevice device)
    {
        lock (gate)
        {
            if (device.Loop is not null || isDisposed)
                return;

            device.Cancellation = new CancellationTokenSource();
            CancellationToken token = device.Cancellation.Token;
            device.Loop = Task.Run(() => device.Session.StartAsync(token), token);
        }
    }

    private async Task StopLoopAsync(ActiveDevice device)
    {
        CancellationTokenSource? cancellation;
        Task? loop;

        lock (gate)
        {
            cancellation = device.Cancellation;
            loop = device.Loop;
            device.Cancellation = null;
            device.Loop = null;
        }

        if (cancellation is null)
            return;

        cancellation.Cancel();
        try
        {
            if (loop is not null)
                await loop.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            AppLogger.Error("Loop de monitoramento encerrado com erro", exception);
        }
        finally
        {
            cancellation.Dispose();
        }
    }

    private async Task RemoveDeviceAsync(ActiveDevice device)
    {
        lock (gate)
        {
            if (!activeDevices.Remove(device.Descriptor.Key))
                return;

            if (string.Equals(primaryKey, device.Descriptor.Key, StringComparison.OrdinalIgnoreCase))
                primaryKey = activeDevices.Keys.FirstOrDefault();
        }

        await StopLoopAsync(device).ConfigureAwait(false);
        device.Session.Dispose();
        AppLogger.Info($"{device.Descriptor.DisplayName} desconectado ({device.Descriptor.Key}).");
    }

    private sealed class DeviceDescriptor
    {
        public DeviceDescriptor(
            string key,
            string displayName,
            Func<IWatercoolerDeviceSession> createSession)
        {
            Key = key;
            DisplayName = displayName;
            CreateSession = createSession;
        }

        public string Key { get; }
        public string DisplayName { get; }
        public Func<IWatercoolerDeviceSession> CreateSession { get; }
    }

    private sealed class ActiveDevice
    {
        public ActiveDevice(DeviceDescriptor descriptor, IWatercoolerDeviceSession session)
        {
            Descriptor = descriptor;
            Session = session;
        }

        public DeviceDescriptor Descriptor { get; }
        public IWatercoolerDeviceSession Session { get; }
        public CancellationTokenSource? Cancellation { get; set; }
        public Task? Loop { get; set; }
    }
}
