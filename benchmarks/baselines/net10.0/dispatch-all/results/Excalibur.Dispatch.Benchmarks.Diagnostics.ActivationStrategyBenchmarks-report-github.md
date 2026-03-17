```

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7922)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.103
  [Host] : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=diag-default  PowerPlanMode=00000000-0000-0000-0000-000000000000  Toolchain=InProcessEmitToolchain  
IterationCount=8  LaunchCount=1  UnrollFactor=1  
WarmupCount=3  

```
| Method                                                | Mean        | Error     | StdDev    | Ratio  | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------------------------ |------------:|----------:|----------:|-------:|--------:|-------:|----------:|------------:|
| &#39;HandlerActivator (warm/frozen)&#39;                      |    19.26 ns |  0.062 ns |  0.027 ns |   1.00 |    0.00 | 0.0013 |      24 B |        1.00 |
| &#39;Reflection activator (naive per-call property scan)&#39; |    26.89 ns |  0.100 ns |  0.045 ns |   1.40 |    0.00 | 0.0030 |      56 B |        2.33 |
| &#39;Reflection activator (cached property lookup)&#39;       |    22.62 ns |  0.443 ns |  0.232 ns |   1.17 |    0.01 | 0.0013 |      24 B |        1.00 |
| &#39;HandlerActivator batch x100&#39;                         | 1,906.76 ns | 20.921 ns | 10.942 ns |  99.01 |    0.55 | 0.1259 |    2400 B |      100.00 |
| &#39;Reflection naive batch x100&#39;                         | 2,595.33 ns | 40.440 ns | 21.151 ns | 134.77 |    1.05 | 0.2975 |    5600 B |      233.33 |
