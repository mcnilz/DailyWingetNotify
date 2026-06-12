using DailyWingetNotify.Models;
using DailyWingetNotify.Services;

namespace DailyWingetNotify.UI;

internal sealed class TrayApplication : IDisposable
{
    private const uint TrayIconId = 1;
    private const string WindowClassName = "DailyWingetNotify.TrayWindow";
    private readonly WingetUpdateService _wingetUpdateService;
    private readonly AutostartService _autostartService;
    private readonly DailyCheckScheduler _scheduler;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly NativeMethods.WindowProcedure _windowProcedure;
    private IntPtr _windowHandle;
    private IntPtr _iconHandle;
    private bool _isChecking;
    private bool _disposed;

    public TrayApplication(
        WingetUpdateService wingetUpdateService,
        AutostartService autostartService,
        DailyCheckScheduler scheduler)
    {
        _wingetUpdateService = wingetUpdateService;
        _autostartService = autostartService;
        _scheduler = scheduler;
        _windowProcedure = WindowProcedure;
    }

    public void Run()
    {
        CreateMessageWindow();
        AddTrayIcon();
        _scheduler.Start(CheckAndNotifyAsync, _autostartService.IsInstalled(), _shutdown.Token);

        while (NativeMethods.GetMessage(out var message, IntPtr.Zero, 0, 0) > 0)
        {
            NativeMethods.TranslateMessage(ref message);
            NativeMethods.DispatchMessage(ref message);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _shutdown.Cancel();
        _scheduler.Dispose();
        RemoveTrayIcon();

        if (_windowHandle != IntPtr.Zero)
        {
            NativeMethods.DestroyWindow(_windowHandle);
            _windowHandle = IntPtr.Zero;
        }

        _shutdown.Dispose();
    }

    private IntPtr WindowProcedure(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam)
    {
        switch (message)
        {
            case NativeMethods.WmCommand:
                HandleCommand((int)(wParam.ToInt64() & 0xffff));
                return IntPtr.Zero;
            case NativeMethods.WmAppTray:
                HandleTrayMessage((int)lParam);
                return IntPtr.Zero;
            case NativeMethods.WmDestroy:
                NativeMethods.PostQuitMessage(0);
                return IntPtr.Zero;
            default:
                return NativeMethods.DefWindowProc(hwnd, message, wParam, lParam);
        }
    }

    private void HandleTrayMessage(int trayMessage)
    {
        if (trayMessage is NativeMethods.WmRButtonUp or NativeMethods.WmContextMenu)
        {
            ShowContextMenu();
        }
        else if (trayMessage == NativeMethods.WmLButtonDblClk)
        {
            _ = RunManualCheckAsync();
        }
    }

    private void HandleCommand(int command)
    {
        switch (command)
        {
            case NativeMethods.CmdCheckNow:
                _ = RunManualCheckAsync();
                break;
            case NativeMethods.CmdAutostart:
                ToggleAutostart();
                break;
            case NativeMethods.CmdAbout:
                ShowAbout();
                break;
            case NativeMethods.CmdExit:
                NativeMethods.DestroyWindow(_windowHandle);
                break;
        }
    }

    private async Task RunManualCheckAsync()
    {
        await _scheduler.RunManualCheckAsync(_shutdown.Token).ConfigureAwait(false);
    }

    private async Task CheckAndNotifyAsync(CancellationToken cancellationToken)
    {
        if (_isChecking)
        {
            return;
        }

        _isChecking = true;
        SetTrayTip("DailyWingetNotify - checking");
        try
        {
            var result = await _wingetUpdateService.CheckForUpdatesAsync(cancellationToken).ConfigureAwait(false);
            NotifyResult(result);
        }
        finally
        {
            _isChecking = false;
            SetTrayTip("DailyWingetNotify");
        }
    }

    private void ToggleAutostart()
    {
        try
        {
            if (_autostartService.IsInstalled())
            {
                _autostartService.Remove();
                ShowBalloon("Autostart removed", "DailyWingetNotify will no longer start with Windows.", NativeMethods.NiifInfo);
            }
            else
            {
                _autostartService.Install();
                ShowBalloon("Autostart installed", "DailyWingetNotify will start with Windows.", NativeMethods.NiifInfo);
            }
        }
        catch (Exception exception)
        {
            ShowBalloon("Autostart failed", exception.Message, NativeMethods.NiifError);
        }
    }

    private void ShowAbout()
    {
        NativeMethods.MessageBox(
            _windowHandle,
            "DailyWingetNotify\n\nChecks daily for winget updates.\nLicense: MIT",
            "About DailyWingetNotify",
            0x00000040);
    }

    private void NotifyResult(WingetCheckResult result)
    {
        if (!result.IsSuccess)
        {
            ShowBalloon("winget check failed", result.ErrorMessage ?? "Unknown error.", NativeMethods.NiifError);
            return;
        }

        if (result.Updates.Count == 0)
        {
            ShowBalloon("No updates available", "winget does not report pending updates.", NativeMethods.NiifInfo);
            return;
        }

        ShowBalloon(FormatUpdateTitle(result.Updates), FormatUpdateMessage(result.Updates), NativeMethods.NiifWarning);
    }

    private void ShowContextMenu()
    {
        var menu = NativeMethods.CreatePopupMenu();
        try
        {
            var checkFlags = _isChecking ? NativeMethods.MfString | NativeMethods.MfGrayed : NativeMethods.MfString;
            NativeMethods.AppendMenu(menu, (uint)checkFlags, NativeMethods.CmdCheckNow, "Check now");
            NativeMethods.AppendMenu(menu, NativeMethods.MfSeparator, 0, null);
            NativeMethods.AppendMenu(menu, NativeMethods.MfString, NativeMethods.CmdAutostart, GetAutostartMenuText());
            NativeMethods.AppendMenu(menu, NativeMethods.MfString, NativeMethods.CmdAbout, "About");
            NativeMethods.AppendMenu(menu, NativeMethods.MfSeparator, 0, null);
            NativeMethods.AppendMenu(menu, NativeMethods.MfString, NativeMethods.CmdExit, "Exit");

            NativeMethods.GetCursorPos(out var point);
            NativeMethods.SetForegroundWindow(_windowHandle);
            NativeMethods.TrackPopupMenu(menu, NativeMethods.TpmRightButton, point.X, point.Y, 0, _windowHandle, IntPtr.Zero);
        }
        finally
        {
            NativeMethods.DestroyMenu(menu);
        }
    }

    private void CreateMessageWindow()
    {
        var windowClass = new NativeMethods.WindowClass
        {
            Size = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.WindowClass>(),
            WindowProcedure = _windowProcedure,
            Instance = NativeMethods.GetModuleHandle(null),
            Icon = LoadApplicationIcon(),
            SmallIcon = LoadApplicationIcon(),
            ClassName = WindowClassName,
        };

        if (NativeMethods.RegisterClassEx(ref windowClass) == 0)
        {
            throw new InvalidOperationException("Failed to register tray window class.");
        }

        _windowHandle = NativeMethods.CreateWindowEx(0, WindowClassName, "DailyWingetNotify", 0, 0, 0, 0, 0, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
        if (_windowHandle == IntPtr.Zero)
        {
            throw new InvalidOperationException("Failed to create tray window.");
        }
    }

    private void AddTrayIcon()
    {
        _iconHandle = LoadApplicationIcon();
        var data = CreateNotifyIconData(NativeMethods.NifMessage | NativeMethods.NifIcon | NativeMethods.NifTip);
        data.Icon = _iconHandle;
        data.Tip = "DailyWingetNotify";

        if (!NativeMethods.Shell_NotifyIcon(NativeMethods.NimAdd, ref data))
        {
            throw new InvalidOperationException("Failed to add tray icon.");
        }
    }

    private void RemoveTrayIcon()
    {
        if (_windowHandle == IntPtr.Zero)
        {
            return;
        }

        var data = CreateNotifyIconData(0);
        NativeMethods.Shell_NotifyIcon(NativeMethods.NimDelete, ref data);
    }

    private void SetTrayTip(string tip)
    {
        var data = CreateNotifyIconData(NativeMethods.NifTip);
        data.Tip = tip;
        NativeMethods.Shell_NotifyIcon(NativeMethods.NimModify, ref data);
    }

    private void ShowBalloon(string title, string text, uint icon)
    {
        var data = CreateNotifyIconData(NativeMethods.NifInfo);
        data.InfoTitle = title;
        data.Info = text;
        data.InfoFlags = icon;
        NativeMethods.Shell_NotifyIcon(NativeMethods.NimModify, ref data);
    }

    private NativeMethods.NotifyIconData CreateNotifyIconData(uint flags) => new()
    {
        Size = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.NotifyIconData>(),
        WindowHandle = _windowHandle,
        Id = TrayIconId,
        Flags = flags,
        CallbackMessage = NativeMethods.WmAppTray,
        Tip = string.Empty,
        Info = string.Empty,
        InfoTitle = string.Empty,
    };

    private string GetAutostartMenuText() => _autostartService.IsInstalled() ? "Remove Autostart" : "Install Autostart";

    private static IntPtr LoadApplicationIcon()
    {
        var module = NativeMethods.GetModuleHandle(null);
        var icon = NativeMethods.LoadIcon(module, new IntPtr(NativeMethods.IconResourceId));

        return icon == IntPtr.Zero
            ? NativeMethods.LoadIcon(IntPtr.Zero, NativeMethods.IdiApplication)
            : icon;
    }

    private static string FormatUpdateTitle(IReadOnlyList<WingetUpdate> updates) =>
        updates.Count == 1 ? "Update available" : $"{updates.Count} updates available";

    private static string FormatUpdateMessage(IReadOnlyList<WingetUpdate> updates)
    {
        if (updates.Count == 1)
        {
            var update = updates[0];
            return TruncateBalloonText($"{update.Name} {update.CurrentVersion} -> {update.AvailableVersion}");
        }

        var lines = new List<string>();
        var usedCharacters = 0;
        for (var index = 0; index < updates.Count; index++)
        {
            var remainingUpdates = updates.Count - index;
            var suffix = remainingUpdates == 1 ? "1 more" : $"{remainingUpdates} more";
            var reservedCharacters = Environment.NewLine.Length + suffix.Length + 2;
            var line = updates[index].Name;
            var nextLength = usedCharacters + (lines.Count == 0 ? 0 : Environment.NewLine.Length) + line.Length;

            if (nextLength + reservedCharacters > NativeMethods.BalloonTextLength)
            {
                lines.Add($"+{suffix}");
                break;
            }

            lines.Add(line);
            usedCharacters = nextLength;
        }

        return TruncateBalloonText(string.Join(Environment.NewLine, lines));
    }

    private static string TruncateBalloonText(string text)
    {
        const string suffix = "...";
        return text.Length <= NativeMethods.BalloonTextLength
            ? text
            : text[..(NativeMethods.BalloonTextLength - suffix.Length)] + suffix;
    }
}
