# scenario22

A Windows WPF desktop app that manages app launch and shutdown scenarios. You build a set of "open/close" actions — for example a "Gaming mode" — assign a global hotkey, and trigger them with a single press from any application. The interface is English by default; Russian can be enabled in Settings.

![scenario22 screenshot](scenario22/Assets/app-preview.png)

## Features

- **Scenarios** — a set of actions executed in order: launching apps, shortcuts, documents and URLs (including `steam://…`), closing processes by name.
- **Music** — start a track or playlist in Spotify, Yandex Music, SoundCloud or YouTube Music; links start playback, plain text opens the service's search.
- **YouTube** — open a video link or run a YouTube search.
- **Windows settings** — change the desktop wallpaper (PNG, JPG, BMP) and switch Windows between light and dark mode.
- **Running-process picker** — choose an open app from a searchable list with icons and paths instead of typing them.
- **Global hotkeys** — work even when the window is minimized to tray; readiness indicator and hints about conflicting combinations.
- **Custom scenario icons** — white vector icons from the built-in palette, or your own image (png/jpg/ico/exe/lnk).
- **Custom colors** — 6 ready-made palettes plus any custom gradient.
- **Delays** — each action can pause for 0–600 seconds.
- **Tray** — a menu with quick scenario launching, settings and exit; closing the window minimizes to tray.
- **Themes** — light and dark, with a soft glow behind the active scenario page.
- **Toasts** — run progress and a result report with error details.

## Requirements

- Windows 10 1709+ (square corners on Windows 11)
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) — only needed for the framework-dependent build

## Build and run

```powershell
git clone https://github.com/wintence/scenario22.git
cd scenario22
dotnet build scenario22\scenario22.csproj
dotnet run --project scenario22\scenario22.csproj
```

Publishing a portable single-file build:

```powershell
dotnet publish scenario22\scenario22.csproj -c Release -r win-x64 -p:PublishSingleFile=true -p:SelfContained=true -p:IncludeNativeLibrariesForSelfExtract=true
```

Ready-made builds are available on the [Releases](https://github.com/wintence/scenario22/releases) page.

## Data storage

Settings and scenarios are stored only on your computer — in `%APPDATA%\scenario22\settings.json`. If the file gets corrupted, the app keeps a backup next to it (`settings.json.broken-*`) and starts fresh.

## Process-killing safety

A number of system processes (`explorer`, `csrss`, `lsass`, `svchost`, `dwm`, etc.) are protected and cannot be closed by scenarios — do not weaken or disable this protection.

## Project structure

```text
scenario22/
├── App.xaml(.cs)          — entry point, single-instance mutex, exception handling
├── MainWindow.xaml(.cs)   — main window and core UI logic
├── ToastWindow.xaml(.cs)  — toast notifications
├── Models/                — Scenario, ScenarioAction, Hotkey, AppState, palettes
├── Services/              — runner, settings, hotkeys, tray, themes, desktop settings, processes
├── Sheets/                — action and scenario dialogs
├── Controls/HotkeyBox     — hotkey combination input
└── Themes/                — Dark.xaml, Light.xaml, Styles.xaml, Icons.xaml
```

## License

MIT — see [LICENSE](LICENSE).
