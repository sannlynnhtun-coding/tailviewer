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