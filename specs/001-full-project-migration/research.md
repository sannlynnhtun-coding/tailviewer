# Migration Research and Decisions

**Date**: 2026-11-19. Source audit and primary documentation review; no old package was executed and no repository database was opened. The prior [compatibility audit](../../docs/AvaloniaMigrationPlan.md) remains background; current declarations are in [dependency-inventory.md](dependency-inventory.md).

## 1. Preserve reusable logic and bounded rendering

**Decision**: Keep API/Core and existing streaming/index/cache. Port UI-independent legacy business/settings behavior into the existing Core as each feature lands; put controls/view adapters in Avalonia. Keep virtualized controls first; add custom drawing only if measured selection/column/performance requirements cannot be met.

**Rationale**: The current reader already avoids whole-file text controls. Legacy LogView drawing uses WPF TextLine/TextCanvas and cannot be copied as-is.

**Alternatives considered**: Whole-file TextBox (fails scale), wholesale MVVM/framework rewrite (unnecessary), immediate custom renderer (too much code without measurement).

**Evidence**: `src/Tailviewer.Avalonia/{LogDocument,MainWindow}.cs`, `src/Tailviewer/Ui/LogView`, `src/Tailviewer.Core/Sources`. [Avalonia custom controls](https://docs.avaloniaui.net/docs/custom-controls/) documents custom rendering as an available extension, not a required rewrite.

## 2. Dependency boundaries and build ordering

**Decision**: Keep existing package pins for the working modern graph. Convert Archiver/API-facing metadata first; use encoded icon bytes instead of WPF BitmapSource. Replace WPF controls/Metrolib presentation/Ookii/Xceed at UI boundaries. Retain CommandLineParser and assertion libraries where compatible; do not upgrade all packages together. Use standard mutex/named pipes for modern single-instance handoff instead of carrying MMQ.

**Rationale**: Desktop, repository and legacy installer depend on Archiver. A simple project retarget would retain PresentationCore/Metrolib and old assembly loading assumptions. Existing legacy log4net advisory must not leak into modern outputs.

**T004 boundary correction**: Legacy source/parser factories use Archiver `IPluginWithDescription` metadata. Their composition adapters belong in `Tailviewer.Avalonia/Plugins`, not Core: Archiver already references Core. Core receives API-only plugin behavior where needed; do not introduce a Core-to-Archiver cycle when porting apparently UI-independent factory code.

**Alternatives considered**: Keep WPF inside the modern app (breaks chosen migration), discard Archiver (drops author/repository functions), modernize every dependency in one commit (hard to review).

**Probe gate**: In P01 restore/build/run a minimal operation per converted project, inspect resolved assets and native dependencies; classify unused EF6/SQLite/protobuf references by actual source usage before removal. Packages.config alone is not proof of a storage API dependency.

## 3. Plugin identity and execution

**Decision**: Version modern host/archive compatibility explicitly. Keep plugin IDs, configuration keys and serialized custom data identity stable or supply keyed converters. Use a per-plugin AssemblyLoadContext with AssemblyDependencyResolver where private dependency resolution is required; share the exact host API contract assembly and Avalonia framework assembly identity. Begin non-collectible with restart-required replacement where necessary; do not promise hot unload.

**Rationale**: User accepted rebuild/port. Legacy WPF custom UI/property presenter types must be adapted, not emulated. Loading contexts isolate dependency identities, not untrusted execution.

**Alternatives considered**: Unchanged .NET Framework/WPF binaries (rejected by clarified scope), generic out-of-process sandbox (not requested), loading every plugin dependency into default context (conflicts).

**Evidence**: `src/Tailviewer.Archiver/Plugins/{PluginAssemblyLoader,PluginPackageIndex}.cs`, API `Plugins` and `Ui` contracts. [Microsoft plugin loading guide](https://learn.microsoft.com/en-us/dotnet/core/tutorials/creating-app-with-plugin-support) explains shared contract identity and OS-level security boundaries. Install only explicitly selected packages; validate archive paths before extraction. Runtime plugins remain trusted local code with user permissions; no sandbox claim.

## 4. Profile persistence and imports

**Decision**: Retain XML semantics/defaults and unknown nodes. Use a distinct modern profile directory, copied backup, temporary validated save then same-volume atomic replacement; prompt before import conflicts. Imported update preference takes precedence over new-profile defaults. Reuse plugin settings after rebuild via identity/version-aware conversion.

**Rationale**: ApplicationSettings covers nested sources/views/filters/custom formats/export/update state; replacing format without need risks data loss.

**Alternatives considered**: Share writable old/new profile (breaks coexistence), silently reset unknown plugin nodes (data loss), introduce a new database for settings (unneeded).

**Evidence**: `src/Tailviewer/Settings/ApplicationSettings.cs` and all settings leaf files in feature inventory. Fixtures must cover defaults, invalid values, unknown extensions and two-restart round-trip.

## 5. Repository transport, persistence and service

**Decision**: Preserve repository operations and ordered/null semantics, but use a versioned modern HTTP client/service contract rather than depend on old SharpRemote wire compatibility. Use HttpClient on the client and the .NET shared web framework for a loopback-capable service. Keep admin CLI commands/options/exit outcomes and Windows service lifecycle; expose console mode for controlled tests. Default binding is loopback. Public hosting is deferred.

**Rationale**: SharpRemote 0.6.184 socket contract couples old framework serialization and client/server. No external unchanged-client support was requested. A migrated client/service pair plus documented conversion retains behavior without an old RPC runtime requirement.

**Alternatives considered**: Blindly retarget SharpRemote, publish the existing server publicly, or omit service/admin commands. All fail either evidence, boundary validation or agreed scope.

**Persistence decision**: Preserve IsabelDb schema/types and existing records if a compatible modern reader passes copied-database open/export/reopen tests. If the old store cannot run, use a read-only migration-time exporter in an isolated legacy environment to a versioned neutral snapshot, then import into a supported SQLite store with transactional constraints and verified IDs/counts/package hashes. No shipped modern runtime depends on the legacy exporter. Only this failing probe justifies the new storage dependency; a fresh empty DB is not an acceptable migration. P01 selects the path by an executable probe before P10 implementation.

**Evidence**: `PluginRepository/PluginRepository.cs` uses IsabelDb dictionaries, `Entities/PublishedPlugin.cs` warns serialization changes break records; packages.config EF/SQLite presence is not evidence that these APIs implement persistence. `Archiver/Repository/IPluginRepository.cs` specifies publish/filter/update/list/description/icon/download behavior. P10 verifies users, tokens, package bytes, icons, version and change metadata against copied fixtures.

## 6. Update provenance, channels and application

**Decision**: Use the fork's GitHub release metadata over HTTPS with a fixed repository/allowed download-host policy and stable/preview version comparison. Match version/platform/distribution and SHA-256 from authenticated release metadata; compare published checksums too. No checksum-only claim protects against a compromised release account. Missing provenance/digest blocks automated application and offers a clearly labeled manual release page. Use HttpClient cancellation/progress and a separate download/install action.

**Rationale**: Legacy AutoUpdater points at Kittyfisto `version.xml`, uses WebClient, installs stable explicitly and lacks the required integrity gate. Current prereleases have GitHub asset digests and checksum files, and current Inno packaging can be reused.

**Alternatives considered**: Keep original URLs (wrong application), silently trust filename/ZIP checksum from arbitrary source (no provenance), force code-signing infrastructure into planning (credentials unavailable and not needed to specify safe refusal).

**Apply decision**: Stage outside the live installation, journal prior/new version and package path, require install/restart consent, use existing Inno upgrade for installed mode; a minimal owned helper may swap the portable EXE after process exit. Preserve backup until health verification. Handle full ZIP migration by swapping its owned payload directory rather than deleting arbitrary files. Failed/cancelled verification never launches a payload; failed apply restores the prior usable payload. Deleting user's logs/profile is never a cleanup strategy.

**Evidence**: `BusinessLogic/AutoUpdates/AutoUpdater.cs`, `installer/Tailviewer.Avalonia.iss`, modern publish profile and release notes. Release signature absence stays disclosed. Validation uses a local fake metadata/download server and synthetic packages; no live publication is required for tests.

## 7. Test layers and acceptance evidence

**Decision**: Preserve recorded baseline results as history; rerun current targeted suites in P00. Use ordinary tests for Core/settings/parser/contracts, Avalonia headless with Skia for controls/themes/input, and native Windows tests/manual recorded steps for picker/drop/clipboard/single-instance/service/install/update. Avoid WpfUnit runtime patching in modern tests. Benchmark on declared hardware with repeatable synthetic fixtures.

**Alternatives considered**: Treat headless render as proof of native behavior (incorrect), rewrite every legacy test framework at once (unnecessary), silently skip missing-service cases (not acceptance).

**Evidence**: [Avalonia headless platform](https://docs.avaloniaui.net/docs/testing/setting-up-the-headless-platform) explains the replacement windowing backend and enabling Skia rendering. Existing prototype tests already use real drawing; that does not establish native picker behavior.

## Research closure

No unresolved product choices. Technical runtime compatibility is deliberately gated by executable P01 probes with a defined retain-or-convert fallback, not claimed as established by this source audit. Remaining work is measurement/implementation, captured in plan phases and validation guide.

## T008 implementation evidence

Archiver now has SDK net452/net10.0 targets and a fully modern test project. Dependency-only metadata is read through built-in PEReader/MetadataReader; no new PE parser package is required on net10.0. Icon validation reuses SkiaSharp 3.119.4 already used by the app. Dynamic fixture assemblies use [PersistedAssemblyBuilder](https://learn.microsoft.com/en-us/dotnet/api/system.reflection.emit.persistedassemblybuilder?view=net-10.0), the platform save-capable emitter, instead of enabling old runtime emit-save or BinaryFormatter compatibility switches. Source ownership and copied test data no longer require legacy desktop/test projects. See [executed results and remaining limits](archiver-sdk-migration.md).

## T009 storage decision — executed probe

Select the neutral-export/supported-SQLite fallback for P10.IsabelDb 0.6.21 can open/export/reopen ordinary copied net452 records on net10.0, but its byte-array serializer fails on supported null-icon records on both runtimes. Read-only raw SQLite export/reopen preserves all tables, serialized metadata, IDs, nulls and BLOBs without changing the source. This probe establishes the preservation path, not a completed semantic production importer. P10 must implement bounded versioned DTO export with null-safe extraction, explicit schema/record validation and transactional import; do not invoke the failing old icon enumerator or execute raw snapshot SQL from untrusted inputs. See [reproduction, exact counts/hashes and limits](storage-probe.md). No production database/package changes occur in T009.
