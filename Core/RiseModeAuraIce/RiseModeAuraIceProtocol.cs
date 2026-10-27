namespace WatercoolerTemp.Core.RiseModeAuraIce;

/// <summary>
/// Protocolo do display do Rise Mode Aura Ice.
///
/// Referência de engenharia reversa:
/// https://github.com/bmortella/wc-aura-ice-linux
///
/// O display recebe um relatório HID de saída com 8 bytes de payload:
///     [TEMP, 0x1C, 0x00, 0x00, 0x1C, 0x00, 0x00, 0x00]
/// onde TEMP é a temperatura da CPU em °C (0-255). Os demais bytes foram
/// observados nas capturas e não têm função confirmada; não os alteramos.
///
/// No Windows a API HID inclui o Report ID como primeiro byte do buffer
/// enviado ao driver. O dispositivo usa Report ID 0, então o buffer completo
/// tem 9 bytes. O projeto Windows de referência confirma esse formato:
/// https://github.com/rafaelwms/win-watercooler-display
/// </summary>
public static class RiseModeAuraIceProtocol
{
    public const int VendorId = 0xAA88;
    public const int ProductId = 0x8666;

    public const byte ReportId = 0x00;
    public const int DisplayPayloadLength = 8;
    public const int HidReportLength = 9;

    /// <summary>Intervalo de atualização confirmado no projeto de referência.</summary>
    public const int UpdateIntervalMs = 1000;

    /// <summary>
    /// O display faz uma transição suave de temperatura. A referência envia o
    /// mesmo valor 5 vezes, com 50 ms entre os envios, para a atualização ficar
    /// mais rápida.
    /// </summary>
    public const int BurstCount = 5;
    public const int BurstDelayMs = 50;

    /// <summary>
    /// Monta o payload de 8 bytes aceito pelo display.
    /// </summary>
    public static byte[] BuildDisplayPayload(int temperatureCelsius)
    {
        if (temperatureCelsius is < 0 or > 255)
            throw new ArgumentOutOfRangeException(nameof(temperatureCelsius));

        return new byte[]
        {
            (byte)temperatureCelsius,
            0x1C,
            0x00,
            0x00,
            0x1C,
            0x00,
            0x00,
            0x00,
        };
    }

    /// <summary>
    /// Monta o buffer de 9 bytes usado pela API HID do Windows: Report ID 0
    /// seguido do payload de 8 bytes.
    /// </summary>
    public static byte[] BuildHidReport(int temperatureCelsius)
    {
        byte[] payload = BuildDisplayPayload(temperatureCelsius);
        var report = new byte[HidReportLength];
        report[0] = ReportId;
        Array.Copy(payload, 0, report, 1, payload.Length);
        return report;
    }
}
