using DailyWingetNotify.Models;

namespace DailyWingetNotify.Services;

internal sealed class PendingNotificationService : IDisposable
{
    private static readonly TimeSpan RetryInterval = TimeSpan.FromMinutes(5);
    private readonly StateStore _stateStore;
    private readonly UserPresenceService _userPresenceService;
    private readonly Action<WingetCheckResult> _showNotification;
    private readonly Timer _timer;
    private readonly Lock _stateLock = new();
    private PendingNotificationState? _pendingNotification;
    private CancellationToken _cancellationToken;
    private bool _disposed;

    public PendingNotificationService(
        StateStore stateStore,
        UserPresenceService userPresenceService,
        Action<WingetCheckResult> showNotification)
    {
        ArgumentNullException.ThrowIfNull(stateStore);
        ArgumentNullException.ThrowIfNull(userPresenceService);
        ArgumentNullException.ThrowIfNull(showNotification);

        _stateStore = stateStore;
        _userPresenceService = userPresenceService;
        _showNotification = showNotification;
        _timer = new Timer(TryShowPendingNotification);
    }

    public void Start(CancellationToken cancellationToken)
    {
        var pendingNotification = _stateStore.Load().PendingNotification;
        if (pendingNotification is null)
        {
            return;
        }

        lock (_stateLock)
        {
            if (_disposed)
            {
                return;
            }

            _cancellationToken = cancellationToken;
            _pendingNotification = pendingNotification;
        }

        ScheduleRetry(TimeSpan.Zero);
    }

    public async Task ShowOrDeferAsync(
        WingetCheckResult result,
        DateOnly logicalDay,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (IsDisposed())
        {
            return;
        }

        if (UserPresenceService.IsUserPresent())
        {
            await ClearPendingNotificationAsync(cancellationToken).ConfigureAwait(false);
            if (!IsDisposed())
            {
                _showNotification(result);
            }

            return;
        }

        var pendingNotification = new PendingNotificationState(logicalDay, result);
        lock (_stateLock)
        {
            if (_disposed)
            {
                return;
            }

            _cancellationToken = cancellationToken;
            _pendingNotification = pendingNotification;
        }

        await _stateStore.SavePendingNotificationAsync(pendingNotification, cancellationToken).ConfigureAwait(false);
        ScheduleRetry(RetryInterval);
    }

    public async Task ShowImmediatelyAsync(WingetCheckResult result, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (IsDisposed())
        {
            return;
        }

        await ClearPendingNotificationAsync(cancellationToken).ConfigureAwait(false);
        if (!IsDisposed())
        {
            _showNotification(result);
        }
    }

    public void Dispose()
    {
        lock (_stateLock)
        {
            _disposed = true;
            _pendingNotification = null;
        }

        _timer.Dispose();
    }

    private async void TryShowPendingNotification(object? state)
    {
        try
        {
            await TryShowPendingNotificationAsync(_cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            ScheduleRetry(RetryInterval);
        }
    }

    private async Task TryShowPendingNotificationAsync(CancellationToken cancellationToken)
    {
        StopRetry();

        var pendingNotification = GetPendingNotification();
        if (pendingNotification is null)
        {
            return;
        }

        if (!UserPresenceService.IsUserPresent())
        {
            ScheduleRetry(RetryInterval);
            return;
        }

        await ClearPendingNotificationAsync(cancellationToken).ConfigureAwait(false);
        if (!IsDisposed())
        {
            _showNotification(pendingNotification.Result);
        }
    }

    private bool IsDisposed()
    {
        lock (_stateLock)
        {
            return _disposed;
        }
    }

    private PendingNotificationState? GetPendingNotification()
    {
        lock (_stateLock)
        {
            if (_disposed)
            {
                return null;
            }

            return _pendingNotification;
        }
    }

    private async Task ClearPendingNotificationAsync(CancellationToken cancellationToken)
    {
        lock (_stateLock)
        {
            if (_disposed)
            {
                return;
            }

            _pendingNotification = null;
        }

        StopRetry();
        await _stateStore.SavePendingNotificationAsync(null, cancellationToken).ConfigureAwait(false);
    }

    private void StopRetry()
    {
        ChangeTimer(Timeout.InfiniteTimeSpan);
    }

    private void ScheduleRetry(TimeSpan dueTime)
    {
        if (IsDisposed())
        {
            return;
        }

        ChangeTimer(dueTime);
    }

    private void ChangeTimer(TimeSpan dueTime)
    {
        try
        {
            _timer.Change(dueTime, Timeout.InfiniteTimeSpan);
        }
        catch (ObjectDisposedException)
        {
        }
    }
}
