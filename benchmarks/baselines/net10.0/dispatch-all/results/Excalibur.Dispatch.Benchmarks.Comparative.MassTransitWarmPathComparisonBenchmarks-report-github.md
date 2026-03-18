```

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7922)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.103
  [Host] : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=warmpath-inproc  PowerPlanMode=00000000-0000-0000-0000-000000000000  Toolchain=InProcessEmitToolchain  

```
| Method                                 | Mean            | Error         | StdDev        | Ratio     | RatioSD | Gen0     | Gen1    | Allocated | Alloc Ratio |
|--------------------------------------- |----------------:|--------------:|--------------:|----------:|--------:|---------:|--------:|----------:|------------:|
| &#39;Dispatch: Single command&#39;             |        47.99 ns |      0.929 ns |      0.869 ns |      1.00 |    0.02 |   0.0102 |       - |     192 B |        1.00 |
| &#39;MassTransit: Single command&#39;          |    22,055.80 ns |    207.302 ns |    173.106 ns |    459.69 |    8.77 |   1.0986 |       - |   22126 B |      115.24 |
| &#39;Dispatch: Event to 2 handlers&#39;        |       116.24 ns |      2.121 ns |      1.984 ns |      2.42 |    0.06 |   0.0153 |       - |     288 B |        1.50 |
| &#39;MassTransit: Event to 2 consumers&#39;    |    24,033.99 ns |    476.436 ns |    567.163 ns |    500.92 |   14.50 |   2.1057 |  0.1221 |   39408 B |      205.25 |
| &#39;Dispatch: 10 concurrent commands&#39;     |       638.28 ns |     10.231 ns |      9.570 ns |     13.30 |    0.30 |   0.0849 |       - |    1600 B |        8.33 |
| &#39;MassTransit: 10 concurrent commands&#39;  |   127,920.55 ns |  1,607.322 ns |  1,342.187 ns |  2,666.16 |   53.91 |  11.4746 |  0.7324 |  219086 B |    1,141.07 |
| &#39;Dispatch: 100 concurrent commands&#39;    |     5,317.19 ns |     51.840 ns |     45.955 ns |    110.82 |    2.15 |   0.7706 |  0.0153 |   14560 B |       75.83 |
| &#39;MassTransit: 100 concurrent commands&#39; | 1,138,992.70 ns | 18,826.040 ns | 17,609.889 ns | 23,739.25 |  546.83 | 115.2344 | 25.3906 | 2185767 B |   11,384.20 |
| &#39;Dispatch: Batch send (10)&#39;            |       424.30 ns |      8.277 ns |      8.499 ns |      8.84 |    0.23 |   0.0634 |       - |    1200 B |        6.25 |
| &#39;MassTransit: Batch send (10)&#39;         |   130,188.30 ns |  1,299.759 ns |  1,014.767 ns |  2,713.43 |   51.68 |  11.4746 |  0.7324 |  219323 B |    1,142.31 |
