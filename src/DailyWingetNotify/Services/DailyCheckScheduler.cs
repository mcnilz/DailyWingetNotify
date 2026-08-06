namespace DailyWingetNotify.Services;

internal sealed class DailyCheckScheduler : IDisposable
{
    private static readonly TimeSpan DayBoundary = TimeSpan.FromHours(3);
    private readonly StateStore _stateStore;
    private readonly SystemLoadService _systemLoadService;
    private readonly Timer _timer;
    private readonly Lock _checkLock = new();
    private Func<DateOnly, CancellationToken, Task>? _checkCallback;
    private CancellationToken _cancellationToken;
    private bool _deferInitialCheckUntilLowCpuUsage;
    private bool _initialCheckDelayHandled;
    private bool _isRunning;

    public DailyCheckScheduler(
        StateStore stateStore,
        SystemLoadService systemLoadService)
    {
        ArgumentNullException.ThrowIfNull(stateStore);
        ArgumentNullException.ThrowIfNull(systemLoadService);

        _stateStore = stateStore;
        _systemLoadService = systemLoadService;
        _timer = new Timer(OnTimerTick);
    }

    public void Start(
        Func<DateOnly, CancellationToken, Task> checkCallback,
        bool deferInitialCheckUntilLowCpuUsage,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(checkCallback);

        _checkCallback = checkCallback;
        _deferInitialCheckUntilLowCpuUsage = deferInitialCheckUntilLowCpuUsage;
        _cancellationToken = cancellationToken;
        ScheduleNextTick(runSoon: true);
    }

    public async Task RunManualCheckAsync(CancellationToken cancellationToken)
    {
        await RunCheckAndSaveLogicalDayAsync(GetLogicalDay(DateTimeOffset.Now), cancellationToken).ConfigureAwait(false);
    }

    public void Dispose()
    {
        _timer.Dispose();
    }

    private async void OnTimerTick(object? state)
    {
        _timer.Change(Timeout.Infinite, Timeout.Infinite);
        try
        {
            await RunDueCheckAsync(_cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (!_cancellationToken.IsCancellationRequested)
            {
                ScheduleNextTick(runSoon: false);
            }
        }
    }

    private async Task WaitForInitialCheckSlotAsync(CancellationToken cancellationToken)
    {
        if (_initialCheckDelayHandled)
        {
            return;
        }

        _initialCheckDelayHandled = true;
        if (_deferInitialCheckUntilLowCpuUsage)
        {
            await SystemLoadService.WaitForLowCpuUsageAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task RunDueCheckAsync(CancellationToken cancellationToken)
    {
        var logicalDay = GetLogicalDay(DateTimeOffset.Now);
        if (!ShouldRunDailyCheck(logicalDay))
        {
            return;
        }

        await WaitForInitialCheckSlotAsync(cancellationToken).ConfigureAwait(false);

        logicalDay = GetLogicalDay(DateTimeOffset.Now);
        if (ShouldRunDailyCheck(logicalDay))
        {
            await RunCheckAndSaveLogicalDayAsync(logicalDay, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task RunCheckAndSaveLogicalDayAsync(DateOnly logicalDay, CancellationToken cancellationToken)
    {
        if (_checkCallback is null || !TryBeginCheck())
        {
            return;
        }

        try
        {
            await _checkCallback(logicalDay, cancellationToken).ConfigureAwait(false);
            await _stateStore.SaveCheckedLogicalDayAsync(logicalDay, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            EndCheck();
        }
    }

    private bool TryBeginCheck()
    {
        lock (_checkLock)
        {
            if (_isRunning)
            {
                return false;
            }

            _isRunning = true;
            return true;
        }
    }

    private void EndCheck()
    {
        lock (_checkLock)
        {
            _isRunning = false;
        }
    }

    private bool ShouldRunDailyCheck(DateOnly logicalDay)
    {
        var state = _stateStore.Load();
        return state.LastCheckedLogicalDay != logicalDay;
    }

    private static DateOnly GetLogicalDay(DateTimeOffset now)
    {
        var local = now.LocalDateTime;
        if (local.TimeOfDay < DayBoundary)
        {
            local = local.AddDays(-1);
        }

        return DateOnly.FromDateTime(local);
    }

    private void ScheduleNextTick(bool runSoon)
    {
        var dueTime = runSoon ? 1_000 : GetNextIntervalMilliseconds(DateTimeOffset.Now);
        _timer.Change(dueTime, Timeout.Infinite);
    }

    private static int GetNextIntervalMilliseconds(DateTimeOffset now)
    {
        var local = now.LocalDateTime;
        var nextBoundary = local.Date.Add(DayBoundary);
        if (local >= nextBoundary)
        {
            nextBoundary = nextBoundary.AddDays(1);
        }

        var nextCheck = nextBoundary.AddMinutes(1);
        var interval = nextCheck - local;
        return Math.Clamp((int)interval.TotalMilliseconds, 60_000, int.MaxValue);
    }
}
