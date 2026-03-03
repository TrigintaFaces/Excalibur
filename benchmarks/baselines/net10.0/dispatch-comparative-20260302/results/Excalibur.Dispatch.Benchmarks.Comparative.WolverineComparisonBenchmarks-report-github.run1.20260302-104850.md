```

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7840)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.103
  [Host] : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=comparative-inproc  Toolchain=InProcessEmitToolchain  

```
| Method                                    | Mean         | Error      | StdDev     | Ratio  | RatioSD | Gen0   | Gen1   | Allocated | Alloc Ratio |
|------------------------------------------ |-------------:|-----------:|-----------:|-------:|--------:|-------:|-------:|----------:|------------:|
| &#39;Dispatch: Single command&#39;                |     82.41 ns |   1.670 ns |   2.925 ns |   1.00 |    0.05 | 0.0139 |      - |     264 B |        1.00 |
| &#39;Dispatch: Single command (ultra-local)&#39;  |     39.78 ns |   0.343 ns |   0.321 ns |   0.48 |    0.02 | 0.0025 |      - |      48 B |        0.18 |
| &#39;Wolverine: Single command (InvokeAsync)&#39; |    186.19 ns |   3.713 ns |   6.305 ns |   2.26 |    0.11 | 0.0365 |      - |     688 B |        2.61 |
| &#39;Wolverine: Single command (SendAsync)&#39;   |  3,915.35 ns |  26.261 ns |  24.564 ns |  47.57 |    1.62 | 0.2365 |      - |    4512 B |       17.09 |
| &#39;Dispatch: Event to 2 handlers&#39;           |    132.34 ns |   1.182 ns |   0.987 ns |   1.61 |    0.06 | 0.0153 |      - |     288 B |        1.09 |
| &#39;Wolverine: Event publish&#39;                |  4,067.45 ns |  72.299 ns |  64.092 ns |  49.41 |    1.82 | 0.2365 |      - |    4512 B |       17.09 |
| &#39;Dispatch: 10 concurrent commands&#39;        |    984.97 ns |  19.085 ns |  31.357 ns |  11.97 |    0.55 | 0.1221 |      - |    2320 B |        8.79 |
| &#39;Wolverine: 10 concurrent commands&#39;       |  1,901.38 ns |  23.202 ns |  21.703 ns |  23.10 |    0.82 | 0.3757 |      - |    7088 B |       26.85 |
| &#39;Dispatch: Query with return value&#39;       |    100.78 ns |   1.755 ns |   1.556 ns |   1.22 |    0.04 | 0.0254 |      - |     480 B |        1.82 |
| &#39;Wolverine: Query with return value&#39;      |    255.94 ns |   3.706 ns |   3.467 ns |   3.11 |    0.11 | 0.0496 |      - |     936 B |        3.55 |
| &#39;Dispatch: 100 concurrent commands&#39;       |  8,645.44 ns |  88.792 ns |  83.056 ns | 105.03 |    3.65 | 1.1444 | 0.0305 |   21760 B |       82.42 |
| &#39;Wolverine: 100 concurrent commands&#39;      | 18,648.70 ns | 262.135 ns | 245.201 ns | 226.55 |    8.12 | 3.6926 |      - |   69728 B |      264.12 |
| &#39;Dispatch: Batch queries (10)&#39;            |  1,200.14 ns |  20.091 ns |  40.124 ns |  14.58 |    0.69 | 0.2174 |      - |    4120 B |       15.61 |
| &#39;Wolverine: Batch queries (10)&#39;           |  2,587.24 ns |  49.396 ns |  56.884 ns |  31.43 |    1.25 | 0.4387 |      - |    8312 B |       31.48 |
