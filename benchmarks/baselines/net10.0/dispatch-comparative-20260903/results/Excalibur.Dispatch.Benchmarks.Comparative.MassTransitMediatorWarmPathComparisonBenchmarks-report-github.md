```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.400
  [Host] : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3

Job=warmpath-inproc  PowerPlanMode=00000000-0000-0000-0000-000000000000  Toolchain=InProcessEmitToolchain  

```
| Method                                                           | Mean          | Error        | StdDev       | Ratio    | RatioSD | Gen0    | Gen1   | Allocated | Alloc Ratio |
|----------------------------------------------------------------- |--------------:|-------------:|-------------:|---------:|--------:|--------:|-------:|----------:|------------:|
| &#39;Dispatch (local): Single command&#39;                               |      65.37 ns |     0.902 ns |     0.800 ns |     1.00 |    0.02 |  0.0098 |      - |     184 B |        1.00 |
| &#39;MassTransit Mediator (ambient scope): Single command&#39;           |   1,202.98 ns |    23.494 ns |    36.578 ns |    18.40 |    0.59 |  0.1869 |      - |    3544 B |       19.26 |
| &#39;MassTransit Mediator (scope per message): Single command&#39;       |   1,600.71 ns |    31.234 ns |    51.319 ns |    24.49 |    0.83 |  0.2289 |      - |    4336 B |       23.57 |
| &#39;Dispatch (tuned direct-local): Single command&#39;                  |      31.65 ns |     0.151 ns |     0.118 ns |     0.48 |    0.01 |  0.0013 |      - |      24 B |        0.13 |
| &#39;Dispatch (local): Notification to 2 handlers&#39;                   |     133.39 ns |     2.521 ns |     2.235 ns |     2.04 |    0.04 |  0.0098 |      - |     184 B |        1.00 |
| &#39;MassTransit Mediator (in-process): Notification to 2 consumers&#39; |   1,754.75 ns |    34.632 ns |    52.886 ns |    26.85 |    0.86 |  0.2213 |      - |    4176 B |       22.70 |
| &#39;Dispatch (local): Query with return&#39;                            |      80.33 ns |     1.229 ns |     1.089 ns |     1.23 |    0.02 |  0.0199 |      - |     376 B |        2.04 |
| &#39;MassTransit Mediator (in-process): Query with return&#39;           |   7,594.65 ns |   220.878 ns |   593.374 ns |   116.19 |    9.13 |  0.6180 | 0.0153 |   11602 B |       63.05 |
| &#39;Dispatch (local): 10 concurrent commands&#39;                       |     765.91 ns |     5.616 ns |     4.690 ns |    11.72 |    0.16 |  0.1183 |      - |    2240 B |       12.17 |
| &#39;MassTransit Mediator (in-process): 10 concurrent commands&#39;      |  12,319.94 ns |   237.005 ns |   263.431 ns |   188.48 |    4.53 |  1.8921 |      - |   35648 B |      193.74 |
| &#39;Dispatch (local): 100 concurrent commands&#39;                      |   7,339.57 ns |    39.263 ns |    36.727 ns |   112.29 |    1.44 |  1.1063 |      - |   20960 B |      113.91 |
| &#39;MassTransit Mediator (in-process): 100 concurrent commands&#39;     | 122,101.28 ns | 2,390.557 ns | 3,650.633 ns | 1,868.03 |   59.32 | 18.7988 |      - |  355329 B |    1,931.14 |
