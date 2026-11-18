# Migration Baseline — T001

**Recorded**: 2026-11-19 (Asia/Rangoon)
**Source commit**: `64deef8ca6fa0ee730f3d322387b22219a73c2f3`
**Platform**: Windows 10 Pro x64, build 19045; 20 logical processors; 34,049,220,608 bytes RAM. Performance gates were not measured.
**Toolchain**: .NET SDK 10.0.302; modern tests target net10.0. Visual Studio Insiders full-framework MSBuild found by explicit path after vswhere returned no registered instance.

## Fresh modern results

`dotnet build Tailviewer.Avalonia.slnx -c Release --nologo -v minimal` passed: 0 errors, 6 known legacy-target restore/build warnings.

| Suite | Total | Passed | Skipped | Failed |
|---|---:|---:|---:|---:|
| avalonia | 14 | 14 | 0 | 0 |
| api | 116 | 116 | 0 | 0 |
| core | 6646 | 6641 | 5 | 0 |

Commands (from repository root):

```powershell
dotnet build Tailviewer.Avalonia.slnx -c Release --nologo -v minimal
dotnet test src/Tailviewer.Avalonia.Tests/Tailviewer.Avalonia.Tests.csproj -c Release --no-build --no-restore --logger 'trx;LogFileName=avalonia.trx' --results-directory artifacts/migration/test-results --nologo
dotnet test src/Tailviewer.Api.Tests/Tailviewer.Api.Tests.csproj -f net10.0 -c Release --no-restore --logger 'trx;LogFileName=api.trx' --results-directory artifacts/migration/test-results --nologo
dotnet test src/Tailviewer.Core.Tests/Tailviewer.Core.Tests.csproj -f net10.0 -c Release --no-restore --logger 'trx;LogFileName=core.trx' --results-directory artifacts/migration/test-results --nologo
```

The modern solution currently includes the Avalonia suite only; API/Core tests above were executed separately. No prior release numbers were substituted for these results. Known NU1902 warnings concern legacy log4net 2.0.12 through multi-target restore; target-specific asset inspection remains T005. Core test compilation also reports existing SYSLIB0001 (UTF-7 test fixture) and CS0168 warnings. None were suppressed.

## Existing Core skips (not migrated-feature acceptance)

- `TestParseStringLiteralWithQuotes`: Not yet implemented
- `TestEmptyConstruction1`: This test doesn't make sense for this implementation
- `TestGetTimestamp4`: Not yet implemented, maybe never will due to https://github.com/Kittyfisto/Tailviewer/issues/140
- `TestTrueAndFoobar`: Not yet implemented
- `TestParseStringLiteralWithBackSlash`: Not yet implemented

These skips remain incomplete behavior/legacy test limitations; they are not evidence that corresponding full migration cases pass.

## Isolated legacy build attempts

Created a detached worktree of `64deef8ca6fa0ee730f3d322387b22219a73c2f3` at ignored `artifacts/migration/legacy-baseline/`; no unrelated working files were changed. Invoked the actual Visual Studio MSBuild executable at `C:/Program Files/Microsoft Visual Studio/18/Insiders/MSBuild/Current/Bin/MSBuild.exe` on both legacy solutions with `/t:Build /p:Configuration=Release /m:1 /nologo /v:minimal`.

- `Tailviewer.sln`: **failed**, exit 1. MSB3644 reports unavailable usable .NET Framework reference assemblies (including 4.5/4.5.2/4.7.1); NETSDK1004 reports missing restored project assets in the clean isolated checkout. This was an environment/prerequisite probe without restore, not proof of a legacy source compile regression. Targeting-pack directory names alone do not establish usable reference assemblies.
- `Tailviewer-Complete.sln`: **failed**, exit 1. MSB3202 references missing tracked-layout projects including `src/Tailviewer.Test/Tailviewer.Test.csproj`, `src/Tailviewer.Scripthost/Tailviewer.Scripthost.csproj`, `src/Tailviewer.Archiver.Test/Tailviewer.Archiver.Test.csproj` and `src/Plugins/Tailviewer.Analysis.QuickInfo/Tailviewer.Analysis.QuickInfo.csproj`. These are stale solution/source-layout references, separate from missing tooling.

No legacy executable or legacy tests ran. Do not infer behavioral parity from source-only inspection; T002 records this baseline limit for affected features. The isolated checkout remains available under ignored artifacts for reproducible follow-up.

## Raw evidence

Files below remain local/ignored to avoid committing machine-specific TRX payloads and large logs. SHA-256 fingerprints identify the exact outputs used for this summary; reruns must update provenance rather than presenting changed logs as the same run.

| Local artifact under `artifacts/migration/` | SHA-256 |
|---|---|
| `modern-build.log` | `c92941e2074055baf77ae9063e3a945d575b47e743c46951f093262449722e97` |
| `avalonia-tests.log` | `d591bade8fa3e3e587309c9ff95808fc38464cbbced8d5960007b946344cf190` |
| `api-tests.log` | `0f7e4ae4eb7d298dd468ba3184425c7c4e90b0351c8dadeda487dbf6bbab7028` |
| `core-tests.log` | `97f62563759b4106dad885d69f5d60280bcb77f83ee6787dc306c97b9d3d476e` |
| `legacy-build.log` | `88df90b4d4100ff0d365ef46eda256dd797d7c6cfb9869cdfd185aeb59c01a58` |
| `legacy-complete-build.log` | `11daedf99e91ef7ed356eca999e6447ba1ae61bd2f603851aa8b4a7159edc8d4` |
| `test-results/avalonia.trx` | `63424ba377144a3c3dd71645795485fc64565467415566296639ff323141a5fa` |
| `test-results/api.trx` | `b7dd60f05453122956e9f7d4d63c7598d4a22826743f8566c92d0eaca3b2090d` |
| `test-results/core.trx` | `bc137b3f8f1d55a4d05fffd76b33a7ddcae2dcfe3b7871dd5ee24ec54555a641` |

## Task outcome and limits

T001 complete: modern build/tests captured and both isolated legacy build attempts recorded honestly. Legacy runtime, native picker/clipboard, plugin compatibility, repository service, installer/update rollback and performance gates remain unverified. GitHub push currently fails DNS resolution; this does not invalidate local test results.
