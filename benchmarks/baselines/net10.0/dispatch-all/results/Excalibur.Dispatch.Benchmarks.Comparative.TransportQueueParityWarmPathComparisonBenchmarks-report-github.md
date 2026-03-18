```

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7922)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.103
  [Host] : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=warmpath-inproc  PowerPlanMode=00000000-0000-0000-0000-000000000000  Toolchain=InProcessEmitToolchain  

```
| Method                                                          | Mean       | Error     | StdDev    | Ratio  | RatioSD | Gen0    | Gen1   | Allocated | Alloc Ratio |
|---------------------------------------------------------------- |-----------:|----------:|----------:|-------:|--------:|--------:|-------:|----------:|------------:|
| &#39;Dispatch (remote): queued command end-to-end&#39;                  |   1.155 μs | 0.0194 μs | 0.0162 μs |   1.00 |    0.02 |  0.0381 |      - |     723 B |        1.00 |
| &#39;Wolverine: queued command end-to-end (SendAsync)&#39;              |   3.768 μs | 0.0398 μs | 0.0372 μs |   3.26 |    0.05 |  0.2365 |      - |    4512 B |        6.24 |
| &#39;MassTransit: queued command end-to-end (Publish)&#39;              |  22.982 μs | 0.3270 μs | 0.2899 μs |  19.90 |    0.36 |  1.0986 |      - |   22135 B |       30.62 |
| &#39;Dispatch (remote): queued event fan-out end-to-end&#39;            |   1.147 μs | 0.0130 μs | 0.0121 μs |   0.99 |    0.02 |  0.0381 |      - |     726 B |        1.00 |
| &#39;Wolverine: queued event fan-out end-to-end (PublishAsync)&#39;     |   3.740 μs | 0.0450 μs | 0.0421 μs |   3.24 |    0.06 |  0.2365 |      - |    4512 B |        6.24 |
| &#39;MassTransit: queued event fan-out end-to-end (Publish)&#39;        |  25.420 μs | 0.4563 μs | 0.7871 μs |  22.01 |    0.74 |  2.1057 | 0.1221 |   39416 B |       54.52 |
| &#39;Dispatch (remote): queued commands end-to-end (10 concurrent)&#39; |   6.351 μs | 0.0445 μs | 0.0395 μs |   5.50 |    0.08 |  0.2289 |      - |    4395 B |        6.08 |
| &#39;Wolverine: queued commands end-to-end (10 concurrent)&#39;         |  38.320 μs | 0.4000 μs | 0.3546 μs |  33.18 |    0.54 |  2.3804 |      - |   45609 B |       63.08 |
| &#39;MassTransit: queued commands end-to-end (10 concurrent)&#39;       | 131.688 μs | 1.6391 μs | 1.4530 μs | 114.02 |    1.96 | 11.4746 | 0.7324 |  219092 B |      303.03 |
