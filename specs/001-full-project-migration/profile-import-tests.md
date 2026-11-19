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