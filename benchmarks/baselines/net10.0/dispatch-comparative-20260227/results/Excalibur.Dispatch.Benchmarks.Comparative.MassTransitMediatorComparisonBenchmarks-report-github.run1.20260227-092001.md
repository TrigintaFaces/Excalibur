```

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7840)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.103
  [Host] : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=comparative-inproc  Toolchain=InProcessEmitToolchain  

```
| Method                                                           | Mean          | Error        | StdDev       | Median        | Ratio    | RatioSD | Gen0   | Gen1   | Gen2   | Allocated | Alloc Ratio |
|----------------------------------------------------------------- |--------------:|-------------:|-------------:|--------------:|---------:|--------:|-------:|-------:|-------:|----------:|------------:|
| &#39;Dispatch (local): Single command&#39;                               |      74.72 ns |     1.526 ns |     1.875 ns |      74.15 ns |     1.00 |    0.03 | 0.0015 |      - |      - |     120 B |        1.00 |
| &#39;MassTransit Mediator (in-process): Single command&#39;              |   1,534.81 ns |    30.656 ns |    81.828 ns |   1,521.07 ns |    20.55 |    1.20 | 0.0610 |      - |      - |    3544 B |       29.53 |
| &#39;Dispatch (local): Notification to 2 handlers&#39;                   |      85.60 ns |     1.743 ns |     2.075 ns |      86.17 ns |     1.15 |    0.04 | 0.0018 |      - |      - |     144 B |        1.20 |
| &#39;MassTransit Mediator (in-process): Notification to 2 consumers&#39; |   1,826.65 ns |    23.757 ns |    21.060 ns |   1,822.77 ns |    24.46 |    0.65 | 0.0553 | 0.0019 | 0.0019 |         - |        0.00 |
| &#39;Dispatch (local): Query with return&#39;                            |     113.58 ns |     0.580 ns |     0.543 ns |     113.49 ns |     1.52 |    0.04 | 0.0045 |      - |      - |     352 B |        2.93 |
| &#39;MassTransit Mediator (in-process): Query with return&#39;           |  14,037.95 ns | 1,214.788 ns | 3,543.595 ns |  15,074.10 ns |   187.97 |   47.45 | 0.1450 |      - |      - |   11607 B |       96.72 |
| &#39;Dispatch (local): 10 concurrent commands&#39;                       |     775.44 ns |    13.845 ns |    11.561 ns |     778.50 ns |    10.38 |    0.29 | 0.0114 |      - |      - |     912 B |        7.60 |
| &#39;MassTransit Mediator (in-process): 10 concurrent commands&#39;      |  15,254.97 ns |   303.037 ns |   658.777 ns |  15,174.35 ns |   204.27 |   10.04 | 0.4578 |      - |      - |   35648 B |      297.07 |
| &#39;Dispatch (local): 100 concurrent commands&#39;                      |   7,166.23 ns |    74.467 ns |    66.013 ns |   7,162.26 ns |    95.96 |    2.47 | 0.1221 | 0.0076 | 0.0076 | 1601977 B |   13,349.81 |
| &#39;MassTransit Mediator (in-process): 100 concurrent commands&#39;     | 149,217.25 ns | 2,955.674 ns | 7,138.285 ns | 150,052.28 ns | 1,998.07 |  106.48 | 4.6387 |      - |      - |  355329 B |    2,961.07 |
