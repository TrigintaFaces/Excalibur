```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8117/25H2/2025Update/HudsonValley2)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.202
  [Host] : .NET 10.0.6 (10.0.6, 10.0.626.17701), X64 RyuJIT x86-64-v3

Job=comparative-inproc  PowerPlanMode=00000000-0000-0000-0000-000000000000  Toolchain=InProcessEmitToolchain  
InvocationCount=1  IterationCount=10  UnrollFactor=1  
WarmupCount=3  

```
| Method                                                          | Mean      | Error     | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|---------------------------------------------------------------- |----------:|----------:|----------:|------:|--------:|----------:|------------:|
| &#39;Dispatch (remote): queued command end-to-end&#39;                  |  64.00 μs |  17.96 μs | 10.686 μs |  1.02 |    0.23 |   9.88 KB |        1.00 |
| &#39;Wolverine: queued command end-to-end (SendAsync)&#39;              | 144.63 μs |  10.44 μs |  6.903 μs |  2.32 |    0.37 |  14.93 KB |        1.51 |
| &#39;MassTransit: queued command end-to-end (Publish)&#39;              | 295.00 μs |  43.00 μs | 25.591 μs |  4.72 |    0.83 |  25.21 KB |        2.55 |
| &#39;Dispatch (remote): queued event fan-out end-to-end&#39;            |  72.49 μs |  24.05 μs | 12.577 μs |  1.16 |    0.26 |  10.38 KB |        1.05 |
| &#39;Wolverine: queued event fan-out end-to-end (PublishAsync)&#39;     | 113.03 μs |  23.13 μs | 15.301 μs |  1.81 |    0.37 |  15.18 KB |        1.54 |
| &#39;MassTransit: queued event fan-out end-to-end (Publish)&#39;        | 342.12 μs |  49.19 μs | 29.271 μs |  5.48 |    0.96 |  42.01 KB |        4.25 |
| &#39;Dispatch (remote): queued commands end-to-end (10 concurrent)&#39; |  80.42 μs |  15.12 μs |  7.907 μs |  1.29 |    0.23 |  14.73 KB |        1.49 |
| &#39;Wolverine: queued commands end-to-end (10 concurrent)&#39;         | 238.42 μs |  22.67 μs | 13.488 μs |  3.82 |    0.63 |  65.26 KB |        6.60 |
| &#39;MassTransit: queued commands end-to-end (10 concurrent)&#39;       | 774.03 μs | 103.07 μs | 61.336 μs | 12.39 |    2.14 | 227.46 KB |       23.02 |
