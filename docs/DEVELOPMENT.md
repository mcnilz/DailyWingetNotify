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

## Versioning and Release Notes

DailyWingetNotify uses Semantic Versioning for releases. Release tags must be prefixed with `v` and must follow SemVer, for example `v1.2.3` for a stable release or `v1.2.3-rc.1` for a prerelease.

Choose version increments as follows:

- Increment `MAJOR` for incompatible behavioral or operational changes.
- Increment `MINOR` for backward-compatible features or meaningful UX improvements.
- Increment `PATCH` for backward-compatible bug fixes, internal maintenance, and documentation-only release corrections.

Every release must have release notes. Maintain manual release notes in `docs/releases/vNEXT.md` while preparing release-relevant changes, then rename the file to the final SemVer tag before creating the release. If no manual file exists for a tag, the release workflow falls back to GitHub generated release notes from merged changes, so keep pull request titles concise and label changes with `breaking-change`, `enhancement`, `bug`, `maintenance`, `documentation`, or `dependencies` when possible.

Release builds embed the release tag in the executable. The About dialog reads `AssemblyInformationalVersion`, so a release tagged `v1.2.3` displays `DailyWingetNotify v1.2.3`. Local development builds use the project default `0.0.0-dev`.

## Manual Test Checklist

- Launch the app and confirm no main window appears.
- Confirm the tray icon is visible.
- Use `Check now` and verify the menu is disabled while the check runs.
- Confirm the notification text changes between no updates, available updates, and errors.
- Toggle autostart and verify the Run key value.
- Exit from the tray menu and confirm the process terminates.
