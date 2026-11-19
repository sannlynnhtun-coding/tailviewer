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
