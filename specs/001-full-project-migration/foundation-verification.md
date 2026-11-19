# T017 — Foundation verification

Recorded on 2026-11-19, Windows x64, SDK 10.0.302. Tested source commit:
914036eb5d0d159fd9525917c943456c362bf834. This audit changes no production code
or package pins. The requested local report also exists at ignored
artifacts/migration/foundation.md; detailed artifacts remain local.

## Executed results

| Check | Passed | Failed / skipped |
|---|---:|---|
| Core Migration profile/import/identity suites | 183 | 0 / 0 |
| Full Archiver suite including metadata/runtime boundary | 81 | 0 / 0 |
| Avalonia prototype suite including configured identity | 23 | 0 / 0 |
| Signed profile / format editor source probe | 8 + 5 | 0 / 0 |
| Owned-process store/bundle interruption boundaries | 5 | 0 / 0 |

Three fresh TRX files independently confirm 287 passing ordinary cases. No test
was ignored or timeout changed. All T017 runs passed on their first run; the
prior T016 scrolling timeout is still recorded in source-identity-tests.md and
has not been erased or reclassified as a passing attempt. Full Core's unrelated
suites were not rerun for this documentation-only audit.

## Dependency and output boundary

All eight restored projects' net10.0 graphs resolve log4net 3.6.0. None contains
Metrolib, Extended.Wpf.Toolkit, Ookii.Dialogs.Wpf, PE, SharpRemote or log4net
2.0.12. Modern shipping graphs contain no System.Drawing.Common. Both generated
Archiver/Avalonia deps.json files also exclude PresentationCore and
PresentationFramework. Recursive inspection of the four shipping net10.0 output
folders found no Metrolib/PE/SharpRemote/PresentationCore/PresentationFramework/
System.Drawing.Common DLLs. Runtime reference assertions are part of the executed
Archiver suite, rather than inferred solely from package names.

The Avalonia test graph retains System.Drawing.Common 6.0.0, through
FluentAssertions 7.2.2 -> System.Configuration.ConfigurationManager 6.0.0 ->
System.Security.Permissions 6.0.0 -> System.Windows.Extensions 6.0.0. It is a
transitive test dependency, not a shipping app/Archiver dependency. No pins were
changed to make this audit pass. This is a compatibility boundary audit, not a
new vulnerability scan or a claim that every test dependency is current.

Legacy net45/net452 targets still resolve log4net 2.0.12 and emit their known
NU1902 advisory during multi-target restore/build, including modern build log
output. The modern assets/runtime outputs resolve 3.6.0. No advisory was hidden.

| Project | Modern target | Package count | Assets SHA-256 |
|---|---|---:|---|
| Tailviewer.Api | net10.0 | 5 | `de1e143dd3fb93df4438cc6f2b2206e386148353ba0067bfa0ebbede81b2ecf2` |
| Tailviewer.Core | net10.0 | 6 | `9877234ba0ebf1d7c1d3da194c0a0ace3afdcd727f1664d7213ab9f56e822e39` |
| Tailviewer.Archiver | net10.0 | 10 | `599d26399df206ac357c1e4a7fdf0220e3a0d5d96710c2c7f4b1f93e9758523c` |
| Tailviewer.Archiver | net10.0/win-x86 | 10 | `599d26399df206ac357c1e4a7fdf0220e3a0d5d96710c2c7f4b1f93e9758523c` |
| Tailviewer.Avalonia | net10.0 | 31 | `8c4a5a039f3db19db52f4691395a023a280105eab4d7e2170a0ce715698b3c85` |
| Tailviewer.Api.Tests | net10.0 | 21 | `f1333575ba5af2a8070974ced1ed47364ef9b03ecba2f13d54fe31a3d7652557` |
| Tailviewer.Core.Tests | net10.0 | 24 | `88b53533d23d5e77788a748b923a5a13405a15f425685ce1dedee5b092df7c8f` |
| Tailviewer.Archiver.Tests | net10.0 | 28 | `d776c6e7e683774a60862aaaf8bbc5d5b5042997d524c646697637a58c94a64d` |
| Tailviewer.Avalonia.Tests | net10.0 | 51 | `310a0abcd13e998c15e16f19477fd40b84f4e6bb17f8cb7a737074daa86ff67a` |

## Reproduce and local evidence

```powershell
dotnet test src/Tailviewer.Core.Tests/Tailviewer.Core.Tests.csproj -c Release -f net10.0 --no-restore -m:1 --filter FullyQualifiedName~Migration
dotnet test Tailviewer.Avalonia.slnx -c Release --no-restore -m:1
& specs/001-full-project-migration/probes/profile/run.ps1
dotnet run --project specs/001-full-project-migration/probes/profile-store/ProfileStoreProbe.csproj -c Release --no-restore
dotnet list src/Tailviewer.Avalonia/Tailviewer.Avalonia.csproj package --framework net10.0 --include-transitive --no-restore
```

Dependency evidence: artifacts/migration/t017-assets.json contains target names,
package counts, hashes and the explicit test-only drawing exception. The audit
read net10.0 and net10.0/RID target entries, not the union of legacy/modern
libraries in project.assets.json. The existing Archiver net10.0/win-x86 assets
entry is inventory evidence only, not a new x86 release/platform promise.

Logs: artifacts/migration/t017-core.log, t017-solution.log,
t017-source-probe.log and t017-interruption.log. TRX files are under
artifacts/migration/t017/. Source-probe run:
artifacts/migration/profile-probe/run-ca07ee42f0c546e6946b1a9b74ceb854.

## Scope remaining

This closes the foundation task T017, not full feature parity or release
acceptance. US1 file/folder/group/navigation stories start at T018. Native
clipboard/desktop/installer checks, physical power loss, EFS key lifecycle,
SMB and Linux/macOS were not exercised. Import UI remains T053. Private recovery
copies remain retained; non-cooperating writers can block recovery. The known
legacy advisory and transitive test-only drawing dependency remain visible.