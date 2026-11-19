# Encoding selector integration — T028 supporting slice

T027's foreground performance gate remains open. This slice advances the
independent decoder/settings integration; it does not satisfy that prerequisite
or mark T028/US1 complete.

The native Avalonia window now exposes an accessible Text encoding combo box.
It edits the existing ILogFileSettings.DefaultEncoding model, includes automatic
detection and the legacy Unicode/ASCII/UTF-7 choices, retains an injected custom
code page, and displays an unavailable persisted name without silently editing
the settings. No new settings format, provider registration or package is added.

MainWindow accepts the existing settings model explicitly. Selecting an encoding
reopens the active file through its configured source, preserving identity and
follow/pause while clearing decoder-dependent rows/selection. Changes during an
open are checked before publication and the file is reopened with the newest
choice rather than publishing the old decoder. File access still validates read
access with ReadWrite/Delete sharing through the factory. Existing Core BOM,
preamble and invalid-byte fallback behavior is retained; this is not a new strict
decoder or a claim of forced-encoding behavior for every conflicting BOM.

Initial behavior test compiled and failed because the selector did not exist
(t028-encoding-red.log/TRX). Three headless cases now cover:

- BOM-less UTF-16 selection, original Unicode copy, stable source ID, retained
  pause state and unchanged file bytes; return to automatic UTF-8 after replacement.
- A Windows-1252 setting restored from an owned XML profile feeds the reader,
  copies cafe-with-accent exactly, and clearing it persists automatic detection.
- An encoding selected during file open applies to the pending source without
  adding another source entry.

Final modern solution: 81 Archiver and 173 Avalonia pass, zero failures/skips.
Targeted Core StreamingTextLogSourceTest: 54 pass, zero failures/skips.
Native Unicode CLI/terminal-newline regression: 3 pass, zero failures/skips
(t028-native.log/TRX). No owned app/testhost processes or native fixture folders
remain after cleanup.
The modern solution also builds the retained legacy Core/Archiver targets.
Light/dark 640x400 screenshots were inspected: the selector fits and the existing
toolbar remains visible. Evidence is under ignored artifacts/migration/t028:
t028-encoding-focused.log, t028-streaming.log, t028-modern-final.log and TRX.
The solution runner overwrote the common TRX filename with the final Avalonia
result; the log retains both suite totals.

Remaining: shipping startup/profile lifecycle and automatic save are owned by
T053-T055; the default window currently uses an in-memory model and never reads
or writes the user's profile. Disk persistence above is an explicitly injected
owned profile test, not proof of restart persistence in the shipping app.
Unavailable-name/invalid-byte UI journeys, all historical system code pages and complete native keyboard/screen
reader acceptance still need their full parity coverage. No public release or
performance gate is closed by these functional results.

## Manual decoder versus conflicting BOM

The ILogFileSettings contract explicitly says a non-null manual encoding applies
regardless of file detection. A new behavior case failed: a manual UTF-8 choice
silently became UTF-16 after seeing the file BOM. Core now accepts an explicit
BOM-detection flag; existing raw-factory/constructor callers keep their previous
preamble-based behavior, automatic documents detect BOMs, and manually configured
documents keep the selected decoder. Matching selected preambles are still
removed consistently by the index and reader.

The regression exercises UTF-16 LE to UTF-8, UTF-8 to UTF-16 LE, UTF-16 BE to
UTF-16 LE, and matching UTF-8 BOM choices, with single/multiple lines. Expected
manual contents come from the selected BCL decoder (including replacement
characters for invalid sequences); automatic reads must return the original
text. Exact copied content, line counts and unchanged file bytes are checked.

Initial manual-BOM behavior case: 1 failure. Focused encoding/automatic-BOM cases:
6 pass before the multiline expansion. Full Core: 7,158 pass / zero failures /
five existing skips. Evidence: t028-manual-bom-red.log, t028-manual-bom-focused.log,
t028-manual-bom-core.log and matching TRX under artifacts/migration/t028.

The final expanded modern suite passes 81 Archiver + 174 Avalonia tests, zero
failures/skips (t028-manual-bom-modern.log, separate TRX files in
t028/manual-bom-modern/). The solution also compiles the legacy net45/net452
targets. No native suite or five-minute gate was repeated for this decoder-mode
change; the earlier native 3/3 checkpoint remains separate. Shipping profile
lifecycle, full code-page/native acceptance and T027 remain pending.

## Unavailable saved selection versus retained live fallback

A new headless case initially failed when an unavailable persisted encoding
retained a valid prior ASCII fallback: the combo box incorrectly displayed ASCII
as the saved selection. LogFileSettings now records whether its last restored
selection resolved, independently of the fallback decoder. Clone preserves that
state; explicit selection/automatic or a subsequent successful restore clears
it. The XML attribute and unknown original name remain unchanged until the user
chooses a replacement. EncodingView displays the unavailable name for both null
and non-null fallback cases, without overwriting the model during construction.

The fixture checks both fallback states, clone independence and explicit return
to automatic. Core model checks cover availability, clone/save preservation and
recovery to a known selection. No provider, serialized field or disk profile
startup behavior was added. Evidence: t028-unavailable-red.log (1 failure),
t028-unavailable-focused.log (1 pass), t028-unavailable-core.log (28 pass),
with separate TRX directories under artifacts/migration/t028.

Final affected Core settings/profile cases: 28 pass, zero failures/skips
(t028-unavailable-core-final.log/TRX). Final modern solution: 81 Archiver +
175 Avalonia pass, zero failures/skips (t028-unavailable-modern.log and separate
TRX files). The earlier 7,158-case full Core checkpoint belongs to the preceding
manual-BOM change; only affected settings/profile cases were repeated afterward.

The original legacy WPF Tailviewer.Tests build was attempted, not passed:
without a developer pack it reports MSB3644; supplying the already installed
net471 reference-assembly package gets past that check but reports missing
packages/NUnit.3.12.0/build/NUnit.props. Logs:
t028-unavailable-legacy.log and t028-unavailable-legacy-reference-pack.log.
This is distinct from the passing retained Core/Archiver target builds in the
modern solution. No legacy test runtime or new native suite was run in this
settings-only slice. Shipping profile lifecycle and native performance remain open.

## Windows system code page choice

The legacy default option used the Windows system code page, while .NET 10
Encoding.Default is UTF-8. On the recorded Windows reference host, direct BCL
observation reports code page 1252/windows-1252 versus modern 65001/utf-8. The
selector initially omitted windows-1252 unless a profile injected it; a new
behavior case failed because no such choice existed. The list now obtains the
system code page from the already available CodePagesEncodingProvider without
registering a global provider or adding a dependency. It retains the separate
UTF-8 option and deduplicates equivalent code pages by the existing web-name rule.

The headless case chooses the system encoding without an injected profile,
opens its synthetic encoded file and verifies rendered text and exact copy.
Focused system-codepage/profile/unavailable cases pass 3/3. A real-process native
case opens the dropdown through UIA, locates the encoding only within windows
owned by its app PID, selects it, requires the decoded text, copies through the
native clipboard and verifies fixture bytes unchanged: 1/1 pass. The fixture's
existing owned-process/temp-directory/conditional clipboard restoration applies.

Evidence under artifacts/migration/t028: t028-system-codepage-red.log,
t028-system-codepage-focused.log and t028-system-codepage-native.log with TRX.
The native run uses the current host's system code page; it is not proof of
every historical Windows code page or the five-minute foreground performance
gate. No shipping profile startup/save or import behavior changed.

Final full Avalonia suite: 176 pass, zero failures/skips
(t028-system-codepage-full.log/TRX). Cleanup audit finds no owned viewer/testhost
processes or native fixture directories. Core and Archiver suites were not
repeated for this UI choice-list change; their preceding checkpoints remain
separate. T028/US1 and full migration are still open for remaining integration
and acceptance, including shipping restart persistence.
