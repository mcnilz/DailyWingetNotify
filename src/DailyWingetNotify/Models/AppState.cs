namespace DailyWingetNotify.Models;

internal sealed record AppState(
    DateOnly? LastCheckedLogicalDay,
    PendingNotificationState? PendingNotification = null);

internal sealed record PendingNotificationState(
    DateOnly LogicalDay,
    WingetCheckResult Result);
