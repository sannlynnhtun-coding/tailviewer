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

## Slice 2: multi-file picker and startup arguments

Two new real-process cases initially failed because the command line discarded
all but its first path and the native picker allowed only a single file. The app
now passes every startup path through MainWindow.OpenFilesAsync. The picker
allows multiple files and resolves all returned local paths before applying any.
Both callers reuse the existing file/folder opening and stable-identity logic.
The native multiple-picker case includes the already configured first file,
asserts exactly two root sources and copies the last file's different content;
this catches duplicate source creation as well as silently discarded paths.

Batch opening snapshots its input before mutation, applies paths in order and
retains errors from all failed paths even if a later path succeeds. Empty input
leaves the current source/selection/follow state alone. Each awaited open is
fenced by the existing request counter: close-all, closure or another user open
invalidates the remainder instead of allowing a late batch to revive sources.
Three headless cases cover empty input, partial success with two reported missing
paths, and close-all during an outstanding batch. Picker cancellation still
passes its native preservation test.

Final results: 5 native tests, 85 Avalonia tests and 81 Archiver tests passed with
zero failures/skips in these suites. Evidence: artifacts/migration/
t021-multiple-red.log and t021-native/t021-multiple-red.trx (two meaningful
failures), t021-multiple-native.log, t024-multiple-modern.log,
t021-multiple-native-final.log and t021-native/t021-multiple-final.trx.
The full Core suite was not repeated for this UI/startup-only change.

T021/T024 remain open: folder picker, drag/drop and single-instance handoff still
need integration/acceptance. Startup file/folder path arguments are covered here;
legacy plugin-loader command modes are not claimed as ported by this change.
## Slice 3: native folder picker

Open folder is now an accessible toolbar command. It shares the file-open enabled
state through an Avalonia binding. A common picker method checks the appropriate
provider capability, allows multiple selections, validates all local paths and
uses the existing fenced batch opener. Existing folder-watch/member identities,
file copying and empty-selection cancellation behavior are reused, not duplicated.

Two new native cases first failed because the folder command did not exist.
After wiring it, cancellation passed; opening initially failed in the test finder
because Windows exposes the folder-name edit as ID 1152 rather than the file
picker's 1148 combo. The shared native fixture now supports both controls. It
opens the owned folder, verifies the original configured file remains alongside
it, waits for the discovered child log, selects that child and copies its exact
Unicode/whitespace content. Cancellation preserves the original source/selection.
Multiple-folder selection is enabled but was not separately exercised here.

Final native fixture: 7 passed / 0 failed / 0 skipped. Modern solution: 81
Archiver + 85 Avalonia passed / 0 failures/skips. The existing light/dark branding
render test now additionally verifies the folder command is visible and the full
toolbar fits the 640-pixel minimum width. An unrelated empty-file Core race found
in the first broader run was fixed separately; see recovery-tests.md slice 4.

Evidence under artifacts/migration: t021-folder-red.log,
t021-native/t021-folder-red.trx, t021-folder-focused.log,
t021-folder-native-final.log, t021-native/t021-folder-final.trx and
t024-folder-modern-final.log. The original failed broader checkpoint is retained
as t024-folder-modern.log; it is not passing evidence.

T021/T024 remain open for drag/drop and single-instance handoff integration and
acceptance. This does not claim the legacy menu system, every folder picker
platform, or all keyboard/accessibility flows are complete.
## Slice 4: file/folder drop event handling (native OLE still pending)

The window now enables dropping and handles enter/over/drop through Avalonia 12
IDataTransfer/DataFormat.File, not the obsolete data-object API. Read-only opening
prefers the legacy Link effect, falls back to allowed Copy, and rejects Move-only
sources. Plain text paths and unsupported formats are rejected. All storage items
are resolved to local path strings before the first await, while the transfer is
still valid; a malformed provider cannot apply a valid prefix before failing.
The existing fenced batch opener preserves identities, watchers and partial-open
error handling. Drag-format failures remain rejected without repeated pointer-move
error messages; drop extraction failures report an actionable status.

Five new headless cases cover two-file repeat drop without mutation/duplicates,
text rejection, folder/file watcher reuse, Link/Copy negotiation/Move rejection,
and a deferred throwing provider after a valid item. The first two failed before
wiring the handlers. One broader run exposed an insufficient test completion
predicate on the repeated drop: the previous active source already satisfied it
while the next batch was still opening. The predicate now also waits for the open
command to be re-enabled; no timeout was increased or assertion removed.

Final modern solution: 90 Avalonia + 81 Archiver pass, zero failures/skips.
A real-process command-line/native-clipboard smoke also passes after enabling
the platform drop target. That smoke does not exercise Windows OLE drag delivery.
The full Core suite was not repeated for this window-only wiring change.

Evidence under artifacts/migration: t024-drop-red.log (two meaningful failures),
t024-drop-focused.log, t024-drop-validation.log, t024-drop-modern.log (observed
test-wait failure), t024-drop-modern-final.log, t024-drop-native-smoke.log and
t021-native/t024-drop-native-smoke.trx. The earlier t024-drop/t024-drop.trx is the
failed checkpoint, not the final passing result.

T021/T024 remain open: a real cross-process/native OLE file/folder drop and
single-instance handoff are still required, as are their preceding source-view/
group integration dependencies. No managed RaiseEvent test is counted as native
OLE acceptance. Multiple-folder picker acceptance remains separately unproven.
## Slice 5: real cross-process Windows OLE drop

Two additional native cases now drive an owned WPF source window with native
mouse input and System.Windows.DragDrop.DoDragDrop into the separately launched
Avalonia process. They never call its managed RaiseEvent API. The source carries
native file-drop data containing an owned folder and Unicode file path; repeating
the gesture leaves exactly three configured roots and the source files unchanged.
The last file becomes active and its original Unicode row reaches the system
clipboard. A text-only native drag returns None and preserves source/selection.
The source logs actual feedback/query callbacks: final focused observations were
2/2 with Link on each file/folder gesture and 2/2 with None for text rejection.
This follows the [Windows OLE source/target lifecycle](https://learn.microsoft.com/en-us/windows/win32/api/ole2/nf-ole2-dodragdrop).

The helper checks released mouse/modifier state and owned-process hit targets,
jumps directly between owned source/target windows, releases its own button state,
closes its source window and restores the cursor if it still occupies the last
position set by the helper. Startup/feedback waits and input-worker completion are
bounded. Failure posts Escape specifically to the owned STA queue, so cleanup can
run even before native mouse capture starts. Tests still require a controlled
interactive desktop; this does not promise isolation from concurrent user input.
The existing fixture retains its clipboard snapshot/restore and owned app cleanup.

The first experimental direct-OLE helper did not start normal source feedback and
was not adequately bounded. Its stuck run was cancelled through the verified
owned STA queue (one owned app process was also terminated); fixture cleanup then
ran. It is not shipped or passing evidence. A stack-only diagnostic tool was
installed under ignored artifacts/migration/diagnostics; no heap/clipboard dump
was collected. The replacement uses the normal WPF source gesture. One text case
then exposed waiting for feedback before moving the pointer; moving directly to
the owned target before waiting resolves that ordering. No test was skipped or
assertion timeout increased to hide these failures.

Final full native suite: 9 passed, 0 failed, 0 skipped. A post-run ownership audit
found zero remaining native harness app/testhost processes and zero fixture dirs.
Evidence: artifacts/migration/t021-ole-first.log (initial focus-query failure),
t021-ole-focused.log (cancelled experimental helper), t021-ole-stack.log,
t021-ole-gesture.log (one pass / one startup-order failure),
t021-ole-gesture-focused.log (two passes), t021-ole-native-final.log and
t021-native/t021-ole-final.trx (nine passes). Production code was unchanged, so
Core/headless suites were not repeated in this test-only slice.

T021 remains open for real-process single-instance handoff. T024 also retains its
source-view/group integration dependencies. Native multi-folder picker acceptance,
Explorer-driven gestures, mixed DPI/monitor layouts and the full accessibility/
keyboard matrix remain unproven; the tested source is the owned WPF window.

## Slice 6: native secondary-instance file and restore handoff

The new native case starts the real shipping app as an owned primary, launches
an owned secondary with a relative Burmese filename, and verifies secondary exit
code zero, both source roots, the correct active file and original native
clipboard text. A further no-argument secondary restores the same minimized
primary PID/window and preserves selection/copy. Each secondary has bounded exit
checks and finally cleanup restricted to its own Process object.

Fixture preconditions now reject existing apphost processes and an occupied
user/session ownership channel before clipboard or workspace mutation. The native
runner links the production ownership helper for that probe, rather than
duplicating its channel algorithm. Both probes release/dispose their own handles.

Focused new case: 1 pass. Complete native suite: 10 pass / 1 fail / 0 skipped.
The failure was the second gesture in the existing file/folder OLE drop case:
its source/target ownership guard detected an occluded point before sending any
mouse down (completed false, feedback/query zero for that gesture). The first
gesture had returned Link normally. No safety check, assertion or timeout was
weakened. A focused recheck of both OLE cases plus the new handoff case passes
3/3 with no production/test changes. Retain the full-run failure as a desktop
occlusion/flakiness observation; this is not an uninterrupted full-suite pass.

Evidence: artifacts/migration/t025-startup-native.log,
t025-startup-native-full.log, t025-startup-native-recheck.log and matching TRX
files under t021-native. After those runs, no app/testhost processes or
tailviewer-native fixture directories remained. Full modern suite separately
passed 81 Archiver + 129 Avalonia cases.

T021/T025 remain open for crash/unresponsive-primary/error-window acceptance,
foreground/off-screen/hidden-window behavior, broader isolation and preceding
source implementation dependencies. Native service/install/update behavior and
cross-user/session security were not exercised here.

## Slice 7: primary exit/crash recovery and timeout UI (T021 complete)

Two new native cases close or terminate only the fixture-owned primary, then
start a new primary, verify original Unicode clipboard text and successfully
hand off an empty request. A non-owning witness handle retains the named kernel
mutex across exit; the crash case therefore exercises abandoned ownership rather
than merely creating a new mutex after the old object disappeared. Graceful exit
must return zero, and source bytes remain unchanged.

A third case holds an owned user/session lease without a pipe listener. A real
secondary reaches its production connection deadline, displays an actionable
connection-error window with no log workspace, and exits one through its Close
button. The fixture's lease remains held; the app neither takes over nor kills
its owner. All application handles and clipboard/temp-directory cleanup use the
existing ownership boundaries.

New focused cases: 3 pass / 0 failed / 0 skipped. Complete native entry-point
suite: 14 pass / 0 failed / 0 skipped, covering both startup-file variants,
picker open/cancel/multiple paths, folder picker open/cancel, cross-process OLE
file/folder and invalid-text drops, Unicode clipboard, relative-path handoff,
restore, graceful/crash restart and unresponsive-channel errors. No OLE helper,
timeout or ownership assertion was changed to obtain this result. The preceding
full-run occlusion failure remains recorded in slice 6 as a desktop flakiness
observation; this new run is a complete passing checkpoint.

Evidence: artifacts/migration/t025-lifecycle-native.log,
t025-lifecycle-full.log and t021-native/t025-lifecycle*.trx. Production code did
not change, so modern/Core suites were not repeated for these test additions.
Post-run audit found no remaining app/testhost processes or native fixture dirs.

T021 is complete as a native test-contract task with its T017 dependency met.
This is not completion of T024/T025 or US1: source/group implementation and
foreground/off-screen/hidden-window behavior remain open. It does not establish
cross-user/session security, installer/service behavior or whole-app accessibility.

## Slice 8: real pointer source-tree arrange/group/Escape

Three native cases now send physical pointer input within the owned app to move
the second file before the first, group it onto the first, or cancel the same
grouping gesture with Escape. They check native root/member order, original
Unicode clipboard text and unchanged file bytes. Source names are resolved
through actual TreeItem peers and coordinates through their rendered text peers.

DragWithin reuses the native helper's existing input/PID/cursor functions. It
rejects held mouse buttons/modifiers, verifies both points belong to the owned
process, calibrates mouse-down/move/release delivery with 50 ms intervals and
checks external cursor changes. Escape is posted only to the owned window;
finally releases only the injected held button and restores cursor position only
when it remains at the helper's last point. No external application or file
transfer is used for these internal tree gestures. Window DPI is recorded.

Initial cases stopped before dragging because full-path text-peer lookup was
incorrect: native text peers exposed basenames and TreeItem peers had empty
names. That inspection identified a genuine accessibility gap, fixed by naming
source containers and updating names after group rename. Center grouping and
cancel then passed, but top-edge arrange still left roots unchanged. The same
point passed after SourcesView received a transparent hit-testable background.
No placement band, test timeout or successful ordering assertion was weakened.
Temporary peer/bounds diagnostics were removed; failed checkpoints are retained.

Final complete modern suite: 81 Archiver + 165 Avalonia pass. Complete native
suite: 17 pass / 0 failed / 0 skipped. All three new cases record 168 DPI. Audit
after the run found no remaining app/testhost processes or native fixture dirs.
Evidence under artifacts/migration: t023-pointer-native.log,
t023-pointer-peers.log, t023-pointer-native-fixed.log,
t023-pointer-order-diagnostic.log, t023-pointer-hit-area.log,
t023-pointer-modern.log, t023-pointer-native-full.log and
t021-native/t023-pointer-native-full.trx (ignored).

This proves the three gestures at the observed DPI, not a mixed-monitor matrix,
native edge scrolling/hover expansion, all cross-parent gestures or full screen
reader/keyboard coverage. T023 and full migration remain open. Shared Core code
was unchanged, so its complete suite was not repeated.

## Slice 9: native collapsed-group hover expansion

An additional real-process case creates an empty group through the actual New
group button, pointer-drags one owned file into it, collapses it through the
native ExpandCollapse pattern, then drags another root onto its header. The
helper keeps mouse down while a bounded UIA observation waits for Expanded;
it never invokes Expand itself during the gesture. Only after expansion is
observed does it release the mouse. The test checks the two ordered member
paths, root count, original Unicode copy and unchanged bytes of all three files.

DragWithin accepts an optional bounded observation while holding capture; its
existing PID, external-cursor, modifier, release and restoration guards remain.
Existing pointer cases use the unchanged no-observation path. Initial focused
hover case passes 1/1; final native pointer suite (before/group/Escape/hover)
passes 4/4 with no failures/skips. The owned windows record 168 DPI. Post-run
audit finds no remaining app/testhost processes or native fixture directories.

Evidence: artifacts/migration/t023-hover-native.log,
t023-hover-pointer-native.log and matching TRX files under t021-native (ignored).
Production sources were unchanged, so modern/Core suites and the full native
picker/OLE/handoff suite were not repeated. The previous complete native
checkpoint remains 17/17; this is a focused four-case passing checkpoint.
Native edge scrolling, mixed-monitor DPI and the full gesture/accessibility
matrix remain pending. T023 and full migration stay open.

## Slice 10: native captured-drag edge scrolling and cancellation

The new native case opens 41 owned three-line files, selects/copies the first,
then holds a physical drag at the tree's bottom edge. A bounded native UIA
observation requires the original source to leave the viewport while another
source becomes visible before Escape is posted. After cancellation, the first
visible row's identity and physical top remain unchanged over 350 ms (more than
three production refresh periods). All root paths retain their original order
and every fixture file's original bytes are checked. No production changes or
new input helper were required.

Focused new case: 1 pass. Combined native pointer/hover/edge cases: 5 pass /
0 failed / 0 skipped, with owned-window DPI recorded as 168. Cleanup audit finds
no remaining app/testhost processes or native fixture directories. Evidence:
artifacts/migration/t023-edge-native.log, t023-edge-pointer-native.log and
matching TRX files under t021-native (ignored).

This is vertical edge/cancel acceptance at the observed DPI; horizontal edge,
mixed-monitor movement and the complete cross-parent/keyboard/accessibility
matrix remain unproven. T023 remains partial. Modern/Core and the full native
picker/OLE/handoff suite were not rerun for this test-only addition; the previous
complete native checkpoint was 17/17 and later focused hover coverage is separate.

## Five-minute native live-tail gate (T027, not yet passing)

Added an explicit Performance-category case using the same paced producer as
the Core harness: 100 LF-terminated numeric records per 100 ms for 300 seconds.
It requires the owned Windows x64 app to remain foreground. UIA visible numeric
text supplies batch observations; unavailable/COM observations are reported and
all pending batches retain their original flush times, including across failed
observations. All 3,000 batches must meet the unchanged nearest-rank p95 budget
of 1,000 ms. Accessibility visibility is not a pixel presentation timestamp.

The test then uses owned Ctrl+A and Copy selected to compare every record and
the terminal newline through the actual clipboard, and appends another record
to verify paused follow/selection. Those final checks have NOT executed in a
complete five-minute run yet. The existing small native terminal-newline test
separately exercises owned Ctrl+A/copy; its prerequisite commit is 1a6490f1.

Initial integration found a missing System.IO import in the linked producer and
an unsupported window SetFocus; the fixture now focuses the log list, as the
existing keyboard helper does. Initial UIA traversal failed with E_FAIL during
live changes. Poll failures are now recorded without crediting success or
discarding delayed batches. The subsequent run reached 180 seconds / 180,000
written records / 1,798 observed batches, then failed its foreground ownership
guard at about 188 seconds. It is an interrupted, unqualified run, not a pass
or an inferred p95 result. Cleanup left no owned app/testhost processes.

Evidence: artifacts/migration/t027-native-tail.log and matching TRX directory;
the attached native-live-tail-48ac8f0ceb8a4aab99ae7c68f5f28ce6.jsonl retains
numeric observations and failed UIA HRESULTs. The earlier UIA failure is kept in
t027-native-tail-uia-failure.log. The shared producer tool builds with zero
errors and two inherited NU1902 warnings. Reproduction command and boundaries
are in tools/Tailviewer.SourcePerformance/README.md. T027 and full migration
remain open; no performance threshold, timeout or focus guard was relaxed.

The subsequent focused terminal-newline regression also failed its existing
ten-second native EOF-row observation (t027-native-tail-keyboard.log/TRX), before
keyboard/copy validation. Its previously recorded passing prerequisite is not
evidence of a passing current native run. No production code changed in this
slice; this desktop observation failure remains to diagnose. Both fixtures
completed cleanup and left no native fixture directories. The Archiver suite
was independently rechecked at 81 pass / zero failures or skips in this turn.
