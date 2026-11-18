AWAKE TOGGLE CLEAN - Windows 10/11

INSTALL (once):
  1. Extract the whole ZIP (don't run files from inside the ZIP).
  2. Double-click Install.cmd. This one-time installer has a command window.
  3. Double-click the new 'Awake Toggle' shortcut on your desktop.

NORMAL USE:
  First double-click  -> display and sleep timeouts set to Never (AC + battery).
  A green 'A' icon appears in the system tray; no taskbar window.
  Next double-click   -> original four timeout values restored; icon disappears.
  You can also double-click the tray icon or right-click > Turn OFF and exit.

DETAILS:
  The installed program is a compiled Windows GUI .exe (not a PowerShell script).
  No CMD, PowerShell, or taskbar window runs during normal use.
  No admin privileges needed under normal Windows policies.
  Does not prevent manually initiated sleep, lid-close sleep, or critical-battery hibernation.
  Uses the active power plan at activation and restores that plan's four settings.
  If Windows is forcibly shut down or the process is killed, run the shortcut once
  after reboot: it first recovers saved settings and then enables Awake Mode.

LOCATION:
  %LOCALAPPDATA%\AwakeToggleClean\AwakeToggle.exe
  %LOCALAPPDATA%\AwakeToggleClean\original-timeouts.txt (while ON)

If your older script previously changed power settings and did not restore them,
those earlier values can't be recovered by this application. Set your preferred
Windows sleep/display timeouts first, then launch this new utility.

The source is included: AwakeToggle.cs. Built locally using the Windows .NET compiler.
