```

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7922)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.103
  [Host] : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=comparative-inproc  PowerPlanMode=00000000-0000-0000-0000-000000000000  Toolchain=InProcessEmitToolchain  
InvocationCount=1  IterationCount=3  UnrollFactor=1  

```
| Method                            | Mean     | Error    | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|---------------------------------- |---------:|---------:|----------:|------:|--------:|----------:|------------:|
| &#39;Dispatch: Container startup&#39;     |       NA |       NA |        NA |     ? |       ? |        NA |           ? |
| &#39;MediatR: Container startup&#39;      | 2.124 ms | 8.524 ms | 0.4672 ms |     ? |       ? |   3.07 MB |           ? |
| &#39;Dispatch: Startup + 10 handlers&#39; |       NA |       NA |        NA |     ? |       ? |        NA |           ? |
| &#39;MediatR: Startup + 10 handlers&#39;  | 2.063 ms | 7.860 ms | 0.4308 ms |     ? |       ? |   3.07 MB |           ? |

Benchmarks with issues:
  StartupComparisonBenchmarks.'Dispatch: Container startup': comparative-inproc(PowerPlanMode=00000000-0000-0000-0000-000000000000, Toolchain=InProcessEmitToolchain, InvocationCount=1, IterationCount=3, UnrollFactor=1)
  StartupComparisonBenchmarks.'Dispatch: Startup + 10 handlers': comparative-inproc(PowerPlanMode=00000000-0000-0000-0000-000000000000, Toolchain=InProcessEmitToolchain, InvocationCount=1, IterationCount=3, UnrollFactor=1)
