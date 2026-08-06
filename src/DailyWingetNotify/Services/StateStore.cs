using System.Text.Json;
using System.Text.Json.Serialization;
using DailyWingetNotify.Models;

namespace DailyWingetNotify.Services;

internal sealed class StateStore(string filePath)
{
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public AppState Load()
    {
        return LoadCore();
    }

    public async Task SaveCheckedLogicalDayAsync(DateOnly logicalDay, CancellationToken cancellationToken)
    {
        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var state = LoadCore();
            await SaveCoreAsync(
                state with { LastCheckedLogicalDay = logicalDay },
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public async Task SavePendingNotificationAsync(
        PendingNotificationState? pendingNotification,
        CancellationToken cancellationToken)
    {
        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var state = LoadCore();
            await SaveCoreAsync(
                state with { PendingNotification = pendingNotification },
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private AppState LoadCore()
    {
        if (!File.Exists(filePath))
        {
            return new AppState(null);
        }

        try
        {
            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize(json, DailyWingetNotifyJsonContext.Default.AppState) ?? new AppState(null);
        }
        catch
        {
            return new AppState(null);
        }
    }

    public async Task SaveAsync(AppState state, CancellationToken cancellationToken)
    {
        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await SaveCoreAsync(state, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private async Task SaveCoreAsync(AppState state, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var file = File.Create(filePath);
        await JsonSerializer.SerializeAsync(file, state, DailyWingetNotifyJsonContext.Default.AppState, cancellationToken).ConfigureAwait(false);
    }
}

[JsonSerializable(typeof(AppState))]
internal sealed partial class DailyWingetNotifyJsonContext : JsonSerializerContext;
