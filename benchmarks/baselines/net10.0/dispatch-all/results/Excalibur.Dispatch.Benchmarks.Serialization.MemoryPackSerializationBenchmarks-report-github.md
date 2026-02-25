```

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7705)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.102
  [Host]     : .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3


```
| Method                                             | Mean        | Error     | StdDev     | Ratio | RatioSD | Gen0   | Gen1   | Allocated | Alloc Ratio |
|--------------------------------------------------- |------------:|----------:|-----------:|------:|--------:|-------:|-------:|----------:|------------:|
| &#39;MemoryPack Serialize Small (100B payload)&#39;        |    93.59 ns |  0.842 ns |   0.747 ns |  1.00 |    0.01 | 0.0148 |      - |     280 B |        1.00 |
| &#39;MemoryPack Serialize Medium (1KB payload)&#39;        |   160.91 ns |  2.509 ns |   2.095 ns |  1.72 |    0.03 | 0.0632 |      - |    1192 B |        4.26 |
| &#39;MemoryPack Serialize Large (64KB payload)&#39;        | 3,506.39 ns | 90.417 ns | 263.752 ns | 37.47 |    2.82 | 3.4714 |      - |   65640 B |      234.43 |
| &#39;MemoryPack Deserialize Small (100B payload)&#39;      |   127.93 ns |  2.208 ns |   2.065 ns |  1.37 |    0.02 | 0.0377 |      - |     712 B |        2.54 |
| &#39;MemoryPack Deserialize Medium (1KB payload)&#39;      |   179.81 ns |  3.512 ns |   8.811 ns |  1.92 |    0.09 | 0.0849 |      - |    1600 B |        5.71 |
| &#39;MemoryPack Deserialize Large (64KB payload)&#39;      | 2,220.58 ns | 56.417 ns | 165.462 ns | 23.73 |    1.77 | 3.4828 | 0.8698 |   65704 B |      234.66 |
| &#39;MemoryPack Serialize EventEnvelope&#39;               |   110.45 ns |  1.654 ns |   1.547 ns |  1.18 |    0.02 | 0.0242 |      - |     456 B |        1.63 |
| &#39;MemoryPack Deserialize EventEnvelope&#39;             |   145.48 ns |  2.929 ns |   5.781 ns |  1.55 |    0.06 | 0.0463 |      - |     872 B |        3.11 |
| &#39;MemoryPack Serialize SnapshotEnvelope (4KB)&#39;      |   321.21 ns | 25.580 ns |  75.424 ns |  3.43 |    0.80 | 0.2241 |      - |    4232 B |       15.11 |
| &#39;MemoryPack Deserialize SnapshotEnvelope (4KB)&#39;    |   322.72 ns | 21.654 ns |  63.846 ns |  3.45 |    0.68 | 0.2427 | 0.0038 |    4568 B |       16.31 |
| &#39;MemoryPack Serialize to BufferWriter (zero-copy)&#39; |   132.65 ns |  2.646 ns |   2.475 ns |  1.42 |    0.03 | 0.0451 |      - |     848 B |        3.03 |
| &#39;System.Text.Json Serialize (comparison)&#39;          |   315.87 ns |  6.360 ns |   8.706 ns |  3.38 |    0.09 | 0.0415 |      - |     784 B |        2.80 |
| &#39;System.Text.Json Deserialize (comparison)&#39;        |   473.82 ns |  2.447 ns |   2.043 ns |  5.06 |    0.04 | 0.0625 |      - |    1184 B |        4.23 |
| &#39;MessagePack Serialize (comparison)&#39;               |   131.47 ns |  1.206 ns |   1.007 ns |  1.40 |    0.01 | 0.0155 |      - |     296 B |        1.06 |
| &#39;MessagePack Deserialize (comparison)&#39;             |   285.35 ns |  1.609 ns |   1.344 ns |  3.05 |    0.03 | 0.0377 |      - |     712 B |        2.54 |
| &#39;MemoryPack Deserialize from ReadOnlySequence&#39;     |   136.05 ns |  1.917 ns |   1.793 ns |  1.45 |    0.02 | 0.0377 |      - |     712 B |        2.54 |
