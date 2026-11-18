# T006/T007 — Archiver metadata boundary evidence

**Recorded**: 2026-11-19. Tests and production changes were committed separately. No full Archiver/legacy desktop migration is claimed.

## Scope and behavior

- `IPluginDescription.Icon` and `PluginDescription.Icon` now carry encoded `byte[]`, with null meaning no image; scalar metadata, stable plugin IDs and serializable-type names remain unchanged.
- `PluginGroup` copies the archive icon stream to owned bytes and disposes the entry stream. It no longer decodes images or references WPF imaging types.
- The legacy WPF `InstalledPluginViewModel` decodes once at the presentation boundary, uses OnLoad to detach from the stream, freezes/caches the image and logs/falls back to no image for malformed/empty data. Plugin text metadata remains usable.
- Existing packer already accepts a stream and writes encoded bytes; its dimension checks/System.Drawing implementation are unchanged and remain part of later Archiver target migration.

## Regression evidence

| Run | Passed | Failed | Meaning |
|---|---:|---:|---|
| Initial metadata tests | 1 | 3 | Runtime failures: ImageSource icon contract, PresentationCore assembly dependency, encoded icon assignment |
| Initial legacy adapter tests | 1 | 2 | Runtime failures assigning encoded bytes to the old ImageSource metadata contract |
| Final metadata tests | 4 | 0 | Portable net10.0 contract, no WPF/Metrolib references, PNG ZIP + JSON metadata round-trip, null/default/ToString behavior |
| Final legacy adapter checks | 4 | 0 | Real WPF decoding on STA: null, valid detached/frozen/cached image, empty and malformed bytes |
| Final modern solution | 18 | 0 | 14 existing Avalonia tests plus 4 metadata tests; no skips |

The final modern solution run uses independent generated TRX filenames, avoiding the explicit-filename collision in the earlier integration run. Metadata and adapter tests use actual linked production source, not replacement model classes.

## Reproduce portable checks

```powershell
dotnet test src/Tailviewer.Archiver.Tests/Migration/Tailviewer.Archiver.Metadata.Tests.csproj -c Release
dotnet test Tailviewer.Avalonia.slnx -c Release
```

The small metadata test project links five production metadata files and the real API project. It deliberately does not build the entire legacy Archiver. The initial test commit used a Windows/WPF target so old source compiled and failed behavioral assertions; the final project targets plain net10.0 with no WPF framework reference. Fold this temporary linked-source harness into the full project-reference suite when T008 modernizes Archiver.

## Legacy adapter probe and its limits

The new permanent regression fixture is `src/Tailviewer.Tests/Ui/InstalledPluginIconTest.cs`, explicitly included in the existing legacy test csproj. Because the full legacy build is still blocked by the baseline prerequisites, it was executed in an isolated net10.0-windows/WPF runner under ignored `artifacts/migration/legacy-icon-probe/LegacyIcon.Tests.csproj`.

That runner references the real API project and links the same five production metadata inputs from the portable project, plus `InstalledPluginViewModel.cs`, `IPluginViewModel.cs` and the new regression fixture. It uses the same NUnit/Test SDK versions as the portable project. The runner tests the real WPF adapter on .NET 10; it does not establish that the whole .NET Framework app builds or runs. The generated local runner and TRX are retained for inspection; original full-framework adapter execution remains pending.

Full PluginGroup/archive-loader/packer integration is also deferred to T008 and the owning archive fixtures; this slice tests metadata representation and the real UI adapter, not full plugin load compatibility. This public metadata type change requires rebuilding its consumers, consistent with the accepted plugin port policy. Known legacy log4net restore warnings remain visible.

## Local raw artifact fingerprints

| Artifact under artifacts/migration | SHA-256 |
|---|---|
| `test-results/metadata-red.trx` | `349d90224177c4fe933c199f7330570f8bb2c28df9afeb3156abd74a0bfdbb69` |
| `test-results/metadata-green.trx` | `74bf64163b3a5550225290eae38096f373fc9e3a629dbbb9615305f78954896a` |
| `test-results/legacy-icon-red.trx` | `e0d419ae680f873d049ddbb8c6a55ea2cfb0ffff9e0624d13b26a701e65a6b7d` |
| `test-results/legacy-icon-final.trx` | `c0b6b2894b674d17e65acf00e9498ca3478155be37f5e0d7c784c219b78c8434` |
| `legacy-icon-probe/LegacyIcon.Tests.csproj` | `2acaf61613e0c36fae40882bb41ab4808265636b48ca6bf91b97c570a3b025d0` |
| `boundary-final.log` | `fda52233e6be8f3cddccd81dedccd062d2cb9e82fd876a16267aec1c711d8585` |
