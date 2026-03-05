```

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7840)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.103
  [Host] : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=comparative-inproc  Toolchain=InProcessEmitToolchain  

```
| Method                                  | Mean       | Error     | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|---------------------------------------- |-----------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Dispatch: 3 middleware behaviors&#39;      |   571.2 ns |  11.44 ns |  18.47 ns |  1.00 |    0.05 | 0.0153 |    1224 B |        1.00 |
| &#39;MediatR: 3 pipeline behaviors&#39;         |   177.3 ns |   3.51 ns |   6.24 ns |  0.31 |    0.01 | 0.0088 |     680 B |        0.56 |
| &#39;Dispatch: 10 concurrent + 3 behaviors&#39; | 6,580.6 ns | 124.94 ns | 138.88 ns | 11.53 |    0.44 | 0.1526 |   11952 B |        9.76 |
| &#39;MediatR: 10 concurrent + 3 behaviors&#39;  | 1,923.3 ns |  38.17 ns |  83.79 ns |  3.37 |    0.18 | 0.0916 |    7168 B |        5.86 |
