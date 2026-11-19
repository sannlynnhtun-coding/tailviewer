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