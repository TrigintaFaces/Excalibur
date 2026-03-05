```

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7840)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.103
  [Host]     : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3
  Job-CNUJVU : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3
  Dry        : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3


```
| Method                         | Job        | InvocationCount | IterationCount | LaunchCount | RunStrategy | UnrollFactor | WarmupCount | Mean            | Error      | StdDev     | Median          | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------- |----------- |---------------- |--------------- |------------ |------------ |------------- |------------ |----------------:|-----------:|-----------:|----------------:|------:|--------:|-------:|----------:|------------:|
| &#39;ProfileSelect: cached (warm)&#39; | DefaultJob | Default         | Default        | Default     | Default     | 16           | Default     |       3.6471 ns |  0.0262 ns |  0.0245 ns |       3.6500 ns |  3.98 |    0.10 |      - |         - |          NA |
| &#39;TypeName: raw reflection&#39;     | DefaultJob | Default         | Default        | Default     | Default     | 16           | Default     |       0.9169 ns |  0.0275 ns |  0.0244 ns |       0.9111 ns |  1.00 |    0.04 |      - |         - |          NA |
| &#39;TypeName: cached&#39;             | DefaultJob | Default         | Default        | Default     | Default     | 16           | Default     |       2.8847 ns |  0.0175 ns |  0.0155 ns |       2.8850 ns |  3.15 |    0.08 |      - |         - |          NA |
| &#39;ActivityName: interpolated&#39;   | DefaultJob | Default         | Default        | Default     | Default     | 16           | Default     |       7.9240 ns |  0.1881 ns |  0.2575 ns |       7.9347 ns |  8.65 |    0.35 | 0.0042 |      80 B |          NA |
| &#39;ActivityName: cached&#39;         | DefaultJob | Default         | Default        | Default     | Default     | 16           | Default     |       2.9053 ns |  0.0255 ns |  0.0238 ns |       2.9004 ns |  3.17 |    0.08 |      - |         - |          NA |
| &#39;MessageKind: string.Contains&#39; | DefaultJob | Default         | Default        | Default     | Default     | 16           | Default     |       2.7373 ns |  0.0141 ns |  0.0132 ns |       2.7402 ns |  2.99 |    0.08 |      - |         - |          NA |
| &#39;MessageKind: cached&#39;          | DefaultJob | Default         | Default        | Default     | Default     | 16           | Default     |       3.5148 ns |  0.0230 ns |  0.0215 ns |       3.5150 ns |  3.84 |    0.10 |      - |         - |          NA |
|                                |            |                 |                |             |             |              |             |                 |            |            |                 |       |         |        |           |             |
| &#39;ProfileSelect: frozen&#39;        | Job-CNUJVU | 1               | Default        | Default     | Default     | 1            | Default     |     370.6522 ns | 19.3394 ns | 54.5472 ns |     400.0000 ns |     ? |       ? |      - |         - |           ? |
|                                |            |                 |                |             |             |              |             |                 |            |            |                 |       |         |        |           |             |
| &#39;ProfileSelect: cached (warm)&#39; | Dry        | Default         | 1              | 1           | ColdStart   | 1            | 1           | 133,200.0000 ns |         NA |  0.0000 ns | 133,200.0000 ns |  1.18 |    0.00 |      - |         - |        0.00 |
| &#39;ProfileSelect: frozen&#39;        | Dry        | Default         | 1              | 1           | ColdStart   | 1            | 1           | 451,800.0000 ns |         NA |  0.0000 ns | 451,800.0000 ns |  3.99 |    0.00 |      - |         - |        0.00 |
| &#39;TypeName: raw reflection&#39;     | Dry        | Default         | 1              | 1           | ColdStart   | 1            | 1           | 113,200.0000 ns |         NA |  0.0000 ns | 113,200.0000 ns |  1.00 |    0.00 |      - |     224 B |        1.00 |
| &#39;TypeName: cached&#39;             | Dry        | Default         | 1              | 1           | ColdStart   | 1            | 1           | 207,800.0000 ns |         NA |  0.0000 ns | 207,800.0000 ns |  1.84 |    0.00 |      - |         - |        0.00 |
| &#39;ActivityName: interpolated&#39;   | Dry        | Default         | 1              | 1           | ColdStart   | 1            | 1           | 122,400.0000 ns |         NA |  0.0000 ns | 122,400.0000 ns |  1.08 |    0.00 |      - |     304 B |        1.36 |
| &#39;ActivityName: cached&#39;         | Dry        | Default         | 1              | 1           | ColdStart   | 1            | 1           | 210,200.0000 ns |         NA |  0.0000 ns | 210,200.0000 ns |  1.86 |    0.00 |      - |         - |        0.00 |
| &#39;MessageKind: string.Contains&#39; | Dry        | Default         | 1              | 1           | ColdStart   | 1            | 1           | 156,200.0000 ns |         NA |  0.0000 ns | 156,200.0000 ns |  1.38 |    0.00 |      - |         - |        0.00 |
| &#39;MessageKind: cached&#39;          | Dry        | Default         | 1              | 1           | ColdStart   | 1            | 1           | 217,800.0000 ns |         NA |  0.0000 ns | 217,800.0000 ns |  1.92 |    0.00 |      - |         - |        0.00 |
