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
## Slice 5: pointer character selection on virtualized rows

Alt+left-drag now selects an original-text character range, including multiple
realized rows and reverse drags. Normal Shift/Ctrl line-selection behavior stays
available. A tooltip documents the gesture. Standard SelectableTextBlock provides
selection highlighting; the existing virtualized ListBox and bounded document
page cache are retained. Display line-number prefixes are excluded from range
coordinates and copied text. The Copy button and existing Ctrl+C route use the
same generation-fenced original-content reader.

The pointer controller captures the source revision at selection time and passes
it through the asynchronous copy. Replacement/reset invalidates selection before
it can copy replacement content under old coordinates. Source/group/folder/close
bindings clear ranges and release any owned pointer capture. Append retains the
range and paused follow state. Selection highlighting is refreshed only while a
range exists; no document-sized text control or parallel content buffer is added.

A real headless pointer gesture initially failed with incorrect clipboard text.
Three new cases now verify bidirectional cross-row drag/highlight, append
preservation, replacement/source-switch clearing, original line-copy fallback,
and explicit rejection of a pre-reset revision. Full affected Avalonia suite:
77 pass, zero failures/skips. A subsequent focused pointer check also passed
with an additional assertion that the native selection brush is present.
Evidence: ignored artifacts/migration/t019-drag-red.log,
t019-drag-focused.log, t019-drag-full.log, t019-drag-highlight.log and
t019-drag/t019-drag.trx.

T019/T029/T030 remain open: keyboard range extension, edge autoscroll for dragging
beyond realized rows, wider large-file/provenance integration and native input/
clipboard acceptance are still pending. The realized-row drag limit is documented
in the controller; this slice does not claim complete selection parity.
## Slice 6: keyboard character range extension

Alt+Shift+arrows/Home/End starts a text range from the selected row (or the first
row if no row is selected). Once a text anchor exists, Shift+arrows/Home/End
extends it; ordinary line navigation remains available outside text-range mode.
Escape clears the text anchor and invalidates queued movements. The row tooltip
documents the keyboard entry gesture. Left/Right step by StringInfo Unicode text
elements instead of splitting surrogate pairs or combining sequences. Up/Down
retain a preferred column across short lines and snap to a text-element boundary.
Line boundaries, Home/End, scrolling to the active endpoint and Ctrl+C are wired.

Background row reads are ordered for rapid key repeats. Epoch/document/revision
checks reject moves after cancellation/source/reset changes; stale work cannot
publish preferred-column state. Copy waits for preceding queued movements,
including a copy requested immediately after starting an asynchronous range.
Read failures clear the range and report an actionable selection error.

Three new headless cases cover Unicode stepping and Ctrl+C, preferred-column
movement/Home/End/collapse, rapid repeats and Escape/new-range isolation. The
initial missing-keyboard-range failure is recorded in t019-keyrange-red.log.
The full affected suite passed 80 Avalonia tests, zero failures/skips. After the
final preferred-column publication change, all four keyboard-filtered cases
passed again. Evidence: ignored artifacts/migration/t019-keyrange-focused.log,
t019-keyrange-full.log, t019-keyrange-final-focused.log and
t019-keyrange/t019-keyrange.trx.

T019/T029/T030 remain open for drag edge autoscroll and their broader
large-file/rotation/provenance/dependency acceptance. Native keyboard/clipboard
and bidirectional-text visual caret behavior have not been accepted yet.
## Slice 7: captured-drag viewport scrolling and T019 completion

The existing 100 ms window refresh now advances a captured text drag at viewport
edges; no second timer is introduced. Vertical movement brings the next/previous
visible source row into view. Long lines use the existing scroll viewer's native
horizontal line scrolling. Hit testing is clipped to the content presenter and
uses the nearest realized visible row, so an endpoint outside the window extends
the selection instead of silently stopping at the original viewport. New visible
rows receive native highlighting on refresh. Release/capture loss stops scrolling;
Escape/source binding/close clears the range and releases capture.

Two new headless scenarios hold the pointer still beyond the original viewport,
assert movement past all initially visible rows, copy through a known new endpoint,
verify upward scrolling and Escape cancellation, and exercise horizontal long-line
scrolling/release/close-all during an active captured drag. Waiting 300 ms after
release/cancellation explicitly checks stability over three refresh intervals.
No assertion timeout was extended or case skipped.

The initial fixture used a terminal newline, creating an extra indexed row; it
was corrected to exactly 200 lines. The corrected test was then run against the
committed pre-autoscroll controller and failed at the missing-scroll assertion,
recorded in artifacts/migration/t019-edge-red.log. Two intermediate focused
viewport runs also stalled; their cause was not established. Later focused tests
and the full solution passed. Retain this headless timing observation rather than
claiming the earlier failures never occurred. Temporary diagnostics were removed.

Final modern solution: 81 Archiver + 82 Avalonia = 163 pass, zero failures/skips.
Evidence: ignored artifacts/migration/t019-edge-final-focused.log and
t019-edge-solution.log. t019-edge/t019-edge.trx contains only the final Avalonia
result because the solution logger reused the same filename; the combined log
retains both executed project summaries.

T019 is now complete as a test-contract task: go-to-line/quick navigation,
character-and-line selection, original-order/text copy and the relevant validation
cases exist and pass. T029/T030 implementation/integration gates remain open,
including their earlier task dependencies. Native desktop input/clipboard,
large-file performance, bidirectional caret visuals and complete rotation/
provenance acceptance remain separate unproven gates; this is not US1 completion.