```

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7705)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.102
  [Host] : .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3

Job=.NET 9.0  Runtime=.NET 9.0  

```
| Method                             | Mean | Error | Ratio | RatioSD | Alloc Ratio |
|----------------------------------- |-----:|------:|------:|--------:|------------:|
| &#39;Dispatch: 3 middleware behaviors&#39; |   NA |    NA |     ? |       ? |           ? |

Benchmarks with issues:
  PipelineComparisonBenchmarks.'Dispatch: 3 middleware behaviors': .NET 9.0(Runtime=.NET 9.0)
