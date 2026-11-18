# T011 — portable settings model migration (in progress)

## Slice 1: export destination and default encoding

Executed 2026-11-19 on Windows x64. `ExportSettings`, `IExportSettings` and `LogFileSettings` now live in `src/Tailviewer.Core/Settings/Migration/`, preserving namespace, XML attribute names, clone behavior and existing defaults. No second model implementation is introduced. The modern Core target compiles them; the original desktop project links the same files. The temporary net45 Core target excludes this directory so the legacy desktop retains its original type ownership while its consumers migrate.

The modern export fallback computes the same Documents/Tailviewer/Export path without referencing host Constants. The legacy branch still uses host Constants.ExportDirectory. Fresh export settings remain null until restoration, matching the original behavior; no new default is guessed. A null encoding still means automatic detection. An invalid encoding retains the prior selection and original warning handling.

### Checks

- Modern Core leaf-model tests: **10 passed / 0 failed / 0 skipped** (XML escaping/Unicode, original fallback, encoding names, automatic detection, invalid value and independent clones).
- Core settings plus fixture-integrity selection: **17 passed / 0 failed / 0 skipped**.
- Core net45/net10.0 build: both passed; known legacy log4net advisory remains visible. No WPF/Metrolib/package reference was added to Core.
- Linked legacy profile contract probe: still exactly **4 pass / 4 expected fail / 0 skipped**. This slice does not claim to fix the T010 profile/storage failures.
- Desktop Compile paths updated for moved files; full old desktop integration build remains outside this verified slice.

```powershell
dotnet build src/Tailviewer.Core/Tailviewer.Core.csproj -c Release --no-restore
dotnet test src/Tailviewer.Core.Tests/Tailviewer.Core.Tests.csproj -c Release -f net10.0 --filter 'FullyQualifiedName~Settings|FullyQualifiedName~FixtureManifestTest'
& ./specs/001-full-project-migration/probes/profile/run.ps1
```

Raw logs: `artifacts/migration/t011-leaf-tests.log`, `t011-core-targets.log`, `t011-settings-tests.log`. Linked profile probe: `artifacts/migration/profile-probe/run-ec14e98520fe478b97454b50dd80ed3a/`.

### Remaining T011 scope

Move remaining UI-independent models/helpers, preserve source/plugin raw XML, adapt primitive window/color values at the UI boundary, preserve DPAPI ciphertext/salt and explicit re-entry behavior, then enable the T010 tests against actual Core models. Keep Core free of an Archiver reference when relocating custom-format plugin identities. Profile file safety and separate modern storage path remain T012 work. T011 is intentionally unchecked until the full owning slice is complete.
