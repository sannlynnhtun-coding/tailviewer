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

## Slice 2: cancellable named-pipe listener and acknowledgement

SingleApplicationPipe owns one asynchronous CurrentUserOnly server connection
at a time. Connection reads, dispatch and acknowledgement share a linked
per-request deadline. Cancellation stops an idle listener and disposes its pipe;
malformed/truncated/disconnected/stalled clients are discarded before accepting
the next connection. A one-byte success acknowledgement follows completed
dispatch. The sender bounds connection/write/acknowledgement with one deadline,
distinguishing its timeout from caller cancellation. Dispatch exceptions other
than expected protocol/IO/cancellation faults remain observable on the listener
task; they are not silently swallowed as successful requests.

Six GUID-named real-pipe cases verify acknowledgement ordering, two successive
requests, malformed and truncated clients, stalled-client expiry, sender timeout
versus cancellation, and channel release/reuse after listener shutdown. The
first run exposed a real recovery failure: InvalidDataException was not caught
by the IOException filter. Adding that explicit connection-level case fixes the
failure without changing the test's five-second bound. Full modern suite then
passed 81 Archiver + 122 Avalonia cases with no failures/skips. A final focused
run passes all six after strengthening the exact request-count assertion.

Evidence: ignored artifacts/migration/t025-pipe-focused.log,
t025-pipe-modern.log and t025-pipe-focused-final.log. No app/user process is
started, killed or modified by these isolated transport cases.

This transport is still not connected to startup. The app must supply finite
deadlines, observe listener faults and ensure dispatch checks cancellation before
publishing queued UI work. Mutex/user-session naming, UI activation, shutdown
integration and real-process crash/handoff acceptance remain open T021/T025
work. The current tests run both pipe ends in one process and do not prove those
cross-process ownership or cross-user security requirements.
