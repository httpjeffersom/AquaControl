using System.Linq;
using LibreHardwareMonitor.Hardware;

namespace WatercoolerTemp.Core;

public sealed class CpuTemperatureReader : IDisposable
{
    private readonly Computer computer = new()
    {
        IsCpuEnabled = true
    };
    private readonly object syncRoot = new();
    private int? cpuUsage;

    public void Open()
    {
        lock (syncRoot)
        {
            computer.Open();
        }

        AppLogger.Info("Leitor de temperatura aberto.");
    }

    public int? CpuUsage
    {
        get
        {
            lock (syncRoot)
                return cpuUsage;
        }
    }

    public int? ReadTemperature()
    {
        // O mesmo leitor é compartilhado entre os drivers quando os dois
        // water coolers estão conectados; a leitura precisa ser serializada.
        lock (syncRoot)
        {
            foreach (IHardware hardware in computer.Hardware)
            {
                if (hardware.HardwareType != HardwareType.Cpu) continue;

                hardware.Update();
                cpuUsage = ReadCpuUsage(hardware);

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
        lock (syncRoot)
        {
            computer.Close();
        }

        AppLogger.Info("Leitor de temperatura fechado.");
    }
}