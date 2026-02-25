```

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7705)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.102
  [Host]     : .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3


```
| Method              | MessageCount | Mean | Error | Ratio | RatioSD | Alloc Ratio |
|-------------------- |------------- |-----:|------:|------:|--------:|------------:|
| **GetAllEntries**       | **100**          |   **NA** |    **NA** |     **?** |       **?** |           **?** |
| GetStatistics       | 100          |   NA |    NA |     ? |       ? |           ? |
| CreateEntry         | 100          |   NA |    NA |     ? |       ? |           ? |
| IsAlreadyProcessed  | 100          |   NA |    NA |     ? |       ? |           ? |
| FullProcessingCycle | 100          |   NA |    NA |     ? |       ? |           ? |
|                     |              |      |       |       |         |             |
| **GetAllEntries**       | **1000**         |   **NA** |    **NA** |     **?** |       **?** |           **?** |
| GetStatistics       | 1000         |   NA |    NA |     ? |       ? |           ? |
| CreateEntry         | 1000         |   NA |    NA |     ? |       ? |           ? |
| IsAlreadyProcessed  | 1000         |   NA |    NA |     ? |       ? |           ? |
| FullProcessingCycle | 1000         |   NA |    NA |     ? |       ? |           ? |
|                     |              |      |       |       |         |             |
| **GetAllEntries**       | **10000**        |   **NA** |    **NA** |     **?** |       **?** |           **?** |
| GetStatistics       | 10000        |   NA |    NA |     ? |       ? |           ? |
| CreateEntry         | 10000        |   NA |    NA |     ? |       ? |           ? |
| IsAlreadyProcessed  | 10000        |   NA |    NA |     ? |       ? |           ? |
| FullProcessingCycle | 10000        |   NA |    NA |     ? |       ? |           ? |

Benchmarks with issues:
  InboxThroughputBenchmarks.GetAllEntries: DefaultJob [MessageCount=100]
  InboxThroughputBenchmarks.GetStatistics: DefaultJob [MessageCount=100]
  InboxThroughputBenchmarks.CreateEntry: DefaultJob [MessageCount=100]
  InboxThroughputBenchmarks.IsAlreadyProcessed: DefaultJob [MessageCount=100]
  InboxThroughputBenchmarks.FullProcessingCycle: DefaultJob [MessageCount=100]
  InboxThroughputBenchmarks.GetAllEntries: DefaultJob [MessageCount=1000]
  InboxThroughputBenchmarks.GetStatistics: DefaultJob [MessageCount=1000]
  InboxThroughputBenchmarks.CreateEntry: DefaultJob [MessageCount=1000]
  InboxThroughputBenchmarks.IsAlreadyProcessed: DefaultJob [MessageCount=1000]
  InboxThroughputBenchmarks.FullProcessingCycle: DefaultJob [MessageCount=1000]
  InboxThroughputBenchmarks.GetAllEntries: DefaultJob [MessageCount=10000]
  InboxThroughputBenchmarks.GetStatistics: DefaultJob [MessageCount=10000]
  InboxThroughputBenchmarks.CreateEntry: DefaultJob [MessageCount=10000]
  InboxThroughputBenchmarks.IsAlreadyProcessed: DefaultJob [MessageCount=10000]
  InboxThroughputBenchmarks.FullProcessingCycle: DefaultJob [MessageCount=10000]
