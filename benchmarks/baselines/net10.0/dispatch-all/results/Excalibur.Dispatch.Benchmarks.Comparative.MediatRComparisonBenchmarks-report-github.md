```

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7922)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.103
  [Host] : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=comparative-inproc  PowerPlanMode=00000000-0000-0000-0000-000000000000  Toolchain=InProcessEmitToolchain  
InvocationCount=1  IterationCount=3  UnrollFactor=1  

```
| Method                                          | Mean      | Error     | StdDev    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------------------ |----------:|----------:|----------:|------:|--------:|----------:|------------:|
| &#39;Dispatch: Single command handler&#39;              |  3.833 μs |  1.053 μs | 0.0577 μs |  1.00 |    0.02 |     168 B |        1.00 |
| &#39;Dispatch: Single command strict direct-local&#39;  |  4.167 μs | 11.586 μs | 0.6351 μs |  1.09 |    0.14 |    4872 B |       29.00 |
| &#39;Dispatch: Single command ultra-local API&#39;      |  2.667 μs |  2.787 μs | 0.1528 μs |  0.70 |    0.04 |      72 B |        0.43 |
| &#39;MediatR: Single command handler&#39;               |  4.300 μs |  4.827 μs | 0.2646 μs |  1.12 |    0.06 |    4872 B |       29.00 |
| &#39;Dispatch: Notification to 3 handlers&#39;          |  6.433 μs |  9.182 μs | 0.5033 μs |  1.68 |    0.12 |    4896 B |       29.14 |
| &#39;MediatR: Notification to 3 handlers&#39;           |  5.233 μs |  4.591 μs | 0.2517 μs |  1.37 |    0.06 |    5272 B |       31.38 |
| &#39;Dispatch: Query with return value&#39;             |  4.167 μs |  5.267 μs | 0.2887 μs |  1.09 |    0.07 |     648 B |        3.86 |
| &#39;Dispatch: Query with return value (typed API)&#39; |  4.450 μs |  6.578 μs | 0.3606 μs |  1.16 |    0.08 |    4008 B |       23.86 |
| &#39;Dispatch: Query ultra-local API&#39;               |  2.967 μs |  6.407 μs | 0.3512 μs |  0.77 |    0.08 |    4848 B |       28.86 |
| &#39;MediatR: Query with return value&#39;              |  9.833 μs | 48.623 μs | 2.6652 μs |  2.57 |    0.60 |     360 B |        2.14 |
| &#39;Dispatch: Ultra-local singleton-promoted&#39;      |  2.567 μs |  1.053 μs | 0.0577 μs |  0.67 |    0.02 |     360 B |        2.14 |
| &#39;Dispatch: Query singleton-promoted&#39;            |  3.233 μs |  4.591 μs | 0.2517 μs |  0.84 |    0.06 |     480 B |        2.86 |
| &#39;Dispatch: 10 concurrent commands&#39;              |  7.767 μs |  1.053 μs | 0.0577 μs |  2.03 |    0.03 |    6016 B |       35.81 |
| &#39;MediatR: 10 concurrent commands&#39;               |  8.400 μs | 23.717 μs | 1.3000 μs |  2.19 |    0.30 |    2496 B |       14.86 |
| &#39;Dispatch: 100 concurrent commands&#39;             | 27.267 μs |  3.798 μs | 0.2082 μs |  7.11 |    0.10 |   13456 B |       80.10 |
| &#39;MediatR: 100 concurrent commands&#39;              | 24.433 μs | 15.516 μs | 0.8505 μs |  6.37 |    0.21 |   28504 B |      169.67 |
