```

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7922)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.103
  [Host] : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=comparative-inproc  PowerPlanMode=00000000-0000-0000-0000-000000000000  Toolchain=InProcessEmitToolchain  
InvocationCount=1  IterationCount=3  UnrollFactor=1  

```
| Method                                 | Mean          | Error         | StdDev      | Ratio    | RatioSD | Allocated | Alloc Ratio |
|--------------------------------------- |--------------:|--------------:|------------:|---------:|--------:|----------:|------------:|
| &#39;Dispatch: Single command&#39;             |      5.767 μs |     13.934 μs |   0.7638 μs |     1.01 |    0.16 |     192 B |        1.00 |
| &#39;MassTransit: Single command&#39;          |    175.400 μs |    590.930 μs |  32.3909 μs |    30.76 |    6.01 |   28160 B |      146.67 |
| &#39;Dispatch: Event to 2 handlers&#39;        |      6.167 μs |      6.407 μs |   0.3512 μs |     1.08 |    0.13 |    5616 B |       29.25 |
| &#39;MassTransit: Event to 2 consumers&#39;    |    392.233 μs |    926.918 μs |  50.8075 μs |    68.79 |   10.88 |   45776 B |      238.42 |
| &#39;Dispatch: 10 concurrent commands&#39;     |      8.900 μs |     17.594 μs |   0.9644 μs |     1.56 |    0.23 |    1600 B |        8.33 |
| &#39;MassTransit: 10 concurrent commands&#39;  |  1,138.333 μs |    342.172 μs |  18.7556 μs |   199.65 |   22.32 |  227880 B |    1,186.88 |
| &#39;Dispatch: 100 concurrent commands&#39;    |     33.167 μs |     28.865 μs |   1.5822 μs |     5.82 |    0.69 |   20224 B |      105.33 |
| &#39;MassTransit: 100 concurrent commands&#39; | 10,577.133 μs | 15,325.485 μs | 840.0416 μs | 1,855.11 |  242.39 | 2230040 B |   11,614.79 |
| &#39;Dispatch: Batch send (10)&#39;            |     13.333 μs |     24.635 μs |   1.3503 μs |     2.34 |    0.33 |    6192 B |       32.25 |
| &#39;MassTransit: Batch send (10)&#39;         |    866.500 μs |  1,974.172 μs | 108.2110 μs |   151.97 |   23.60 |  224896 B |    1,171.33 |
