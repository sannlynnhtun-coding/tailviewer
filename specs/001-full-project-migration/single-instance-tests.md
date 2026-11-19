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

## Slice 3: user/session ownership lease

SingleApplicationHelper derives a stable versioned channel name from the Windows
user SID and process session ID using SHA-256; the raw SID is not included in
the channel name. Its named mutex uses .NET 10 CurrentUserOnly and
CurrentSessionOnly options. TryAcquire is nonblocking, accepts transferred
abandoned ownership, and returns no lease while another primary holds it. An
in-process guard prevents recursive acquisition on the same thread from creating
another primary. Dispose is idempotent and requires the acquiring thread before
changing either kernel ownership or the guard. The future synchronous STA Main
must retain/dispose the lease across the complete desktop lifetime.

Four cases verify same-thread/helper and competing-thread exclusion, a separate
raw kernel-mutex contender, release/reacquisition, abandoned raw-mutex ownership
transfer, wrong-thread disposal preserving ownership, and channel-name separation
across user/session inputs. All mutex names are test-owned GUIDs. Abandonment is
produced by an owned thread exiting without ReleaseMutex while a witness handle
keeps the kernel object alive; this is not a process-crash acceptance result.

Full modern suite: 81 Archiver + 126 Avalonia pass, no failures/skips.
Evidence: ignored artifacts/migration/t025-owner-focused.log and
t025-owner-modern.log. Existing legacy NU1902 warnings remain visible.

Startup/pipe/UI integration is still pending, so application instance behavior
has not changed yet. Windows native two-process handoff, actual process-crash
recovery and cross-user/session isolation acceptance remain unproven. The Unix
username/session-zero naming fallback is explicitly a later-milestone shortcut,
not Linux/macOS single-instance support. No unrelated process is enumerated or
killed by this ownership helper.

## Slice 4: desktop startup and bounded UI intake

The shipping synchronous STA entry point now holds the ownership lease for the
whole desktop lifetime. Secondary launches send their arguments with a
five-second deadline and exit zero after acknowledgement. Failure presents a
small connection-error window and exits one when closed; it does not kill or
replace the primary. The primary starts its pipe after initial arguments have
finished opening. Exit cancels the listener; Main observes its completion before
releasing the mutex. Listener faults are surfaced through the existing status UI.

UI dispatch checks cancellation before accepting work into a queue of at most
16 pending requests. Acknowledgement means accepted by that queue, not that all
files finished opening. Requests use the existing ordered OpenFilesAsync flow;
empty requests restore/activate the window without resetting rows/selection.
Close all and window shutdown clear queued requests. Accepted requests persist
after connection completion; acknowledgement loss is delivery-uncertain rather
than an exactly-once guarantee. Existing configured-path deduplication remains
the source identity boundary.

Three headless cases verify ordered source requests, empty-request restore with
selection/pause/copy preservation, cancellation/closed-window rejection and
close-all cleanup after actually awaiting the in-flight queue task. Complete
modern suite: 81 Archiver + 129 Avalonia pass, no failures/skips. Evidence:
artifacts/migration/t025-startup-build.log, t025-startup-focused.log and
t025-startup-modern.log. Native test additions were checked in the working tree
and are committed separately with their results.

T025 remains open for real-process crash/timeout behavior, off-screen/hidden
window handling and the remaining source-task dependencies. No full Core rerun
was performed because shared Core code did not change. This is an unreleased
working-tree application change; published alpha artifacts are unchanged.

## Slice 5: native lifecycle contracts

The real shipping entry point now has native evidence for restart after both a
graceful exit and an owned-process crash, including abandoned-mutex recovery with
a retained witness handle. An occupied mutex without a listener exercises the
five-second production connection timeout, visible error window, exit code one
and preservation of the existing ownership lease. No runtime fix was required.

Focused lifecycle cases pass 3/3; the complete native suite passes all 14 cases
with no failures/skips. Details and evidence paths are in
[native-entry-point-tests.md](native-entry-point-tests.md), slice 7. T021's test
contract is complete. T025 remains open for its implementation dependencies and
remaining window behavior; no cross-user/session isolation or fully completed
single-instance migration is claimed.
