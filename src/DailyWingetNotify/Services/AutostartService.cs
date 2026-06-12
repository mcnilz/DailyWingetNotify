using Microsoft.Win32;

namespace DailyWingetNotify.Services;

internal sealed class AutostartService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "DailyWingetNotify";
    private readonly string _executablePath;

    public AutostartService(string executablePath)
    {
        _executablePath = executablePath;
    }

    public bool IsInstalled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return string.Equals(key?.GetValue(ValueName) as string, Quote(_executablePath), StringComparison.OrdinalIgnoreCase);
    }

    public void Install()
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
        key.SetValue(ValueName, Quote(_executablePath), RegistryValueKind.String);
    }

    public void Remove()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        key?.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    private static string Quote(string value) => $"\"{value}\"";
}

