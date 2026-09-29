using System.Diagnostics;

namespace WatercoolerTemp.Core;

public sealed class WatercoolerMonitorService : IDisposable
{
    private readonly CpuTemperatureReader temperatureReader;
    private readonly Aqua240XSerialClient serialClient;
    private readonly TimeSpan interval;
    private readonly TemperatureRamp temperatureRamp = new();

    public WatercoolerMonitorService(
        CpuTemperatureReader temperatureReader,
        Aqua240XSerialClient serialClient,
        TimeSpan interval)
    {
        this.temperatureReader = temperatureReader;
        this.serialClient = serialClient;
        this.interval = interval;
    }

    public event Action<int, byte[]>? TemperatureSent;
    public event Action<int>? TemperatureRead;
    public event Action? TemperatureUnavailable;
    public event Action<Exception>? Error;

    public void Open()
    {
        temperatureReader.Open();
        serialClient.Open();
        AppLogger.Info("Monitoramento aberto.");
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var elapsed = Stopwatch.StartNew();
            TimeSpan nextReadAt = TimeSpan.Zero;
            int? targetTemperature = null;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (elapsed.Elapsed >= nextReadAt)
                {
                    int? temperature = temperatureReader.ReadTemperature();
                    nextReadAt = elapsed.Elapsed + interval;

                    if (temperature is null)
                    {
                        targetTemperature = null;
                        TemperatureUnavailable?.Invoke();
                    }
                    else
                    {
                        targetTemperature = temperature.Value;
                        TemperatureRead?.Invoke(temperature.Value);
                    }
                }

                if (targetTemperature is int target)
                {
                    int temperatureToSend = temperatureRamp.AdvanceToward(target);
                    byte[] pacote = Aqua240XProtocol.MontarPacote(temperatureToSend);
                    serialClient.Send(pacote);
                    TemperatureSent?.Invoke(temperatureToSend, pacote);
                }

                await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            AppLogger.Error("Erro no monitoramento", ex);
            Error?.Invoke(ex);
            throw;
        }
    }

    public void Dispose()
    {
        serialClient.Dispose();
        temperatureReader.Dispose();
    }
}