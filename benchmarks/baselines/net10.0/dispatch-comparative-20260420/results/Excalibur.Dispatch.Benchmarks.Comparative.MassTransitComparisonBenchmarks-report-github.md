```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8117/25H2/2025Update/HudsonValley2)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.202
  [Host] : .NET 10.0.6 (10.0.6, 10.0.626.17701), X64 RyuJIT x86-64-v3

Job=comparative-inproc  PowerPlanMode=00000000-0000-0000-0000-000000000000  Toolchain=InProcessEmitToolchain  
InvocationCount=1  IterationCount=10  UnrollFactor=1  
WarmupCount=3  

```
| Method                                 | Mean        | Error      | StdDev     | Ratio  | RatioSD | Allocated | Alloc Ratio |
|--------------------------------------- |------------:|-----------:|-----------:|-------:|--------:|----------:|------------:|
| &#39;Dispatch: Single command&#39;             |    10.49 μs |   2.986 μs |   1.562 μs |   1.02 |    0.22 |     264 B |        1.00 |
| &#39;MassTransit: Single command&#39;          |   256.20 μs |  81.183 μs |  48.311 μs |  24.99 |    6.08 |   28160 B |      106.67 |
| &#39;Dispatch: Event to 2 handlers&#39;        |    14.82 μs |   1.740 μs |   1.035 μs |   1.45 |    0.25 |    6336 B |       24.00 |
| &#39;MassTransit: Event to 2 consumers&#39;    |   272.56 μs |  49.019 μs |  32.423 μs |  26.58 |    5.29 |   43568 B |      165.03 |
| &#39;Dispatch: 10 concurrent commands&#39;     |    28.01 μs |  19.469 μs |  12.877 μs |   2.73 |    1.29 |    3280 B |       12.42 |
| &#39;MassTransit: 10 concurrent commands&#39;  | 1,317.76 μs |  75.893 μs |  50.199 μs | 128.53 |   21.39 |  225720 B |      855.00 |
| &#39;Dispatch: 100 concurrent commands&#39;    |    65.36 μs |   3.710 μs |   2.454 μs |   6.38 |    1.06 |   28096 B |      106.42 |
| &#39;MassTransit: 100 concurrent commands&#39; | 9,103.87 μs | 328.193 μs | 217.079 μs | 887.97 |  145.59 | 2230536 B |    8,449.00 |
| &#39;Dispatch: Batch send (10)&#39;            |    18.85 μs |   6.158 μs |   4.073 μs |   1.84 |    0.49 |    3264 B |       12.36 |
| &#39;MassTransit: Batch send (10)&#39;         | 1,080.19 μs | 113.343 μs |  74.969 μs | 105.36 |   18.51 |  229400 B |      868.94 |
