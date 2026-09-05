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
BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9168/25H2)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.400
Runtime: .NET 10.0.11 (10.0.1126.37416), X64 RyuJIT x86-64-v3
Job=warmpath-inproc  Toolchain=InProcessEmitToolchain
```

**Current baseline:** the WarmPath epoch of 2026-09-05, committed at
`benchmarks/baselines/net10.0/dispatch-comparative-20260905/results/`. It supersedes every earlier
epoch for current claims; earlier epochs are kept because they measured different code.
**Prior baselines** (superseded, and not cross-diffable with anything from before April 2026 due to
the BenchmarkDotNet 0.15.4 → 0.15.8 `InvocationCount` semantic shift). Ratios within each report
remain apples-to-apples; **do not cross-diff individual Mean values** across epoch boundaries.

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
| In-process parity (MediatR) | **MediatR is ~1.10x faster on a single command and ~1.42x on notification fan-out**; Dispatch allocates 1.58x and 6.4x less on those rows, and is within run-to-run variance on both concurrency tiers |
| In-process parity (Wolverine `InvokeAsync`) | **Dispatch ~3.8x faster on command, ~4.0x on query, ~1.66x on notification fan-out**, allocating 2.7-6.3x less |
| In-process parity (MassTransit Mediator) | **Dispatch leads every tier** — ~17x on single command against MassTransit's ambient-scope mediator, ~22x against its scope-per-message mediator |
| In-process parity (MassTransit bus) | **Dispatch ~370x faster on a single command** against the in-memory bus, at ~230x less allocation |
| Pipeline parity (3 middleware each) | **Dispatch leads every framework measured** — 1.74x over MediatR, 3.30x over Wolverine, 29.7x over MassTransit, allocating 2.83x, 2.83x and 19.0x less |

:::caution Read the latency figures as indicative, the allocation figures as exact

Allocation is reproducible and byte-identical between runs. Latency is not: the same arm varied about
4% run to run for Dispatch and about 8.6% for MediatR, which is several times BenchmarkDotNet's
reported error — that error measures spread *within* one process, not reproducibility *between*
processes. Do not read a ratio inside that band as a finding, in either direction, and do not read a
single run of your own as contradicting these. The allocation columns are the ones to hold us to.
:::

:::info Every allocation figure here is a floor

A dispatch publishes an ambient message context, so a nested dispatch inherits causation, correlation,
tenant and user rather than starting a fresh root. That costs one `ExecutionContext` copy-on-write,
and the copy is of the whole async-local value map — so the real cost scales with how many
`AsyncLocal` values *your* application keeps live: 72 of the 96 bytes when there are none, roughly
160 B with one other, roughly 992 B with fifteen. A production host usually carries several before it
reaches this framework, and the framework itself declares two. Budget from your own async-local
density; the numbers below are the floor, not the bill.
:::

:::note The query comparison is deliberately absent

MediatR's own query row moved about 21% between epochs — consistently across every run of the current
one, and for reasons that have nothing to do with this framework. Until that is explained the ratio is
meaningless in either direction, so no Dispatch-versus-MediatR query row is published. Dispatch's own
query figures appear below without a comparison column.
:::

## Track A: In-Process Parity

### Dispatch vs MediatR

Source: `MediatRWarmPathComparisonBenchmarks`, 2026-09-05 epoch (ns scale)

| Scenario | Dispatch | MediatR | Relative Result |
|----------|----------|---------|-----------------|
| Single command handler | 45.58 ns / 96 B | 41.32 ns / 152 B | MediatR ~1.10x faster; **Dispatch allocates ~1.58x less** |
| Single command, strict direct-local profile | 46.00 ns / 96 B | 41.32 ns / 152 B | MediatR ~1.11x faster; **Dispatch allocates ~1.58x less** |
| Single command, context-less 2-arg overload | 53.00 ns / 96 B | 41.32 ns / 152 B | MediatR ~1.28x faster; **Dispatch allocates ~1.58x less** |
| Singleton-promoted command | 53.85 ns / 96 B | 41.32 ns / 152 B | MediatR ~1.30x faster; **Dispatch allocates ~1.58x less** |
| Notification to 3 handlers | 134.99 ns / 96 B | 95.01 ns / 616 B | MediatR ~1.42x faster; **Dispatch allocates ~6.4x less** |
| 10 concurrent commands | 596.06 ns / 1,360 B | 541.74 ns / 1,856 B | Within variance (~1.10x); **Dispatch allocates ~1.36x less** |
| 100 concurrent commands | 5,584.43 ns / 12,160 B | 5,146.08 ns / 17,064 B | Within variance (~1.09x); **Dispatch allocates ~1.40x less** |

Dispatch's own query rows from the same run, published without a MediatR comparison for the reason
given above:

| Scenario | Dispatch |
|----------|----------|
| Query with return value | 63.67 ns / 192 B |
| Query, strict direct-local profile | 63.02 ns / 192 B |
| Query with return value (typed API) | 63.76 ns / 288 B |
| Query, context-less 2-arg overload | 69.87 ns / 288 B |
| Query, singleton-promoted | 68.99 ns / 288 B |

Stated plainly: **MediatR is a few nanoseconds ahead on the bare command and notification paths.**
Dispatch allocates less on every scenario measured, is within run-to-run variance on both concurrency
tiers, and leads decisively once middleware is in the pipeline (Track B) and against every other
framework here.

### Dispatch vs Wolverine (InvokeAsync parity)

Source: `WolverineInProcessWarmPathComparisonBenchmarks`, 2026-09-05 epoch (ns scale)

| Scenario | Dispatch | Wolverine (InvokeAsync) | Relative Result |
|----------|----------|--------------------------|-----------------|
| Single command (local) | 47.00 ns / 96 B | 179.13 ns / 584 B | **Dispatch 3.81x faster**; allocates ~6.1x less |
| Single command (context-less 2-arg) | 54.11 ns / 96 B | 179.13 ns / 584 B | **Dispatch 3.31x faster**; allocates ~6.1x less |
| Notification to 2 handlers | 120.12 ns / 96 B | 199.66 ns / 600 B | **Dispatch 1.66x faster**; allocates ~6.3x less |
| Query with return | 63.74 ns / 288 B | 252.73 ns / 776 B | **Dispatch 3.96x faster**; allocates ~2.7x less |
| 10 concurrent commands | 585.42 ns / 1,360 B | 2,028.61 ns / 6,048 B | **Dispatch 3.47x faster**; allocates ~4.4x less |
| 100 concurrent commands | 5,662.98 ns / 12,160 B | 20,154.80 ns / 59,328 B | **Dispatch 3.56x faster**; allocates ~4.9x less |

:::note Both sides log nothing

Wolverine is hosted through `Host.CreateDefaultBuilder()`, which installs console logging by default,
while the Dispatch side is a bare service collection with no logging provider. Measured with that
difference left in place, Wolverine's **queued** path was about 35% slower purely from writing one
console line per message inside the measured region — an artifact of our harness, biased in our
favour. The providers are now cleared so neither side logs, and the figures above are from that
configuration. The inline paths shown here moved by less than run-to-run variance when logging was
removed; only the queued path was materially affected.

:::

### Dispatch vs MassTransit Mediator

Source: `MassTransitMediatorWarmPathComparisonBenchmarks`, 2026-09-05 epoch

MassTransit exposes two mediator entry points that differ in scope behaviour: `IScopedMediator` reuses
an ambient scope, while plain `IMediator` creates a dependency-injection scope per message. The
difference is measurable and is shown rather than hidden — a scope created once outside the measured
region would have made MassTransit look faster than a default consumer will find it. The Dispatch arms
in this class are configured with a local bus and allocate 184 B rather than the 96 B of the leaner
pairings above; read the row whose configuration matches your own.

| Scenario | Dispatch | MassTransit Mediator | Relative Result |
|----------|----------|----------------------|-----------------|
| Single command — local vs ambient scope | 75.37 ns / 184 B | 1,275.66 ns / 3,544 B | **Dispatch ~16.9x faster**, ~19.3x less memory |
| Single command — local vs scope per message | 75.37 ns / 184 B | 1,630.79 ns / 4,336 B | **Dispatch ~21.6x faster**, ~23.6x less memory |
| Single command — tuned direct-local vs ambient scope | 76.60 ns / 184 B | 1,275.66 ns / 3,544 B | **Dispatch ~16.7x faster**, ~19.3x less memory |
| Notification to 2 handlers | 138.14 ns / 184 B | 1,765.57 ns / 4,176 B | **Dispatch ~12.8x faster**, ~22.7x less memory |
| Query with return | 87.63 ns / 376 B | 11,426.39 ns / 11,601 B | **Dispatch ~130x faster**, ~31x less memory |
| 10 concurrent commands | 801.83 ns / 2,240 B | 12,481.32 ns / 35,648 B | **Dispatch ~15.6x faster**, ~15.9x less memory |
| 100 concurrent commands | 7,650.61 ns / 20,960 B | 125,098.21 ns / 355,329 B | **Dispatch ~16.4x faster**, ~17.0x less memory |

MassTransit's per-message scope costs **355 ns and 792 B** on this workload — the gap between its two
tiers. That is the price of the scope isolation its consumer model provides, and it is real work
rather than overhead we can claim credit for avoiding: Dispatch opens a scope too, but only for
handlers whose dependencies actually require one.

The query row carries a very wide error on the MassTransit side (standard deviation about 27% of the
mean). Treat its ratio as an order of magnitude, not a figure.

### Dispatch vs MassTransit (in-memory bus)

Source: `MassTransitWarmPathComparisonBenchmarks`, 2026-09-05 epoch. MassTransit's full bus does
transport work even in memory, so this is an architecture comparison rather than a like-for-like one.

| Scenario | Dispatch | MassTransit (bus) | Relative Result |
|----------|----------|-------------------|-----------------|
| Single command | 46.25 ns / 96 B | 17,118.48 ns / 22,080 B | **Dispatch ~370x faster**, ~230x less memory |
| Event to 2 handlers | 112.10 ns / 96 B | 32,799.39 ns / 39,377 B | **Dispatch ~293x faster**, ~410x less memory |
| 10 concurrent commands | 604.90 ns / 1,360 B | 187,233.96 ns / 219,151 B | **Dispatch ~310x faster**, ~161x less memory |
| 100 concurrent commands | 5,777.73 ns / 12,160 B | 1,522,021.74 ns / 2,185,202 B | **Dispatch ~263x faster**, ~180x less memory |
| Batch send (10) | 512.30 ns / 960 B | 160,922.30 ns / 219,296 B | **Dispatch ~314x faster**, ~228x less memory |

## Track B: Pipeline Parity (3 Middleware Each)

Each framework configured with 3 passthrough middleware/behaviors that mirror each other:
- **Dispatch**: 3 `IDispatchMiddleware` (logging, validation, timing)
- **MediatR**: 3 `IPipelineBehavior<T, Unit>` (logging, validation, timing)
- **Wolverine**: 3 convention-based middleware with `BeforeAsync`/`AfterAsync`
- **MassTransit**: 3 `IFilter<ConsumeContext<T>>` (logging, validation, timing)

Source: `PipelineWarmPathComparisonBenchmarks`, 2026-09-05 epoch (ns scale).

| Scenario | Dispatch | MediatR | Wolverine | MassTransit |
|----------|----------|---------|-----------|-------------|
| 3 middleware / behaviors | **71.68 ns / 240 B** | 124.87 ns / 680 B | 236.34 ns / 680 B | 2,128.02 ns / 4,568 B |
| 10 concurrent + 3 behaviors | **888.19 ns / 2,112 B** | 1,314.09 ns / 7,168 B | 2,432.01 ns / 7,008 B | 21,023.12 ns / 45,888 B |

Dispatch leads on both latency and allocation against every framework measured. This is the track
where the lead over MediatR is largest, and it is the configuration most applications actually run.

## Track C: Queued/Bus End-to-End Parity

Source: `TransportQueueParityWarmPathComparisonBenchmarks`, 2026-09-05 epoch.

| Scenario | Dispatch | Wolverine | MassTransit |
|----------|----------|-----------|-------------|
| Queued command end-to-end | **1.361 μs / 793 B** | 4.050 μs / 4,400 B | 16.457 μs / 22,086 B |
| Queued event fan-out end-to-end | **1.420 μs / 794 B** | 3.983 μs / 4,400 B | 31.624 μs / 39,416 B |
| Queued commands, 10 concurrent | **7.072 μs / 5,118 B** | 40.701 μs / 44,489 B | 161.395 μs / 219,091 B |

Dispatch is about 2.8-5.8x faster than Wolverine and about 12-23x faster than MassTransit on these
rows, at 5.5-8.7x and 28-49x less allocation respectively. The MassTransit rows carry wide error
margins; treat their ratios as directional.

:::warning Interpretation Guardrail

Use Track A for closest in-process handler overhead parity. Use Track B when comparing
middleware/pipeline cost across frameworks. Use Track C when comparing queued/bus completion
semantics.
:::

## Allocation Profiles

Excalibur.Dispatch offers several dispatch paths with different allocation characteristics. Every
figure below is the floor described at the top of this page.

| Profile | Allocation | Latency | When to Use |
|---------|-----------|---------|-------------|
| Single command, caller-supplied context | **96 B** | ~46 ns | You already hold an `IMessageContext` and want to pass it through |
| Strict direct-local profile | **96 B** | ~46 ns | Explicit no-middleware profile |
| Context-less 2-arg overload | **96 B** | ~53 ns | `DispatchAsync(message, ct)` — the framework creates the context |
| Singleton-promoted | **96 B** | ~54 ns | Handlers registered as singletons via promotion |
| Query with response | **192 B** | ~64 ns | Typed query responses |
| Query via the typed API or the 2-arg overload | **288 B** | ~64-70 ns | Typed query APIs that materialise their own context |

:::tip Allocation Guidance

- A command dispatch allocates **96 B at the floor**, of which 72 B is the ambient-context
  `ExecutionContext` copy — the price of a nested dispatch inheriting causation, correlation, tenant
  and user rather than silently starting a fresh root. It scales with your application's async-local
  density, not with ours.
- A query costs 192 B when it materialises one result, and 288 B through the typed and context-less
  APIs.
- If you need that floor to be lower, reduce the number of `AsyncLocal` values live at the call site;
  no framework setting removes the copy.
:::

## Routing-First Local + Hybrid Parity

:::note These paths cost more than the direct local path, and we cannot yet say why

A pre-routed message carries a routing decision, so it takes a different path from the direct local
dispatch above. Since April its remote rows have allocated 72 B more than they did (232 B to 304 B),
with a matching latency increase. This epoch recovers 7-9% of that latency, inherited from removing
two caches that cost more than the work they replaced; it recovers none of the allocation.

Three explanations have been ruled out rather than assumed:

- **an ambient tenant context** — not present in the change history for these paths;
- **a security change altering middleware selection for unclassified messages** — likewise absent;
- **the ambient message-context publication** — excluded by direct experiment. Adding a 72 B ambient
  push moved the local rows by exactly 72 B and left all eight remote rows unchanged.

Three eliminations are not an explanation. The figures below are published as measured, with no claim
about the cause and no commitment about when the difference will close.
:::

Source: `RoutingFirstParityWarmPathBenchmarks`, 2026-09-05 epoch.

| Scenario | Mean | Allocated | Relative to local command |
|----------|------|-----------|---------------------------|
| Pre-routed local command | 94.34 ns | 280 B | baseline |
| Pre-routed local query | 96.01 ns | 472 B | +1.8% |
| Pre-routed remote event (AWS SQS) | 167.33 ns | 304 B | +77.4% |
| Pre-routed remote event (Azure Service Bus) | 172.91 ns | 304 B | +83.3% |
| Pre-routed remote event (AWS SNS) | 168.50 ns | 304 B | +78.6% |
| Pre-routed remote event (AWS EventBridge) | 168.58 ns | 304 B | +78.7% |
| Pre-routed remote event (Azure Event Hubs) | 171.58 ns | 304 B | +81.9% |
| Pre-routed remote event (gRPC) | 167.69 ns | 304 B | +77.7% |
| Pre-routed remote event (Kafka) | 169.71 ns | 304 B | +79.9% |
| Pre-routed remote event (RabbitMQ) | 169.79 ns | 304 B | +80.0% |

### Provider Profile Extensions

| Scenario | Mean | Allocated |
|----------|------|-----------|
| Kafka throughput profile | 226.89 ns | 352 B |
| Kafka retry profile | 225.02 ns | 376 B |
| Kafka poison profile | 210.00 ns | 328 B |
| Kafka observability profile | 310.48 ns | 376 B |
| RabbitMQ throughput profile | 225.19 ns | 352 B |
| RabbitMQ retry profile | 220.34 ns | 376 B |
| RabbitMQ poison profile | 207.79 ns | 328 B |
| RabbitMQ observability profile | 300.81 ns | 376 B |

The profile rows carry the same unexplained +72 B as the remote rows above (280 B to 352 B, 304 B to
376 B, 256 B to 328 B).

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
