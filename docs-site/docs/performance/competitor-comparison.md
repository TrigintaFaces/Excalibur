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
| In-process parity (MediatR) | MediatR ~1.6x faster on standard; **Dispatch ultra-local ~1.21x faster with 6.3x less memory**; **Dispatch allocates 2.57x less on notifications** |
| In-process parity (Wolverine InvokeAsync) | **Dispatch ~2.45x faster on command, ~3.00x on query, ~1.52x on notification fan-out** |
| In-process parity (MassTransit Mediator) | **Dispatch leads on every tier** — 13.4x on single command against MassTransit's ambient-scope mediator, 17.6x against its scope-per-message mediator |
| Pipeline parity (3 middleware each) | See `PipelineComparisonBenchmarks` for current ratios (μs scale, 20260420 epoch) |

:::note April 20, 2026 Epoch

Ultra-local dispatch remains the standout path: **34.0 ns / 24 B** — about 1.21x faster than MediatR with 6.3x less memory. Numbers below are from a full WarmPath run on an idle machine, with logging providers cleared on both sides of every comparison so neither framework's logging is inside the measured region.

:::caution Read the latency figures as indicative, the allocation figures as exact
Allocation is reproducible and was byte-identical across every run we have taken, including runs months apart and across a change of handler registration default. Latency is not: the same benchmark has varied 6-10% run to run for Dispatch and 15-22% for MediatR, which is several times BenchmarkDotNet's reported error — that error measures spread *within* one process, not reproducibility *between* processes. Treat a latency ratio under about 1.2x as parity rather than a lead, in either direction, and do not read a single run of your own as contradicting these. The allocation columns are the ones to hold us to.
::: LightMode opt-in disables correlation ID generation for workloads that don't need it. Hot-path breakdown (from `DispatchHotPathBreakdownBenchmarks`, last refreshed 2026-04-13 — not in current epoch): handler activation 24.4 ns / 0 B, handler invocation 6.0 ns / 0 B — all zero-allocation internals. See `benchmarks/experiments/` for optimization experiment details.

One WarmPath row under investigation: `Dispatch: 100 concurrent commands` allocation vs MediatR — a methodology-matched rerun is queued for a future sprint. No claim is made on this tier until that rerun completes.
:::

## Track A: In-Process Parity

### Dispatch vs MediatR

Source: `MediatRWarmPathComparisonBenchmarks-report-github.md` (20260420 baseline, ns scale)

| Scenario | Dispatch | MediatR | Relative Result |
|----------|----------|---------|-----------------|
| Single command handler | 66.8 ns / 240 B | 41.0 ns / 152 B | MediatR ~1.63x faster |
| Single command direct-local | 65.8 ns / 240 B | 41.0 ns / 152 B | MediatR ~1.61x faster |
| Single command ultra-local | 34.0 ns / 24 B | 41.0 ns / 152 B | **Dispatch ~1.21x faster**; Dispatch allocates ~6.3x less |
| Singleton-promoted command | 34.2 ns / 24 B | 41.0 ns / 152 B | **Dispatch ~1.20x faster**; Dispatch allocates ~6.3x less |
| Notification to 3 handlers | 144.8 ns / 240 B | 97.2 ns / 616 B | MediatR ~1.49x faster; **Dispatch allocates ~2.57x less** |
| Query with return value | 79.8 ns / 336 B | 43.8 ns / 296 B | MediatR ~1.82x faster |
| Query with return (typed API) | 86.5 ns / 432 B | 43.8 ns / 296 B | MediatR ~1.98x faster |
| Query ultra-local | 58.3 ns / 192 B | 43.8 ns / 296 B | MediatR ~1.33x faster; **Dispatch allocates ~1.54x less** |
| Query singleton-promoted | 58.2 ns / 192 B | 43.8 ns / 296 B | MediatR ~1.33x faster; **Dispatch allocates ~1.54x less** |
| 10 concurrent commands | 829.0 ns / 2,080 B | 508.4 ns / 1,856 B | MediatR ~1.63x faster |
| 100 concurrent commands | 7,242.8 ns / 19,360 B | 4,741.4 ns / 17,064 B | MediatR ~1.53x faster |

### Dispatch vs Wolverine (InvokeAsync parity)

Source: `WolverineInProcessWarmPathComparisonBenchmarks-report-github.md` (20260420 baseline, ns scale)

| Scenario | Dispatch | Wolverine (InvokeAsync) | Relative Result |
|----------|----------|--------------------------|-----------------|
| Single command (local) | 75.4 ns / 264 B | 185.0 ns / 584 B | **Dispatch 2.45x faster** |
| Single command (ultra-local) | 36.6 ns / 24 B | 185.0 ns / 584 B | **Dispatch 5.06x faster** |
| Notification to 2 handlers | 143.1 ns / 288 B | 217.8 ns / 600 B | **Dispatch 1.52x faster**; Dispatch allocates ~2.08x less |
| Query with return | 86.7 ns / 456 B | 259.8 ns / 848 B | **Dispatch 3.00x faster** |
| 10 concurrent commands | 889.2 ns / 2,320 B | 2,010.1 ns / 6,048 B | **Dispatch 2.26x faster** |
| 100 concurrent commands | 7,794.5 ns / 21,760 B | 19,435.8 ns / 59,328 B | **Dispatch 2.49x faster** |

:::note Both sides log nothing

Wolverine is hosted through `Host.CreateDefaultBuilder()`, which installs console logging by default, while
the Dispatch side is a bare service collection with no logging provider. Measured with that difference left
in place, Wolverine's **queued** path was about 35% slower purely from writing one console line per message
inside the measured region — an artifact of our harness, biased in our favour. The providers are now cleared
so neither side logs, and the figures above are from that configuration. The inline paths shown here moved
by less than run-to-run variance when logging was removed; only the queued path was materially affected.

:::

### Dispatch vs MassTransit Mediator

Source: `MassTransitMediatorWarmPathComparisonBenchmarks-report-github.md`

Both frameworks publish **two tiers**, because each one's idiomatic usage spans two shapes and comparing a
tuned configuration against an untuned one would not mean anything. Read the row whose configuration matches
your own.

MassTransit exposes two mediator entry points that differ in scope behaviour: `IScopedMediator` reuses an
ambient scope, while plain `IMediator` creates a dependency-injection scope per message. The difference is
measurable and is shown rather than hidden — a scope created once outside the measured region would have
made MassTransit look faster than a default consumer will find it.

| Scenario | Dispatch | MassTransit Mediator | Relative Result |
|----------|----------|----------------------|-----------------|
| Single command — standard vs ambient scope | 91.1 ns / 352 B | 1,221.2 ns / 3,544 B | **Dispatch 13.4x faster**, ~10.1x less memory |
| Single command — standard vs scope per message | 91.1 ns / 352 B | 1,600.7 ns / 4,336 B | **Dispatch 17.6x faster**, ~12.3x less memory |
| Single command — tuned direct-local vs ambient scope | 33.5 ns / 24 B | 1,221.2 ns / 3,544 B | **Dispatch 36.5x faster**, ~148x less memory |
| Notification to 2 handlers | 163.0 ns / 376 B | 1,719.3 ns / 4,176 B | **Dispatch 10.5x faster**, ~11.1x less memory |
| Query with return | 100.8 ns / 544 B | 14,724.7 ns / 11,650 B | **Dispatch 146x faster** |
| 10 concurrent commands | 1,114.5 ns / 3,200 B | 12,271.8 ns / 35,648 B | **Dispatch 11.0x faster** |

MassTransit's per-message scope costs **379 ns and 792 B** on this workload — the gap between its two tiers.
That is the price of the scope isolation its consumer model provides, and it is real work rather than
overhead we can claim credit for avoiding: Dispatch opens a scope too, but only for handlers whose
dependencies actually require one.

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
