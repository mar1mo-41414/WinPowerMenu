# WinPowerMenu

[← 日本語 README](README.md)

A lightweight Windows tray app that pops up a big "Shutdown / Restart /
Sleep / Cancel" menu when you press the physical power button (or any
key you assign).

Ships a **display-off hook** trigger mode so it works on ROG Ally,
ASUS / MSI notebooks and other machines where the power button is
swallowed by ACPI before it reaches any userland event.

## Highlights

- **Plays well with other tools.** The keyboard-mode low-level hook
  consumes **only the one key you register** — every other key passes
  through untouched, so AutoHotkey / PowerToys keep working.
- **Touch-friendly UI.** Four large buttons in a 2×2 grid centered on
  screen, rounded semi-transparent panel. Esc or click outside cancels.
- **Key learning.** Hit *Learn*, press the key you want, and its VK /
  scan code is registered automatically.
- **Display-off hook.** For handhelds and notebooks whose power button
  never reaches userland. Sets Windows' power button action to
  "Turn off the display" and reacts to the resulting
  `PBT_POWERSETTINGCHANGE(GUID_CONSOLE_DISPLAY_STATE=0)`.
- **Runs in the tray.** Right-click for *Show menu / Settings / Exit*.
- **Auto-start.** One-click sign-in registration via HKCU\...\Run
  (asked on first launch).

## Requires

- Windows 10 / 11 (x64)
- .NET 8 Desktop Runtime

## Build

```powershell
cd WinPowerMenu
dotnet build -c Release
```

Output: `WinPowerMenu\bin\Release\net8.0-windows\win-x64\WinPowerMenu.exe`.

Single-file publish:

```powershell
dotnet publish WinPowerMenu -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```

## Usage (by machine type)

### Machines where the power button is a real key

Sleep-key equipped external keyboards, many older desktops.

1. Set Windows power button action to **Do nothing**.
2. Launch `WinPowerMenu.exe`.
3. Right-click the tray icon → **Settings…** → **Learn** and press the
   power button once. The VK code is captured.
4. **OK** to save.

### Machines where the power button is unreachable

Confirmed on ROG Ally RC71L, ASUS notebook, MSI gaming notebook:
Windows swallows the "Do nothing" case at the ACPI level and delivers
**nothing** to userland — no low-level keyboard hook event, no Raw
Input, no `WM_POWERBROADCAST`, no ATK-WMI, no Kernel-Power log entry.

Use **display-off hook** mode instead:

1. Launch `WinPowerMenu.exe`.
2. Right-click tray → **Settings…** → select the
   *"Display-off hook (ROG Ally etc.)"* radio.
3. Click **"Power button → Turn off display"** — the app runs
   `powercfg` to change the Windows setting.
4. **OK**.
5. Pressing the power button now triggers a brief display-off
   (1-2 s on Modern Standby machines), and the popup appears when the
   display comes back on.

Internally the app subscribes to `GUID_CONSOLE_DISPLAY_STATE` and only
treats a display-off event as a press when the last user input was
within the last 5 seconds — idle-timeout display-offs are ignored.

### ⚠️ ROG Ally caveat

Armoury Crate implements its performance-profile switcher
(Turbo / Silent / Performance) by **replacing the active Windows power
scheme**. There are 6 shipped ASUS schemes (+ overlays), **5 of which
default to `PBUTTONACTION = 3` (Shutdown)**. Plugging or unplugging the
charger triggers a mode switch, and the moment Armoury flips to
Silent-on-battery, pressing the power button executes a real shutdown.

WinPowerMenu's DisplayOff mode overwrites `PBUTTONACTION` to 4 on
**every** enumerated scheme at startup, and re-applies every 30 seconds
via a DispatcherTimer.

## Settings file

`%LOCALAPPDATA%\WinPowerMenu\settings.json`

```json
{
  "TriggerSource": "DisplayOff",
  "TriggerVkCode": 95,
  "TriggerScanCode": 0,
  "TriggerHidUsagePage": 1,
  "TriggerHidUsage": 129,
  "TriggerLabel": "VK_SLEEP (0x5F)",
  "FirstLaunchDone": true
}
```

- `TriggerSource`:
  - `Keyboard` — low-level keyboard hook (VK match)
  - `HidSystemControl` / `HidConsumer` — Raw Input on HID collections
  - `HidKeyboard` — Raw Input keyboard type
  - `DisplayOff` — the notebook/handheld path described above

## Diagnostics

Under `%LOCALAPPDATA%\WinPowerMenu\`:

- `learn.log` — every event observed during a Learn session
- `crash.log` — startup, unhandled exceptions, and PBUTTONACTION-
  protection cycles

## Power actions

| Button    | Action                                          |
|-----------|-------------------------------------------------|
| Shutdown  | `shutdown.exe /s /t 0`                          |
| Restart   | `shutdown.exe /r /t 0`                          |
| Sleep     | `powrprof.SetSuspendState(false, false, false)` |
| Cancel    | Just closes the popup                           |

## Project layout

```
WinPowerMenu/
├── WinPowerMenu.sln
└── WinPowerMenu/
    ├── WinPowerMenu.csproj
    ├── app.manifest
    ├── App.xaml(.cs)              entry, trigger rebuild, crash handlers
    ├── AppSettings.cs             JSON persistence + TriggerSource enum
    ├── AutoStartManager.cs        HKCU\…\Run auto-start
    ├── LowLevelKeyboardHook.cs    WH_KEYBOARD_LL, consumes target only
    ├── RawInputHost.cs            Raw Input (HID) + WM_POWERBROADCAST
    ├── HiddenRawInputWindow.cs    runtime host for HID trigger sources
    ├── DisplayOffTrigger.cs       GUID_CONSOLE_DISPLAY_STATE + all-schemes protection
    ├── PowercfgHelper.cs          powercfg /L enumerator + PBUTTONACTION writer
    ├── ExecutionState.cs          SetThreadExecutionState wrapper
    ├── PowerActions.cs            shutdown / restart / sleep
    ├── PowerMenuWindow.xaml(.cs)  the popup
    ├── SettingsWindow.xaml(.cs)   settings UI (learn + display-off + auto-start)
    ├── TrayIcon.cs                NotifyIcon
    ├── LearnLogger.cs             diagnostic log during Learn
    └── CrashLog.cs                unhandled exceptions & runtime notes
```

## License

MIT
