```

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7705)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.102
  [Host]     : .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3


```
| Method              | Mean      | Error     | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|-------------------- |----------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
| StringConcatenation |  8.707 ns | 0.2088 ns | 0.2485 ns |  1.00 |    0.04 | 0.0047 |      88 B |        1.00 |
| StringInterpolation |  9.652 ns | 0.3534 ns | 1.0310 ns |  1.11 |    0.12 | 0.0047 |      88 B |        1.00 |
| StringFormat        | 35.251 ns | 0.6139 ns | 0.8195 ns |  4.05 |    0.15 | 0.0046 |      88 B |        1.00 |
