# Performance Comparison - Delivery Guarantees

This example explains the performance trade-offs between the delivery guarantee levels.

## Performance Summary

| Level | DB Round-Trips | Throughput | Failure Window |
|-------|----------------|------------|----------------|
| **AtLeastOnce** | 1 per batch | Highest | Entire batch |
| **MinimizedWindow** | N per batch | ~50% lower | Single message |

## Benchmark Results

From benchmark suite (`DeliveryGuaranteeBenchmarks.cs`):

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
