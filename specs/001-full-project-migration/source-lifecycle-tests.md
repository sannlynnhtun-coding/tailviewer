# T018 — Source lifecycle contracts (in progress)

## Slice 1: executable file lifecycle baseline

Read the legacy IDataSources/DataSourcesTest and FolderDataSourceTest contracts
before adding modern tests. Legacy file/folder/group operations, folder pattern
and recursive settings, and removal remain the source behavior baseline.

Five new ordinary Avalonia headless tests in SourcesTest.cs passed:

- Opening a file preserves original whitespace on copy; switching shows and
  copies only the newly active file without modifying the original.
- A missing file open leaves current rows/title and original text usable.
- Opening after window closure cannot publish rows into the closed window.
- A missing configured file retains its path/ID and can open after it appears.
- Two group members with identical display names open their own files by ID.

Each case owns disposable GUID-directory files and closes/disposes its windows
and documents. Clipboard assertions reset to an owned sentinel before copying,
so old clipboard contents cannot satisfy the expected result accidentally.
These use the headless clipboard, not the native Windows clipboard.

The group case exercises the existing configuration-to-LogDocument boundary;
it does not prove that a group tree, group switching, exclusion or close commands
exist in the UI. The current prototype still has one visible active file.

Executed with SDK 10.0.302 on Windows x64:

- Filtered SourcesTest run: 5 passed, zero failures/skips.
- Final complete Avalonia suite: 28 passed, zero failures/skips.
- No production code, package pins or timeout values changed in this slice.

Evidence: ignored artifacts/migration/t018-source-baseline.log,
t018-prototype.log and t018/t018-source-baseline.trx.

## T018 remains open

Still add executable folder enumeration/recursive-pattern/missing-folder,
source-tree/group operations, close-current/all, workspace switch/retention,
cycle/duplicate activation feedback and persisted-workspace-ID cases. Existing
Core graph tests are supporting evidence, not a substitute for those application
contracts. T022/T023 will supply the missing legacy business/UI integration;
T024 covers multi-file/folder picker/drop/argument wiring. Do not mark T018 or
US1 complete based on this file-only baseline.
## Slice 2: workspace/folder gaps captured behaviorally

Added three ordinary headless cases using the existing MainWindow and real
settings/document APIs. No missing type/compiler placeholder or production stub
was introduced:

- Saved/reloaded selected source retains its original ID and copies the selected
  file's actual content: passed.
- Reopening the same canonical path must keep one selectable source-tree entry:
  failed because the current window has no source tree.
- A folder path must create a folder source while retaining the open file and
  exposing its matching logs: failed because the current file-only entry point
  rejects the directory. The later assertions pin two root entries and three
  aggregate log entries for the owned two-file fixture; those assertions have
  not executed yet and do not constitute folder-enumeration evidence.

Executed SourcesTest result: 6 passed, 2 failed, 0 skipped (8 total).
Evidence: ignored artifacts/migration/t018-source-contracts-red.log and
artifacts/migration/t018/t018-source-contracts.trx. The new cases are normal
net10.0 tests, so the ordinary Avalonia suite is currently expected to fail on
these two missing features until source-tree/folder integration is implemented.
The earlier 28-case passing checkpoint predates these new contracts.

T018 remains unchecked: close-current/all, group UI, recursive patterns/missing
folder handling and cycle/duplicate UI feedback still need executable contracts.
No UI implementation, full-suite green claim or US1 completion is implied.
## Slice 3: selectable file sources implemented

The first implementation slice now supplies the source tree used by the existing
red file contract. It binds existing DataSource models, retains file identities
by canonical full path (Windows case-insensitive comparison), and lets users
select previously opened files. Reopening a normalized equivalent path reuses
the entry/ID. Labels retain full-path tooltips/accessibility names. Switching
reuses the configured ID through LogDocument and disposes the previous document.
The existing FollowTail field preserves the paused/following choice when returning
to a source; new-file initial follow behavior is unchanged.

A new actual-tree selection test checks return to the earlier file, correct
original copied text, stable ID, normalized-path duplicate avoidance and retained
pause state. No property assertion was weakened and no test was skipped.

Executed final full Avalonia suite: 31 passed, one failed, zero skipped (32 total).
TRX confirms the only remaining failure is
OpeningAFolderRetainsTheFileAndAddsItsMatchingLogs. The earlier missing-tree
failure now passes. Evidence: artifacts/migration/t018-file-tree-full.log and
t018/t018-file-tree.trx, ignored local artifacts.

This is a test-first vertical file slice of T018/T022, not completion of either
task. The list is currently file-only and retained in the running window; saved
workspace binding, folder/group enumeration and controls, loading/error tree
states, close-current/all, stale open-request ordering and complete per-source
selection/position persistence remain their assigned tasks. The expected folder
failure remains ordinary and visible in the suite. No native desktop/clipboard
acceptance or full Core rerun was performed for this UI-only change.
## Slice 4: live folder discovery and child navigation

Ported the folder discovery portion of legacy FolderDataSource into Core
Sources/Migration/FolderSources.cs. It reuses the installed IFilesystem watchdog,
semicolon-separated case-sensitive/unanchored '*' rules, recursive configuration
and disposal ownership. The helper retains the folder configuration and derives
child IDs from folder ID plus normalized path, so removal/reappearance and reopen
do not assign new identities. It reports missing folders/bounded pattern failures
without writing logs or dropping the configured root. Ordinary status reads do
not enumerate directory contents. A blank pattern accepts all files, as in the
legacy implementation. MainWindow uses fresh DataSourceSettings defaults until
actual workspace/settings binding is implemented; it does not silently load a
real user profile or invent new pattern/recursion defaults.

Folder nodes now expose live selectable child files in the source tree. Opening
a folder preserves existing root files, and selecting a child opens its configured
identity without adding a duplicate root entry. Membership reconciliation keeps
unchanged child models/selection/pause state. Folder probing and watcher creation
run off the UI thread; queued updates ignore closed windows. Windows own/dispose
their directory watchers and shared folder scheduler.

### Corrected baseline assumption (not an aggregate implementation)

The earlier plain-text fixture's expected aggregate count of three was wrong:
legacy MergedDataSource constructs Core MergedLogSource, whose
MergedLogSourceIndex.ComputeIndices requires timestamp != null. Raw plain-text
entries are excluded from that merged representation. This evidence is in
BusinessLogic/DataSources/MergedDataSource.cs and Core/Sources/Merged/
MergedLogSourceIndex.cs; no merge behavior was changed to satisfy a UI test.
The folder contract now checks both discovered child paths and selects/copies
actual original text from each file, retaining the separate root file. Folder
aggregate/timestamp parsing/provenance remains the assigned US2 work; the folder
node explicitly asks users to select a child instead of pretending to aggregate.

### Executed checks

- Eight real-filesystem Core cases: recursive/nonrecursive semicolon patterns,
  legacy unanchored/case-sensitive/literal-question/pipe behavior, blank pattern,
  missing folder recovery and stable child IDs across removal/reopen.
- Migration suite checkpoint: 191 passed, zero failures/skips.
- Modern solution checkpoint: 81 Archiver + 33 Avalonia passed.
- Final full Avalonia suite after async/lazy watcher setup: 33 passed, zero
  failures/skips. Includes actual child selection/copy and live insertion/removal
  while another child remains selected with its original ID and paused follow.
- Final focused Core helper rerun after removing an unused snapshot allocation:
  all eight passed. No package, timeout or skipped-test changes.

Evidence: ignored artifacts/migration/t018-folder-core.log,
t018-folder-migration-final.log, t018-folder-solution-final.log,
t018-folder-ui-final.log and t018/t018-folder-ui.trx.

The prior ordinary source-tree/folder failures are resolved for this slice.
T018/T022 remain unchecked: group/close-current/all contracts, complete source
view/loading/error bindings, saved workspace integration and group/aggregate UI
remain pending. Full Core, native desktop/clipboard, large-folder cost, junctions
and SMB were not validated by this slice. Discovery child snapshots are still
rebuilt per watcher change; broader bounds/performance remain acceptance work.
## Slice 5: close-current/all and pending-open invalidation

Added sidebar commands with distinct accessibility names/mnemonics. Closing a
root disposes the active document, removes only workspace configuration and the
owned folder watch/children, then activates the remaining first root. Closing all
clears rows/selection and disposes every folder watch while leaving the shared
scheduler available for a new workspace. Physical files are never deleted by
these commands. Folder-managed children cannot be closed individually, matching
legacy FolderDataSource's ownership rule; their parent folder must be closed.

A monotonically increasing open-request generation now rejects late success and
error completions after close, window shutdown or a newer open. Detached documents
and directory watches are disposed rather than attached to the cleared workspace.
Programmatic tree selection no longer starts a redundant open. Queued folder
updates check membership before accessing removed dictionary entries. This is
result invalidation, not forcible cancellation of already-running filesystem IO.

Four initial new command contracts failed meaningfully before implementation;
they now pass. Two further cases cover last-root cleanup/exclusive file-handle
reacquisition and closing a folder while retaining its standalone root file.
The temporary-directory cleanup assertion also verifies the resolved parent
before recursively deleting only the test-owned directory.

Executed evidence:

- Initial source contracts: 10 passed / four expected missing-command failures.
- Modern solution checkpoint: 81 Archiver + 38 Avalonia passed.
- Final complete Avalonia suite with the folder-close case: 39 passed, zero
  failures/skips; final source-fixture rerun after cleanup guarding: 16 passed.
- Tests retain original file bytes, disable empty-workspace commands, reopen
  after close-all, keep discovered children under their folder, and verify an
  overlapping open cannot undo close-all. The overlap test allows actual task
  scheduling orders; it does not claim a forced stalled IO/process scenario.

Ignored evidence: artifacts/migration/t018-close-contracts-red.log,
t018-close-contracts.log, t018-close-solution.log, t018-close-ui-final.log,
t018-close-sources-final.log and t018-close/*.trx.

T018/T022/T023 remain incomplete: group activation/cycle/duplicate UI feedback,
include/exclude/rename, complete loading/error source adapters and saved workspace
binding remain pending. Native desktop/clipboard acceptance, full Core rerun and
slow/network IO cancellation were not performed for this UI slice.
## Slice 6: ordered groups and T018 contract completion

Added SourcesViewModel as the configured-source/group owner used by MainWindow.
It reuses DataSource models, preserves ordered ID-based membership, and validates
candidate graphs through the existing Core SourceConfiguration before mutation.
Group add, idempotent membership, explicit ungroup and group removal are bound
to the source tree/new-group button/context menus. A member already owned by
another group must first be removed, matching legacy MergedDataSource.Add.
Closing groups promotes their members in order rather than discarding their
configuration; nested promotion retains applicable parent/member exclusions.

Context menus refresh current group targets in the tree's context-request tunnel,
including keyboard requests on tree item containers. This fixes the stale-menu
case when a file label was created before a group existed. Tests verify the popup
actually opens, invoke ID-tagged targets and check original copied file content.
Cycle errors persist across the normal status refresh without mutating membership.
Repeated file-open lookup includes configured group members, avoiding root copies.
Folder-managed discoveries remain under their separate folder owner.

### Requirement-to-evidence mapping

| T018 contract | Executed evidence |
|---|---|
| File open/switch/original copy | OpeningAndSwitchingFilesCopiesOnlyTheActiveSource, earlier-source selection/pause case |
| Folder membership/live discovery | OpeningAFolderRetainsTheFileAndAddsItsMatchingLogs, LiveFolderMembershipKeepsTheSelectedChildAndItsIdentity; eight Core folder pattern/recursion/recovery cases |
| Group identity/order | NewGroupCreatesANamedSelectableSourceWithAStableIdentity, ContextMenuGroupingPreservesIdsAndClosingTheGroupKeepsItsFile; ordered/idempotent model cases |
| Close-current/all | Six source close/overlap/ownership/file-release cases from slice 5 |
| Missing files/folders | MissingOpenRetainsTheCurrentRowsAndCopyableText, MissingConfiguredSourceCanRecoverWithoutChangingItsIdentity, MissingFolderKeepsItsConfiguredIdentityAndRecoversInTheTree |
| Cycles/duplicates | Self/nested context-menu rejection; duplicate configured-ID and idempotent-member cases; canonical file reopen within a group |
| Persisted IDs | SavedSelectedSourceReopensWithItsOriginalIdentityAndContent and actual group/member save/reload assertions in the context-menu case |

T018's test task is complete. It is not completion of all source implementation,
workspace restoration UI or US1 acceptance. Group aggregate/parse behavior remains
US2; group nodes currently navigate to their members. No concatenation or synthetic
timestamps were introduced to manufacture aggregate parity.

### Executed results

Initial new-group UI case: one meaningful missing-command failure before code.
Final modern solution: 81 Archiver + 53 Avalonia passed, zero failures/skips.
The Avalonia count includes 21 SourcesTest lifecycle cases, nine SourceGroupsTest
model cases, and 23 existing prototype cases. New cases cover group/member disk
round-trip, self/nested cycle feedback, missing-folder UI recovery, duplicate IDs,
ordered promotion, exclusion retention on promotion, and explicit ungroup.

Ignored evidence: artifacts/migration/t018-group-red.log, t018-group-model.log,
t018-group-ui.log, t018-group-solution-final.log and t018-groups/*.trx. Earlier
menu diagnostic/failed checkpoints remain evidence; no timeout/assertion was
weakened or failed case skipped. Native pointer/keyboard/clipboard acceptance,
full Core rerun, rename/include-exclude UI, drag reorder/automatic group dissolution,
loading/error source adapters and actual saved-workspace UI binding remain pending.
## Group inclusion/exclusion command slice

The source context menu now exposes Exclude from group / Include in group for
configured group members. SourcesViewModel validates current membership before
updating the existing parent ExcludedDataSources set. Repeated exclusion is
idempotent; ungrouped/closed identities are rejected without mutating settings.
Changing exclusion does not reopen the selected document or remove source/file
identities. Existing ungroup cleanup and promotion rules still apply.

A headless UI regression failed first because the command was absent. It now
excludes/includes through the real menu, verifies selected identity and original
whitespace copy, and saves/restores both states using ApplicationSettings in its
owned temporary directory. A model case covers idempotence, clone preservation,
ungroup cleanup and invalid/closed members.

Verification: complete modern solution 81 Archiver + 92 Avalonia pass, no skips;
after adding disk round-trip assertions, focused source-group/UI cases 11 pass.
Evidence: ignored artifacts/migration/t023-exclusion-red.log,
t023-exclusion-focused.log, t023-exclusion-modern.log and
t023-exclusion-persistence.log. No dependencies or timeouts were added.

T023 remains partial: rename, remaining group behavior and its implementation
dependencies are open. Group timestamp-aware aggregate rendering is still US2;
this slice preserves exclusion configuration but does not claim merged-output
filtering. Native menu/keyboard accessibility and full Core tests were not rerun
for this UI/model-only change. Automatic workspace persistence remains T053.
## Group rename command slice

Legacy FileDataSourceViewModel and FolderDataSourceViewModel reject renaming;
MergedDataSourceViewModel permits it. The modern source menu follows that policy:
Rename group opens an inline, automation-named Group name editor. Enter commits;
Escape or lost focus cancels. Renaming validates the current configured group,
updates its tree label/tooltip/accessibility name and selected-group title, and
preserves its ID, parent, exclusions, members and current document. Existing
navigation reads the updated name directly. No physical paths are renamed.

The headless menu test failed first because Rename group was missing. It now
commits a Burmese/emoji name, cancels a subsequent edit, verifies navigation and
original active-file copy, and saves/restores the renamed group through owned
ApplicationSettings XML. A model test covers nested membership/exclusions and
rejects files, folders and removed groups without changing their paths.

Complete modern solution: 81 Archiver + 94 Avalonia pass, zero failures/skips.
Ignored evidence: artifacts/migration/t023-rename-red.log,
t023-rename-focused.log and t023-rename-modern.log. T023 remains open for its
remaining dependency/group behavior requirements; timestamp-aware aggregation
remains US2. No native editor/accessibility acceptance or full Core rerun was
performed for this UI/model-only slice; automatic workspace persistence is T053.
## Source tree state presentation slice (T022)

SourcesView now presents a separate automation-named Source status beneath each
heading. Existing UI refresh and folder discovery supply empty-group, empty-folder,
unavailable-folder, active-file loading/empty/read-error and configured-open-error
states. Empty/error text clears after append or successful recovery; failed
configured opens retain their ID/path and the prior copyable document. Loading
markers are fenced by the existing open-request lifecycle and cleared on group
activation, cancellation and close. Closing sources/all or removing watched
children releases retained status entries.

A behavior regression failed first at the missing empty-group tree state. It
now verifies empty file/group/folder states, folder disappearance/recovery,
append, active file deletion/recovery and original copy. A second case verifies
an unresolved configured tree item, retained error/identity and successful retry.
Full modern suite: 81 Archiver + 95 Avalonia pass, zero failures/skips. After the
second test was added, both focused recovery cases pass (production unchanged).
Evidence: ignored artifacts/migration/t022-states-red.log,
t022-states-focused.log, t022-states-modern.log and t022-states-recovery.log.

T022 remains partial: inactive files retain the last observed status until
reopened (documented shortcut); the later multi-document/parser pipeline must
monitor them. Initial new-path opens still publish configured tree items only
after successful construction. The transient loading marker is wired to actual
Core processing progress but was not timing-asserted in this slice. Native
screen-reader/layout and representative many-folder performance acceptance were
not executed. No full Core rerun was needed for these UI-only changes.

## Single-member group dissolution slice (T023)

Legacy DataSourcesViewModel.DragFromGroup / DissolveGroupIfNecessary removes a
group only when moving a member out leaves exactly one child, promoting that
child at the group's original root position. Empty groups are retained. The
modern Remove from group command previously left that single-child container.

Three model cases pin zero/one/two remaining members, surrounding root order,
stable IDs/parents, exclusion cleanup and SourceConfiguration validation. The
one-remaining-member case failed before the fix; the other two were passing
controls. Ungroup now reuses the existing Remove promotion logic when exactly
one member remains. This is one production line; closing/removing a source does
not gain an unrelated automatic-dissolution rule.

A headless menu case groups two real owned logs, removes the selected member,
verifies ordered promotion of both original identities, then selects and copies
both original contents without deleting/changing either file. Focused cases:
4 pass. Complete modern solution: 81 Archiver + 134 Avalonia pass, no failures
or skips. Evidence under ignored artifacts/migration: t023-dissolve-red.log,
t023-dissolve-focused.log and t023-dissolve-modern.log.

T023 remains partial for its implementation dependencies, tree drag/reordering
and aggregate behavior. Native menu/keyboard/accessibility, full Core and the
native entry-point matrix were not repeated for this model/menu-only change.

## Same-parent source ordering slice (T023)

Legacy DataSourcesViewModel.DropToArrange inserts sources before/after their
destination. SourcesViewModel now supports before/after ordering within an
existing root/group container using AvaloniaList.Move. The source menu exposes
Move up / Move down, disabled at the respective boundaries. Moving the active
source retains its current document, selected rows and paused follow state;
moving a different source activates it through the existing source-open flow.

The menu regression failed before implementation because the commands were
absent. It now moves a selected file up/down and verifies root order, identity,
selection/pause and original copy. Eight model cases cover root/group, both move
directions and before/after placement, retaining parents/exclusions and the
separate legacy Ctrl+Tab insertion order. They also validate self-target no-ops
and ordered SourceConfiguration snapshots. An invalid-operation case rejects
cross-parent or missing identities before mutation.

Complete modern solution: 81 Archiver + 144 Avalonia pass, no failures/skips.
Ignored evidence: artifacts/migration/t023-order-red.log,
t023-order-focused.log and t023-order-modern.log.

This is the shared ordering operation and accessible menu phase, not completion
of legacy tree drag/drop. Cross-parent arrange/merge gestures and their cycle,
dissolution and source-limit rules still require integration. Automatic saved
workspace binding remains T053. Native pointer/menu/accessibility and full Core
tests were not rerun for this UI/model-only slice; T023 stays open.

## Cross-parent arrange and group transfer slice (T023)

MoveRelative now supports arranging a source beside a destination in another
parent. Add to group can transfer an already-grouped source directly; the menu
is no longer restricted to roots. Both use one Reparent operation that checks
the destination's 255-source limit and validates the candidate parent graph
before mutating either collection. A successful move retains the original ID,
clears its old parent's exclusion, inserts it in destination order, and dissolves
the old group only when exactly one member remains. Empty old groups remain.
When arranging beside the old parent itself, insertion precedes dissolution so
the promoted child retains the correct before/after anchor position.

Four new arrange cases failed first at the previous same-parent restriction.
They now pin cross-group order, exclusion cleanup, old sibling promotion and
arranging before/after one's dissolving parent. Two full-destination cases prove
that both transfer entry points leave the original graph/exclusion intact on
rejection. The earlier temporary reject-all-cross-parent test is replaced by
cycle/missing-ID rejection; existing nested-cycle tests still pass. A headless
menu case moves a real selected log into another group, promotes its old sibling
and verifies both source IDs and unchanged file contents/native-independent copy.

Complete modern solution: 81 Archiver + 151 Avalonia pass, no failures/skips.
Evidence under ignored artifacts/migration: t023-cross-red.log,
t023-cross-focused.log and t023-cross-modern.log. No dependencies were added.

This implements the move operation and menu transfer, not pointer drag/drop.
Tree gesture routing, legacy grouping-on-file behavior and native drag/menu
acceptance remain open. Shared Core code was unchanged and its full suite was
not rerun; T023 and the full migration remain incomplete.

## Grouped-folder activation identity regression (T022/T023)

The folder activation branch added any non-root folder back into Roots even
when it was already a configured group member. A real menu regression failed
with XmlException: Source identities must be unique. The branch now uses the
same ParentId-is-empty condition as file activation before adding a root.

The case groups an owned watched folder, checks its sole configured identity,
creates another matching log and waits for live discovery, selects/copies that
child, then reactivates the grouped folder. The original group/folder IDs and
single root remain intact; no duplicate watcher or configured root is created.
One production line changed. Focused case passes; complete modern solution
passes 81 Archiver + 152 Avalonia tests with zero failures/skips.

Ignored evidence: artifacts/migration/t022-grouped-folder-red.log,
t022-grouped-folder-focused.log and t022-grouped-folder-modern.log. T022/T023
remain partial; native folder-group menus/accessibility and the full Core suite
were not rerun for this UI activation guard. Pointer tree drag and aggregation
remain separate pending work.

## Group-on-file operation and menu slice (T023)

Legacy DataSourcesViewModel.Drag creates a group at the destination's root
position, with the dragged source first and destination second. Dropping onto
an existing group/member instead reuses that group. SourcesViewModel.GroupWith
now implements those rules using AddGroup, MoveRelative and AddToGroup; existing
validation, source limits, exclusion cleanup and old-group dissolution are reused.
Self targets, folder targets/sources and dragged groups are rejected before
creating any group, matching the legacy grouping gesture policy.

The source menu exposes Group with for other ungrouped file/custom sources;
existing groups remain available through Add to group. Creating a pair selects
its group, as the legacy grouping operation does. A headless menu regression
failed first because the command was absent. It now verifies ordered children,
stable identities, selecting/copying a member and unchanged original file bytes.
Six model cases pin both root directions, destination position, insertion-order
navigation, ordered configuration snapshots, old-sibling promotion, existing
group/member reuse, idempotence and invalid-target atomicity.

Complete modern solution: 81 Archiver + 159 Avalonia pass, no failures/skips.
Ignored evidence: artifacts/migration/t023-pair-red.log,
t023-pair-focused.log and t023-pair-modern.log. No dependencies were added.

Pointer tree gesture routing is not implemented by this menu/model slice.
Native drag/menu accessibility and timestamp-aware merged rendering remain open;
group selection still asks for an individual member. Full Core/native suites
were not rerun for these UI/model changes. T023 and full migration stay open.

## Internal pointer tree arrange/group slice (T023)

SourceTreeDrag now captures an unmodified left-pointer gesture after a four-DIP
axis threshold. File rows use the legacy 20% before / 60% group / 20% after
regions; dragged groups use the legacy before/after half split and cannot nest
through this gesture. The controller resolves only current configured source
identities, rejects folder/discovered-child sources and folder targets, previews
group capacity, and invokes the existing validated move/group operations on
release. A border indicates placement and a no-drop cursor indicates rejection.
It transfers no file contents and has no external data format; existing native
file/OLE drops continue through their separate event handlers.

Escape, release outside the tree, capture loss, Close all and shutdown clear the
candidate, capture and indicator. Text editors/modifier gestures do not start a
tree drag. The shared group command is reused by the menu and pointer paths.

The initial pointer grouping case failed before implementation because the
roots never became a group. Three headless pointer cases then verify grouping
and original copy, edge arrangement without grouping, and Escape/outside/close-all
cancellation. Complete modern solution: 81 Archiver + 162 Avalonia pass, no
failures/skips. Afterward a folder/discovered-child rejection case was added;
the final focused drag suite passes 4/4 with production unchanged. Two early
compile errors in geometry helper usage were corrected, not counted as behavior
regression evidence.

Evidence: artifacts/migration/t023-tree-drag-red.log,
t023-tree-drag-focused.log, t023-tree-drag-gestures.log,
t023-tree-drag-modern.log and t023-tree-drag-guards.log (ignored).

T023 remains open. Native pointer/keyboard/DPI acceptance, the full cross-parent
gesture matrix, edge scrolling and hover expansion remain pending. The visible-row
limit is an explicit code shortcut, not a completed tree-drag parity claim.
Shared Core/native suites were not rerun for this UI controller; native OLE
regression and accessibility will be checked with the native gesture slice.

## Captured tree drag viewport-edge scrolling (T023)

SourceTreeDrag.Refresh reuses the existing 100 ms window timer. While a current
configured source owns pointer capture, a 16-DIP viewport edge band calls the
existing ScrollViewer line-scroll operations. Pointer coordinates stay relative
to the tree; layout and the target/placement preview are refreshed after
scrolling so release resolves the currently visible row. A deleted source clears
the gesture. Escape/capture loss/Close all/shutdown already clear the active
gesture, so no second timer or background scrolling work is introduced.

The initial regression failed because the scroll offset never advanced. It
uses 61 owned source configurations in a short window, drags at the bottom then
top viewport edges, cancels with Escape and observes a stable offset over three
timer periods with source identity/order unchanged. Complete modern solution:
81 Archiver + 164 Avalonia pass, no failures/skips. After that checkpoint the
same test was strengthened to require travel beyond one viewport before moving
back up; its focused rerun passes with production unchanged and no timeout
increase. The implementation also wires horizontal edge line scrolling; that
direction has not received a dedicated native/headless acceptance case yet.

Ignored evidence: artifacts/migration/t023-tree-scroll-red.log,
t023-tree-scroll-focused.log, t023-tree-scroll-modern.log and
t023-tree-scroll-page.log. T023 remains open for hover expansion, native pointer/
DPI/accessibility and the full gesture matrix. Shared Core/native suites were
not repeated for this UI-only timer integration.

## Collapsed-group hover expansion (T023)

The existing captured-drag refresh now expands a nonempty collapsed group after
500 ms over its valid central grouping region. Dwell time uses Stopwatch's
monotonic clock, not wall time. Target/region changes and invalid destinations
reset dwell tracking; Clear resets it on Escape, capture loss, close and shutdown.
This reuses the existing window timer and removes the manual-expansion shortcut.

The initial headless case failed because the group never expanded. It now
cancels a hover, waits beyond the dwell interval to prove no delayed expansion,
then performs another drag, waits for expansion and drops into the same stable
group ID with ordered members. Complete modern solution: 81 Archiver + 165
Avalonia pass, no failures/skips. Afterward the same case was strengthened to
release the real captured IPointer as another cancellation path; its focused
rerun passes with production unchanged. No test timeout was increased.

Ignored evidence: artifacts/migration/t023-tree-hover-red.log,
t023-tree-hover-focused.log, t023-tree-hover-modern.log and
t023-tree-hover-capture.log. Native pointer/DPI/accessibility and the full tree
gesture matrix remain open; T023 is still partial. Shared Core and native
entry-point suites were not repeated for this UI-only hover state.

## Native source-row accessibility and edge hit-area fixes (T023)

Native inspection found unnamed TreeItem peers; their text peers exposed only
the rendered basename, despite the label's configured automation name. Source
containers now receive their configured path/group name directly. Group rename
updates the container name as well, and reattachment resolves the current name
rather than a stale template capture. The existing Unicode rename headless case
also asserts that accessible container name.

The first native top-edge arrange attempt left roots unchanged while center
grouping and Escape cancellation already passed. Giving SourcesView a transparent
background makes the entire row hit-testable, including empty edge pixels. The
same native point then arranges correctly without changing the 20/60/20 bands,
test deadline or pointer coordinates. This changes hit testing, not visible row
colors. No filesystem move/copy behavior was introduced.

Complete modern solution: 81 Archiver + 165 Avalonia pass. Complete native
entry-point suite: 17 pass, all zero failures/skips, with owned pointer-window
DPI recorded as 168. Native fixture additions and detailed evidence are committed
separately in native-entry-point-tests.md. Native mixed-monitor DPI, edge-scroll/
hover gesture coverage and the full accessibility matrix remain open; T023 and
the full migration are not complete.

## Inactive configured-file availability (T022)

A new regression first failed because deleting an inactive configured file never
updated its tree status. The shared Core FileSourceStates job now probes read
access and length every 500 ms without reading contents. The UI only reads cached
results. Missing, inaccessible and empty states recover without switching the
active document, changing source IDs/order or losing the current selection/copy.
Configured group members are included through the workspace's complete source set.

Configuration generations reject results after close, path replacement or disposal;
probe handles are released on every observation. Four deterministic Core cases
cover missing/empty/locked/recovered files and paused in-flight probes during
close, dispose and reconfiguration. The headless UI regression verifies inactive
missing -> empty -> ready recovery while the second source remains selected.

Checks: Core 7,124 pass / 0 failed / 5 existing skips; modern solution 81 Archiver
and 167 Avalonia pass / 0 failed / 0 skipped on the final full run. An earlier
full run had one character-selection clipboard timeout; that case and the new
regression passed together in isolation, followed by the full green run. No
assertions were weakened or timeouts increased.

Evidence under ignored artifacts/migration: t022-inactive-red.log,
t022-inactive-core-full.log, t022-inactive-modern.log,
t022-inactive-recheck.log and t022-inactive-modern-recheck.log.

This supersedes the earlier cached-only inactive availability limitation, but
T022 remains partial. Inactive parser/index errors and content indexing still
need the shared reader lifecycle; this probe does not claim to implement it.
Folder-derived children retain their existing discovery lifecycle. Native
accessibility, many-source performance and remote-share latency were not checked.

Legacy net471 test-project build also passes with zero errors and five existing
warnings; legacy tests were not executed. Log: t022-inactive-legacy-build.log.

## First-open missing file retention (T022)

Previously a new missing path was reported only in the global error text and
was absent from the workspace. File configurations now enter the tree with their
stable identity before the background open starts. Failed initial reads preserve
the prior active document/selection/copy while keeping the unresolved path for
retry. Repeated opens reuse the same configuration; successful recovery activates
that same identity. Existing open-request fences still prevent close-all from
being undone by late construction.

The new identity/retry regression failed first on the missing tree item. Seven
focused open/failure/batch/close cases then pass. The mixed batch contract now
retains both failed paths in input order alongside both usable files and still
activates the last successful document. Full modern suite: 81 Archiver and 168
Avalonia pass, zero failures/skips. No Core production source changed in this
slice, so the immediately preceding Core baseline remains separate.

Evidence under ignored artifacts/migration: t022-missing-new-red.log,
t022-missing-new-focused.log and t022-missing-new-modern.log.
This supersedes the earlier limitation that new paths enter the tree only after
successful construction. Invalid path syntax still reports an error without
creating a configuration. T022 remains partial pending complete source-lifecycle
parity; workspace disk persistence is the later T055 integration.

Relevant native multi-file CLI/picker and secondary-instance handoff checks also
pass 3/3 with zero skips: t022-missing-new-native.log. Full native acceptance,
Core/legacy runtime suites and disk-profile integration were not rerun here.

## Cached background folder status (T022)

A regression first demonstrated ten filesystem calls for ten reads of the
folder's Error property. FolderSources now accepts the existing shared scheduler
and publishes cached status/membership from a 500 ms background refresh. File
system access is outside the publication lock, so reading status cannot wait on
that I/O. The initial probe remains in background construction at the app caller.
All constructor callers were updated; no dependency or UI timer was added.

The periodic observation also detects empty-directory disappearance/recovery,
where file-membership change notifications alone provide no status transition.
It notifies only when status, membership or the unfiltered count changes. Existing
members survive transient I/O/pattern failures. Disposal stops the periodic job
and rejects any in-flight snapshot before publication.

Four new cases cover cached getter access, empty directory disappearance/recovery,
slow-probe cached reads, and disposal during a gated probe. All 12 folder cases
pass. Full Core suite: 7,128 pass / 0 failed / 5 existing skips. Full modern
solution: 81 Archiver and 168 Avalonia pass / 0 failed / 0 skipped.
Evidence under ignored artifacts/migration: t022-folder-status-red.log,
t022-folder-status-focused.log, t022-folder-status-core-full.log and
t022-folder-status-modern.log.

T022 remains partial; inactive parser/index lifecycle is still pending. Remote
share latency, many-folder performance and complete native accessibility were
not measured. The existing watcher still supplies its asynchronously refreshed
file snapshot, so status and membership converge across background polling ticks.

Native folder picker open/cancel: 2 pass, no skips (t022-folder-status-native.log).
Legacy net471 test-project build: zero errors, five existing warnings
(t022-folder-status-legacy-build.log); legacy runtime tests were not executed.

## Active-file lock/recovery contract (T022/T031)

Added an owned-file headless regression for an already indexed, copyable file
becoming exclusively locked and then accessible again without an explicit reopen.
It verifies a visible error/empty-row transition, automatic exact-content recovery,
stable source identity and preservation of paused follow mode. The exclusive
handle is acquired with a bounded retry and released even on assertion failure;
fixture contents remain unchanged. This contract passed against existing runtime
code, so no production change or invented failing baseline was needed.

Focused case: 1 pass. Full modern solution: 81 Archiver + 169 Avalonia pass,
zero failures/skips. Evidence: ignored artifacts/migration/
t022-active-lock-focused.log and t022-active-lock-modern.log.
Core/native/legacy suites were not rerun for this test-only slice. T022/T031
remain incomplete; this does not establish network-share recovery or full parity.

## Terminal newline / indexed EOF row (T022/T030)

A complete terminal LF already creates a valid EOF index position. Raw reads
returned null there, so the Avalonia page stayed Loading and copying a selection
including the final row failed. The headless document regression first timed out;
all twelve UTF-8/BOM, UTF-16LE/BE and UTF-32LE/BE contiguous/fragmented behavior
cases first failed with expected empty string / actual null.

Both raw request paths now use one shared indexed-line reader. A null ReadLine
becomes empty text only for a positive-length file whose same-generation indexed
position equals that stream's EOF. Invalid/out-of-range positions remain null;
empty files are not given invented rows. The existing generation/publication
fences remain. Sparse reads can continue from an EOF row to earlier rows and
preserve duplicates/request order. Append into that empty row and another terminal
LF remain copyable; no decoder, line count or persisted format changes.

Core tests verify EOF, out-of-range padding, append/reappend and sparse ordering.
The headless document case verifies resolved empty text, copy including the
trailing newline and subsequent append. The native case verifies the actual row's
accessible line label without Loading and multi-row copy with the trailing newline;
its fixture bytes stay unchanged. The existing clipboard snapshot/conditional
restore now also tracks expected owned multiline copies without logging contents.

Final validation: Core 7,158 pass / zero failures / five existing skips; modern
solution 81 Archiver + 170 Avalonia pass / zero failures or skips; legacy net471
test-project build zero errors / five existing warnings. Targeted native EOF and
both CLI-leading-line cases pass 3/3 on the final run. One initial native leading-
empty-line clipboard case timed out; retain t022-terminal-row-native.log beside
the final t022-terminal-row-native-recheck.log rather than claiming no failure.

The first modern run had character-drag and distant-scroll timeouts; a focused
rerun retained the scroll failure. Diagnostic output showed realized=0 after the
source count reached 20,000. A separate fixture-only commit (24feede3) realizes
headless layout before its explicit ScrollIntoView, keeping the exact target-text
and fewer-than-100-realized-row assertions. The focused three cases and final
full solution then pass. No timeout was increased or assertion weakened.

Evidence under ignored artifacts/migration: t022-terminal-row-ui-red.log,
t022-terminal-row-core-red.log, t022-terminal-row-core-focused.log,
t022-terminal-row-ui-focused.log, t022-terminal-row-layout-recheck.log,
t022-terminal-row-core-full.log, t022-terminal-row-modern-final.log,
t022-terminal-row-legacy.log and the native logs above. The full Core TRX is in
t022-terminal-row/t022-terminal-row.trx. Legacy runtime, native five-minute tail
and the broader performance matrix were not executed in this slice. T022/T030
and the full migration remain open for their remaining contracts/integration.

The independent encoding/settings reader integration is now recorded in
[encoding-tests.md](encoding-tests.md). Its functional checks do not close the
open foreground-performance or shipping profile-lifecycle gates.

Captured tree drags now cancel on Escape even when keyboard focus is in the log
list. A raw-key headless regression failed with an unwanted group before the
window-level active-drag handler was added; focused cases and the strengthened
native pointer/hover/edge cases pass afterward. Full validation details and the
separate native baseline are in [native entry-point tests](native-entry-point-tests.md).
T023 remains partial for its remaining integration and acceptance scope.

## Retained open-file documents (T022/T026)

Legacy sources retain their reader/index while another source is visible. The
Avalonia shell instead disposed its only document on every switch. A new
regression failed because appending to an inactive file never advanced its
existing line collection (t022-inactive-document-red.log/TRX).

MainWindow now retains successfully opened documents by configured source ID
and decoder choice. The existing timer refreshes inactive document collections
and status while the active view/copy/navigation remains bound to one document.
Returning with the same decoder reuses the existing index and bounded page
cache; a changed decoder replaces/disposes that source's previous document.
Window close and Close all dispose all retained documents. Closing a file or
folder prunes documents no longer configured; removed folder children are
pruned once they are no longer active. No new dependency or reader abstraction
was added. Core's existing generation fences remain responsible for rejecting
old reads after replacement/disposal.

New headless cases exercise inactive append before reactivation, exact document
reuse, paused follow, Close all followed by fresh identity/document, same-size
same-mtime inactive rewrite of cached text with stale selection cleared, and a
decoder change while the affected source is inactive. Existing source tests
cover unavailable/access/empty state and folder/close/racing-open paths.

The first focused integration run passed 57 cases but exposed one regression:
the reader's missing-file explanation replaced the established cached
"File is unavailable." status. Availability failures/empty status now retain
precedence while nonempty accessible files show reader progress/errors.
A subsequent run passed 59 cases and failed the new encoding fixture because
it looked up a nonexistent "Open file" automation name; it now waits through
the existing "Open folder" enabled-state helper. Neither failure was hidden or
resolved by extending timeouts.

This retains documents that have actually been opened. Newly discovered folder
members still use availability probes until first activation; parser/aggregate
output and automatic activation of every discovered reader remain pending.
Each retained source keeps its existing index/scheduler, so many-source memory,
CPU and responsiveness still require the planned concurrency acceptance.
Selection/viewport restoration across switches and shipping disk-profile
persistence are separate remaining tasks. T022/T026 and US1 remain incomplete.

Final review also caught a cache-key edge: changing a configured file path while
retaining its source ID could return the previous file's document. A separate
headless regression failed before adding canonical-path comparison to the
existing ID/decoder check (t022-cached-path-red.log/TRX). It now verifies exact
new-file copy and retained ID with a different document collection.

Validation: the modern solution passes 81 Archiver + 181 Avalonia tests without
failures/skips before that final path guard; the final affected SourcesTest suite
passes 56/56, including the additional path case, without failures/skips.
Native multi-file picker/arguments, relative-path handoff and Ctrl+G pass 4/4
before the path-only guard. Evidence is under artifacts/migration:
t022-inactive-document-modern.log/TRX, t022-inactive-document-final.log/TRX and
t022-inactive-document-native.log/TRX. Earlier failed focused/recheck logs are
retained. Core code did not change, so its full suite was not repeated; the full
native/performance matrix was not repeated after changing retained-reader
lifecycle. Existing multi-target legacy NU1902 warnings remain visible.

## Per-source selection and viewport (T030/T031)

The legacy LogViewerControl restores SelectedLogLines, VisibleLogLine and
HorizontalOffset when its source changes. The port now saves whole-line
selection through the existing DataSource.SelectedLogLines field, plus an
in-memory document/revision/scroll-offset snapshot. Returning to an unchanged
document restores the selected original line indices and paused horizontal and
vertical viewport. A following source keeps its selected lines and horizontal
offset while returning to the tail. Selection changes are batched.

Reset/removal notifications invalidate saved state and selected-line settings;
document identity plus revision fences prevent reuse after decoder/path changes
or file replacement. A posted viewport restore also checks the current open
request/document/revision, so a superseded callback cannot move another source's
viewport. Switching away before a pending restore executes keeps the earlier
snapshot instead of saving the temporary viewport. Pruning/closing documents
also clears their snapshots. Shipping restart persistence and character-range
restoration remain separate work; no file format or profile path changed.

The initial regression failed on missing restored selection. It now uses
noncontiguous lines 1000/1002, independently scrolls to line 1400, checks both
offsets within one pixel, exact multi-row copy, and rapid cached source switches.
A following-source case verifies selected line 0 with the visible tail and
horizontal offset. The prior inactive same-size/same-mtime rewrite case still
requires cleared selection and exact replacement text.

Two existing source cases initially failed because their single-line copy helper
added a new selected index to restored selection. That helper now explicitly
replaces selection; a separate CopySelected helper verifies the restored
multi-selection without modifying it. No expected copied text was loosened.

Following/independent-viewport tests exposed queued scroll ordering. Immediate
layout calls, temporarily disabling auto-scroll and a Loaded-priority callback
did not resolve it. Temporary synthetic diagnostics showed correct offsets
inside the callback followed by a return to the selected anchor. Avalonia's
[SelectingItemsControl](https://raw.githubusercontent.com/AvaloniaUI/Avalonia/12.1.6/src/Avalonia.Controls/Primitives/SelectingItemsControl.cs)
posts its selected-item scroll at default priority;
[Loaded priority](https://raw.githubusercontent.com/AvaloniaUI/Avalonia/12.1.6/src/Avalonia.Base/Threading/DispatcherPriority.cs)
runs ahead of that queue. The final callback uses default priority after the
selection work, then applies layout and the intended offset. Diagnostic console
output and temporary property/layout workarounds were removed.

The rapid-switch regression separately failed when an unapplied temporary
viewport overwrote the saved one. Tracking the pending restore prevents that
save. The full modern solution subsequently passes 81 Archiver + 184 Avalonia
tests, zero failures/skips (artifacts/migration/t031-source-view-final.log/TRX).
Failed red/intermediate probes remain under t031-source-view-* artifacts,
including rapid-red, callback-diagnostic and the earlier modern run; the final
passing result does not erase those observations. No timeout was extended.

Core production code did not change. Full Core, native performance and the
five-minute tail workload were not repeated in this slice. Large restored
selections and multi-source memory/CPU acceptance remain open, as do the rest
of T022/T026/T030/T031 and full migration.

Native targeted validation passes 4/4, zero failures/skips
(artifacts/migration/t031-source-view-native.log/TRX). The picker/argument cases
now switch to the first file, select/copy its original Unicode line, then return
to the second and copy its restored selection without reselecting it. Relative
handoff and Ctrl+G also pass. This is native selection/copy integration evidence;
precise native viewport restoration and the complete native suite were not
executed here. Owned processes exit and clipboard/temp ownership guards remain.

## Editable folder configuration (T022)

Legacy FolderDataSourceViewModel edits folder path, search pattern and recursive
discovery. These fields already existed in the migrated configuration and Core
watcher but had no Avalonia editor. The initial UI regression failed because
the folder context menu did not expose Folder options.

Configured folders now expose a modal editor with accessible path/pattern
fields, Include subfolders, Apply, Cancel and Escape. Semicolon/wildcard matching
continues to use FolderSources' existing bounded legacy matcher. Apply stages a
cloned configuration and replacement watcher on a worker before publishing the
three accepted fields. It keeps the folder ID and unrelated configuration;
unchanged member paths keep their existing child identities. The old watcher
is disposed after replacement. Cancellation, closed/removed owners and a
superseded watcher cannot publish the staged result; uncommitted watchers are
disposed. Invalid paths leave the dialog and original watcher usable.

Changing path updates the tree heading, tooltip, accessibility name and active
folder title; group renaming reuses the same heading refresh. Editing another
folder does not switch the active file, its selection or paused follow. No log
files are modified by the application. Settings are updated in the existing
in-memory configuration; shipping profile restart persistence remains T055.

Three headless/Skia cases verify:

- Escape cancel preserves actual original values; applying a pattern filters
  members, recursive off/on changes nested membership, new matching files stay
  discoverable, stable members retain IDs and the active file still copies its
  original text.
- Invalid empty path reports an error and leaves the old watcher live; a valid
  path change retains folder identity, updates its heading/title and discovers
  new-path files without including old-path additions.
- Cancelling while Apply is pending leaves the original settings and watcher
  usable after the handler completes.

The first fixture incorrectly expected XML-restored defaults from a newly
constructed DataSourceSettings instance. It now checks cancellation against
the actual opening values; no production default was changed. The first full
run passed 81 Archiver and 186 Avalonia cases but the Escape fixture attempted
key release on the dialog that Escape had already disposed. Releasing on the
still-open owner fixes that fixture without weakening cancellation assertions.

Final modern solution: 81 Archiver + 187 Avalonia pass, zero failures/skips.
Evidence: artifacts/migration/t022-folder-options-red.log/TRX,
t022-folder-options-focused.log/TRX (default-assumption failure),
t022-folder-options-patterns.log/TRX, t022-folder-options-contracts.log/TRX,
t022-folder-options-modern.log/TRX (closed-window fixture failure) and
t022-folder-options-final.log/TRX. No dependency or Core production code changed.
Core/full native/performance suites and native dialog interaction were not
repeated for this editor. T022 and the full migration remain incomplete for
aggregate output, unopened-reader lifecycle and the other recorded gates.

The final pattern/recursion fixture additionally asserts that the active line
selection and paused follow remain unchanged before any new copy selection.
That strengthened case passes 1/1 (t022-folder-options-pause.log/TRX); production
code is unchanged from the 268-case passing modern solution above.

## Bulk group/folder inclusion controls (T023)

Legacy IncludeAllInGroupViewModel/ExcludeAllInGroupViewModel operate only on
direct ISingleDataSourceViewModel members. File/folder renaming is explicitly
unsupported in the legacy viewmodels, so no additional rename feature was
introduced. Group and configured-folder menus now expose Include all/Exclude
all, acting on current direct file/custom members. Nested groups/folders and
unavailable member IDs retain their saved exclusions. Include all removes only
current eligible IDs, rather than clearing the whole persisted set.

New headless tests verify bulk group exclusions, preserving a nested group and
an unknown ID, XML round-trip, folder discovery of a new member, and unchanged
active source/selection/paused follow/original copy. The existing per-member
group exclusion case also passes: 3/3, zero failures/skips. Evidence:
artifacts/migration/t023-bulk-exclusion-behavior-red.log/TRX and
t023-bulk-exclusion-contracts.log/TRX. Initial fixture API compile mistakes are
retained in t023-bulk-exclusion-red.log; they are not the behavioral red. One
focused run passed the exclusion assertions but failed its active-tree-item
assertion because direct model reparenting had cleared fixture selection before
the command. The fixture now explicitly reselects its prepared active source.

These are configuration controls. Aggregate output/filtering remains US2;
changing inclusion does not close an individual log or remove its saved source.
Full modern/native/Core/performance suites were not repeated for this small
menu-only slice. Folder-child single-member controls and remaining T023/US1
integration are separate work; the full migration remains incomplete.

## Folder-child inclusion and stale menus (T023)

Folder-managed files now expose Include in folder/Exclude from folder while
retaining their existing ownership restrictions: they cannot be independently
closed, moved or removed from their watcher. The same menu builder handles the
existing group-member action and continues to use SourcesViewModel.SetExcluded
for configured group members. Folder actions update the parent's existing
ExcludedDataSources set only after validating current membership.

Both action paths validate the owning parent and the actual current member
object at invocation. A menu retained from a removed child cannot mutate a new
child with the same stable ID after rediscovery. Disappearance does not erase
the saved excluded ID; returning paths keep that exclusion until a current
menu explicitly includes them again.

The new fixture failed first because folder child menus were empty. It now
excludes a child, removes/recreates the owned file, verifies stable ID with a
different current configuration object, invokes an old Include action and
requires exclusion to remain. The new child's Include action then works while
the unrelated active source, selected line and exact copy remain unchanged.

Final modern solution: 81 Archiver + 190 Avalonia tests pass, no failures/skips,
including the preceding bulk controls and existing folder ownership tests.
Evidence: artifacts/migration/t023-folder-member-exclusion-red.log/TRX and
t023-folder-member-exclusion-modern.log/TRX. Core/native/performance suites were
not repeated for this UI configuration slice. Aggregate merge/filter output and
the full migration remain pending; these settings controls do not establish
aggregate inclusion behavior or shipping restart persistence.

## Folder-options native picker (T022/T024)

Legacy FolderDataSourceViewModel.ChooseFolder opens a folder picker with the
current edited path and stages the result until the edit is accepted. The
Avalonia editor now exposes Browse with the same workflow, using its own
Window.StorageProvider, one folder, and a suggested current local directory.
Picker cancellation leaves the path unchanged; selection updates only the
editor, and Apply still controls watcher/configuration publication. Closed-owner
checks reject late picker results. An unavailable picker reports a recoverable
message while manual path editing/Apply stay usable. Suggested and returned
storage objects are disposed once no longer needed.

The native fixture adds only the fixed Shift+F10 chord through the existing
guarded keyboard implementation. PID/focus ancestry, held mouse/modifier/chord
checks, immediate ownership recheck, atomic injection and partial-release
cleanup are retained; no arbitrary public chord API is exposed. Existing Ctrl+A
and Ctrl+G wrappers still submit the same Control chords. F10 joins the held-key
guard. The owned-window lookup used by existing pickers is reused for the
options editor and its nested native folder picker.

Two Windows cases open the real context menu/editor/picker, select or cancel,
verify editor path versus unchanged configured folder before Apply, then
apply/cancel and copy the expected watched Unicode log. Both original and
destination fixtures remain unchanged. Initial cases failed because Browse was
missing, proving the context-menu/editor path already worked. After adding the
button, they failed with Unsupported Pattern because an untyped path-name query
matched the static label instead of the edit field. The typed Edit/Value query
also waits for the async picker result; no timeout/assertion was relaxed.

Native picker cases plus Ctrl+G/large Ctrl+A regression pass 4/4, zero skips or
failures (artifacts/migration/t022-folder-browse-native-typed.log/TRX).
The preceding failures remain in t022-folder-browse-native-red.log/TRX and
t022-folder-browse-native.log/TRX. The modern solution passes 81 Archiver + 191
Avalonia cases, zero failures/skips (t022-folder-browse-modern.log/TRX), including
the headless unavailable-picker/manual-Apply case. This solution run precedes
the final storage-object disposal-only change; affected checks are recorded
separately. Full Core/native/performance suites and restart persistence remain
outside this slice. T022/T024 and the full migration are still incomplete.

After adding storage-object disposal, both native picker cases pass 2/2 and
the headless unavailable-picker/manual-Apply case passes 1/1, no failures/skips:
t022-folder-browse-cleanup-native.log/TRX and
t022-folder-browse-cleanup-headless.log/TRX. No owned viewer processes remain.

## Discovered-file access state and global identities (T022/T026)

The availability tracker previously received only configured workspace roots
and group members; folder-managed children were omitted until activation.
A regression locked an unselected child with FileShare.None and failed because
its row never reported File cannot be accessed. The tracker now receives the
complete configured/discovered snapshot. Refresh, Close current and Close all
reuse that same snapshot helper, preserving existing generation invalidation
and cached background-only filesystem probes.

Full-snapshot validation can detect a configured ID colliding with a generated
folder-child ID. Validation failures are reported without replacing the previous
probe snapshot, and file activation validates the same snapshot before creating
or reusing a reader. No conflicting source/configuration is silently deleted.
A test verifies the error, rejection of activation while identities conflict,
continued active-source copy, and successful child activation after removing
the conflicting configuration.

The lock/recovery test additionally verifies unchanged selected source/line,
exact copy, retained discovered-child object identity and unchanged owned file
contents after releasing the lock. The affected availability/identity cases
pass 3/3; the final modern solution passes 81 Archiver + 193 Avalonia cases,
zero failures/skips. Evidence: artifacts/migration/t022-folder-child-access-red
(.log/TRX), t022-folder-child-access-focused (.log/TRX) and
t022-folder-child-access-modern (.log/TRX).

This closes the discovered-child availability gap, not the remaining reader
lifecycle: unopened files still need coordinated background reader ownership
and aggregate/parser integration. LogSourceProxy is a replaceable forwarding
source, not an existing reference-counted reader owner. No second index or
speculative shared-reader abstraction was added here. Core production code is
unchanged; full Core/native/performance and many-source cost checks were not
repeated. T022/T026 and the full migration remain open.

Targeted native folder-picker apply/cancel and Ctrl+G checks pass 3/3 with no
failures/skips (t022-folder-child-access-native.log/TRX). These verify normal
file/folder activation after adding the global identity check; they are not a
native lock-state or full-suite run.

## Reader ownership foundation (T022/T026, integration pending)

SourceDocuments is an internal application-level owner for existing LogDocument
instances/Core readers. A configured source owns one reader reference; explicit
view leases keep retired readers usable until their last holder releases them.
Configuration snapshots validate identities/canonical file paths before changing
the catalog, clone settings/decoder inputs, and start factories off the caller
thread. Matching ID/path/decoder reuses one pending or live open. Changed paths
or automatic-versus-manual decoder choices retire the old entry. Failed opens
are observable and retried on the next configuration refresh.

Removal/owner closure rejects pending acquisitions. An unleased late completion
is disposed, while an already-issued lease stays valid until released. Lease
disposal is idempotent; source disposal happens outside the catalog lock. The
owner validates raw-reader identities/paths; workspace graph validation remains
with the existing settings/source model. Parser-dependent key invalidation is
explicitly deferred to T043 in a shortcut comment. No reference-counted forwarding
ILogSource implementation or new package/public plugin API was introduced.

Seven owned-temp-file contract tests cover shared opens, final-lease disposal,
late completion after removal/closure, canonical path reuse, path/decoder
replacement, failed-open retry, invalid snapshots retaining the old catalog,
and refusal of new acquisitions after owner disposal. They establish successful
original copy before testing read failure after final release. An initial
closure test assumed only IOException; the underlying Core scheduler can also
cancel an in-flight read during disposal, so the test recognizes the normal
I/O/disposed/cancelled outcomes without accepting successful reads indefinitely.
No timeout was extended and the factory gates are released in finally blocks.

Evidence: artifacts/migration/t022-reader-owner-behavior-red.log/TRX (five
unimplemented contracts), t022-reader-owner-focused.log/TRX (5/5),
t022-reader-owner-contracts.log/TRX and t022-reader-owner-final.log/TRX (the narrow
closure-exception fixture failure), t022-reader-owner-closure.log/TRX (7/7), and
t022-reader-owner-modern.log/TRX (81 Archiver + 200 Avalonia, zero failures/skips).
The initial unread stub-constructor parameter compile failure is separately
retained in t022-reader-owner-red.log; it is not the behavioral red.

This foundation is not yet wired into MainWindow. Automatic indexing of newly
discovered files, shared scheduler/host lifetime integration, UI leases/status,
and many-source performance remain the next implementation work. The current
shipping window still uses its retained-document cache. Core/native/performance
suites were not repeated because their callers and Core production code did not
change. T022/T026 and full migration remain incomplete.

## Native pointer diagnostics during background-reader integration

The t022-background-reader-native run passed three cases (folder-options
browse apply/cancel and Ctrl+G), but all five tree drag cases stopped at the
pre-input owned-point guard. A focused hover rerun with point/PID diagnostics
reported target (436,855), expected owned PID 22456, actual PID 12116; a process
lookup identified PID 12116 as ChatGPT. The guard remains enforced and no
foreign-window input was sent. This is evidence of desktop occlusion, not a
passing drag regression result or proof that production geometry is correct.

The native helper now reports the rejected point and expected/actual PIDs
without capturing window titles, clipboard contents or logs. Evidence:
artifacts/migration/t022-background-reader-native.log/TRX and
artifacts/migration/t022-background-reader-pointer-diagnostic.log/TRX.
The five drag cases still require an unobstructed-desktop rerun.

## Background raw-reader integration (T022/T026, partial)

MainWindow now configures SourceDocuments for every configured/discovered raw
file, including sources never selected in the log view. Ready readers are
leased into the existing cache; activation reuses the same reader. Removed or
replaced readers remain alive while actively displayed, then release after
rows detach. Pending/stale opens return their leases, and closure releases the
catalog and view leases. Active decoder checks use the owner's frozen decoder
snapshot. Failed first opens retry on the existing 500ms configuration refresh.

Each file tree row exposes a separate accessible Indexed line count; existing
availability/loading/error text remains distinct. Background refresh updates
counts without changing the visible source, paused selection or clipboard copy.
A behavioral test first failed without this integration, then passed after
wiring the owner. Expanded owned-file tests verify append before first
activation, newly discovered files, removal/reappearance with stable ID and a
fresh reader, and recovery from an exclusive lock held before initial opening.
The final activation copies the newly appended original text exactly.

Evidence: artifacts/migration/t022-background-reader-ui-red.log/TRX,
t022-background-reader-ui-focused.log/TRX (5/5),
t022-background-reader-lifecycle.log/TRX (15/15 lifecycle/encoding cases), and
t022-background-reader-final.log/TRX (81 Archiver + 201 Avalonia, zero failures
or skips). The earlier t022-background-reader-modern pass predates the expanded
lifecycle cases. Native results and their desktop-occlusion limitation are
recorded above; the five tree drag cases are not claimed as passing.

Core production code is unchanged. Per-document schedulers remain in use;
shared scheduler lifetime and many-source CPU/memory measurements are still
pending. Aggregate/parser sources and profile persistence are also unfinished.
T022/T026 remain unchecked and the full migration goal remains active.

## Forty-reader ownership preflight

A new owned-temp-file test configures 40 distinct raw sources without selecting
a window source, indexes and copies every original line, and refreshes the same
configuration without duplicate factory calls. Removing/releasing 20 readers
leaves the other 20 able to tail and copy their distinct appended lines. Closing
the catalog preserves an issued lease; returning all leases then permits an
exclusive read/write open of every file, proving no reader handle is retained.
Cleanup returns every acquired lease even if an assertion fails.

The focused run passed: total measured workload 1156.0ms, process CPU 1312.5ms,
working set 94,334,976 bytes, process thread count 39. These are one in-process
functional preflight sample including test-host overhead, not incremental
per-reader costs, idle CPU, native UI responsiveness, a scheduler comparison or
SC-004/005 acceptance. Per-document schedulers remain unchanged; these results
do not by themselves justify adding shared-scheduler lifetime machinery.
All eight reader-owner contracts pass, zero failures/skips. Evidence:
artifacts/migration/t022-many-readers.log/TRX and
t022-many-readers-contracts.log/TRX. No production code changed in this slice,
so the full modern/native/Core suites were not repeated. Whole-app many-source
profiling, parser/aggregate integration and the full migration remain pending.

## Modern group lifecycle foundation (T023, UI binding pending)

SourceGroup ports the essential ownership/reconfiguration behavior from legacy
MergedDataSource without its WPF-host AbstractDataSource dependency. It reuses
Core MergedLogSource and LogSourceProxy, keeps a stable forwarding source, and
exposes a read-only ordered identity list for interpreting source slots.
Excluded members occupy EmptyLogSource slots, matching the legacy attribution
rule. Unknown exclusion IDs and caller settings are retained. Group disposal
owns only its merge/proxy, not member sources or the caller's scheduler.

Membership snapshots reject empty/duplicate/self identities, null sources and
more than the existing 255 source slots before replacing the active merge.
Workspace graph validation remains responsible for indirect cycles. Existing
Core timestamp ordering is preserved: ties follow member order; unstamped
entries with no timestamp-bearing parent are omitted. Replacement detaches the
old merge; queued/late old-member updates cannot populate the stable view.

Four tests cover exclusions/re-inclusion with source slot 1 retained, unknown
settings, timestamp ties and original indexes, pending/late old-member updates,
empty membership, invalid snapshots preserving the usable view, idempotent
closure and surviving member reads/appends. The initial fixture namespace
qualification compile failure is retained separately from the behavioral red:
three tests then failed with NotImplementedException before production code.
Final group + existing merge suites pass 1024/1024, zero failures/skips. Core
net45 and net10.0 both build with zero errors (known NU1902 warnings visible).

Evidence: artifacts/migration/t023-group-owner-red.log (fixture compile),
t023-group-owner-behavior-red.log/TRX, t023-group-owner-focused.log/TRX,
t023-group-owner-merge.log/TRX and t023-group-owner-build.log.

This is a Core foundation, not completed group/folder output in the desktop.
MainWindow still clears the log view for groups/folders. Binding leased member
readers and restoring the parsed timestamp pipeline are required next; raw
readers alone cannot produce meaningful timestamp merges. Full modern/native
and full Core suites were not repeated for this isolated additive class;
existing merge behavior was checked by its complete affected suite. T023 and
the overall migration remain incomplete.

## Parser provenance prerequisite for group binding (T026/T043, partial)

GenericTextLogSource previously fetched only Index/RawContent before handing
entries to a parser. Consequently the generic parser's forwarding entry could
not retain OriginalIndex, LogEntryIndex, SourceId or other underlying columns.
The shared accessor now buffers the underlying source's advertised columns;
parsed Timestamp/LogLevel still override their input values. This fixes the
shared path used by the legacy parser factory and acceptance sources, without
adding a second parser or changing the desktop's current raw-reader pipeline.

The regression requests actual input row 42, retaining OriginalIndex 42,
LogEntryIndex 42 and source slot 3 while parsing a different timestamp and
Error level from the text. Initial fixture mistakes are retained in the local
logs: heterogeneous descriptor array inference, and assuming InMemoryLogSource
accepted arbitrary index overrides or advertised SourceId by default. The
fixture now creates real preceding rows and explicitly advertises SourceId.
The final fixture was rerun against the original production line and failed
with OriginalIndex Invalid instead of 42; restored production passes.

Affected parser/group/merge checks: 1025/1025 pass, zero skips. Full net10.0 Core
suite: 7163 passed, zero failures, five pre-existing skips (empty-construction,
true/foobar, two string-literal cases and TestGetTimestamp4). Both Core targets
net45/net10.0 build with zero errors; known legacy NU1902 warnings remain.
Evidence: artifacts/migration/t043-parser-provenance-final-red.log/TRX,
t043-parser-provenance-green.log/TRX, t043-parser-provenance-core.log/TRX and
t043-parser-provenance-build.log. Earlier fixture/focused failures are retained
under the same prefix and are not reported as production regressions.

No desktop group output is claimed yet: parser/leased-member wiring and UI
binding remain next. Avalonia/native and original Framework runtime suites were
not repeated in this shared-accessor slice. T026/T043 and full migration remain
incomplete; this change does not establish plugin parser compatibility.

## Parser listener retirement for group reconfiguration

ProxyLogListenerCollection.RemoveListener now removes its dictionary entry as
well as unsubscribing the underlying source. Previously the dictionary retained
retired merge listeners, and re-registering a removed listener threw a duplicate
key exception. The new add/remove/re-add regression failed with that exception
before the one-line fix. A separate check confirms the existing parser buffer
already preserves original control characters; no new text-preservation option
was needed or retained.

The initial parser-contract run also exposed an incorrect fixture assumption
that the legacy buffer sanitized RawContent; that assertion was corrected to
the actual original-text contract, not used as evidence of a production bug.
Affected parser/proxy/group tests pass 989/989. Full net10.0 Core passes 7165
with zero failures and the same five pre-existing skips. Evidence:
artifacts/migration/t023-group-parser-contracts-red.log/TRX,
t023-group-parser-contracts.log/TRX and t023-group-parser-core.log/TRX.
Desktop binding changes are committed separately. No native or Framework runtime
execution was performed for this listener fix.

## Selected group output with leased parsed readers (T023/T026/T043, partial)

Selecting a group now binds a real virtualized LogDocument over the existing
Core merge instead of clearing the log view. GroupDocument holds file-reader
leases and owns only its group/merge layers. Nested configured groups recurse
in member order with cycle checks. Exclusions preserve source slots without
opening excluded members. Missing/inaccessible members contribute empty slots
and a visible group error while readable members continue; when the catalog
successfully opens a previously failed member, the active group is rebuilt.
Membership/exclusion/reader identity changes trigger reconfiguration on the
existing refresh cycle. Stale pending activation is disposed rather than bound.

FileLogSourceFactory.ParseText reuses GenericTextLogSource and the existing
LogSourceColumnAdorner. Streaming readers advertise raw/index/offset columns;
merge additionally needs valid log-entry indexes, so parsing alone initially
produced zero merged rows. Adding the house adorner resolves this without
buffering entire files. Raw single-file display/copy remains on its original
source, while group members use the parsed/adorned view. Existing buffered
RawContent already retains control characters; no speculative preservation
option was retained. Decoder changes activate by source kind, so a group name
is never treated as a file path.

The headless behavioral red failed waiting for group output. Final tests cover
chronological merge, nested-group tailing, exclusion/re-inclusion, exact copy
including an owned control character, decoder refresh, switching back to the
original child reader, and unchanged owned file contents. A second scenario
retains readable members beside a missing file, excludes/re-includes the still
missing member, then creates it and observes automatic merge recovery without
another configuration change. That stronger test exposed copy clearing the
operation-error field; stable group errors now come from the group owner, so
copy cannot hide them. No assertion was weakened or timeout extended.

Final modern solution: 81 Archiver + 204 Avalonia = 285 passed, zero failures
or skips (t023-group-ui-verified.log/TRX). Core full-suite result is recorded in
the listener-fix section above. Intermediate runs: t023-group-ui-red (missing
binding), -focused/-diagnostic (missing entry-index adorner), -parsed (2/2),
-modern (81+203), -lifecycle (2/2), -final (81+204 before decoder-caller/error
refinements), -encoding (16/16), -recovery (one stable-error regression) and
-recovery-fixed (2/2). All artifacts are under ignored artifacts/migration/;
the final verified run includes the latest changes. Modern solution build also
builds retained Core/Archiver legacy targets; legacy NU1902 remains visible.

Remaining scope: selecting a folder still uses its existing folder overview;
folder aggregate output is next. Group activation rebuilds its view rather than
restoring viewport/selection snapshots (explicit T031 shortcut); source-aware
provenance UI, multiline/custom/plugin parser integration, parser-state changes
across replacement, and full performance/native acceptance remain pending.
Native/Framework runtime suites were not rerun in this slice. No task checklist
is marked complete from this partial implementation; full migration stays open.

## Selected folder aggregate output (T022/T023, partial)

Folder activation now reuses the same leased GroupDocument/parsed Core merge as
configured groups. The existing watcher still owns discovery and stable child
IDs; membership/exclusion changes rebuild the selected folder merge on refresh.
The shared activation accepts the current open-request ID, so a folder at the
start of OpenFilesAsync does not increment the request twice or abort remaining
batch items. Automatic aggregate refresh is suppressed while an explicit file
open is in progress. Folder path/title and unavailable/no-matching-files status
remain visible; single-file activation retains its existing reader/cache path.

A behavioral test failed before wiring folder output. It now opens folder then
standalone file in one batch, activates the folder, verifies timestamp order and
exact copied text, tails an append, discovers a new middle-timestamp file,
removes it, excludes/re-includes a member, and verifies original owned bytes.
The first affected run passed this case plus missing-folder/close-all checks,
but an existing nested-group tail case timed out. A diagnostic rerun passed both
merge cases. Its readiness check was strengthened to require the actual bound
navigation source ID: parent and child groups share the same default title, so
title equality cannot prove the parent view is active. No timeout/assertion was
weakened. The original timeout is retained; its precise cause is not conclusively
isolated and these passes do not prove all timing flakiness is eliminated.

Final modern solution passes 81 Archiver + 205 Avalonia = 286, zero failures or
skips. Targeted Windows native folder-picker apply/cancel, folder-options browse
apply/cancel and Ctrl+G checks pass 5/5. These native cases verify entry/dialog/
navigation flows, not chronological folder aggregate rendering. Evidence:
artifacts/migration/t022-folder-merge-red.log/TRX,
t022-folder-merge-focused.log/TRX (3 pass/1 timeout),
t022-folder-merge-diagnostic.log/TRX (2/2),
t022-folder-merge-modern.log/TRX and t022-folder-merge-native.log/TRX.
Temporary reflection diagnostics were removed. Core production code is unchanged;
its full suite and performance workloads were not rerun. Known legacy NU1902
warnings remain visible.

Folder/group output now works with built-in single-line timestamps. Viewport/
selection restoration for aggregate views, parser-state changes on replacement,
multiline/custom/plugin parsers, provenance navigation and full performance
acceptance remain unfinished. T022/T023/T026/T031 remain unchecked; the complete
migration goal is still active.

## Aggregate selection/viewport restoration and member revisions (T026/T031, partial)

Opened aggregate documents now remain in the existing window lifetime, with
leased member readers, when switching to another source. Group/folder and raw
file activation share the existing ShowDocument restoration path instead of
duplicating selection/follow/scroll logic. Returning to unchanged content reuses
the same aggregate document, restores saved line selection, paused state and X/Y
viewport, and retains the existing default-priority scroll ordering/request and
revision guards. Aggregate reset/removal events invalidate saved selection just
like file documents. Closure/pruning disposes configured aggregate caches; retired
active aggregates live only until their displayed rows detach.

Inactive valid aggregates refresh their row state. Membership/exclusion/reader
identity changes retire inactive caches. Member reader revisions are now part
of aggregate validity as well: a rewrite retires the old derived merge even if
the raw document identity is unchanged. This closes the newly exercised stale
aggregate path without accepting old selection or cached text on return. The
previous rebuild-on-every-activation shortcut has been removed; session-only
profile persistence remains the existing T055 shortcut.

Two owned-file headless tests use 2000 timestamped long lines for group/folder
views, select lines 1000/1002, park an independent viewport near line 1400 with
horizontal offset 140, switch to a file and back, and verify document identity,
exact two-line copy and X/Y restoration within one pixel. They then rewrite an
inactive member, observe aggregate revision invalidation, return to the new
one-line result, reject old selection and copy the replacement exactly.

The initial fixture timed out on viewport visibility because it scrolled before
the selected-item auto-scroll settled; the merge count was already 2000. Waiting
for the selected row as the existing raw-file fixture does exposed the actual
behavioral red: both aggregate documents were recreated. After caching/restoring,
both reached the later inactive-rewrite failure. Member revision validation
resolved it. No timeout increased or assertion weakened. Evidence under ignored
artifacts/migration/: t031-aggregate-view-red (fixture ordering),
-behavior-red (two document-identity failures), -focused (raw restoration passes,
two inactive-rewrite failures), -revision (5/5), -modern (81 Archiver + 207
Avalonia = 288 passed, zero failures/skips), and -native (folder-picker apply/
cancel plus Ctrl+G, 3/3). Native checks verify entry/shortcut flows, not native
aggregate viewport rendering. Core production code is unchanged; full Core and
performance workloads were not repeated. Legacy NU1902 remains visible.

Restart/profile persistence, character-range selection restoration, parser-state
changes across replacement, multiline/custom/plugin parsing, provenance
navigation and complete performance/native acceptance remain open. T026/T031
and the complete migration remain unchecked/in progress.
