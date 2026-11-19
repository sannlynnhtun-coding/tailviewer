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
## Slice 3: previous/next-source keyboard navigation

Legacy LogViewMainPanelViewModel navigates its all-source insertion-order list,
wraps in both directions and does nothing when the current source is absent.
Ctrl+Tab / Ctrl+Shift+Tab now apply those rules to existing configured source
identities, using the same insertion ordering as the quick chooser. Grouping
changes tree placement without changing this order. Plain Tab retains normal
focus navigation. Zero/one-source cases do not reopen the current document or
reset its selection/follow state. Successful switches restore log/tree focus.

Two new headless cases cover bidirectional wrapping, plain Tab and zero/one
source. Existing model checks now also assert grouped-source cycle order and
unknown-current rejection. A meaningful pre-implementation source-switch timeout
is recorded in artifacts/migration/t019-cycle-red.log; the initial assertion
that plain Tab must remain unhandled was corrected because Avalonia handles it
for normal focus navigation. No production change was made for that assertion.

Complete affected suite: 72 Avalonia tests passed, zero failures/skips.
Evidence: artifacts/migration/t019-cycle-tests.log and
t019-cycle/t019-cycle.trx. Native keyboard input was not exercised.
T019/T029 remain open for character/multiline selection and the remaining
large-file/rotation/provenance integration/dependencies described above.
## Slice 4: original character-range copy contract

Legacy TextCanvas.OnCopyToClipboard copies whole selected lines; its character
range behavior is not a baseline claim. T019/T030 explicitly request character
selection, so LogSelection now records anchor/active UTF-16 line/column positions
for the Avalonia implementation. LogDocument normalizes reverse selections,
reads original Core RawContent, slices only the first/last lines and preserves
internal empty lines, spaces, Unicode and platform newlines. Both range and
whole-line copy reuse the existing generation-fenced read/publish path.
Invalid line/column offsets are rejected rather than silently copying a nearby
range; whole-line indices are also checked before accessing Core.

The initial callable range overload returned whole lines, giving a meaningful
wrong-content red test (emoji expected, complete line returned). Two new cases
now verify forward/reverse single/multiline ranges, emoji/Burmese/whitespace,
empty ranges and invalid coordinates/indices. Full Avalonia suite: 74 passed,
zero failures/skips. Evidence: ignored artifacts/migration/t019-character-red.log,
t019-character-tests.log, t019-character-full.log and
t019-character/t019-character.trx.

This is the shared copy contract, not an end-user character-selection UI.
Mouse/keyboard range selection, highlighting and invalidation on source/reset
still need integration. T019/T029/T030 remain unchecked. No native clipboard,
Core suite, or large-range memory acceptance was run for this slice.