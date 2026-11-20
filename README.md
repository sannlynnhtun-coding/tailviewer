# Tailviewer

<img src="assets/branding/icon-256.png" width="96" height="96" alt="Tailviewer logo" />

Tailviewer is an open-source Windows desktop app for monitoring and investigating local logs. Watch new events arrive, search across folders, and inspect structured log data alongside the original messages.

## Download v1.0.1

Both Windows x64 downloads bundle .NET 10. Choose the portable EXE to run directly or the setup EXE to install Tailviewer for your Windows account and add a Start menu shortcut.

- [Portable EXE](https://github.com/sannlynnhtun-coding/tailviewer/releases/download/v1.0.1/tailviewer-1.0.1-win-x64-portable.exe)
- [Windows installer](https://github.com/sannlynnhtun-coding/tailviewer/releases/download/v1.0.1/tailviewer-1.0.1-win-x64-setup.exe)
- [Release notes and SHA-256 checksums](https://github.com/sannlynnhtun-coding/tailviewer/releases/tag/v1.0.1)

The executables are unsigned. The portable EXE extracts its bundled files to the .NET cache on first launch.

After installing, right-click a `.log` or `.txt` file in File Explorer and select **Open with → Tailviewer**. For other text files or the portable build, use **Open with → Choose another app** and browse to the Tailviewer executable.

## Workflow

1. On the dashboard, select **Open File** for a log or **Add Folders** to monitor directories. Use **All Folders** to narrow the view or open **Sources** to manage and browse loaded logs.
2. Use **Live Tail** to follow new events. Turn off **Auto-scroll** to hold your place while events continue to arrive.
3. Enter text in **Search logs** or enable regular expression mode. Narrow results by time range, service, environment, log level, HTTP status, or host when those fields are available.
4. Select an event to open the **Log Inspector** and review its message and parsed fields. Use the export button to save the filtered events as JSON.
5. In **Sources**, browse the original log lines, copy a selection, or change the text encoding. Select **Dashboard** to return to the event view.

## Build from source

Run these commands from the repository root with the .NET SDK specified in [global.json](global.json).

```powershell
dotnet run --project src/Tailviewer.Avalonia
dotnet test Tailviewer.Avalonia.slnx -c Release
```

Pass a path after the argument separator to open a log on startup.

```powershell
dotnet run --project src/Tailviewer.Avalonia -- "C:\logs\application.log"
```

The desktop app uses Avalonia and Tailviewer Core. Legacy WPF source remains in the repository. See the [desktop app notes](src/Tailviewer.Avalonia/README.md) and [migration specification](specs/001-full-project-migration/spec.md).

## Credits and license

Tailviewer is based on [Kittyfisto/Tailviewer](https://github.com/Kittyfisto/Tailviewer), originally developed by Simon Mießler and contributors. Licensed under the [MIT License](LICENSE).
