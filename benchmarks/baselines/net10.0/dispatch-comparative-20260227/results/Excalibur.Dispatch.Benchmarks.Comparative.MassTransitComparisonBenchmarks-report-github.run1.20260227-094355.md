```

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7840)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.103
  [Host] : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=comparative-inproc  Toolchain=InProcessEmitToolchain  

```
| Method                                 | Mean            | Error         | StdDev         | Ratio     | RatioSD  | Gen0    | Allocated | Alloc Ratio |
|--------------------------------------- |----------------:|--------------:|---------------:|----------:|---------:|--------:|----------:|------------:|
| &#39;Dispatch: Single command&#39;             |        73.20 ns |      1.465 ns |       2.192 ns |      1.00 |     0.04 |  0.0015 |     120 B |        1.00 |
| &#39;MassTransit: Single command&#39;          |    26,176.73 ns |  1,110.343 ns |   3,131.744 ns |    357.93 |    43.87 |  0.2441 |   22083 B |      184.03 |
| &#39;Dispatch: Event to 2 handlers&#39;        |        88.13 ns |      1.790 ns |       2.389 ns |      1.21 |     0.05 |  0.0018 |     144 B |        1.20 |
| &#39;MassTransit: Event to 2 consumers&#39;    |    35,134.89 ns |  1,333.661 ns |   3,932.330 ns |    480.43 |    55.33 |  0.4883 |   39382 B |      328.18 |
| &#39;Dispatch: 10 concurrent commands&#39;     |       789.54 ns |     15.562 ns |      16.651 ns |     10.80 |     0.39 |  0.0114 |     912 B |        7.60 |
| &#39;MassTransit: 10 concurrent commands&#39;  |   208,324.90 ns |  4,119.783 ns |   9,382.831 ns |  2,848.58 |   152.05 |  2.6855 |  219107 B |    1,825.89 |
| &#39;Dispatch: 100 concurrent commands&#39;    |     7,329.85 ns |    141.301 ns |     145.106 ns |    100.23 |     3.50 |  0.0916 |    7392 B |       61.60 |
| &#39;MassTransit: 100 concurrent commands&#39; | 1,855,137.78 ns | 45,715.807 ns | 134,794.099 ns | 25,366.68 | 1,978.48 | 27.3438 | 2185035 B |   18,208.62 |
| &#39;Dispatch: Batch send (10)&#39;            |       601.13 ns |      9.761 ns |       9.130 ns |      8.22 |     0.27 |  0.0057 |     480 B |        4.00 |
| &#39;MassTransit: Batch send (10)&#39;         |   219,621.34 ns |  4,868.849 ns |  14,279.492 ns |  3,003.05 |   213.20 |  2.6855 |  219268 B |    1,827.23 |
