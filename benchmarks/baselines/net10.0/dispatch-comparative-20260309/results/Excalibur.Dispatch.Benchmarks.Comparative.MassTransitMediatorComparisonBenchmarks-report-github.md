```

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7922)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.103
  [Host] : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=comparative-inproc  Toolchain=InProcessEmitToolchain  

```
| Method                                                           | Mean          | Error        | StdDev       | Median        | Ratio    | RatioSD | Gen0    | Gen1   | Allocated | Alloc Ratio |
|----------------------------------------------------------------- |--------------:|-------------:|-------------:|--------------:|---------:|--------:|--------:|-------:|----------:|------------:|
| &#39;Dispatch (local): Single command&#39;                               |      93.99 ns |     0.441 ns |     0.413 ns |      93.92 ns |     1.00 |    0.01 |  0.0186 |      - |     352 B |        1.00 |
| &#39;MassTransit Mediator (in-process): Single command&#39;              |   1,353.52 ns |    26.864 ns |    58.967 ns |   1,377.32 ns |    14.40 |    0.63 |  0.1869 |      - |    3544 B |       10.07 |
| &#39;Dispatch (local): Notification to 2 handlers&#39;                   |     141.23 ns |     1.336 ns |     1.185 ns |     141.58 ns |     1.50 |    0.01 |  0.0198 |      - |     376 B |        1.07 |
| &#39;MassTransit Mediator (in-process): Notification to 2 consumers&#39; |   1,881.38 ns |    37.343 ns |    64.415 ns |   1,898.00 ns |    20.02 |    0.68 |  0.2213 |      - |    4176 B |       11.86 |
| &#39;Dispatch (local): Query with return&#39;                            |     113.03 ns |     2.238 ns |     4.093 ns |     112.41 ns |     1.20 |    0.04 |  0.0288 |      - |     544 B |        1.55 |
| &#39;MassTransit Mediator (in-process): Query with return&#39;           |   7,436.58 ns |   436.863 ns | 1,111.955 ns |   7,129.45 ns |    79.12 |   11.76 |  0.6104 | 0.0153 |   11657 B |       33.12 |
| &#39;Dispatch (local): 10 concurrent commands&#39;                       |   1,183.69 ns |    11.953 ns |     9.981 ns |   1,180.39 ns |    12.59 |    0.12 |  0.1698 |      - |    3200 B |        9.09 |
| &#39;MassTransit Mediator (in-process): 10 concurrent commands&#39;      |  13,725.82 ns |   273.617 ns |   383.573 ns |  13,882.81 ns |   146.03 |    4.06 |  1.8921 |      - |   35648 B |      101.27 |
| &#39;Dispatch (local): 100 concurrent commands&#39;                      |  10,332.10 ns |   114.801 ns |   101.768 ns |  10,346.40 ns |   109.93 |    1.15 |  1.6174 | 0.0458 |   30560 B |       86.82 |
| &#39;MassTransit Mediator (in-process): 100 concurrent commands&#39;     | 135,184.65 ns | 2,667.590 ns | 5,509.036 ns | 137,715.81 ns | 1,438.29 |   58.40 | 18.7988 |      - |  355330 B |    1,009.46 |
