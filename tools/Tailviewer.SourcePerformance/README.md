# Core source performance preflight

Run on the recorded Windows reference machine:

```powershell
dotnet run --project tools/Tailviewer.SourcePerformance/Tailviewer.SourcePerformance.csproj -c Release
```

This generates the manifest's 100 MiB and 1 GiB fixed 128-byte records in one
owned temporary directory. Each size gets three read-through warm-cache runs.
It reads the first 32 records, waits for complete indexing, verifies **every**
offset, removes a middle LF without changing final size/write time, and verifies
all rewritten offsets plus the merged record. The raw index's terminal EOF
position is checked separately from the complete record count.

JSON output includes Core first-page readiness, indexing/recovery time and the
process lifetime peak working set sampled after indexing, plus process CPU time and allocation deltas. Nonzero exit means
incorrect contents, timeout, insufficient reference hardware/disk space or an
exceeded budget. The fixture is removed on exit. No user log/profile is opened.

**Scope:** Core data readiness is not rendered UI, Core process memory is not
complete-application memory, and a read-through does not prove OS cache residency.
Record SSD/storage details and background load separately. This preflight is
deliberately outside the routine solution; it adds no skip to functional suites.
Core live-tail mode is described below; native UI gates remain required.

The first measured run failed the 1 GiB three-second recovery budget. Later optimized standalone runs pass this Core preflight; earlier failures and timing variability are retained in
`specs/001-full-project-migration/recovery-tests.md`. Do not lower the threshold
or present its other measurements as a complete performance acceptance pass.

## Five-minute live tail

```powershell
dotnet run --project tools/Tailviewer.SourcePerformance/Tailviewer.SourcePerformance.csproj -c Release -- tail
```

The `tail` mode writes 100 LF-terminated monotonic integer records every 100 ms
for 300 seconds (300,000 records). One readiness sample per batch checks the
last complete record through Core, and final verification reads **every** record
back in order. A terminal EOF offset is counted separately. No shorter-duration
option is provided. JSON progress is emitted every 15 seconds.

The nearest-rank p95 of 3,000 Core readiness samples must be at most 1,000 ms.
Writer pacing is reported separately; p95 writer lateness over 100 ms rejects
an unqualified workload. A stalled reader has a bounded catch-up deadline. On
failure the producer is cancelled/awaited before source/temp cleanup.

This is Core availability after flush, not native UI display latency. Rendered
UI/pause/navigation, full-application memory and the wider source matrix still
need their own acceptance evidence. Unknown command-line modes exit 2 before
creating a fixture.

## Native Windows live-tail gate

```powershell
dotnet test src/Tailviewer.Acceptance.Tests/Migration/Tailviewer.Native.Acceptance.Tests.csproj -c Release --filter FullyQualifiedName~NativeFiveMinuteTail
```

This isolated interactive Windows x64 fixture links the same `PacedLogWriter`
as the Core workload. Close existing Tailviewer instances first. It owns its
process and synthetic file, snapshots/restores the clipboard conditionally, and
requires its viewer to remain foreground throughout sampling. It records visible
numeric UI Automation text after each batch, including delayed batches after
failed observations; unavailable UIA samples are logged, not counted as success.
The nearest-rank p95 budget remains 1,000 ms for all 3,000 batches.

After five minutes it uses actual Ctrl+A and the Copy selected command to verify
all 300,000 records and the terminal newline through the native clipboard. An
extra append must retain paused follow state and the original selection. Numeric
JSONL progress/measurements are attached to the NUnit result; no clipboard data
is logged. Visible accessibility text is the observation boundary, not a pixel
presentation timestamp. This does not replace the wider recovery, memory,
navigation or mixed-monitor acceptance matrix.
