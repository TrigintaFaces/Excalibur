```

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7705)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.102
  [Host]     : .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3


```
| Method            | Mean | Error | Ratio | RatioSD | Alloc Ratio |
|------------------ |-----:|------:|------:|--------:|------------:|
| StageSingleSmall  |   NA |    NA |     ? |       ? |           ? |
| StageSingleMedium |   NA |    NA |     ? |       ? |           ? |
| StageSingleLarge  |   NA |    NA |     ? |       ? |           ? |
| StageBatch10      |   NA |    NA |     ? |       ? |           ? |
| StageBatch100     |   NA |    NA |     ? |       ? |           ? |
| StageLargePayload |   NA |    NA |     ? |       ? |           ? |

Benchmarks with issues:
  OutboxStagingBenchmarks.StageSingleSmall: DefaultJob
  OutboxStagingBenchmarks.StageSingleMedium: DefaultJob
  OutboxStagingBenchmarks.StageSingleLarge: DefaultJob
  OutboxStagingBenchmarks.StageBatch10: DefaultJob
  OutboxStagingBenchmarks.StageBatch100: DefaultJob
  OutboxStagingBenchmarks.StageLargePayload: DefaultJob
