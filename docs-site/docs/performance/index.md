---
sidebar_position: 11
title: Performance Overview
description: Performance characteristics and optimization strategies for Dispatch
---

# Performance Overview

Excalibur.Dispatch is designed for low-latency messaging with explicit performance profiles for local and transport paths.

## Before You Start

- **.NET 8.0+** (benchmarks validated on .NET 10.0.103)
- Familiarity with [pipeline profiles](../pipeline/profiles.md) and [middleware](../middleware/index.md)

## Key Performance Metrics

Results: `benchmarks/runs/BenchmarkDotNet.Artifacts/results/`

| Metric | Value | Source |
|--------|-------|--------|
| Dispatch single command (standard) | 54.07 ns / 240 B | MediatRWarmPathComparisonBenchmarks |
| Dispatch ultra-local API (single command) | 31.11 ns / 24 B | MediatRWarmPathComparisonBenchmarks |
| Dispatch vs MediatR single command | 54.07 ns vs 41.37 ns | MediatRWarmPathComparisonBenchmarks |
| Dispatch ultra-local vs MediatR | 31.11 ns vs 41.37 ns (**Dispatch 1.3x faster**) | MediatRWarmPathComparisonBenchmarks |
| Dispatch vs Wolverine InvokeAsync | 70.35 ns vs 183.56 ns (**Dispatch 2.6x faster**) | WolverineInProcessWarmPathComparisonBenchmarks |
| MessageContext pool rent+return | 9.13 ns / 0 B | MessageContextPoolBenchmarks |
| MessageContext creation (no pool) | 10.27 ns / 216 B | MessageContextPoolBenchmarks |

## Diagnostics Baseline

| Metric | Value | Allocated |
|--------|-------|-----------|
| Single command dispatch (standard) | 54.07 ns | 240 B |
| Single command dispatch (ultra-local) | 31.11 ns | 24 B |
| Query with response | 65.97 ns | 336 B |
| Notification to 3 handlers | 109.69 ns | 240 B |
| Singleton-promoted command | 31.39 ns | 24 B |
| Singleton-promoted query | 51.92 ns | 192 B |

## Comparison Snapshot

| Track | Summary |
|------|---------|
| MediatR in-process parity | MediatR ~1.3x faster on standard; **Dispatch ultra-local ~1.3x faster**; **Dispatch allocates 6.3x less on ultra-local** |
| Wolverine in-process parity | **Dispatch ~2.6x faster on command, ~61x on notifications** |
| Pipeline parity (3 middleware) | MediatR ~2.7x faster; **Dispatch 1.2x faster than Wolverine**; **Dispatch 6.8x faster than MassTransit** |

See [Competitor Comparison](./competitor-comparison.md) for full tables and methodology notes.

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
