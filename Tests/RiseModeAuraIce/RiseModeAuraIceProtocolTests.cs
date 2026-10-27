using WatercoolerTemp.Core.RiseModeAuraIce;
using Xunit;

namespace AquaControl.Tests;

public sealed class RiseModeAuraIceProtocolTests
{
    [Fact]
    public void BuildDisplayPayloadSegueFormatoConfirmado()
    {
        byte[] payload = RiseModeAuraIceProtocol.BuildDisplayPayload(55);

        Assert.Equal(8, payload.Length);
        Assert.Equal(new byte[]
        {
            55, 0x1C, 0x00, 0x00, 0x1C, 0x00, 0x00, 0x00,
        }, payload);
    }

    [Fact]
    public void BuildHidReportAdicionaReportIdZero()
    {
        byte[] report = RiseModeAuraIceProtocol.BuildHidReport(72);

        Assert.Equal(9, report.Length);
        Assert.Equal(0x00, report[0]);
        Assert.Equal(72, report[1]);
        Assert.Equal(0x1C, report[2]);
        Assert.Equal(0x1C, report[5]);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(255)]
    public void BuildHidReportAceitaLimitesDoByte(int temperatura)
    {
        byte[] report = RiseModeAuraIceProtocol.BuildHidReport(temperatura);

        Assert.Equal(temperatura, report[1]);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(256)]
    public void BuildHidReportRejeitaTemperaturaForaDoByte(int temperatura)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => RiseModeAuraIceProtocol.BuildHidReport(temperatura));
    }

    [Fact]
    public void ConstantesDoDispositivoCorrespondemAoModeloSuportado()
    {
        Assert.Equal(0xAA88, RiseModeAuraIceProtocol.VendorId);
        Assert.Equal(0x8666, RiseModeAuraIceProtocol.ProductId);
        Assert.Equal(9, RiseModeAuraIceProtocol.HidReportLength);
        Assert.Equal(1000, RiseModeAuraIceProtocol.UpdateIntervalMs);
    }
}
