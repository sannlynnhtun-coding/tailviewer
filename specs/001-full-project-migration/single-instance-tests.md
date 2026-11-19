# Single-instance migration evidence

## Slice 1: bounded source-request protocol

The legacy helper uses MMQ, and its acceptance cases contain retry/sleep tooling.
Research calls for mutex/named pipes in the modern application. The new internal
SingleApplicationMessage codec is the first transport primitive; it is not yet
connected to application startup and does not change current instance behavior.

Messages have a four-byte little-endian positive length and a versioned JSON
request containing ordered paths. Limits are 256 KiB serialized payload, 256
paths, 32,767 UTF-16 code units per path and 256 Ki code units total. Empty paths
are the bring-to-front request. Relative outgoing paths become absolute in the
sender's working directory. Duplicate paths retain order for existing source-open
deduplication. Incoming paths must be absolute and nonblank, without NULs.
Unknown fields/versions, missing/null paths, malformed JSON, invalid lengths,
truncated frames and excessive path counts/sizes are rejected. Validation of the
complete outgoing request precedes any write. Reads accept a cancellation token;
the future listener must supply its bounded per-connection deadline.

Twenty codec cases cover Unicode/relative paths, bring-to-front, limits,
truncation and malformed data. An owned GUID-named CurrentUserOnly pipe proves
actual byte-stream transfer and cancellation after an incomplete next header.
No application/user processes or profiles are involved. Initial runs exposed
fixture mistakes (nonexposable MemoryStream buffer, a pipe write before the
matching read, and normalizing an intentionally overlong incoming fixture);
these were corrected without weakening limits/assertions or increasing timeouts.
They are not claimed as production behavior regressions caught before the code.

Final complete modern suite: 81 Archiver + 116 Avalonia pass, no failures/skips.
Ignored evidence: artifacts/migration/t025-message-focused.log,
t025-message-focused-final.log, t025-message-modern.log and
t025-message-modern-final.log. Existing legacy NU1902 warnings remain visible.

T021/T025 remain open. Mutex ownership, user/session channel naming, listener
recovery/deadlines, acknowledgements, startup/UI dispatch, restore/activation,
crash recovery and real-process native acceptance are not implemented by this
slice. No sandbox/cross-user security or completed handoff claim follows from
the same-user pipe test. Full Core/native suites were not rerun because this
primitive is not yet wired into the app and changes no shared Core behavior.
