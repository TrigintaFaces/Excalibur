
BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7922)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.103
  [Host] : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=comparative-inproc  Toolchain=InProcessEmitToolchain  

 Method                                          | Mean         | Error      | StdDev     | Median       | Ratio  | RatioSD | Gen0   | Gen1   | Allocated | Alloc Ratio |
------------------------------------------------ |-------------:|-----------:|-----------:|-------------:|-------:|--------:|-------:|-------:|----------:|------------:|
 'Dispatch: Single command handler'              |     68.11 ns |   0.609 ns |   0.570 ns |     68.03 ns |   1.00 |    0.01 | 0.0126 |      - |     240 B |        1.00 |
 'Dispatch: Single command strict direct-local'  |     67.94 ns |   0.588 ns |   0.491 ns |     68.08 ns |   1.00 |    0.01 | 0.0126 |      - |     240 B |        1.00 |
 'Dispatch: Single command ultra-local API'      |     31.65 ns |   0.235 ns |   0.219 ns |     31.62 ns |   0.46 |    0.00 | 0.0013 |      - |      24 B |        0.10 |
 'MediatR: Single command handler'               |     44.13 ns |   0.216 ns |   0.181 ns |     44.12 ns |   0.65 |    0.01 | 0.0080 |      - |     152 B |        0.63 |
 'Dispatch: Notification to 3 handlers'          |    112.46 ns |   0.971 ns |   0.861 ns |    112.15 ns |   1.65 |    0.02 | 0.0126 |      - |     240 B |        1.00 |
 'MediatR: Notification to 3 handlers'           |     91.35 ns |   1.786 ns |   1.583 ns |     90.97 ns |   1.34 |    0.02 | 0.0327 |      - |     616 B |        2.57 |
 'Dispatch: Query with return value'             |     79.32 ns |   0.679 ns |   0.530 ns |     79.44 ns |   1.16 |    0.01 | 0.0178 |      - |     336 B |        1.40 |
 'Dispatch: Query with return value (typed API)' |     78.36 ns |   0.779 ns |   0.691 ns |     78.18 ns |   1.15 |    0.01 | 0.0229 |      - |     432 B |        1.80 |
 'Dispatch: Query ultra-local API'               |     53.17 ns |   0.837 ns |   0.783 ns |     53.02 ns |   0.78 |    0.01 | 0.0102 |      - |     192 B |        0.80 |
 'MediatR: Query with return value'              |     52.01 ns |   1.059 ns |   1.177 ns |     52.26 ns |   0.76 |    0.02 | 0.0157 |      - |     296 B |        1.23 |
 'Dispatch: Ultra-local singleton-promoted'      |     34.33 ns |   0.475 ns |   0.421 ns |     34.35 ns |   0.50 |    0.01 | 0.0013 |      - |      24 B |        0.10 |
 'Dispatch: Query singleton-promoted'            |     54.95 ns |   1.800 ns |   4.774 ns |     53.20 ns |   0.81 |    0.07 | 0.0102 |      - |     192 B |        0.80 |
 'Dispatch: 10 concurrent commands'              |    850.76 ns |   7.027 ns |   6.230 ns |    849.61 ns |  12.49 |    0.13 | 0.1097 |      - |    2080 B |        8.67 |
 'MediatR: 10 concurrent commands'               |    826.10 ns |   9.871 ns |   9.234 ns |    826.55 ns |  12.13 |    0.16 | 0.0982 |      - |    1856 B |        7.73 |
 'Dispatch: 100 concurrent commands'             | 12,200.64 ns | 242.065 ns | 226.428 ns | 12,259.86 ns | 179.14 |    3.53 | 1.0223 | 0.0153 |   19360 B |       80.67 |
 'MediatR: 100 concurrent commands'              |  4,848.33 ns |  39.837 ns |  31.102 ns |  4,857.42 ns |  71.19 |    0.72 | 0.9003 |      - |   17064 B |       71.10 |
