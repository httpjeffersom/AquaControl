using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using WatercoolerTemp.Core;

namespace WatercoolerTemp.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged, IAsyncDisposable
{
    private const int IntervalMs = 1000;
    private const int ReconnectIntervalMs = 5000;
    private const int HighTemperatureThreshold = 90;
    private const int AlertResetThreshold = 80;
    private readonly DispatcherTimer displayTimer;
    private readonly DispatcherTimer reconnectTimer;
    private WatercoolerDeviceManager? deviceManager;
    private bool monitoringRequested = true;
    private bool isConnecting;
    private bool isDisposing;
    private string notice = string.Empty;
    private DateTime? lastSentAt;
    private int connectedDeviceCount;
    private bool hasTemperature;
    private bool isHighTemperature;
    private int highTemperatureValue;
    private string deviceSummary = "NENHUM DISPOSITIVO";
    private double targetTemperature;
    private double displayedTemperature;
    private bool isConnected;
    private bool isMonitoring;
    private int? minimumTemperature;
    private int? maximumTemperature;
    private int? cpuUsage;
    private double averageTemperature;
    private int temperatureSamples;
    private bool highTemperatureAlertShown;

    public MainViewModel()
    {
        displayTimer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        displayTimer.Tick += AnimateDisplay;
        reconnectTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(ReconnectIntervalMs) };
        reconnectTimer.Tick += async (_, _) => await AttemptConnectAsync();

        reconnectTimer.Start();
        _ = AttemptConnectAsync();
    }

    public bool IsStartupEnabled
    {
        get => StartupManager.IsEnabled;
        set
        {
            if (value == IsStartupEnabled)
                return;

            try
            {
                StartupManager.SetEnabled(value);
                OnPropertyChanged();
            }
            catch (Exception exception)
            {
                AppLogger.Error("Falha ao alterar a inicialização com o Windows", exception);
                SetNotice("FALHA AO ALTERAR A INICIALIZAÇÃO COM O WINDOWS");
                OnPropertyChanged();
            }
        }
    }

    public bool IsMonitoring
    {
        get => isMonitoring;
        private set
        {
            if (SetField(ref isMonitoring, value))
            {
                OnPropertyChanged(nameof(ConnectionText));
                OnPropertyChanged(nameof(ConnectionBrush));
            }
        }
    }

    public string DeviceSummary
    {
        get => deviceSummary;
        private set
        {
            if (SetField(ref deviceSummary, value))
                OnPropertyChanged(nameof(ConnectionText));
        }
    }

    /// <summary>
    /// Linha única de estado exibida abaixo do anel. Substitui o antigo bloco
    /// "DISPOSITIVO" (que ocupava duas linhas e tinha uma bolinha ao lado).
    /// </summary>
    public string ConnectionText
    {
        get
        {
            if (!IsConnected)
                return "NENHUM WATER COOLER CONECTADO";

            if (!IsMonitoring)
                return "MONITORAMENTO PAUSADO";

            return connectedDeviceCount > 1
                ? $"{connectedDeviceCount} WATER COOLERS CONECTADOS"
                : $"WATER COOLER CONECTADO · {DeviceSummary}";
        }
    }

    public System.Windows.Media.Brush ConnectionBrush =>
        !IsConnected ? FaultBrush
        : !IsMonitoring ? PausedBrush
        : ConnectedBrush;

    /// <summary>Erro atual (prioritário) ou horário da última leitura.</summary>
    public string FooterText => notice.Length > 0
        ? notice
        : lastSentAt is null
            ? IsConnected ? "AGUARDANDO PRIMEIRA LEITURA" : string.Empty
            : $"ÚLTIMA LEITURA {lastSentAt:HH:mm:ss}";

    public System.Windows.Media.Brush FooterBrush => notice.Length > 0 ? FaultBrush : MutedBrush;

    public bool IsHighTemperature
    {
        get => isHighTemperature;
        private set => SetField(ref isHighTemperature, value);
    }

    public string HighTemperatureText => $"TEMPERATURA ALTA · {highTemperatureValue} °C";

    public string MinimumTemperatureText => IsConnected && minimumTemperature is not null ? $"{minimumTemperature} °C" : "--";
    public string MaximumTemperatureText => IsConnected && maximumTemperature is not null ? $"{maximumTemperature} °C" : "--";
    public string AverageTemperatureText => IsConnected && temperatureSamples != 0 ? $"{averageTemperature:0} °C" : "--";
    public string CpuUsageText => cpuUsage is null ? "--%" : $"{cpuUsage}%";
    public System.Windows.Media.Brush CpuUsageBrush => cpuUsage switch
    {
        null => DisconnectedBrush,
        >= 90 => HighCpuUsageBrush,
        >= 80 => ElevatedCpuUsageBrush,
        _ => ConnectedBrush
    };

    public double DisplayedTemperature
    {
        get => displayedTemperature;
        private set
        {
            if (SetField(ref displayedTemperature, value))
            {
                OnPropertyChanged(nameof(TemperatureText));
                OnPropertyChanged(nameof(Progress));
                OnPropertyChanged(nameof(IndicatorBrush));
            }
        }
    }

    public string TemperatureText => hasTemperature ? $"{Math.Round(DisplayedTemperature):0}" : "--";
    public double Progress => Math.Clamp(DisplayedTemperature, 0, 100) / 100;
    public System.Windows.Media.Brush IndicatorBrush => CreateBrush(GetTemperatureColor(DisplayedTemperature));

    public bool IsConnected
    {
        get => isConnected;
        private set
        {
            if (SetField(ref isConnected, value))
            {
                OnPropertyChanged(nameof(ConnectionText));
                OnPropertyChanged(nameof(ConnectionBrush));
                OnPropertyChanged(nameof(MinimumTemperatureText));
                OnPropertyChanged(nameof(MaximumTemperatureText));
                OnPropertyChanged(nameof(AverageTemperatureText));
                OnPropertyChanged(nameof(FooterText));
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action<int>? HighTemperatureAlert;

    private async Task AttemptConnectAsync()
    {
        if (isConnecting || isDisposing)
            return;

        isConnecting = true;
        try
        {
            deviceManager ??= CreateDeviceManager();

            bool wasConnected = IsConnected;
            await deviceManager.ReconcileAsync();

            if (monitoringRequested && deviceManager.IsConnected && !deviceManager.IsMonitoring)
                deviceManager.StartMonitoring();

            if (!wasConnected && deviceManager.IsConnected)
                ResetStatistics();

            UpdateConnectionState();
        }
        catch (Exception exception)
        {
            AppLogger.Error("Falha ao localizar ou conectar ao watercooler", exception);
            UpdateConnectionState();
        }
        finally
        {
            isConnecting = false;
        }
    }

    private WatercoolerDeviceManager CreateDeviceManager()
    {
        var manager = new WatercoolerDeviceManager();
        manager.TemperatureRead += OnTemperatureRead;
        manager.CpuUsageRead += OnCpuUsageRead;
        manager.TemperatureSent += OnTemperatureSent;
        manager.TemperatureUnavailable += OnTemperatureUnavailable;
        manager.DevicesChanged += OnDevicesChanged;
        manager.Error += OnManagerError;
        return manager;
    }

    private void OnDevicesChanged()
    {
        if (System.Windows.Application.Current?.Dispatcher is { } dispatcher
            && !dispatcher.CheckAccess())
        {
            dispatcher.InvokeAsync(UpdateConnectionState);
            return;
        }

        UpdateConnectionState();
    }

    private void UpdateConnectionState()
    {
        bool connected = deviceManager?.IsConnected == true;
        bool monitoring = deviceManager?.IsMonitoring == true;

        // Preenche as dependências antes de IsConnected/IsMonitoring, porque os
        // setters já notificam a linha de estado.
        connectedDeviceCount = deviceManager?.ConnectedDeviceCount ?? 0;
        DeviceSummary = deviceManager?.DeviceSummary ?? "NENHUM DISPOSITIVO";

        IsConnected = connected;
        IsMonitoring = monitoring;

        if (!connected)
        {
            // Ao reconectar, o valor antigo não deve aparecer como se fosse
            // uma leitura nova: volta para "--" até o primeiro envio.
            if (hasTemperature)
            {
                hasTemperature = false;
                OnPropertyChanged(nameof(TemperatureText));
            }

            if (IsHighTemperature)
                IsHighTemperature = false;
        }

        OnPropertyChanged(nameof(ConnectionText));
        OnPropertyChanged(nameof(ConnectionBrush));
    }

    public void StartMonitoring()
    {
        if (isDisposing)
            return;

        monitoringRequested = true;
        deviceManager?.StartMonitoring();
        UpdateConnectionState();
    }

    public async Task StopMonitoringAsync(bool reportErrors = true)
    {
        monitoringRequested = false;
        if (deviceManager is null)
            return;

        try
        {
            await deviceManager.StopMonitoringAsync();
        }
        catch (Exception exception)
        {
            AppLogger.Error("Falha ao pausar o monitoramento", exception);
            if (reportErrors)
                SetNotice("FALHA AO PAUSAR O MONITORAMENTO");
        }

        UpdateConnectionState();
    }

    private void OnTemperatureRead(int temperature)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            targetTemperature = Math.Clamp(temperature, 0, 100);
            EvaluateTemperatureAlert(temperature);

            if (!hasTemperature)
            {
                hasTemperature = true;
                OnPropertyChanged(nameof(TemperatureText));
            }

            temperatureSamples++;
            averageTemperature = ((averageTemperature * (temperatureSamples - 1)) + temperature) / temperatureSamples;
            minimumTemperature = minimumTemperature is null ? temperature : Math.Min(minimumTemperature.Value, temperature);
            maximumTemperature = maximumTemperature is null ? temperature : Math.Max(maximumTemperature.Value, temperature);
            displayTimer.Start();
            OnPropertyChanged(nameof(MinimumTemperatureText));
            OnPropertyChanged(nameof(MaximumTemperatureText));
            OnPropertyChanged(nameof(AverageTemperatureText));
        });
    }

    private void OnCpuUsageRead(int? usage)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            cpuUsage = usage;
            OnPropertyChanged(nameof(CpuUsageText));
            OnPropertyChanged(nameof(CpuUsageBrush));
        });
    }

    private void OnTemperatureSent(int temperature, byte[] pacote, string deviceName)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            // O envio a cada segundo não vira mensagem na tela (antes ele
            // piscava "TEMPERATURA ENVIADA" e não sobrava espaço para erros).
            lastSentAt = DateTime.Now;
            SetNotice(string.Empty);
            OnPropertyChanged(nameof(FooterText));
        });
    }

    private void EvaluateTemperatureAlert(int temperature)
    {
        if (temperature <= AlertResetThreshold)
        {
            highTemperatureAlertShown = false;
            if (isHighTemperature)
            {
                highTemperatureValue = 0;
                IsHighTemperature = false;
                OnPropertyChanged(nameof(HighTemperatureText));
            }
            return;
        }

        if (temperature >= HighTemperatureThreshold && !highTemperatureAlertShown)
        {
            highTemperatureAlertShown = true;
            highTemperatureValue = temperature;
            IsHighTemperature = true;
            OnPropertyChanged(nameof(HighTemperatureText));
            HighTemperatureAlert?.Invoke(temperature);
            return;
        }

        if (isHighTemperature)
        {
            highTemperatureValue = temperature;
            OnPropertyChanged(nameof(HighTemperatureText));
        }
    }

    private void OnTemperatureUnavailable()
    {
        System.Windows.Application.Current.Dispatcher.Invoke(
            () => SetNotice("SENSOR DA CPU INDISPONÍVEL"));
    }

    private void OnManagerError(Exception exception)
    {
        // O gerenciador já removeu a sessão com falha. Se ainda houver outro
        // water cooler ativo, o programa continua normalmente com ele.
        AppLogger.Error("Monitoramento de um watercooler desconectado", exception);
        System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            if (deviceManager?.IsConnected != true)
                SetNotice("PERDA DE COMUNICAÇÃO COM O WATER COOLER");
        });
    }

    private void AnimateDisplay(object? sender, EventArgs eventArgs)
    {
        double difference = targetTemperature - DisplayedTemperature;
        if (Math.Abs(difference) < 0.05)
        {
            DisplayedTemperature = targetTemperature;
            displayTimer.Stop();
            return;
        }

        DisplayedTemperature += difference * 0.14;
    }

    private static System.Windows.Media.Color GetTemperatureColor(double temperature)
    {
        // O anel fica completamente vermelho a partir de 90 °C, que é o mesmo
        // ponto em que o alerta de temperatura alta dispara.
        if (temperature <= 65)
            return System.Windows.Media.Color.FromRgb(83, 235, 151);
        if (temperature <= 80)
            return Interpolate(System.Windows.Media.Color.FromRgb(83, 235, 151), System.Windows.Media.Color.FromRgb(245, 213, 73), (temperature - 65) / 15);
        if (temperature < 90)
            return Interpolate(System.Windows.Media.Color.FromRgb(245, 213, 73), System.Windows.Media.Color.FromRgb(255, 65, 75), (temperature - 80) / 10);

        return System.Windows.Media.Color.FromRgb(255, 65, 75);
    }

    private static System.Windows.Media.Color Interpolate(System.Windows.Media.Color from, System.Windows.Media.Color to, double amount)
    {
        amount = Math.Clamp(amount, 0, 1);
        return System.Windows.Media.Color.FromRgb(
            (byte)(from.R + (to.R - from.R) * amount),
            (byte)(from.G + (to.G - from.G) * amount),
            (byte)(from.B + (to.B - from.B) * amount));
    }

    public async ValueTask DisposeAsync()
    {
        isDisposing = true;
        reconnectTimer.Stop();

        if (deviceManager is not null)
        {
            deviceManager.TemperatureRead -= OnTemperatureRead;
            deviceManager.CpuUsageRead -= OnCpuUsageRead;
            deviceManager.TemperatureSent -= OnTemperatureSent;
            deviceManager.TemperatureUnavailable -= OnTemperatureUnavailable;
            deviceManager.DevicesChanged -= OnDevicesChanged;
            deviceManager.Error -= OnManagerError;

            await deviceManager.DisposeAsync();
            deviceManager = null;
        }

        displayTimer.Stop();
    }

    private void ResetStatistics()
    {
        minimumTemperature = null;
        maximumTemperature = null;
        averageTemperature = 0;
        temperatureSamples = 0;
        OnPropertyChanged(nameof(MinimumTemperatureText));
        OnPropertyChanged(nameof(MaximumTemperatureText));
        OnPropertyChanged(nameof(AverageTemperatureText));
    }

    private static readonly System.Windows.Media.Brush ConnectedBrush = CreateBrush(System.Windows.Media.Color.FromRgb(83, 235, 151));
    private static readonly System.Windows.Media.Brush PausedBrush = CreateBrush(System.Windows.Media.Color.FromRgb(245, 213, 73));
    private static readonly System.Windows.Media.Brush FaultBrush = CreateBrush(System.Windows.Media.Color.FromRgb(255, 65, 75));
    private static readonly System.Windows.Media.Brush MutedBrush = CreateBrush(System.Windows.Media.Color.FromRgb(139, 158, 154));
    private static readonly System.Windows.Media.Brush DisconnectedBrush = CreateBrush(System.Windows.Media.Color.FromRgb(104, 116, 113));
    private static readonly System.Windows.Media.Brush ElevatedCpuUsageBrush = CreateBrush(System.Windows.Media.Color.FromRgb(245, 137, 54));
    private static readonly System.Windows.Media.Brush HighCpuUsageBrush = CreateBrush(System.Windows.Media.Color.FromRgb(255, 65, 75));

    private static System.Windows.Media.Brush CreateBrush(System.Windows.Media.Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private void SetNotice(string value)
    {
        if (notice == value)
            return;

        notice = value;
        OnPropertyChanged(nameof(FooterText));
        OnPropertyChanged(nameof(FooterBrush));
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}