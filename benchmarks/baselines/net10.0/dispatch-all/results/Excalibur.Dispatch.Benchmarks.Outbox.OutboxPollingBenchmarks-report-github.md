```

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7705)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.102
  [Host]     : .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3


```
| Method        | MessageCount | Mean     | Error    | StdDev   | Ratio | RatioSD | Allocated | Alloc Ratio |
|-------------- |------------- |---------:|---------:|---------:|------:|--------:|----------:|------------:|
| **PollBatch10**   | **0**            |       **NA** |       **NA** |       **NA** |     **?** |       **?** |        **NA** |           **?** |
| PollBatch100  | 0            |       NA |       NA |       NA |     ? |       ? |        NA |           ? |
| PollBatch500  | 0            |       NA |       NA |       NA |     ? |       ? |        NA |           ? |
| GetStatistics | 0            | 667.5 μs | 15.96 μs | 44.76 μs |     ? |       ? |   8.05 KB |           ? |
|               |              |          |          |          |       |         |           |             |
| **PollBatch10**   | **10**           |       **NA** |       **NA** |       **NA** |     **?** |       **?** |        **NA** |           **?** |
| PollBatch100  | 10           |       NA |       NA |       NA |     ? |       ? |        NA |           ? |
| PollBatch500  | 10           |       NA |       NA |       NA |     ? |       ? |        NA |           ? |
| GetStatistics | 10           |       NA |       NA |       NA |     ? |       ? |        NA |           ? |
|               |              |          |          |          |       |         |           |             |
| **PollBatch10**   | **100**          |       **NA** |       **NA** |       **NA** |     **?** |       **?** |        **NA** |           **?** |
| PollBatch100  | 100          |       NA |       NA |       NA |     ? |       ? |        NA |           ? |
| PollBatch500  | 100          |       NA |       NA |       NA |     ? |       ? |        NA |           ? |
| GetStatistics | 100          |       NA |       NA |       NA |     ? |       ? |        NA |           ? |
|               |              |          |          |          |       |         |           |             |
| **PollBatch10**   | **1000**         |       **NA** |       **NA** |       **NA** |     **?** |       **?** |        **NA** |           **?** |
| PollBatch100  | 1000         |       NA |       NA |       NA |     ? |       ? |        NA |           ? |
| PollBatch500  | 1000         |       NA |       NA |       NA |     ? |       ? |        NA |           ? |
| GetStatistics | 1000         |       NA |       NA |       NA |     ? |       ? |        NA |           ? |

Benchmarks with issues:
  OutboxPollingBenchmarks.PollBatch10: DefaultJob [MessageCount=0]
  OutboxPollingBenchmarks.PollBatch100: DefaultJob [MessageCount=0]
  OutboxPollingBenchmarks.PollBatch500: DefaultJob [MessageCount=0]
  OutboxPollingBenchmarks.PollBatch10: DefaultJob [MessageCount=10]
  OutboxPollingBenchmarks.PollBatch100: DefaultJob [MessageCount=10]
  OutboxPollingBenchmarks.PollBatch500: DefaultJob [MessageCount=10]
  OutboxPollingBenchmarks.GetStatistics: DefaultJob [MessageCount=10]
  OutboxPollingBenchmarks.PollBatch10: DefaultJob [MessageCount=100]
  OutboxPollingBenchmarks.PollBatch100: DefaultJob [MessageCount=100]
  OutboxPollingBenchmarks.PollBatch500: DefaultJob [MessageCount=100]
  OutboxPollingBenchmarks.GetStatistics: DefaultJob [MessageCount=100]
  OutboxPollingBenchmarks.PollBatch10: DefaultJob [MessageCount=1000]
  OutboxPollingBenchmarks.PollBatch100: DefaultJob [MessageCount=1000]
  OutboxPollingBenchmarks.PollBatch500: DefaultJob [MessageCount=1000]
  OutboxPollingBenchmarks.GetStatistics: DefaultJob [MessageCount=1000]
