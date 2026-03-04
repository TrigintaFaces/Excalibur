```

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7840)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.103
  [Host] : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=comparative-inproc  Toolchain=InProcessEmitToolchain  

```
| Method                                                          | Mean       | Error     | StdDev     | Ratio  | RatioSD | Gen0   | Allocated | Alloc Ratio |
|---------------------------------------------------------------- |-----------:|----------:|-----------:|-------:|--------:|-------:|----------:|------------:|
| &#39;Dispatch (remote): queued command end-to-end&#39;                  |   1.385 μs | 0.0277 μs |  0.0319 μs |   1.00 |    0.03 | 0.0114 |     884 B |        1.00 |
| &#39;Wolverine: queued command end-to-end (SendAsync)&#39;              |   3.811 μs | 0.0648 μs |  0.0929 μs |   2.75 |    0.09 | 0.0534 |    4512 B |        5.10 |
| &#39;MassTransit: queued command end-to-end (Publish)&#39;              |  27.443 μs | 0.7367 μs |  2.0899 μs |  19.83 |    1.57 | 0.2747 |   22101 B |       25.00 |
| &#39;Dispatch (remote): queued event fan-out end-to-end&#39;            |   1.449 μs | 0.0275 μs |  0.0257 μs |   1.05 |    0.03 | 0.0114 |     887 B |        1.00 |
| &#39;Wolverine: queued event fan-out end-to-end (PublishAsync)&#39;     |   3.881 μs | 0.0758 μs |  0.1112 μs |   2.80 |    0.10 | 0.0534 |    4512 B |        5.10 |
| &#39;MassTransit: queued event fan-out end-to-end (Publish)&#39;        |  34.697 μs | 1.1003 μs |  3.2270 μs |  25.07 |    2.39 | 0.4883 |   39393 B |       44.56 |
| &#39;Dispatch (remote): queued commands end-to-end (10 concurrent)&#39; |   7.396 μs | 0.1471 μs |  0.2799 μs |   5.34 |    0.23 | 0.0763 |    5997 B |        6.78 |
| &#39;Wolverine: queued commands end-to-end (10 concurrent)&#39;         |  39.445 μs | 0.6782 μs |  0.9508 μs |  28.50 |    0.93 | 0.5493 |   45609 B |       51.59 |
| &#39;MassTransit: queued commands end-to-end (10 concurrent)&#39;       | 230.753 μs | 6.5008 μs | 19.0658 μs | 166.71 |   14.21 | 2.4414 |  219147 B |      247.90 |
