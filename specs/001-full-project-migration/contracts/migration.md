# Migration Boundary Contracts

## 1. Settings/profile import

Input is a selected/detected legacy profile plus its dependent settings data, never a writable shared old/new profile. Offer first-run import; declining uses defaults. Later Settings import checks conflicts before replacing modern values. Preserve unknown elements and plugin data verbatim where possible, including unavailable plugin schemas. Output includes separate modern profile, verified backup and actionable conversion report. Failure leaves source and previous valid target unchanged. Fixture contract: original/backup hashes equal, required fields match after two restarts, repeated import does not silently duplicate source IDs/bookmarks.

## 2. Plugin host and archive

The modern API keeps the behavioral surface of `Tailviewer.Api/Plugins` and `Ui`, not old binary identity. Version API/interface/archive changes and document author rebuild steps. Cover custom source/config/custom view; source parser; entry parser; format matcher; custom format creator; issues/listeners/analyser; outline/viewmodel; property presenter/navigation. Share host API assembly identity. Use encoded icons in metadata and Avalonia adapters at presentation boundaries, no WPF types in modern shared contracts.

Archive input must validate identity/version/interface/dependencies, every path/length, duplicates and extraction limits before activation. Reject rooted/traversal/symlink escapes; stream extraction with an enforced total size bound. Preserve known legacy IDs/config names or explicit converters. Install/update uses staged directories and retains old usable version until commit. Restart-required replacement is valid; hot unloading is not promised. Local install works with no repository.

Existing CLI commands/options, packaging metadata, stdout/stderr and exit-code meanings are baseline contracts: capture them from Archiver application/option classes and test fixtures in P00/P08 before modernizing parser syntax. Include desktop `/testloadplugin <file> <interface>` success/nonzero failure behavior, default file argument and single-instance forwarding. Do not claim a legacy command was preserved without a mapped executable case.

## 3. Versioned repository client/service

Source of truth: `src/Tailviewer.Archiver/Repository/IPluginRepository.cs` and repository admin option classes. Proposed modern transport: HTTP `/api/v1` with explicit DTOs/error codes; client/server migrate together. No old SharpRemote wire compatibility claim.

| Operation | Contract |
|---|---|
| PublishPlugin | authenticated author token, package bytes/metadata; validate authorization, IDs/version, compatibility and archive; commit atomically or return typed failure |
| FindAllPluginsFor | filter by supported interface identity/version as legacy contract specifies |
| FindUpdatesFor | return eligible newer versions for requested plugin/interface set; preserve baseline version/filter rules |
| FindAllPlugins | list known plugin identities with deterministic documented ordering |
| GetDescriptions / GetIcons | preserve request order and null placeholders for missing identities; no compacting results |
| DownloadPlugin | exact published bytes for requested identity/version or explicit missing result; stream/cancel within configured bounds |
| Admin operations | add/remove/list users; add/remove/list plugins; export and write configuration; preserve original authorization and failure semantics |

Use loopback binding for acceptance, isolated database and generated test credentials. Authenticate mutating operations; never log tokens. Non-loopback endpoints require TLS and explicit configuration; public deployment is deferred. Reject invalid sizes and unsupported versions before deserializing large input. Keep database identity/relationships/package/icon hashes across verified export/import; schema version mismatch gives guidance and never overwrites the original store. Retain Windows service lifecycle and console-run mode; no service registration is needed for most integration tests.

## 4. Release/update protocol

Release source is `sannlynnhtun-coding/tailviewer`; query stable/preview channel as selected. Parse semantic prerelease ordering independently of assembly file version. Match platform and installed/portable distribution; do not install ZIP/installer assets interchangeably. Fixed HTTPS repository/download-host policy includes the documented GitHub asset redirect destinations; redirects to arbitrary hosts or downgrade to HTTP are rejected.

New profiles check automatically and notify only; imported preference wins. Settings disables automatic checks; manual check still works. Download requires user action, validates expected length plus SHA-256 against authenticated release asset metadata and checksum file, and does not execute unverified content. Missing required metadata blocks automatic apply and links the known release page for manual action. Repository account compromise remains outside what checksum validation can prove; signature verification may strengthen provenance when a signing identity is provisioned, never bypass a present failing signature.

Install is separate from download and requires explicit action; restart requires consent. Stage, journal, stop only the owned app after consent, apply only owned files, health-check then retain recoverable backup until commit. Roll back failed apply; on interrupted startup report recovery status and preserve logs/settings/plugins. Installer elevation is not required for per-user install. Wrong architecture/channel/repository, timeout/rate limit, cancellation, disk-full, locked payload, digest mismatch and interrupted apply have distinct actionable outcomes.

## 5. File-derived state and privacy

Copy/export always refers to source entry identity and original text; merge/filter display indexes are not source IDs. Async batch/cache/search results carry source generation so a rotation cannot publish stale content. Encoding change invalidates decoding and prompts/reopens according to the persisted choice; preserve original byte/text handling baseline.

No logs/profile/plugin secrets are uploaded during normal reading, imports, diagnostics or update checks. Network calls are limited to explicitly configured service/release operations and user-requested publishing. Headless tests substitute clipboard/update/service boundaries; native acceptance validates actual OS behavior separately.
