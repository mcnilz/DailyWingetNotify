using System.Diagnostics;
using System.Text.RegularExpressions;
using DailyWingetNotify.Models;

namespace DailyWingetNotify.Services;

internal sealed partial class WingetUpdateService
{
    public async Task<WingetCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "winget",
                    Arguments = "upgrade --accept-source-agreements --disable-interactivity --nowarn",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                },
            };

            process.Start();
            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

            var output = await outputTask.ConfigureAwait(false);
            var error = await errorTask.ConfigureAwait(false);
            var rawOutput = string.Join(Environment.NewLine, new[] { output, error }.Where(value => !string.IsNullOrWhiteSpace(value)));

            if (process.ExitCode != 0 && string.IsNullOrWhiteSpace(rawOutput))
            {
                return new WingetCheckResult([], rawOutput, $"winget exited with code {process.ExitCode}.");
            }

            return new WingetCheckResult(ParseUpdates(output), rawOutput, null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return new WingetCheckResult([], string.Empty, exception.Message);
        }
    }

    private static IReadOnlyList<WingetUpdate> ParseUpdates(string output)
    {
        var lines = GetOutputLines(output);
        var updates = new List<WingetUpdate>();
        var separatorIndex = Array.FindIndex(lines, IsTableSeparator);

        if (separatorIndex < 1)
        {
            return updates;
        }

        for (var index = separatorIndex + 1; index < lines.Length; index++)
        {
            var line = lines[index];
            if (IsEndOfUpdateTable(line))
            {
                break;
            }

            if (TryParseUpdateLine(line, out var update))
            {
                updates.Add(update);
            }
        }

        return updates;
    }

    private static string[] GetOutputLines(string output)
    {
        var normalizedOutput = AnsiEscapeRegex().Replace(output, string.Empty);

        return normalizedOutput
            .Split(["\r\n", "\n", "\r"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static bool IsTableSeparator(string line) =>
        TableSeparatorRegex().IsMatch(line);

    private static bool IsEndOfUpdateTable(string line) =>
        string.IsNullOrWhiteSpace(line)
        || line.Contains("upgrades available", StringComparison.OrdinalIgnoreCase)
        || line.Contains("upgrade available", StringComparison.OrdinalIgnoreCase)
        || line.Contains("Aktualisierungen verfügbar", StringComparison.OrdinalIgnoreCase)
        || line.Contains("Aktualisierung verfügbar", StringComparison.OrdinalIgnoreCase)
        || line.StartsWith("The following", StringComparison.OrdinalIgnoreCase);

    private static bool TryParseUpdateLine(string line, out WingetUpdate update)
    {
        var parts = WhitespaceRegex().Split(line.Trim());
        if (parts.Length < 5)
        {
            update = default!;
            return false;
        }

        var idIndex = parts.Length - 4;
        update = new WingetUpdate(
            string.Join(' ', parts[..idIndex]),
            parts[idIndex],
            parts[idIndex + 1],
            parts[idIndex + 2],
            parts[idIndex + 3]);

        return true;
    }

    [GeneratedRegex(@"\e\[[0-9;?]*[ -/]*[@-~]")]
    private static partial Regex AnsiEscapeRegex();

    [GeneratedRegex(@"^\s*(?=(?:.*-){10,})[-\s]+\s*$")]
    private static partial Regex TableSeparatorRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
