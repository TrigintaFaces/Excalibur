---
sidebar_position: 11
title: Performance Overview
description: Performance characteristics and optimization strategies for Dispatch
---

# Performance Overview

Low allocation and predictable latency are design goals for the local dispatch path. This
page preserves dated measurements, the conditions they were taken under, and the tuning
levers that matter. These are measurements on one machine, not guarantees.

## November 4 component evidence

The November 4 pooled metadata query experiment is a separate managed WarmPath workload;
it does not refresh the typed-query table below. Its two held-out matched pairs measured
20.8–23.3% lower process-mean time with unchanged 192 B/op. The descriptive, posthoc paired ratio
interval was 0.63395–0.95741 (95%, two pairs, assumption-sensitive); the control interval
was too wide to establish environmental equivalence. Final scoped admission is recorded in
`artifacts/benchmarks/current-refresh-20261101/pooled-metadata-final-admission-r1.json`;
the original aggregation record preserves its earlier pending review status. No competitor ranking, production
DataProcessing throughput, or NativeAOT timing follows from this result.

The repository report `docs/performance/pooled-metadata-query-2026-11-04.md` provides the
exact commands, dirty-source hashes, raw evidence and correctness verification. It records
.NET 10.0.14 / SDK 10.0.403, BenchmarkDotNet 0.15.8, Windows x64 on an Intel Core i9-14900K,
and the original `MediatRWarmPathComparisonBenchmarks.Dispatch_QueryWithReturnValue`
fixture. The separately executed NativeAOT probe verifies behavior, not these managed timings.

## Historical local dispatch overhead — October 19, 2026

Median of independent processes: commands and queries measured 19 October 2026. BenchmarkDotNet 0.15.8, adaptive
warm-path job, in-process emit toolchain; .NET 10.0.12, SDK 10.0.401, x64 RyuJIT; Intel Core
i9-14900K, Windows 11 build 26300.9711. Rounded to the precision the between-process spread
supports.

Each row comes from three separate original-observation processes. Query rows use
the runtime files selected for that October 19 measurement after the fixture rental-lifecycle repair. They do
not certify the entire rebuilt checkout or actual NativeAOT performance. The
earlier isolated-marker query experiment remains dated history. These observations predate
subsequent framework changes and do not establish current-source performance.

| Path | Latency | Allocation |
|---|---:|---:|
| Single command | **~65 ns** | **96 B** |
| Typed query, default pipeline | **~136 ns** | **288 B** |
| Typed query, context-less overload | ~146 ns | 288 B |
| Typed query, singleton promotion configured | ~149 ns | 288 B |

The command handler is a no-op; query handlers double the input ID. These fixtures
measure dispatch and completion overhead rather than application work. Query timing
increased from the earlier isolated experiment on both peers; its cause remains unresolved.

Full records, with per-process figures and the retained distribution warnings, are in the
[benchmark results](./competitor-comparison.md).

Separately dated [MassTransit Mediator query observations](./competitor-comparison.md#masstransit-mediator-query-observations)
cover three original warm-path processes on 19 October. Their fixture and variance limits are
distinct from the local-overhead table above.

:::info Comparative coverage is incomplete

The figures above measure Dispatch overhead and establish no general competitive ranking.
Specific dated observations retain their own verification scope; other comparative fixtures in
`benchmarks/Excalibur.Dispatch.Benchmarks` remain unverified. A published comparison must match completed semantic work, runtime and job settings on
the same machine, disclose library versions and configurations, and retain failures, unavailable
results and warnings. Framework overhead and end-to-end broker work answer different questions.

We disclose our project affiliation and provide the methodology and raw results for reproduction
and independent review. Measure your own workload before choosing a framework.
:::

:::note Reading the dated local-overhead observations

Allocation in that table was byte-identical across processes. Its latency figures are indicative.
The October 19 command observations ranged from 58.10 to 65.27 ns between processes (about 12.3% maximum/minimum spread), exceeding BenchmarkDotNet's reported error
-- that error describes spread *within* one process, not reproducibility *between* processes. A
difference inside that band is not a finding.

The October 19 query process ranges were 134–141 ns for the default profile,
144–147 ns for context-less and 143–153 ns with promotion configured. Effective
singleton promotion was not verified. These are warm, in-process, single-operation
fixtures. No sustained-throughput,
percentile SLO or cold-start claim follows from them.

The separately dated MassTransit query observations retain their own allocation ranges and
distribution warnings.
:::


## What the allocation buys

Ambient message-context flow allows nested dispatches to inherit causation, correlation,
tenant and user information. Its allocation cost depends on the selected dispatch path,
runtime and the application's other async-local values. The historical local-dispatch
table above does not isolate that cost or establish a universal allocation budget.

Measure the configured path in your own process. `LightMode` disables correlation-ID
generation. Ambient message-context flow remains enabled so nested dispatches can
inherit context; enabling `LightMode` does not remove that flow or its allocation cost.

## Before you start

- **.NET 10.0.** October 19 figures: runtime 10.0.12 / SDK 10.0.401. Earlier figures below:
  runtime 10.0.11 / SDK 10.0.400. BenchmarkDotNet 0.15.8 throughout.
- Familiarity with [pipeline profiles](../pipeline/profiles.md) and [middleware](../middleware/index.md).

## Earlier measurements, and what has not been re-measured

The figures above replace the command and query rows of a September 5, 2026 comparison epoch,
which measured a single command at 45.6 ns before a correction that restored default correlation
and causation initialization. That epoch is not a baseline for the current source.

The remaining September comparison rows are preserved below. **They describe that epoch's
code.** Later, separately scoped October observations do not refresh these comparisons.

| Scenario (September 5, 2026 epoch) | Measurement |
|---|---|
| Notification to 3 handlers | 135.0 ns / 96 B |
| Nominal three-middleware fixture | 71.7 ns / 240 B |
| 100 concurrent commands | 5,584 ns / 12,160 B |

Historical pipeline stage execution and parity remain unverified. Current-source
controls found that profile selection cleared the registered Dispatch middleware;
the repaired fixture establishes a separate workload epoch
(`artifacts/benchmarks/current-refresh-20261101/pipeline-fixture-fix-evidence-r1.json`).

Source: `benchmarks/baselines/net10.0/dispatch-comparative-20260905/results/`. Full tables and
methodology notes: [Benchmark Results](./competitor-comparison.md).

:::warning Do not cross-diff across the April 2026 methodology boundary

Historical records on opposite sides of this boundary used different tooling and
reported job behavior. They do not establish a matched comparison of the current
source. Preserve their epochs and avoid direct differences; the tool version alone
does not prove the cause of the timing shift. Ratios within a report still require
equivalent completed work and an uncertainty assessment. See `benchmarks/RUNBOOK.md`.

:::

> MediatR is a trademark of its owner. It is named in these docs only to describe API
> compatibility; no affiliation or endorsement is implied.


## Configuration choices to verify

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
public class CreateOrderHandler : IActionHandler<CreateOrderCommand>
{
    public Task HandleAsync(CreateOrderCommand message, CancellationToken cancellationToken)
        => Task.CompletedTask; // Replace with application work.
}
```

### 3. Verify auto-freeze registration and lifecycle

```csharp
var host = builder.Build();
await host.RunAsync();
```

Starting a host alone does not demonstrate that a cache was frozen. Follow the
[auto-freeze registration and lifecycle guidance](./auto-freeze.md) and verify the configured path.

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
| [Benchmark Results](./competitor-comparison.md) | Measured dispatch overhead, methodology and how to run it |

## Hot-Path Optimizations

Implementation mechanisms relevant to the dispatch hot path; benefits depend on the measured path:

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

Mechanisms relevant to allocation and dependency costs (not a claim that every path benefits):

1. Object pooling for `MessageContext`
2. `ArrayPool<T>` on batch-style paths
3. Lazy initialization for optional context state
4. ValueTask-based local fast paths
5. Hot-path single-write patterns eliminating redundant dictionary allocations
6. Package extraction reducing dependency graph complexity; this alone does not establish lower per-operation allocations

## Running Benchmarks

```bash
# Declared matrix selection
pwsh ./eng/run-benchmark-matrix.ps1 -NoRestore -NoBuild

# In-process parity track
pwsh ./eng/run-benchmark-matrix.ps1 -NoRestore -NoBuild -Classes PipelineComparisonBenchmarks

# Queued/bus comparison fixtures; verify completed-work parity
pwsh ./eng/run-benchmark-matrix.ps1 -NoRestore -NoBuild -Classes TransportQueueParityComparisonBenchmarks
```

Results default to `benchmarks/runs/BenchmarkDotNet.Artifacts/results/`.

## See Also

- [Benchmark Results](./competitor-comparison.md)
- [Migrating off IDirectLocalDispatcher](./ultra-local-dispatch.md)
- [MessageContext Best Practices](./messagecontext-best-practices.md)
