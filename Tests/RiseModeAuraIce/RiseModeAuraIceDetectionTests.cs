using WatercoolerTemp.Core.RiseModeAuraIce;
using Xunit;

namespace AquaControl.Tests;

public sealed class RiseModeAuraIceDetectionTests
{
    [Theory]
    [InlineData(0xAA88, 0x8666, 9, true)]
    [InlineData(0x1A86, 0x484A, 9, false)] // CH340 do Pichau Aqua
    [InlineData(0xAA88, 0x8666, 8, false)] // sem espaço para o Report ID
    [InlineData(0xAA88, 0x8666, 64, false)] // relatório diferente do confirmado
    [InlineData(0xAA88, 0x8667, 9, false)]
    [InlineData(0xAA87, 0x8666, 9, false)]
    public void IsSupportedDeviceExigeVidPidERelatorioDe9Bytes(
        int vendorId,
        int productId,
        int maxOutputReportLength,
        bool esperado)
    {
        Assert.Equal(
            esperado,
            RiseModeAuraIceDeviceDiscovery.IsSupportedDevice(
                vendorId,
                productId,
                maxOutputReportLength));
    }

    [Fact]
    public void NomeExibidoDiferenciaDoPichau()
    {
        Assert.Equal("RISE MODE AURA ICE", RiseModeAuraIceDeviceInfo.DisplayName);
    }
}
