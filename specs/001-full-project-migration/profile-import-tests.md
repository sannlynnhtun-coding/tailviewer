# T013 — Profile import contracts

Executed on Windows x64 with SDK 10.0.302 against the real Core parser/store.
The minimal importer copies a selected profile into a private backup directory,
reopens it and checks SHA-256 before validating and publishing the destination.
Existing backups are verified and never overwritten. Explicit keep/replace
choices and cancellation preserve the current target. Original files remain
read-only inputs; unknown XML survives two destination save/restart cycles.

## Executed evidence

- 16 import cases: 14 passed, two meaningful behavioral failures remain for T014:
  dependent bookmarks are explicitly rejected, and legacy sources without IDs
  receive different identities on repeated import. Neither is a compiler failure.
- Read-only source/backup cleanup was corrected in the owned temporary fixture;
  it does not change production file attributes.
- Existing default Migration tests: 138 passed, zero failures/skips.
- The ordinary test build excludes these pending contracts temporarily. Execute
  them with `-p:ProfileImportContracts=true`; remove the guard during T014 when
  conversion is complete. A green default run does not prove import completion.

```powershell
dotnet test src/Tailviewer.Core.Tests/Tailviewer.Core.Tests.csproj -c Release -f net10.0 -m:1 -p:ProfileImportContracts=true --filter FullyQualifiedName~ProfileImportTest
dotnet test src/Tailviewer.Core.Tests/Tailviewer.Core.Tests.csproj -c Release -f net10.0 -m:1 --filter FullyQualifiedName~Migration
```

Ignored evidence: artifacts/migration/t013-import-red.log,
artifacts/migration/t013-import-contract/t013-import-contract.trx,
artifacts/migration/t013-existing-migration-tests.log.

T014 remains incomplete: bundle publication/recovery, stable missing identities
and further cancellation/failure edge cases need implementation and verification.
There is no Avalonia import UI integration yet. These tests use disposable files,
not real user profiles. Non-Windows paths, native alias races and physical power
loss are not verified by this slice.
## T014 slice 1 — reproducible missing source identities

The importer assigns missing top-level source IDs in the validated in-memory
XML before invoking the existing model parser. SHA-256 of the immutable source
backup plus the source position determines each ID. Existing IDs and original
backup bytes remain unchanged; ordinary settings restore retains its original
behavior. A changed original snapshot is a different import identity domain.

Executed contracts now total 17: 16 pass, one bookmark-bundle failure remains.
The expanded missing-ID cases cover one/two missing sources, distinct IDs,
unchanged existing IDs and repeated import equality. Default Migration regression
suite passes 138 tests. Logs: artifacts/migration/t014-stable-identities.log and
artifacts/migration/t014-stable-migration-regression.log. T014 stays unchecked;
the temporary contract opt-in guard remains until bundle conversion is complete.
## T014 slice 2 — verified dependent bookmark backups

The importer now locks both original inputs against writes/deletion, bounds each
input, and creates a bundle-keyed private backup directory when bookmarks exist.
The key includes both source hashes, so a changed bookmark file cannot reuse an
unrelated prior bundle. Both reopened copies must match before verified backup
is reported. Corrupt existing copies remain untouched. The existing bookmark
parser validates the copied XML; malformed bookmarks retain their verified raw
backup and leave the current target unchanged.

Executed import contracts: 18 pass, one dependent bundle publication failure
remains (19 total). Added malformed-bookmark and corrupt-bookmark-backup cases.
Default Migration regressions: 138 passed. Logs are ignored artifacts/migration/
t014-bundle-backup.log and t014-bundle-backup-regression.log.

Publication deliberately still returns DependentSettingsNotSupported for valid
bundles until recoverable multi-file publication is implemented. T014 is not
complete, and the contract guard remains. Existing result hashes describe the
selected settings file; bundle verification additionally checks bookmarks before
setting BackupFile. No bundle metadata result/API or UI is claimed complete.
## T014 slice 3 — destination bundle conflicts

An existing destination bookmarks.xml requires an explicit conflict decision even
when settings.xml does not exist. Both destination paths are checked for reparse
points. Settings and bookmark destinations cannot be the same file. These checks
run before keep/backup/publication; the profile-only importer no longer silently
ignores an existing bookmark destination when deciding whether to ask.

Three new regression cases failed behaviorally before the fix and now pass.
Full import contracts: 21 pass, one bundle-publication failure (22 total).
Default Migration regressions: 138 passed. Evidence: ignored artifacts/migration/
t014-bundle-conflict-red.log, t014-bundle-conflict.log and
t014-bundle-conflict-regression.log. Recoverable publication is still pending;
these early and prepublication checks alone do not eliminate concurrent writer
races. T014 remains incomplete and no import UI is enabled.
## T014 slice 4 — recoverable bundle publication (completed service)

The final implementation imports settings and dependent bookmarks together.
Both original inputs remain locked read-only through backup/conversion/publication;
both backups are reopened and hash-verified first. Missing source and quick-filter
IDs are deterministic within the immutable source snapshot and identity domain;
existing IDs remain unchanged. Duplicate identities fail before publication.
Unknown root/plugin/bookmark XML, labels and processing instructions survive
import and two save/restart cycles. Replacing from a profile without bookmarks
publishes an empty bookmark collection rather than retaining unrelated old marks.

A named folder mutex serializes cooperating modern readers/writers in the Windows
session. The importer prepares both validated candidates and bounded, flushed undo
copies in an owner-only directory, then publishes a bounded undo journal before
changing either destination. Deleting that journal is the commit point. Failure
or cancellation before commit restores both original byte snapshots; first-save
rollback removes only the transaction-created member. A pending journal is
recovered before subsequent store reads/writes, including restore existence checks.
Recovery verifies all original copies/current hashes and confines journal paths
before mutating anything. Unexpected external changes or corrupt recovery data
block recovery rather than overwriting them. Original DACL/creation metadata and
normal copy semantics are retained for undo copies. Separate model reads can still
straddle a later successful import; a future workspace loader must load its pair
under the shared lock when it needs one simultaneous snapshot.

### Executed checks

- All 34 import/recovery cases are now ordinary net10.0 tests; the temporary
  ProfileImportContracts compiler property/guard was removed.
- Final affected Migration suite: 172 passed, zero failures/skips. Includes
  cancellation after either member, locked-second-member rollback with/without
  prior settings, exact-byte undo, corrupt-undo refusal and malformed journal
  stage/target/digest/entry validation.
- Full Core checkpoint: 7064 passed, zero failed, five pre-existing parser/time
  TODO skips (7069 total). The final cancellation-before-commit guard and second
  cancellation case were subsequently checked in the 172-case affected suite.
- Modern solution checkpoint: 81 Archiver + 22 Avalonia passed.
- Legacy net45 Core build: passed; existing NU1902 warnings remain visible.
- Signed source probe: eight profile + five format-editor cases passed; evidence
  directory profile-probe/run-2ce01b1519954b38a0929a0d26a5a6b7.
- Native owned-process interruption probe: five PASS cases, including partial
  pair publication, both members written before commit, and post-commit. Pending
  transactions restore the old pair; committed pairs retain the new values.

Ignored evidence: artifacts/migration/t014-migration-final.log,
t014-migration/t014-migration.trx, t014-core.log, t014-core/t014-core.trx,
t014-solution.log, t014-legacy-build.log, t014-source-probe.log,
t014-bundle-interruption.log and t014-filter-identity-red.log.

T014's conversion/conflict service is complete. First-launch/later-import UI is
T053 and remains pending. Recovery/private preparation directories are deliberately
retained; no crash-orphan sweep or retention UI is implemented. A broken/partial
immutable backup is never overwritten automatically. Non-cooperating writers or
held files can block recovery until released. These checks do not prove physical
power-loss durability, EFS key lifecycle, cross-session/path-alias locking, SMB,
Linux/macOS, or full legacy desktop/service runtime behavior.