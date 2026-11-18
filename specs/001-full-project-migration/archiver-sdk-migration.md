# T008 — Archiver SDK migration evidence

**Recorded**: 2026-11-19, Windows x64. Archiver and its full test project are SDK-style. The temporary T006 linked-source test project is retired; its tests now reference the real built Archiver assembly.

## Changes and compatibility boundaries

- Archiver targets `net10.0` plus temporary `net452` for remaining desktop/repository/service consumers. Signing, assembly identity, shared version/attribution and legacy output paths are retained. Warnings-as-errors remains enabled with the existing visible legacy log4net advisory exception.
- Modern target uses log4net 3.6.0, the existing application stack's SkiaSharp 3.119.4 for encoded icon validation, and built-in PEReader/MetadataReader for PE/assembly metadata. Its restored graph and direct assembly references exclude WPF, Metrolib, old PE, SharpRemote and System.Drawing.Common. Legacy references are conditional on net452; no blanket warning suppression or unsafe formatter switch was added.
- Assembly metadata/dependency inspection does not load or execute dependency code. Original x86 metadata/packaging cases work from a 64-bit test host. CLR-managed plugin entry loading is still required for existing interface discovery; full versioned plugin isolation/compatibility work remains P08/P09, not a sandbox claim.
- Raw COFF object entries remain raw data rather than being mistaken for native PE images. A failing null-reference regression was captured and fixed before the final pass.
- Full tests target plain net10.0 with NUnit 3.12.0 / adapter 5.0.0 / Test SDK 17.14.1 / FluentAssertions 5.6.0 / Moq 4.7.127. Original UI/host project references are removed; three existing small test helpers are linked instead of referencing whole legacy test assemblies.
- Dynamic test plugins use .NET PersistedAssemblyBuilder. Test output/current-directory and plugin folders are confined to a unique ignored test-run directory; no real installed/downloaded plugin path is used. TestData is copied by SDK items and CodeBase assumptions are replaced with Location.
- BinaryFormatter test helper is removed. Modern exception constructor/message/inner behavior is tested in place of the unsupported legacy binary wire round-trip; legacy exception serialization members remain available only on the legacy production target.
- Legacy dependency embedding is retained conditionally for the legacy target; modern CLI uses normal SDK apphost/DLL/deps/runtimeconfig deployment files. Do not distribute only modern archive.exe without its normal output dependencies.

## Fresh results

| Check | Result |
|---|---|
| Archiver build, net10.0 and net452 | Both passed; known legacy-target NU1902 warnings visible |
| First full port run | 75 pass / 4 failures, no skips; x86 loading, invalid-path semantics and dependency resolution were corrected |
| Final full Archiver suite | 81 pass / 0 failed / 0 skipped |
| Final modern solution | 81 Archiver + 14 Avalonia = 95 pass / 0 failed / 0 skipped |
| Real modern CLI pack | Exit 0; generated .tvp ZIP integrity and Index.xml validated from an owned generated test plugin |
| Real git-get-changes CLI | Exit 0; generated XML parsed successfully |
| Dependency audit | net10.0 restored graph uses log4net 3.6.0; no modern WPF/Metrolib/PE/SharpRemote/System.Drawing.Common entries |

An intermediate solution run had one existing `TestScrollingVirtualizesAndLoadsDistantLines` eight-second timeout (13 Avalonia pass, 1 fail). That exact case passed in isolation, then the complete latest solution passed. No assertion was weakened, timeout extended or case skipped; retain this as a flakiness observation rather than claiming it never failed.

## Reproduce

```powershell
dotnet build src/Tailviewer.Archiver/Tailviewer.Archiver.csproj -c Release
dotnet test src/Tailviewer.Archiver.Tests/Tailviewer.Archiver.Tests.csproj -c Release
dotnet test Tailviewer.Avalonia.slnx -c Release
dotnet list src/Tailviewer.Archiver/Tailviewer.Archiver.csproj package --include-transitive --framework net10.0 --no-restore
```

For CLI pack smoke tests, choose an owned temporary working directory: the existing command writes its `<plugin-id>.<version>.tvp` to the current directory. Invoke the modern `archive.dll` with `dotnet`, or its normal SDK apphost and sidecars. `--help` retains the legacy parser's -2 exit behavior. Packaging a DLL works; `.sln`/`.csproj` packing and `list` remain original baseline stubs for the later author-tool phase, not completed functionality claimed here.

## Remaining scope

Modern `publish` retains its CLI option shape but returns a clear ConnectionError explaining that legacy SharpRemote transport is unavailable until P10; it never silently reports success or falls back to original public services. The net452 command retains its original transport. HTTP repository/service migration is not part of T008.

The archive schema's existing x86/AnyCPU managed and x86 native packaging rules remain unchanged, including explicit x64-only rejection tests. Broader modern plugin/native architecture policy and archive/API versioning are still P08/P09 work. Full legacy desktop/service integration, Linux/macOS and plugin compatibility/security isolation are not validated by this slice. The retained legacy log4net 2.0.12 advisory remains; the modern resolved version is 3.6.0. Core/API suites were not rerun in this slice because their production sources were unchanged; their recorded baseline is separate.

## Local evidence fingerprints

Raw logs/TRX/dependency JSON stay under ignored artifacts. They identify executed results; no plan checklist substitutes for tests.

| Artifact under artifacts/migration | SHA-256 |
|---|---|
| `archiver-both-targets.log` | `b4bcd05c57ffba38730d3fb73587f1fb4bee83b9636cb3204a5a7167c3e7bf28` |
| `archiver-tests-port.log` | `8f9429eacfd871ff3f3ae122feaf760748ebbcb3a2d89e5dec7ac1eab1f80bfe` |
| `archiver-verified.log` | `f8af416c4d18a495a8049ee650957f313ee554814db3163f2c16976f4adeb092` |
| `coff-red.log` | `18161285b2b6cb5c3f00c4da16e931ff8fd5e3980416385914cfb3dcbc3fd629` |
| `t008-final-solution.log` | `72f48fdb4186f34ba2344ea4d486b45fb461b0fd16b3970323e89073608208de` |
| `t008-scroll-recheck.log` | `bc8409b29f325036b142fc78b168b2e884a844794d63a61c5f6801325898c27a` |
| `t008-verified-solution.log` | `d2800b9542c251362278b927b92605ec59a13ddbcdeaad9cb5d1ef6fbacb4525` |
| `archiver-cli-pack.log` | `85a0e5d204fd6ef5c62fb446f1b5218751d4adcadbb3bc367cd3df0c3c26e297` |
| `archiver-cli-changes.log` | `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855` |
| `t008-packages.json` | `b5d957f0dc694c563dcf471bf2564ffd52f2a2fa370f493426936b4b27331112` |
