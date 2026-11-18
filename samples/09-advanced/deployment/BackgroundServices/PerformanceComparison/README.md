# Performance Comparison - Delivery Guarantees

This example explains the performance trade-offs between the delivery guarantee levels.

## Performance Summary

The percentages and timings below are unverified historical illustrations. Their matched workload, runtime and source-epoch provenance has not been established; they are not current measurements or guarantees.

| Level | DB Round-Trips | Throughput | Failure Window |
|-------|----------------|------------|----------------|
| **AtLeastOnce** | Provider-, batching- and retry-dependent | Unverified ranking | Entire batch |
| **MinimizedWindow** | Provider-, batching- and retry-dependent | Earlier unverified ~50% lower estimate | Single message |

## Benchmark Results

Earlier figures attributed to `DeliveryGuaranteeBenchmarks.cs`, without verified matched provenance:

| Scenario | AtLeastOnce | MinimizedWindow |
|----------|-------------|-----------------|
| 100 messages | 12ms | 89ms |
| 1000 messages | 45ms | 823ms |

## Decision Matrix

| Scenario | Recommended Level |
|----------|-------------------|
| Idempotent handlers accepting batch redelivery | Evaluate AtLeastOnce |
| Financial transactions, audit logging | MinimizedWindow |

## Running the Example

```bash
cd samples/09-advanced/deployment/BackgroundServices/PerformanceComparison
dotnet run
```

## Running the Synthetic Delivery Fixture

Run the synthetic in-memory delivery bookkeeping benchmarks below. They do not publish messages, access a database, recover durable state or establish production delivery-mode throughput rankings. See the [dated fixture observations](../../../../../docs/performance/original-cdc-delivery-observations-2026-10-18.md) for workload scope and evidence limitations.

```bash
cd benchmarks/Excalibur.Dispatch.Benchmarks
dotnet run -c Release -- --filter "*DeliveryGuarantee*"
```
