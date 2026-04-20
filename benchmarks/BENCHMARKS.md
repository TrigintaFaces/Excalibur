# Excalibur Benchmark Baseline (Latest Sync)

This file summarizes the current committed comparative baselines used by docs.

## Run Metadata

- Date: **April 20, 2026** (20260420 epoch)
- Runtime: .NET 10.0.6
- SDK: 10.0.202
- Tooling: BenchmarkDotNet v0.15.8
- Baseline folder: `benchmarks/baselines/net10.0/dispatch-comparative-20260420/results/`
- Configs captured: `ComparativeBenchmarkConfig` (μs-scale, literal `InvocationCount=1`) + `WarmPathBenchmarkConfig` (ns-scale, auto-tuned)
- Prior baseline (superseded, preserved on disk): `benchmarks/baselines/net10.0/dispatch-comparative-20260302/` — **not cross-diffable** with 20260420 due to BDN 0.15.4 → 0.15.8 InvocationCount semantic shift

## Comparative Snapshot

### Track A: In-Process Parity

#### Dispatch vs MediatR

Source: `Excalibur.Dispatch.Benchmarks.Comparative.MediatRComparisonBenchmarks-report-github.md` (μs scale)

| Scenario | Dispatch | MediatR |
|----------|---------:|--------:|
| Single command handler | 8.76 μs | 14.72 μs |
| Single command ultra-local API | 9.78 μs | 14.72 μs |
| Notification to 3 handlers | 12.44 μs | 9.87 μs |
| Query with return value | 11.81 μs | 13.04 μs |
| Query ultra-local API | 9.08 μs | 13.04 μs |
| 10 concurrent commands | 13.59 μs | 23.64 μs |
| 100 concurrent commands | 25.59 μs | 55.19 μs |

WarmPath companion (ns-scale, `MediatRWarmPathComparisonBenchmarks`):

| Scenario | Dispatch | MediatR |
|----------|---------:|--------:|
| Single command handler | 70.87 ns / 240 B | 44.20 ns / 152 B |
| Single command ultra-local API | 34.56 ns / 24 B | 44.20 ns / 152 B |
| Notification to 3 handlers | 117.36 ns / 240 B | 94.47 ns / 616 B |
| Query with return value | 76.61 ns / 336 B | 51.81 ns / 296 B |
| Query ultra-local API | 56.63 ns / 192 B | 51.81 ns / 296 B |

#### Dispatch vs Wolverine (Invoke/local in-process)

Source: `WolverineInProcessWarmPathComparisonBenchmarks-report-github.md` (ns scale)

| Scenario | Dispatch | Wolverine |
|----------|---------:|----------:|
| Single command | 74.83 ns / 264 B | 197.75 ns / 672 B |
| Notification to 2 handlers | 120.28 ns / 288 B | 6,455.11 ns / 5,640 B |
| Query with return | 89.45 ns / 456 B | 267.92 ns / 936 B |
| 10 concurrent commands | 942.99 ns / 2,320 B | 2,129.25 ns / 6,928 B |
| 100 concurrent commands | 8,173.28 ns / 21,760 B | 21,169.25 ns / 68,128 B |

#### Dispatch vs MassTransit Mediator (in-process)

Source: `MassTransitMediatorComparisonBenchmarks-report-github.md` (μs scale)

| Scenario | Dispatch | MassTransit Mediator |
|----------|---------:|---------------------:|
| Single command | 12.68 μs | 95.31 μs |
| Notification to 2 consumers | 16.87 μs | 88.28 μs |
| Query with return | 12.80 μs | 278.10 μs |
| 10 concurrent commands | 16.08 μs | 133.57 μs |
| 100 concurrent commands | 25.67 μs | 557.91 μs |

### Track B: Queued/Bus Semantics

#### Dispatch vs Wolverine vs MassTransit (end-to-end queued parity)

Source: `Excalibur.Dispatch.Benchmarks.Comparative.TransportQueueParityComparisonBenchmarks-report-github.md`

| Scenario | Dispatch (remote route) | Wolverine | MassTransit |
|----------|------------------------:|----------:|------------:|
| Queued command end-to-end | 64.00 μs | 144.63 μs | 295.00 μs |
| Queued event fan-out end-to-end | 72.49 μs | 113.03 μs | 342.12 μs |
| Queued commands end-to-end (10 concurrent) | 80.42 μs | 238.42 μs | 774.03 μs |

## Routing-First Parity Snapshot

Source: `Excalibur.Dispatch.Benchmarks.Comparative.RoutingFirstParityBenchmarks-report-github.md` — 9 rows exercising routing-only overhead across in-process, queued, and fan-out paths. See report file directly for full table.

## Pipeline Parity Snapshot

Source: `Excalibur.Dispatch.Benchmarks.Comparative.PipelineComparisonBenchmarks-report-github.md` — 3-middleware-layer overhead comparison across Dispatch / MediatR / Wolverine / MassTransit. See report file directly for full table.

## Under Investigation

One WarmPath row flagged for methodology-matched rerun before making win/loss claims:
- `Dispatch: 100 concurrent commands` allocation vs MediatR (`MediatRWarmPathComparisonBenchmarks`). The 20260420 WarmPath allocation differs from the 2026-04-13 `dispatch-all/` snapshot by more than the noise floor, but the prior snapshot used BDN 0.15.4 so the delta is confounded by the harness-semantic shift. A methodology-matched rerun is queued for a future sprint.

## Methodology + runbook

- **Regression thresholds + run procedure:** see `benchmarks/RUNBOOK.md`
- **Reporting conventions:** see `docs/performance/competitor-benchmarks.md`
- **Canonical runner script gap:** `eng/run-comparative-benchmarks.ps1` is missing `RoutingFirstParityBenchmarks` in its filter — tracked for fix in a future sprint
