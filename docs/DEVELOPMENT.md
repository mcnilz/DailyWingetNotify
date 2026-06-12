# Development

## Architecture

DailyWingetNotify is intentionally small and separated into a few services:

- `TrayApplicationContext` owns the Windows tray icon and menu.
- `DailyCheckScheduler` decides when the next daily check should run.
- `WingetUpdateService` executes `winget upgrade` and parses available updates.
- `AutostartService` manages the current-user Windows Run key.
- `StateStore` persists the last completed logical check day.

The logical day starts at 03:00 local time. For example, `2026-06-12 02:30` belongs to logical day `2026-06-11`; `2026-06-12 03:00` belongs to `2026-06-12`.

## Native AOT

The project is configured for .NET 10 Native AOT publishing with a Windows x64 runtime identifier. Keep dependencies minimal and avoid reflection-heavy libraries unless they are explicitly made AOT-safe.

Native AOT intermediate and linker output are written below `%TEMP%\DailyWingetNotify` so publishing stays reliable when the repository is located in a synced folder such as OneDrive.

## Common Commands

```powershell
dotnet build .\src\DailyWingetNotify\DailyWingetNotify.csproj -c Release
dotnet publish .\src\DailyWingetNotify\DailyWingetNotify.csproj -c Release -r win-x64
```

## Manual Test Checklist

- Launch the app and confirm no main window appears.
- Confirm the tray icon is visible.
- Use `Check now` and verify the menu is disabled while the check runs.
- Confirm the notification text changes between no updates, available updates, and errors.
- Toggle autostart and verify the Run key value.
- Exit from the tray menu and confirm the process terminates.
