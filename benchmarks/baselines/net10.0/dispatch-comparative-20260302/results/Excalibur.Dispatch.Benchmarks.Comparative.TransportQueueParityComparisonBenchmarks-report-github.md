```

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7840)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.103
  [Host] : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=comparative-inproc  Toolchain=InProcessEmitToolchain  

```
| Method                                                          | Mean       | Error     | StdDev    | Ratio  | RatioSD | Gen0   | Allocated | Alloc Ratio |
|---------------------------------------------------------------- |-----------:|----------:|----------:|-------:|--------:|-------:|----------:|------------:|
| &#39;Dispatch (remote): queued command end-to-end&#39;                  |   1.147 μs | 0.0082 μs | 0.0081 μs |   1.00 |    0.01 | 0.0095 |     852 B |        1.00 |
| &#39;Wolverine: queued command end-to-end (SendAsync)&#39;              |   4.305 μs | 0.0193 μs | 0.0161 μs |   3.75 |    0.03 | 0.0534 |    4512 B |        5.30 |
| &#39;MassTransit: queued command end-to-end (Publish)&#39;              |  14.141 μs | 0.1495 μs | 0.1167 μs |  12.32 |    0.13 | 0.2747 |   22197 B |       26.05 |
| &#39;Dispatch (remote): queued event fan-out end-to-end&#39;            |   1.241 μs | 0.0247 μs | 0.0347 μs |   1.08 |    0.03 | 0.0095 |     822 B |        0.96 |
| &#39;Wolverine: queued event fan-out end-to-end (PublishAsync)&#39;     |   3.949 μs | 0.0282 μs | 0.0263 μs |   3.44 |    0.03 | 0.0534 |    4512 B |        5.30 |
| &#39;MassTransit: queued event fan-out end-to-end (Publish)&#39;        |  26.065 μs | 0.5072 μs | 0.9146 μs |  22.72 |    0.80 | 0.4883 |   39544 B |       46.41 |
| &#39;Dispatch (remote): queued commands end-to-end (10 concurrent)&#39; |   6.249 μs | 0.0776 μs | 0.0725 μs |   5.45 |    0.07 | 0.0687 |    5675 B |        6.66 |
| &#39;Wolverine: queued commands end-to-end (10 concurrent)&#39;         |  39.326 μs | 0.1638 μs | 0.1452 μs |  34.27 |    0.26 | 0.5493 |   45609 B |       53.53 |
| &#39;MassTransit: queued commands end-to-end (10 concurrent)&#39;       | 132.652 μs | 2.0049 μs | 1.8754 μs | 115.61 |    1.77 | 2.6855 |  219734 B |      257.90 |
