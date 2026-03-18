```

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7922)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.103
  [Host] : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=diag-default  PowerPlanMode=00000000-0000-0000-0000-000000000000  Toolchain=InProcessEmitToolchain  
IterationCount=8  LaunchCount=1  UnrollFactor=1  
WarmupCount=3  

```
| Method                                      | Mean     | Error    | StdDev   | Ratio | Gen0   | Allocated | Alloc Ratio |
|-------------------------------------------- |---------:|---------:|---------:|------:|-------:|----------:|------------:|
| &#39;HandlerInvoker: precompiled cache-hit&#39;     | 34.00 ns | 0.282 ns | 0.125 ns |  1.00 | 0.0051 |      96 B |        1.00 |
| &#39;HandlerInvoker: runtime fallback (cached)&#39; | 32.43 ns | 0.439 ns | 0.195 ns |  0.95 | 0.0051 |      96 B |        1.00 |
