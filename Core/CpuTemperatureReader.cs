using System.Linq;
using LibreHardwareMonitor.Hardware;

namespace WatercoolerTemp.Core;

public sealed class CpuTemperatureReader : IDisposable
{
    private readonly Computer computer = new()
    {
        IsCpuEnabled = true
    };

    public void Open()
    {
        computer.Open();
        AppLogger.Info("Leitor de temperatura aberto.");
    }

    public int? CpuUsage { get; private set; }

    public int? ReadTemperature()
    {
        foreach (IHardware hardware in computer.Hardware)
        {
            if (hardware.HardwareType != HardwareType.Cpu) continue;

            hardware.Update();
            CpuUsage = ReadCpuUsage(hardware);

            ISensor? sensorPackage = hardware.Sensors.FirstOrDefault(s =>
                s.SensorType == SensorType.Temperature && s.Name.Contains("Package"));

            if (sensorPackage?.Value is not null)
                return (int)Math.Round(sensorPackage.Value.Value);

            ISensor? qualquerSensor = hardware.Sensors.FirstOrDefault(s =>
                s.SensorType == SensorType.Temperature && s.Value is not null);

            if (qualquerSensor?.Value is not null)
                return (int)Math.Round(qualquerSensor.Value.Value);
        }

        return null;
    }

    private static int? ReadCpuUsage(IHardware hardware)
    {
        ISensor[] loadSensors = hardware.Sensors
            .Where(sensor => sensor.SensorType == SensorType.Load && sensor.Value is not null)
            .ToArray();

        ISensor? totalLoad = loadSensors.FirstOrDefault(sensor =>
            sensor.Name.Contains("Total", StringComparison.OrdinalIgnoreCase));

        if (totalLoad?.Value is float total)
            return (int)Math.Round(Math.Clamp(total, 0, 100));

        if (loadSensors.Length == 0)
            return null;

        double averageLoad = loadSensors.Average(sensor => sensor.Value!.Value);
        return (int)Math.Round(Math.Clamp(averageLoad, 0, 100));
    }

    public void Dispose()
    {
        computer.Close();
        AppLogger.Info("Leitor de temperatura fechado.");
    }
}