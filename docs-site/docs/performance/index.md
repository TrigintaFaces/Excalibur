---
sidebar_position: 11
title: Performance Overview
description: Performance characteristics and optimization strategies for Dispatch
---

# Performance Overview

Excalibur.Dispatch is designed for low-latency messaging with explicit performance profiles for local and transport paths.

## Before You Start

- **.NET 10.0** (benchmarks validated on .NET 10.0.11, SDK 10.0.400, BenchmarkDotNet 0.15.8)
- Familiarity with [pipeline profiles](../pipeline/profiles.md) and [middleware](../middleware/index.md)

## Key Performance Metrics

Source: the WarmPath comparison epoch of 2026-09-05, committed at
`benchmarks/baselines/net10.0/dispatch-comparative-20260905/results/`.

| Metric | Value | Source |
|--------|-------|--------|
| Single command, caller-supplied context | 45.6 ns / 96 B | `MediatRWarmPathComparisonBenchmarks` |
| Single command, context-less 2-arg overload | 53.0 ns / 96 B | `MediatRWarmPathComparisonBenchmarks` |
| Singleton-promoted handler | 53.9 ns / 96 B | `MediatRWarmPathComparisonBenchmarks` |
| Query with return value | 63.7 ns / 192 B | `MediatRWarmPathComparisonBenchmarks` |
| Notification to 3 handlers | 135.0 ns / 96 B | `MediatRWarmPathComparisonBenchmarks` |
| Three-middleware pipeline | 71.7 ns / 240 B | `PipelineWarmPathComparisonBenchmarks` |
| 100 concurrent commands | 5,584 ns / 12,160 B | `MediatRWarmPathComparisonBenchmarks` |
| Dispatch vs Wolverine `InvokeAsync` | 47.0 ns / 96 B vs 179.1 ns / 584 B (**3.8x faster, 6.1x less memory**) | `WolverineInProcessWarmPathComparisonBenchmarks` |

:::info The allocation figure is a floor

Every dispatch publishes an ambient message context, so a nested dispatch inherits causation,
correlation, tenant and user rather than starting a fresh root. That costs one `ExecutionContext`
copy-on-write, and the copy is of the whole async-local value map — so what you pay scales with how
many `AsyncLocal` values *your* application keeps live: 72 of the 96 bytes when there are none,
roughly 160 B with one other, roughly 992 B with fifteen. A production host usually carries several
before it reaches this framework, and the framework itself declares two. Budget from your own
async-local density, not from the floor.
:::

:::caution Read latency as indicative, allocation as exact

Allocation is byte-identical between runs. Latency is not: the same arm varied about 4% run to run
for Dispatch and about 8.6% for MediatR, which is several times BenchmarkDotNet's reported error —
that error measures spread *within* one process, not reproducibility *between* processes. Do not read
a latency ratio inside that band as a finding, in either direction.
:::

:::warning Epoch boundary

These numbers are from the **September 5, 2026 epoch**. Baselines from before April 2026 used
BenchmarkDotNet 0.15.4, which auto-tuned `InvocationCount` and produced numerically different (but
qualitatively equivalent) ns-scale numbers. **Do not cross-diff individual Mean values across that
boundary** — ratios within each report remain apples-to-apples. See `benchmarks/RUNBOOK.md` for the
methodology shift.
:::

## Comparison Snapshot

| Track | Summary |
|------|---------|
| MediatR in-process parity | MediatR is ~1.10x faster on a single command and ~1.42x on notification fan-out; **Dispatch allocates 1.58x and 6.4x less** on those rows, and leads the pipeline track |
| Wolverine in-process parity | **Dispatch ~3.8x faster on command, ~4.0x on query, ~1.66x on notification fan-out**, allocating 2.7-6.3x less |
| MassTransit in-memory parity | **Dispatch leads every in-process tier** by two orders of magnitude against the bus and by ~17-22x against its mediator |
| Pipeline parity (3 middleware) | **Dispatch leads every framework measured** — 1.74x over MediatR, 3.30x over Wolverine, 29.7x over MassTransit, at 2.83x, 2.83x and 19.0x less allocation |

See [Competitor Comparison](./competitor-comparison.md) for full tables and methodology notes. On the
100-concurrent-command tier Dispatch allocates 12,160 B against MediatR's 17,064 B, with the two
within run-to-run variance on latency.

## Quick Wins

### 1. Let the local fast path select itself

```csharp
var result = await dispatcher.DispatchAsync(new CreateOrderAction(...), ct);
```

This is automatic. The local fast path is selected for you when the message can stay in-process and
no middleware applies to its type; there is no explicit API to call. See
[Migrating off IDirectLocalDispatcher](./ultra-local-dispatch.md) if you were using one.

### 2. Keep messages deterministic where possible

```csharp
public record CreateOrderCommand(Guid OrderId, string CustomerId) : IDispatchAction;
public class CreateOrderHandler : IActionHandler<CreateOrderCommand> { }
```

### 3. Keep auto-freeze enabled

```csharp
var host = builder.Build();
await host.RunAsync();
```

### 4. Prefer direct `IMessageContext` properties

```csharp
context.ProcessingAttempts++;
```

## Performance Guides

| Guide | Description |
|-------|-------------|
| [Migrating off IDirectLocalDispatcher](./ultra-local-dispatch.md) | The removed explicit local API, and what to call instead |
| [Auto-Freeze](./auto-freeze.md) | Automatic cache optimization |
| [MessageContext Best Practices](./messagecontext-best-practices.md) | Hot-path optimization patterns |
| [Competitor Comparison](./competitor-comparison.md) | Multi-track benchmarks vs MediatR/Wolverine/MassTransit |

## Hot-Path Optimizations

Nine micro-optimizations targeting the dispatch hot path:

| Optimization | Pattern |
|-------------|---------|
| Dual-write elimination in `RoutingDecisionAccessor` | Single-write via `CachedRoutingDecision` field with Features dictionary fallback |
| `RoutingDecision.Local` singleton | Cached static property (like `Task.CompletedTask`) |
| Lock removal on `MessageContext.Success` | Volatile fields + `AggressiveInlining` |
| Single-lookup `GetOrCreateFeature` | `TryGetValue` + direct store |
| Lightweight context init | Skip `GetTransportBinding` for outbound dispatches when no transport correlation needed |
| Per-profile middleware bypass | Pre-computed `_hasAnyNonRoutingMiddleware` flag skips FrozenDictionary chain lookup |
| Single transport bus pre-resolution | Pre-resolve single non-local bus at construction, bypass ConcurrentDictionary lookup |
| Routing decision cache | `ConcurrentDictionary<Type, RoutingDecision>` for deterministic single-route types |
| Combined transport fast path | The four transport optimizations above compose into a single pre-resolved outbound path |

## Memory Allocation Strategy

Dispatch reduces allocations through:

1. Object pooling for `MessageContext`
2. `ArrayPool<T>` on batch-style paths
3. Lazy initialization for optional context state
4. ValueTask-based local fast paths
5. Hot-path single-write patterns eliminating redundant dictionary allocations
6. Package extraction reducing dependency graph complexity

## Running Benchmarks

```bash
# Full matrix refresh
pwsh ./eng/run-benchmark-matrix.ps1 -NoRestore -NoBuild

# In-process parity track
pwsh ./eng/run-benchmark-matrix.ps1 -NoRestore -NoBuild -Classes MediatRComparisonBenchmarks,WolverineInProcessComparisonBenchmarks,MassTransitMediatorComparisonBenchmarks

# Queued/bus end-to-end parity track
pwsh ./eng/run-benchmark-matrix.ps1 -NoRestore -NoBuild -Classes TransportQueueParityComparisonBenchmarks
```

Results default to `benchmarks/runs/BenchmarkDotNet.Artifacts/results/`.

## See Also

- [Competitor Comparison](./competitor-comparison.md)
- [Migrating off IDirectLocalDispatcher](./ultra-local-dispatch.md)
- [MessageContext Best Practices](./messagecontext-best-practices.md)
