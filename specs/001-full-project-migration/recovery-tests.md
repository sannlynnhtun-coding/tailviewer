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

## Streaming index-column request bounds (T026)

While reviewing the source-position path, GetIndices was found to iterate over
the entire indexed file instead of the requested row list. Empty and sparse
requests could throw after writing part of the destination; out-of-range rows
could also be reported as valid indices. The private helper has one caller in
StreamingTextLogSource.GetColumn; its callers keep the same public API.

The helper now validates the destination slice before writing, fills only the
requested positions in request order (including duplicates), and uses the Index
column's Invalid default for absent rows. Six cases cover empty/single/sparse/
duplicate/out-of-range requests, destination offsets and rejected slices retaining
all sentinel values. The original four behavior cases recorded three failures
and one pass before the fix. The complete streaming fixture now passes 34 cases.

Evidence under ignored artifacts/migration: t026-index-red.log and
t026-index-focused.log. This is a narrow shared-source position fix, not completion
of T026 or the future filter/merge/provenance integration. Reverse original-index
lookup and full derived-source integration remain separate work.

Full verification after the index fix: Core 7,134 pass / zero failures / five
existing skips; modern solution 81 Archiver + 169 Avalonia pass / zero failures
or skips. Legacy net471 test-project build passes with zero errors and five
existing warnings. Logs: t026-index-core-full.log, t026-index-modern.log and
t026-index-legacy-build.log under ignored artifacts/migration. Legacy runtime,
native entry-point and performance suites were not rerun for this shared helper
fix. No assertion was weakened or existing skip added.

## Streaming original-position lookup (T026)

StreamingTextLogSource.GetLogLineIndexOfOriginalLineIndex still threw
NotImplementedException. Existing GenericTextLogSource, PageBufferedLogSource,
column adorner and source proxy delegate to this contract. The raw streaming
source has no transformed row order, so the implementation now returns the same
position only when it exists in the current index; invalid, unavailable or
out-of-range positions return LogLineIndex.Invalid under the index lock.
No caller signature or persisted representation changes.

A manual-scheduler regression first failed with the runtime
NotImplementedException. It now covers pre-index/empty, indexed first/last rows,
out-of-range, append, truncate, deletion, recovery and disposal. The full
streaming fixture passes 35 cases. Evidence: ignored artifacts/migration/
t026-original-position-red.log and t026-original-position-focused.log.

This is current-position lookup, not content-based relocation of old bookmarks
or proof of complete filter/merge provenance. Revision-aware consumers and the
later bookmark integration still need their own acceptance evidence. T026 and
the full migration remain incomplete.

Full verification: Core 7,135 pass / zero failures / five existing skips; modern
solution 81 Archiver + 169 Avalonia pass / zero failures or skips. Legacy net471
test-project build passes with zero errors and five existing warnings. Logs:
t026-original-position-core-full.log, t026-original-position-modern.log and
t026-original-position-legacy-build.log under ignored artifacts/migration.
Native/performance and legacy runtime suites were not executed for this helper.

## Concurrent write between offset scan and digest (T027)

A deterministic owned-stream fixture rewrites a small file immediately after the
offset detector reads its old bytes, preserving length and write timestamp. The
old scanner retained four offsets against the final three-line file even after
three polling passes: it paired old offsets with a digest of the new contents.
The companion append-during-scan case pins eventual append convergence.

A changed scan is now bounded to the length observed at its start. It compares
full-prefix digests before and after indexing and discards/reset-invalidates the
generation on a changed digest, a short scan or truncation. Bytes appended beyond
the bound are processed next time; they do not require discarding a stable prefix
under a continuously appending writer. Idle checks retain their existing full
indexed-prefix audit. Changed scans need extra read work; this is not a free
performance improvement or proof of representative large-file latency.

The rewrite case failed with expected three / actual four lines before the fix;
the initial combined streaming/offset-detector fixtures then passed 99 cases.
Logs under ignored artifacts/migration: t027-concurrent-scan-red.log and
t027-concurrent-scan-focused.log.

T027 remains incomplete. Digest bracketing is not an atomic snapshot and cannot
prove consistency against an ABA write that restores bytes between probes;
observed-byte validation is still required. Representative 1 GiB/multi-source and
five-minute append latency gates remain pending. This section supersedes only
the single overlapping rewrite/index-digest pairing limitation, not those gates.

An additional retained-prefix fixture rewrites the already indexed prefix after
the idle audit but before the full pre-scan hash during an append pass. It first
failed with expected four / actual five rows. Incremental scans now revalidate
that retained prefix after indexing too. The fixture disables FileStream's extra
read cache so its gates distinguish fresh byte reads from cached data. This
narrows a second ordinary single-rewrite race; it does not solve the ABA limit.
Final combined streaming/offset fixtures: 100 pass. Additional evidence:
t027-retained-prefix-red.log and t027-concurrent-scan-focused-final.log.

Measured in the final full Core run on Intel Core i7-13700H (14 cores/20 threads),
SDK 10.0.302, owned local temporary files; cache/background load was uncontrolled:
1 MiB initial 20.71 ms, idle audits 1.34-1.42 ms, recovery 14.58 ms;
100 MiB initial 608.52 ms, idle audits 149.41-178.08 ms, recovery 623.23 ms.
These are synthetic samples from the existing fixture, not a representative
hardware matrix or a 1 GiB/continuous-append gate. Full per-test stdout is in
ignored t027-concurrent-final/t027-concurrent-final.trx.

Final verification after both race fixes: Core 7,138 pass / zero failures / five
existing skips; modern solution 81 Archiver + 169 Avalonia pass / zero failures
or skips; targeted native Unicode CLI/picker 3 pass / zero failures or skips.
Legacy net471 test-project build passes with zero errors and five existing
warnings; legacy runtime tests were not executed. Final logs under ignored
artifacts/migration: t027-concurrent-final-core.log,
t027-concurrent-final-modern.log, t027-concurrent-final-legacy-build.log and
t027-concurrent-final-native.log. Earlier green full-run counts predate the
retained-prefix regression; final figures above supersede them. No assertion
was weakened and no skip or warning suppression was added.

## Observed-byte checkpoint validation (T027)

Two ABA fixtures first failed despite the pre/post digests agreeing: a temporary
newline rewrite left three rows for a restored four-row file, and a temporary
UTF-16 BOM swap left one row. Both fixtures restore exact bytes, length and write
time before the post-scan audit and prove that their read/write gates ran.

Checkpoints now come from the same byte buffers consumed by the offset detector.
A second rolling digest of those observed bytes validates the prefix belonging
to retained indices during incremental append. The entire observed digest is
compared with the current bounded prefix before publication, and that observed
digest becomes the checkpoint. Independent pre-scan/post-retained reads are no
longer used as the evidence for what the detector actually saw.

BOM detection uses a bounded four-byte snapshot, and the bytes subsequently
observed for hashing must agree with that encoding snapshot. First-line BOM
skipping uses the same snapshot rather than a separate file read. Short prefix
reads are handled without assuming one Read fills the buffer. Existing UTF-8,
UTF-16/32 BOM/replacement, short leading-newline, append and stale-read tests
remain in the focused run. SHA-256 is reused from the BCL; no new dependency,
persistent unbounded buffer or file-write operation was added to production.

Focused streaming/offset fixtures: 102 pass / zero failures or skips. Evidence
under ignored artifacts/migration: t027-observed-bytes-red.log and
t027-observed-bytes-focused.log. This supersedes the preceding ABA shortcut for
these source paths; it does not claim an OS-level atomic file read or establish
representative latency under large/changing workloads. T027 remains open for
1 GiB/multi-source and five-minute append performance/recovery acceptance.

Final validation: Core 7,140 pass / zero failures / five existing skips; modern
solution 81 Archiver + 169 Avalonia pass / zero failures or skips; targeted native
Unicode CLI/picker 3 pass / zero failures or skips. Legacy net471 test-project
build passes with zero errors and five existing warnings; legacy runtime tests
were not executed. The original modern run stopped before its Avalonia result;
no test process remained, so only unfinished checks were resumed. The completed
Core run was retained rather than repeated.

Final evidence under ignored artifacts/migration: t027-observed-bytes-core-full.log,
t027-observed-bytes/t027-observed-bytes.trx,
t027-observed-bytes-modern-resumed.log, t027-observed-bytes-legacy-build.log and
t027-observed-bytes-native.log. Synthetic benchmark stdout from that full Core
run: 1 MiB initial 20.16 ms, idle audits 1.41-1.48 ms, recovery 17.04 ms;
100 MiB initial 690.85 ms, idle audits 150.51-175.95 ms, recovery 639.81 ms.
Same local i7-13700H test environment as the preceding sample; uncontrolled
cache/background load means this is not an apples-to-apples speed comparison or
a performance acceptance gate. No assertion was weakened or skip added.

## Manifest-sized record workload: failing recovery preflight (T027)

Added tools/Tailviewer.SourcePerformance as a standalone Core-only self-check,
kept outside routine solution/test discovery. It generates the exact manifest
PERF-100MB/PERF-1GB 128-byte record shapes, performs three read-through warm-cache
runs for each, checks the first 32 decoded records and every byte offset, changes
one middle LF while restoring final size/write time, then checks every new offset
and the exact 255-character merged record. EOF position is accounted separately
from complete records. Failed budgets return nonzero after all runs; thresholds
are not relaxed. Owned temporary data is removed in finally, with a checked path.
The harness requires reference CPU/memory and 2 GiB of free temporary disk space.

Reference host: Windows x64, Intel Core i7-13700H (14 cores/20 logical processors),
31.71 GiB available to the runtime, C: on INTEL SSDPEKNU010TZ NVMe SSD. SDK
10.0.302 / runtime 10.0.15. OS/background load and cache residency are uncontrolled;
read-through warming is reported as intent, not proof of cache residency.

| Core measurement | 100 MiB (819,200 complete records), three runs | 1 GiB (8,388,608 complete records), three runs |
|---|---|---|
| First decoded 32 records | 301.77 / 24.68 / 10.49 ms | 14.74 / 17.51 / 21.07 ms |
| Complete index | 1,389.89 / 546.38 / 643.00 ms | 5,047.71 / 4,982.70 / 5,089.57 ms |
| Recovery after middle LF replacement | 567.12 / 599.87 / 568.37 ms | **6,733.42 / 5,624.92 / 5,573.96 ms — FAIL** |
| Process lifetime peak working set sampled after indexing | 77,082,624 / 82,477,056 / 83,079,168 bytes | 250,871,808 / 442,875,904 / 463,511,552 bytes |
| Every original/recovered offset and merged content | Verified | Verified |

Evidence: ignored artifacts/migration/t027-large-records.log (exit 1 for all
three 1 GiB recovery budget failures); new harness build passes with zero errors
and two visible inherited NU1902 warnings (t027-large-records-build.log). The
reference hardware/free-space assertions were added after the measured run and
compiled; their inputs were independently inspected on this host. No temporary
fixture directories remained after the run.

This is a reproducible **failing** T027 performance baseline, not an acceptance
pass or a full-migration completion claim. Core readiness is not rendered UI,
Core process working set is not complete-application memory, and native p95
interaction, five-minute live tail and the wider source matrix remain unmeasured.
No production source was changed and functional suites were not repeated for
this standalone harness. Reproduce with the command in its README; fix the
recovery bottleneck rather than changing SC-006's three-second threshold.

## Safe bounded bulk append prerequisite (T027)

Reviewing the allocation-heavy offset insertion path found an existing typed
LogBufferList.AddRange overload ignoring count and appending the entire backing
array. Four regressions first fail: zero/partial appends leak unused values into
a later row, and negative/oversized counts are accepted instead of preserving
existing columns. The public boundary now validates count before mutating any
column, and the column implementation appends only the requested ArraySegment.
No new helper or dependency was added. Caller audit found the forwarding method
and existing fixtures; the scanner will reuse this path in a separate change.

Focused LogBufferList suite: 132 pass / zero failures or skips. Legacy net471
test-project build: zero errors / five existing warnings. Evidence under ignored
artifacts/migration: t027-bulk-count-red.log, t027-bulk-count-focused.log and
t027-bulk-count-legacy.log. Full source suites/performance will be rerun after
the scanner adopts the corrected bulk operation.

Critical-only performance scan scope: StreamingTextLogSource, LogEntry and
LogBufferList. Execution checklist: literal IndexOf 0, Substring 0, literal
StartsWith/EndsWith 0, literal Contains 0; exact counts are in
artifacts/migration/t027-perf-scan.json. The relevant allocation finding is the
per-offset LogEntry construction in AddLines (list/dictionary/boxed value),
not those absent string patterns. Benchmark evidence remains necessary; static
inspection alone is not evidence that the three-second recovery gate passes.

## Batched offset indexing and snapshot-checkpoint audit (T027)

The measured hot insertion loop built a LogEntry/list/dictionary/boxed value for
every offset. A new 20,000-record allocation contract first failed at 9,199,528
bytes against its 4 MiB ceiling. It now uses the corrected typed bulk append;
all offsets, including EOF, remain verified. Initial focused allocation after
bulk insertion was 537,040 bytes. The scanner's first-entry helper remains for
one entry per source; no per-row abstraction or dependency was added.

The single-byte LF path uses BCL Array.IndexOf; UTF-16/32 keep their existing
aligned matching. The observed-byte checkpoint already identifies indexed bytes,
so a redundant independent post-scan full digest read was removed. Every indexed
byte is still checked by the next full SHA-256 audit; an observed retained-prefix
mismatch, decoder-prefix mismatch, short/truncated pass or reset invalidates the
generation. ABA tests still converge. The retained-prefix fixture now gates the
actual observed-prefix read, rather than the retired pre-scan full digest read.
Audit/scan buffers are bounded at 64/16 KiB. Digest strength, byte coverage, performance thresholds and stale-publication fences are retained; the eager post-read comparison is replaced by next-pass auditing against the observed checkpoint.

Intermediate runs remain evidence, not hidden failures: bulk-only recovery
5.01-5.12 s; single-byte fast path 3.84-4.21 s; snapshot checkpoint 3.14-3.93 s;
buffer-change run 3.75/4.90/17.97 s. The last run's wall-time outlier and varying
CPU/wall deltas show that background load/launch conditions were uncontrolled.
These numbers are not an isolated causal comparison of each optimization.
Logs: t027-bulk-index-records.log, t027-vector-index-records.log,
t027-index-snapshot-records.log and t027-batched-index-records.log.

An isolated BCL hash probe measured SHA-256 at 712-775 ms/GiB and SHA-512 at
2,476-2,649 ms/GiB on this host; SHA-256 was retained. Eight owned-process stack
samples found offset scanning and checkpoint/observed hashing; these are limited
stack observations, not statistically sampled CPU percentages. The diagnostic
process contained only synthetic fixtures and no heap dump was taken. Its timed
results are excluded from acceptance. Ignored evidence: t027-hash-speed.log,
t027-profile-stack-*.log and t027-profile.out.log.

A subsequent unprofiled standalone apphost run (Start-Process -WindowStyle Hidden,
no priority/affinity override, same built binaries) exited 0. All original and
rewritten offsets and merged content pass in every run:

| Core measurement | 100 MiB, three runs | 1 GiB, three runs |
|---|---|---|
| First decoded records | 176.71 / 13.59 / 16.38 ms | 23.00 / 12.18 / 41.70 ms |
| Complete indexing | 295.32 / 123.42 / 142.32 ms | 1,152.86 / 1,540.96 / 1,523.32 ms |
| Hidden-rewrite recovery | 253.40 / 189.69 / 247.92 ms | 2,153.50 / 2,385.29 / 2,039.96 ms |
| Process lifetime peak working set after indexing | 64,016,384 / 94,093,312 / 114,302,976 bytes | 249,303,040 / 527,978,496 / 856,424,448 bytes |

Final workload logs: t027-batched-final.out.log and t027-batched-final.err.log.
This is a passing Core preflight on the recorded host, not proof of stable native
UI/application-memory latency under all load. T027 remains open for five-minute
live tail, broader recovery/performance coverage and prerequisite integration;
P13 still requires rendered UI/navigation/memory acceptance.

Final functional validation of the combined prerequisite/performance changes:
Core 7,145 pass / zero failures / five existing skips; modern solution
81 Archiver + 169 Avalonia pass / zero failures or skips; targeted native Unicode
CLI/picker 3 pass / zero failures or skips. Legacy net471 test-project build
passes with zero errors and five existing warnings; legacy runtime was not run.
Final 20,000-record allocation contract reports 548,832 bytes (about 94% below
the recorded failing baseline), with every expected offset checked. Logs under
ignored artifacts/migration: t027-bulk-final-core.log,
t027-bulk-final/t027-bulk-final.trx, t027-bulk-final-modern.log,
t027-bulk-final-legacy.log and t027-bulk-final-native.log. No owned performance
fixture directories remained. No performance threshold or assertion was weakened.

## Five-minute Core live-tail preflight (T027)

Added the fixed-duration `tail` workload to the existing standalone performance
harness. It writes 100 complete LF-terminated integer records every 100 ms for
300 seconds: 300,000 records at the manifest's 1,000-lines/second rate. One
sample per batch reads the last record through Core; final validation reads
all complete records in bounded batches and compares the exact sequence
0..299999. The terminal EOF index position is accounted separately. Unknown
modes exit 2 before fixture creation. The producer is cancelled/awaited on failure
before source/owned temporary-directory cleanup; there is no short-run option.

An unprofiled standalone apphost run on the same recorded Windows x64 reference
host exited 0 with these results:

| Measurement | Result |
|---|---|
| Writer duration | 300.0024864 seconds |
| Complete records / readiness samples | 300,000 / 3,000 |
| Core-ready nearest-rank p95 | 125.2431 ms (budget 1,000 ms) |
| Maximum sampled readiness | 202.8216 ms |
| Writer p95 lateness against 100 ms batch deadlines | 13.8421 ms (qualification limit 100 ms) |
| Every final complete record | Exact sequence verified |
| Indexed terminal EOF positions | 1, separate from complete records |

Evidence under ignored artifacts/migration: t027-live-tail.out.log,
t027-live-tail.err.log (empty), t027-live-tail-build.log (zero errors, two visible
inherited NU1902 warnings) and t027-live-tail-arguments.log (unknown mode rejected
with exit 2). Progress is logged every 15 seconds. No owned performance fixture
directories remained after exit. Reproduce with the command in the tool README.

No production source changed in this slice; the functional suite baseline from
the preceding commit was not rerun for a standalone harness change. This proves
Core availability after writer flush, **not native UI display latency**. It does
not close all of SC-006 or T027: rendered five-minute tail, interaction/pause,
broader recovery/performance matrices and prerequisite integration remain pending.
The full migration remains active and no user-story gate is marked complete.
