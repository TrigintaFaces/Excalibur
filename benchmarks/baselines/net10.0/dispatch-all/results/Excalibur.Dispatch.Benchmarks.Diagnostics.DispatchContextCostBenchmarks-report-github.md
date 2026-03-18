```

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7922)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.103
  [Host] : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=diag-default  PowerPlanMode=00000000-0000-0000-0000-000000000000  Toolchain=InProcessEmitToolchain  
IterationCount=8  LaunchCount=1  UnrollFactor=1  
WarmupCount=3  

```
| Method                    | Mean      | Error     | StdDev    | Ratio | Gen0   | Allocated | Alloc Ratio |
|-------------------------- |----------:|----------:|----------:|------:|-------:|----------:|------------:|
| &#39;Items write hot key&#39;     | 11.557 ns | 0.1654 ns | 0.0734 ns |  1.00 | 0.0013 |      24 B |        1.00 |
| &#39;Items read hot key&#39;      |  3.205 ns | 0.0881 ns | 0.0391 ns |  0.28 |      - |         - |        0.00 |
| &#39;Contains + read hot key&#39; |  7.459 ns | 0.2079 ns | 0.1088 ns |  0.65 |      - |         - |        0.00 |
| &#39;SetItem + GetItem path&#39;  |  9.658 ns | 0.1379 ns | 0.0721 ns |  0.84 | 0.0013 |      24 B |        1.00 |
