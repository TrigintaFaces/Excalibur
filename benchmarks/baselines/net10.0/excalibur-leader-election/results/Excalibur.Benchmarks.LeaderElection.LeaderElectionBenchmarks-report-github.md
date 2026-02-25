```

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7705)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.102
  [Host]     : .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3
  Job-CNUJVU : .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3

InvocationCount=1  UnrollFactor=1  

```
| Method                    | CandidateCount | Mean            | Error         | StdDev       | Median          | Ratio    | RatioSD | Allocated | Alloc Ratio |
|-------------------------- |--------------- |----------------:|--------------:|-------------:|----------------:|---------:|--------:|----------:|------------:|
| **SingleCandidateAcquire**    | **1**              |      **4,603.2 ns** |     **406.41 ns** |   **1,152.9 ns** |      **4,300.0 ns** |     **1.05** |    **0.35** |     **240 B** |        **1.00** |
| MultipleCandidatesCompete | 1              |      4,569.2 ns |     313.55 ns |     879.2 ns |      4,300.0 ns |     1.05 |    0.30 |     272 B |        1.13 |
| CheckIsLeader             | 1              |      4,401.0 ns |     334.28 ns |     964.5 ns |      4,150.0 ns |     1.01 |    0.31 |     240 B |        1.00 |
| GetCurrentLeaderId        | 1              |      4,352.7 ns |     324.73 ns |     921.2 ns |      4,000.0 ns |     1.00 |    0.30 |     240 B |        1.00 |
| UpdateHealth              | 1              |      6,756.1 ns |     476.85 ns |   1,391.0 ns |      6,400.0 ns |     1.55 |    0.46 |     872 B |        3.63 |
| GetCandidateHealth        | 1              |      6,908.3 ns |     573.97 ns |   1,656.0 ns |      6,200.0 ns |     1.58 |    0.51 |     568 B |        2.37 |
| LeaderFailover            | 1              |        629.6 ns |      71.71 ns |     209.2 ns |        600.0 ns |     0.14 |    0.06 |         - |        0.00 |
| UnhealthyStepDown         | 1              |      5,430.3 ns |     287.13 ns |     795.6 ns |      5,500.0 ns |     1.24 |    0.32 |     464 B |        1.93 |
| GracefulShutdown          | 1              |      4,959.4 ns |     264.03 ns |     761.8 ns |      4,750.0 ns |     1.14 |    0.30 |     240 B |        1.00 |
|                           |                |                 |               |              |                 |          |         |           |             |
| **SingleCandidateAcquire**    | **5**              |      **5,097.8 ns** |     **319.80 ns** |     **907.2 ns** |      **5,000.0 ns** |     **1.03** |    **0.25** |     **240 B** |        **1.00** |
| MultipleCandidatesCompete | 5              |      8,247.4 ns |     508.89 ns |   1,460.1 ns |      8,100.0 ns |     1.67 |    0.41 |    1336 B |        5.57 |
| CheckIsLeader             | 5              |      5,786.2 ns |     535.93 ns |   1,529.0 ns |      5,700.0 ns |     1.17 |    0.37 |     240 B |        1.00 |
| GetCurrentLeaderId        | 5              |      5,337.8 ns |     438.39 ns |   1,278.8 ns |      5,100.0 ns |     1.08 |    0.32 |     240 B |        1.00 |
| UpdateHealth              | 5              |      9,206.3 ns |     731.40 ns |   2,098.5 ns |      9,100.0 ns |     1.86 |    0.53 |     872 B |        3.63 |
| GetCandidateHealth        | 5              |      7,801.0 ns |     472.36 ns |   1,362.9 ns |      7,800.0 ns |     1.58 |    0.38 |     568 B |        2.37 |
| LeaderFailover            | 5              | 14,472,886.5 ns | 285,126.08 ns | 484,165.8 ns | 14,375,300.0 ns | 2,923.64 |  498.26 |    1672 B |        6.97 |
| UnhealthyStepDown         | 5              |      6,394.8 ns |     378.91 ns |   1,099.3 ns |      6,200.0 ns |     1.29 |    0.31 |     464 B |        1.93 |
| GracefulShutdown          | 5              |      6,297.9 ns |     459.03 ns |   1,331.7 ns |      6,400.0 ns |     1.27 |    0.34 |     240 B |        1.00 |
|                           |                |                 |               |              |                 |          |         |           |             |
| **SingleCandidateAcquire**    | **10**             |      **5,610.1 ns** |     **478.83 ns** |   **1,404.3 ns** |      **5,700.0 ns** |     **1.06** |    **0.38** |     **240 B** |        **1.00** |
| MultipleCandidatesCompete | 10             |     10,442.1 ns |     509.74 ns |   1,462.6 ns |     10,400.0 ns |     1.98 |    0.58 |    2576 B |       10.73 |
| CheckIsLeader             | 10             |      6,396.8 ns |     560.22 ns |   1,589.3 ns |      6,400.0 ns |     1.21 |    0.44 |     240 B |        1.00 |
| GetCurrentLeaderId        | 10             |      6,571.4 ns |     612.94 ns |   1,718.7 ns |      6,200.0 ns |     1.25 |    0.46 |     240 B |        1.00 |
| UpdateHealth              | 10             |     12,794.9 ns |   1,370.86 ns |   4,020.5 ns |     13,000.0 ns |     2.43 |    1.00 |     872 B |        3.63 |
| GetCandidateHealth        | 10             |      8,768.0 ns |     651.96 ns |   1,922.3 ns |      8,700.0 ns |     1.66 |    0.56 |     568 B |        2.37 |
| LeaderFailover            | 10             | 14,480,466.7 ns | 285,351.46 ns | 427,100.7 ns | 14,421,800.0 ns | 2,748.19 |  702.96 |    2912 B |       12.13 |
| UnhealthyStepDown         | 10             |      7,558.6 ns |     507.24 ns |   1,487.6 ns |      7,600.0 ns |     1.43 |    0.47 |     464 B |        1.93 |
| GracefulShutdown          | 10             |      7,138.1 ns |     519.86 ns |   1,508.2 ns |      7,000.0 ns |     1.35 |    0.45 |     240 B |        1.00 |
