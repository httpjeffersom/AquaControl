namespace WatercoolerTemp.Core;

public sealed class TemperatureRamp
{
    private int? currentTemperature;

    public int AdvanceToward(int targetTemperature)
    {
        if (currentTemperature is null)
        {
            currentTemperature = targetTemperature;
        }
        else if (currentTemperature < targetTemperature)
        {
            currentTemperature++;
        }
        else if (currentTemperature > targetTemperature)
        {
            currentTemperature--;
        }

        return currentTemperature.Value;
    }
}