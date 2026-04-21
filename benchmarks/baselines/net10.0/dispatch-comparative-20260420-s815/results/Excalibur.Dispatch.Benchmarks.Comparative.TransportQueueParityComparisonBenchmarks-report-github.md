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
| &#39;Dispatch (remote): queued command end-to-end&#39;                  |  77.09 μs | 43.674 μs | 25.990 μs |  1.08 |    0.45 |  11.52 KB |        1.00 |
| &#39;Wolverine: queued command end-to-end (SendAsync)&#39;              | 112.66 μs | 12.543 μs |  8.296 μs |  1.58 |    0.42 |  14.98 KB |        1.30 |
| &#39;MassTransit: queued command end-to-end (Publish)&#39;              | 273.87 μs | 83.937 μs | 55.519 μs |  3.85 |    1.25 |  26.46 KB |        2.30 |
| &#39;Dispatch (remote): queued event fan-out end-to-end&#39;            |  98.39 μs | 49.839 μs | 32.965 μs |  1.38 |    0.58 |  11.56 KB |        1.00 |
| &#39;Wolverine: queued event fan-out end-to-end (PublishAsync)&#39;     | 105.09 μs | 15.216 μs | 10.064 μs |  1.48 |    0.40 |  14.98 KB |        1.30 |
| &#39;MassTransit: queued event fan-out end-to-end (Publish)&#39;        | 299.10 μs | 91.498 μs | 60.520 μs |  4.21 |    1.36 |  49.57 KB |        4.30 |
| &#39;Dispatch (remote): queued commands end-to-end (10 concurrent)&#39; |  71.76 μs |  5.661 μs |  2.961 μs |  1.01 |    0.26 |  13.41 KB |        1.16 |
| &#39;Wolverine: queued commands end-to-end (10 concurrent)&#39;         | 198.79 μs | 27.529 μs | 18.209 μs |  2.80 |    0.76 |  53.85 KB |        4.67 |
| &#39;MassTransit: queued commands end-to-end (10 concurrent)&#39;       | 598.98 μs | 93.711 μs | 55.766 μs |  8.43 |    2.28 | 227.82 KB |       19.77 |
