# Resolved dependency baseline — T005

**Recorded**: 2026-11-19. Modern restored assets inspected after fresh T001 build/tests; legacy source/build limitations remain in [baseline.md](baseline.md). Direct declarations/project edges remain in [dependency-inventory.md](dependency-inventory.md). No dependency was upgraded or removed by this audit.

## Modern resolved targets

| Project | Resolved package count (net10.0) | Native/RID asset declarations | Assets SHA-256 |
|---|---:|---:|---|
| Tailviewer.Api | 5 | 1 | `de1e143dd3fb93df4438cc6f2b2206e386148353ba0067bfa0ebbede81b2ecf2` |
| Tailviewer.Core | 6 | 3 | `c4a83fc135efab1c9b4c647f2744a2162dfe00bd66b06a122fa8f03f5ec6c88f` |
| Tailviewer.Avalonia | 31 | 45 | `581649717f83ac1aba0006c30efd1ecfbf662d1930be9b1ac3fe0613e0231089` |
| Tailviewer.Api.Tests | 21 | 1 | `f1333575ba5af2a8070974ced1ed47364ef9b03ecba2f13d54fe31a3d7652557` |
| Tailviewer.Core.Tests | 24 | 3 | `ff14f50dc28a853feed00866a67aae547d0ef651f0af6ba63674cb785f4bc1a0` |
| Tailviewer.Avalonia.Tests | 51 | 49 | `9274bf0e10cd3928b1e04795eda8bce88cfdc34881322386bf1572c96e2e34b1` |

Each inspected net10.0 target resolves log4net **3.6.0**, with no 2.0.12, Metrolib, Extended.Wpf.Toolkit or Ookii.Dialogs.Wpf package in that target graph. Full package/native paths are retained in ignored `artifacts/migration/resolved-dependencies.json`; asset hashes identify this exact snapshot. Native/RID declarations include other platforms and support assets; their count is not the number shipped in win-x64 nor proof they execute on those platforms. Standard runtime WindowsBase shims are not a WPF UI framework dependency.

Commands:

```powershell
dotnet list src/Tailviewer.Core/Tailviewer.Core.csproj package --include-transitive --framework net10.0 --no-restore
dotnet list src/Tailviewer.Avalonia/Tailviewer.Avalonia.csproj package --vulnerable --include-transitive --framework net10.0 --no-restore --format json
dotnet list src/Tailviewer.Api/Tailviewer.Api.csproj package --vulnerable --include-transitive --framework net45 --no-restore --format json
```

## Advisory feed provenance

The modern Avalonia graph command reported no known vulnerable packages; this is a feed-specific result, not a guarantee of safety or future compatibility. The legacy API net45 command reports log4net 2.0.12, Moderate, advisory `GHSA-4f7c-pmjv-c25w`. Multi-target restore explains NU1902 warnings even while building/testing modern targets; warnings were not hidden. Feed index read from the local NuGet cache:

- base: `2026-11-14T05:29:55.8176483Z`; https://api.nuget.org/v3-vulnerabilities/2026.11.14.05.29.55/vulnerability.base.json
- update: `2026-11-18T17:30:28.9710478Z`; https://api.nuget.org/v3-vulnerabilities/2026.11.14.05.29.55/2026.11.18.17.30.28/vulnerability.update.json

Cache index file modification UTC: 1795031832.0757415 (filesystem epoch seconds). Raw CLI outputs: `artifacts/migration/vulnerabilities.json` and `legacy-vulnerabilities.json`. Network/feed freshness is bounded by the timestamps above; no live repository service/plugin compatibility claim follows from this result.

## Explicit legacy framework/UI references

These are project declarations, not a claim of successful legacy restore/runtime resolution. API WPF references are conditional on net45; they were not found in the inspected modern package graph.

| Source project | Framework/UI assembly declarations |
|---|---|
| `src/Installer/Installer.csproj` | Metrolib, PresentationCore, PresentationFramework, System.Windows.Forms, WindowsBase |
| `src/Plugins/Tailviewer.Formats.Serilog/Tailviewer.Formats.Serilog.csproj` | System.Web |
| `src/Tailviewer.Acceptance.Tests/Tailviewer.Acceptance.Tests.csproj` | Metrolib, PresentationCore, PresentationFramework, WindowsBase |
| `src/Tailviewer.Api/Tailviewer.Api.csproj` | PresentationFramework, WindowsBase |
| `src/Tailviewer.Archiver.Tests/Tailviewer.Archiver.Tests.csproj` | PresentationCore, System.Drawing, WindowsBase |
| `src/Tailviewer.Archiver/Tailviewer.Archiver.csproj` | Metrolib, PresentationCore, System.Drawing, System.Web, System.Web.Services, WindowsBase |
| `src/Tailviewer.PluginRepository.Service/Tailviewer.PluginRepository.Service.csproj` | System.ServiceProcess, System.Web |
| `src/Tailviewer.PluginRepository.Tests/Tailviewer.PluginRepository.Tests.csproj` | System.Web |
| `src/Tailviewer.PluginRepository/Tailviewer.PluginRepository.csproj` | System.Web |
| `src/Tailviewer.Tests/Tailviewer.Tests.csproj` | Metrolib, PresentationCore, PresentationFramework, WindowsBase |
| `src/Tailviewer/Tailviewer.csproj` | Metrolib, PresentationCore, PresentationFramework, System.Drawing, System.Web, System.Windows.Forms, WindowsBase |

## Source usage and action decisions

| Dependency | Actual source evidence | Decision |
|---|---|---|
| IsabelDb | `PluginRepository/PluginRepository.cs`, `PluginRepository.Tests/PluginRepositoryTest.cs` use databases/dictionaries/custom records | Preserve schema and run copied-data probe T009 before selecting retain/conversion; no database was opened by this audit. |
| SharpRemote | Repository Server, plugin updater/publish/client paths, binary serializer tests | Keep behavior contract; migrate transport/client/service together, not a blind target bump. |
| MMQ | `Tailviewer/SingleApplicationHelper.cs` and About attribution | Replace actual handoff with OS mutex/named pipes; attribution only removed if no shipped component still requires it. |
| PE 0.1.13 | `Archiver/Plugins/PluginPacker.cs` calls `PortableExecutable.TryReadHeader` | Used, not removable as dead package; preserve architecture/header checks or prove platform API replacement. |
| EF6 / System.Data.SQLite / protobuf-net | Present in legacy package declarations; no direct source `System.Data.Entity`, `System.Data.SQLite` or `ProtoBuf` usages found in tracked production/test C# source | Candidate inherited/transitive/unused declarations only. Do not remove without restored graph and owning build/tests; do not infer an EF storage schema. |
| WPF/Metrolib/Xceed/Ookii | Desktop controls/settings helpers plus Archiver description/icon/loader code | Replace at adapters; don't move Archiver-dependent plugin factories into Core or keep WPF in modern host. |
| WpfUnit | Legacy control/dispatcher/acceptance fixtures | Map to ordinary/Skia headless/native tests with original assertions and tracked skip reasons. |
| System.Drawing / ServiceBase / Registry / PInvoke | Archiver image packer, service/installer/native desktop integrations | Windows x64 adapters/tool behavior require execution tests; platform portability remains later milestone. |

## Gate outcome

T005 complete as dependency capture/usage classification. No legacy package compatibility is marked proven: old projects have no successful restored/runtime baseline yet. T006 starts meaningful metadata tests before conversion; T009 provides storage executable evidence. The first Setup phase T001-T005 now has source/test/fixture/dependency evidence and can advance to foundation.
