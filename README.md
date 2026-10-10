# Keep Awake

A small portable Windows app. No installer or administrator privileges needed.

Made with **GPT-6.1 Sol**.

## Use

1. Double-click **KeepAwake.exe**. It immediately prevents automatic idle sleep.
2. Click **Pause** to restore your usual sleep behavior, or **Resume** to keep awake again.
3. Click **Quit**, close the window with **X**, or right-click the tray icon and choose **Quit** to stop the program and restore normal sleep behavior.

Minimizing the window puts it in the system tray near the clock. Its icon may be inside the up-arrow menu. Double-click the tray icon to reopen the window. The tray menu also provides Pause/Resume and Quit.

The screen may turn off normally. Select **Also keep the screen on** if desired. The app makes no permanent changes to your Windows power settings, installs no service, and does not run automatically at startup. You can move the EXE anywhere and run it by itself.

Quitting restores the existing sleep policy; it does not immediately put the PC to sleep. Other apps can still keep the PC awake. Manual sleep, shutting a laptop lid, battery protection, and Windows or organization power policies can override this app. It does not prevent Windows locking or screen savers.

## Implementation and verification

Uses Windows' `SetThreadExecutionState` API on the main UI thread with `ES_CONTINUOUS | ES_SYSTEM_REQUIRED`. The optional display setting adds `ES_DISPLAY_REQUIRED`. Pause and cleanup clear the request with `ES_CONTINUOUS`; terminating the process also ends the requesting thread. No mouse or keyboard activity is simulated.

Requires Windows with .NET Framework 4.x (included in typical Windows 10/11 installations). The application manifest uses `asInvoker`, so it runs with the launching user's privileges.

Build from source with `powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1`. This uses the Windows .NET Framework compiler and needs no package downloads.

For a short integration check, run `KeepAwake.exe --self-test "C:\path\to\report.txt"` and wait for it to exit. The test constructs the actual app window without showing it, checks start/pause/resume/display states against Windows' returned execution-state flags, and checks that disposal releases the request. It briefly requests wakefulness and always clears its request afterward. This is an API check, not a full idle-sleep test.

API reference: https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-setthreadexecutionstate
