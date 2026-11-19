# T020 recovery contracts — in progress

## Slice 1: truncate/regrow and reset read publication

StreamingTextLogSource had no direct fixture at the planned test path. A new
fixture uses the existing ManualTaskScheduler and real files confined to an owned
GUID temporary directory. Scheduler steps establish scan/read order without
watcher sleeps or changes to production scan intervals.

Five cases now cover:

- Truncate to empty, scan the empty state, then regrow beyond the original size.
- Truncate/regrow entirely between scans, preserving final Unicode/whitespace.
- Contiguous and fragmented reads queued before a same-size reset must return
  default/null results instead of replacement content under old coordinates.
- An already-running request paused before publication must not copy old bytes
  into a destination after reset. An owned gate is released in a finally block.

The queued-read cases initially failed with `new` returned instead of null;
truncate/regrow cases already passed. Production requests now capture the source
generation at creation. Reset, unavailable-source index clearing and disposal
advance that generation under the index lock. Destination publication checks the
captured generation under the same lock, preserving existing request cancellation
locking and filling stale destination ranges with defaults. Tests prefill those
ranges with a sentinel so doing nothing cannot accidentally pass the assertion.
Fresh requests after reset still read the complete current contents.

## Executed evidence

| Check | Result |
|---|---|
| Initial recovery cases | 2 pass, 2 meaningful queued-read failures |
| Focused cases after correction | 4 pass, before adding the gated in-flight case |
| Complete net10.0 Core suite including all five new cases | 7,089 pass, 0 failed, 5 existing skips |
| Legacy net471 Core test project build | 0 errors, 5 existing warnings |
| Modern solution | 81 Archiver + 82 Avalonia pass, 0 failures/skips |

The five existing Core skips are TestEmptyConstruction1, TestTrueAndFoobar,
TestParseStringLiteralWithBackSlash, TestParseStringLiteralWithQuotes and
TestGetTimestamp4. None was added, removed or suppressed in this slice.
Known legacy log4net NU1902 and existing test warnings remain visible.

Ignored evidence under artifacts/migration: t020-queued-red.log,
t020-queued-focused.log, t020-reset-core-full.log, t020-reset/t020-reset.trx,
t020-reset-legacy-build.log and t020-reset-modern-solution.log.

## Remaining detection and encoding scope

T020 remains unchecked: middle-only same-size rewrites with indistinguishable
metadata and changed-encoding fixtures still need executable contracts and fixes.
The current scanner checks only the first/last 4 KiB of indexed content. Identical
metadata and identical sampled bytes cannot distinguish an edit confined to an
unsampled region; the existing shortcut comment explicitly acknowledges this.
This slice does not alter that detection policy or claim SC-003/SC-006 recovery.

T027 must resolve that correctness gap and measure the cost against the 100 MB/
1 GB performance fixtures. Reading all prior bytes is linear in file size, and
metadata/sample-only polling cannot prove arbitrary hidden middle rewrites.
The three-second convergence target remains unchanged and unproven; filesystem/
I/O limits must be recorded as issues rather than weakening the test silently.
Changed-decoder/index invalidation also remains part of T020/T028. Legacy tests
were built but not executed; native entry-point and performance acceptance were
not performed here.

## Slice 2: BOM replacement and aligned Unicode offsets

Four replacement fixtures cover UTF-16 LE/BE and UTF-32 LE/BE, Burmese, emoji,
spaces, append after replacement and a later return to BOM-less UTF-8. They read
individual later rows first, through both contiguous and fragmented query paths.
An initial whole-file-only test passed despite wrong random-access offsets;
adding isolated later-row reads produced four meaningful wrong-text failures.
The scanner now resolves the current BOM before indexing and uses that encoding
for both the initial preamble offset and newline detection. Each actual read
request resolves the BOM before seeking past the header, inside existing request
error handling. No idle read prefetch is introduced. The shared reader retains
the platform's original 1 KiB buffer size and existing BOM detection policy.

A separate explicit preambleless-encoding fixture preserves the previous policy:
BOM bytes remain content when detection was disabled by the supplied encoding.
This is not a new settings option, BOM-less encoding inference, or a completed
encoding-selector UI (T028).

Eight additional LineOffsetDetector cases reject false newline patterns spanning
adjacent UTF-16/32 code units. Four failed before correction; four small 3-byte
buffer variants additionally exercise split patterns/code units. Matching now
begins only on the correct code-unit boundary. Single-byte newline encodings keep
the fast path without the alignment modulo operation.

Verification:

- Full Core checkpoint: 7,102 passed, 0 failed, 5 unchanged existing skips.
- Legacy net471 Core test project: build passed, 0 errors, 5 existing warnings.
- After lazy request BOM initialization, modern solution: 81 Archiver + 82
  Avalonia passed, 0 failures/skips.
- After the final reader buffer-size preservation, all 72 affected streaming/
  offset-detector tests passed again. The entire Core suite was not repeated for
  that buffer-size-only refinement.

Ignored evidence: artifacts/migration/t020-encoding-red.log,
t020-alignment-red.log, t020-encoding-focused.log,
t020-encoding-final-focused.log, t020-encoding-core-full.log,
t020-encoding/t020-encoding.trx, t020-encoding-legacy-build.log and
t020-encoding-modern-final.log. The broader initial solution checkpoint is also
retained as t020-encoding-modern-solution.log.

T020 remains open for middle-only same-size rewrites with indistinguishable
metadata. Its detection/performance issue above is unchanged. Automatic detection
of arbitrary BOM-less encoding changes, manual persisted encoding selection,
full recovery performance and native desktop acceptance are not proven here.
## Slice 3: hidden middle rewrites and T020 completion

The equal-size fixture moves a newline in the unsampled middle while keeping the
first/last 8 KiB unchanged. It explicitly restores the original last-write time
and asserts FileFingerprint equality (path, size, creation/write times) before
scanning. The changed-metadata control passed; the hidden-metadata variant failed
with four stale indexed rows instead of the expected three.

Content checkpoints now hash all previously indexed bytes with the built-in
SHA-256 implementation. A reusable 16 KiB buffer bounds audit memory; cancellation
is checked between chunks. Appended bytes are excluded from the old-prefix check,
so normal append still extends the index rather than resetting it. No dependency,
configuration option, ignored case or scan interval was added. The old bounded-
probe shortcut is removed; this is deliberately a linear full-content audit.

Two additional real-file fixtures (1 and 100 MiB) replace a middle byte with a
newline while retaining equal metadata, verify exact resulting byte offsets and
report initial scan, three idle audits and recovery cost. These fixtures contain
one very long line, not the representative multiline application acceptance log.

Measured on Windows x64 / SDK 10.0.302, Intel Core i7-13700H (14 cores / 20 logical
processors), 31.71 GiB RAM, owned temporary files on C: NTFS. Drive media was not
verified. The synthetic file was just written and audits were sequential/warm:

| Size | Initial scan + digest (ms) | Three idle audits (ms) | Hidden rewrite/reindex (ms) |
|---|---:|---|---:|
| 1 MiB | 11.91 | 1.44, 1.03, 0.99 | 8.82 |
| 100 MiB | 226.08 | 85.28, 80.45, 75.44 | 288.20 |

Full-content polling increases I/O/CPU linearly with file size and source count.
A 100 ms scheduling delay follows each scan; this is not a fixed-duration audit.
These results do not prove the 1 GiB, five-minute append, UI latency or universal
three-second recovery targets. T027/T120 must measure representative workloads
and record filesystem/throughput limits; the original acceptance thresholds are
unchanged. Indexing and digesting currently use separate reads: snapshot
consistency under a concurrent rewrite is a known remaining T027 issue, also
marked in code. No atomic filesystem snapshot guarantee is claimed.

Executed checks: full Core 7,106 pass / 0 failed / 5 unchanged existing skips;
legacy net471 test project build 0 errors / 5 existing warnings; modern solution
81 Archiver + 82 Avalonia pass / 0 failures/skips. Evidence under ignored
artifacts/migration: t020-middle-red.log, t020-middle-focused.log,
t020-middle-cost.log, t020-middle-core-full.log, t020-middle/t020-middle.trx,
t020-middle-legacy-build.log and t020-middle-modern-solution.log.

T020 is complete as a test-contract task: hidden middle/identical-metadata,
truncate/regrow, changed-BOM encoding and stale queued/in-flight result cases now
exist and pass. T027/T028 and native/performance acceptance remain open. This
completion does not claim full recovery implementation or US1 parity.
## Slice 4: append during an empty scan

While verifying the folder-picker slice, the existing empty/partial-line Avalonia
case stalled at its first two-line count. A deterministic real-file gate now
appends immediately after a stream Length check has returned zero. Before the
fix, two scanner passes permanently indexed one line instead of two: the first
line was omitted but the new content digest was accepted.

An empty length snapshot is now completed as empty without feeding a racing
append to the line detector. The next pass discovers and indexes all appended
bytes from offset zero. Actual stream length also participates in the no-change
check so bytes not yet indexed are not hidden by an earlier metadata snapshot.
The gate uses existing Moq/manual-scheduler tooling and owned local FileStream;
no sleep, skip or enlarged timeout makes it pass.

Results: all 15 streaming recovery cases pass; the original partial-line case
passes in isolation; full Core 7,107 pass / 0 failed / 5 unchanged existing skips;
legacy net471 test-project build passes with 0 errors / 5 existing warnings;
modern solution 81 Archiver + 85 Avalonia pass. Modern solution verification also
included the pending folder-picker UI work, excluded from this Core fix commit.

Evidence: artifacts/migration/t024-folder-modern.log (initial observed failure),
t020-empty-race-red.log, t020-empty-race-focused.log,
t020-empty-race-ui-focused.log, t020-empty-race-core-full.log,
t020-empty-race/t020-empty-race.trx, t020-empty-race-legacy-build.log and
t024-folder-modern-final.log. General concurrent rewrite/index/digest snapshot
consistency remains the separate T027 issue described above.
## Slice 5: BOM-less short and empty leading lines

Five deterministic cases exposed skipped leading newlines when a BOM-enabled
UTF-8 fallback read BOM-less files. All five failed before the fix, including a
one-character file whose later append lost a line. Preamble probing consumed
bytes without restoring the scanner position when no preamble matched.

Resetting the stream to the actual first-line offset before the existing index
check fixes both initial scans and rescans after append. Tests assert exact raw
text and line counts for short lines, empty leading lines, LF and CRLF, before
and after append. The native command-line/clipboard case now also opens a file
with an empty first line and verifies the original Unicode second line.

Results: streaming fixture 20 pass; full net10.0 Core 7,112 pass / 0 failed /
5 unchanged existing skips; net471 test-project build 0 errors; modern solution
81 Archiver + 90 Avalonia pass; targeted native command-line/clipboard 2 pass /
0 failed / 0 skipped. No new dependencies, skips or timeout changes.

Evidence under ignored artifacts/migration: t020-preamble-red.log,
t020-preamble-focused.log, t020-preamble-core-full.log,
t020-preamble-legacy-build.log, t020-preamble-modern.log,
t020-preamble-native.log and the corresponding preamble/native TRX files.
General concurrent rewrite/index/digest consistency and representative large-file
performance remain T027 work. The legacy test project was built, not executed.

## Default file-open factory and BOM replacement fallback (T022/T027)

LogDocument previously probed a file with StreamReader.Peek and passed that
initial detected encoding as Core's permanent fallback. A meaningful UI regression
opens UTF-16/BOM text, replaces it with BOM-less UTF-8 and verifies new Unicode
rows/copy. It failed before the change because the initial UTF-16 encoding was
retained after its BOM disappeared.

StreamingTextLogSourceFactory now has an existing-file default entry point. It
normalizes the path and validates read access with writer/delete sharing, then
uses UTF-8 fallback with the scanner/reader's existing per-read BOM detection.
LogDocument delegates to that entry point and removes its duplicate BOM probe.
The existing three-argument factory/interface entry point remains unchanged for
explicit encodings and legacy/plugin consumers; call sites were audited.

Eight new Core cases cover UTF-8 with/without BOM, UTF-16 LE/BE, UTF-32 LE/BE,
leading empty/Unicode lines, replacement by BOM-less UTF-8, and missing/locked
file rejection followed by recovery. Streaming fixture 28/28 passes. Complete
Core net10.0: 7,120 pass / 0 failed / 5 unchanged pre-existing skips. Legacy
net471 test-project build: zero errors, five existing warnings (not executed).
Modern solution: 81 Archiver + 166 Avalonia pass. Native command-line variants,
file picker and secondary-instance handoff: 4/4 pass, zero skips.

Ignored evidence under artifacts/migration: t022-file-open-red.log,
t022-file-open-focused.log, t022-file-factory-core-focused.log,
t022-file-factory-core-full.log, t022-file-factory-legacy-build.log,
t022-file-factory-modern.log, t022-file-factory-native.log and corresponding TRX
files. No packages, warning suppression or test timeout changes were introduced.

T022 remains partial for inactive-source lifecycle/state monitoring. This does
not complete T027's concurrent rewrite/performance requirements or T028's explicit
encoding selector/persistence. The entire native gesture matrix was not rerun
for this file-opening change; the four relevant entry-point cases were executed.
