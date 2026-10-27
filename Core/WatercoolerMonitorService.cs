using System.Diagnostics;

namespace WatercoolerTemp.Core;

public sealed class WatercoolerMonitorService : IDisposable
{
    private readonly CpuTemperatureReader temperatureReader;
    private readonly Aqua240XSerialClient serialClient;
    private readonly TimeSpan interval;
    private readonly bool ownsTemperatureReader;

    public WatercoolerMonitorService(
        CpuTemperatureReader temperatureReader,
        Aqua240XSerialClient serialClient,
        TimeSpan interval)
        : this(temperatureReader, serialClient, interval, ownsTemperatureReader: true)
    {
    }

    /// <summary>
    /// Permite compartilhar um único leitor de CPU entre os drivers quando
    /// mais de um water cooler está conectado. O construtor acima mantém o
    /// comportamento original (o serviço é dono e fecha o leitor).
    /// </summary>
    public WatercoolerMonitorService(
        CpuTemperatureReader temperatureReader,
        Aqua240XSerialClient serialClient,
        TimeSpan interval,
        bool ownsTemperatureReader)
    {
        this.temperatureReader = temperatureReader;
        this.serialClient = serialClient;
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
            int? lastSentTemperature = null;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (elapsed.Elapsed >= nextReadAt)
                {
                    int? temperature = temperatureReader.ReadTemperature();
                    CpuUsageRead?.Invoke(temperatureReader.CpuUsage);
                    nextReadAt = elapsed.Elapsed + interval;

                    if (temperature is null)
                    {
                        targetTemperature = null;
                        lastSentTemperature = null;
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
                    // Envia apenas quando a temperatura muda (ou religa o sensor),
                    // evitando pacotes redundantes na serial.
                    if (target != lastSentTemperature)
                    {
                        byte[] pacote = Aqua240XProtocol.MontarPacote(target);
                        serialClient.Send(pacote);
                        lastSentTemperature = target;
                        TemperatureSent?.Invoke(target, pacote);
                    }
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
        if (ownsTemperatureReader)
            temperatureReader.Dispose();
    }
}