namespace WatercoolerTemp.Core.Devices;

/// <summary>
/// Adaptador fino para o suporte existente ao Pichau Aqua 240X e variantes.
/// Não reimplementa nem altera o protocolo serial: apenas expõe o
/// <see cref="WatercoolerMonitorService"/> já usado pelo programa na interface
/// comum do coordenador de dispositivos.
/// </summary>
public sealed class PichauAqua240XSession : IWatercoolerDeviceSession
{
    private readonly WatercoolerMonitorService service;
    private readonly string portName;

    public PichauAqua240XSession(
        string portName,
        CpuTemperatureReader sharedTemperatureReader,
        int intervalMs = 1000)
    {
        this.portName = portName;

        var client = new Aqua240XSerialClient(portName, 9600);
        service = new WatercoolerMonitorService(
            sharedTemperatureReader,
            client,
            TimeSpan.FromMilliseconds(intervalMs),
            ownsTemperatureReader: false);

        service.TemperatureSent += (temperature, packet) => TemperatureSent?.Invoke(temperature, packet);
        service.TemperatureRead += temperature => TemperatureRead?.Invoke(temperature);
        service.CpuUsageRead += usage => CpuUsageRead?.Invoke(usage);
        service.TemperatureUnavailable += () => TemperatureUnavailable?.Invoke();
        service.Error += exception => Error?.Invoke(exception);
    }

    public string DeviceKey => $"pichau-aqua-serial:{portName}";
    public string DisplayName => "PICHAU AQUA 240X";

    public event Action<int, byte[]>? TemperatureSent;
    public event Action<int>? TemperatureRead;
    public event Action<int?>? CpuUsageRead;
    public event Action? TemperatureUnavailable;
    public event Action<Exception>? Error;

    public void Open() => service.Open();

    public Task StartAsync(CancellationToken cancellationToken) => service.StartAsync(cancellationToken);

    public void Dispose() => service.Dispose();
}
