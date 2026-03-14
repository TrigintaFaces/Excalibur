```

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7922)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.103
  [Host] : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=comparative-inproc  Toolchain=InProcessEmitToolchain  

```
| Method                                    | Mean         | Error      | StdDev     | Ratio  | RatioSD | Gen0   | Gen1   | Allocated | Alloc Ratio |
|------------------------------------------ |-------------:|-----------:|-----------:|-------:|--------:|-------:|-------:|----------:|------------:|
| &#39;Dispatch: Single command&#39;                |     78.98 ns |   0.635 ns |   0.563 ns |   1.00 |    0.01 | 0.0139 |      - |     264 B |        1.00 |
| &#39;Dispatch: Single command (ultra-local)&#39;  |     40.98 ns |   0.401 ns |   0.375 ns |   0.52 |    0.01 | 0.0025 |      - |      48 B |        0.18 |
| &#39;Wolverine: Single command (InvokeAsync)&#39; |    196.60 ns |   1.433 ns |   1.340 ns |   2.49 |    0.02 | 0.0365 |      - |     688 B |        2.61 |
| &#39;Wolverine: Single command (SendAsync)&#39;   |  4,033.36 ns |  36.997 ns |  34.607 ns |  51.07 |    0.55 | 0.2365 |      - |    4512 B |       17.09 |
| &#39;Dispatch: Event to 2 handlers&#39;           |    116.69 ns |   0.480 ns |   0.426 ns |   1.48 |    0.01 | 0.0153 |      - |     288 B |        1.09 |
| &#39;Wolverine: Event publish&#39;                |  4,065.69 ns |  21.834 ns |  20.423 ns |  51.48 |    0.44 | 0.2365 |      - |    4512 B |       17.09 |
| &#39;Dispatch: 10 concurrent commands&#39;        |    925.52 ns |   6.391 ns |   5.979 ns |  11.72 |    0.11 | 0.1230 |      - |    2320 B |        8.79 |
| &#39;Wolverine: 10 concurrent commands&#39;       |  2,060.70 ns |  25.786 ns |  22.859 ns |  26.09 |    0.33 | 0.3738 |      - |    7088 B |       26.85 |
| &#39;Dispatch: Query with return value&#39;       |     89.89 ns |   0.688 ns |   0.610 ns |   1.14 |    0.01 | 0.0242 |      - |     456 B |        1.73 |
| &#39;Wolverine: Query with return value&#39;      |    265.36 ns |   1.439 ns |   1.275 ns |   3.36 |    0.03 | 0.0496 |      - |     936 B |        3.55 |
| &#39;Dispatch: 100 concurrent commands&#39;       |  8,117.17 ns |  85.536 ns |  80.010 ns | 102.78 |    1.21 | 1.1444 | 0.0305 |   21760 B |       82.42 |
| &#39;Wolverine: 100 concurrent commands&#39;      | 20,777.18 ns | 233.058 ns | 218.002 ns | 263.09 |    3.23 | 3.6926 |      - |   69728 B |      264.12 |
| &#39;Dispatch: Batch queries (10)&#39;            |  1,076.51 ns |  14.693 ns |  13.744 ns |  13.63 |    0.19 | 0.2060 |      - |    3880 B |       14.70 |
| &#39;Wolverine: Batch queries (10)&#39;           |  2,660.35 ns |  24.514 ns |  22.930 ns |  33.69 |    0.37 | 0.4387 |      - |    8312 B |       31.48 |
