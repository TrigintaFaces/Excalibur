# Excalibur Benchmark Baseline (Latest Sync)

This file summarizes the current committed comparative baselines used by docs.

## Run Metadata

- Date: February 19, 2026
- Runtime: .NET 10.0.3
- Tooling: BenchmarkDotNet v0.15.4
- Baseline folder: `benchmarks/baselines/net10.0/dispatch-comparative-20260219/results/`

## Comparative Snapshot

### Track A: In-Process Parity

#### Dispatch vs MediatR

| Scenario | Dispatch | MediatR |
|----------|----------|---------|
| Single command handler | 118.79 ns | 40.92 ns |
| Single command ultra-local API | 47.12 ns | 40.92 ns |
| Notification to 3 handlers | 154.47 ns | 96.10 ns |
| Query with return value | 126.63 ns | 49.29 ns |
| Query ultra-local API | 66.94 ns | 49.29 ns |
| 10 concurrent commands | 1,244.58 ns | 497.81 ns |
| 100 concurrent commands | 12,107.20 ns | 4,797.88 ns |

#### Dispatch vs Wolverine (Invoke/local in-process)

| Scenario | Dispatch | Wolverine |
|----------|----------|-----------|
| Single command | 70.31 ns | 193.14 ns |
| Notification to 2 handlers | 75.95 ns | 4,138.96 ns |
| Query with return | 97.43 ns | 259.53 ns |
| 10 concurrent commands | 747.27 ns | 1,985.19 ns |
| 100 concurrent commands | 7,033.04 ns | 19,976.37 ns |

#### Dispatch vs MassTransit Mediator (in-process)

| Scenario | Dispatch | MassTransit Mediator |
|----------|----------|----------------------|
| Single command | 67.20 ns | 1,185.70 ns |
| Notification to 2 consumers | 83.36 ns | 1,705.15 ns |
| Query with return | 95.19 ns | 18,771.04 ns |
| 10 concurrent commands | 779.68 ns | 11,924.61 ns |
| 100 concurrent commands | 7,037.86 ns | 120,396.41 ns |

### Track B: Queued/Bus Semantics

#### Dispatch vs Wolverine vs MassTransit (end-to-end queued parity)

Source: `benchmarks/baselines/net10.0/dispatch-comparative-20260219/results/Excalibur.Dispatch.Benchmarks.Comparative.TransportQueueParityComparisonBenchmarks-report-github.md`

| Scenario | Dispatch (remote route) | Wolverine | MassTransit |
|----------|--------------------------|-----------|-------------|
| Queued command end-to-end | 1.317 us | 4.005 us | 22.655 us |
| Queued event fan-out end-to-end | 1.362 us | 3.943 us | 23.184 us |
| Queued commands end-to-end (10 concurrent) | 7.132 us | 39.655 us | 147.692 us |

## Routing-First Parity Snapshot

| Scenario | Mean |
|----------|------|
| Pre-routed local command | 106.0 ns |
| Pre-routed local query | 141.3 ns |
| Pre-routed remote event (AWS SQS) | 183.3 ns |
| Pre-routed remote event (Azure Service Bus) | 191.8 ns |
| Pre-routed remote event (Kafka) | 189.1 ns |
| Pre-routed remote event (RabbitMQ) | 184.1 ns |

## Notes

- Use Track A for closest handler-overhead parity discussions.
- Use Track B for queue/bus architecture path comparisons.
- Dispatch and competitor rows are measured with the same benchmark harness per class.
