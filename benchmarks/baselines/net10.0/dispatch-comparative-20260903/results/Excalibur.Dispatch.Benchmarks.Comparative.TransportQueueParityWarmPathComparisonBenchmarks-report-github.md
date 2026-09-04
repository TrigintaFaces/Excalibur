```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.400
  [Host] : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3

Job=warmpath-inproc  PowerPlanMode=00000000-0000-0000-0000-000000000000  Toolchain=InProcessEmitToolchain  

```
| Method                                                          | Mean       | Error     | StdDev     | Ratio  | RatioSD | Gen0    | Gen1   | Allocated | Alloc Ratio |
|---------------------------------------------------------------- |-----------:|----------:|-----------:|-------:|--------:|--------:|-------:|----------:|------------:|
| &#39;Dispatch (remote): queued command end-to-end&#39;                  |   1.442 μs | 0.0285 μs |  0.0644 μs |   1.00 |    0.06 |  0.0420 |      - |     797 B |        1.00 |
| &#39;Wolverine: queued command end-to-end (SendAsync)&#39;              |   4.309 μs | 0.0854 μs |  0.0713 μs |   2.99 |    0.14 |  0.2289 |      - |    4400 B |        5.52 |
| &#39;MassTransit: queued command end-to-end (Publish)&#39;              |  17.540 μs | 0.6874 μs |  1.9163 μs |  12.19 |    1.43 |  1.1749 | 0.0305 |   22006 B |       27.61 |
| &#39;Dispatch (remote): queued event fan-out end-to-end&#39;            |   1.476 μs | 0.0294 μs |  0.0413 μs |   1.03 |    0.05 |  0.0420 |      - |     796 B |        1.00 |
| &#39;Wolverine: queued event fan-out end-to-end (PublishAsync)&#39;     |   4.247 μs | 0.0390 μs |  0.0365 μs |   2.95 |    0.13 |  0.2289 |      - |    4400 B |        5.52 |
| &#39;MassTransit: queued event fan-out end-to-end (Publish)&#39;        |  28.748 μs | 1.7664 μs |  5.1806 μs |  19.97 |    3.69 |  2.0752 | 0.1221 |   39131 B |       49.10 |
| &#39;Dispatch (remote): queued commands end-to-end (10 concurrent)&#39; |   7.908 μs | 0.1575 μs |  0.3682 μs |   5.49 |    0.35 |  0.2594 |      - |    5117 B |        6.42 |
| &#39;Wolverine: queued commands end-to-end (10 concurrent)&#39;         |  43.205 μs | 0.5008 μs |  0.4684 μs |  30.02 |    1.37 |  2.3193 |      - |   44489 B |       55.82 |
| &#39;MassTransit: queued commands end-to-end (10 concurrent)&#39;       | 160.465 μs | 4.3057 μs | 12.4917 μs | 111.48 |    9.95 | 11.4746 | 0.7324 |  217833 B |      273.32 |
