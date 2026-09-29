// Envia a temperatura da CPU para o display do watercooler Pichau Aqua 240X
// (chip WCH CH340, VID 0x1a86 / PID 0x484a), via porta serial virtual.
//
// Baseado na engenharia reversa do protocolo feita capturando o tráfego USB
// do software oficial com Wireshark + USBPcap.
//
// Estrutura do pacote identificada (payload, 23 bytes):
//     74 00 TT 08 26 XX YY ZZ 02 03 2e 02 02 02 02 02 02 02 02 02 02 30 1d
//              ^^
//              byte que carrega a temperatura da CPU em °C (confirmado)
//
// IMPORTANTE: os bytes fixos (08 26, XX YY ZZ, 02 03 2e, os 02 repetidos,
// 30 1d) foram copiados de capturas reais e podem codificar outros dados
// (RPM da bomba, checksum) que ainda não identificamos com certeza.
// Se o display se comportar de forma estranha, é sinal de que algum desses
// precisa ser calculado dinamicamente em vez de fixo.
//
// Requer rodar como Administrador (já configurado via app.manifest) e que
// o software oficial do watercooler esteja FECHADO (ele usa a porta serial sozinho).

using System;
using System.Threading;
using WatercoolerTemp.Core;

namespace WatercoolerTemp;

internal class Program
{
    // ==== CONFIGURAÇÃO ====
    private const int BaudRate = 9600;            // valor comum para CH340
    private const int IntervaloMs = 1000;         // de quanto em quanto tempo atualizar

    private static async Task Main()
    {
        Console.WriteLine("=== Aqua Control - Display do Pichau Aqua 240X ===\n");

        string portaCom;
        try
        {
            portaCom = Aqua240XPortDiscovery.FindPort();
            Console.WriteLine($"Watercooler encontrado na porta {portaCom}.\n");
        }
        catch (Exception exception)
        {
            Console.WriteLine($"ERRO ao localizar o watercooler: {exception.Message}");
            return;
        }

        using var temperatureReader = new CpuTemperatureReader();
        using var serialClient = new Aqua240XSerialClient(portaCom, BaudRate);
        using var service = new WatercoolerMonitorService(
            temperatureReader,
            serialClient,
            TimeSpan.FromMilliseconds(IntervaloMs));

        service.TemperatureUnavailable += () =>
            Console.WriteLine("Não consegui ler a temperatura da CPU. Tentando de novo...");
        service.TemperatureSent += (temperature, pacote) =>
            Console.WriteLine($"Enviado: {temperature}°C -> {BitConverter.ToString(pacote).Replace("-", " ")}");

        using var cancellationTokenSource = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellationTokenSource.Cancel();
        };

        try
        {
            service.Open();
            Console.WriteLine($"Porta {portaCom} aberta com sucesso.\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERRO ao abrir a porta {portaCom}: {ex.Message}");
            Console.WriteLine("Verifique se:");
            Console.WriteLine("  1. O software oficial do watercooler está FECHADO");
            Console.WriteLine("  2. O watercooler está conectado e o driver CH340 está instalado");
            Console.WriteLine("\nPressione qualquer tecla para sair...");
            Console.ReadKey();
            return;
        }

        Console.WriteLine("Leitura da CPU a cada 1 segundo; envio ao display a cada 500 ms. Pressione Ctrl+C para parar.\n");

        try
        {
            await service.StartAsync(cancellationTokenSource.Token);
        }
        catch (OperationCanceledException) when (cancellationTokenSource.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\nErro durante o envio: {ex.Message}");
        }
    }
}
