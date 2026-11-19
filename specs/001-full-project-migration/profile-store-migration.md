# T012 — bounded profile IO and atomic saves

## Slice 1: shared bounded reads

ProfileStore.Read is shared by modern ApplicationSettings and Bookmarks.
It opens a read handle that allows replacement/deletion, so an in-progress read
can retain its coherent old file handle while a writer publishes a new file.
Reads reject files above 16 MiB, XML reader depth above 64 and document character
counts above 16 Mi characters. DTD processing is explicitly prohibited and the
resolver is null. A bounded preflight depth scan precedes tree allocation;
the second bounded pass preserves XML whitespace/document nodes. Only an
unqualified xml document root is accepted. Limits are safety policy, not a
claim that every historical plugin payload fits; oversized/deep input remains
unchanged and saves stay blocked until the user supplies a valid profile.

The bookmark snapshot now keeps an XDocument, so processing instructions and
comments outside its root survive clone/save alongside opaque bookmark payloads.
Modern bookmark errors report exception types without configuration contents.
Legacy IO remains conditional and unchanged. No real user profile is inspected.

Executed: initial seven public-model behavior cases **5 failed / 2 passed**;
DTD rejection already passed, while size/depth and outer document metadata failed.
After the reader port, **51 affected profile/bookmark/original-host cases passed**,
no failures/skips. Expanded net452 source/API build passed with the existing
legacy log4net advisory warnings. Logs: artifacts/migration/t012-read-red.log,
t012-read-green.log, t012-read-legacy.log.

T012 remains open: saves still use the old direct writer in this slice.
Candidate validation, flush/replacement/failure/concurrent-reader tests follow
before this task is marked complete or modern UI persistence is connected.

Implementation reference: [Microsoft XML reader settings](https://learn.microsoft.com/en-us/dotnet/api/system.xml.xmlreadersettings?view=net-10.0).


## Slice 2: validated same-volume publication

Both modern models now call ProfileStore.Write instead of opening/truncating the
destination. It creates a unique CreateNew candidate in the same directory,
serializes UTF-8, enforces output size, flushes file data with Flush(true), reopens
through the bounded reader, and runs the same model parser as Restore before
publishing. Existing files use File.Replace; first saves use non-overwriting
File.Move. No copy/delete/truncate fallback exists. Failed serialization, XML or
model validation, sharing violations and read-only replacement leave prior bytes
intact. Cleanup is limited to the candidate actually created by that invocation;
abandoned/foreign temporary files are never guessed as a source or swept away.

Readers allow deletion/replacement but not in-place writes. The old open handle
continues reading its complete old snapshot after publication. Concurrent writers
publish complete validated snapshots; the last completed replacement wins (no
cross-process settings merge). An IO failure can return a failure to one writer
without corrupting the destination. Import keep/replace choices and backup hashes
remain T013/T014, not an implicit conflict policy hidden in this runtime writer.

A new compatibility regression exposed carriage-return character references
being normalized to LF in opaque text by NewLineHandling.Replace. Entitize now
preserves the parsed text characters. UTF-16 input, Unicode/CDATA and the exact
16 MiB read boundary also pass. The UTF-16 test's first comparison included
writer-added formatting whitespace; the final assertion compares XML semantics,
without relaxing Unicode/CDATA or text-value preservation.

Executed evidence:

- Public held-reader atomic-save cases first **2 failed / 0 passed**; both now pass.
- Write fault cases cover partial serialization IO errors, semantic rejection,
  malformed/wrong-root/oversized/deep/DTD candidates, read-only/locked targets,
  first creation and two concurrently staged writers. Only owned candidates are
  removed; a pre-existing malformed temporary file remains untouched.
- The carriage-return case failed before the writer setting changed.
- Final affected persistence selection: **67 passed / 0 failed / 0 skipped**.
- Full Core before the final text case: **7028 passed / 0 failed / 5 pre-existing
  skips**; the final full-run log is t012-core-final.log.
- Modern solution: **81 Archiver + 22 Avalonia = 103 passed**, no failures/skips.
- net452 shared-source/API build passes with existing legacy log4net warnings.
- Signed source runner: **8 profile + 5 editor cases passed**; evidence
  artifacts/migration/profile-probe/run-4c14b4049bbe4f099d26d93dcac36f2f.

Logs: artifacts/migration/t012-atomic-red.log, t012-atomic-green.log,
t012-atomic-faults.log, t012-text-newline-red.log, t012-write-final.log,
t012-persistence-verified.log, t012-core-full.log, t012-core-verified.log,
t012-core-final.log, t012-atomic-legacy.log, t012-solution.log,
t012-source-probe.log. The physical interruption probe is recorded separately.

Implementation references: [File.Replace](https://learn.microsoft.com/en-us/dotnet/api/system.io.file.replace?view=net-10.0),
[Flush(Boolean)](https://learn.microsoft.com/en-us/dotnet/api/system.io.filestream.flush?view=net-10.0),
[NewLineHandling](https://learn.microsoft.com/en-us/dotnet/api/system.xml.newlinehandling?view=net-10.0).
