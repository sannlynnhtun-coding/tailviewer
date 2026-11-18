# Tailviewer

Tailviewer is an open-source log file viewer. This repository is migrating the original WPF application to **.NET 10 and Avalonia**.

The current Avalonia application is a **working prototype**, not a stable release or a complete replacement for the legacy application. It uses the migrated Tailviewer Core to index and tail local log files.

## Current prototype

- Open one local log file through the file picker or a command-line argument.
- Scroll vertically and horizontally through a virtualized list of log lines.
- Select whole lines and copy their original text in file order, without display line numbers.
- Follow live appends, including updates to an unfinished last line.
- Pause following when scrolling or selecting; enable **Follow tail** to return to the end.
- Read UTF-8 by default and detect byte-order marks, including UTF-16.
- Recover from detected same-size rewrites, rename rotation, truncate-and-regrow, and a file disappearing and reappearing.

The UI reads text in background batches and caches up to 1,024 lines. It keeps an integer index per line rather than loading the entire file into a text control.

## Run from source

Requirements: the **.NET 10 SDK** matching [global.json](global.json), currently pinned to **10.0.302** with patch roll-forward. The prototype uses **Avalonia 12.1.6**.

```shell
git clone https://github.com/sannlynnhtun-coding/tailviewer.git
cd tailviewer
dotnet run --project src/Tailviewer.Avalonia
```

To open a file immediately:

```shell
dotnet run --project src/Tailviewer.Avalonia -- "C:\logs\application.log"
```

Use the appropriate local file path on Linux or macOS. Those platforms have not yet been validated for this application. No packaged Avalonia release is currently published.

## Controls

| Action | Control |
| --- | --- |
| Open a file | **Open file…** or Ctrl/Cmd+O |
| Select lines | Click; Shift for a range; Ctrl/Cmd for multiple selections |
| Copy selected lines | **Copy selected** or Ctrl/Cmd+C |
| Follow new output | Enable **Follow tail** |
| Read earlier output | Scroll or select a line to pause following |

Selection is line-based; character-level selection is not implemented.

## Build and test

Run these commands from the repository root:

```shell
dotnet build Tailviewer.Avalonia.slnx --configuration Release
dotnet test Tailviewer.Avalonia.slnx --configuration Release
```

To run the migrated Core suite separately:

```shell
dotnet test src/Tailviewer.Core.Tests/Tailviewer.Core.Tests.csproj --framework net10.0
```

Current validation on Windows: **13 Avalonia headless integration tests passed**, plus **6,641 Core tests passed** with 5 existing skips. Integration coverage includes file reading, Unicode/BOM, scrolling and virtualization, selection/clipboard, live tailing, replacement/rotation, error recovery, and closing during a pending read.

## Known limits

- Windows native rendering and the file picker still need manual verification; desktop capture timed out during automated verification.
- Linux/macOS execution and 100 MB/1 GB performance comparisons have not been completed.
- Rotation detection checks metadata and the first/last 4 KiB of previously read content. Rewrites outside those sampled regions can be missed if metadata does not distinguish them. Reopen the file to force a fresh index.
- A rotated file continues to use the encoding detected when the file was opened. Reopen it if the encoding changes.
- Copying a large selection requires memory for the selected text.
- Search, filtering, merged sources, bookmarks, parsed columns, plugins, and an installer have not yet been migrated into the Avalonia prototype.

API/Core temporarily target both .NET Framework 4.5 and .NET 10 to support the remaining legacy projects. Multi-target restore may report the existing `NU1902` advisory for legacy `log4net 2.0.12`; the Avalonia executable uses `log4net 3.6.0` through its .NET 10 dependency graph.

## Migration documentation

- [Migration plan and dependency compatibility audit](docs/AvaloniaMigrationPlan.md) — Burmese
- [Prototype usage and implementation notes](src/Tailviewer.Avalonia/README.md) — Burmese
- [Legacy plugin development guide](docs/DevelopingPlugins.md)
- [Legacy contribution guide](CONTRIBUTING.md)

The original WPF application remains under `src/Tailviewer` with its original solutions. Its search, filtering, merging, bookmarks, and plugin features should not be assumed to exist in the Avalonia prototype. External plugins and consumers need rebuilding for the changed API/Core contracts.

## Credits and license

Based on [Kittyfisto/Tailviewer](https://github.com/Kittyfisto/Tailviewer), originally developed by Simon Mießler and contributors. This repository preserves the original Git history and attribution.

Licensed under the [MIT License](LICENSE).
