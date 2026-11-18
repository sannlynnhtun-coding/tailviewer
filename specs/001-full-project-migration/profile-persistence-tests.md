# T010 — profile persistence contract tests

Executed 2026-11-19 on Windows x64, SDK 10.0.302. Test source: `src/Tailviewer.Core.Tests/Migration/ProfilePersistenceTest.cs`.

## Executed baseline

Eight tests compile and execute against the **actual linked legacy settings sources**, not a mock serializer or a missing-type/compiler-error placeholder:

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

**4 passed / 4 failed / 0 skipped.** These are behavior failures, not a green feature gate. T011/T012 must enable and pass the same tests in Core when the actual models/storage move there. The test file has an explicit temporary `PROFILE_PERSISTENCE_CONTRACTS` guard so the existing plain net10 Core project can still build before those types exist. Only the probe enables it now. Do not cite ordinary Core-suite totals as coverage of these eight cases until that guard is removed/enabled by the port.

## Reproduce

```powershell
& ./specs/001-full-project-migration/probes/profile/run.ps1
```

The script generates an isolated ignored runner under `artifacts/migration/profile-probe/run-<guid>/`, links existing sources plus existing HashSet helper/Constants/Resource, and uses WindowsDesktop/legacy Metrolib **only inside the probe**. The empty UI namespace satisfies a documentation-only using; no settings behavior is replaced. No WPF dependency is added to modern Core or the application. It checks the exact four failure names and eight executed cases. Its successful wrapper exit means the expected **red baseline** was reproduced, not that migration tests passed.

All writes occur in unique owned test directories. `ApplicationSettings.Create()` is only inspected for its selected private path; no save/restore runs against the real user's default profile. The path assertion can move to the public storage path when T012 exposes that boundary. Invalid XML fixtures contain no credentials.

Final baseline evidence: `artifacts/migration/profile-probe/run-79b44d08be4f48f49bed96fb1148c170/red.log`, SHA-256 `b7410624358eb0918df3b44098f03ea22fe066271274546d0b6d5952896db035`; TRX is adjacent in `TestResults/profile-contract.trx`.

## Remaining owning-task checks

The failed-write case exercises OS-denied writes plus a leftover malformed candidate. It does not simulate power loss between flush and replacement. T012 must additionally exercise bounded/DTD-disabled reads, validation failure and replacement interruption at its actual atomic-save boundary, with the previous file retained. T011 adds typed/proxy/DPAPI/color port coverage; T013/T014 cover original backup hashes and import conflicts. No user-data migration or production settings change happens in T010.
