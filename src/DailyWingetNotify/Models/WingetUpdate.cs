namespace DailyWingetNotify.Models;

internal sealed record WingetUpdate(string Name, string Id, string CurrentVersion, string AvailableVersion, string Source);

