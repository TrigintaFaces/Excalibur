```

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7922)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.103
  [Host] : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=diag-default  PowerPlanMode=00000000-0000-0000-0000-000000000000  Toolchain=InProcessEmitToolchain  
IterationCount=8  LaunchCount=1  UnrollFactor=1  
WarmupCount=3  

```
| Method                                      | BatchOperations | Mean            | Error         | StdDev        | Ratio     | RatioSD  | Gen0     | Allocated | Alloc Ratio |
|-------------------------------------------- |---------------- |----------------:|--------------:|--------------:|----------:|---------:|---------:|----------:|------------:|
| &#39;Stage alloc: dispatcher&#39;                   | 10000           |        55.20 ns |      2.293 ns |      1.199 ns |      1.00 |     0.03 |   0.0085 |     160 B |        1.00 |
| &#39;Stage alloc: final handler&#39;                | 10000           |        89.92 ns |      1.193 ns |      0.624 ns |      1.63 |     0.04 |   0.0156 |     296 B |        1.85 |
| &#39;Stage alloc: local bus send&#39;               | 10000           |        75.47 ns |      0.442 ns |      0.196 ns |      1.37 |     0.03 |   0.0080 |     152 B |        0.95 |
| &#39;Stage alloc: handler activator&#39;            | 10000           |        33.65 ns |      0.417 ns |      0.218 ns |      0.61 |     0.01 |   0.0029 |      56 B |        0.35 |
| &#39;Stage alloc: handler invoker&#39;              | 10000           |        78.94 ns |      1.303 ns |      0.682 ns |      1.43 |     0.03 |   0.0098 |     184 B |        1.15 |
| &#39;GC counter delta: gen0 per dispatch batch&#39; | 10000           | 3,159,082.42 ns | 42,988.562 ns | 19,087.188 ns | 57,254.84 | 1,221.49 | 199.2188 | 3760017 B |   23,500.11 |
