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

## Slice 2: source configuration and bookmark values

Moved the original `DataSource`, `DataSourceSettings`, `IDataSourcesSettings`, `DataSourceDisplayMode` and `BookmarkSettings` into the same Core migration directory. Namespace, serialized names, source/group/filter IDs, selected source, folder flags/patterns, display state and bookmark indices are retained. The legacy host links the same source files; modern Core has no Metrolib/WPF/Archiver reference. The former UI namespace was only a documentation reference and is removed. HashSet additions use standard UnionWith rather than importing a host helper.

Metrolib XML behavior was inspected against the installed 0.3.0.162 assembly before replacing it with internal primitive helpers. Dates are raw ticks (not ISO timestamps), doubles are invariant, enums are case-sensitive, and its permissive bool reader accepts True/false but treats 1/invalid as false. New cases pin those distinctions and localized-culture round-trips. The legacy branch still resolves its original Metrolib helpers.

The new clone regression initially failed because plugin ID/configuration and UserDefinedName were omitted. Clone now carries those values and clones mutable plugin configuration through its existing ICloneable contract. Plugin exceptions are not swallowed or converted into empty settings.

Verification: **159 existing linked data-source cases passed**; eight of nine new source/bookmark cases initially passed and the plugin-clone regression failed, then the complete affected selection passed **185 / 0 failed / 0 skipped**. Core net45/net10.0 builds passed. The profile contract probe still reproduces its four known behavior failures, without silently enabling or skipping them in Core. Raw logs: `artifacts/migration/t011-source-tests.log`, `t011-source-extra-red.log`, `t011-source-green.log`, `t011-source-targets.log`; latest profile probe `run-9cb41268487748548de599a542686016`.

This is a model-port slice, not complete modern profile persistence. Full unknown/plugin XML preservation, window/color adapters and proxy/DPAPI settings remain T011 work; duplicate legacy quick-filter XML blocks and other baseline serializer behavior are not broadly rewritten in this port. Group-cycle/revision policy remains T015/T016, and bookmark UI/store behavior remains its owning story. The full old desktop was not built.

## Slice 3: proxy credentials and update preferences

Moved AutoUpdateSettings/IAutoUpdateSettings into the shared migration directory and linked them from the legacy desktop. Modern Core uses the platform System.Security.Cryptography.ProtectedData 10.0.0 package, conditional to net10.0; no modern Metrolib/WPF/SharpRemote/PE/System.Drawing.Common dependency was introduced. Original update/proxy/repository XML names and preference defaults remain. Keeping the legacy automatically-install value as data does not authorize automatic installation in the future modern update flow.

The nine original AutoUpdateSettings cases pass on Windows. New regression cases initially produced **3 failures / 1 pass**: restoration regenerated non-20-byte entropy, and unavailable encrypted credentials became a NetworkCredential with an empty password. Restore now preserves the supplied ciphertext/salt exactly; only an explicit successful password change commits replacement bytes. Failed protection leaves previous encrypted state intact. Temporary plaintext byte buffers are cleared after protection/unprotection, and crypto failure logs use fixed messages without password/ciphertext values.

IAutoUpdateSettings.ProxyPasswordRequiresReentry exposes an unavailable stored-password state for the later settings UI. GetProxyCredentials returns null when an existing encrypted password cannot be decrypted, rather than silently substituting an empty password. Explicit password re-entry repairs the state; an existing nonstandard-length salt remains usable as DPAPI entropy. Unsupported platforms retain stored bytes and report unavailable credentials; Windows x64 is the current acceptance target.

Verification: the final affected selection passed **199 / 0 failed / 0 skipped**, including 9 original proxy cases and 5 new preservation/DPAPI/re-entry cases. Current-user protection/restore/clone and plaintext-free XML were exercised with generated test credentials. Core net45/net10.0 builds passed. The linked profile contract probe still reports its four expected failures. Modern target assets were inspected for the dependency boundary above. Logs: `artifacts/migration/t011-proxy-red.log`, `t011-proxy-final.log`, `t011-proxy-targets.log`; profile probe `run-d1819b75e39b432283b385f5aa0c1025`.

System proxy acquisition retains WebRequest.GetSystemWebProxy with a warning suppression limited to that one call, preserving its per-instance behavior until HTTP-client migration. No network request is performed by these model tests. The modern settings screen/re-entry prompt, real foreign-account DPAPI data, non-Windows execution and full old desktop integration are not validated here. Window/color models and complete unknown XML preservation still keep T011 open.

## Slice 4: UI-neutral log viewer and color values

Moved LogViewerSettings/ILogViewerSettings/LogLevelSettings/ColorXmlExtensions into Core. The modern models use the standard **System.Drawing.Primitives Color value type**, not WPF brushes/colors or System.Drawing.Common/GDI+. The linked legacy desktop still compiles the same source with WPF Color. Defaults and XML names remain unchanged. Existing color suites are shared with Core; their assertions compare all rendered ARGB channels instead of System.Drawing's extra named-color identity state.

Hex RGB/ARGB, named colors and finite scRGB inputs are decoded without loading WPF. Conversion follows the standard sRGB transfer curve, checked against the [WPF Color implementation](https://source.dot.net/presentationcore/System/Windows/Media/Color.cs.html) and, more importantly, an executable WPF converter oracle. Original color text is retained through restore/clone/save, including scRGB precision; an explicit user color edit replaces only that value with canonical #AARRGGBB. Unsupported/malformed values retain the existing safe default and original text. ICC ContextColor profiles are not loaded/converted; their text is preserved with a warning, leaving that rendering limitation explicit.

Avalonia gets a small two-way settings-color adapter; three alpha/RGB round-trip cases pass. Applying the persisted palette in the full settings UI/theme flows remains its owning UI phase. No desktop/mobile/theme screenshot or full palette UI claim is made by these value tests.

### Executed results

- Existing log-viewer/log-level cases: **52 pass**.
- Affected Core settings/source/fixture cases: **258 pass / 0 failed / 0 skipped**, including seven new malformed/unsupported/edit-preservation cases.
- Windows-only converter oracle: **10 pass**, comparing actual WPF ColorConverter ARGB and preserved original strings for hex, named and scRGB samples.
- Modern solution: **81 Archiver + 17 Avalonia = 98 pass / 0 failed / 0 skipped**.
- Core net45/net10.0 builds: both pass. Modern assets exclude WPF/Metrolib/SharpRemote/PE/System.Drawing.Common. A first no-restore consumer run showed stale ProtectedData 6-vs-10 assets from the preceding slice; the fresh solution restore resolved them with no MSB3277 warning.
- Profile probe: still 4 pass / 4 expected fail; it is not treated as a completed persistence gate.

```powershell
dotnet test specs/001-full-project-migration/probes/colors/ColorOracle.Tests.csproj -c Release
dotnet test src/Tailviewer.Core.Tests/Tailviewer.Core.Tests.csproj -c Release -f net10.0 --filter 'FullyQualifiedName~Settings|FullyQualifiedName~FixtureManifestTest|FullyQualifiedName~DataSourceTest'
dotnet test Tailviewer.Avalonia.slnx -c Release
```

The oracle requires WindowsDesktop only in its separate test project, outside the modern production graph. Evidence logs: `artifacts/migration/t011-color-tests.log`, `t011-color-oracle.log`, `t011-color-final.log`, `t011-color-adapter.log`, `t011-color-targets.log`, `t011-color-solution.log`. Profile probe: `run-b457f70dd35841f4b12e471d9a790833`.

T011 stays open for primitive window placement/UI conversion and complete profile/plugin XML preservation. Full Core/API suites, full old desktop integration, ICC profile rendering and the final settings/theme UI have not been verified in this slice.

## Slice 5: window placement and desktop geometry

Moved MainWindowSettings/IMainWindowSettings/Desktop into the shared migration directory. Modern Core replaces Metrolib WindowSettings and WPF Size/WindowState with a small internal placement record and public numeric size/state values. OS screen enumeration, Forms screen constructors and WPF Window methods remain conditional legacy UI code; modern Core receives screen bounds/working areas explicitly. The original host links the same sources, retaining its WPF-facing API. Avalonia receives checked two-way mappings for the three legacy window states.

Defaults were executed against the actual Metrolib 0.3.0.162 library: Left/Top/Width/Height are all 0 and state is Normal; the original WPF RestoreTo also applies those zero dimensions. No window size was invented in Core. Normal/Maximized/Minimized wire values and fractional/negative coordinates were read in both directions between Metrolib and modern Core. Modern reads reject invalid/nonfinite sizes/positions and invalid enum values in favor of existing safe values.

Regression tests initially produced **3 failures / 5 passes**: clone lost the pre-minimize state, a right-edge containment check compared Y rather than X, and oversized windows selected the smallest rather than largest working area. The shared implementation now retains prior state in clones, uses correct axis overlap/containment and selects the largest working area when resizing is necessary. Cross-shaped overlap/cutting is covered as well. Minimized startup state resolves to the last visible state, with Normal as the fallback for unusable previous values.

### Executed results

- Existing desktop/rectangle cases: **11 passed**. Nine original pure main-window cases are linked into Core; the four native WPF method cases remain legacy-only, with modern state/value equivalents tested separately rather than counted as migrated native journeys.
- Final affected Core selection: **290 passed / 0 failed / 0 skipped**.
- Avalonia window-state adapter: **5 passed**.
- Separate WindowsDesktop oracle: **15 passed** (10 prior color cases + 5 window default/native-WPF-default/legacy-wire cases).
- Modern solution: **81 Archiver + 22 Avalonia = 103 passed / 0 failed / 0 skipped**.
- The actual legacy source branches compile against net452, Core net45 and original Metrolib in the separate build probe. Modern Core builds and its restored graph stays free of WPF/Forms/Metrolib/SharpRemote/PE/System.Drawing.Common.
- Linked profile probe remains **4 passed / 4 expected failures**, not a completed profile gate.

```powershell
dotnet build specs/001-full-project-migration/probes/windows/LegacyWindow.csproj -c Release
dotnet test specs/001-full-project-migration/probes/colors/ColorOracle.Tests.csproj -c Release
dotnet test src/Tailviewer.Core.Tests/Tailviewer.Core.Tests.csproj -c Release -f net10.0 --filter 'FullyQualifiedName~Settings|FullyQualifiedName~FixtureManifestTest|FullyQualifiedName~DataSourceTest|FullyQualifiedName~DesktopTest|FullyQualifiedName~RectangleTest'
dotnet test Tailviewer.Avalonia.slnx -c Release
```

Evidence: `artifacts/migration/t011-window-defaults.log`, `t011-window-red.log`, `t011-window-final.log`, `t011-window-adapter.log`, `t011-window-oracle.log`, `t011-window-legacy-verified.log`, `t011-window-solution.log`; profile probe `run-8563553c58554c0e9189b54729681d42`.

The legacy compile probe is a source/API build check, not a complete old desktop/service build. Native Avalonia window capture/restore, display pixel-to-DIP conversion and mixed-DPI/monitor hardware calibration are still owning UI acceptance work; do not interpret state conversion or geometry tests as verification of those native journeys. T011 remains open for the profile/custom-format models and full unknown/plugin XML preservation. No real user window/profile was modified; the WPF oracle window was created without showing it and closed in the test.
