---
sidebar_position: 4
title: Competitor Comparison
description: Performance comparison of Excalibur.Dispatch vs MediatR, Wolverine, and MassTransit
---

# Competitor Comparison

This page documents comparative benchmarks for **Excalibur.Dispatch** using three explicit tracks:

1. **In-process parity** (raw handler-dispatch, no middleware)
2. **Pipeline parity** (3 passthrough middleware/behaviors per framework)
3. **Queued/bus semantics** (publish/send + consumer flow)

## Test Environment

```
BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7922)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.103
Runtime: .NET 10.0.3 (10.0.326.7603), X64 RyuJIT x86-64-v3
```

Results: `benchmarks/runs/BenchmarkDotNet.Artifacts/results/`

:::info Scope
These are microbenchmarks for framework overhead and path cost. They are not end-to-end production latency claims.
:::

:::info Methodology
All comparisons use lean `AddDispatch()` registration with no middleware enabled, matching each competitor's minimal configuration. A fresh `IMessageContext` is created and returned per iteration. Handler and pipeline caches are warmed up and frozen before measurement.
:::

:::tip Dual Benchmark Methodology
This project uses two benchmark configurations for different purposes:

- **WarmPath** (`WarmPathBenchmarkConfig`): BDN defaults with auto-calibrated InvocationCount and UnrollFactor. Measures steady-state throughput with warm JIT and caches. Used for published competitor comparisons (Tracks A, B above).
- **ColdPath** (`ComparativeBenchmarkConfig`): `InvocationCount=1`, `UnrollFactor=1`, `IterationCount=3`. Measures single-invocation correctness including framework setup overhead. Used for CI regression gates (Track C, performance gate checks).

WarmPath numbers reflect what users experience in production; ColdPath numbers catch regressions in framework initialization paths.
:::

## Executive Summary

| Track | Summary |
|------|---------|
| In-process parity (MediatR) | Near parity on standard command (~0.95x); **Dispatch ultra-local ~1.4x faster**; **Dispatch wins 100-concurrent** |
| In-process parity (Wolverine InvokeAsync) | **Dispatch ~3.7x faster on command, ~34x on notifications** |
| In-process parity (MassTransit Mediator) | **Dispatch ~26x-255x faster** |
| In-process parity (MassTransit full bus) | **Dispatch ~460x faster on single command** |
| Pipeline parity (3 middleware each) | MediatR ~2.4x faster; Dispatch ~= Wolverine; **Dispatch ~6.9x faster than MassTransit** |
| Queued/bus end-to-end parity | **Dispatch 1.16 μs, ~3.3x faster than Wolverine, ~19.9x faster than MassTransit** |

## Track A: In-Process Parity

### Dispatch vs MediatR

| Scenario | Dispatch | MediatR | Relative Result |
|----------|----------|---------|-----------------|
| Single command handler | 41.43 ns / 168 B | 43.79 ns / 152 B | **Dispatch ~1.05x faster** |
| Single command direct-local | 41.27 ns / 168 B | 43.79 ns / 152 B | **Dispatch ~1.06x faster** |
| Single command ultra-local | 31.81 ns / 24 B | 43.79 ns / 152 B | **Dispatch ~1.4x faster**; Dispatch allocates ~6.3x less |
| Singleton-promoted command | 32.01 ns / 24 B | 43.79 ns / 152 B | **Dispatch ~1.4x faster**; Dispatch allocates ~6.3x less |
| Notification to 3 handlers | 112.92 ns / 240 B | 94.37 ns / 616 B | MediatR ~1.2x faster; **Dispatch allocates ~2.6x less** |
| Query with return value | 52.89 ns / 264 B | 50.23 ns / 296 B | Near parity (~1.05x) |
| Query with return (typed API) | 54.93 ns / 360 B | 50.23 ns / 296 B | Near parity (~1.09x) |
| Query ultra-local | 53.36 ns / 192 B | 50.23 ns / 296 B | Near parity; Dispatch allocates ~1.5x less |
| Query singleton-promoted | 52.97 ns / 192 B | 50.23 ns / 296 B | Near parity; Dispatch allocates ~1.5x less |
| 10 concurrent commands | 562.00 ns / 1,360 B | 526.07 ns / 1,856 B | Near parity; **Dispatch allocates ~1.4x less** |
| 100 concurrent commands | 4,524 ns / 12,160 B | 5,135 ns / 17,064 B | **Dispatch ~1.13x faster**; **Dispatch allocates ~1.4x less** |

### Dispatch vs Wolverine (InvokeAsync parity)

| Scenario | Dispatch | Wolverine (InvokeAsync) | Relative Result |
|----------|----------|--------------------------|-----------------|
| Single command (local) | 50.55 ns / 192 B | 189.15 ns / 672 B | **Dispatch 3.7x faster** |
| Single command (ultra-local) | 40.57 ns / 48 B | 189.15 ns / 672 B | **Dispatch 4.7x faster** |
| Notification to 2 handlers | 115.79 ns / 288 B | 3,986.93 ns / 4,512 B | **Dispatch 34.4x faster** |
| Query with return | 63.26 ns / 384 B | 252.46 ns / 936 B | **Dispatch 4.0x faster** |
| 10 concurrent commands | 643.40 ns / 1,600 B | 2,050.55 ns / 6,928 B | **Dispatch 3.2x faster** |
| 100 concurrent commands | 5,422 ns / 14,560 B | 19,674 ns / 68,128 B | **Dispatch 3.6x faster** |

### Dispatch vs Wolverine (Full: InvokeAsync + SendAsync)

| Scenario | Dispatch | Wolverine InvokeAsync | Wolverine SendAsync | Relative Result |
|----------|----------|----------------------|---------------------|-----------------|
| Single command | 51.44 ns / 192 B | 342.08 ns / 688 B | 7,486.56 ns / 4,512 B | **Dispatch 6.7x faster (invoke), 145.6x faster (send)** |
| Single command (ultra-local) | 38.00 ns / 48 B | 342.08 ns / 688 B | - | **Dispatch 9.0x faster (invoke)** |
| Event to 2 handlers | 209.58 ns / 288 B | - | 7,544.87 ns / 4,512 B | **Dispatch 36.0x faster (publish)** |
| Query with return | 120.07 ns / 384 B | 450.98 ns / 936 B | - | **Dispatch 3.8x faster** |
| 10 concurrent commands | 1,024 ns / 1,600 B | 3,570 ns / 7,088 B | - | **Dispatch 3.5x faster** |
| 100 concurrent commands | 9,813 ns / 14,560 B | 36,062 ns / 69,728 B | - | **Dispatch 3.7x faster** |
| Batch queries (10) | 1,313 ns / 3,160 B | 4,526 ns / 8,312 B | - | **Dispatch 3.4x faster** |

### Dispatch vs MassTransit Mediator (in-process)

| Scenario | Dispatch | MassTransit Mediator | Relative Result |
|----------|----------|----------------------|-----------------|
| Single command | 48.04 ns / 192 B | 1,241.95 ns / 3,544 B | **Dispatch ~25.9x faster** |
| Notification to 2 consumers | 129.13 ns / 376 B | 1,685.68 ns / 4,176 B | **Dispatch ~13.1x faster** |
| Query with return | 60.67 ns / 384 B | 15,451 ns / 11,651 B | **Dispatch ~254.7x faster** |
| 10 concurrent commands | 638.12 ns / 1,600 B | 12,383 ns / 35,648 B | **Dispatch ~19.4x faster** |
| 100 concurrent commands | 5,317 ns / 14,560 B | 121,138 ns / 355,329 B | **Dispatch ~22.8x faster** |

### Dispatch vs MassTransit (full bus)

| Scenario | Dispatch | MassTransit | Relative Result |
|----------|----------|-------------|-----------------|
| Single command | 47.99 ns / 192 B | 22,056 ns / 22,126 B | **Dispatch ~460x faster** |
| Event to 2 handlers | 116.24 ns / 288 B | 24,034 ns / 39,408 B | **Dispatch ~206.8x faster** |
| 10 concurrent commands | 638.28 ns / 1,600 B | 127,921 ns / 219,086 B | **Dispatch ~200.4x faster** |
| 100 concurrent commands | 5,317 ns / 14,560 B | 1,138,993 ns / 2,185,767 B | **Dispatch ~214.3x faster** |
| Batch send (10) | 424.30 ns / 1,200 B | 130,188 ns / 219,323 B | **Dispatch ~306.9x faster** |

## Track B: Pipeline Parity (3 Middleware Each)

Each framework configured with 3 passthrough middleware/behaviors that mirror each other:
- **Dispatch**: 3 `IDispatchMiddleware` (logging, validation, timing)
- **MediatR**: 3 `IPipelineBehavior<T, Unit>` (logging, validation, timing)
- **Wolverine**: 3 convention-based middleware with `BeforeAsync`/`AfterAsync`
- **MassTransit**: 3 `IFilter<ConsumeContext<T>>` (logging, validation, timing)

| Scenario | Dispatch | MediatR | Wolverine | MassTransit | Relative Result |
|----------|----------|---------|-----------|-------------|-----------------|
| 3 middleware (single) | 285.4 ns / 392 B | 119.4 ns / 680 B | 236.6 ns / 768 B | 1,974.4 ns / 4,568 B | MediatR 2.4x faster; Wolverine 1.2x faster; **Dispatch 6.9x faster than MT** |
| 10 concurrent + 3 middleware | 2,998 ns / 3,632 B | 1,283 ns / 7,168 B | 2,430 ns / 7,888 B | 19,805 ns / 45,888 B | MediatR 2.3x faster; Dispatch ~= Wolverine; **Dispatch 6.6x faster than MT** |

## Track C: Queued/Bus End-to-End Parity

### Dispatch vs Wolverine vs MassTransit (queued end-to-end parity)

| Scenario | Dispatch (remote) | Wolverine | MassTransit | Relative Result |
|----------|-------------------|-----------|-------------|-----------------|
| Queued command end-to-end | 1.16 μs / 723 B | 3.77 μs / 4,512 B | 22.98 μs / 22,135 B | **Dispatch ~3.3x faster than Wolverine, ~19.9x faster than MassTransit** |
| Queued event fan-out end-to-end | 1.15 μs / 726 B | 3.74 μs / 4,512 B | 25.42 μs / 39,416 B | **Dispatch ~3.3x faster than Wolverine, ~22.1x faster than MassTransit** |
| Queued commands end-to-end (10 concurrent) | 6.35 μs / 4,395 B | 38.32 μs / 45,609 B | 131.69 μs / 219,092 B | **Dispatch ~6.0x faster than Wolverine, ~20.7x faster than MassTransit** |

:::note Track C methodology
Track C benchmarks use `InvocationCount=1`, `UnrollFactor=1`, `IterationCount=3` with `InProcessEmitToolchain`. Error margins are higher with fewer iterations; treat relative ratios as directional rather than precise.
:::

:::warning Interpretation Guardrail
Use Track A for closest in-process handler overhead parity. Use Track B when comparing middleware/pipeline cost across frameworks. Use Track C when comparing queued/bus completion semantics.
:::

## Routing-First Local + Hybrid Parity

| Scenario | Mean | Allocated | Relative to local command |
|----------|------|-----------|---------------------------|
| Pre-routed local command | 75.42 ns | 232 B | baseline |
| Pre-routed local query | 86.58 ns | 424 B | +14.8% |
| Pre-routed remote event (AWS SQS) | 134.53 ns | 232 B | +78.4% |
| Pre-routed remote event (Azure Service Bus) | 138.17 ns | 232 B | +83.2% |
| Pre-routed remote event (AWS SNS) | 133.72 ns | 232 B | +77.3% |
| Pre-routed remote event (AWS EventBridge) | 139.65 ns | 232 B | +85.2% |
| Pre-routed remote event (Azure Event Hubs) | 136.87 ns | 232 B | +81.5% |
| Pre-routed remote event (gRPC) | 128.99 ns | 232 B | +71.0% |
| Pre-routed remote event (Kafka) | 132.57 ns | 232 B | +75.8% |
| Pre-routed remote event (RabbitMQ) | 131.23 ns | 232 B | +74.0% |

### Provider Profile Extensions

| Scenario | Mean | Allocated |
|----------|------|-----------|
| Kafka throughput profile | 190.12 ns | 280 B |
| Kafka retry profile | 186.46 ns | 304 B |
| Kafka poison profile | 175.34 ns | 256 B |
| Kafka observability profile | 272.03 ns | 304 B |
| RabbitMQ throughput profile | 190.15 ns | 280 B |
| RabbitMQ retry profile | 186.29 ns | 304 B |
| RabbitMQ poison profile | 176.35 ns | 256 B |
| RabbitMQ observability profile | 268.35 ns | 304 B |

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

# WarmPath (published comparisons -- BDN defaults, auto-calibrated iterations)
dotnet run -c Release --project benchmarks/Excalibur.Dispatch.Benchmarks -- --filter *MediatRComparisonBenchmarks* --join --anyCategories WarmPath

# ColdPath / CI gates (single-invocation, used by CI performance gates)
dotnet run -c Release --project benchmarks/Excalibur.Dispatch.Benchmarks -- --filter *ComparisonBenchmarks* --join

# Track C (queued/bus end-to-end parity)
pwsh ./eng/run-benchmark-matrix.ps1 -NoBuild -NoRestore -Classes TransportQueueParityComparisonBenchmarks
```

Results are written to `benchmarks/runs/BenchmarkDotNet.Artifacts/results/` unless `-ArtifactsPath` is provided.
