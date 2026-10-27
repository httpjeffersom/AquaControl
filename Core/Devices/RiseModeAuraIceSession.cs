using WatercoolerTemp.Core.RiseModeAuraIce;

namespace WatercoolerTemp.Core.Devices;

/// <summary>
/// Sessão independente do display Rise Mode Aura Ice.
/// </summary>
public sealed class RiseModeAuraIceSession : IWatercoolerDeviceSession
{
    private readonly RiseModeAuraIceMonitorService service;
    private readonly RiseModeAuraIceDeviceInfo device;

    public RiseModeAuraIceSession(
        RiseModeAuraIceDeviceInfo device,
        CpuTemperatureReader sharedTemperatureReader,
        int intervalMs = 1000)
    {
        this.device = device;

        var client = new RiseModeAuraIceClient(device.DevicePath);
        service = new RiseModeAuraIceMonitorService(
            sharedTemperatureReader,
            client,
            TimeSpan.FromMilliseconds(intervalMs),
            ownsTemperatureReader: false);

        service.TemperatureSent += (temperature, report) => TemperatureSent?.Invoke(temperature, report);
        service.TemperatureRead += temperature => TemperatureRead?.Invoke(temperature);
        service.CpuUsageRead += usage => CpuUsageRead?.Invoke(usage);
        service.TemperatureUnavailable += () => TemperatureUnavailable?.Invoke();
        service.Error += exception => Error?.Invoke(exception);
    }

    public string DeviceKey => $"rise-mode-aura-ice:{device.DevicePath}";
    public string DisplayName => RiseModeAuraIceDeviceInfo.DisplayName;

    public event Action<int, byte[]>? TemperatureSent;
    public event Action<int>? TemperatureRead;
    public event Action<int?>? CpuUsageRead;
    public event Action? TemperatureUnavailable;
    public event Action<Exception>? Error;

    public void Open() => service.Open();

    public Task StartAsync(CancellationToken cancellationToken) => service.StartAsync(cancellationToken);

    public void Dispose() => service.Dispose();
}
