```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.400
  [Host] : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3


```
| Method                         | Job        | Toolchain              | InvocationCount | UnrollFactor | Mean          | Error       | StdDev      | Median        | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------- |----------- |----------------------- |---------------- |------------- |--------------:|------------:|------------:|--------------:|------:|--------:|-------:|----------:|------------:|
| &#39;ProfileSelect: cached (warm)&#39; | DefaultJob | Default                | Default         | 16           |            NA |          NA |          NA |            NA |     ? |       ? |     NA |        NA |           ? |
| &#39;TypeName: raw reflection&#39;     | DefaultJob | Default                | Default         | 16           |            NA |          NA |          NA |            NA |     ? |       ? |     NA |        NA |           ? |
| &#39;TypeName: cached&#39;             | DefaultJob | Default                | Default         | 16           |            NA |          NA |          NA |            NA |     ? |       ? |     NA |        NA |           ? |
| &#39;ActivityName: interpolated&#39;   | DefaultJob | Default                | Default         | 16           |            NA |          NA |          NA |            NA |     ? |       ? |     NA |        NA |           ? |
| &#39;ActivityName: cached&#39;         | DefaultJob | Default                | Default         | 16           |            NA |          NA |          NA |            NA |     ? |       ? |     NA |        NA |           ? |
| &#39;MessageKind: string.Contains&#39; | DefaultJob | Default                | Default         | 16           |            NA |          NA |          NA |            NA |     ? |       ? |     NA |        NA |           ? |
| &#39;MessageKind: cached&#39;          | DefaultJob | Default                | Default         | 16           |            NA |          NA |          NA |            NA |     ? |       ? |     NA |        NA |           ? |
|                                |            |                        |                 |              |               |             |             |               |       |         |        |           |             |
| &#39;ProfileSelect: frozen&#39;        | Job-CNUJVU | Default                | 1               | 1            |            NA |          NA |          NA |            NA |     ? |       ? |     NA |        NA |           ? |
|                                |            |                        |                 |              |               |             |             |               |       |         |        |           |             |
| &#39;ProfileSelect: cached (warm)&#39; | Job-JWQWGO | InProcessEmitToolchain | Default         | 16           |     3.3719 ns |   0.0143 ns |   0.0134 ns |     3.3742 ns |  3.75 |    0.04 |      - |         - |          NA |
| &#39;TypeName: raw reflection&#39;     | Job-JWQWGO | InProcessEmitToolchain | Default         | 16           |     0.8990 ns |   0.0098 ns |   0.0082 ns |     0.8961 ns |  1.00 |    0.01 |      - |         - |          NA |
| &#39;TypeName: cached&#39;             | Job-JWQWGO | InProcessEmitToolchain | Default         | 16           |     2.6605 ns |   0.0088 ns |   0.0082 ns |     2.6581 ns |  2.96 |    0.03 |      - |         - |          NA |
| &#39;ActivityName: interpolated&#39;   | Job-JWQWGO | InProcessEmitToolchain | Default         | 16           |     8.5879 ns |   0.2185 ns |   0.4932 ns |     8.4080 ns |  9.55 |    0.55 | 0.0042 |      80 B |          NA |
| &#39;ActivityName: cached&#39;         | Job-JWQWGO | InProcessEmitToolchain | Default         | 16           |     2.6575 ns |   0.0068 ns |   0.0060 ns |     2.6586 ns |  2.96 |    0.03 |      - |         - |          NA |
| &#39;MessageKind: string.Contains&#39; | Job-JWQWGO | InProcessEmitToolchain | Default         | 16           |     4.3710 ns |   0.0434 ns |   0.0406 ns |     4.3578 ns |  4.86 |    0.06 |      - |         - |          NA |
| &#39;MessageKind: cached&#39;          | Job-JWQWGO | InProcessEmitToolchain | Default         | 16           |     4.3329 ns |   0.0353 ns |   0.0313 ns |     4.3332 ns |  4.82 |    0.05 |      - |         - |          NA |
|                                |            |                        |                 |              |               |             |             |               |       |         |        |           |             |
| &#39;ProfileSelect: frozen&#39;        | Job-VLPMSV | InProcessEmitToolchain | 1               | 1            | 1,341.5789 ns | 137.8896 ns | 395.6312 ns | 1,350.0000 ns |     ? |       ? |      - |         - |           ? |

Benchmarks with issues:
  CacheOptimizationBenchmarks.'ProfileSelect: cached (warm)': DefaultJob
  CacheOptimizationBenchmarks.'TypeName: raw reflection': DefaultJob
  CacheOptimizationBenchmarks.'TypeName: cached': DefaultJob
  CacheOptimizationBenchmarks.'ActivityName: interpolated': DefaultJob
  CacheOptimizationBenchmarks.'ActivityName: cached': DefaultJob
  CacheOptimizationBenchmarks.'MessageKind: string.Contains': DefaultJob
  CacheOptimizationBenchmarks.'MessageKind: cached': DefaultJob
  CacheOptimizationBenchmarks.'ProfileSelect: frozen': Job-CNUJVU(InvocationCount=1, UnrollFactor=1)
