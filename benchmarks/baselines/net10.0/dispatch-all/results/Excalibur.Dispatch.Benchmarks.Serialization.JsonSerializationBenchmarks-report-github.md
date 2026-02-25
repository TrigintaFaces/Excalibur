```

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7705)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.102
  [Host]     : .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3


```
| Method                            | Mean         | Error       | StdDev      | Ratio    | RatioSD | Gen0    | Gen1    | Gen2    | Allocated | Alloc Ratio |
|---------------------------------- |-------------:|------------:|------------:|---------:|--------:|--------:|--------:|--------:|----------:|------------:|
| SerializeSmallMessage             |     115.7 ns |     2.03 ns |     2.42 ns |     1.00 |    0.03 |  0.0080 |       - |       - |     152 B |        1.00 |
| DeserializeSmallMessage           |     179.1 ns |     3.58 ns |     3.68 ns |     1.55 |    0.04 |  0.0100 |       - |       - |     192 B |        1.26 |
| SerializeMediumMessage            |   2,128.6 ns |    13.71 ns |    12.16 ns |    18.40 |    0.38 |  0.1602 |       - |       - |    3048 B |       20.05 |
| DeserializeMediumMessage          |   4,184.9 ns |    46.11 ns |    43.13 ns |    36.19 |    0.81 |  0.4654 |  0.0076 |       - |    8760 B |       57.63 |
| SerializeLargeMessage             |  63,948.7 ns | 1,261.57 ns | 1,349.86 ns |   552.94 |   15.84 | 27.7100 | 27.7100 | 27.7100 |   88108 B |      579.66 |
| DeserializeLargeMessage           |  71,714.2 ns |   695.40 ns |   616.45 ns |   620.08 |   13.41 | 13.7939 |  6.4697 |       - |  261536 B |    1,720.63 |
| RoundTripSmallMessage             |     332.7 ns |     6.16 ns |     5.76 ns |     2.88 |    0.07 |  0.0181 |       - |       - |     344 B |        2.26 |
| RoundTripMediumMessage            |   6,820.8 ns |    99.42 ns |    92.99 ns |    58.98 |    1.41 |  0.6256 |  0.0153 |       - |   11808 B |       77.68 |
| RoundTripLargeMessage             | 158,792.3 ns | 3,163.77 ns | 7,205.52 ns | 1,373.01 |   67.62 | 27.5879 | 27.5879 | 27.5879 |  349652 B |    2,300.34 |
| &#39;Serialize Polymorphic Message&#39;   |     916.5 ns |     7.32 ns |     6.84 ns |     7.92 |    0.17 |  0.0696 |       - |       - |    1312 B |        8.63 |
| &#39;Deserialize Polymorphic Message&#39; |   1,754.7 ns |    18.89 ns |    16.75 ns |    15.17 |    0.33 |  0.1564 |       - |       - |    2944 B |       19.37 |
