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
| In-process parity (MediatR) | MediatR ~1.4x faster on standard; **Dispatch ultra-local 1.3x faster with 6.3x less memory**; **Dispatch allocates 2.6x less on notifications** |
| In-process parity (Wolverine InvokeAsync) | **Dispatch ~2.6x faster on command, ~61x on notifications** |
| Pipeline parity (3 middleware each) | MediatR 2.7x faster; Wolverine 1.2x faster; **Dispatch 6.8x faster than MassTransit**; **Dispatch allocates least** |

:::note April 2026 Performance Update
Ultra-local dispatch remains the standout path: **33 ns / 24 B** -- 1.3x faster than MediatR with 6.3x less memory. LightMode opt-in disables correlation ID generation for workloads that don't need it. Hot-path breakdown: handler activation 24.4 ns / 0 B, handler invocation 6.0 ns / 0 B — all zero-allocation internals. See `benchmarks/experiments/` for optimization experiment details.
:::

## Track A: In-Process Parity

### Dispatch vs MediatR

| Scenario | Dispatch | MediatR | Relative Result |
|----------|----------|---------|-----------------|
| Single command handler | 62.76 ns / 240 B | 43.96 ns / 152 B | MediatR ~1.4x faster |
| Single command direct-local | 62.39 ns / 240 B | 43.96 ns / 152 B | MediatR ~1.4x faster |
| Single command ultra-local | 33.30 ns / 24 B | 43.96 ns / 152 B | **Dispatch ~1.3x faster**; Dispatch allocates ~6.3x less |
| Singleton-promoted command | 33.01 ns / 24 B | 43.96 ns / 152 B | **Dispatch ~1.3x faster**; Dispatch allocates ~6.3x less |
| Notification to 3 handlers | 115.33 ns / 240 B | 95.40 ns / 616 B | MediatR ~1.2x faster; **Dispatch allocates ~2.6x less** |
| Query with return value | 72.94 ns / 336 B | 52.18 ns / 296 B | MediatR ~1.4x faster |
| Query with return (typed API) | 80.49 ns / 432 B | 52.18 ns / 296 B | MediatR ~1.5x faster |
| Query ultra-local | 56.09 ns / 192 B | 52.18 ns / 296 B | Near parity; Dispatch allocates ~1.5x less |
| Query singleton-promoted | 56.05 ns / 192 B | 52.18 ns / 296 B | Near parity; Dispatch allocates ~1.5x less |
| 10 concurrent commands | 772 ns / 2,080 B | 531 ns / 1,856 B | MediatR ~1.5x faster |
| 100 concurrent commands | 6,696 ns / 19,360 B | 4,994 ns / 17,064 B | MediatR ~1.3x faster |

### Dispatch vs Wolverine (InvokeAsync parity)

| Scenario | Dispatch | Wolverine (InvokeAsync) | Relative Result |
|----------|----------|--------------------------|-----------------|
| Single command (local) | 70.35 ns / 264 B | 183.56 ns / 672 B | **Dispatch 2.6x faster** |
| Single command (ultra-local) | 39.43 ns / 48 B | 183.56 ns / 672 B | **Dispatch 4.7x faster** |
| Notification to 2 handlers | 116.38 ns / 288 B | 7,128 ns / 5,640 B | **Dispatch 61.3x faster** |
| Query with return | 74.15 ns / 456 B | 258.00 ns / 936 B | **Dispatch 3.5x faster** |
| 10 concurrent commands | 828.62 ns / 2,320 B | 1,994 ns / 6,928 B | **Dispatch 2.4x faster** |
| 100 concurrent commands | 6,474 ns / 21,760 B | 17,391 ns / 68,128 B | **Dispatch 2.7x faster** |

## Track B: Pipeline Parity (3 Middleware Each)

Each framework configured with 3 passthrough middleware/behaviors that mirror each other:
- **Dispatch**: 3 `IDispatchMiddleware` (logging, validation, timing)
- **MediatR**: 3 `IPipelineBehavior<T, Unit>` (logging, validation, timing)
- **Wolverine**: 3 convention-based middleware with `BeforeAsync`/`AfterAsync`
- **MassTransit**: 3 `IFilter<ConsumeContext<T>>` (logging, validation, timing)

| Scenario | Dispatch | MediatR | Wolverine | MassTransit | Relative Result |
|----------|----------|---------|-----------|-------------|-----------------|
| 3 middleware (single) | 280.3 ns / 392 B | 105.2 ns / 680 B | 228.3 ns / 768 B | 1,892.8 ns / 4,568 B | MediatR 2.7x faster; Wolverine 1.2x faster; **Dispatch 6.8x faster than MT**; **Dispatch allocates 1.7x less than MediatR** |
| 10 concurrent + 3 middleware | 2,868 ns / 3,632 B | 1,108 ns / 7,168 B | 2,223 ns / 7,888 B | 19,290 ns / 45,888 B | MediatR 2.6x faster; Wolverine 1.3x faster; **Dispatch 6.7x faster than MT**; **Dispatch allocates 2x less than MediatR** |

## Track C: Queued/Bus End-to-End Parity

:::note Track C methodology
Track C benchmarks use `InvocationCount=1`, `UnrollFactor=1`, `IterationCount=3` with `InProcessEmitToolchain`. Error margins are higher with fewer iterations; treat relative ratios as directional rather than precise. Run `*TransportQueueParityWarmPathComparisonBenchmarks*` to regenerate.
:::

:::warning Interpretation Guardrail
Use Track A for closest in-process handler overhead parity. Use Track B when comparing middleware/pipeline cost across frameworks. Use Track C when comparing queued/bus completion semantics.
:::

## Allocation Profiles

Excalibur.Dispatch offers multiple dispatch paths with different allocation characteristics.

| Profile | Allocation | Latency | When to Use |
|---------|-----------|---------|-------------|
| Standard dispatch | **240 B** | ~63 ns | Default path for all message types (April 2026 WarmPath) |
| Ultra-local dispatch | **24 B** | ~33 ns | Lowest-overhead local path, near-zero allocation |
| Singleton-promoted | **24 B** | ~33 ns | Handlers registered as singletons via promotion |
| Query with response | **336 B** | ~73 ns | Typed query responses |
| Query ultra-local | **192 B** | ~56 ns | Ultra-local query path |
| MessageContext pool rent+return | **0 B** | ~9 ns | Pool infrastructure cost only |

:::tip Allocation Guidance
- **"Near-zero allocation"**: Ultra-local and singleton-promoted paths (24 B per dispatch)
- **"Low-allocation"**: Standard path (240 B -- context + routing metadata + ambient context flow)
- **"Zero-allocation internals"**: Handler activation (24.4 ns / 0 B), invocation (6.0 ns / 0 B)
:::

## Routing-First Local + Hybrid Parity

:::note
Routing-first numbers below are from the March 2026 baseline. These paths were not affected by the April 2026 dispatch hot-path optimizations since routing occurs before the dispatch fast path.
:::

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
