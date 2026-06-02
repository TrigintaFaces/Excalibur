---
sidebar_position: 11
title: Performance Overview
description: Performance characteristics and optimization strategies for Dispatch
---

# Performance Overview

Excalibur.Dispatch is designed for low-latency messaging with explicit performance profiles for local and transport paths.

## Before You Start

- **.NET 10.0** (benchmarks validated on .NET 10.0.6, SDK 10.0.202, BenchmarkDotNet 0.15.8)
- Familiarity with [pipeline profiles](../pipeline/profiles.md) and [middleware](../middleware/index.md)

## Key Performance Metrics

Source baseline: `benchmarks/baselines/net10.0/dispatch-comparative-20260420/results/` (April 20, 2026 epoch)

| Metric | Value | Source |
|--------|-------|--------|
| Dispatch single command (standard) | 70.87 ns / 240 B | MediatRWarmPathComparisonBenchmarks (April 20, 2026) |
| Dispatch ultra-local command | 34.56 ns / 24 B | MediatRWarmPathComparisonBenchmarks |
| Dispatch vs MediatR (ultra-local) | 34.56 ns vs 44.20 ns (**1.28x faster, 6.3x less memory**) | MediatRWarmPathComparisonBenchmarks |
| Handler activation (precreated) | 24.4 ns / 0 B | DispatchHotPathBreakdownBenchmarks (not in 20260420 epoch; see performance-report) |
| Handler invocation | 6.0 ns / 0 B | DispatchHotPathBreakdownBenchmarks (not in 20260420 epoch) |
| Dispatch vs Wolverine InvokeAsync | 74.83 ns vs 197.75 ns (**2.64x faster**) | WolverineInProcessWarmPathComparisonBenchmarks |

:::warning Epoch boundary

These numbers are from the **20260420 epoch** (BenchmarkDotNet 0.15.8 literal `InvocationCount=1`). Prior baselines (20260302, dispatch-all 20260413) used BDN 0.15.4 which auto-tuned `InvocationCount`, producing numerically different (but qualitatively equivalent) ns-scale numbers. **Do not cross-diff individual Mean values between the two epochs** — ratios within each report remain apples-to-apples. See `benchmarks/RUNBOOK.md` for the methodology shift.
:::

## Diagnostics Baseline (April 13, 2026)

| Component | Value | Allocated |
|-----------|-------|-----------|
| Single command dispatch (full) | 58.5 ns | 208 B |
| Query with response | 74.8 ns | 400 B |
| Middleware invoker direct | 44.2 ns | 280 B |
| FinalDispatchHandler action | 58.7 ns | 208 B |
| LocalMessageBus send | 38.9 ns | 64 B |
| Handler activator (precreated) | 24.4 ns | 0 B |
| Handler invocation | 6.0 ns | 0 B |
| Handler registry lookup | 6.1 ns | 0 B |

:::info Breakdown vs Comparison

The diagnostics baseline above is from `DispatchHotPathBreakdownBenchmarks` which isolates each component (last refreshed April 13, 2026 — NOT in the April 20, 2026 epoch). The comparison numbers (70.87 ns for standard command) are from `MediatRWarmPathComparisonBenchmarks` in the 20260420 epoch and measure the full end-to-end path including context factory creation and return — matching how consumers use the framework.
:::

## Comparison Snapshot (April 20, 2026 epoch)

| Track | Summary |
|------|---------|
| MediatR WarmPath parity | MediatR ~1.6x faster on standard; **Dispatch ultra-local 1.28x faster with 6.3x less memory** |
| Wolverine in-process parity | **Dispatch ~2.64x faster on command; ~54x faster on notifications** (Dispatch 120 ns vs Wolverine 6,455 ns to 2 handlers) |
| MassTransit in-memory parity | **Dispatch leads on all in-process tiers**, see MassTransitComparisonBenchmarks |
| Pipeline parity (3 middleware) | See PipelineComparisonBenchmarks — Dispatch leads on allocation; latency tiers per ratio column |

See [Competitor Comparison](./competitor-comparison.md) for full tables and methodology notes. One finding under investigation: `Dispatch: 100 concurrent commands` WarmPath allocation vs MediatR — a methodology-matched rerun is queued for a future sprint.

## Quick Wins

### 1. Use Ultra-Local for local hot paths

```csharp
var result = await dispatcher.DispatchAsync(new CreateOrderAction(...), ct);
```

For explicit control, see [Ultra-Local Dispatch](./ultra-local-dispatch.md).

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
| [Ultra-Local Dispatch](./ultra-local-dispatch.md) | Lowest-overhead local command/query path |
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
| Lightweight context init (Sprint 660) | Skip `GetTransportBinding` for outbound dispatches when no transport correlation needed |
| Per-profile middleware bypass (Sprint 660) | Pre-computed `_hasAnyNonRoutingMiddleware` flag skips FrozenDictionary chain lookup |
| Single transport bus pre-resolution (Sprint 660) | Pre-resolve single non-local bus at construction, bypass ConcurrentDictionary lookup |
| Routing decision cache (Sprint 660) | `ConcurrentDictionary<Type, RoutingDecision>` for deterministic single-route types |
| Combined transport fast path (Sprint 660) | All 4 optimizations compose: Wolverine parity improved from 0.59x to 2.3x on SingleCommand |

## Memory Allocation Strategy

Dispatch reduces allocations through:

1. Object pooling for `MessageContext`
2. `ArrayPool<T>` on batch-style paths
3. Lazy initialization for optional context state
4. ValueTask-based local fast paths
5. Hot-path single-write patterns eliminating redundant dictionary allocations
6. Package extraction reducing dependency graph complexity (64.88 MB at 100K ops)

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
- [Ultra-Local Dispatch](./ultra-local-dispatch.md)
- [MessageContext Best Practices](./messagecontext-best-practices.md)
