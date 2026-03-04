
BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7840)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.103
  [Host] : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=comparative-inproc  Toolchain=InProcessEmitToolchain  

 Method                            | Mean     | Error    | StdDev   | Ratio | RatioSD | Gen0    | Allocated | Alloc Ratio |
---------------------------------- |---------:|---------:|---------:|------:|--------:|--------:|----------:|------------:|
 'Dispatch: Container startup'     |       NA |       NA |       NA |     ? |       ? |      NA |        NA |           ? |
 'MediatR: Container startup'      | 758.1 μs | 15.12 μs | 31.23 μs |     ? |       ? | 41.0156 |    2.6 MB |           ? |
 'Dispatch: Startup + 10 handlers' |       NA |       NA |       NA |     ? |       ? |      NA |        NA |           ? |
 'MediatR: Startup + 10 handlers'  | 787.4 μs | 15.37 μs | 21.04 μs |     ? |       ? | 46.8750 |    2.6 MB |           ? |

Benchmarks with issues:
  StartupComparisonBenchmarks.'Dispatch: Container startup': comparative-inproc(Toolchain=InProcessEmitToolchain)
  StartupComparisonBenchmarks.'Dispatch: Startup + 10 handlers': comparative-inproc(Toolchain=InProcessEmitToolchain)
