using Microsoft.Win32;

namespace DailyWingetNotify.Services;

internal sealed class AutostartService(string executablePath)
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "DailyWingetNotify";

    public bool IsInstalled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return string.Equals(key?.GetValue(ValueName) as string, Quote(executablePath), StringComparison.OrdinalIgnoreCase);
    }

    public void Install()
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
        key.SetValue(ValueName, Quote(executablePath), RegistryValueKind.String);
    }

    public static void Remove()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        key?.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    private static string Quote(string value) => $"\"{value}\"";
}
