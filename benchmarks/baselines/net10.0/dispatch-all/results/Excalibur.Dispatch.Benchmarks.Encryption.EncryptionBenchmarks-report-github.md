```

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7705)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.102
  [Host]     : .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3


```
| Method                     | Mean         | Error        | StdDev       | Median       | Ratio  | RatioSD | Gen0     | Gen1     | Gen2     | Allocated  | Alloc Ratio |
|--------------------------- |-------------:|-------------:|-------------:|-------------:|-------:|--------:|---------:|---------:|---------:|-----------:|------------:|
| EncryptSmall               |     336.5 ns |      6.49 ns |      9.52 ns |     336.3 ns |   1.00 |    0.04 |   0.0577 |        - |        - |    1.06 KB |        1.00 |
| DecryptSmall               |           NA |           NA |           NA |           NA |      ? |       ? |       NA |       NA |       NA |         NA |           ? |
| EncryptMedium              |   1,602.7 ns |     31.83 ns |     54.04 ns |   1,602.4 ns |   4.77 |    0.21 |   0.5455 |        - |        - |   10.06 KB |        9.47 |
| DecryptMedium              |           NA |           NA |           NA |           NA |      ? |       ? |       NA |       NA |       NA |         NA |           ? |
| EncryptLarge               |  15,023.0 ns |    826.77 ns |  2,437.76 ns |  15,346.9 ns |  44.68 |    7.32 |  28.1677 |  28.1372 |  28.1372 |  100.28 KB |       94.38 |
| DecryptLarge               |           NA |           NA |           NA |           NA |      ? |       ? |       NA |       NA |       NA |         NA |           ? |
| EncryptVeryLarge           | 157,096.2 ns | 10,223.83 ns | 30,145.21 ns | 160,845.2 ns | 467.21 |   90.19 | 247.5586 | 247.3145 | 247.3145 | 1026.02 KB |      965.67 |
| DecryptVeryLarge           | 160,786.5 ns |  3,333.46 ns |  9,776.47 ns | 159,871.7 ns | 478.18 |   31.85 | 248.7793 | 248.7793 | 248.7793 |  1024.1 KB |      963.86 |
| RoundTripSmall             |     721.0 ns |     14.28 ns |     23.06 ns |     722.2 ns |   2.14 |    0.09 |   0.1135 |        - |        - |    2.09 KB |        1.96 |
| RoundTripMedium            |   3,404.4 ns |     66.01 ns |     55.12 ns |   3,430.9 ns |  10.12 |    0.32 |   1.0910 |   0.0191 |        - |   20.09 KB |       18.90 |
| EncryptWithNonceGeneration |     441.9 ns |     15.72 ns |     46.35 ns |     456.1 ns |   1.31 |    0.14 |   0.0596 |        - |        - |     1.1 KB |        1.04 |

Benchmarks with issues:
  EncryptionBenchmarks.DecryptSmall: DefaultJob
  EncryptionBenchmarks.DecryptMedium: DefaultJob
  EncryptionBenchmarks.DecryptLarge: DefaultJob
