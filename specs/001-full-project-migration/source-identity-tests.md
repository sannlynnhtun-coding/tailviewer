# T015/T016 — Source identity, membership and generation contracts

## T015 executed baseline

Ten ordinary Core tests exercise existing source XML models plus the minimal
SourceConfiguration snapshot/generation service. Six passed; four behavioral
failures demonstrate missing duplicate/empty identity and self/two-node cycle
validation. This is executable behavior, not a missing-type/compiler failure.

Passing baseline cases cover missing paths/unknown XML across reopen, duplicate
display names with ID-based membership, configured member ordering and isolated
snapshots, unresolved parents, late async completion after invalidation, and
independent generations for different sources. Async tests use a controlled
TaskCompletionSource, without sleeps or actual user files.

Evidence: ignored artifacts/migration/t015-source-identity-red.log.
The normal test suite intentionally contains the red graph cases until T016's
following implementation commit; no compiler flag or ignored tests are used.
T016 must validate graphs and connect the generation gate to the Avalonia reader.
## T016 implementation and executed verification

SourceConfiguration reuses deep-cloned DataSourceSettings/DataSource models and
keeps unavailable paths, unresolved parent IDs, selection and unknown XML. Group
members are resolved by ParentId in configured list order, with no name-based
identity or duplicate entry model. Empty/duplicate IDs and parent cycles are
rejected by an iterative graph pass; missing parents stay unresolved. The importer
validates this graph before publication and retains a verified original backup
and unchanged target on invalid cycles.

Per-source runtime generations invalidate pending results independently. A
synchronous publication gate serializes generation checks with invalidation.
Avalonia LogDocument now uses this shared gate for page results, late errors and
copy results. Core reset notifications invalidate immediately, before the next UI
refresh; truncation fallback and disposal also invalidate. The configured-source
constructor preserves the supplied ID across reopen/replacement. The existing
filename constructor creates a fresh source identity; persisted workspace binding
and file/group lifecycle UI remain their later story tasks.

Executed evidence on Windows x64 / SDK 10.0.302:

- All ten T015 cases now pass; four graph cases were recorded red beforehand.
- Final Migration suite: 183 passed, zero failed/skipped, including importer
  cycle rollback and all earlier persistence/import contracts.
- Modern solution: 81 Archiver + 23 Avalonia tests passed. The added actual-file
  replacement/reopen test verifies configured identity and new copied text.
- An initial 22-case Avalonia run hit the previously recorded eight-second
  TestScrollingVirtualizesAndLoadsDistantLines timeout. The exact case passed in
  isolation and the subsequent full solution passed. No timeout/assertion change
  or skip was used; retain this observation as test flakiness.
- The solution also built the retained legacy Core/Archiver targets successfully;
  their known NU1902 warnings remain visible.

Ignored logs: artifacts/migration/t016-migration.log, t016-solution.log,
t016-prototype.log and t016-scroll-recheck.log.

No full Core rerun or native clipboard/desktop acceptance is claimed for this
slice. T017 separately audits the foundation suites and modern resolved assets.
Full workspace persistence/multi-source UI and native plugin source lifecycles
remain pending; this helper does not claim those story gates complete.