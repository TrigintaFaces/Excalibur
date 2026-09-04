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

**Current baseline:** full WarmPath run of 2026-09-03. The prior epoch is
`benchmarks/baselines/net10.0/dispatch-comparative-20260420/results/` (April 20, 2026).
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
| In-process parity (MediatR) | **Dispatch ~1.42x faster on single command with 6.3x less memory**; parity on query and concurrent tiers; MediatR ~1.44x faster on notification latency while **Dispatch allocates 6.4x less** there |
| In-process parity (Wolverine InvokeAsync) | **Dispatch ~5.64x faster on command, ~5.61x on query, ~1.69x on notification fan-out**, allocating 3.6-24x less |
| In-process parity (MassTransit Mediator) | **Dispatch leads on every tier** — 42x on single command against MassTransit's ambient-scope mediator, 55x against its scope-per-message mediator |
| Pipeline parity (3 middleware each) | **Dispatch leads every framework measured** — ~3.2x faster than MediatR, ~4.7x than Wolverine, ~44x than MassTransit, allocating 4.4x, 4.0x and 27x less |

:::note September 3, 2026 run

The standard dispatch path now matches the ultra-local one: **30.5 ns / 24 B** on a single command, about 1.42x faster than MediatR with 6.3x less memory. It used to cost 66.8 ns / 240 B, and the difference is a message context that is no longer published to handlers which never asked for one — see [how `DispatchAsync` behaves by context](../handlers.md#how-dispatchasync-behaves-by-context). Numbers below are from a full WarmPath run on an idle machine, with logging providers cleared on both sides of every comparison so neither framework's logging is inside the measured region.

:::caution Read the latency figures as indicative, the allocation figures as exact
Allocation is reproducible and was byte-identical across every run we have taken, including runs months apart and across a change of handler registration default. Latency is not: the same benchmark has varied 6-10% run to run for Dispatch and 15-22% for MediatR, which is several times BenchmarkDotNet's reported error — that error measures spread *within* one process, not reproducibility *between* processes. Treat a latency ratio under about 1.2x as parity rather than a lead, in either direction, and do not read a single run of your own as contradicting these. The allocation columns are the ones to hold us to.
::: LightMode opt-in disables correlation ID generation for workloads that don't need it. Hot-path breakdown (from `DispatchHotPathBreakdownBenchmarks`, last refreshed 2026-04-13 — not in current epoch): handler activation 24.4 ns / 0 B, handler invocation 6.0 ns / 0 B — all zero-allocation internals. See `benchmarks/experiments/` for optimization experiment details.

The `100 concurrent commands` allocation row that was previously held back is resolved by this run: Dispatch allocates 4,960 B against MediatR's 17,064 B on that tier, having previously allocated 19,360 B. The claim is no longer withheld.
:::

## Track A: In-Process Parity

### Dispatch vs MediatR

Source: `MediatRWarmPathComparisonBenchmarks`, full WarmPath run 2026-09-03 (ns scale)

| Scenario | Dispatch | MediatR | Relative Result |
|----------|----------|---------|-----------------|
| Single command handler | 30.5 ns / 24 B | 43.4 ns / 152 B | **Dispatch ~1.42x faster**; Dispatch allocates ~6.3x less |
| Single command direct-local | 30.2 ns / 24 B | 43.4 ns / 152 B | **Dispatch ~1.43x faster**; Dispatch allocates ~6.3x less |
| Single command ultra-local | 33.2 ns / 24 B | 43.4 ns / 152 B | **Dispatch ~1.31x faster**; Dispatch allocates ~6.3x less |
| Singleton-promoted command | 33.2 ns / 24 B | 43.4 ns / 152 B | **Dispatch ~1.31x faster**; Dispatch allocates ~6.3x less |
| Notification to 3 handlers | 140.8 ns / 96 B | 97.6 ns / 616 B | MediatR ~1.44x faster; **Dispatch allocates ~6.4x less** |
| Query with return value | 43.0 ns / 120 B | 39.3 ns / 224 B | Parity (MediatR ~1.09x); **Dispatch allocates ~1.9x less** |
| Query with return (typed API) | 43.3 ns / 216 B | 39.3 ns / 224 B | Parity (MediatR ~1.10x) |
| Query ultra-local | 45.1 ns / 120 B | 39.3 ns / 224 B | MediatR ~1.15x faster; **Dispatch allocates ~1.9x less** |
| Query singleton-promoted | 44.8 ns / 120 B | 39.3 ns / 224 B | MediatR ~1.14x faster; **Dispatch allocates ~1.9x less** |
| 10 concurrent commands | 491.7 ns / 640 B | 546.7 ns / 1,856 B | Parity (Dispatch ~1.11x); **Dispatch allocates ~2.9x less** |
| 100 concurrent commands | 4,484.1 ns / 4,960 B | 5,203.0 ns / 17,064 B | Parity (Dispatch ~1.16x); **Dispatch allocates ~3.4x less** |

Applying this page's own parity rule — treat a latency ratio under about 1.2x as parity — Dispatch **leads** on every single-command tier, is at **parity** on query and concurrent tiers, and **trails only on notification latency**, where it allocates 6.4x less. Every allocation column favours Dispatch.

### Dispatch vs Wolverine (InvokeAsync parity)

Source: `WolverineInProcessWarmPathComparisonBenchmarks`, full WarmPath run 2026-09-03 (ns scale)

| Scenario | Dispatch | Wolverine (InvokeAsync) | Relative Result |
|----------|----------|--------------------------|-----------------|
| Single command (local) | 33.1 ns / 24 B | 186.5 ns / 584 B | **Dispatch 5.64x faster**; allocates ~24x less |
| Single command (ultra-local) | 33.4 ns / 24 B | 186.5 ns / 584 B | **Dispatch 5.58x faster**; allocates ~24x less |
| Notification to 2 handlers | 120.0 ns / 96 B | 202.6 ns / 600 B | **Dispatch 1.69x faster**; allocates ~6.2x less |
| Query with return | 45.0 ns / 216 B | 252.6 ns / 776 B | **Dispatch 5.61x faster**; allocates ~3.6x less |
| 10 concurrent commands | 463.5 ns / 640 B | 2,014.2 ns / 6,048 B | **Dispatch 4.35x faster**; allocates ~9.4x less |
| 100 concurrent commands | 4,416.9 ns / 4,960 B | 20,048.5 ns / 59,328 B | **Dispatch 4.54x faster**; allocates ~12x less |

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
| Single command — standard vs ambient scope | 28.6 ns / 24 B | 1,207.9 ns / 3,544 B | **Dispatch 42.3x faster**, ~148x less memory |
| Single command — standard vs scope per message | 28.6 ns / 24 B | 1,570.6 ns / 4,336 B | **Dispatch 55.0x faster**, ~181x less memory |
| Single command — tuned direct-local vs ambient scope | 31.8 ns / 24 B | 1,207.9 ns / 3,544 B | **Dispatch 38.0x faster**, ~148x less memory |
| Notification to 2 handlers | 139.2 ns / 184 B | 1,741.1 ns / 4,176 B | **Dispatch 12.5x faster**, ~22.7x less memory |
| Query with return | 41.6 ns / 216 B | 12,377.0 ns / 11,637 B | **Dispatch 298x faster**, ~54x less memory |
| 10 concurrent commands | 434.5 ns / 640 B | 12,303.2 ns / 35,648 B | **Dispatch 28.3x faster**, ~56x less memory |

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

Source: `PipelineWarmPathComparisonBenchmarks`, full WarmPath run 2026-09-03 (ns scale).

| Scenario | Dispatch | MediatR | Wolverine | MassTransit |
|----------|----------|---------|-----------|-------------|
| 3 middleware / behaviors | **49.9 ns / 168 B** | 161.0 ns / 744 B | 235.3 ns / 680 B | 2,195.9 ns / 4,568 B |
| 10 concurrent + 3 behaviors | **678.5 ns / 1,392 B** | 1,724.6 ns / 7,808 B | 2,423.2 ns / 7,008 B | 22,201.4 ns / 45,888 B |

Dispatch leads on both latency and allocation against every framework measured, which is a change
from the previous baseline where it traded with MediatR and Wolverine on latency.

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
| Standard dispatch | **24 B** | ~30 ns | Default path for a handler that takes no message context (2026-09-03 WarmPath) |
| Ultra-local dispatch | **24 B** | ~33 ns | Explicit lowest-overhead local API |
| Singleton-promoted | **24 B** | ~33 ns | Handlers registered as singletons via promotion |
| Query with response | **120 B** | ~43 ns | Typed query responses |
| Query ultra-local | **120 B** | ~45 ns | Ultra-local query path |
| MessageContext pool rent+return | **0 B** | ~9 ns | Pool infrastructure cost only (not refreshed in 20260420 — see `DispatchHotPathBreakdownBenchmarks` 2026-04-13) |

:::tip Allocation Guidance

- **"Near-zero allocation"**: the standard path for a handler that takes no message context, plus the ultra-local and singleton-promoted paths (24 B per dispatch)
- **"Low-allocation"**: a handler that declares it reads the message context, which reinstates the context flow it asked for
- **"Zero-allocation internals"**: Handler activation (24.4 ns / 0 B), invocation (6.0 ns / 0 B)
:::

## Routing-First Local + Hybrid Parity

:::note

Routing-first numbers below are from the full WarmPath run of 2026-09-03.

**These paths regressed against the April 20 baseline and the regression is not yet explained.**
Latency is up 22-36% and every remote row allocates 72 B more than it did (232 B to 304 B). A
pre-routed message carries a routing decision, so it does not take the fast path that was changed
in this release — these paths still publish an ambient context, and the change that removed that
publication elsewhere cannot account for an increase here. The cause is somewhere in the five
months between the two runs and is being investigated; the numbers are published as measured
rather than held back.
:::

| Scenario | Mean | Allocated | Relative to local command |
|----------|------|-----------|---------------------------|
| Pre-routed local command | 91.77 ns | 208 B | baseline |
| Pre-routed local query | 117.61 ns | 400 B | +28.2% |
| Pre-routed remote event (AWS SQS) | 181.60 ns | 304 B | +97.9% |
| Pre-routed remote event (Azure Service Bus) | 189.39 ns | 304 B | +106.4% |
| Pre-routed remote event (AWS SNS) | 181.92 ns | 304 B | +98.2% |
| Pre-routed remote event (AWS EventBridge) | 186.56 ns | 304 B | +103.3% |
| Pre-routed remote event (Azure Event Hubs) | 186.88 ns | 304 B | +103.6% |
| Pre-routed remote event (gRPC) | 183.03 ns | 304 B | +99.4% |
| Pre-routed remote event (Kafka) | 185.42 ns | 304 B | +102.0% |
| Pre-routed remote event (RabbitMQ) | 183.52 ns | 304 B | +100.0% |

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
