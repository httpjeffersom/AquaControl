namespace WatercoolerTemp.Core.RiseModeAuraIce;

/// <summary>
/// Identidade de um display Rise Mode Aura Ice encontrado no Windows.
/// </summary>
public sealed record RiseModeAuraIceDeviceInfo(
    string DevicePath,
    string? ProductName,
    string? SerialNumber)
{
    public const string DisplayName = "RISE MODE AURA ICE";
}
