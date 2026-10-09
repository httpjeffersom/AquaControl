using System.Management;

namespace WatercoolerTemp.Core;

public static class Aqua240XPortDiscovery
{
    private const string DeviceId = "VID_1A86&PID_484A";

    public static string FindPort()
    {
        using var searcher = new ManagementObjectSearcher(
            "SELECT DeviceID, PNPDeviceID FROM Win32_SerialPort");
        using ManagementObjectCollection devices = searcher.Get();

        var ports = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (ManagementObject device in devices)
        {
            using (device)
            {
                string? pnpDeviceId = device["PNPDeviceID"] as string;
                string? portName = device["DeviceID"] as string;
                if (pnpDeviceId?.Contains(DeviceId, StringComparison.OrdinalIgnoreCase) == true
                    && portName?.StartsWith("COM", StringComparison.OrdinalIgnoreCase) == true)
                {
                    ports.Add(portName);
                }
            }
        }

        if (ports.Count == 0)
            throw new InvalidOperationException(
                "Watercooler Pichau Aqua não encontrado. Verifique a conexão USB e o driver CH340.");

        if (ports.Count > 1)
            throw new InvalidOperationException(
                $"Mais de uma porta serial compatível encontrada: {string.Join(", ", ports)}. " +
                "Deixe conectado apenas o watercooler que deseja usar.");

        return ports.Single();
    }
}