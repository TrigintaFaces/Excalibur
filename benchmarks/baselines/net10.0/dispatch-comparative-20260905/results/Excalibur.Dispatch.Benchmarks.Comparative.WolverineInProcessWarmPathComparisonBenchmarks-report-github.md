```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.400
  [Host] : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3

Job=warmpath-inproc  PowerPlanMode=00000000-0000-0000-0000-000000000000  Toolchain=InProcessEmitToolchain  

```
| Method                                                        | Mean         | Error      | StdDev     | Ratio  | RatioSD | Gen0   | Allocated | Alloc Ratio |
|-------------------------------------------------------------- |-------------:|-----------:|-----------:|-------:|--------:|-------:|----------:|------------:|
| &#39;Dispatch (local): Single command&#39;                            |     47.00 ns |   0.307 ns |   0.239 ns |   1.00 |    0.01 | 0.0051 |      96 B |        1.00 |
| &#39;Dispatch (context-less 2-arg): Single command&#39;               |     54.11 ns |   0.562 ns |   0.498 ns |   1.15 |    0.01 | 0.0051 |      96 B |        1.00 |
| &#39;Wolverine (in-process): Single command InvokeAsync&#39;          |    179.13 ns |   2.444 ns |   2.286 ns |   3.81 |    0.05 | 0.0305 |     584 B |        6.08 |
| &#39;Dispatch (local): Notification to 2 handlers&#39;                |    120.12 ns |   0.607 ns |   0.474 ns |   2.56 |    0.02 | 0.0050 |      96 B |        1.00 |
| &#39;Wolverine (in-process): Notification to 2 handlers (inline)&#39; |    199.66 ns |   2.272 ns |   2.125 ns |   4.25 |    0.05 | 0.0317 |     600 B |        6.25 |
| &#39;Dispatch (local): Query with return&#39;                         |     63.74 ns |   1.287 ns |   1.966 ns |   1.36 |    0.04 | 0.0153 |     288 B |        3.00 |
| &#39;Wolverine (in-process): Query with return InvokeAsync&#39;       |    252.73 ns |   4.924 ns |   6.047 ns |   5.38 |    0.13 | 0.0410 |     776 B |        8.08 |
| &#39;Dispatch (local): 10 concurrent commands&#39;                    |    585.42 ns |   7.088 ns |   6.630 ns |  12.46 |    0.15 | 0.0715 |    1360 B |       14.17 |
| &#39;Wolverine (in-process): 10 concurrent commands&#39;              |  2,028.61 ns |  38.190 ns |  35.723 ns |  43.17 |    0.77 | 0.3204 |    6048 B |       63.00 |
| &#39;Dispatch (local): 100 concurrent commands&#39;                   |  5,662.98 ns |  69.959 ns |  65.440 ns | 120.50 |    1.47 | 0.6409 |   12160 B |      126.67 |
| &#39;Wolverine (in-process): 100 concurrent commands&#39;             | 20,154.80 ns | 208.226 ns | 194.775 ns | 428.86 |    4.54 | 3.1433 |   59328 B |      618.00 |
