# Migration Validation Guide

**Status**: Planning artifact, 2026-11-19. Commands in the first section work with the current modern subset. Later recipes are acceptance work for the owning phases; they are not implemented tests or evidence of passing migration.

## Prerequisites and safe setup

- Windows x64, SDK selected by root `global.json` (10.0.302 with patch roll-forward), restored NuGet packages and a clean/separately preserved working tree.
- Use synthetic logs, copied legacy XML profiles, copied repository databases and generated plugin/test credentials. Never run conversion against the only copy of a profile/database.
- Use a temporary directory under ignored `artifacts/migration/` and loopback service endpoints. Record commit, OS/hardware, fixture sizes/hashes, command and result per phase.
- Inno Setup 7.1.1 for installer compilation, following the existing release packaging guide. No public deployment or release publication is needed for validation.

## Current runnable checks (repository root)

```powershell
dotnet --version
dotnet build Tailviewer.Avalonia.slnx --configuration Release
dotnet test Tailviewer.Avalonia.slnx --configuration Release
dotnet test src/Tailviewer.Api.Tests/Tailviewer.Api.Tests.csproj --framework net10.0 --configuration Release
dotnet test src/Tailviewer.Core.Tests/Tailviewer.Core.Tests.csproj --framework net10.0 --configuration Release
dotnet run --project src/Tailviewer.Avalonia -- "C:\logs\synthetic.log"
```

Create/select your own synthetic path before running the last command. Expected today: prototype one-file reading/copy/follow works; other planned legacy workflows remain absent. The fresh [T001 baseline](baseline.md) records 14 Avalonia and 116 API passes, plus 6,641 Core passes and 5 existing skips; T003 adds a separately passing fixture-manifest integrity test. Multi-target restore can emit the known legacy log4net advisory even when testing the modern target; see the [T005 target-specific audit](dependency-baseline.md).

Inspect modern packages for each converted project with `dotnet list <project-path> package --include-transitive` and `dotnet list <project-path> package --vulnerable --include-transitive`. The placeholder is a recipe, not a literal path. Capture advisory feed timestamp and resolved target assets; do not claim compatibility or safety solely from package target frameworks.

## P00/P01: baseline and project conversion

1. Save the current modern test output. Attempt the legacy solution with its required build tools in an isolated checkout/environment; record missing tools/packages explicitly if it cannot run.
2. Walk [feature-inventory.md](feature-inventory.md), map every option/setting/command to baseline test/fixture and identify exact expected semantics. Cross-check all 28 project rows and direct/explicit references from [dependency-inventory.md](dependency-inventory.md).
3. Convert one project, update callers and tests together, then restore/build/run its smallest meaningful operation. After it joins the modern solution, run its project tests and affected integration cases.
4. For Archiver inspect resolved assets for WPF/Metrolib and test encoded icon/metadata round-trip. For storage use a copy: open, export, reopen, compare IDs/counts/package/icon hashes. A failed compatibility probe triggers the research fallback before storage implementation proceeds.

## P02–P07: desktop acceptance

- Import: first-run offer/decline/later import; compare original/backup hashes; verify nested settings and unavailable-plugin data after two restarts; provoke target conflict, corrupt input, read-only destination and interrupted save.
- Sources/reading: file/folder/drop/command-line/single-instance; groups, inaccessible path, follow/pause, character/line selection, original-text copy, go-to-line and encoding selector.
- Recovery: append during reading/search/merge, partial final line, same-size replacement, middle-only rewrite with indistinguishable metadata, rotation/reappearance and encoding changes. Assert eventual final contents and rejection of stale-generation results; record any unavoidable filesystem detection limit instead of weakening SC-003 silently.
- Investigation: fixed expected search matches, invalid patterns, composed filters/highlights, time boundaries, parsed columns and merge ties/missing timestamps/offsets/provenance.
- Workflow: bookmarks through restart/rotation, properties/outline/issues, export text/order/encoding, cancellation, disk-full/overwrite, progress/error/help panels. Record keyboard and light/dark checks; native picker/clipboard/drop results are separate from headless tests.

## P08–P10: plugins and controlled repository

1. Rebuild one fixture per extension category in [contracts/migration.md](contracts/migration.md); include bundled Serilog, creator and example plugin. Pack/inspect/test-load using migrated CLI; verify original CLI exit semantics.
2. Test install/update/disable/remove, missing/incompatible dependencies, preserved raw settings and conversion after port. Try traversal/rooted/duplicate/oversized archive entries; verify no path outside destination is written.
3. Start the modern service in console mode bound to loopback with temporary store and test-only credentials. Use the migrated client/CLI to publish, compatibility-filter, query updates, list, lookup descriptions/icons with missing slots, download exact bytes and exercise admin authorization failures.
4. Stop/restart service and compare persisted records/hashes. Verify Windows service registration/start/stop separately in a dedicated test environment; clean up only the explicitly created test service.
5. With repository unconfigured/offline, local plugin install and normal log reading must work. A mock-only test does not replace the real controlled client/service integration case. Public service deployment is not a gate.

## P11/P12: distribution and updates

Use local metadata/download fixtures; never exercise destructive update steps against the developer's current app/profile. Check stable/preview ordering, old-import preference, no-update/offline/rate-limit states and consent. Automatic checks must download zero package bytes; disable preference must stop scheduled checks.

Test tampered/incomplete/wrong-platform/wrong-repository/redirected packages: no execution or replacement after validation failure. Install each supported mode in a temporary location; update vN to vN+1, cancel before install/restart, lock payload, simulate disk exhaustion and interrupt each journal state. Restart/recovery must restore or preserve a working payload and original profile/plugins/logs. Uninstall removes owned files only.

Existing packaging commands/notice preparation: [alpha.2 release notes](../../docs/releases/v2.0.0-alpha.2.md). Reuse its portable profile and Inno definition; do not republish immutable prior assets as part of a test. Packaging startup smoke alone does not prove update rollback.

## P13: measured completion

Record a ≥4-core/16 GB/SSD reference machine; use deterministic 100 MB and 1 GB logs with fixed encoding/line distribution and separate multiline/long-line cases. Run three warm-cache first-page measurements, record peak process memory during complete indexing and p95 UI latency. Drive 1,000 complete lines/s for five minutes and compare final entry count/content plus display-delay samples. Apply SC-004–006 thresholds exactly.

Run all converted suites, native journeys and copied-data migration/rollback checks. Close every inventory row only with evidence. Existing skips must name a reason and cannot cover missing acceptance. A failed gate blocks a full-migration release claim; fix the behavior or explicitly revise the spec with the user rather than relabeling results.

## Planning artifact self-check

Verify required files exist, local Markdown links resolve, FR-001–018 are mapped in plan/inventory, phase prerequisites refer only to P00–P13, manifests still match inventory counts and no unresolved template placeholders remain. These checks validate the plan, not application behavior. The generated [tasks.md](tasks.md) defines implementation order. Setup T001–T005 is recorded complete; next is T006, metadata boundary tests before Archiver conversion. Future environment changes require a new baseline rather than reusing these results as current.
