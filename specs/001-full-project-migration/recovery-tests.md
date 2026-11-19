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
