# T021 native entry points — in progress

## Slice 1: real Windows process, picker and clipboard

The legacy acceptance project compiles WpfUnit fixtures against the old desktop
application. Its project and fixtures remain unchanged until T117. An isolated
runner under its Migration directory compiles only SourceEntryPointTest.cs and
reuses NUnit 3.12.0, adapter 5.0.0 and Test SDK 17.14.1 already used in the repo.
Windows Desktop/WPF is test-only for built-in UI Automation and clipboard APIs;
the Avalonia application and shipping dependency graphs do not acquire WPF.
No UI automation package or production feature was added.

The runner builds the actual Avalonia app through a non-reference project edge
and copies its ordinary SDK output/sidecars into the runner output after Build.
Tests launch that executable as an owned external process. They use native UI
Automation peers/patterns and the actual Windows file dialog/clipboard, not the
headless platform, an in-process MainWindow, or a fake storage provider.

Three interactive Windows x64 cases pass:

- A quoted Unicode/space-containing command-line file path opens, the second row
  is selected through SelectionItemPattern, and InvokePattern copy sends its
  exact whitespace/Burmese/emoji text to the system clipboard.
- Opening and cancelling the native picker preserves the current window/source
  and selected row, verified by copying that original row afterwards.
- Typing an owned Unicode path into the native picker's filename control and
  invoking Open activates that file and copies its different expected row.

The initial picker query failed because it searched desktop siblings only.
The corrected finder also searches owned-window descendants; no assertion was
weakened or timeout extended. Final fixture: 3 passed, 0 failed, 0 skipped.
The initial compile error (missing System.IO import) is not behavioral evidence.

## Ownership and cleanup

Each case uses a GUID temp directory and its own process handle/PID. Cleanup
closes or, after a bounded wait, kills only that owned process/tree. Recursive
temp cleanup checks the resolved parent before deletion. An observed post-run
audit found zero remaining harness app processes and zero native fixture dirs.

Tests run nonparallel on an STA thread. Before changing the clipboard they copy
all accessible original formats into an in-memory DataObject; unsupported/null
format snapshots fail before alteration. After owned fixture output, cleanup
restores the snapshot. Unexpected externally changed clipboard text is retained.
Original clipboard data is never written to files/logs or assertion messages.
The clipboard remains a shared desktop resource; these are interactive tests,
not a claim of isolation from concurrent user input. Current app profile storage
is not connected; revalidate profile isolation when T053 wires persistence.

## Reproduce and remaining scope

Run from an interactive Windows desktop:

```powershell
dotnet test src/Tailviewer.Acceptance.Tests/Migration/Tailviewer.Native.Acceptance.Tests.csproj -c Release
```

The native runner is deliberately separate from the default headless solution;
it must not be presented as having run in a noninteractive CI job. T117/T121 must
consolidate and include the appropriate explicit native acceptance job. This
runner does not port the full legacy acceptance project or test every UI control.

Ignored executed evidence: artifacts/migration/t021-native-first.log (one pass /
one picker-finder failure), t021-picker-focused.log (one pass),
t021-native-final.log and t021-native/t021-native-final.trx (three passes).
Existing legacy log4net advisory diagnostics remain visible during app build.
Core/headless suites were not repeated because production sources were unchanged.

T021 stays unchecked: native drag/drop, multi-file/folder entry points and
single-instance handoff/owned-process cleanup scenarios still need work alongside
T024/T025. Native keyboard, text-range drag, accessibility and full US1 acceptance
are not implied by these three passing cases.
