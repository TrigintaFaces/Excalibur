```

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7922)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.103
  [Host] : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=comparative-inproc  Toolchain=InProcessEmitToolchain  

```
| Method                                                          | Mean       | Error     | StdDev    | Median     | Ratio | RatioSD | Gen0    | Gen1   | Allocated | Alloc Ratio |
|---------------------------------------------------------------- |-----------:|----------:|----------:|-----------:|------:|--------:|--------:|-------:|----------:|------------:|
| &#39;Dispatch (remote): queued command end-to-end&#39;                  |   1.426 μs | 0.0284 μs | 0.0652 μs |   1.433 μs |  1.00 |    0.06 |  0.0458 |      - |     891 B |        1.00 |
| &#39;Wolverine: queued command end-to-end (SendAsync)&#39;              |   4.003 μs | 0.0393 μs | 0.0348 μs |   4.014 μs |  2.81 |    0.13 |  0.2365 |      - |    4512 B |        5.06 |
| &#39;MassTransit: queued command end-to-end (Publish)&#39;              |  13.738 μs | 0.5872 μs | 1.5876 μs |  13.268 μs |  9.65 |    1.20 |  1.1902 | 0.0458 |   22072 B |       24.77 |
| &#39;Dispatch (remote): queued event fan-out end-to-end&#39;            |   1.398 μs | 0.0278 μs | 0.0542 μs |   1.377 μs |  0.98 |    0.06 |  0.0439 |      - |     854 B |        0.96 |
| &#39;Wolverine: queued event fan-out end-to-end (PublishAsync)&#39;     |   4.082 μs | 0.0400 μs | 0.0355 μs |   4.078 μs |  2.87 |    0.13 |  0.2365 |      - |    4512 B |        5.06 |
| &#39;MassTransit: queued event fan-out end-to-end (Publish)&#39;        |  21.548 μs | 1.3254 μs | 3.9080 μs |  22.850 μs | 15.14 |    2.82 |  2.1057 | 0.1221 |   39416 B |       44.24 |
| &#39;Dispatch (remote): queued commands end-to-end (10 concurrent)&#39; |   7.268 μs | 0.1396 μs | 0.1371 μs |   7.199 μs |  5.11 |    0.25 |  0.3204 |      - |    6077 B |        6.82 |
| &#39;Wolverine: queued commands end-to-end (10 concurrent)&#39;         |  41.126 μs | 0.2732 μs | 0.2555 μs |  41.171 μs | 28.90 |    1.35 |  2.3804 |      - |   45609 B |       51.19 |
| &#39;MassTransit: queued commands end-to-end (10 concurrent)&#39;       | 127.642 μs | 0.9024 μs | 0.7536 μs | 127.622 μs | 89.68 |    4.18 | 11.4746 | 0.7324 |  219092 B |      245.89 |
