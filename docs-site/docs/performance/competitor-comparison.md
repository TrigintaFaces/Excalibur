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
BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8117)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.202
Runtime: .NET 10.0.6 (10.0.626.17701), X64 RyuJIT x86-64-v3
```

**Current baseline:** `benchmarks/baselines/net10.0/dispatch-comparative-20260420/results/` (April 20, 2026 epoch).
**Prior baselines** (superseded, not cross-diffable due to BDN 0.15.4 → 0.15.8 InvocationCount semantic shift): `dispatch-comparative-20260302/`, `dispatch-all/` (2026-04-13). Ratios within each report remain apples-to-apples; **do not cross-diff individual Mean values** across epoch boundaries.

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
| In-process parity (MediatR) | MediatR ~1.6x faster on standard; **Dispatch ultra-local 1.28x faster with 6.3x less memory**; **Dispatch allocates 2.57x less on notifications** |
| In-process parity (Wolverine InvokeAsync) | **Dispatch ~2.64x faster on command, ~54x faster on notifications** |
| In-process parity (MassTransit Mediator) | **Dispatch leads on every tier** (7.5× faster on Single command, 21× faster on Query, 44× faster on 100 concurrent — see MassTransitMediatorComparisonBenchmarks) |
| Pipeline parity (3 middleware each) | See `PipelineComparisonBenchmarks` for current ratios (μs scale, 20260420 epoch) |

:::note April 20, 2026 Epoch

Ultra-local dispatch remains the standout path: **34.56 ns / 24 B** — 1.28x faster than MediatR with 6.3x less memory. Numbers below reflect the 20260420 WarmPath baseline (BDN 0.15.8 literal `InvocationCount=1`-compatible configuration). LightMode opt-in disables correlation ID generation for workloads that don't need it. Hot-path breakdown (from `DispatchHotPathBreakdownBenchmarks`, last refreshed 2026-04-13 — not in current epoch): handler activation 24.4 ns / 0 B, handler invocation 6.0 ns / 0 B — all zero-allocation internals. See `benchmarks/experiments/` for optimization experiment details.

One WarmPath row under investigation: `Dispatch: 100 concurrent commands` allocation vs MediatR — a methodology-matched rerun is queued for a future sprint. No claim is made on this tier until that rerun completes.
:::

## Track A: In-Process Parity

### Dispatch vs MediatR

Source: `MediatRWarmPathComparisonBenchmarks-report-github.md` (20260420 baseline, ns scale)

| Scenario | Dispatch | MediatR | Relative Result |
|----------|----------|---------|-----------------|
| Single command handler | 70.87 ns / 240 B | 44.20 ns / 152 B | MediatR ~1.60x faster |
| Single command direct-local | 71.40 ns / 240 B | 44.20 ns / 152 B | MediatR ~1.61x faster |
| Single command ultra-local | 34.56 ns / 24 B | 44.20 ns / 152 B | **Dispatch ~1.28x faster**; Dispatch allocates ~6.3x less |
| Singleton-promoted command | 33.67 ns / 24 B | 44.20 ns / 152 B | **Dispatch ~1.31x faster**; Dispatch allocates ~6.3x less |
| Notification to 3 handlers | 117.36 ns / 240 B | 94.47 ns / 616 B | MediatR ~1.24x faster; **Dispatch allocates ~2.57x less** |
| Query with return value | 76.61 ns / 336 B | 51.81 ns / 296 B | MediatR ~1.48x faster |
| Query with return (typed API) | 79.26 ns / 432 B | 51.81 ns / 296 B | MediatR ~1.53x faster |
| Query ultra-local | 56.63 ns / 192 B | 51.81 ns / 296 B | Near parity; Dispatch allocates ~1.54x less |
| Query singleton-promoted | 57.79 ns / 192 B | 51.81 ns / 296 B | Near parity; Dispatch allocates ~1.54x less |
| 10 concurrent commands | 826.80 ns / 2,080 B | 529.14 ns / 1,856 B | MediatR ~1.56x faster |
| 100 concurrent commands | 7,293.79 ns / 19,360 B | 5,014.96 ns / 17,064 B | MediatR ~1.45x faster (⚠ allocation under investigation) |

### Dispatch vs Wolverine (InvokeAsync parity)

Source: `WolverineInProcessWarmPathComparisonBenchmarks-report-github.md` (20260420 baseline, ns scale)

| Scenario | Dispatch | Wolverine (InvokeAsync) | Relative Result |
|----------|----------|--------------------------|-----------------|
| Single command (local) | 74.83 ns / 264 B | 197.75 ns / 672 B | **Dispatch 2.64x faster** |
| Single command (ultra-local) | 34.23 ns / 24 B | 197.75 ns / 672 B | **Dispatch 5.78x faster** |
| Notification to 2 handlers | 120.28 ns / 288 B | 6,455.11 ns / 5,640 B | **Dispatch 53.7x faster** |
| Query with return | 89.45 ns / 456 B | 267.92 ns / 936 B | **Dispatch 3.00x faster** |
| 10 concurrent commands | 942.99 ns / 2,320 B | 2,129.25 ns / 6,928 B | **Dispatch 2.26x faster** |
| 100 concurrent commands | 8,173.28 ns / 21,760 B | 21,169.25 ns / 68,128 B | **Dispatch 2.59x faster** |

## Track B: Pipeline Parity (3 Middleware Each)

Each framework configured with 3 passthrough middleware/behaviors that mirror each other:
- **Dispatch**: 3 `IDispatchMiddleware` (logging, validation, timing)
- **MediatR**: 3 `IPipelineBehavior<T, Unit>` (logging, validation, timing)
- **Wolverine**: 3 convention-based middleware with `BeforeAsync`/`AfterAsync`
- **MassTransit**: 3 `IFilter<ConsumeContext<T>>` (logging, validation, timing)

Source: `PipelineComparisonBenchmarks-report-github.md` (20260420 baseline, μs scale — literal `InvocationCount=1`). See the report directly for the full table; headline relative ordering preserved from prior baseline: Dispatch leads MassTransit significantly, trades with MediatR/Wolverine on absolute latency, leads both on pure allocation footprint for the standard pipeline.

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
| Standard dispatch | **240 B** | ~71 ns | Default path for all message types (April 20, 2026 WarmPath) |
| Ultra-local dispatch | **24 B** | ~35 ns | Lowest-overhead local path, near-zero allocation |
| Singleton-promoted | **24 B** | ~34 ns | Handlers registered as singletons via promotion |
| Query with response | **336 B** | ~77 ns | Typed query responses |
| Query ultra-local | **192 B** | ~57 ns | Ultra-local query path |
| MessageContext pool rent+return | **0 B** | ~9 ns | Pool infrastructure cost only (not refreshed in 20260420 — see `DispatchHotPathBreakdownBenchmarks` 2026-04-13) |

:::tip Allocation Guidance

- **"Near-zero allocation"**: Ultra-local and singleton-promoted paths (24 B per dispatch)
- **"Low-allocation"**: Standard path (240 B -- context + routing metadata + ambient context flow)
- **"Zero-allocation internals"**: Handler activation (24.4 ns / 0 B), invocation (6.0 ns / 0 B)
:::

## Routing-First Local + Hybrid Parity

:::note

Routing-first numbers below are from the April 20, 2026 baseline (`RoutingFirstParityBenchmarks-report-github.md`). These paths were not affected by recent dependency bumps since routing occurs before the dispatch fast path.
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
