---
sidebar_position: 4
title: Competitor Comparison
description: Performance comparison of Excalibur.Dispatch vs MediatR, Wolverine, and MassTransit
---

# Competitor Comparison

This page documents comparative benchmark baselines for **Excalibur.Dispatch** using two explicit tracks:

1. **In-process parity** (raw handler-dispatch style)
2. **Queued/bus semantics** (publish/send + consumer flow)

## Baseline Artifacts

- Baseline folder: `benchmarks/baselines/net10.0/dispatch-comparative-20260219/results/`
- Runtime: `.NET 10.0.3`
- Tooling: `BenchmarkDotNet v0.15.4`
- Machine: `Intel Core i9-14900K`, `Windows 11 (10.0.26200.7840)`

:::info Scope
These are microbenchmarks for framework overhead and path cost. They are not end-to-end production latency claims.
:::

## Executive Summary

| Track | Summary |
|------|---------|
| In-process parity (MediatR) | MediatR is faster in current baseline |
| In-process parity (Wolverine InvokeAsync) | Dispatch is faster |
| In-process parity (MassTransit Mediator) | Dispatch is faster |
| Queued/bus end-to-end parity (Dispatch remote route / Wolverine SendAsync / MassTransit bus) | Dispatch is faster in current baseline |

## Track A: In-Process Parity

### Dispatch vs MediatR

Source: `benchmarks/baselines/net10.0/dispatch-comparative-20260219/results/Excalibur.Dispatch.Benchmarks.Comparative.MediatRComparisonBenchmarks-report-github.md`

| Scenario | Dispatch | MediatR | Relative Result |
|----------|----------|---------|-----------------|
| Single command handler | 118.79 ns | 40.92 ns | MediatR ~2.9x faster |
| Single command strict direct-local | 116.29 ns | 40.92 ns | MediatR ~2.8x faster |
| Single command ultra-local API | 47.12 ns | 40.92 ns | MediatR ~1.2x faster |
| Notification to 3 handlers | 154.47 ns | 96.10 ns | MediatR ~1.6x faster |
| Query with return value | 126.63 ns | 49.29 ns | MediatR ~2.6x faster |
| Query ultra-local API | 66.94 ns | 49.29 ns | MediatR ~1.4x faster |
| 10 concurrent commands | 1,244.58 ns | 497.81 ns | MediatR ~2.5x faster |
| 100 concurrent commands | 12,107.20 ns | 4,797.88 ns | MediatR ~2.5x faster |

### Dispatch vs Wolverine (InvokeAsync parity)

Source: `benchmarks/baselines/net10.0/dispatch-comparative-20260219/results/Excalibur.Dispatch.Benchmarks.Comparative.WolverineInProcessComparisonBenchmarks-report-github.md`

| Scenario | Dispatch | Wolverine (Invoke/local) | Relative Result |
|----------|----------|---------------------------|-----------------|
| Single command | 70.31 ns | 193.14 ns | Dispatch ~2.7x faster |
| Notification to 2 handlers | 75.95 ns | 4,138.96 ns | Dispatch ~54.5x faster |
| Query with return | 97.43 ns | 259.53 ns | Dispatch ~2.7x faster |
| 10 concurrent commands | 747.27 ns | 1,985.19 ns | Dispatch ~2.7x faster |
| 100 concurrent commands | 7,033.04 ns | 19,976.37 ns | Dispatch ~2.8x faster |

### Dispatch vs MassTransit Mediator (in-process)

Source: `benchmarks/baselines/net10.0/dispatch-comparative-20260219/results/Excalibur.Dispatch.Benchmarks.Comparative.MassTransitMediatorComparisonBenchmarks-report-github.md`

| Scenario | Dispatch | MassTransit Mediator | Relative Result |
|----------|----------|----------------------|-----------------|
| Single command | 67.20 ns | 1,185.70 ns | Dispatch ~17.7x faster |
| Notification to 2 consumers | 83.36 ns | 1,705.15 ns | Dispatch ~20.4x faster |
| Query with return | 95.19 ns | 18,771.04 ns | Dispatch ~197.2x faster |
| 10 concurrent commands | 779.68 ns | 11,924.61 ns | Dispatch ~15.3x faster |
| 100 concurrent commands | 7,037.86 ns | 120,396.41 ns | Dispatch ~17.1x faster |

## Track B: Queued/Bus End-to-End Parity

### Dispatch vs Wolverine vs MassTransit (queued end-to-end parity)

Source: `benchmarks/baselines/net10.0/dispatch-comparative-20260219/results/Excalibur.Dispatch.Benchmarks.Comparative.TransportQueueParityComparisonBenchmarks-report-github.md`

| Scenario | Dispatch (remote route) | Wolverine | MassTransit | Relative Result |
|----------|--------------------------|-----------|-------------|-----------------|
| Queued command end-to-end | 1.317 us | 4.005 us | 22.655 us | Dispatch ~3.0x faster than Wolverine, ~17.2x faster than MassTransit |
| Queued event fan-out end-to-end | 1.362 us | 3.943 us | 23.184 us | Dispatch ~2.9x faster than Wolverine, ~17.0x faster than MassTransit |
| Queued commands end-to-end (10 concurrent) | 7.132 us | 39.655 us | 147.692 us | Dispatch ~5.6x faster than Wolverine, ~20.7x faster than MassTransit |

:::warning Interpretation Guardrail
Use Track A for closest in-process handler overhead parity. Use Track B when comparing queued/bus completion semantics.
:::

## Routing-First Local + Hybrid Parity

Source: `benchmarks/baselines/net10.0/dispatch-comparative-20260219/results/Excalibur.Dispatch.Benchmarks.Comparative.RoutingFirstParityBenchmarks-report-github.md`

| Scenario | Mean | Relative to local command |
|----------|------|---------------------------|
| Dispatch: pre-routed local command | 106.0 ns | baseline |
| Dispatch: pre-routed local query | 141.3 ns | +33.3% |
| Dispatch: pre-routed remote event (AWS SQS) | 183.3 ns | +72.9% |
| Dispatch: pre-routed remote event (Azure Service Bus) | 191.8 ns | +81.0% |
| Dispatch: pre-routed remote event (Kafka) | 189.1 ns | +78.4% |
| Dispatch: pre-routed remote event (RabbitMQ) | 184.1 ns | +73.7% |
| Dispatch: pre-routed Kafka observability profile | 321.2 ns | +203.0% |
| Dispatch: pre-routed RabbitMQ observability profile | 321.4 ns | +203.2% |

## Running These Comparisons

```bash
# Build once
dotnet build benchmarks/Excalibur.Dispatch.Benchmarks/Excalibur.Dispatch.Benchmarks.csproj -c Release --nologo -v minimal

# Track A (in-process parity)
pwsh ./eng/run-benchmark-matrix.ps1 -NoBuild -NoRestore -Classes MediatRComparisonBenchmarks,WolverineInProcessComparisonBenchmarks,MassTransitMediatorComparisonBenchmarks

# Track B (queued/bus end-to-end parity)
pwsh ./eng/run-benchmark-matrix.ps1 -NoBuild -NoRestore -Classes TransportQueueParityComparisonBenchmarks
```

Results are written to `BenchmarkDotNet.Artifacts/results/` unless `-ArtifactsPath` is provided.
