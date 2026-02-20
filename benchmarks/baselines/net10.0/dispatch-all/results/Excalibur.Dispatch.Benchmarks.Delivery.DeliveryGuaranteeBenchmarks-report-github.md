```

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7705)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.102
  [Host]     : .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3
  Job-CNUJVU : .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3

InvocationCount=1  UnrollFactor=1  

```
| Method                  | GuaranteeLevel       | BatchSize | Mean      | Error     | StdDev    | Median    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------ |--------------------- |---------- |----------:|----------:|----------:|----------:|------:|--------:|----------:|------------:|
| **ProcessBatch**            | **AtLeastOnce**          | **10**        |  **3.520 μs** | **0.1370 μs** | **0.3842 μs** |  **3.500 μs** |  **1.01** |    **0.15** |     **328 B** |        **1.00** |
| ThroughputMeasurement   | AtLeastOnce          | 10        |  6.871 μs | 0.3392 μs | 0.9840 μs |  6.600 μs |  1.97 |    0.35 |     840 B |        2.56 |
| FailureRecoveryOverhead | AtLeastOnce          | 10        |  7.025 μs | 0.3485 μs | 1.0221 μs |  7.100 μs |  2.02 |    0.36 |     472 B |        1.44 |
|                         |                      |           |           |           |           |           |       |         |           |             |
| **ProcessBatch**            | **AtLeastOnce**          | **100**       | **12.528 μs** | **0.2486 μs** | **0.3644 μs** | **12.500 μs** |  **1.00** |    **0.04** |    **1048 B** |        **1.00** |
| ThroughputMeasurement   | AtLeastOnce          | 100       | 55.449 μs | 1.6214 μs | 4.7553 μs | 56.200 μs |  4.43 |    0.40 |    3000 B |        2.86 |
| FailureRecoveryOverhead | AtLeastOnce          | 100       |  6.296 μs | 0.2719 μs | 0.7714 μs |  6.000 μs |  0.50 |    0.06 |    1984 B |        1.89 |
|                         |                      |           |           |           |           |           |       |         |           |             |
| **ProcessBatch**            | **MinimizedWindow**      | **10**        |  **3.396 μs** | **0.1310 μs** | **0.3739 μs** |  **3.300 μs** |  **1.01** |    **0.15** |     **328 B** |        **1.00** |
| ThroughputMeasurement   | MinimizedWindow      | 10        |  6.182 μs | 0.1280 μs | 0.3714 μs |  6.100 μs |  1.84 |    0.22 |     840 B |        2.56 |
| FailureRecoveryOverhead | MinimizedWindow      | 10        |  4.009 μs | 0.2967 μs | 0.8369 μs |  3.800 μs |  1.19 |    0.28 |     472 B |        1.44 |
|                         |                      |           |           |           |           |           |       |         |           |             |
| **ProcessBatch**            | **MinimizedWindow**      | **100**       | **12.643 μs** | **0.4360 μs** | **1.2717 μs** | **12.750 μs** |  **1.01** |    **0.17** |    **1048 B** |        **1.00** |
| ThroughputMeasurement   | MinimizedWindow      | 100       | 37.976 μs | 0.7599 μs | 2.1183 μs | 37.500 μs |  3.04 |    0.43 |    3000 B |        2.86 |
| FailureRecoveryOverhead | MinimizedWindow      | 100       |  6.253 μs | 0.2976 μs | 0.8393 μs |  5.950 μs |  0.50 |    0.09 |    1984 B |        1.89 |
|                         |                      |           |           |           |           |           |       |         |           |             |
| **ProcessBatch**            | **Trans(...)cable [27]** | **10**        |  **3.288 μs** | **0.1376 μs** | **0.4014 μs** |  **3.200 μs** |  **1.01** |    **0.17** |     **368 B** |        **1.00** |
| ThroughputMeasurement   | Trans(...)cable [27] | 10        |  4.885 μs | 0.1937 μs | 0.5432 μs |  4.700 μs |  1.51 |    0.24 |     960 B |        2.61 |
| FailureRecoveryOverhead | Trans(...)cable [27] | 10        |  3.787 μs | 0.2309 μs | 0.6624 μs |  3.700 μs |  1.17 |    0.25 |     472 B |        1.28 |
|                         |                      |           |           |           |           |           |       |         |           |             |
| **ProcessBatch**            | **Trans(...)cable [27]** | **100**       |  **6.590 μs** | **0.1864 μs** | **0.5226 μs** |  **6.500 μs** |  **1.01** |    **0.11** |    **1088 B** |        **1.00** |
| ThroughputMeasurement   | Trans(...)cable [27] | 100       | 18.861 μs | 0.5335 μs | 1.5478 μs | 18.500 μs |  2.88 |    0.32 |    3120 B |        2.87 |
| FailureRecoveryOverhead | Trans(...)cable [27] | 100       |  6.494 μs | 0.2700 μs | 0.7747 μs |  6.400 μs |  0.99 |    0.14 |    1984 B |        1.82 |
