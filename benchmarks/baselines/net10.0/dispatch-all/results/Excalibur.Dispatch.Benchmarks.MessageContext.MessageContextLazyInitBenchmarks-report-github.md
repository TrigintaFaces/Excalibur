```

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7705)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.102
  [Host]     : .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3


```
| Method                                       | Mean     | Error    | StdDev   | Median   | Ratio | RatioSD | Gen0   | Gen1   | Allocated | Alloc Ratio |
|--------------------------------------------- |---------:|---------:|---------:|---------:|------:|--------:|-------:|-------:|----------:|------------:|
| &#39;MessageContext: Create (no Items)&#39;          | 262.2 ns |  5.02 ns |  4.70 ns | 262.0 ns |  1.00 |    0.02 | 0.0858 |      - |   1.58 KB |        1.00 |
| &#39;ContainsItem on fresh context (no alloc)&#39;   | 228.1 ns |  1.79 ns |  1.59 ns | 228.0 ns |  0.87 |    0.02 | 0.0641 |      - |   1.18 KB |        0.75 |
| &#39;GetItem on fresh context (no alloc)&#39;        | 261.5 ns |  5.04 ns |  6.00 ns | 259.1 ns |  1.00 |    0.03 | 0.0858 |      - |   1.58 KB |        1.00 |
| &#39;RemoveItem on fresh context (no alloc)&#39;     | 241.7 ns |  4.72 ns |  4.63 ns | 240.1 ns |  0.92 |    0.02 | 0.0639 |      - |   1.18 KB |        0.75 |
| &#39;SetItem on fresh context (allocs dict)&#39;     | 432.8 ns |  7.93 ns |  8.81 ns | 432.3 ns |  1.65 |    0.04 | 0.1726 | 0.0010 |   3.18 KB |        2.01 |
| &#39;Items.Count on fresh context (allocs dict)&#39; | 689.1 ns | 13.35 ns | 12.48 ns | 688.9 ns |  2.63 |    0.06 | 0.1698 | 0.0010 |   3.13 KB |        1.99 |
| &#39;Simple dispatch: no Items usage&#39;            | 259.2 ns |  5.06 ns |  7.42 ns | 258.6 ns |  0.99 |    0.03 | 0.0858 |      - |   1.58 KB |        1.00 |
| &#39;Dispatch: middleware writes Items&#39;          | 511.0 ns | 10.15 ns | 20.27 ns | 507.1 ns |  1.95 |    0.08 | 0.1802 | 0.0010 |   3.33 KB |        2.11 |
| &#39;CreateChildContext (no Items)&#39;              | 583.0 ns | 11.66 ns | 29.05 ns | 571.9 ns |  2.22 |    0.12 | 0.1774 |      - |   3.27 KB |        2.07 |
| &#39;Multiple reads (no alloc)&#39;                  | 237.9 ns |  3.63 ns |  3.39 ns | 238.3 ns |  0.91 |    0.02 | 0.0639 |      - |   1.18 KB |        0.75 |
| &#39;First write allocs, subsequent cheap&#39;       | 538.1 ns | 10.57 ns | 15.16 ns | 538.6 ns |  2.05 |    0.07 | 0.1831 | 0.0010 |   3.37 KB |        2.13 |
