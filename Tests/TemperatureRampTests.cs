using WatercoolerTemp.Core;
using Xunit;

namespace AquaControl.Tests;

public sealed class TemperatureRampTests
{
    [Fact]
    public void AdvanceTowardDesceUmGrauPorChamada()
    {
        var ramp = new TemperatureRamp();

        Assert.Equal(50, ramp.AdvanceToward(50));
        Assert.Equal(49, ramp.AdvanceToward(45));
        Assert.Equal(48, ramp.AdvanceToward(45));
        Assert.Equal(47, ramp.AdvanceToward(45));
        Assert.Equal(46, ramp.AdvanceToward(45));
        Assert.Equal(45, ramp.AdvanceToward(45));
    }

    [Fact]
    public void AdvanceTowardSobeUmGrauPorChamada()
    {
        var ramp = new TemperatureRamp();

        Assert.Equal(45, ramp.AdvanceToward(45));
        Assert.Equal(46, ramp.AdvanceToward(50));
        Assert.Equal(47, ramp.AdvanceToward(50));
        Assert.Equal(48, ramp.AdvanceToward(50));
        Assert.Equal(49, ramp.AdvanceToward(50));
        Assert.Equal(50, ramp.AdvanceToward(50));
    }
}