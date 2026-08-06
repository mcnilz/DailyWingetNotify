using System.Runtime.InteropServices;

namespace DailyWingetNotify.Services;

internal sealed partial class UserPresenceService
{
    private static readonly TimeSpan MaximumIdleTime = TimeSpan.FromMinutes(5);

    public static bool IsUserPresent() =>
        TryGetIdleTime(out var idleTime) && idleTime <= MaximumIdleTime;

    private static bool TryGetIdleTime(out TimeSpan idleTime)
    {
        var info = new LastInputInfo
        {
            Size = (uint)Marshal.SizeOf<LastInputInfo>(),
        };

        if (!GetLastInputInfo(ref info))
        {
            idleTime = TimeSpan.Zero;
            return false;
        }

        var currentTick = unchecked((uint)GetTickCount64());
        var elapsedMilliseconds = currentTick - info.Time;
        idleTime = TimeSpan.FromMilliseconds(elapsedMilliseconds);
        return true;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LastInputInfo
    {
        public uint Size;
        public uint Time;
    }

    [LibraryImport("user32.dll", EntryPoint = "GetLastInputInfoA", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetLastInputInfo(ref LastInputInfo lastInputInfo);

    [LibraryImport("kernel32.dll", EntryPoint = "GetTickCount64A")]
    private static partial ulong GetTickCount64();
}
