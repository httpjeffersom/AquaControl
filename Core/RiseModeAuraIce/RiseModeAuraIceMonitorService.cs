namespace WatercoolerTemp.Core.RiseModeAuraIce;

/// <summary>
/// Loop de leitura da CPU e envio da temperatura ao display Rise Mode Aura Ice.
///
/// A leitura da CPU usa o mesmo <see cref="CpuTemperatureReader"/> do suporte
/// existente ao Pichau. O protocolo e o HID ficam isolados neste módulo.
/// </summary>
public sealed class RiseModeAuraIceMonitorService : IDisposable
{
    private readonly CpuTemperatureReader temperatureReader;
    private readonly RiseModeAuraIceClient client;
    private readonly TimeSpan interval;
    private readonly bool ownsTemperatureReader;

    public RiseModeAuraIceMonitorService(
        CpuTemperatureReader temperatureReader,
        RiseModeAuraIceClient client,
        TimeSpan interval,
        bool ownsTemperatureReader = true)
    {
        this.temperatureReader = temperatureReader;
        this.client = client;
        this.interval = interval;
        this.ownsTemperatureReader = ownsTemperatureReader;
    }

    public event Action<int, byte[]>? TemperatureSent;
    public event Action<int>? TemperatureRead;
    public event Action<int?>? CpuUsageRead;
    public event Action? TemperatureUnavailable;
    public event Action<Exception>? Error;

    public void Open()
    {
        temperatureReader.Open();
        client.Open();
        AppLogger.Info("Monitoramento Rise Mode Aura Ice aberto.");
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                int? temperature = temperatureReader.ReadTemperature();
                CpuUsageRead?.Invoke(temperatureReader.CpuUsage);

                if (temperature is null)
                {
                    TemperatureUnavailable?.Invoke();
                }
                else
                {
                    int target = Math.Clamp(temperature.Value, 0, 255);
                    TemperatureRead?.Invoke(target);

                    byte[] report = RiseModeAuraIceProtocol.BuildHidReport(target);
                    for (int attempt = 0; attempt < RiseModeAuraIceProtocol.BurstCount; attempt++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        client.Send(report);

                        if (attempt < RiseModeAuraIceProtocol.BurstCount - 1)
                            await Task.Delay(RiseModeAuraIceProtocol.BurstDelayMs, cancellationToken);
                    }

                    TemperatureSent?.Invoke(target, report);
                }

                await Task.Delay(interval, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            AppLogger.Error("Erro no monitoramento Rise Mode Aura Ice", exception);
            Error?.Invoke(exception);
            throw;
        }
    }

    public void Dispose()
    {
        client.Dispose();
        if (ownsTemperatureReader)
            temperatureReader.Dispose();
    }
}
