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
| Dispatch single command (lean) | 41.43 ns | MediatRWarmPathComparisonBenchmarks |
| Dispatch ultra-local API (single command) | 31.81 ns | MediatRWarmPathComparisonBenchmarks |
| Dispatch vs MediatR single command | 41.43 ns vs 43.79 ns (**Dispatch faster**) | MediatRWarmPathComparisonBenchmarks |
| Dispatch vs Wolverine InvokeAsync | 50.55 ns vs 189.15 ns | WolverineInProcessWarmPathComparisonBenchmarks |
| Dispatch vs MassTransit Mediator | 48.04 ns vs 1,241.95 ns | MassTransitMediatorWarmPathComparisonBenchmarks |
| Handler activation | 26.69 ns / 24 B | DispatchHotPathBreakdownBenchmarks |
| Context creation | 13.23 ns / 216 B | DispatchThroughputBenchmarks |

## Diagnostics Baseline

28 benchmark classes, 0 failures.

| Metric | Value | Allocated |
|--------|-------|-----------|
| Single command dispatch | 41.43 ns | 168 B |
| Query with response | 52.89 ns | 264 B |
| FinalDispatchHandler | 94.81 ns | 296 B |
| Handler activation | 26.69 ns | 24 B |
| Context creation | 13.23 ns | 216 B |
| Notification to 3 handlers | 112.92 ns | 240 B |
| Dispatch single command (MediatR bench) | 41.43 ns | 168 B |

## Comparison Snapshot

| Track | Summary |
|------|---------|
| MediatR in-process parity | **Dispatch ~1.05x faster** on standard command; **ultra-local ~1.4x faster**; **Dispatch wins 100-concurrent and allocation** |
| Wolverine in-process parity | **Dispatch ~3.7x faster on command, ~34x on notifications** |
| MassTransit mediator in-process parity | **Dispatch ~26x faster on single command** |
| Queued/bus end-to-end parity | **Dispatch ~3.3x faster than Wolverine, ~19.9x faster than MassTransit** on single queued command |

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

Five micro-optimizations targeting the dispatch hot path:

| Optimization | Pattern |
|-------------|---------|
| Dual-write elimination in `RoutingDecisionAccessor` | Single-write via `CachedRoutingDecision` field with Features dictionary fallback |
| `RoutingDecision.Local` singleton | Cached static property (like `Task.CompletedTask`) |
| Lock removal on `MessageContext.Success` | Volatile fields + `AggressiveInlining` |
| Single-lookup `GetOrCreateFeature` | `TryGetValue` + direct store |

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
