# BrowserBlocker

BrowserBlocker is a small Windows desktop widget that closes and blocks common
web browsers for one hour. The countdown deadline is stored in the current
user's local application data, so reopening BrowserBlocker does not reset an
active session.

## Build

Open `BrowserBlocker.sln` in Visual Studio 2022 and build the Release
configuration, or run:

```powershell
& "C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe" `
  .\BrowserBlocker.sln /t:Build /p:Configuration=Release
```

The executable is written to:

```text
BrowserBlocker\bin\Release\BrowserBlocker.exe
```

## Behavior

- Clicking **Block Browsers** starts an irreversible one-hour countdown.
- BrowserBlocker closes recognized browser processes every 300 milliseconds.
- Windows Task Scheduler runs a per-user watchdog for the duration of a block.
  Closing or force-quitting the widget does not lift an active block.
- The watchdog removes its scheduled task after the saved UTC deadline expires.
- WebView2 is intentionally not blocked because many ordinary Windows apps use
  it for embedded interfaces.
- BrowserBlocker does not install a service or modify the registry and does not
  require administrator rights. A browser running with higher privileges may
  not be closable by a non-administrator BrowserBlocker process.
