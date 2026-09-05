# Excalibur Benchmark Baseline (Latest Sync)

This file summarizes the current committed comparative baselines used by docs.

## Run Metadata

### Current epoch

- Date: **September 5, 2026** (20260905 epoch)
- Runtime: .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3
- SDK: 10.0.400
- OS: Windows 11 (10.0.26200.9168/25H2)
- CPU: Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
- Tooling: BenchmarkDotNet v0.15.8
- Job: `warmpath-inproc`, `InProcessEmitToolchain`
- Baseline folder: `benchmarks/baselines/net10.0/dispatch-comparative-20260905/results/`
- Configs captured: `WarmPathBenchmarkConfig` only (ns-scale, auto-tuned). **There is no
  `ComparativeBenchmarkConfig` (μs-scale) run in this epoch**, so a claim needing the
  full-isolation job has no current source.
- Assembled from three runs: MassTransit, MassTransitMediator, Pipeline, RoutingFirstParity and
  TransportQueueParity from the first; Wolverine and WolverineInProcess from a second (re-run after
  a benchmark-arm label correction); MediatR from a third (re-run after a second correction).
- Measured run-to-run variance: about 4% on the Dispatch arms, about 8.6% on MediatR's. Do not read
  a latency ratio inside that band as a finding.
- **Do not publish the MediatR query comparison.** MediatR's own query row shifted about 21%
  between epochs for reasons unrelated to this framework; see the epoch README.
- Every allocation figure is a **floor** — 72 B of a command dispatch's 96 B is one
  `ExecutionContext` copy-on-write, which scales with the consuming application's async-local
  density.

### Prior epochs (superseded, preserved on disk)

- `benchmarks/baselines/net10.0/dispatch-comparative-20260903-2230/` — September 3, 2026, 22:30.
  Kept because it measured different code, not because it is wrong. Its headline figure
  (30.49 ns / 24 B) shipped in no released package.
- `benchmarks/baselines/net10.0/dispatch-comparative-20260903/` — the 15:59 run of the same day.
- `benchmarks/baselines/net10.0/dispatch-comparative-20260420/` — April 20, 2026. .NET 10.0.6,
  SDK 10.0.202, BenchmarkDotNet 0.15.8. The only epoch here that captured both
  `ComparativeBenchmarkConfig` (μs-scale, literal `InvocationCount=1`) and `WarmPathBenchmarkConfig`.
- `benchmarks/baselines/net10.0/dispatch-comparative-20260302/` — **not cross-diffable** with
  anything later, due to the BenchmarkDotNet 0.15.4 → 0.15.8 `InvocationCount` semantic shift.

## Comparative Snapshot (20260905 epoch)

### Track A: In-Process Parity

#### Dispatch vs MediatR

Source: `MediatRWarmPathComparisonBenchmarks-report-github.md`

| Scenario | Dispatch | MediatR |
|----------|---------:|--------:|
| Single command handler | 45.58 ns / 96 B | 41.32 ns / 152 B |
| Single command, strict direct-local | 46.00 ns / 96 B | 41.32 ns / 152 B |
| Single command, context-less 2-arg | 53.00 ns / 96 B | 41.32 ns / 152 B |
| Singleton-promoted command | 53.85 ns / 96 B | 41.32 ns / 152 B |
| Notification to 3 handlers | 134.99 ns / 96 B | 95.01 ns / 616 B |
| 10 concurrent commands | 596.06 ns / 1,360 B | 541.74 ns / 1,856 B |
| 100 concurrent commands | 5,584.43 ns / 12,160 B | 5,146.08 ns / 17,064 B |

Dispatch query rows, published without a MediatR column (see the epoch README): query with return
value 63.67 ns / 192 B, strict direct-local 63.02 ns / 192 B, typed API 63.76 ns / 288 B,
context-less 2-arg 69.87 ns / 288 B, singleton-promoted 68.99 ns / 288 B.

#### Dispatch vs Wolverine (Invoke/local in-process)

Source: `WolverineInProcessWarmPathComparisonBenchmarks-report-github.md`

| Scenario | Dispatch | Wolverine |
|----------|---------:|----------:|
| Single command | 47.00 ns / 96 B | 179.13 ns / 584 B |
| Notification to 2 handlers | 120.12 ns / 96 B | 199.66 ns / 600 B |
| Query with return | 63.74 ns / 288 B | 252.73 ns / 776 B |
| 10 concurrent commands | 585.42 ns / 1,360 B | 2,028.61 ns / 6,048 B |
| 100 concurrent commands | 5,662.98 ns / 12,160 B | 20,154.80 ns / 59,328 B |

#### Dispatch vs MassTransit Mediator (in-process)

Source: `MassTransitMediatorWarmPathComparisonBenchmarks-report-github.md`. The Dispatch arms in
this class run a local bus and allocate 184 B rather than the 96 B of the leaner pairings above.

| Scenario | Dispatch | MassTransit Mediator (ambient scope) |
|----------|---------:|-------------------------------------:|
| Single command | 75.37 ns / 184 B | 1,275.66 ns / 3,544 B |
| Notification to 2 consumers | 138.14 ns / 184 B | 1,765.57 ns / 4,176 B |
| Query with return | 87.63 ns / 376 B | 11,426.39 ns / 11,601 B |
| 10 concurrent commands | 801.83 ns / 2,240 B | 12,481.32 ns / 35,648 B |
| 100 concurrent commands | 7,650.61 ns / 20,960 B | 125,098.21 ns / 355,329 B |

Scope-per-message mediator (plain `IMediator`) on single command: 1,630.79 ns / 4,336 B.

#### Dispatch vs MassTransit (in-memory bus)

Source: `MassTransitWarmPathComparisonBenchmarks-report-github.md`

| Scenario | Dispatch | MassTransit |
|----------|---------:|------------:|
| Single command | 46.25 ns / 96 B | 17,118.48 ns / 22,080 B |
| Event to 2 handlers | 112.10 ns / 96 B | 32,799.39 ns / 39,377 B |
| 10 concurrent commands | 604.90 ns / 1,360 B | 187,233.96 ns / 219,151 B |
| 100 concurrent commands | 5,777.73 ns / 12,160 B | 1,522,021.74 ns / 2,185,202 B |
| Batch send (10) | 512.30 ns / 960 B | 160,922.30 ns / 219,296 B |

### Track B: Queued/Bus Semantics

Source: `TransportQueueParityWarmPathComparisonBenchmarks-report-github.md`

| Scenario | Dispatch (remote route) | Wolverine | MassTransit |
|----------|------------------------:|----------:|------------:|
| Queued command end-to-end | 1.361 μs / 793 B | 4.050 μs / 4,400 B | 16.457 μs / 22,086 B |
| Queued event fan-out end-to-end | 1.420 μs / 794 B | 3.983 μs / 4,400 B | 31.624 μs / 39,416 B |
| Queued commands end-to-end (10 concurrent) | 7.072 μs / 5,118 B | 40.701 μs / 44,489 B | 161.395 μs / 219,091 B |

## Routing-First Parity Snapshot

Source: `RoutingFirstParityWarmPathBenchmarks-report-github.md` — 42 rows exercising routing-only
overhead across local, remote and provider-profile paths. Pre-routed local command 94.34 ns / 280 B;
remote event rows 167-173 ns / 304 B. **The remote rows cost 72 B more than they did in April and
the difference is unexplained** — three candidate causes have been ruled out, which is not the same
as an explanation. See `docs/performance.md` for the full table and the eliminations.

## Pipeline Parity Snapshot

Source: `PipelineWarmPathComparisonBenchmarks-report-github.md`

| Scenario | Dispatch | MediatR | Wolverine | MassTransit |
|----------|---------:|--------:|----------:|------------:|
| 3 middleware / behaviors | 71.68 ns / 240 B | 124.87 ns / 680 B | 236.34 ns / 680 B | 2,128.02 ns / 4,568 B |
| 10 concurrent + 3 behaviors | 888.19 ns / 2,112 B | 1,314.09 ns / 7,168 B | 2,432.01 ns / 7,008 B | 21,023.12 ns / 45,888 B |

## Under Investigation

- **Routing-first remote allocation.** Every pre-routed remote row allocates 72 B more than it did
  in the April epoch, with a matching latency increase that this epoch partly recovers. Three
  candidate causes have been excluded (an ambient tenant context, a middleware-selection change for
  unclassified messages, and the ambient message-context publication — the last by direct
  experiment). No explanation yet; the numbers are published as measured.
- **MediatR's own query row** moved about 21% between epochs, consistently across all three runs of
  this one. Nothing in this framework touches it. Until it is explained, the query **comparison**
  is not published in either direction.

## Methodology + runbook

- **Regression thresholds + run procedure:** see `benchmarks/RUNBOOK.md`
- **Reporting conventions:** see `docs/performance/competitor-benchmarks.md`
- **Canonical runner script gap:** `eng/run-comparative-benchmarks.ps1` is missing `RoutingFirstParityBenchmarks` in its filter — tracked for fix in a future sprint
