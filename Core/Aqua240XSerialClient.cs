using System.IO.Ports;

namespace WatercoolerTemp.Core;

public sealed class Aqua240XSerialClient : IDisposable
{
    private readonly SerialPort porta;

    public Aqua240XSerialClient(string portaCom, int baudRate = 9600)
    {
        porta = new SerialPort(portaCom, baudRate)
        {
            WriteTimeout = 1000
        };
    }

    public string PortName => porta.PortName;

    public bool IsOpen => porta.IsOpen;

    public void Open()
    {
        porta.Open();
        AppLogger.Info($"Porta {PortName} aberta.");
    }

    public void Send(byte[] pacote)
    {
        porta.Write(pacote, 0, pacote.Length);
    }

    public void Dispose()
    {
        if (porta.IsOpen)
        {
            porta.Close();
            AppLogger.Info($"Porta {PortName} fechada.");
        }

        porta.Dispose();
    }
}