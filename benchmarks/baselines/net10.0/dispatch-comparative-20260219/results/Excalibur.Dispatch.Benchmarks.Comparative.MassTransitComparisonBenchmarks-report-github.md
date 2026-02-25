```

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7840)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.103
  [Host] : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=comparative-inproc  Toolchain=InProcessEmitToolchain  

```
| Method                                 | Mean            | Error         | StdDev         | Median          | Ratio     | RatioSD  | Gen0     | Gen1    | Allocated | Alloc Ratio |
|--------------------------------------- |----------------:|--------------:|---------------:|----------------:|----------:|---------:|---------:|--------:|----------:|------------:|
| &#39;Dispatch: Single command&#39;             |        64.84 ns |      0.745 ns |       0.661 ns |        65.04 ns |      1.00 |     0.01 |   0.0063 |       - |     120 B |        1.00 |
| &#39;MassTransit: Single command&#39;          |    22,488.77 ns |    329.636 ns |     275.261 ns |    22,436.51 ns |    346.87 |     5.35 |   1.0986 |       - |   22112 B |      184.27 |
| &#39;Dispatch: Event to 2 handlers&#39;        |        78.46 ns |      0.405 ns |       0.359 ns |        78.42 ns |      1.21 |     0.01 |   0.0076 |       - |     144 B |        1.20 |
| &#39;MassTransit: Event to 2 consumers&#39;    |    30,789.62 ns |    939.835 ns |   2,771.124 ns |    30,422.61 ns |    474.90 |    42.81 |   2.1057 |  0.1221 |   39407 B |      328.39 |
| &#39;Dispatch: 10 concurrent commands&#39;     |       728.23 ns |      4.914 ns |       4.596 ns |       728.74 ns |     11.23 |     0.13 |   0.0477 |       - |     912 B |        7.60 |
| &#39;MassTransit: 10 concurrent commands&#39;  |   152,226.28 ns |  4,915.966 ns |  14,417.677 ns |   147,355.35 ns |  2,347.93 |   222.57 |  11.4746 |  0.7324 |  219082 B |    1,825.68 |
| &#39;Dispatch: 100 concurrent commands&#39;    |     6,822.42 ns |     51.816 ns |      45.933 ns |     6,801.91 ns |    105.23 |     1.25 |   0.3891 |       - |    7392 B |       61.60 |
| &#39;MassTransit: 100 concurrent commands&#39; | 1,350,919.07 ns | 48,069.319 ns | 141,733.483 ns | 1,329,607.13 ns | 20,836.48 | 2,185.83 | 115.2344 | 25.3906 | 2184603 B |   18,205.03 |
| &#39;Dispatch: Batch send (10)&#39;            |       612.02 ns |      4.389 ns |       4.105 ns |       611.19 ns |      9.44 |     0.11 |   0.0248 |       - |     480 B |        4.00 |
| &#39;MassTransit: Batch send (10)&#39;         |   158,266.49 ns |  5,221.755 ns |  15,314.504 ns |   156,095.78 ns |  2,441.09 |   236.36 |  11.4746 |  0.7324 |  219316 B |    1,827.63 |
