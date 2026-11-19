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


## Slice 3: single-rename publication and Windows metadata

The slice-2 File.Replace checkpoint is superseded. Microsoft's ReplaceFileW
contract documents native errors 1176/1177 where the original filename can be
removed or moved; a null backup is therefore too weak for the required failure
invariant. The final helper uses one same-directory File.Move rename, with the
replace-existing intent captured before staging. A first-save race now fails
without overwriting a file created by another writer during validation.

Before serialization, an exclusively created owned candidate receives a copy
of existing metadata/data, an explicit Windows DACL copy and creation time.
Only that candidate is truncated/serialized, flushed and validated. This retains
NTFS alternate streams and restrictive ACLs without relying on ReplaceFile's
multi-step metadata merging. No new package or native P/Invoke is introduced.
A new metadata test caught permission broadening in a naive rename and verified
that a reconstructed FileSecurity descriptor is actually persisted (passing an
unmodified descriptor object to SetAccessControl did not copy the DACL).
Read-only copied candidates are cleaned by clearing only their own read-only bit.
The original file's permissions/attributes are never relaxed for publication.

Windows MoveFileEx can deny replacement of an open target even when its reader
allows deletion. The final held-reader cases therefore assert explicit save
failure, unchanged bytes/read handle, and successful retry after closing it.
There is no fallback to truncation or ReplaceFile. This is the intended Windows
sharing/error contract, not a claim of nonblocking publication to held readers.
Parallel staged writers still produce a whole validated winner or a safe native
sharing/access failure; existing-file updates use last-completed-writer semantics.

Additional red evidence: a naive rename broadened the protected DACL, and the
first-save race overwrote the newly arrived destination. Both are now covered,
as are actual NTFS alternate-stream and creation-time preservation. Final
persistence selection: **69 passed / 0 failed / 0 skipped**. No new test is skipped.
The native acceptance platform is Windows x64; Linux/macOS remain later scope.

Logs: artifacts/migration/t012-rename-metadata-red.log,
t012-first-save-race-red.log, t012-rename-native.log,
t012-single-rename-verified.log, t012-rename-core-final.log,
t012-rename-solution-final.log, t012-rename-interruption-final.log.
The initial metadata-copy transition also exposed Windows held-reader denial;
its failure and subsequent explicit retry behavior are retained in these logs.

References: [ReplaceFileW failure states](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-replacefilew),
[MoveFileExW](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-movefileexw),
[CopyFileW metadata behavior](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-copyfilew).


## Slice 4 / final T012 acceptance

The checked-in profile-store console probe calls the production helper under
Core's existing signed Tailviewer friend identity. It launches only owned,
hidden child processes and terminates them after flushed/validated staging or
after publication returns. Both cases preserve a readable committed destination
and permit a later save. Recovery does not load or remove the abandoned candidate.
Only the GUID temporary directory created by the parent is cleaned, after a
resolved-path parent check. No real profile, credential, log or EFS key is touched.

Final executed results (Windows x64, SDK 10.0.302, NTFS for both the probe volume
and the default LocalApplicationData volume):

| Gate | Result |
|---|---|
| Profile/bookmark/persistence selection | 69 passed, no failures/skips |
| Full Core suite, after single-rename/metadata/race changes | 7031 passed / 0 failed / 5 pre-existing skips (7036 total) |
| Modern solution | 81 Archiver + 22 Avalonia = 103 passed, no failures/skips |
| Abrupt owned-process termination | Before and after publication both PASS; later saves succeed |
| Expanded legacy source/API compile | Pass; legacy log4net warnings remain visible |
| Signed profile/editor source probe | Eight profile and five editor cases pass |

T012 is complete: isolated modern paths, bounded/DTD-disabled/depth-checked
reads, owned private same-volume staging, output bounds/flush/model validation,
metadata-preserving single-rename publication and failure/retry behavior are
executed. The legacy target deliberately retains its original writer until its
host is retired; none of these results claim full old desktop/service integration.

No journal, new package, broad visibility exception, destination truncation,
backup guessing or foreign-file cleanup was added. Import original/backup hashes,
explicit conflict choices and transactional conversion remain T013/T014. Modern
UI persistence/rebinding belongs to the owning workspace/settings story.
Physical power loss, EFS keys/encryption lifecycle, filesystem device failure,
network filesystems and non-Windows platforms are not verified by this task.

Reproduce the abrupt-termination check using the command in
probes/profile-store/README.md. Final logs: artifacts/migration/
t012-rename-core-final.log, t012-rename-solution-final.log,
t012-rename-interruption-final.log, t012-rename-source-probe-final.log.


## Final staging privacy hardening

CopyFile can reset a destination file's inherited ACL during metadata copying.
The final store therefore creates an owner-only private staging subdirectory,
with its DACL supplied at directory creation on Windows, before copying any
profile bytes. Candidate data stays inside that restricted directory while the
original file's DACL is applied and before new serialization starts. The final
metadata case asserts the candidate DACL and protected owner-only staging rules
inside the serializer callback as well as the published file's DACL, alternate
stream and creation time. This closes the transient broad-ACL window in a naive
metadata-copy approach. No encryption keys/certificates are created by the tests.

Staging remains on the destination volume, in a direct child of its directory;
this supersedes earlier checkpoint wording that the candidate file itself was
in the destination directory. Cleanup removes only the owned candidate and its
empty staging directory, never recursively sweeps profile data. Expected cleanup
IO/permission failures may leave a private orphan; they do not turn an already
committed valid rename into a reported save failure. Crash leftovers are ignored.

The staging-permission transition's failed metadata assertion is retained in
artifacts/migration/t012-staging-acl-verified.log; the final owner-only directory
case passes in t012-private-stage-verified.log. Final closeout logs follow below.

Final private-staging closeout: full Core **7031 passed / 0 failed / 5 existing
skips**, modern solution **103 passed**, abrupt-termination probe **2 PASS**,
legacy source/API compile **PASS**, source runner **8 profile + 5 editor PASS**.
Logs: artifacts/migration/t012-private-stage-core-final.log,
t012-private-stage-solution-final.log, t012-private-stage-interruption-final.log,
t012-private-stage-legacy-final.log, t012-private-stage-source-probe-final.log.
Latest signed source-runner directory:
artifacts/migration/profile-probe/run-3ccd59ce644f4fd1b33996d67d1334fd.
