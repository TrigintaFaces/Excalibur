
BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7840)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.103
  [Host] : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=comparative-inproc  Toolchain=InProcessEmitToolchain  

 Method                                                          | Mean       | Error     | StdDev     | Ratio  | RatioSD | Gen0   | Allocated | Alloc Ratio |
---------------------------------------------------------------- |-----------:|----------:|-----------:|-------:|--------:|-------:|----------:|------------:|
 'Dispatch (remote): queued command end-to-end'                  |   1.541 μs | 0.0305 μs |  0.0351 μs |   1.00 |    0.03 | 0.0114 |     882 B |        1.00 |
 'Wolverine: queued command end-to-end (SendAsync)'              |   4.100 μs | 0.0814 μs |  0.1315 μs |   2.66 |    0.10 | 0.0534 |    4512 B |        5.12 |
 'MassTransit: queued command end-to-end (Publish)'              |  25.923 μs | 1.0064 μs |  2.9036 μs |  16.83 |    1.91 | 0.2441 |   22090 B |       25.05 |
 'Dispatch (remote): queued event fan-out end-to-end'            |   1.536 μs | 0.0306 μs |  0.0408 μs |   1.00 |    0.03 | 0.0114 |     884 B |        1.00 |
 'Wolverine: queued event fan-out end-to-end (PublishAsync)'     |   4.153 μs | 0.0760 μs |  0.1114 μs |   2.70 |    0.09 | 0.0534 |    4512 B |        5.12 |
 'MassTransit: queued event fan-out end-to-end (Publish)'        |  35.612 μs | 1.3461 μs |  3.9266 μs |  23.12 |    2.59 | 0.4883 |   39385 B |       44.65 |
 'Dispatch (remote): queued commands end-to-end (10 concurrent)' |   7.663 μs | 0.1522 μs |  0.1424 μs |   4.97 |    0.14 | 0.0763 |    5998 B |        6.80 |
 'Wolverine: queued commands end-to-end (10 concurrent)'         |  41.539 μs | 0.7559 μs |  1.1543 μs |  26.96 |    0.95 | 0.5493 |   45609 B |       51.71 |
 'MassTransit: queued commands end-to-end (10 concurrent)'       | 212.915 μs | 4.7804 μs | 14.0951 μs | 138.21 |    9.62 | 2.6855 |  219046 B |      248.35 |
