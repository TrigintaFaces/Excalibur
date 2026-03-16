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
Runtime: .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3
```

Results: `benchmarks/runs/BenchmarkDotNet.Artifacts/results/`

:::info Scope
These are microbenchmarks for framework overhead and path cost. They are not end-to-end production latency claims.
:::

:::info Methodology
All comparisons use lean `AddDispatch()` registration with no middleware enabled, matching each competitor's minimal configuration. A fresh `IMessageContext` is created and returned per iteration. Handler and pipeline caches are warmed up and frozen before measurement.
:::

## Executive Summary

| Track | Summary |
|------|---------|
| In-process parity (MediatR) | MediatR ~1.4x faster on standard command/query; Dispatch ultra-local ~1.5x faster; near parity on 10 concurrent |
| In-process parity (Wolverine InvokeAsync) | **Dispatch ~2.8x faster on command, ~54x on notifications** |
| In-process parity (MassTransit Mediator) | Dispatch ~13.2-72.5x faster |
| In-process parity (MassTransit full bus) | **Dispatch ~390x faster on single command** |
| Pipeline parity (3 middleware each) | MediatR ~2.3x faster; Wolverine ~1.2x faster; Dispatch ~7.1x faster than MassTransit |
| Queued/bus end-to-end parity | **Dispatch ~44.60 μs, ~3.1x faster than Wolverine on single command**; Wolverine ~138.58 μs; MassTransit ~401.93 μs |

## Track A: In-Process Parity

### Dispatch vs MediatR

| Scenario | Dispatch | MediatR | Relative Result |
|----------|----------|---------|-----------------|
| Single command handler | 63.56 ns / 240 B | 44.69 ns / 152 B | MediatR ~1.4x faster |
| Single command direct-local | 61.88 ns / 240 B | 44.69 ns / 152 B | MediatR ~1.4x faster |
| Single command ultra-local | 30.32 ns / 24 B | 44.69 ns / 152 B | **Dispatch ~1.5x faster**; Dispatch allocates ~6.3x less |
| Singleton-promoted command | 30.12 ns / 24 B | 44.69 ns / 152 B | **Dispatch ~1.5x faster**; Dispatch allocates ~6.3x less |
| Notification to 3 handlers | 115.03 ns / 240 B | 93.22 ns / 616 B | MediatR ~1.2x faster; Dispatch allocates ~2.6x less |
| Query with return value | 73.17 ns / 336 B | 51.65 ns / 296 B | MediatR ~1.4x faster |
| Query with return (typed API) | 83.75 ns / 432 B | 51.65 ns / 296 B | MediatR ~1.6x faster |
| Query ultra-local | 51.73 ns / 192 B | 51.65 ns / 296 B | Near parity; Dispatch allocates ~1.5x less |
| Query singleton-promoted | 51.83 ns / 192 B | 51.65 ns / 296 B | Near parity; Dispatch allocates ~1.5x less |
| 10 concurrent commands | 812.15 ns / 2,080 B | 526.97 ns / 1,856 B | MediatR ~1.5x faster |
| 100 concurrent commands | 7,090.23 ns / 19,360 B | 4,998.64 ns / 17,064 B | MediatR ~1.4x faster |

### Dispatch vs Wolverine (InvokeAsync parity)

| Scenario | Dispatch | Wolverine (InvokeAsync) | Relative Result |
|----------|----------|--------------------------|-----------------|
| Single command (local) | 72.02 ns / 264 B | 200.31 ns / 672 B | **Dispatch 2.8x faster** |
| Single command (ultra-local) | 35.43 ns / 48 B | 200.31 ns / 672 B | **Dispatch 5.7x faster** |
| Notification to 2 handlers | 123.94 ns / 288 B | 6,681.31 ns / 5,640 B | **Dispatch 53.9x faster** |
| Query with return | 88.81 ns / 456 B | 266.30 ns / 936 B | **Dispatch 3.0x faster** |
| 10 concurrent commands | 878.34 ns / 2,320 B | 2,076.72 ns / 6,928 B | **Dispatch 2.4x faster** |
| 100 concurrent commands | 7,702.68 ns / 21,760 B | 20,792.29 ns / 68,128 B | **Dispatch 2.7x faster** |

### Dispatch vs Wolverine (Full: InvokeAsync + SendAsync)

| Scenario | Dispatch | Wolverine InvokeAsync | Wolverine SendAsync | Relative Result |
|----------|----------|----------------------|---------------------|-----------------|
| Single command | 74.08 ns / 264 B | 201.39 ns / 688 B | 6,705.09 ns / 5,640 B | Dispatch 2.7x faster (invoke), 90.5x faster (send) |
| Single command (ultra-local) | 36.97 ns / 48 B | 201.39 ns / 688 B | - | Dispatch 5.4x faster (invoke) |
| Event to 2 handlers | 117.22 ns / 288 B | - | 6,916.18 ns / 5,616 B | Dispatch 59.0x faster (publish) |
| Query with return | 97.12 ns / 456 B | 271.08 ns / 936 B | - | Dispatch 2.8x faster |
| 10 concurrent commands | 933.70 ns / 2,320 B | 2,110.38 ns / 7,088 B | - | Dispatch 2.3x faster |
| 100 concurrent commands | 8,217.77 ns / 21,760 B | 20,941.12 ns / 69,728 B | - | Dispatch 2.5x faster |
| Batch queries (10) | 1,112.62 ns / 3,880 B | 2,730.96 ns / 8,312 B | - | Dispatch 2.5x faster |

### Dispatch vs MassTransit Mediator (in-process)

| Scenario | Dispatch | MassTransit Mediator | Relative Result |
|----------|----------|----------------------|-----------------|
| Single command | 96.59 ns / 352 B | 1,276.16 ns / 3,544 B | Dispatch ~13.2x faster |
| Notification to 2 consumers | 134.59 ns / 376 B | 1,842.16 ns / 4,176 B | Dispatch ~13.7x faster |
| Query with return | 121.00 ns / 544 B | 8,762.46 ns / 11,604 B | Dispatch ~72.4x faster |
| 10 concurrent commands | 1,127.01 ns / 3,200 B | 13,091.85 ns / 35,648 B | Dispatch ~11.6x faster |
| 100 concurrent commands | 10,226.61 ns / 30,560 B | 125,818.74 ns / 355,328 B | Dispatch ~12.3x faster |

### Dispatch vs MassTransit (full bus)

| Scenario | Dispatch | MassTransit | Relative Result |
|----------|----------|-------------|-----------------|
| Single command | 68.90 ns / 264 B | 26,922.64 ns / 22,068 B | Dispatch ~390.7x faster |
| Event to 2 handlers | 119.08 ns / 288 B | 29,594.03 ns / 39,389 B | Dispatch ~248.5x faster |
| 10 concurrent commands | 882.31 ns / 2,320 B | 162,091.56 ns / 219,133 B | Dispatch ~183.7x faster |
| 100 concurrent commands | 7,607.22 ns / 21,760 B | 1,421,228.47 ns / 2,184,535 B | Dispatch ~186.8x faster |
| Batch send (10) | 636.89 ns / 1,920 B | 162,830.77 ns / 219,308 B | Dispatch ~255.7x faster |

## Track B: Pipeline Parity (3 Middleware Each)

Each framework configured with 3 passthrough middleware/behaviors that mirror each other:
- **Dispatch**: 3 `IDispatchMiddleware` (logging, validation, timing)
- **MediatR**: 3 `IPipelineBehavior<T, Unit>` (logging, validation, timing)
- **Wolverine**: 3 convention-based middleware with `BeforeAsync`/`AfterAsync`
- **MassTransit**: 3 `IFilter<ConsumeContext<T>>` (logging, validation, timing)

| Scenario | Dispatch | MediatR | Wolverine | MassTransit | Relative Result |
|----------|----------|---------|-----------|-------------|-----------------|
| 3 middleware (single) | 305.9 ns / 392 B | 131.8 ns / 680 B | 251.2 ns / 768 B | 2,170.7 ns / 4,568 B | MediatR 2.3x faster; Wolverine 1.2x faster; Dispatch 7.1x faster than MT |
| 10 concurrent + 3 middleware | 3,148.3 ns / 3,632 B | 1,376.9 ns / 7,168 B | 2,528.5 ns / 7,888 B | 21,824.6 ns / 45,888 B | MediatR 2.3x faster; Wolverine 1.2x faster; Dispatch 6.9x faster than MT |

## Track C: Queued/Bus End-to-End Parity

### Dispatch vs Wolverine vs MassTransit (queued end-to-end parity)

| Scenario | Dispatch (remote) | Wolverine | MassTransit | Relative Result |
|----------|-------------------|-----------|-------------|-----------------|
| Queued command end-to-end | 44.60 μs / 1.47 KB | 138.58 μs / 12.59 KB | 401.93 μs / 29.15 KB | **Dispatch ~3.1x faster than Wolverine, ~9.0x faster than MassTransit** |
| Queued event fan-out end-to-end | 168.73 μs / 1.39 KB | 119.97 μs / 12.60 KB | 462.12 μs / 46.02 KB | Wolverine ~1.4x faster; **Dispatch ~2.7x faster than MassTransit** |
| Queued commands end-to-end (10 concurrent) | 181.33 μs / 16.11 KB | 406.08 μs / 62.96 KB | 1,282.10 μs / 224.70 KB | **Dispatch ~2.2x faster than Wolverine, ~7.1x faster than MassTransit** |

:::note Track C methodology
Track C benchmarks use `InvocationCount=1`, `UnrollFactor=1`, `IterationCount=3` with `InProcessEmitToolchain`. Error margins are higher with fewer iterations; treat relative ratios as directional rather than precise.
:::

:::warning Interpretation Guardrail
Use Track A for closest in-process handler overhead parity. Use Track B when comparing middleware/pipeline cost across frameworks. Use Track C when comparing queued/bus completion semantics.
:::

## Routing-First Local + Hybrid Parity

| Scenario | Mean | Allocated | Relative to local command |
|----------|------|-----------|---------------------------|
| Pre-routed local command | 78.95 ns | 232 B | baseline |
| Pre-routed local query | 86.24 ns | 424 B | +9.2% |
| Pre-routed remote event (AWS SQS) | 684.83 ns | 528 B | +767% |
| Pre-routed remote event (Azure Service Bus) | 674.26 ns | 528 B | +754% |
| Pre-routed remote event (AWS SNS) | 671.53 ns | 528 B | +751% |
| Pre-routed remote event (AWS EventBridge) | 695.90 ns | 528 B | +781% |
| Pre-routed remote event (Azure Event Hubs) | 687.83 ns | 528 B | +771% |
| Pre-routed remote event (gRPC) | 678.66 ns | 528 B | +760% |
| Pre-routed remote event (Kafka) | 684.31 ns | 528 B | +767% |
| Pre-routed remote event (RabbitMQ) | 681.04 ns | 528 B | +763% |

### Provider Profile Extensions

| Scenario | Mean | Allocated |
|----------|------|-----------|
| Kafka throughput profile | 740.35 ns | 576 B |
| Kafka retry profile | 721.32 ns | 576 B |
| Kafka poison profile | 712.18 ns | 552 B |
| Kafka observability profile | 726.19 ns | 576 B |
| RabbitMQ throughput profile | 710.56 ns | 576 B |
| RabbitMQ retry profile | 710.33 ns | 576 B |
| RabbitMQ poison profile | 732.15 ns | 552 B |
| RabbitMQ observability profile | 706.86 ns | 576 B |

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
