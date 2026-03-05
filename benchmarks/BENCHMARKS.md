# Excalibur Benchmark Baseline (Latest Sync)

This file summarizes the current committed comparative baselines used by docs.

## Run Metadata

- Date: March 2, 2026
- Runtime: .NET 10.0.3
- Tooling: BenchmarkDotNet v0.15.4
- Baseline folder: `benchmarks/baselines/net10.0/dispatch-comparative-20260302/results/`

## Comparative Snapshot

### Track A: In-Process Parity

#### Dispatch vs MediatR

| Scenario | Dispatch | MediatR |
|----------|----------|---------|
| Single command handler | 75.32 ns | 47.27 ns |
| Single command ultra-local API | 31.54 ns | 47.27 ns |
| Notification to 3 handlers | 118.65 ns | 119.24 ns |
| Query with return value | 83.57 ns | 62.38 ns |
| Query ultra-local API | 58.27 ns | 62.38 ns |
| 10 concurrent commands | 879.24 ns | 544.39 ns |
| 100 concurrent commands | 7,539.10 ns | 5,160.23 ns |

#### Dispatch vs Wolverine (Invoke/local in-process)

| Scenario | Dispatch | Wolverine |
|----------|----------|-----------|
| Single command | 132.26 ns | 368.19 ns |
| Notification to 2 handlers | 219.40 ns | 3,954.40 ns |
| Query with return | 96.88 ns | 289.44 ns |
| 10 concurrent commands | 940.32 ns | 2,192.44 ns |
| 100 concurrent commands | 8,249.13 ns | 22,060.96 ns |

#### Dispatch vs MassTransit Mediator (in-process)

| Scenario | Dispatch | MassTransit Mediator |
|----------|----------|----------------------|
| Single command | 178.2 ns | 4,120.8 ns |
| Notification to 2 consumers | 261.5 ns | 5,742.8 ns |
| Query with return | 117.7 ns | 6,553.7 ns |
| 10 concurrent commands | 1,196.9 ns | 14,750.7 ns |
| 100 concurrent commands | 10,905.2 ns | 147,353.3 ns |

### Track B: Queued/Bus Semantics

#### Dispatch vs Wolverine vs MassTransit (end-to-end queued parity)

Source: `benchmarks/baselines/net10.0/dispatch-comparative-20260302/results/Excalibur.Dispatch.Benchmarks.Comparative.TransportQueueParityComparisonBenchmarks-report-github.md`

| Scenario | Dispatch (remote route) | Wolverine | MassTransit |
|----------|--------------------------|-----------|-------------|
| Queued command end-to-end | 1.147 us | 4.305 us | 14.141 us |
| Queued event fan-out end-to-end | 1.241 us | 3.949 us | 26.065 us |
| Queued commands end-to-end (10 concurrent) | 6.249 us | 39.326 us | 132.652 us |

## Routing-First Parity Snapshot

| Scenario | Mean |
|----------|------|
| Pre-routed local command | 78.17 ns |
| Pre-routed local query | 93.86 ns |
| Pre-routed remote event (AWS SQS) | 157.17 ns |
| Pre-routed remote event (Azure Service Bus) | 167.66 ns |
| Pre-routed remote event (Kafka) | 163.22 ns |
| Pre-routed remote event (RabbitMQ) | 159.09 ns |

## Notes

- Use Track A for closest handler-overhead parity discussions.
- Use Track B for queue/bus architecture path comparisons.
- Dispatch and competitor rows are measured with the same benchmark harness per class.
