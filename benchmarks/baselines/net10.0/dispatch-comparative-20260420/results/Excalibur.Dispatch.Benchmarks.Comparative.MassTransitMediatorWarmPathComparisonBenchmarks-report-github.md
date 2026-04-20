```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8117/25H2/2025Update/HudsonValley2)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.202
  [Host] : .NET 10.0.6 (10.0.6, 10.0.626.17701), X64 RyuJIT x86-64-v3

Job=warmpath-inproc  PowerPlanMode=00000000-0000-0000-0000-000000000000  Toolchain=InProcessEmitToolchain  

```
| Method                                                           | Mean          | Error        | StdDev       | Ratio    | RatioSD | Gen0    | Gen1   | Allocated | Alloc Ratio |
|----------------------------------------------------------------- |--------------:|-------------:|-------------:|---------:|--------:|--------:|-------:|----------:|------------:|
| &#39;Dispatch (local): Single command&#39;                               |      87.18 ns |     0.607 ns |     0.507 ns |     1.00 |    0.01 |  0.0186 |      - |     352 B |        1.00 |
| &#39;MassTransit Mediator (in-process): Single command&#39;              |   1,236.83 ns |    24.018 ns |    32.064 ns |    14.19 |    0.37 |  0.1869 |      - |    3544 B |       10.07 |
| &#39;Dispatch (local): Notification to 2 handlers&#39;                   |     138.03 ns |     1.197 ns |     0.999 ns |     1.58 |    0.01 |  0.0198 |      - |     376 B |        1.07 |
| &#39;MassTransit Mediator (in-process): Notification to 2 consumers&#39; |   1,751.99 ns |    20.723 ns |    19.385 ns |    20.10 |    0.24 |  0.2213 |      - |    4176 B |       11.86 |
| &#39;Dispatch (local): Query with return&#39;                            |     105.01 ns |     1.386 ns |     1.228 ns |     1.20 |    0.02 |  0.0288 |      - |     544 B |        1.55 |
| &#39;MassTransit Mediator (in-process): Query with return&#39;           |  19,676.29 ns |   483.048 ns | 1,378.163 ns |   225.71 |   15.78 |  0.6104 |      - |   11653 B |       33.11 |
| &#39;Dispatch (local): 10 concurrent commands&#39;                       |   1,060.64 ns |    10.403 ns |     9.731 ns |    12.17 |    0.13 |  0.1698 |      - |    3200 B |        9.09 |
| &#39;MassTransit Mediator (in-process): 10 concurrent commands&#39;      |  12,515.54 ns |   245.788 ns |   344.560 ns |   143.57 |    3.97 |  1.8921 |      - |   35648 B |      101.27 |
| &#39;Dispatch (local): 100 concurrent commands&#39;                      |   9,179.82 ns |    83.936 ns |    78.514 ns |   105.30 |    1.05 |  1.6174 | 0.0458 |   30560 B |       86.82 |
| &#39;MassTransit Mediator (in-process): 100 concurrent commands&#39;     | 123,882.74 ns | 2,443.552 ns | 4,406.228 ns | 1,421.09 |   50.60 | 18.7988 | 0.1221 |  355329 B |    1,009.46 |
