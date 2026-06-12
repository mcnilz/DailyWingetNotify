# DailyWingetNotify

DailyWingetNotify is a small Windows 11 tray application that checks once per local day whether `winget` has updates available. The day boundary is 03:00 local time, so checks after midnight still count as the previous day until 03:00.

## Features

- Runs only in the Windows taskbar notification area.
- Checks `winget upgrade` once per local day.
- Allows manual checks from the tray menu.
- Shows a notification when updates are available.
- Supports current-user autostart installation and removal.
- Publishes as a .NET 10 Native AOT single-file executable.

## Requirements

- Windows 11
- .NET 10 SDK for development
- Windows Package Manager (`winget`) available on the target machine

## Usage

Start `DailyWingetNotify.exe`. The application adds an icon to the Windows notification area and does not open a main window.

The tray context menu contains:

- `Check now`: immediately checks for available updates.
- `Install Autostart` / `Remove Autostart`: toggles current-user startup registration.
- `About`: shows version and license information.
- `Exit`: closes the application.

## Build

```powershell
dotnet build .\src\DailyWingetNotify\DailyWingetNotify.csproj -c Release
```

## Publish

```powershell
dotnet publish .\src\DailyWingetNotify\DailyWingetNotify.csproj -c Release -r win-x64
```

The publish output is written to:

```text
src\DailyWingetNotify\bin\Release\net10.0-windows\win-x64\publish
```

## GitHub Actions

The repository contains GitHub Actions workflows for CI builds and releases:

- `Build` runs on pushes, pull requests, and manual dispatch. It builds the project and publishes the Windows x64 Native AOT executable.
- `Release` runs for tags matching `v*` or by manual dispatch with a tag name. It publishes the Native AOT executable and uploads `DailyWingetNotify.exe` as the release asset.

Create a release from GitHub by pushing a version tag:

```powershell
git tag v1.0.0
git push origin v1.0.0
```

## Data

DailyWingetNotify stores its small state file in:

```text
%LocalAppData%\DailyWingetNotify\state.json
```

Autostart is registered under:

```text
HKCU\Software\Microsoft\Windows\CurrentVersion\Run
```
