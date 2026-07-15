# WinPowerMenu

[← 日本語 README](README.md)

A lightweight tray app for Windows that pops up a big "Shutdown / Restart /
Sleep / Cancel" menu when you press the physical power button (or any key
you assign).

## Highlights

- **Plays well with other tools.** A low-level keyboard hook consumes
  **only the one key you register** — every other key passes through
  untouched, so AutoHotkey, PowerToys, etc. keep working.
- **Touch-friendly UI.** Four large buttons in a 2×2 grid centered on
  screen, rounded semi-transparent panel. Press Esc or click outside to
  cancel.
- **Key learning.** Hit *Learn*, press the key you want (your power
  button), and its VK / scan code is registered automatically.
- **Runs in the tray.** Right-click for *Show menu / Settings / Exit*.

## Prerequisite

Set Windows Power Options → power button action = **Do nothing**,
otherwise the OS eats the event before the app sees it.

- Settings → System → Power & battery → Power button behavior
- or `powercfg` advanced settings

## Requires

- Windows 10 / 11 (x64)
- .NET 8 Desktop Runtime

## Build

```powershell
cd WinPowerMenu
dotnet build -c Release
```

Single-file publish:

```powershell
dotnet publish WinPowerMenu -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```

Output: `WinPowerMenu/bin/Release/net8.0-windows/win-x64/publish/WinPowerMenu.exe`.

## Usage

1. Launch `WinPowerMenu.exe` (it lives in the system tray).
2. Right-click the tray icon → **Settings…** → **Learn** and press the
   power button / key you want to use.
3. **OK** to save. From now on that key opens the popup.

To auto-start, drop a shortcut to `WinPowerMenu.exe` into
`shell:startup` (Win+R → `shell:startup`).

## Settings file

Stored at `%LOCALAPPDATA%\WinPowerMenu\settings.json`.

```json
{
  "TriggerVkCode": 95,
  "TriggerScanCode": 0,
  "TriggerLabel": "VK_SLEEP (0x5F)"
}
```

Default is `VK_SLEEP (0x5F)`. You can edit the JSON by hand.

## Power actions

| Button    | Action                                          |
|-----------|-------------------------------------------------|
| Shutdown  | `shutdown.exe /s /t 0`                          |
| Restart   | `shutdown.exe /r /t 0`                          |
| Sleep     | `powrprof.SetSuspendState(false, false, false)` |
| Cancel    | Just closes the popup                           |

## Layout

```
WinPowerMenu/
├── WinPowerMenu.sln
└── WinPowerMenu/
    ├── WinPowerMenu.csproj
    ├── app.manifest
    ├── App.xaml / App.xaml.cs         — entry point, tray + hook
    ├── AppSettings.cs                 — settings load/save (JSON)
    ├── LowLevelKeyboardHook.cs        — WH_KEYBOARD_LL, only consumes target
    ├── PowerActions.cs                — shutdown / restart / sleep
    ├── PowerMenuWindow.xaml(.cs)      — popup
    ├── SettingsWindow.xaml(.cs)       — settings / learn
    └── TrayIcon.cs                    — NotifyIcon
```

## License

MIT
