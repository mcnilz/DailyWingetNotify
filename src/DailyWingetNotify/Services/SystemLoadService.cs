using System.Runtime.InteropServices;

namespace DailyWingetNotify.Services;

internal sealed class SystemLoadService
{
    private static readonly TimeSpan SampleDuration = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan RetryInterval = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan MaximumStartupDelay = TimeSpan.FromMinutes(15);
    private const double MaximumCpuUsage = 50;

    public async Task WaitForLowCpuUsageAsync(CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow.Add(MaximumStartupDelay);

        while (DateTimeOffset.UtcNow < deadline)
        {
            var cpuUsage = await TryGetCpuUsageAsync(cancellationToken).ConfigureAwait(false);
            if (cpuUsage is null || cpuUsage <= MaximumCpuUsage)
            {
                return;
            }

            var delay = deadline - DateTimeOffset.UtcNow;
            if (delay <= TimeSpan.Zero)
            {
                return;
            }

            await Task.Delay(delay < RetryInterval ? delay : RetryInterval, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<double?> TryGetCpuUsageAsync(CancellationToken cancellationToken)
    {
        if (!TryGetSystemTimes(out var first))
        {
            return null;
        }

        await Task.Delay(SampleDuration, cancellationToken).ConfigureAwait(false);

        if (!TryGetSystemTimes(out var second))
        {
            return null;
        }

        var idle = second.Idle - first.Idle;
        var total = second.Kernel + second.User - first.Kernel - first.User;
        if (total <= 0 || idle < 0)
        {
            return null;
        }

        return 100 - (idle * 100d / total);
    }

    private static bool TryGetSystemTimes(out SystemTimes times)
    {
        if (!GetSystemTimes(out var idleTime, out var kernelTime, out var userTime))
        {
            times = default;
            return false;
        }

        times = new SystemTimes(ToUInt64(idleTime), ToUInt64(kernelTime), ToUInt64(userTime));
        return true;
    }

    private static ulong ToUInt64(FileTime fileTime) =>
        ((ulong)fileTime.HighDateTime << 32) | fileTime.LowDateTime;

    private readonly record struct SystemTimes(ulong Idle, ulong Kernel, ulong User);

    [StructLayout(LayoutKind.Sequential)]
    private struct FileTime
    {
        public uint LowDateTime;
        public uint HighDateTime;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetSystemTimes(out FileTime idleTime, out FileTime kernelTime, out FileTime userTime);
}
