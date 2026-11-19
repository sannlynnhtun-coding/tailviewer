# T019 navigation contracts — in progress

## Slice 1: go-to-line and whole-line copy

Read legacy GoToLineViewModel/its tests and QuickNavigationViewModel first.
Legacy line numbers are one-based and chosen indices are zero-based. Quick
navigation matches source names/paths; it is a separate feature from line seek.

A meaningful missing-Ctrl+G control failure was captured before implementing the
first NavigationView slice. The view binds the current LogDocument, checks input
against its current indexed line count, emits the zero-based index, and reports
invalid/unavailable input without changing selection or follow state. Successful
seek selects/scrolls the row and pauses follow. Source switch/group/folder/close
rebinding clears the old input/document. Reopening navigation clears prior input,
as in the legacy model. Escape/Cancel hides the view and returns focus to the log
or source tree. The log list is explicitly focusable; a real focus regression
was found by the cancellation test before the final passing run.

Six ordinary headless cases now pass:

- 1-based line 2 selects index 1, pauses follow and copies original Burmese text
  including surrounding spaces.
- Zero/negative/out-of-range/overflow/nonnumeric/empty inputs retain selection
  and follow state and display actionable validation.
- Switching to another source clears prior input and validates against the new
  source rather than navigating the previously opened file.
- Unordered duplicate selected indices copy in source order while preserving
  repeated text from distinct original lines.
- Escape restores log focus without changing the existing selection.
- No active log gives guidance without selecting a nonexistent line.

Final complete Avalonia suite: 59 passed, zero failures/skips. Modern solution
checkpoint before the final cancellation/focus additions: 81 Archiver + 57
Avalonia passed. No package pins, timeouts, ignored tests or Core sources changed.

Evidence: ignored artifacts/migration/t019-navigation-red.log,
t019-navigation-focused.log, t019-navigation-solution.log,
t019-navigation-ui-final.log and t019/t019-navigation.trx. Failed intermediate
focus/build checkpoints are not claimed as passing behavior evidence.

T019 and T029 remain unchecked. Quick source navigation, character/multiline
selection, broader navigation keyboard/large-file/rotation cases and complete
source/provenance integration remain pending. This slice does not replace those
requirements with whole-line selection or claim US1 parity. Native keyboard/
clipboard acceptance and full Core rerun were not performed.
## Slice 2: quick source navigation

Legacy EditMenuViewModel binds Ctrl+T to the source chooser. The port searches
configured file/folder paths and group names with CurrentCultureIgnoreCase,
retains original text in highlighted match segments, and preserves source
insertion order even when the source tree is regrouped. Candidates reference
existing DataSource identities; duplicate display names remain distinct.
The insertion ordinal is workspace-local; restoring saved source ordering still
belongs to profile/workspace integration (T053).

A missing-Ctrl+T behavioral failure was recorded before implementation.
Four model cases cover matching, grouping order, empty/unmatched queries and
folder/group identity. Seven headless UI cases cover choosing by path or group
name, Up/Down/Enter, empty/unmatched candidate clearing and disabled Open,
Escape focus/selection preservation, copying search text rather than log rows,
and choosing the current source without losing selection or paused follow.
The view also revalidates source identity before activating a stale candidate.

Final complete Avalonia suite: 70 passed, zero failures/skips. The preceding
modern solution checkpoint passed 81 Archiver + 69 Avalonia tests; the final
keyboard case was added afterwards and is included in the 70-test run.
Evidence: ignored artifacts/migration/t019-quick-red.log,
t019-quick-model.log, t019-quick-solution-final.log,
t019-quick-ui-final.log and t019-quick/t019-quick-final.trx.
Intermediate template/build failures were fixed and are not passing evidence.
No packages, test timeouts, ignored cases or Core production code changed.

T019/T029 remain unchecked: character/multiline selection, previous/next-source
shortcuts, broader large-file/rotation/provenance integration and their remaining
dependencies still need work. Native keyboard/clipboard acceptance was not run.