```

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7922)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.103
  [Host] : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=comparative-inproc  PowerPlanMode=00000000-0000-0000-0000-000000000000  Toolchain=InProcessEmitToolchain  
InvocationCount=1  IterationCount=3  UnrollFactor=1  

```
| Method                                                  | Mean       | Error      | StdDev     | Ratio | RatioSD | Allocated | Alloc Ratio |
|-------------------------------------------------------- |-----------:|-----------:|-----------:|------:|--------:|----------:|------------:|
| &#39;Dispatch (local): Single command&#39;                      |   8.200 μs |  15.800 μs |  0.8660 μs |  1.01 |    0.13 |     192 B |        1.00 |
| &#39;Dispatch (ultra-local): Single command&#39;                |   2.967 μs |   5.574 μs |  0.3055 μs |  0.36 |    0.05 |     720 B |        3.75 |
| &#39;Wolverine (in-process): Single command InvokeAsync&#39;    |  29.533 μs | 275.718 μs | 15.1130 μs |  3.63 |    1.65 |    7344 B |       38.25 |
| &#39;Dispatch (local): Notification to 2 handlers&#39;          |  16.967 μs |  17.530 μs |  0.9609 μs |  2.09 |    0.23 |     576 B |        3.00 |
| &#39;Wolverine (in-process): Notification to 2 handlers&#39;    |  98.500 μs |  66.982 μs |  3.6715 μs | 12.11 |    1.24 |   12096 B |       63.00 |
| &#39;Dispatch (local): Query with return&#39;                   |  14.200 μs |   1.824 μs |  0.1000 μs |  1.75 |    0.17 |     672 B |        3.50 |
| &#39;Wolverine (in-process): Query with return InvokeAsync&#39; |  28.633 μs |  48.349 μs |  2.6502 μs |  3.52 |    0.44 |     936 B |        4.88 |
| &#39;Dispatch (local): 10 concurrent commands&#39;              |  18.767 μs |  30.382 μs |  1.6653 μs |  2.31 |    0.29 |    1600 B |        8.33 |
| &#39;Wolverine (in-process): 10 concurrent commands&#39;        |  37.033 μs |  53.262 μs |  2.9195 μs |  4.55 |    0.54 |   11584 B |       60.33 |
| &#39;Dispatch (local): 100 concurrent commands&#39;             |  52.850 μs |  11.963 μs |  0.6557 μs |  6.50 |    0.64 |   19552 B |      101.83 |
| &#39;Wolverine (in-process): 100 concurrent commands&#39;       | 124.467 μs |  44.551 μs |  2.4420 μs | 15.30 |    1.51 |   74800 B |      389.58 |
