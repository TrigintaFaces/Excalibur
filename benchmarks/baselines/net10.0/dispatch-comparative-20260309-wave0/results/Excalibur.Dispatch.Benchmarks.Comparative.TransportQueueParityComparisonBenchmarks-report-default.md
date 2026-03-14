
BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7922)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.103
  [Host] : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=comparative-inproc  Toolchain=InProcessEmitToolchain  

 Method                                                          | Mean       | Error     | StdDev     | Median     | Ratio  | RatioSD | Gen0    | Gen1   | Allocated | Alloc Ratio |
---------------------------------------------------------------- |-----------:|----------:|-----------:|-----------:|-------:|--------:|--------:|-------:|----------:|------------:|
 'Dispatch (remote): queued command end-to-end'                  |   1.422 μs | 0.0283 μs |  0.0656 μs |   1.419 μs |   1.00 |    0.07 |  0.0458 |      - |     894 B |        1.00 |
 'Wolverine: queued command end-to-end (SendAsync)'              |   4.055 μs | 0.0733 μs |  0.0612 μs |   4.049 μs |   2.86 |    0.14 |  0.2365 |      - |    4512 B |        5.05 |
 'MassTransit: queued command end-to-end (Publish)'              |  16.771 μs | 1.2023 μs |  3.4106 μs |  15.452 μs |  11.82 |    2.46 |  1.1902 | 0.0458 |   22072 B |       24.69 |
 'Dispatch (remote): queued event fan-out end-to-end'            |   1.378 μs | 0.0276 μs |  0.0271 μs |   1.368 μs |   0.97 |    0.05 |  0.0439 |      - |     856 B |        0.96 |
 'Wolverine: queued event fan-out end-to-end (PublishAsync)'     |   4.090 μs | 0.0400 μs |  0.0355 μs |   4.094 μs |   2.88 |    0.14 |  0.2365 |      - |    4512 B |        5.05 |
 'MassTransit: queued event fan-out end-to-end (Publish)'        |  25.832 μs | 1.6241 μs |  4.7633 μs |  26.061 μs |  18.21 |    3.45 |  2.1057 | 0.1221 |   39384 B |       44.05 |
 'Dispatch (remote): queued commands end-to-end (10 concurrent)' |   7.626 μs | 0.1457 μs |  0.1843 μs |   7.613 μs |   5.37 |    0.28 |  0.3204 |      - |    6078 B |        6.80 |
 'Wolverine: queued commands end-to-end (10 concurrent)'         |  40.583 μs | 0.3146 μs |  0.2627 μs |  40.654 μs |  28.60 |    1.34 |  2.3804 |      - |   45609 B |       51.02 |
 'MassTransit: queued commands end-to-end (10 concurrent)'       | 144.049 μs | 4.2164 μs | 12.2327 μs | 139.671 μs | 101.53 |    9.80 | 11.4746 | 0.7324 |  219088 B |      245.06 |
