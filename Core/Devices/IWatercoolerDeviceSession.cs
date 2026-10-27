namespace WatercoolerTemp.Core.Devices;

/// <summary>
/// Interface comum usada pelo coordenador para conversar com qualquer
/// water cooler suportado. Cada driver mantém sua própria implementação de
/// transporte e protocolo.
/// </summary>
public interface IWatercoolerDeviceSession : IDisposable
{
    string DeviceKey { get; }
    string DisplayName { get; }

    event Action<int, byte[]>? TemperatureSent;
    event Action<int>? TemperatureRead;
    event Action<int?>? CpuUsageRead;
    event Action? TemperatureUnavailable;
    event Action<Exception>? Error;

    void Open();
    Task StartAsync(CancellationToken cancellationToken);
}
