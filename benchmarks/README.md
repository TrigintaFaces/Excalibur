# Benchmarks Directory

This folder contains benchmark projects, baseline artifacts, and benchmark tooling scripts.

## Canonical Docs

- Consumer-facing comparison page: `docs-site/docs/performance/competitor-comparison.md`
- Dispatch benchmark suite guide: `benchmarks/Excalibur.Dispatch.Benchmarks/README.md`
- October 19 four-assembly case census and historical comparison boundaries:
  [`benchmark-census-2026-10-19.md`](../docs/performance/benchmark-census-2026-10-19.md).
- October 19 routing lifecycle repair and three original WarmPath observations:
  [`routing-context-lifecycle-2026-10-19.md`](../docs/performance/routing-context-lifecycle-2026-10-19.md).

## Canonical Runner

```bash
pwsh ./eng/run-benchmark-matrix.ps1 -NoBuild -NoRestore -ArtifactsPath ./benchmarks/runs/BenchmarkDotNet.Artifacts.FullRefresh-20260219
```

This produces per-class logs and summary files in `benchmarks/runs/BenchmarkDotNet.Artifacts.FullRefresh-20260219/results/`.

## Policy

- Do not commit `benchmarks/runs/BenchmarkDotNet.Artifacts*` directories.
- Update docs only from dated, reproducible verified runs; keep subset results separate from full-matrix coverage.
- Keep MediatR-local parity and transport comparison interpretations separate.

## Leader-election lifecycle observations — October 18, 2026

The original `GracefulShutdown` cases completed after the in-memory resource-bucket
retention repair. These are start/stop lifecycle observations, **not hot-path or
competitor comparisons**. Each timed call starts and stops candidate 0; the other
candidates only change the setup population.

| CandidateCount | Mean | Standard deviation | Allocated per operation |
| --- | ---: | ---: | ---: |
| 1 | 4.9125 µs | 0.8943 µs | 240 B |
| 5 | 5.8392 µs | 1.1053 µs | 240 B |
| 10 | 4.8682 µs | 0.3541 µs | 240 B |

This single controlled Release capture used .NET 10.0.12 RyuJIT and the original
HostProcess job, invocation count 1, unroll factor 1 and adaptive iteration policy.
All three children exited successfully: 300 actual measurements, 281 retained
results and 19 removed outliers. BenchmarkDotNet reported short iterations in all
three cases and multimodal distributions in two. These values do not establish a
stable latency target, candidate-count scaling, a speedup or NativeAOT performance.

The local campaign evidence is under
`artifacts/benchmarks/full-refresh-20261015/hot-current-controls/leader-shutdown-current-r1/`:
`leader-shutdown-three-r3/` contains the raw capture and
`original-three-reconciliation-r3.json` records the scope. Parent inputs stayed
unchanged; the generated child provider and fixture PE/PDB source documents match
the repaired sources. Full transitive loaded-image/source closure was not certified.
Earlier failed and source-drift attempts remain separate evidence.

The follow-on acquisition, health, property and failover-labelled subsets completed
all 27 original cases in this fixture. This includes one diagnostic no-op:
`LeaderFailover(CandidateCount: 1)` returns immediately. Its timing is not a
capability result. The other `LeaderFailover` cases use independent resources and
a fixed 10 ms delay, so they do not measure shared-resource handoff. Health and
property cases include startup; they are not isolated hot-path reads. Raw R4–R7
captures and their scoped reconciliations retain those distinctions and warnings.
This fixture coverage does not establish completion of the full benchmark suite.

Separately, 168 in-memory tests, a native lifecycle/fencing executable and 1,152
untimed calls to the actual fixture verified the repair. Candidate buckets returned
to zero after every fixture cleanup. These controls are not assertions on every
timed invocation and do not establish a bound on all shared-state memory; fencing
history remains retained.

## SQL saga fixture

`Excalibur.Benchmarks.Saga.SqlServerSagaStoreBenchmarks` requires a nonblank
`BENCHMARK_SQL_CONNECTIONSTRING`; setup fails closed when it is missing. Use a
disposable database. The supplied login needs permission to create schemas, tables
and indexes, perform saga DML, inspect object metadata, and drop its owned table
and schema.

Each case creates a unique `saga_benchmark_<GUID>` schema from the embedded
canonical saga DDL. Cleanup targets only that exact owned schema and its canonical
`sagas` table; it refuses unexpected objects. It never cleans up the caller's
`dispatch.sagas` table. A cleanup failure must be investigated rather than bypassed
with a broad schema cascade.

The five original load, miss, save, update and save/load intents and HostProcess
job remain. Canonical DDL binding and expected-version tracking define a repaired
fixture epoch, not a baseline speedup. The benchmark and regression-test projects
build in Release with zero warnings and errors, and all three fixture unit tests
pass. All five original cases subsequently completed against disposable SQL Server
2022 CU27. Six real-SQL integration tests passed for payload preservation, updates,
stale-write rejection without lost updates, and tenant isolation. The executed
benchmark assemblies embed the exact shipped schema resource; their repaired
fixture and relevant provider source checksums agree with the verified sources.
The separately rebuilt provider binaries are not byte-identical.

Evidence lives under
`artifacts/benchmarks/full-refresh-20261015/hot-current-controls/sql-saga-original-r1/`.
The first read capture failed during setup and remains recorded. Later observations
do not establish a speedup, NativeAOT performance, or full-suite completion.
Normal cleanup left no owned schemas or tables; unknown-object injection and
post-commit seed-failure cleanup remain unexecuted negative paths.
