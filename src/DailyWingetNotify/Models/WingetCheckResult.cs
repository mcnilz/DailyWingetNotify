namespace DailyWingetNotify.Models;

internal sealed record WingetCheckResult(
    IReadOnlyList<WingetUpdate> Updates,
    string RawOutput,
    string? ErrorMessage)
{
    public bool IsSuccess => ErrorMessage is null;
}

