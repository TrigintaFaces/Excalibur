```

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7922)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.103
  [Host] : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=diag-default  PowerPlanMode=00000000-0000-0000-0000-000000000000  Toolchain=InProcessEmitToolchain  
IterationCount=8  LaunchCount=1  UnrollFactor=1  
WarmupCount=3  

```
| Method                                                   | Mean            | Error           | StdDev          | Ratio | RatioSD | Gen0   | Gen1   | Allocated | Alloc Ratio |
|--------------------------------------------------------- |----------------:|----------------:|----------------:|------:|--------:|-------:|-------:|----------:|------------:|
| &#39;Retry: succeeds on third attempt&#39;                       | 30,348,324.6 ns | 1,180,146.38 ns |   617,239.38 ns | 1.000 |    0.03 |      - |      - |    1176 B |        1.00 |
| &#39;Retry overhead: succeeds on third attempt (zero delay)&#39; |      2,414.7 ns |        52.76 ns |        23.42 ns | 0.000 |    0.00 | 0.0381 |      - |     728 B |        0.62 |
| &#39;Retry: exhausted failures&#39;                              | 30,186,597.7 ns | 1,949,479.87 ns | 1,019,615.67 ns | 0.995 |    0.04 |      - |      - |    2884 B |        2.45 |
| &#39;Retry overhead: exhausted failures (zero delay)&#39;        |      6,346.4 ns |       143.26 ns |        63.61 ns | 0.000 |    0.00 | 0.1144 |      - |    2208 B |        1.88 |
| &#39;Dispatch: faulting handler&#39;                             |      1,459.8 ns |         5.52 ns |         2.45 ns | 0.000 |    0.00 | 0.0591 |      - |    1136 B |        0.97 |
| &#39;Dispatch: cancellation in-flight&#39;                       |      5,101.2 ns |        94.49 ns |        49.42 ns | 0.000 |    0.00 | 0.1373 |      - |    2649 B |        2.25 |
| &#39;Dispatch: pre-canceled token&#39;                           |      2,101.9 ns |        16.63 ns |         8.70 ns | 0.000 |    0.00 | 0.0381 |      - |     784 B |        0.67 |
| &#39;Dead-letter: store message&#39;                             |        631.5 ns |        10.19 ns |         4.52 ns | 0.000 |    0.00 | 0.1249 | 0.0010 |    2352 B |        2.00 |
| &#39;Dead-letter: query + replay marker&#39;                     |      1,342.5 ns |        42.13 ns |        22.04 ns | 0.000 |    0.00 | 0.1411 |      - |    2664 B |        2.27 |
