```

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7922)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.103
  [Host] : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=diag-default  PowerPlanMode=00000000-0000-0000-0000-000000000000  Toolchain=InProcessEmitToolchain  
IterationCount=8  LaunchCount=1  UnrollFactor=1  
WarmupCount=3  

```
| Method                              | Mean        | Error      | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------ |------------:|-----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Size via JSON string + UTF8 count&#39; | 231.9277 ns |  6.5384 ns | 3.4197 ns | 1.000 |    0.02 | 0.0691 |    1304 B |        1.00 |
| &#39;Size via SerializeToUtf8Bytes&#39;     | 165.6421 ns | 11.3342 ns | 5.9280 ns | 0.714 |    0.03 | 0.0355 |     672 B |        0.52 |
| &#39;Size estimation skipped&#39;           |   0.0000 ns |  0.0000 ns | 0.0000 ns | 0.000 |    0.00 |      - |         - |        0.00 |
