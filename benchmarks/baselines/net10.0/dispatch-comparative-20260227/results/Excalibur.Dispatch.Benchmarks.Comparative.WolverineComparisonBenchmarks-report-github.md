```

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7840)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.103
  [Host] : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=comparative-inproc  Toolchain=InProcessEmitToolchain  

```
| Method                                    | Mean         | Error      | StdDev       | Ratio  | RatioSD | Gen0   | Gen1   | Gen2   | Allocated | Alloc Ratio |
|------------------------------------------ |-------------:|-----------:|-------------:|-------:|--------:|-------:|-------:|-------:|----------:|------------:|
| &#39;Dispatch: Single command&#39;                |     72.64 ns |   1.482 ns |     2.476 ns |   1.00 |    0.05 | 0.0015 |      - |      - |     120 B |        1.00 |
| &#39;Wolverine: Single command (InvokeAsync)&#39; |    239.56 ns |   4.784 ns |    11.462 ns |   3.30 |    0.19 | 0.0086 |      - |      - |     688 B |        5.73 |
| &#39;Wolverine: Single command (SendAsync)&#39;   |  3,926.69 ns |  77.070 ns |    91.746 ns |  54.12 |    2.21 | 0.0534 |      - |      - |    4512 B |       37.60 |
| &#39;Dispatch: Event to 2 handlers&#39;           |     82.53 ns |   1.685 ns |     2.722 ns |   1.14 |    0.05 | 0.0018 |      - |      - |     144 B |        1.20 |
| &#39;Wolverine: Event publish&#39;                |  3,889.38 ns |  73.936 ns |    75.926 ns |  53.60 |    2.08 | 0.0534 |      - |      - |    4512 B |       37.60 |
| &#39;Dispatch: 10 concurrent commands&#39;        |    766.69 ns |  14.826 ns |    19.278 ns |  10.57 |    0.44 | 0.0114 |      - |      - |     912 B |        7.60 |
| &#39;Wolverine: 10 concurrent commands&#39;       |  2,570.20 ns |  51.339 ns |   106.024 ns |  35.42 |    1.88 | 0.0916 |      - |      - |    7088 B |       59.07 |
| &#39;Dispatch: Query with return value&#39;       |    126.87 ns |   2.572 ns |     7.502 ns |   1.75 |    0.12 | 0.0045 |      - |      - |     352 B |        2.93 |
| &#39;Wolverine: Query with return value&#39;      |    332.98 ns |   6.611 ns |    13.050 ns |   4.59 |    0.24 | 0.0119 |      - |      - |     936 B |        7.80 |
| &#39;Dispatch: 100 concurrent commands&#39;       |  7,224.72 ns | 136.114 ns |   127.321 ns |  99.57 |    3.77 | 0.1144 | 0.0153 | 0.0153 |    7392 B |       61.60 |
| &#39;Wolverine: 100 concurrent commands&#39;      | 24,862.26 ns | 493.173 ns | 1,152.775 ns | 342.65 |   19.58 | 0.8850 |      - |      - |         - |        0.00 |
| &#39;Dispatch: Batch queries (10)&#39;            |  1,273.85 ns |  24.961 ns |    35.798 ns |  17.56 |    0.77 | 0.0362 |      - |      - |    2872 B |       23.93 |
| &#39;Wolverine: Batch queries (10)&#39;           |  3,289.26 ns |  62.834 ns |   166.627 ns |  45.33 |    2.75 | 0.1411 |      - |      - |    8312 B |       69.27 |
