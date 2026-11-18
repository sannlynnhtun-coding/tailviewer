# T009 — copied repository storage probe

Executed 2026-11-19 on Windows x64, SDK 10.0.302. Production repository code/data are unchanged.

## Reproduce

From the repository root, run:

```powershell
& ./specs/001-full-project-migration/probes/storage/run.ps1
```

Requires Python 3 (standard-library SQLite only) and the installed .NET Framework runtime. The recipe creates a unique ignored directory under `artifacts/migration/storage-probe/`, copies the probe sources there, builds and runs real net452/net10.0 executables, and fails on unexpected exits or verification differences. No production database path is accepted. It uses the actual linked repository entities, original assembly name/version/signing key, actual Archiver contracts and original five registered custom types. Only generated test usernames/tokens are used; no database/export/token values are checked in.

IsabelDb 0.6.21, SQLite 1.0.111.0 and protobuf-net 2.4.0 match the original repository declarations. An unpinned first restore produced NU1605 because IsabelDb declares conflicting SQLite minimum versions; the probe explicitly selects the original repository versions. SQLite's old content target flattened an x86 interop DLL into the legacy output root; the recipe selects its existing x64 native asset for this x64 probe. This is probe setup, not a shipped modern packaging fix. NU1701 framework compatibility and the known legacy log4net advisory remain visible.

## Executed corpus/results

- Legacy net452 creates a store with 2 users, 2 token mappings, 3 packages, 3 icons and 3 publication descriptions. IDs are `probe.plugin` v1.1.0/v1.2.0 and `probe.other` v1.3.0. Metadata includes version/interface requirements, UTC dates, optional null website, Unicode description and byte lengths.
- With an empty icon rather than null: both runtimes open/export all five collections. The modern process reopens/exports again. All five neutral XML files match byte-for-byte between runtimes; modern DataContractSerializer reads and reserializes them. Original/copy database hashes remain equal.
- With an actual null icon: BOTH legacy and modern `GetAll()` exports fail in `IsabelDb.Serializers.ByteArraySerializer.TryDeserialize` with `InvalidCastException: System.DBNull -> System.Byte[]`. This is a reachable original record shape: `PluginRepository.PublishPlugin` stores `archive.ReadIcon()?.ReadToEnd()` directly. It cannot be treated as a corrupt synthetic-only case or replaced with empty bytes silently.
- Read-only raw SQLite export of the null corpus, followed by fresh-store reopen, preserves every table schema/ordered row/value, including null and BLOB bytes. It also preserves the 29 type rows and 27 field rows needed to interpret serialized records. Source hash is unchanged. The copied null store still exhibits the known IsabelDb failure; raw snapshot preservation does not pretend to repair that library.

The JSON raw-schema round-trip is a disposable self-check against known generated tables, **not** the product import format. Do not execute schema SQL from an untrusted snapshot in a shipped importer. P10 must use a bounded, versioned, explicit DTO format and allowlisted schema, retain the raw backup, decode and verify every custom record/ID/hash before cutover, then import transactionally. No modern storage provider was added by this task.

## Decision for P10

Select the **neutral export + supported SQLite fallback**, rather than ship IsabelDb 0.6.21 as the modern store. Ordinary records do work on this Windows runtime; claiming a total runtime incompatibility would be false. The full compatibility gate fails on supported nullable icon records, and the old framework/native dependency graph requires deployment adaptations. Preserve the read-only legacy environment as a migration tool, including explicit null-safe extraction; do not use its failing icon enumeration unchanged. Unknown/malformed custom types must block cutover with an actionable report, never create an empty replacement or discard rows. P10's semantic null-safe exporter/importer and full repository tests remain required.

## Hash evidence

Final run: `artifacts/migration/storage-probe/run-20002d6c8acb45ff94563572845e3002/` (ignored).

| Artifact/value | SHA-256 |
|---|---|
| Null corpus database, before/after | `44cefc110bea1055750032538a31f2bf497e59863cca13a647edf00650300cc0` |
| Null-preserving raw neutral snapshot | `c2e7ba8efac1bd0cb6cd8b2bf9f767a3ae59672dd5902f6c009780e03230d52d` |
| Users neutral XML | `4e1a0d5eae695b55bb78d39bf02ddfbc0dc61b4cec2cff1526fc8c287833518d` |
| Token mappings neutral XML | `4cc22d7d7df1af10420d8f683a0112e3130879943c513fc55448a7fdb66cfa16` |
| Packages neutral XML | `30ca6dda9449598305690bb1ab562a571ebd5ab51995745177a95e6482dad480` |
| Icons neutral XML | `d6e6a628ff0936a197e1819e5ffc9fec74011bd697d3b48539edc55fee037fca` |
| Descriptions neutral XML | `febb3d7090b45db89e3a30f374d1b3b79c766c1ee7b7d235e08ad417402ea408` |

| Plugin ID/version | Package SHA-256 | Non-null icon SHA-256 |
|---|---|---|
| `probe.plugin/1.1.0` | `54acfbfedc4d8da40f76f275e1a98f10af8ef1fb9fb39e5a67a00aabcbe6597c` | empty: `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855` |
| `probe.plugin/1.2.0` | `5a2769ed36b70d29683c649b8ca34df2b1a9cd9a525c1f2666d7c0e20b5babdf` | `b858da80d8a57dc546905fd147612ebddd3c9188620405d058f9ee5ab1e6bc52` |
| `probe.other/1.3.0` | `e6375f062504d75519d345781e7824c4d7b7e6117f885649819b4fe6564dd6f0` | `4aba23aea5e2a91b7807cf3026cdd10a1c38533ce55332683d4ccb88456e0703` |

The null corpus uses null instead of the first empty icon; the raw snapshot equality check preserves that distinction. Packages/icon payloads are deterministic byte fixtures, not claimed valid plugin packages/images. Actual package validation remains Archiver/P10 test scope. Synthetic fixtures do not prove every real historical database version, crash state or unknown plugin schema is migratable.
