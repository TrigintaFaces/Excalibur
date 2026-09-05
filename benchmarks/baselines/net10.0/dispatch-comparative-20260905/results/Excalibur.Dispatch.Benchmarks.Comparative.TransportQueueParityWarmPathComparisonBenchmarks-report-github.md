```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.400
  [Host] : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3

Job=warmpath-inproc  PowerPlanMode=00000000-0000-0000-0000-000000000000  Toolchain=InProcessEmitToolchain  

```
| Method                                                          | Mean       | Error     | StdDev     | Ratio  | RatioSD | Gen0    | Gen1   | Allocated | Alloc Ratio |
|---------------------------------------------------------------- |-----------:|----------:|-----------:|-------:|--------:|--------:|-------:|----------:|------------:|
| &#39;Dispatch (remote): queued command end-to-end&#39;                  |   1.361 μs | 0.0269 μs |  0.0567 μs |   1.00 |    0.06 |  0.0420 |      - |     793 B |        1.00 |
| &#39;Wolverine: queued command end-to-end (SendAsync)&#39;              |   4.050 μs | 0.0793 μs |  0.0882 μs |   2.98 |    0.14 |  0.2289 |      - |    4400 B |        5.55 |
| &#39;MassTransit: queued command end-to-end (Publish)&#39;              |  16.457 μs | 0.8112 μs |  2.3662 μs |  12.11 |    1.80 |  1.0986 |      - |   22086 B |       27.85 |
| &#39;Dispatch (remote): queued event fan-out end-to-end&#39;            |   1.420 μs | 0.0279 μs |  0.0582 μs |   1.04 |    0.06 |  0.0420 |      - |     794 B |        1.00 |
| &#39;Wolverine: queued event fan-out end-to-end (PublishAsync)&#39;     |   3.983 μs | 0.0505 μs |  0.0447 μs |   2.93 |    0.12 |  0.2289 |      - |    4400 B |        5.55 |
| &#39;MassTransit: queued event fan-out end-to-end (Publish)&#39;        |  31.624 μs | 1.5938 μs |  4.6993 μs |  23.27 |    3.57 |  2.1057 | 0.1526 |   39416 B |       49.70 |
| &#39;Dispatch (remote): queued commands end-to-end (10 concurrent)&#39; |   7.072 μs | 0.1411 μs |  0.1449 μs |   5.20 |    0.24 |  0.2670 |      - |    5118 B |        6.45 |
| &#39;Wolverine: queued commands end-to-end (10 concurrent)&#39;         |  40.701 μs | 0.6176 μs |  0.5777 μs |  29.95 |    1.29 |  2.3193 |      - |   44489 B |       56.10 |
| &#39;MassTransit: queued commands end-to-end (10 concurrent)&#39;       | 161.395 μs | 6.6747 μs | 19.5757 μs | 118.77 |   15.14 | 11.4746 | 0.7324 |  219091 B |      276.28 |
