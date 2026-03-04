```

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7840)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.103
  [Host] : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=comparative-inproc  Toolchain=InProcessEmitToolchain  

```
| Method                                                           | Mean          | Error        | StdDev       | Ratio    | RatioSD | Gen0    | Gen1   | Allocated | Alloc Ratio |
|----------------------------------------------------------------- |--------------:|-------------:|-------------:|---------:|--------:|--------:|-------:|----------:|------------:|
| &#39;Dispatch (local): Single command&#39;                               |      96.07 ns |     1.722 ns |     1.527 ns |     1.00 |    0.02 |  0.0186 |      - |     352 B |        1.00 |
| &#39;MassTransit Mediator (in-process): Single command&#39;              |   1,175.07 ns |    23.455 ns |    54.825 ns |    12.23 |    0.60 |  0.1869 |      - |    3544 B |       10.07 |
| &#39;Dispatch (local): Notification to 2 handlers&#39;                   |     142.63 ns |     1.356 ns |     1.202 ns |     1.48 |    0.03 |  0.0198 |      - |     376 B |        1.07 |
| &#39;MassTransit Mediator (in-process): Notification to 2 consumers&#39; |   1,701.90 ns |    34.054 ns |    57.826 ns |    17.72 |    0.65 |  0.2213 | 0.0019 |    4176 B |       11.86 |
| &#39;Dispatch (local): Query with return&#39;                            |     111.62 ns |     1.003 ns |     0.938 ns |     1.16 |    0.02 |  0.0302 |      - |     568 B |        1.61 |
| &#39;MassTransit Mediator (in-process): Query with return&#39;           |   6,476.29 ns |   126.820 ns |   228.683 ns |    67.43 |    2.57 |  0.6104 |      - |   11600 B |       32.95 |
| &#39;Dispatch (local): 10 concurrent commands&#39;                       |   1,161.81 ns |    14.634 ns |    12.972 ns |    12.10 |    0.23 |  0.1698 |      - |    3200 B |        9.09 |
| &#39;MassTransit Mediator (in-process): 10 concurrent commands&#39;      |  12,114.48 ns |   241.058 ns |   544.108 ns |   126.13 |    5.95 |  1.8921 |      - |   35648 B |      101.27 |
| &#39;Dispatch (local): 100 concurrent commands&#39;                      |  10,954.26 ns |   121.735 ns |   107.915 ns |   114.05 |    2.07 |  1.6174 | 0.0458 |   30560 B |       86.82 |
| &#39;MassTransit Mediator (in-process): 100 concurrent commands&#39;     | 115,241.17 ns | 2,287.241 ns | 2,974.060 ns | 1,199.81 |   35.54 | 18.7988 | 0.1221 |  355329 B |    1,009.46 |
