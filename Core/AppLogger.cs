using System.IO;

namespace WatercoolerTemp.Core;

public static class AppLogger
{
    private static readonly object SyncRoot = new();
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AquaControl",
        "aquacontrol.log");

    public static void Info(string message) => Write("INFO", message);

    public static void Error(string message, Exception? exception = null) =>
        Write("ERRO", exception is null ? message : $"{message}: {exception}");

    // Rotação simples: quando o log passa de 1 MB, vira .old.log.
    private const long MaxLogBytes = 1024 * 1024;

    private static void Write(string level, string message)
    {
        try
        {
            lock (SyncRoot)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
                RotateIfNeeded();
                File.AppendAllText(LogPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // Logging cannot interrupt hardware monitoring.
        }
    }

    private static void RotateIfNeeded()
    {
        var file = new FileInfo(LogPath);
        if (!file.Exists || file.Length < MaxLogBytes)
            return;

        string backupPath = Path.ChangeExtension(LogPath, ".old.log");
        if (File.Exists(backupPath))
            File.Delete(backupPath);
        File.Move(LogPath, backupPath);
    }
}