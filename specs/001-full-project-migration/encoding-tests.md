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
Conflicting BOM/manual override semantics, unavailable-name/invalid-byte UI
journeys, all historical system code pages and complete native keyboard/screen
reader acceptance still need their full parity coverage. No public release or
performance gate is closed by these functional results.
