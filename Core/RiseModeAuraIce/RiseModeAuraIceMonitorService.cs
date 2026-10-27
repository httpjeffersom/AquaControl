using System.IO;

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

    // Falhas transitórias de HID não derrubam a sessão na primeira ocorrência.
    private const int MaxConsecutiveSendFailures = 5;

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
            int? lastKnownTemperature = null;
            int consecutiveSendFailures = 0;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                int? temperature = temperatureReader.ReadTemperature();
                CpuUsageRead?.Invoke(temperatureReader.CpuUsage);

                // Leitura pontualmente indisponível: mantém o último valor
                // válido para o display não ficar sem informação.
                if (temperature is int value)
                {
                    int clamped = Math.Clamp(value, 0, 255);
                    TemperatureRead?.Invoke(clamped);
                    lastKnownTemperature = clamped;
                }
                else
                {
                    TemperatureUnavailable?.Invoke();
                }

                if (lastKnownTemperature is int target)
                {
                    try
                    {
                        await SendBurstAsync(target, cancellationToken).ConfigureAwait(false);
                        TemperatureSent?.Invoke(target, RiseModeAuraIceProtocol.BuildHidReport(target));
                        consecutiveSendFailures = 0;
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception exception)
                        when (exception is IOException or TimeoutException
                            or InvalidOperationException or UnauthorizedAccessException)
                    {
                        // Tenta de novo nos próximos ciclos; só vira erro
                        // fatal depois de todas as tentativas falharem.
                        consecutiveSendFailures++;
                        AppLogger.Error("Falha ao enviar relatório ao display", exception);
                        if (consecutiveSendFailures >= MaxConsecutiveSendFailures)
                            throw;
                    }
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

    private async Task SendBurstAsync(int target, CancellationToken cancellationToken)
    {
        byte[] report = RiseModeAuraIceProtocol.BuildHidReport(target);
        for (int attempt = 0; attempt < RiseModeAuraIceProtocol.BurstCount; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            client.Send(report);

            if (attempt < RiseModeAuraIceProtocol.BurstCount - 1)
                await Task.Delay(RiseModeAuraIceProtocol.BurstDelayMs, cancellationToken);
        }
    }

    public void Dispose()
    {
        client.Dispose();
        if (ownsTemperatureReader)
            temperatureReader.Dispose();
    }
}
