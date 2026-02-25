```

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7705)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.102
  [Host]     : .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3


```
| Method                                             | Mean       | Error     | StdDev    | Median      | Ratio | RatioSD | Gen0   | Gen1   | Allocated | Alloc Ratio |
|--------------------------------------------------- |-----------:|----------:|----------:|------------:|------:|--------:|-------:|-------:|----------:|------------:|
| &#39;MemoryPack Serialize Small (100B payload)&#39;        |   100.3 ns |   2.13 ns |   6.11 ns |    99.77 ns |  1.00 |    0.09 | 0.0148 |      - |     280 B |        1.00 |
| &#39;MemoryPack Serialize Medium (1KB payload)&#39;        |   160.0 ns |   4.42 ns |  12.88 ns |   157.42 ns |  1.60 |    0.16 | 0.0632 |      - |    1192 B |        4.26 |
| &#39;MemoryPack Serialize Large (64KB payload)&#39;        | 3,491.4 ns | 122.43 ns | 353.24 ns | 3,508.54 ns | 34.95 |    4.09 | 3.4714 |      - |   65640 B |      234.43 |
| &#39;MemoryPack Deserialize Small (100B payload)&#39;      |   137.2 ns |   3.34 ns |   9.81 ns |   135.08 ns |  1.37 |    0.13 | 0.0377 |      - |     712 B |        2.54 |
| &#39;MemoryPack Deserialize Medium (1KB payload)&#39;      |   194.5 ns |  11.35 ns |  33.11 ns |   185.09 ns |  1.95 |    0.35 | 0.0849 |      - |    1600 B |        5.71 |
| &#39;MemoryPack Deserialize Large (64KB payload)&#39;      | 3,048.2 ns | 160.18 ns | 464.70 ns | 3,019.24 ns | 30.51 |    4.98 | 3.4828 | 0.8698 |   65704 B |      234.66 |
| &#39;MemoryPack Serialize EventEnvelope&#39;               |   113.1 ns |   2.29 ns |   6.14 ns |   112.18 ns |  1.13 |    0.09 | 0.0242 |      - |     456 B |        1.63 |
| &#39;MemoryPack Deserialize EventEnvelope&#39;             |   139.3 ns |   2.73 ns |   7.58 ns |   137.44 ns |  1.39 |    0.11 | 0.0463 |      - |     872 B |        3.11 |
| &#39;MemoryPack Serialize SnapshotEnvelope (4KB)&#39;      |   253.7 ns |  11.49 ns |  33.70 ns |   247.86 ns |  2.54 |    0.37 | 0.2241 |      - |    4232 B |       15.11 |
| &#39;MemoryPack Deserialize SnapshotEnvelope (4KB)&#39;    |   289.3 ns |  18.55 ns |  54.68 ns |   271.75 ns |  2.90 |    0.57 | 0.2427 | 0.0038 |    4568 B |       16.31 |
| &#39;MemoryPack Serialize to BufferWriter (zero-copy)&#39; |   141.2 ns |   3.92 ns |  11.57 ns |   139.45 ns |  1.41 |    0.14 | 0.0451 |      - |     848 B |        3.03 |
| &#39;System.Text.Json Serialize (comparison)&#39;          |   329.5 ns |   6.59 ns |  17.26 ns |   327.22 ns |  3.30 |    0.26 | 0.0415 |      - |     784 B |        2.80 |
| &#39;System.Text.Json Deserialize (comparison)&#39;        |   474.4 ns |   9.36 ns |   9.20 ns |   470.61 ns |  4.75 |    0.30 | 0.0620 |      - |    1184 B |        4.23 |
| &#39;MessagePack Serialize (comparison)&#39;               |   130.9 ns |   2.11 ns |   2.66 ns |   130.65 ns |  1.31 |    0.08 | 0.0155 |      - |     296 B |        1.06 |
| &#39;MessagePack Deserialize (comparison)&#39;             |   283.6 ns |   5.07 ns |   7.74 ns |   283.83 ns |  2.84 |    0.18 | 0.0377 |      - |     712 B |        2.54 |
| &#39;MemoryPack Deserialize from ReadOnlySequence&#39;     |   136.2 ns |   2.75 ns |   3.85 ns |   135.76 ns |  1.36 |    0.09 | 0.0377 |      - |     712 B |        2.54 |
