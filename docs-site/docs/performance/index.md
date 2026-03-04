---
sidebar_position: 11
title: Performance Overview
description: Performance characteristics and optimization strategies for Dispatch
---

# Performance Overview

Excalibur.Dispatch is designed for low-latency messaging with explicit performance profiles for local and transport paths.

## Before You Start

- **.NET 8.0+** (latest comparative validation run used .NET 10.0.103)
- Familiarity with [pipeline profiles](../pipeline/profiles.md) and [middleware](../middleware/index.md)

## Key Performance Metrics (Mar 2, 2026 Baseline)

Baseline source folder:
- `benchmarks/baselines/net10.0/dispatch-comparative-20260302/results/`

| Metric | Value | Source |
|--------|-------|--------|
| Dispatch single command (lean) | 75.32 ns | MediatRComparisonBenchmarks |
| Dispatch ultra-local API (single command) | 31.54 ns | MediatRComparisonBenchmarks |
| Dispatch singleton-promoted (single command) | 31.73 ns | MediatRComparisonBenchmarks |
| Dispatch vs Wolverine InvokeAsync | 132.26 ns vs 368.19 ns | WolverineInProcessComparisonBenchmarks |
| Dispatch vs MassTransit Mediator | 178.2 ns vs 4,120.8 ns | MassTransitMediatorComparisonBenchmarks |
| Dispatch queued command end-to-end | 1.147 us | TransportQueueParityComparisonBenchmarks |
| Pre-routed local command | 78.17 ns | RoutingFirstParityBenchmarks |

## Latest Comparative Validation (Mar 2, 2026)

- Command:
  - `pwsh eng/run-comparative-benchmarks.ps1 -NoBuild -NoRestore -RuntimeProfile ci`
- Status: `7/7` classes passed, `78` benchmark rows, `0` failures
- Duration: `00:29:10.72`
- Evidence reports:
  - `benchmarks/baselines/net10.0/dispatch-comparative-20260302/results/Excalibur.Dispatch.Benchmarks.Comparative.MediatRComparisonBenchmarks-report-github.md`
  - `benchmarks/baselines/net10.0/dispatch-comparative-20260302/results/Excalibur.Dispatch.Benchmarks.Comparative.WolverineInProcessComparisonBenchmarks-report-github.md`
  - `benchmarks/baselines/net10.0/dispatch-comparative-20260302/results/Excalibur.Dispatch.Benchmarks.Comparative.WolverineComparisonBenchmarks-report-github.md`
  - `benchmarks/baselines/net10.0/dispatch-comparative-20260302/results/Excalibur.Dispatch.Benchmarks.Comparative.MassTransitMediatorComparisonBenchmarks-report-github.md`
  - `benchmarks/baselines/net10.0/dispatch-comparative-20260302/results/Excalibur.Dispatch.Benchmarks.Comparative.MassTransitComparisonBenchmarks-report-github.md`
  - `benchmarks/baselines/net10.0/dispatch-comparative-20260302/results/Excalibur.Dispatch.Benchmarks.Comparative.PipelineComparisonBenchmarks-report-github.md`
  - `benchmarks/baselines/net10.0/dispatch-comparative-20260302/results/Excalibur.Dispatch.Benchmarks.Comparative.TransportQueueParityComparisonBenchmarks-report-github.md`

This comparative refresh is now the active source for the in-process, pipeline, and queue values above.

## Comparison Snapshot

| Track | Status |
|------|--------|
| MediatR in-process parity | MediatR ~1.3-1.6x faster on standard command/query paths; Dispatch ultra-local/singleton paths ~1.1-1.5x faster |
| Wolverine in-process parity | **Dispatch ~2.3-3.0x faster on command/query paths, ~18x on notifications** |
| MassTransit mediator in-process parity | Dispatch ~12.3-55.7x faster |
| Pipeline parity (3 middleware) | MediatR ~1.8-2.2x faster; Wolverine ~1.2-1.5x faster; Dispatch ~5.7-7.4x faster than MassTransit |
| Queued/bus end-to-end parity | Dispatch ~3.2-6.3x faster than Wolverine, ~12.3-21.2x faster than MassTransit |

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

Results default to `benchmarks/runs/BenchmarkDotNet.Artifacts/results/`.

## See Also

- [Competitor Comparison](./competitor-comparison.md)
- [Ultra-Local Dispatch](./ultra-local-dispatch.md)
- [MessageContext Best Practices](./messagecontext-best-practices.md)
