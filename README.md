# Keep Watching 👀

**A lightweight Windows utility that keeps your computer and display awake with a single click.**

No unnecessary background windows, complicated settings, or third-party applications.

## Features

- One-click toggle between Awake ON and OFF
- Prevents automatic display timeout and system sleep
- Works on both battery and plugged-in power
- System tray indicator while Awake Mode is active
- No visible PowerShell or CMD window during normal use
- Restores original power settings when disabled
- Lightweight and free

## Installation

1. Download the latest ZIP from [GitHub Releases](../../releases).
2. Extract the ZIP.
3. Run `Install.cmd`.
4. Double-click the **Awake Toggle** desktop shortcut.

No administrator access is normally required.

## Usage

| Action | Behavior |
|---|---|
| First double-click | Enable Awake Mode |
| Green A tray icon | Awake Mode is active |
| Second double-click | Disable Awake Mode |
| Right-click tray icon | Turn OFF and exit |

## How It Works

Built using **C#**, Windows Forms, and native Windows APIs.

- `powercfg` manages display and sleep timeout settings.
- `SetThreadExecutionState` requests that Windows keep the system and display awake.
- `NotifyIcon` provides the system tray indicator.
- A named mutex and event enable single-instance operation and toggling.
- Original timeout settings are saved locally and restored when disabled.

## Requirements

- Windows 10 or Windows 11
- Compatible .NET Framework

## Limitations

- Does not override manual sleep or closing the laptop lid.
- Does not prevent critical-battery shutdown or hibernation.
- Keeping the display active increases battery consumption.

## Privacy

Keep Watching does not require an account or internet connection for normal operation. Its purpose is local Windows power management.

## License

MIT License — see [LICENSE](LICENSE).

---

Made for a simpler Windows experience.
