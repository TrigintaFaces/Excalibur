```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8117/25H2/2025Update/HudsonValley2)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.202
  [Host] : .NET 10.0.6 (10.0.6, 10.0.626.17701), X64 RyuJIT x86-64-v3

Job=comparative-inproc  PowerPlanMode=00000000-0000-0000-0000-000000000000  Toolchain=InProcessEmitToolchain  
InvocationCount=1  IterationCount=10  UnrollFactor=1  
WarmupCount=3  

```
| Method                                 | Mean         | Error        | StdDev      | Ratio    | RatioSD | Allocated | Alloc Ratio |
|--------------------------------------- |-------------:|-------------:|------------:|---------:|--------:|----------:|------------:|
| &#39;Dispatch: Single command&#39;             |     7.406 μs |     1.506 μs |   0.8960 μs |     1.01 |    0.16 |     264 B |        1.00 |
| &#39;MassTransit: Single command&#39;          |   299.083 μs |    50.687 μs |  30.1629 μs |    40.89 |    5.95 |   26144 B |       99.03 |
| &#39;Dispatch: Event to 2 handlers&#39;        |    24.160 μs |    13.732 μs |   9.0831 μs |     3.30 |    1.24 |    7008 B |       26.55 |
| &#39;MassTransit: Event to 2 consumers&#39;    |   372.160 μs |    69.935 μs |  46.2574 μs |    50.88 |    8.22 |   46784 B |      177.21 |
| &#39;Dispatch: 10 concurrent commands&#39;     |    20.222 μs |     3.702 μs |   2.2033 μs |     2.76 |    0.42 |    9040 B |       34.24 |
| &#39;MassTransit: 10 concurrent commands&#39;  | 1,123.889 μs |    42.855 μs |  25.5025 μs |   153.66 |   17.08 |  227064 B |      860.09 |
| &#39;Dispatch: 100 concurrent commands&#39;    |    63.050 μs |     5.745 μs |   3.7998 μs |     8.62 |    1.06 |   28432 B |      107.70 |
| &#39;MassTransit: 100 concurrent commands&#39; | 8,782.310 μs | 1,131.108 μs | 748.1576 μs | 1,200.70 |  163.53 | 2230280 B |    8,448.03 |
| &#39;Dispatch: Batch send (10)&#39;            |    16.133 μs |     2.716 μs |   1.6163 μs |     2.21 |    0.32 |    2208 B |        8.36 |
| &#39;MassTransit: Batch send (10)&#39;         |   961.550 μs |    90.459 μs |  47.3120 μs |   131.46 |   15.60 |  228584 B |      865.85 |
