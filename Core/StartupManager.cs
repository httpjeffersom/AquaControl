using Microsoft.Win32;
using System.Diagnostics;
using System.IO;

namespace WatercoolerTemp.Core;

public static class StartupManager
{
    private const string LegacyRunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string LegacyValueName = "WatercoolerTemp";
    private const string TaskName = "AquaControl";
    private const string StartupArgument = "--startup";

    public static bool IsEnabled => RunSchtasks("/Query", "/TN", TaskName).ExitCode == 0;

    public static void SetEnabled(bool enabled)
    {
        if (!enabled)
        {
            if (IsEnabled)
                EnsureSuccess(RunSchtasks("/Delete", "/TN", TaskName, "/F"));
            RemoveLegacyRunEntry();
            return;
        }

        string executablePath = Environment.ProcessPath
            ?? Process.GetCurrentProcess().MainModule?.FileName
            ?? throw new InvalidOperationException("Não foi possível localizar o executável da aplicação.");
        string taskAction = $"\"{executablePath}\" {StartupArgument}";

        EnsureSuccess(RunSchtasks(
            "/Create",
            "/TN", TaskName,
            "/SC", "ONLOGON",
            "/TR", taskAction,
            "/RL", "HIGHEST",
            "/IT",
            "/F"));
        RemoveLegacyRunEntry();
    }

    public static bool IsStartupLaunch(string[] args) =>
        args.Any(argument => string.Equals(argument, StartupArgument, StringComparison.OrdinalIgnoreCase));

    private static (int ExitCode, string Output) RunSchtasks(params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = Path.Combine(Environment.SystemDirectory, "schtasks.exe"),
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        foreach (string argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Não foi possível iniciar o Agendador de Tarefas do Windows.");
        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
        Task<string> standardError = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        Task.WaitAll(standardOutput, standardError);

        string output = string.Join(
            Environment.NewLine,
            new[] { standardOutput.Result, standardError.Result }.Where(value => !string.IsNullOrWhiteSpace(value)));
        return (process.ExitCode, output);
    }

    private static void EnsureSuccess((int ExitCode, string Output) result)
    {
        if (result.ExitCode != 0)
        {
            string detail = string.IsNullOrWhiteSpace(result.Output)
                ? "O Windows não conseguiu configurar a tarefa de inicialização."
                : result.Output.Trim();
            throw new InvalidOperationException(detail);
        }
    }

    private static void RemoveLegacyRunEntry()
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(LegacyRunKey, writable: true);
        key?.DeleteValue(LegacyValueName, throwOnMissingValue: false);
    }
}