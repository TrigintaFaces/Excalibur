```

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7922)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.103
  [Host] : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=warmpath-inproc  PowerPlanMode=00000000-0000-0000-0000-000000000000  Toolchain=InProcessEmitToolchain  

```
| Method                                    | Mean         | Error      | StdDev     | Ratio  | RatioSD | Gen0   | Gen1   | Allocated | Alloc Ratio |
|------------------------------------------ |-------------:|-----------:|-----------:|-------:|--------:|-------:|-------:|----------:|------------:|
| &#39;Dispatch: Single command&#39;                |     51.44 ns |   0.500 ns |   0.443 ns |   1.00 |    0.01 | 0.0102 |      - |     192 B |        1.00 |
| &#39;Dispatch: Single command (ultra-local)&#39;  |     38.00 ns |   0.410 ns |   0.342 ns |   0.74 |    0.01 | 0.0025 |      - |      48 B |        0.25 |
| &#39;Wolverine: Single command (InvokeAsync)&#39; |    342.08 ns |   5.729 ns |   5.359 ns |   6.65 |    0.11 | 0.0362 |      - |     688 B |        3.58 |
| &#39;Wolverine: Single command (SendAsync)&#39;   |  7,486.56 ns |  32.163 ns |  30.086 ns | 145.55 |    1.33 | 0.2365 |      - |    4512 B |       23.50 |
| &#39;Dispatch: Event to 2 handlers&#39;           |    209.58 ns |   1.971 ns |   1.747 ns |   4.07 |    0.05 | 0.0153 |      - |     288 B |        1.50 |
| &#39;Wolverine: Event publish&#39;                |  7,544.87 ns |  25.206 ns |  19.679 ns | 146.69 |    1.26 | 0.2289 |      - |    4512 B |       23.50 |
| &#39;Dispatch: 10 concurrent commands&#39;        |  1,024.08 ns |   7.793 ns |   6.908 ns |  19.91 |    0.21 | 0.0839 |      - |    1600 B |        8.33 |
| &#39;Wolverine: 10 concurrent commands&#39;       |  3,569.57 ns |  61.508 ns |  57.535 ns |  69.40 |    1.22 | 0.3738 |      - |    7088 B |       36.92 |
| &#39;Dispatch: Query with return value&#39;       |    120.07 ns |   2.431 ns |   2.894 ns |   2.33 |    0.06 | 0.0203 |      - |     384 B |        2.00 |
| &#39;Wolverine: Query with return value&#39;      |    450.98 ns |   5.573 ns |   4.940 ns |   8.77 |    0.12 | 0.0496 |      - |     936 B |        4.88 |
| &#39;Dispatch: 100 concurrent commands&#39;       |  9,812.66 ns | 153.883 ns | 136.413 ns | 190.78 |    3.01 | 0.7629 | 0.0153 |   14560 B |       75.83 |
| &#39;Wolverine: 100 concurrent commands&#39;      | 36,062.01 ns | 389.103 ns | 363.967 ns | 701.11 |    8.96 | 3.6621 |      - |   69728 B |      363.17 |
| &#39;Dispatch: Batch queries (10)&#39;            |  1,312.89 ns |  26.066 ns |  31.030 ns |  25.53 |    0.63 | 0.1678 |      - |    3160 B |       16.46 |
| &#39;Wolverine: Batch queries (10)&#39;           |  4,526.46 ns |  45.869 ns |  42.906 ns |  88.00 |    1.09 | 0.4349 |      - |    8312 B |       43.29 |
