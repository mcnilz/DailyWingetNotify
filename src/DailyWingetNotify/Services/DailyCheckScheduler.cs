using DailyWingetNotify.Models;

namespace DailyWingetNotify.Services;

internal sealed class DailyCheckScheduler : IDisposable
{
    private static readonly TimeSpan DayBoundary = TimeSpan.FromHours(3);
    private readonly StateStore _stateStore;
    private readonly SystemLoadService _systemLoadService;
    private readonly Timer _timer;
    private Func<CancellationToken, Task>? _checkCallback;
    private CancellationToken _cancellationToken;
    private bool _deferInitialCheckUntilLowCpuUsage;
    private bool _initialCheckDelayHandled;
    private bool _isRunning;

    public DailyCheckScheduler(StateStore stateStore, SystemLoadService systemLoadService)
    {
        _stateStore = stateStore;
        _systemLoadService = systemLoadService;
        _timer = new Timer(OnTimerTick);
    }

    public void Start(
        Func<CancellationToken, Task> checkCallback,
        bool deferInitialCheckUntilLowCpuUsage,
        CancellationToken cancellationToken)
    {
        _checkCallback = checkCallback;
        _deferInitialCheckUntilLowCpuUsage = deferInitialCheckUntilLowCpuUsage;
        _cancellationToken = cancellationToken;
        ScheduleNextTick(runSoon: true);
    }

    public async Task RunManualCheckAsync(CancellationToken cancellationToken)
    {
        await RunCheckAsync(markLogicalDay: true, cancellationToken).ConfigureAwait(false);
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
            if (ShouldRunDailyCheck(DateTimeOffset.Now))
            {
                await WaitForInitialCheckSlotAsync(_cancellationToken).ConfigureAwait(false);

                if (ShouldRunDailyCheck(DateTimeOffset.Now))
                {
                    await RunCheckAsync(markLogicalDay: true, _cancellationToken).ConfigureAwait(false);
                }
            }
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
            await _systemLoadService.WaitForLowCpuUsageAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task RunCheckAsync(bool markLogicalDay, CancellationToken cancellationToken)
    {
        if (_checkCallback is null || _isRunning)
        {
            return;
        }

        _isRunning = true;
        try
        {
            await _checkCallback(cancellationToken).ConfigureAwait(false);
            if (markLogicalDay)
            {
                await _stateStore.SaveAsync(new AppState(GetLogicalDay(DateTimeOffset.Now)), cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            _isRunning = false;
        }
    }

    private bool ShouldRunDailyCheck(DateTimeOffset now)
    {
        var state = _stateStore.Load();
        return state.LastCheckedLogicalDay != GetLogicalDay(now);
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
