```

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7840)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.103
  [Host] : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=comparative-inproc  Toolchain=InProcessEmitToolchain  

```
| Method                                                  | Mean         | Error      | StdDev     | Median       | Ratio  | RatioSD | Gen0   | Gen1   | Gen2   | Allocated | Alloc Ratio |
|-------------------------------------------------------- |-------------:|-----------:|-----------:|-------------:|-------:|--------:|-------:|-------:|-------:|----------:|------------:|
| &#39;Dispatch (local): Single command&#39;                      |     74.41 ns |   1.482 ns |   1.706 ns |     74.48 ns |   1.00 |    0.03 | 0.0015 |      - |      - |     120 B |        1.00 |
| &#39;Wolverine (in-process): Single command InvokeAsync&#39;    |    241.58 ns |   4.804 ns |   9.140 ns |    241.40 ns |   3.25 |    0.14 | 0.0086 |      - |      - |     672 B |        5.60 |
| &#39;Dispatch (local): Notification to 2 handlers&#39;          |     87.07 ns |   1.734 ns |   2.255 ns |     86.54 ns |   1.17 |    0.04 | 0.0018 |      - |      - |     144 B |        1.20 |
| &#39;Wolverine (in-process): Notification to 2 handlers&#39;    |  3,967.96 ns |  77.849 ns | 103.926 ns |  3,981.93 ns |  53.35 |    1.82 | 0.0534 |      - |      - |    4512 B |       37.60 |
| &#39;Dispatch (local): Query with return&#39;                   |    130.36 ns |   2.557 ns |   6.319 ns |    129.94 ns |   1.75 |    0.09 | 0.0045 |      - |      - |     352 B |        2.93 |
| &#39;Wolverine (in-process): Query with return InvokeAsync&#39; |    324.47 ns |   7.044 ns |  20.771 ns |    323.57 ns |   4.36 |    0.29 | 0.0114 |      - |      - |     936 B |        7.80 |
| &#39;Dispatch (local): 10 concurrent commands&#39;              |    803.56 ns |  16.053 ns |  39.378 ns |    789.40 ns |  10.80 |    0.58 | 0.0162 |      - |      - |     912 B |        7.60 |
| &#39;Wolverine (in-process): 10 concurrent commands&#39;        |  2,559.40 ns |  50.935 ns | 138.572 ns |  2,561.55 ns |  34.41 |    2.01 | 0.0877 |      - |      - |    6928 B |       57.73 |
| &#39;Dispatch (local): 100 concurrent commands&#39;             |  7,386.27 ns | 112.339 ns | 105.082 ns |  7,400.02 ns |  99.31 |    2.62 | 0.1373 |      - |      - |    7392 B |       61.60 |
| &#39;Wolverine (in-process): 100 concurrent commands&#39;       | 24,843.14 ns | 494.807 ns | 917.157 ns | 24,752.63 ns | 334.02 |   14.33 | 0.9460 | 0.0305 | 0.0305 |         - |        0.00 |
