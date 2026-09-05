```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.400
  [Host] : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3

Job=warmpath-inproc  PowerPlanMode=00000000-0000-0000-0000-000000000000  Toolchain=InProcessEmitToolchain  

```
| Method                                                           | Mean          | Error        | StdDev       | Median        | Ratio    | RatioSD | Gen0    | Allocated | Alloc Ratio |
|----------------------------------------------------------------- |--------------:|-------------:|-------------:|--------------:|---------:|--------:|--------:|----------:|------------:|
| &#39;Dispatch (local): Single command&#39;                               |      75.37 ns |     4.527 ns |    13.063 ns |      69.13 ns |     1.03 |    0.23 |  0.0098 |     184 B |        1.00 |
| &#39;MassTransit Mediator (ambient scope): Single command&#39;           |   1,275.66 ns |    25.384 ns |    64.612 ns |   1,267.20 ns |    17.35 |    2.63 |  0.1869 |    3544 B |       19.26 |
| &#39;MassTransit Mediator (scope per message): Single command&#39;       |   1,630.79 ns |    28.454 ns |    49.835 ns |   1,617.64 ns |    22.18 |    3.25 |  0.2289 |    4336 B |       23.57 |
| &#39;Dispatch (tuned direct-local): Single command&#39;                  |      76.60 ns |     1.106 ns |     1.034 ns |      76.59 ns |     1.04 |    0.15 |  0.0098 |     184 B |        1.00 |
| &#39;Dispatch (local): Notification to 2 handlers&#39;                   |     138.14 ns |     1.742 ns |     1.360 ns |     138.38 ns |     1.88 |    0.27 |  0.0098 |     184 B |        1.00 |
| &#39;MassTransit Mediator (in-process): Notification to 2 consumers&#39; |   1,765.57 ns |    28.519 ns |    25.281 ns |   1,761.26 ns |    24.01 |    3.45 |  0.2213 |    4176 B |       22.70 |
| &#39;Dispatch (local): Query with return&#39;                            |      87.63 ns |     3.978 ns |    11.024 ns |      84.20 ns |     1.19 |    0.23 |  0.0199 |     376 B |        2.04 |
| &#39;MassTransit Mediator (in-process): Query with return&#39;           |  11,426.39 ns | 1,070.271 ns | 3,105.049 ns |  10,824.11 ns |   155.40 |   47.91 |  0.6104 |   11601 B |       63.05 |
| &#39;Dispatch (local): 10 concurrent commands&#39;                       |     801.83 ns |     8.234 ns |     6.876 ns |     804.78 ns |    10.91 |    1.56 |  0.1183 |    2240 B |       12.17 |
| &#39;MassTransit Mediator (in-process): 10 concurrent commands&#39;      |  12,481.32 ns |   248.412 ns |   428.499 ns |  12,510.04 ns |   169.75 |   24.98 |  1.8921 |   35648 B |      193.74 |
| &#39;Dispatch (local): 100 concurrent commands&#39;                      |   7,650.61 ns |   127.774 ns |   106.697 ns |   7,669.00 ns |   104.05 |   14.96 |  1.0986 |   20960 B |      113.91 |
| &#39;MassTransit Mediator (in-process): 100 concurrent commands&#39;     | 125,098.21 ns | 2,465.552 ns | 4,866.760 ns | 125,591.81 ns | 1,701.36 |  252.31 | 18.7988 |  355329 B |    1,931.14 |
