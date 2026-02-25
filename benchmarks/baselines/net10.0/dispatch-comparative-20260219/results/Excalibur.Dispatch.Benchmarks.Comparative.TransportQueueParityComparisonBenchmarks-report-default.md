
BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7840)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.103
  [Host] : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=comparative-inproc  Toolchain=InProcessEmitToolchain  

 Method                                                          | Mean       | Error     | StdDev     | Median     | Ratio  | RatioSD | Gen0    | Gen1   | Allocated | Alloc Ratio |
---------------------------------------------------------------- |-----------:|----------:|-----------:|-----------:|-------:|--------:|--------:|-------:|----------:|------------:|
 'Dispatch (remote): queued command end-to-end'                  |   1.317 μs | 0.0238 μs |  0.0274 μs |   1.317 μs |   1.00 |    0.03 |  0.0458 |      - |     886 B |        1.00 |
 'Wolverine: queued command end-to-end (SendAsync)'              |   4.005 μs | 0.0274 μs |  0.0243 μs |   4.006 μs |   3.04 |    0.06 |  0.2365 |      - |    4512 B |        5.09 |
 'MassTransit: queued command end-to-end (Publish)'              |  22.655 μs | 2.4868 μs |  7.3323 μs |  24.984 μs |  17.21 |    5.55 |  1.0986 |      - |   22099 B |       24.94 |
 'Dispatch (remote): queued event fan-out end-to-end'            |   1.362 μs | 0.0267 μs |  0.0468 μs |   1.349 μs |   1.03 |    0.04 |  0.0458 |      - |     888 B |        1.00 |
 'Wolverine: queued event fan-out end-to-end (PublishAsync)'     |   3.943 μs | 0.0429 μs |  0.0358 μs |   3.942 μs |   2.99 |    0.07 |  0.2365 |      - |    4512 B |        5.09 |
 'MassTransit: queued event fan-out end-to-end (Publish)'        |  23.184 μs | 1.3755 μs |  3.9465 μs |  21.855 μs |  17.61 |    3.00 |  2.1057 | 0.1221 |   39416 B |       44.49 |
 'Dispatch (remote): queued commands end-to-end (10 concurrent)' |   7.132 μs | 0.1415 μs |  0.1889 μs |   7.127 μs |   5.42 |    0.18 |  0.3128 |      - |    5996 B |        6.77 |
 'Wolverine: queued commands end-to-end (10 concurrent)'         |  39.655 μs | 0.3412 μs |  0.3025 μs |  39.514 μs |  30.12 |    0.65 |  2.3804 |      - |   45609 B |       51.48 |
 'MassTransit: queued commands end-to-end (10 concurrent)'       | 147.692 μs | 4.2325 μs | 12.2117 μs | 145.143 μs | 112.17 |    9.50 | 11.4746 | 0.7324 |  219090 B |      247.28 |
