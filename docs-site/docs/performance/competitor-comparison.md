---
sidebar_position: 4
title: Competitor Comparison
description: Performance comparison of Excalibur.Dispatch vs MediatR, Wolverine, and MassTransit
---

# Competitor Comparison

This page documents comparative benchmark baselines for **Excalibur.Dispatch** using three explicit tracks:

1. **In-process parity** (raw handler-dispatch, no middleware)
2. **Pipeline parity** (3 passthrough middleware/behaviors per framework)
3. **Queued/bus semantics** (publish/send + consumer flow)

## Baseline Artifacts

- Baseline folder: `benchmarks/baselines/net10.0/dispatch-comparative-20260302/results/`
- Previous baseline: `benchmarks/baselines/net10.0/dispatch-comparative-20260227-optimized/results/`
- Runtime: `.NET 10.0.103`
- Tooling: `BenchmarkDotNet v0.15.4`
- Machine: `DESKTOP-5OUKF4A`, `Windows 10.0.26200` (32 logical processors)

## Latest Validation Run (Mar 2, 2026)

Validation command:

```powershell
pwsh eng/run-comparative-benchmarks.ps1 -NoBuild -NoRestore -RuntimeProfile ci
```

Validation outcome:

- Commit: `26a40b85413183114f73b4fd1e4d979899303461`
- Runtime: `.NET 10.0.3`
- OS: `Windows 11 (10.0.26200.7840)`
- Classes: `7`
- Total benchmark rows: `78`
- Duration: `00:29:10.72`
- Failures: `0`
- Summary:
  - `benchmarks/baselines/net10.0/dispatch-comparative-20260302/results/Excalibur.Dispatch.Benchmarks.Comparative.MediatRComparisonBenchmarks-report-github.md`
  - `benchmarks/baselines/net10.0/dispatch-comparative-20260302/results/Excalibur.Dispatch.Benchmarks.Comparative.WolverineInProcessComparisonBenchmarks-report-github.md`
  - `benchmarks/baselines/net10.0/dispatch-comparative-20260302/results/Excalibur.Dispatch.Benchmarks.Comparative.WolverineComparisonBenchmarks-report-github.md`
  - `benchmarks/baselines/net10.0/dispatch-comparative-20260302/results/Excalibur.Dispatch.Benchmarks.Comparative.MassTransitMediatorComparisonBenchmarks-report-github.md`
  - `benchmarks/baselines/net10.0/dispatch-comparative-20260302/results/Excalibur.Dispatch.Benchmarks.Comparative.MassTransitComparisonBenchmarks-report-github.md`
  - `benchmarks/baselines/net10.0/dispatch-comparative-20260302/results/Excalibur.Dispatch.Benchmarks.Comparative.PipelineComparisonBenchmarks-report-github.md`
  - `benchmarks/baselines/net10.0/dispatch-comparative-20260302/results/Excalibur.Dispatch.Benchmarks.Comparative.TransportQueueParityComparisonBenchmarks-report-github.md`

Validation class results:

| Class | Rows | Status |
|---|---:|---|
| `MediatRComparisonBenchmarks` | 16 | Pass |
| `WolverineInProcessComparisonBenchmarks` | 11 | Pass |
| `WolverineComparisonBenchmarks` | 14 | Pass |
| `MassTransitMediatorComparisonBenchmarks` | 10 | Pass |
| `MassTransitComparisonBenchmarks` | 10 | Pass |
| `PipelineComparisonBenchmarks` | 8 | Pass |
| `TransportQueueParityComparisonBenchmarks` | 9 | Pass |

:::note Published baseline
The numeric scenario tables below are sourced from the committed `dispatch-comparative-20260302` baseline.
:::

:::note Allocation normalization
Some committed `*-report-github.md` files in that baseline include anomalous allocation values (`0 B` or unusually high spikes) that do not match the paired `report.run1.*.csv` artifacts. For those affected rows, allocation values below are normalized to the stable run1 median for the same method.
:::

:::info Scope
These are microbenchmarks for framework overhead and path cost. They are not end-to-end production latency claims.
:::

:::info Methodology
All comparisons use lean `AddDispatch()` registration with no middleware enabled, matching each competitor's minimal configuration. A fresh `IMessageContext` is created and returned per iteration. Handler and pipeline caches are warmed up and frozen before measurement. See `docs/performance/competitor-benchmarks.md` for full methodology.
:::

## Executive Summary

| Track | Summary |
|------|---------|
| In-process parity (MediatR) | MediatR ~1.3-1.6x faster on standard command/query paths; Dispatch ultra-local/singleton paths ~1.1-1.5x faster |
| In-process parity (Wolverine InvokeAsync) | **Dispatch ~2.3-3.0x faster on command/query paths, ~18x on notifications** |
| In-process parity (MassTransit Mediator) | Dispatch ~12.3-55.7x faster |
| Pipeline parity (3 middleware each) | MediatR ~1.8-2.2x faster; Wolverine ~1.2-1.5x faster; Dispatch ~5.7-7.4x faster than MassTransit |
| Queued/bus end-to-end parity | Dispatch ~3.2-6.3x faster than Wolverine, ~12.3-21.2x faster than MassTransit |

## Track A: In-Process Parity

### Dispatch vs MediatR

Source: `benchmarks/baselines/net10.0/dispatch-comparative-20260302/results/Excalibur.Dispatch.Benchmarks.Comparative.MediatRComparisonBenchmarks-report-github.md`

| Scenario | Dispatch | MediatR | Relative Result |
|----------|----------|---------|-----------------|
| Single command handler | 75.32 ns / 240 B | 47.27 ns / 152 B | MediatR ~1.6x faster |
| Single command direct-local | 70.63 ns / 240 B | 47.27 ns / 152 B | MediatR ~1.5x faster |
| Single command ultra-local | 31.54 ns / 24 B | 47.27 ns / 152 B | **Dispatch ~1.5x faster**; Dispatch allocates ~6.3x less |
| Singleton-promoted command | 31.73 ns / 24 B | 47.27 ns / 152 B | **Dispatch ~1.5x faster**; Dispatch allocates ~6.3x less |
| Notification to 3 handlers | 118.65 ns / 240 B | 119.24 ns / 616 B | Near parity; Dispatch allocates ~2.6x less |
| Query with return value | 83.57 ns / 336 B | 62.38 ns / 296 B | MediatR ~1.3x faster |
| Query ultra-local | 58.27 ns / 165 B | 62.38 ns / 296 B | **Dispatch ~1.1x faster**; Dispatch allocates ~1.8x less |
| Query singleton-promoted | 58.23 ns / 192 B | 62.38 ns / 296 B | **Dispatch ~1.1x faster** |
| 10 concurrent commands | 879.24 ns / 2,080 B | 544.39 ns / 1,856 B | MediatR ~1.6x faster |
| 100 concurrent commands | 7,539.10 ns / 19,360 B | 5,160.23 ns / 17,064 B | MediatR ~1.5x faster |

### Dispatch vs Wolverine (InvokeAsync parity)

Source: `benchmarks/baselines/net10.0/dispatch-comparative-20260302/results/Excalibur.Dispatch.Benchmarks.Comparative.WolverineInProcessComparisonBenchmarks-report-github.md`

| Scenario | Dispatch | Wolverine (InvokeAsync) | Relative Result |
|----------|----------|--------------------------|-----------------|
| Single command (local) | 132.26 ns / 264 B | 368.19 ns / 672 B | **Dispatch 2.8x faster** |
| Single command (ultra-local) | 61.27 ns / 48 B | 368.19 ns / 672 B | **Dispatch 6.0x faster** |
| Notification to 2 handlers | 219.40 ns / 288 B | 3,954.40 ns / 4,512 B | **Dispatch 18.0x faster** |
| Query with return | 96.88 ns / 480 B | 289.44 ns / 936 B | **Dispatch 3.0x faster** |
| 10 concurrent commands | 940.32 ns / 2,320 B | 2,192.44 ns / 6,928 B | **Dispatch 2.3x faster** |
| 100 concurrent commands | 8,249.13 ns / 21,760 B | 22,060.96 ns / 68,128 B | **Dispatch 2.7x faster** |

### Dispatch vs Wolverine (Full: InvokeAsync + SendAsync)

Source: `benchmarks/baselines/net10.0/dispatch-comparative-20260302/results/Excalibur.Dispatch.Benchmarks.Comparative.WolverineComparisonBenchmarks-report-github.md`

| Scenario | Dispatch | Wolverine InvokeAsync | Wolverine SendAsync | Relative Result |
|----------|----------|----------------------|---------------------|-----------------|
| Single command | 80.86 ns / 264 B | 214.48 ns / 688 B | 3,900.31 ns / 4,512 B | Dispatch 2.7x faster (invoke), 48.2x faster (send) |
| Single command (ultra-local) | 39.43 ns / 48 B | 214.48 ns / 688 B | - | Dispatch 5.4x faster (invoke) |
| Event to 2 handlers | 119.37 ns / 288 B | - | 7,849.49 ns / 4,512 B | Dispatch 65.8x faster (publish) |
| Query with return | 171.76 ns / 456 B | 482.56 ns / 936 B | - | Dispatch 2.8x faster |
| 10 concurrent commands | 1,468.21 ns / 2,320 B | 3,833.38 ns / 7,088 B | - | Dispatch 2.6x faster |
| 100 concurrent commands | 13,780.10 ns / 21,760 B | 37,536.13 ns / 69,729 B | - | Dispatch 2.7x faster |
| Batch queries (10) | 1,853.82 ns / 3,880 B | 4,844.69 ns / 8,312 B | - | Dispatch 2.6x faster |

### Dispatch vs MassTransit Mediator (in-process)

Source: `benchmarks/baselines/net10.0/dispatch-comparative-20260302/results/Excalibur.Dispatch.Benchmarks.Comparative.MassTransitMediatorComparisonBenchmarks-report-github.md`

| Scenario | Dispatch | MassTransit Mediator | Relative Result |
|----------|----------|----------------------|-----------------|
| Single command | 178.2 ns / 352 B | 4,120.8 ns / 3,544 B | Dispatch ~23.1x faster |
| Notification to 2 consumers | 261.5 ns / 376 B | 5,742.8 ns / 4,176 B | Dispatch ~22.0x faster |
| Query with return | 117.7 ns / 544 B | 6,553.7 ns / 11,600 B | Dispatch ~55.7x faster |
| 10 concurrent commands | 1,196.9 ns / 3,200 B | 14,750.7 ns / 35,648 B | Dispatch ~12.3x faster |
| 100 concurrent commands | 10,905.2 ns / 30,560 B | 147,353.3 ns / 355,330 B | Dispatch ~13.5x faster |

### Dispatch vs MassTransit (full bus)

Source: `benchmarks/baselines/net10.0/dispatch-comparative-20260302/results/Excalibur.Dispatch.Benchmarks.Comparative.MassTransitComparisonBenchmarks-report-github.md`

| Scenario | Dispatch | MassTransit | Relative Result |
|----------|----------|-------------|-----------------|
| Single command | 74.12 ns / 264 B | 22,615.67 ns / 22,190 B | Dispatch ~305.1x faster |
| Event to 2 handlers | 114.71 ns / 1,844 B | 26,851.31 ns / 39,536 B | Dispatch ~234.1x faster |
| 10 concurrent commands | 880.29 ns / - | 294,094.51 ns / 219,695 B | Dispatch ~334.1x faster |
| 100 concurrent commands | 13,405.73 ns / 21,760 B | 2,513,199.57 ns / 2,191,146 B | Dispatch ~187.5x faster |
| Batch send (10) | 1,143.54 ns / 1,920 B | 291,198.10 ns / 219,922 B | Dispatch ~254.6x faster |

## Track B: Pipeline Parity (3 Middleware Each)

Source: `benchmarks/baselines/net10.0/dispatch-comparative-20260302/results/Excalibur.Dispatch.Benchmarks.Comparative.PipelineComparisonBenchmarks-report-github.md`

Each framework configured with 3 passthrough middleware/behaviors that mirror each other:
- **Dispatch**: 3 `IDispatchMiddleware` (logging, validation, timing)
- **MediatR**: 3 `IPipelineBehavior<T, Unit>` (logging, validation, timing)
- **Wolverine**: 3 convention-based middleware with `BeforeAsync`/`AfterAsync`
- **MassTransit**: 3 `IFilter<ConsumeContext<T>>` (logging, validation, timing)

| Scenario | Dispatch | MediatR | Wolverine | MassTransit | Relative Result |
|----------|----------|---------|-----------|-------------|-----------------|
| 3 middleware (single) | 316.7 ns / 536 B | 178.0 ns / 744 B | 255.7 ns / 768 B | 2,347.6 ns / 4,568 B | MediatR 1.8x faster; Wolverine 1.2x faster; Dispatch 7.4x faster than MT |
| 10 concurrent + 3 middleware | 4,096.1 ns / 5,072 B | 1,853.9 ns / 7,808 B | 2,652.0 ns / 7,888 B | 23,432.5 ns / 45,888 B | MediatR 2.2x faster; Wolverine 1.5x faster; Dispatch 5.7x faster than MT |

## Track C: Queued/Bus End-to-End Parity

### Dispatch vs Wolverine vs MassTransit (queued end-to-end parity)

Source: `benchmarks/baselines/net10.0/dispatch-comparative-20260302/results/Excalibur.Dispatch.Benchmarks.Comparative.TransportQueueParityComparisonBenchmarks-report-github.md`

| Scenario | Dispatch (remote route) | Wolverine | MassTransit | Relative Result |
|----------|--------------------------|-----------|-------------|-----------------|
| Queued command end-to-end | 1.147 us / 852 B | 4.305 us / 4,512 B | 14.141 us / 22,197 B | Dispatch ~3.8x faster than Wolverine, ~12.3x faster than MassTransit |
| Queued event fan-out end-to-end | 1.241 us / 822 B | 3.949 us / 4,512 B | 26.065 us / 39,544 B | Dispatch ~3.2x faster than Wolverine, ~21.0x faster than MassTransit |
| Queued commands end-to-end (10 concurrent) | 6.249 us / 5,675 B | 39.326 us / 45,609 B | 132.652 us / 219,734 B | Dispatch ~6.3x faster than Wolverine, ~21.2x faster than MassTransit |

:::warning Interpretation Guardrail
Use Track A for closest in-process handler overhead parity. Use Track B when comparing middleware/pipeline cost across frameworks. Use Track C when comparing queued/bus completion semantics.
:::

## Routing-First Local + Hybrid Parity

Source: `benchmarks/baselines/net10.0/dispatch-comparative-20260302/results/Excalibur.Dispatch.Benchmarks.Comparative.RoutingFirstParityBenchmarks-report-github.md`

| Scenario | Mean | Relative to local command |
|----------|------|---------------------------|
| Dispatch: pre-routed local command | 78.17 ns | baseline |
| Dispatch: pre-routed local query | 93.86 ns | +20.1% |
| Dispatch: pre-routed remote event (AWS SQS) | 157.17 ns | +101.1% |
| Dispatch: pre-routed remote event (Azure Service Bus) | 167.66 ns | +114.5% |
| Dispatch: pre-routed remote event (Kafka) | 163.22 ns | +108.8% |
| Dispatch: pre-routed remote event (RabbitMQ) | 159.09 ns | +103.5% |
| Dispatch: pre-routed Kafka observability profile | 292.44 ns | +274.1% |
| Dispatch: pre-routed RabbitMQ observability profile | 287.99 ns | +268.4% |

## Running These Comparisons

```bash
# Build once
dotnet build benchmarks/Excalibur.Dispatch.Benchmarks/Excalibur.Dispatch.Benchmarks.csproj -c Release --nologo -v minimal

# All competitor benchmarks
pwsh ./eng/run-comparative-benchmarks.ps1 -RuntimeProfile ci

# Track A (in-process parity)
pwsh ./eng/run-benchmark-matrix.ps1 -NoBuild -NoRestore -Classes MediatRComparisonBenchmarks,WolverineInProcessComparisonBenchmarks,MassTransitMediatorComparisonBenchmarks

# Track B (pipeline parity)
pwsh ./eng/run-benchmark-matrix.ps1 -NoBuild -NoRestore -Classes PipelineComparisonBenchmarks

# Track C (queued/bus end-to-end parity)
pwsh ./eng/run-benchmark-matrix.ps1 -NoBuild -NoRestore -Classes TransportQueueParityComparisonBenchmarks
```

Results are written to `benchmarks/runs/BenchmarkDotNet.Artifacts/results/` unless `-ArtifactsPath` is provided.
