# Performance Comparison - Delivery Guarantees

This example explains the performance trade-offs between the delivery guarantee levels.

## Performance Summary

The percentages and timings below are unverified historical illustrations. Their matched workload, runtime and source-epoch provenance has not been established; they are not current measurements or guarantees.

| Level | DB Round-Trips | Throughput | Failure Window |
|-------|----------------|------------|----------------|
| **AtLeastOnce** | 1 per batch | Unverified ranking | Entire batch |
| **MinimizedWindow** | N per batch | Earlier unverified ~50% lower estimate | Single message |

## Benchmark Results

Earlier figures attributed to `DeliveryGuaranteeBenchmarks.cs`, without verified matched provenance:

| Scenario | AtLeastOnce | MinimizedWindow |
|----------|-------------|-----------------|
| 100 messages | 12ms | 89ms |
| 1000 messages | 45ms | 823ms |

## Decision Matrix

| Scenario | Recommended Level |
|----------|-------------------|
| High throughput, idempotent handlers | AtLeastOnce |
| Financial transactions, audit logging | MinimizedWindow |

## Running the Example

```bash
cd samples/BackgroundServices/PerformanceComparison
dotnet run
```

## Running Actual Benchmarks

For real benchmark numbers on your hardware:

```bash
cd benchmarks/Excalibur.Dispatch.Benchmarks
dotnet run -c Release -- --filter *DeliveryGuarantee*
```
