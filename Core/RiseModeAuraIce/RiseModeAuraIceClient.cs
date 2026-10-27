using HidSharp;

namespace WatercoolerTemp.Core.RiseModeAuraIce;

/// <summary>
/// Comunicação HID nativa com o display Rise Mode Aura Ice.
///
/// Não usa WinUSB, libusb, Zadig nem driver de kernel: o Windows já expõe
/// esse dispositivo como HID e o HidSharp conversa com a API hid.dll.
/// </summary>
public sealed class RiseModeAuraIceClient : IDisposable
{
    private readonly string devicePath;
    private HidStream? stream;

    public RiseModeAuraIceClient(string devicePath)
    {
        if (string.IsNullOrWhiteSpace(devicePath))
            throw new ArgumentException("Caminho do dispositivo HID não informado.", nameof(devicePath));

        this.devicePath = devicePath;
    }

    public string DevicePath => devicePath;

    public bool IsOpen => stream is not null;

    public void Open()
    {
        HidDevice? device = DeviceList.Local
            .GetHidDevices(RiseModeAuraIceProtocol.VendorId, RiseModeAuraIceProtocol.ProductId)
            .FirstOrDefault(candidate => string.Equals(
                candidate.DevicePath,
                devicePath,
                StringComparison.OrdinalIgnoreCase));

        if (device is null)
            throw new InvalidOperationException(
                "Display Rise Mode Aura Ice não encontrado. Verifique a conexão USB.");

        int outputReportLength = device.GetMaxOutputReportLength();
        if (!RiseModeAuraIceDeviceDiscovery.IsSupportedDevice(
                device.VendorID,
                device.ProductID,
                outputReportLength))
        {
            throw new InvalidOperationException(
                "O HID encontrado não expõe o relatório de saída de 9 bytes " +
                $"esperado pelo protocolo ({outputReportLength} bytes).");
        }

        if (!device.TryOpen(out HidStream? opened) || opened is null)
            throw new InvalidOperationException(
                "Não foi possível abrir o display Rise Mode Aura Ice. " +
                "Feche o software oficial do water cooler e tente novamente.");

        stream = opened;
        stream.WriteTimeout = 1000;
        AppLogger.Info($"Display Rise Mode Aura Ice aberto ({devicePath}).");
    }

    public void Send(byte[] report)
    {
        HidStream? current = stream;
        if (current is null)
            throw new InvalidOperationException("O display Rise Mode Aura Ice não está aberto.");

        current.Write(report);
    }

    public void Dispose()
    {
        HidStream? current = stream;
        stream = null;
        if (current is null)
            return;

        try
        {
            current.Dispose();
        }
        catch (Exception exception)
        {
            AppLogger.Error("Falha ao fechar o display Rise Mode Aura Ice", exception);
        }

        AppLogger.Info($"Display Rise Mode Aura Ice fechado ({devicePath}).");
    }
}
