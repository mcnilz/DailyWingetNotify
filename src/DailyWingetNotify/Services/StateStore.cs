using System.Text.Json;
using System.Text.Json.Serialization;
using DailyWingetNotify.Models;

namespace DailyWingetNotify.Services;

internal sealed class StateStore
{
    private readonly string _filePath;

    public StateStore(string filePath)
    {
        _filePath = filePath;
    }

    public AppState Load()
    {
        if (!File.Exists(_filePath))
        {
            return new AppState(null);
        }

        try
        {
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize(json, DailyWingetNotifyJsonContext.Default.AppState) ?? new AppState(null);
        }
        catch
        {
            return new AppState(null);
        }
    }

    public async Task SaveAsync(AppState state, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using var file = File.Create(_filePath);
        await JsonSerializer.SerializeAsync(file, state, DailyWingetNotifyJsonContext.Default.AppState, cancellationToken).ConfigureAwait(false);
    }
}

[JsonSerializable(typeof(AppState))]
internal sealed partial class DailyWingetNotifyJsonContext : JsonSerializerContext;
