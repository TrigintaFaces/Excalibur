```

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7705)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.102
  [Host]     : .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3


```
| Method                    | Mean          | Error      | StdDev     | Median        | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|-------------------------- |--------------:|-----------:|-----------:|--------------:|------:|--------:|-------:|----------:|------------:|
| DirectPropertySetter      |     0.0235 ns |  0.0169 ns |  0.0288 ns |     0.0161 ns |     ? |       ? |      - |         - |           ? |
| PrecompiledDelegateSetter |     3.7188 ns |  0.0959 ns |  0.2333 ns |     3.6750 ns |     ? |       ? | 0.0013 |      24 B |           ? |
| ActivateHandler_Cached    |    11.2328 ns |  0.2814 ns |  0.7938 ns |    11.0668 ns |     ? |       ? | 0.0013 |      24 B |           ? |
| ReflectionPropertySetter  |    23.0244 ns |  0.4771 ns |  1.2981 ns |    22.6791 ns |     ? |       ? | 0.0013 |      24 B |           ? |
| ActivateHandler_Batch100  |   982.6062 ns | 26.3428 ns | 73.8681 ns |   966.2643 ns |     ? |       ? | 0.1268 |    2400 B |           ? |
| ReflectionSetter_Batch100 | 1,250.1932 ns | 32.4622 ns | 93.6610 ns | 1,237.1885 ns |     ? |       ? | 0.1259 |    2400 B |           ? |
