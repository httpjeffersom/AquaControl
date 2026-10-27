using HidSharp;

namespace WatercoolerTemp.Core.RiseModeAuraIce;

/// <summary>
/// Detecção do display HID Rise Mode Aura Ice (VID 0xAA88 / PID 0x8666).
///
/// Esses identificadores são usados por vários water coolers com o mesmo
/// controlador de display (por exemplo, modelos revendidos por Mancer). Por
/// isso a detecção não usa o nome comercial: usa VID/PID e valida que o HID
/// expõe um relatório de saída compatível com os 9 bytes do protocolo.
/// </summary>
public static class RiseModeAuraIceDeviceDiscovery
{
    public static bool IsSupportedDevice(int vendorId, int productId, int maxOutputReportLength)
    {
        return vendorId == RiseModeAuraIceProtocol.VendorId
            && productId == RiseModeAuraIceProtocol.ProductId
            && maxOutputReportLength == RiseModeAuraIceProtocol.HidReportLength;
    }

    /// <summary>
    /// Lista os displays presentes. Ausência de dispositivo retorna uma lista
    /// vazia; falhas de enumeração são registradas e também retornam vazio para
    /// não interromper o restante do programa.
    /// </summary>
    public static IReadOnlyList<RiseModeAuraIceDeviceInfo> FindDevices()
    {
        var found = new List<RiseModeAuraIceDeviceInfo>();
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            foreach (HidDevice device in DeviceList.Local.GetHidDevices(
                         RiseModeAuraIceProtocol.VendorId,
                         RiseModeAuraIceProtocol.ProductId))
            {
                int outputReportLength = device.GetMaxOutputReportLength();
                if (!IsSupportedDevice(device.VendorID, device.ProductID, outputReportLength))
                {
                    AppLogger.Info(
                        $"HID Rise Mode ignorado: saída de {outputReportLength} bytes " +
                        $"(esperado {RiseModeAuraIceProtocol.HidReportLength}).");
                    continue;
                }

                string path = device.DevicePath;
                if (string.IsNullOrWhiteSpace(path) || !paths.Add(path))
                    continue;

                found.Add(new RiseModeAuraIceDeviceInfo(
                    path,
                    SafeGet(device.GetProductName),
                    SafeGet(device.GetSerialNumber)));
            }
        }
        catch (Exception exception)
        {
            AppLogger.Error("Falha ao enumerar dispositivos HID Rise Mode", exception);
        }

        return found;
    }

    private static string? SafeGet(Func<string> getter)
    {
        try
        {
            return getter();
        }
        catch
        {
            return null;
        }
    }
}
