```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8117/25H2/2025Update/HudsonValley2)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.202
  [Host] : .NET 10.0.6 (10.0.6, 10.0.626.17701), X64 RyuJIT x86-64-v3

Job=warmpath-inproc  PowerPlanMode=00000000-0000-0000-0000-000000000000  Toolchain=InProcessEmitToolchain  

```
| Method                                                          | Mean       | Error     | StdDev     | Ratio  | RatioSD | Gen0    | Gen1   | Allocated | Alloc Ratio |
|---------------------------------------------------------------- |-----------:|----------:|-----------:|-------:|--------:|--------:|-------:|----------:|------------:|
| &#39;Dispatch (remote): queued command end-to-end&#39;                  |   1.464 μs | 0.0294 μs |  0.0866 μs |   1.00 |    0.08 |  0.0420 |      - |     793 B |        1.00 |
| &#39;Wolverine: queued command end-to-end (SendAsync)&#39;              |   6.927 μs | 0.1337 μs |  0.1373 μs |   4.75 |    0.30 |  0.2975 | 0.2594 |    5640 B |        7.11 |
| &#39;MassTransit: queued command end-to-end (Publish)&#39;              |  16.565 μs | 0.8444 μs |  2.2685 μs |  11.35 |    1.69 |  1.1597 | 0.0305 |   21992 B |       27.73 |
| &#39;Dispatch (remote): queued event fan-out end-to-end&#39;            |   1.435 μs | 0.0284 μs |  0.0612 μs |   0.98 |    0.07 |  0.0420 |      - |     796 B |        1.00 |
| &#39;Wolverine: queued event fan-out end-to-end (PublishAsync)&#39;     |   6.664 μs | 0.1257 μs |  0.2201 μs |   4.57 |    0.31 |  0.2975 | 0.2899 |    5616 B |        7.08 |
| &#39;MassTransit: queued event fan-out end-to-end (Publish)&#39;        |  31.394 μs | 1.0919 μs |  3.1503 μs |  21.51 |    2.50 |  2.0752 | 0.1221 |   39160 B |       49.38 |
| &#39;Dispatch (remote): queued commands end-to-end (10 concurrent)&#39; |   7.573 μs | 0.1507 μs |  0.2113 μs |   5.19 |    0.34 |  0.2594 |      - |    5116 B |        6.45 |
| &#39;Wolverine: queued commands end-to-end (10 concurrent)&#39;         |  66.782 μs | 1.2006 μs |  2.3700 μs |  45.76 |    3.16 |  2.9297 | 2.0752 |   56890 B |       71.74 |
| &#39;MassTransit: queued commands end-to-end (10 concurrent)&#39;       | 165.110 μs | 5.1821 μs | 15.1983 μs | 113.15 |   12.36 | 11.4746 | 0.7324 |  217839 B |      274.70 |
