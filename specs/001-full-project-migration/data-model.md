# Data Model and State Transitions

These are logical models mapped to existing classes; do not create duplicate DTOs for every item without a real serialization boundary.

| Entity | Identity and fields | Relations and validation |
|---|---|---|
| LogSource | stable source ID; file/folder/custom type; path/config; encoding; display name; availability; timestamp offset; included state | Groups reference IDs, not display names. Reopening same configured source preserves identity. Paths can be missing; never silently remove unavailable sources. |
| LogEntry / merged entry | source ID + original source position; text; timestamp/level/parsed fields; content-generation revision | Retain original position/provenance through filters/merges. Revision invalidates cached rows and derived results after replacement; stale async reads must not publish into newer generation. |
| Source group | stable ID; ordered member IDs; merge settings | No cycles or duplicate member identities; deterministic timestamp-tie and missing-timestamp rules pinned to baseline fixtures. |
| Workspace/profile | schema version; source/groups; active source; selection/position; view/column/window preferences; filters/highlights; formats; export/update preferences; plugin settings; unknown XML | Isolated migrated profile. Validate known values/defaults; preserve unknown data. Do not overwrite older original. Existing defaults are baseline, not guessed. |
| Import/backup record | original location/hash; backup location/hash; source schema; migrated schema; completion state; timestamp | Copy and validate backup before saving destination. On conflict require keep/replace choice, retain current valid target if cancelled. No backup success claim before existence/hash checks. |
| Filter/highlight/search | rule identity; text/pattern/type; case/options; level/time/expression; include/exclude; color/priority; enabled state | Reject invalid rules without crashing; ordering/composition from baseline. Search result references source position/revision. Large operations cancellable. |
| Bookmark | source ID; original entry identity/position; label; saved context | Mark unresolved after replacement when original content cannot be located; never silently jump to unrelated content. |
| Format/column | stable format/column IDs; parser/matcher config; value type/default; timestamp/level/multiline mappings; visibility/order/width | Preserve serialized IDs and defaults. Missing plugin-owned format retained as unresolved rather than dropped. |
| Plugin descriptor/package | stable plugin ID/version; API/interface versions; dependencies; metadata/license; archive entries/icons; signature/digest if provided | Duplicate IDs/version conflicts explicit. Archive paths constrained to installation root; no traversal/rooted links. Reject incompatible version before activation. |
| Plugin settings | plugin ID + config schema/type identity; raw preserved data; converter version | Port reuses identity or provides explicit old-to-new mapping. Conversion is transactional with original raw data retained until success. |
| Repository user | stable username/ID; email; access-token mapping | Preserve existing authorization and uniqueness rules. Never include tokens in error logs or checked-in fixtures. |
| Repository publication | plugin ID/version; author; compatibility interfaces; package/icon bytes; metadata; changes | Preserve ordering/null lookup semantics. Export/import validates counts, IDs, content hashes and custom serialized records before cutover. |
| Release/update candidate | repository; version/channel; win-x64; distribution kind; asset ID/URL/size; digest/provenance; previous payload; apply journal | Reject wrong repo/host/version/platform, invalid size/digest or absent validation. Stable selection excludes previews; imported preference persists. |
| Parity evidence | inventory ID; FR/story/SC; fixture/hash; old/new result; command; commit; environment; result/skip reason | Only executed, passing acceptance evidence closes a row. Source-only baseline limits are explicit. |

## State machines

- Source: `Configured -> Loading -> Ready/Empty/Error`; append stays Ready with revision updates, replacement enters Loading/new generation, disappearance enters Error with retained config, recovery reindexes. Closed cancels pending work and suppresses late UI updates.
- Import: `Detected -> Offered -> Declined` (defaults still usable) or `BackupVerified -> ConflictResolved -> Converted -> Validated -> Saved`. Any failure leaves original/last-valid destination usable; later import remains available.
- Plugin: `Discovered -> Validated -> Installed -> Enabled`; incompatible/failed activation retains config and actionable status. `Enabled -> Disabled -> Removed` only after effects on active sources are disclosed. Update stages a new version and restores old version/config on failure; restart required if unload cannot be guaranteed.
- Update: `Idle -> Checking -> Current/Available/Offline/Error`. Automatic flow ends at notification. User action: `Available -> Downloading -> Verified -> AwaitInstallConsent -> Staged -> AwaitRestartConsent -> Applying -> HealthCheck -> Complete`; cancellation before apply preserves old payload; apply failure enters Rollback/RecoverableError. Journal survives restart/interruption.

## Persistence boundaries

Use existing XML settings serializers with bounded parsing and unknown-node retention. Save on same volume through temporary validated files and atomic replacement, never serialize directly over the last valid profile. Legacy repository conversion works on copies with explicit export schema and import verification; no inferred EF schema. Logs remain read-only; install manifests list owned payload files so uninstall/update never sweeps unknown user files.
