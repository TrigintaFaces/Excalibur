---
sidebar_position: 11
title: Performance Overview
description: Performance characteristics and optimization strategies for Dispatch
---

# Performance Overview

Dispatch is designed for low-latency messaging with explicit performance profiles for local and transport paths.

## Before You Start

- **.NET 8.0+** (latest baselines are .NET 10.0.3)
- Familiarity with [pipeline profiles](../pipeline/profiles.md) and [middleware](../middleware/index.md)

## Key Performance Metrics (Feb 19, 2026 Baseline)

Baseline source folder:
- `benchmarks/baselines/net10.0/dispatch-comparative-20260219/results/`

| Metric | Value | Source |
|--------|-------|--------|
| Dispatch single command (MediatR harness) | 118.79 ns | `...MediatRComparisonBenchmarks-report-github.md` |
| Dispatch single command (Wolverine in-process harness) | 70.31 ns | `...WolverineInProcessComparisonBenchmarks-report-github.md` |
| Dispatch single command (MassTransit mediator harness) | 67.20 ns | `...MassTransitMediatorComparisonBenchmarks-report-github.md` |
| Dispatch queued command end-to-end (remote route parity harness) | 1.317 us | `...TransportQueueParityComparisonBenchmarks-report-github.md` |
| Dispatch ultra-local API (single command) | 47.12 ns | `...MediatRComparisonBenchmarks-report-github.md` |
| Pre-routed local command | 106.0 ns | `...RoutingFirstParityBenchmarks-report-github.md` |

## Comparison Snapshot

| Track | Status |
|------|--------|
| MediatR in-process parity | MediatR faster in current baseline |
| Wolverine in-process parity | Dispatch faster |
| MassTransit mediator in-process parity | Dispatch faster |
| Queued/bus end-to-end parity | Dispatch faster in current baseline |

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
| [Competitor Comparison](./competitor-comparison.md) | Two-track benchmarks vs MediatR/Wolverine/MassTransit |

## Memory Allocation Strategy

Dispatch reduces allocations through:

1. Object pooling for `MessageContext`
2. `ArrayPool<T>` on batch-style paths
3. Lazy initialization for optional context state
4. ValueTask-based local fast paths

## Running Benchmarks

```bash
# Full matrix refresh
pwsh ./eng/run-benchmark-matrix.ps1 -NoRestore -NoBuild

# In-process parity track
pwsh ./eng/run-benchmark-matrix.ps1 -NoRestore -NoBuild -Classes MediatRComparisonBenchmarks,WolverineInProcessComparisonBenchmarks,MassTransitMediatorComparisonBenchmarks

# Queued/bus end-to-end parity track
pwsh ./eng/run-benchmark-matrix.ps1 -NoRestore -NoBuild -Classes TransportQueueParityComparisonBenchmarks
```

Results default to `BenchmarkDotNet.Artifacts/results/`.

## See Also

- [Competitor Comparison](./competitor-comparison.md)
- [Ultra-Local Dispatch](./ultra-local-dispatch.md)
- [MessageContext Best Practices](./messagecontext-best-practices.md)
