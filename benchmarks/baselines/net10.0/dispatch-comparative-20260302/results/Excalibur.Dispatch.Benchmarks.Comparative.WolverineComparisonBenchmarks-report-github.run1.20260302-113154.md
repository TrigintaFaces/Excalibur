```

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7840)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.103
  [Host] : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=comparative-inproc  Toolchain=InProcessEmitToolchain  

```
| Method                                    | Mean         | Error      | StdDev     | Ratio  | RatioSD | Gen0   | Gen1   | Allocated | Alloc Ratio |
|------------------------------------------ |-------------:|-----------:|-----------:|-------:|--------:|-------:|-------:|----------:|------------:|
| &#39;Dispatch: Single command&#39;                |     82.36 ns |   0.604 ns |   0.536 ns |   1.00 |    0.01 | 0.0139 |      - |     264 B |        1.00 |
| &#39;Dispatch: Single command (ultra-local)&#39;  |     39.20 ns |   0.298 ns |   0.264 ns |   0.48 |    0.00 | 0.0025 |      - |      48 B |        0.18 |
| &#39;Wolverine: Single command (InvokeAsync)&#39; |    199.55 ns |   3.318 ns |   2.590 ns |   2.42 |    0.03 | 0.0365 |      - |     688 B |        2.61 |
| &#39;Wolverine: Single command (SendAsync)&#39;   |  4,031.26 ns |  76.456 ns |  71.517 ns |  48.95 |    0.90 | 0.2365 |      - |    4512 B |       17.09 |
| &#39;Dispatch: Event to 2 handlers&#39;           |    126.63 ns |   1.780 ns |   1.578 ns |   1.54 |    0.02 | 0.0153 |      - |     288 B |        1.09 |
| &#39;Wolverine: Event publish&#39;                |  4,077.36 ns |  49.049 ns |  43.481 ns |  49.51 |    0.60 | 0.2365 |      - |    4512 B |       17.09 |
| &#39;Dispatch: 10 concurrent commands&#39;        |  1,012.41 ns |  19.950 ns |  29.242 ns |  12.29 |    0.36 | 0.1221 |      - |    2320 B |        8.79 |
| &#39;Wolverine: 10 concurrent commands&#39;       |  2,063.29 ns |  39.717 ns |  37.151 ns |  25.05 |    0.46 | 0.3738 |      - |    7088 B |       26.85 |
| &#39;Dispatch: Query with return value&#39;       |    102.41 ns |   2.048 ns |   3.127 ns |   1.24 |    0.04 | 0.0253 |      - |     480 B |        1.82 |
| &#39;Wolverine: Query with return value&#39;      |    250.22 ns |   4.717 ns |   5.047 ns |   3.04 |    0.06 | 0.0496 |      - |     936 B |        3.55 |
| &#39;Dispatch: 100 concurrent commands&#39;       |  8,834.33 ns | 148.626 ns | 131.753 ns | 107.27 |    1.69 | 1.1444 | 0.0305 |   21760 B |       82.42 |
| &#39;Wolverine: 100 concurrent commands&#39;      | 19,768.99 ns | 294.335 ns | 245.783 ns | 240.03 |    3.25 | 3.6926 |      - |   69728 B |      264.12 |
| &#39;Dispatch: Batch queries (10)&#39;            |  1,254.88 ns |  22.693 ns |  21.227 ns |  15.24 |    0.27 | 0.2174 |      - |    4120 B |       15.61 |
| &#39;Wolverine: Batch queries (10)&#39;           |  2,540.73 ns |  35.100 ns |  32.832 ns |  30.85 |    0.43 | 0.4387 |      - |    8312 B |       31.48 |
