```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8117/25H2/2025Update/HudsonValley2)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.202
  [Host] : .NET 10.0.6 (10.0.6, 10.0.626.17701), X64 RyuJIT x86-64-v3

Job=warmpath-inproc  PowerPlanMode=00000000-0000-0000-0000-000000000000  Toolchain=InProcessEmitToolchain  

```
| Method                                    | Mean         | Error      | StdDev     | Ratio  | RatioSD | Gen0   | Gen1   | Allocated | Alloc Ratio |
|------------------------------------------ |-------------:|-----------:|-----------:|-------:|--------:|-------:|-------:|----------:|------------:|
| &#39;Dispatch: Single command&#39;                |     74.90 ns |   0.535 ns |   0.500 ns |   1.00 |    0.01 | 0.0139 |      - |     264 B |        1.00 |
| &#39;Dispatch: Single command (ultra-local)&#39;  |     34.60 ns |   0.628 ns |   0.557 ns |   0.46 |    0.01 | 0.0013 |      - |      24 B |        0.09 |
| &#39;Wolverine: Single command (InvokeAsync)&#39; |    203.69 ns |   1.781 ns |   1.487 ns |   2.72 |    0.03 | 0.0365 |      - |     688 B |        2.61 |
| &#39;Wolverine: Single command (SendAsync)&#39;   |  6,505.46 ns |  41.800 ns |  39.100 ns |  86.86 |    0.76 | 0.2975 | 0.2747 |    5640 B |       21.36 |
| &#39;Dispatch: Event to 2 handlers&#39;           |    119.52 ns |   1.738 ns |   1.541 ns |   1.60 |    0.02 | 0.0153 |      - |     288 B |        1.09 |
| &#39;Wolverine: Event publish&#39;                |  6,535.26 ns | 116.530 ns | 109.002 ns |  87.26 |    1.52 | 0.2975 | 0.2823 |    5616 B |       21.27 |
| &#39;Dispatch: 10 concurrent commands&#39;        |    936.19 ns |   7.129 ns |   6.669 ns |  12.50 |    0.12 | 0.1221 |      - |    2320 B |        8.79 |
| &#39;Wolverine: 10 concurrent commands&#39;       |  2,155.39 ns |  27.978 ns |  24.802 ns |  28.78 |    0.37 | 0.3738 |      - |    7088 B |       26.85 |
| &#39;Dispatch: Query with return value&#39;       |     84.91 ns |   1.351 ns |   1.055 ns |   1.13 |    0.02 | 0.0242 |      - |     456 B |        1.73 |
| &#39;Wolverine: Query with return value&#39;      |    267.99 ns |   2.347 ns |   2.080 ns |   3.58 |    0.04 | 0.0496 |      - |     936 B |        3.55 |
| &#39;Dispatch: 100 concurrent commands&#39;       |  8,088.89 ns | 104.222 ns |  92.390 ns | 108.00 |    1.38 | 1.1444 | 0.0305 |   21760 B |       82.42 |
| &#39;Wolverine: 100 concurrent commands&#39;      | 20,954.48 ns | 221.378 ns | 207.077 ns | 279.78 |    3.23 | 3.6926 |      - |   69728 B |      264.12 |
| &#39;Dispatch: Batch queries (10)&#39;            |  1,073.27 ns |   8.841 ns |   6.902 ns |  14.33 |    0.13 | 0.2060 |      - |    3880 B |       14.70 |
| &#39;Wolverine: Batch queries (10)&#39;           |  3,112.46 ns |  35.366 ns |  31.351 ns |  41.56 |    0.49 | 0.4387 |      - |    8312 B |       31.48 |
