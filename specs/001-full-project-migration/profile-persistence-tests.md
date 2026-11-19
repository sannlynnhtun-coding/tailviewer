# T010 — profile persistence contract tests

Executed 2026-11-19 on Windows x64, SDK 10.0.302. Test source: `src/Tailviewer.Core.Tests/Migration/ProfilePersistenceTest.cs`.

## Executed baseline

The original T010 run compiled and executed eight tests against the **actual linked legacy settings sources**, not a mock serializer or a missing-type/compiler-error placeholder:

| Case | Legacy result |
|---|---|
| Missing profile: original defaults 12pt / tab 4 / wheel 2, null encoding, empty sources/filters, update flags false; no file created | PASS |
| Invalid known numeric values restore original defaults | PASS |
| Unknown root/known-section attributes, nested extension and namespaced opaque plugin XML survive two restart/save cycles while known font changes persist | FAIL: extensions discarded |
| Known section names inside an unknown section must not apply as root settings | FAIL: font becomes 99 |
| Malformed restore must preserve prior valid in-memory values and source bytes | FAIL: font/tab partially mutated |
| Read-only destination: save returns false, last valid bytes unchanged | PASS |
| Failed write with abandoned malformed candidate: preserve/reopen last valid profile | PASS |
| Default modern path must differ from original LocalAppData/Tailviewer/settings.xml without writing actual profile | FAIL: selects legacy file |

**Historical baseline: 4 passed / 4 failed / 0 skipped.** These were behavior
failures, not compiler placeholders. The temporary PROFILE_PERSISTENCE_CONTRACTS
guard originally kept these eight cases out of plain Core until the models moved.

## Current modern result (T011 root port)

ApplicationSettings/IApplicationSettings now live in Core's shared migration
sources and the test guard is simply NET10_0_OR_GREATER. All eight cases execute
in the ordinary Core suite and pass. The modern default path is isolated from
the original profile. Additional lifecycle/XML cases cover failed-restore save
blocking, duplicate sections, wrong roots, candidate failure, snapshot cloning,
scalar/level extensions, document nodes and clearing owned records. Eight
original ApplicationSettingsTest cases also run against the modern model.

T012 now verifies bounded parsing and same-directory single-rename publication
separately; see [profile store evidence](profile-store-migration.md). These eight
behavior cases alone do not establish that storage gate.

## Reproduce

```powershell
& ./specs/001-full-project-migration/probes/profile/run.ps1
```

The script generates an isolated ignored runner under
artifacts/migration/profile-probe/run-<guid> and now consumes the real modern Core
profile model. It verifies **eight passing profile cases** and **five passing
source-linked editor cases**; it no longer treats the old four failures as
expected. WindowsDesktop/legacy Metrolib remain confined to the source-linked
editor runner, not modern Core. The historical baseline logs below remain as
red evidence rather than a description of current outcomes.

All writes occur in unique owned test directories. `ApplicationSettings.Create()` is only inspected for its selected private path; no save/restore runs against the real user's default profile. The path assertion can move to the public storage path when T012 exposes that boundary. Invalid XML fixtures contain no credentials.

Final baseline evidence: `artifacts/migration/profile-probe/run-79b44d08be4f48f49bed96fb1148c170/red.log`, SHA-256 `b7410624358eb0918df3b44098f03ea22fe066271274546d0b6d5952896db035`; TRX is adjacent in `TestResults/profile-contract.trx`.

## Remaining owning-task checks

The failed-write case exercises OS-denied writes plus a leftover malformed candidate. It does not simulate power loss between flush and replacement. T012 separately added bounded/DTD-disabled reads, validation failures, safe native sharing/metadata behavior and owned-process interruption before/after publication; see the profile-store evidence. Physical power loss is not simulated. T011 adds typed/proxy/DPAPI/color port coverage; T013/T014 cover original backup hashes and import conflicts. No user-data migration or production settings change happens in T010.
