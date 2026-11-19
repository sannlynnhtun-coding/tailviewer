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
The five-minute live-tail workload and native UI gates remain required.

The first measured run failed the 1 GiB three-second recovery budget. Later optimized standalone runs pass this Core preflight; earlier failures and timing variability are retained in
`specs/001-full-project-migration/recovery-tests.md`. Do not lower the threshold
or present its other measurements as a complete performance acceptance pass.
