using System.Diagnostics;

using System.IO;

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

    // Reenvio mesmo sem mudança de temperatura, para o display não entrar
    // em timeout e apagar quando a temperatura fica estável por seguidos ciclos.
    private static readonly TimeSpan SendHeartbeat = TimeSpan.FromSeconds(2);

    // Falhas transitórias de serial (ex.: reinstanciação do driver CH340) não
    // desmontam tudo: só depois de N tentativas seguidas o erro é fatal.
    private const int MaxConsecutiveSendFailures = 5;

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
            int? lastKnownTemperature = null;
            int? lastSentTemperature = null;
            TimeSpan? lastSentAt = null;
            int consecutiveSendFailures = 0;

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
                        // Leitura pontualmente indisponível: mantém o último
                        // valor válido para o display não ficar sem informação.
                        TemperatureUnavailable?.Invoke();
                    }
                    else
                    {
                        TemperatureRead?.Invoke(temperature.Value);
                        lastKnownTemperature = temperature.Value;
                    }
                }

                if (lastKnownTemperature is int target)
                {
                    bool temperatureChanged = target != lastSentTemperature;
                    bool heartbeatDue = lastSentAt is TimeSpan sentAt
                        && elapsed.Elapsed - sentAt >= SendHeartbeat;

                    if (temperatureChanged || heartbeatDue)
                    {
                        byte[] pacote = Aqua240XProtocol.MontarPacote(target);
                        try
                        {
                            serialClient.Send(pacote);
                            consecutiveSendFailures = 0;
                            lastSentTemperature = target;
                            lastSentAt = elapsed.Elapsed;
                            TemperatureSent?.Invoke(target, pacote);
                        }
                        catch (Exception exception)
                            when (exception is IOException or TimeoutException
                                or InvalidOperationException or UnauthorizedAccessException)
                        {
                            // Tenta de novo nos próximos ciclos; só vira erro
                            // fatal depois de todas as tentativas falharem.
                            consecutiveSendFailures++;
                            lastSentTemperature = null;
                            lastSentAt = null;
                            AppLogger.Error("Falha ao enviar pacote ao cooler", exception);
                            if (consecutiveSendFailures >= MaxConsecutiveSendFailures)
                                throw;
                        }
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