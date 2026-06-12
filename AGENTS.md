# Agent Notes

## Project

DailyWingetNotify is a small Windows 11 tray application that checks once per local day whether `winget` reports package updates. The local day starts at 03:00.

## Development Guidelines

- Keep application code, comments, documentation, identifiers, and commit messages in English.
- Prefer small services with explicit responsibilities over UI-heavy logic.
- Keep the app dependency-light so Native AOT publishing remains predictable.
- Preserve the tray-only UX: no main window, no startup splash, and no background console.
- Do not persist user data outside `%LocalAppData%\DailyWingetNotify` unless the user explicitly asks for it.
- Autostart must use the current-user Windows Run key only.

## Verification

Run these checks before handing off changes:

```powershell
dotnet build .\src\DailyWingetNotify\DailyWingetNotify.csproj -c Release
dotnet publish .\src\DailyWingetNotify\DailyWingetNotify.csproj -c Release -r win-x64
```

Manual checks:

- Start the app and confirm only a tray icon appears.
- Open the tray context menu and test `Check now`, `Install Autostart` / `Remove Autostart`, `About`, and `Exit`.
- Confirm `winget upgrade --accept-source-agreements --disable-interactivity` works on the target machine.

