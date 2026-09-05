```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.400
  [Host] : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3


```
| Method                                        | Job        | Toolchain              | Mean        | Error      | StdDev     | Ratio    | RatioSD | Code Size | Gen0   | Allocated | Alloc Ratio |
|---------------------------------------------- |----------- |----------------------- |------------:|-----------:|-----------:|---------:|--------:|----------:|-------:|----------:|------------:|
| DirectProperty_CorrelationId                  | DefaultJob | Default                |          NA |         NA |         NA |        ? |       ? |        NA |     NA |        NA |           ? |
| DirectProperty_UserId                         | DefaultJob | Default                |          NA |         NA |         NA |        ? |       ? |        NA |     NA |        NA |           ? |
| DirectProperty_TenantId                       | DefaultJob | Default                |          NA |         NA |         NA |        ? |       ? |        NA |     NA |        NA |           ? |
| DirectProperty_MessageId                      | DefaultJob | Default                |          NA |         NA |         NA |        ? |       ? |        NA |     NA |        NA |           ? |
| DirectProperty_Source                         | DefaultJob | Default                |          NA |         NA |         NA |        ? |       ? |        NA |     NA |        NA |           ? |
| DirectProperty_MessageType                    | DefaultJob | Default                |          NA |         NA |         NA |        ? |       ? |        NA |     NA |        NA |           ? |
| ItemsDictionary_CorrelationId                 | DefaultJob | Default                |          NA |         NA |         NA |        ? |       ? |        NA |     NA |        NA |           ? |
| ItemsDictionary_UserId                        | DefaultJob | Default                |          NA |         NA |         NA |        ? |       ? |        NA |     NA |        NA |           ? |
| ItemsDictionary_TenantId                      | DefaultJob | Default                |          NA |         NA |         NA |        ? |       ? |        NA |     NA |        NA |           ? |
| ItemsDictionary_CustomItem                    | DefaultJob | Default                |          NA |         NA |         NA |        ? |       ? |        NA |     NA |        NA |           ? |
| ItemsDictionary_TransportSpecific_SQS         | DefaultJob | Default                |          NA |         NA |         NA |        ? |       ? |        NA |     NA |        NA |           ? |
| ItemsDictionary_TransportSpecific_RabbitMQ    | DefaultJob | Default                |          NA |         NA |         NA |        ? |       ? |        NA |     NA |        NA |           ? |
| ItemsDictionary_TryGetValue_Exists            | DefaultJob | Default                |          NA |         NA |         NA |        ? |       ? |        NA |     NA |        NA |           ? |
| ItemsDictionary_TryGetValue_NotExists         | DefaultJob | Default                |          NA |         NA |         NA |        ? |       ? |        NA |     NA |        NA |           ? |
| ItemsDictionary_ContainsKey_Exists            | DefaultJob | Default                |          NA |         NA |         NA |        ? |       ? |        NA |     NA |        NA |           ? |
| ItemsDictionary_ContainsKey_NotExists         | DefaultJob | Default                |          NA |         NA |         NA |        ? |       ? |        NA |     NA |        NA |           ? |
| DirectProperty_Write_CorrelationId            | DefaultJob | Default                |          NA |         NA |         NA |        ? |       ? |        NA |     NA |        NA |           ? |
| ItemsDictionary_Write_NewKey                  | DefaultJob | Default                |          NA |         NA |         NA |        ? |       ? |        NA |     NA |        NA |           ? |
| ItemsDictionary_Write_ExistingKey             | DefaultJob | Default                |          NA |         NA |         NA |        ? |       ? |        NA |     NA |        NA |           ? |
| GetItem_Typed_String                          | DefaultJob | Default                |          NA |         NA |         NA |        ? |       ? |        NA |     NA |        NA |           ? |
| GetItem_Typed_Bool                            | DefaultJob | Default                |          NA |         NA |         NA |        ? |       ? |        NA |     NA |        NA |           ? |
| SetItem_Typed                                 | DefaultJob | Default                |          NA |         NA |         NA |        ? |       ? |        NA |     NA |        NA |           ? |
| ContainsItem_Exists                           | DefaultJob | Default                |          NA |         NA |         NA |        ? |       ? |        NA |     NA |        NA |           ? |
| ContainsItem_NotExists                        | DefaultJob | Default                |          NA |         NA |         NA |        ? |       ? |        NA |     NA |        NA |           ? |
| CompoundOperation_CachingMiddlewarePattern    | DefaultJob | Default                |          NA |         NA |         NA |        ? |       ? |        NA |     NA |        NA |           ? |
| CompoundOperation_ValidationMiddlewarePattern | DefaultJob | Default                |          NA |         NA |         NA |        ? |       ? |        NA |     NA |        NA |           ? |
| CompoundOperation_TransportReceiverPattern    | DefaultJob | Default                |          NA |         NA |         NA |        ? |       ? |        NA |     NA |        NA |           ? |
| CompoundOperation_FullHotPathAccess           | DefaultJob | Default                |          NA |         NA |         NA |        ? |       ? |        NA |     NA |        NA |           ? |
| CreateChildContext_Basic                      | DefaultJob | Default                |          NA |         NA |         NA |        ? |       ? |        NA |     NA |        NA |           ? |
|                                               |            |                        |             |            |            |          |         |           |        |           |             |
| DirectProperty_CorrelationId                  | Job-JWQWGO | InProcessEmitToolchain |   0.1883 ns |  0.0090 ns |  0.0080 ns |     1.00 |    0.06 |     243 B |      - |         - |          NA |
| DirectProperty_UserId                         | Job-JWQWGO | InProcessEmitToolchain |   7.3207 ns |  0.0502 ns |  0.0445 ns |    38.93 |    1.55 |     243 B |      - |         - |          NA |
| DirectProperty_TenantId                       | Job-JWQWGO | InProcessEmitToolchain |   7.7020 ns |  0.0285 ns |  0.0253 ns |    40.96 |    1.61 |     243 B |      - |         - |          NA |
| DirectProperty_MessageId                      | Job-JWQWGO | InProcessEmitToolchain |   0.2078 ns |  0.0085 ns |  0.0066 ns |     1.11 |    0.06 |     243 B |      - |         - |          NA |
| DirectProperty_Source                         | Job-JWQWGO | InProcessEmitToolchain |   7.0686 ns |  0.1243 ns |  0.1163 ns |    37.59 |    1.59 |     243 B |      - |         - |          NA |
| DirectProperty_MessageType                    | Job-JWQWGO | InProcessEmitToolchain |   5.6962 ns |  0.0580 ns |  0.0543 ns |    30.29 |    1.22 |     243 B |      - |         - |          NA |
| ItemsDictionary_CorrelationId                 | Job-JWQWGO | InProcessEmitToolchain |   6.8856 ns |  0.1000 ns |  0.0936 ns |    36.62 |    1.52 |     243 B |      - |         - |          NA |
| ItemsDictionary_UserId                        | Job-JWQWGO | InProcessEmitToolchain |   5.9700 ns |  0.0451 ns |  0.0400 ns |    31.75 |    1.26 |     243 B |      - |         - |          NA |
| ItemsDictionary_TenantId                      | Job-JWQWGO | InProcessEmitToolchain |   5.6774 ns |  0.0218 ns |  0.0182 ns |    30.19 |    1.19 |     243 B |      - |         - |          NA |
| ItemsDictionary_CustomItem                    | Job-JWQWGO | InProcessEmitToolchain |   6.2131 ns |  0.0944 ns |  0.0837 ns |    33.04 |    1.37 |     243 B |      - |         - |          NA |
| ItemsDictionary_TransportSpecific_SQS         | Job-JWQWGO | InProcessEmitToolchain |   6.2563 ns |  0.0310 ns |  0.0290 ns |    33.27 |    1.31 |     243 B |      - |         - |          NA |
| ItemsDictionary_TransportSpecific_RabbitMQ    | Job-JWQWGO | InProcessEmitToolchain |   6.3371 ns |  0.1037 ns |  0.0970 ns |    33.70 |    1.41 |     243 B |      - |         - |          NA |
| ItemsDictionary_TryGetValue_Exists            | Job-JWQWGO | InProcessEmitToolchain |   7.2794 ns |  0.0265 ns |  0.0235 ns |    38.71 |    1.52 |     243 B |      - |         - |          NA |
| ItemsDictionary_TryGetValue_NotExists         | Job-JWQWGO | InProcessEmitToolchain |   4.0062 ns |  0.0163 ns |  0.0144 ns |    21.31 |    0.84 |     243 B |      - |         - |          NA |
| ItemsDictionary_ContainsKey_Exists            | Job-JWQWGO | InProcessEmitToolchain |   7.1582 ns |  0.0569 ns |  0.0532 ns |    38.07 |    1.52 |     243 B |      - |         - |          NA |
| ItemsDictionary_ContainsKey_NotExists         | Job-JWQWGO | InProcessEmitToolchain |   3.9815 ns |  0.0368 ns |  0.0326 ns |    21.17 |    0.85 |     243 B |      - |         - |          NA |
| DirectProperty_Write_CorrelationId            | Job-JWQWGO | InProcessEmitToolchain |   0.1756 ns |  0.0006 ns |  0.0005 ns |     0.93 |    0.04 |     243 B |      - |         - |          NA |
| ItemsDictionary_Write_NewKey                  | Job-JWQWGO | InProcessEmitToolchain |   5.5864 ns |  0.1313 ns |  0.1349 ns |    29.71 |    1.36 |     243 B |      - |         - |          NA |
| ItemsDictionary_Write_ExistingKey             | Job-JWQWGO | InProcessEmitToolchain |   7.3866 ns |  0.0807 ns |  0.0755 ns |    39.28 |    1.59 |     243 B |      - |         - |          NA |
| GetItem_Typed_String                          | Job-JWQWGO | InProcessEmitToolchain |   4.3304 ns |  0.0290 ns |  0.0257 ns |    23.03 |    0.91 |     243 B |      - |         - |          NA |
| GetItem_Typed_Bool                            | Job-JWQWGO | InProcessEmitToolchain |   4.8829 ns |  0.0359 ns |  0.0336 ns |    25.97 |    1.03 |     243 B |      - |         - |          NA |
| SetItem_Typed                                 | Job-JWQWGO | InProcessEmitToolchain |   3.7407 ns |  0.0181 ns |  0.0169 ns |    19.89 |    0.79 |     243 B |      - |         - |          NA |
| ContainsItem_Exists                           | Job-JWQWGO | InProcessEmitToolchain |   3.8217 ns |  0.0796 ns |  0.0706 ns |    20.32 |    0.88 |     243 B |      - |         - |          NA |
| ContainsItem_NotExists                        | Job-JWQWGO | InProcessEmitToolchain |   3.1710 ns |  0.0194 ns |  0.0181 ns |    16.86 |    0.67 |     243 B |      - |         - |          NA |
| CompoundOperation_CachingMiddlewarePattern    | Job-JWQWGO | InProcessEmitToolchain |  24.6619 ns |  0.1614 ns |  0.1510 ns |   131.15 |    5.21 |     243 B |      - |         - |          NA |
| CompoundOperation_ValidationMiddlewarePattern | Job-JWQWGO | InProcessEmitToolchain |  56.6881 ns |  0.8117 ns |  0.8336 ns |   301.47 |   12.59 |     243 B | 0.0029 |      56 B |          NA |
| CompoundOperation_TransportReceiverPattern    | Job-JWQWGO | InProcessEmitToolchain |  37.6817 ns |  0.1680 ns |  0.1572 ns |   200.39 |    7.91 |     243 B |      - |         - |          NA |
| CompoundOperation_FullHotPathAccess           | Job-JWQWGO | InProcessEmitToolchain |  45.6462 ns |  0.1944 ns |  0.1723 ns |   242.75 |    9.57 |     243 B |      - |         - |          NA |
| CreateChildContext_Basic                      | Job-JWQWGO | InProcessEmitToolchain | 885.3616 ns | 14.4934 ns | 13.5572 ns | 4,708.39 |  197.61 |   4,253 B | 0.1183 |    2232 B |          NA |

Benchmarks with issues:
  MessageContextBenchmarks.DirectProperty_CorrelationId: DefaultJob
  MessageContextBenchmarks.DirectProperty_UserId: DefaultJob
  MessageContextBenchmarks.DirectProperty_TenantId: DefaultJob
  MessageContextBenchmarks.DirectProperty_MessageId: DefaultJob
  MessageContextBenchmarks.DirectProperty_Source: DefaultJob
  MessageContextBenchmarks.DirectProperty_MessageType: DefaultJob
  MessageContextBenchmarks.ItemsDictionary_CorrelationId: DefaultJob
  MessageContextBenchmarks.ItemsDictionary_UserId: DefaultJob
  MessageContextBenchmarks.ItemsDictionary_TenantId: DefaultJob
  MessageContextBenchmarks.ItemsDictionary_CustomItem: DefaultJob
  MessageContextBenchmarks.ItemsDictionary_TransportSpecific_SQS: DefaultJob
  MessageContextBenchmarks.ItemsDictionary_TransportSpecific_RabbitMQ: DefaultJob
  MessageContextBenchmarks.ItemsDictionary_TryGetValue_Exists: DefaultJob
  MessageContextBenchmarks.ItemsDictionary_TryGetValue_NotExists: DefaultJob
  MessageContextBenchmarks.ItemsDictionary_ContainsKey_Exists: DefaultJob
  MessageContextBenchmarks.ItemsDictionary_ContainsKey_NotExists: DefaultJob
  MessageContextBenchmarks.DirectProperty_Write_CorrelationId: DefaultJob
  MessageContextBenchmarks.ItemsDictionary_Write_NewKey: DefaultJob
  MessageContextBenchmarks.ItemsDictionary_Write_ExistingKey: DefaultJob
  MessageContextBenchmarks.GetItem_Typed_String: DefaultJob
  MessageContextBenchmarks.GetItem_Typed_Bool: DefaultJob
  MessageContextBenchmarks.SetItem_Typed: DefaultJob
  MessageContextBenchmarks.ContainsItem_Exists: DefaultJob
  MessageContextBenchmarks.ContainsItem_NotExists: DefaultJob
  MessageContextBenchmarks.CompoundOperation_CachingMiddlewarePattern: DefaultJob
  MessageContextBenchmarks.CompoundOperation_ValidationMiddlewarePattern: DefaultJob
  MessageContextBenchmarks.CompoundOperation_TransportReceiverPattern: DefaultJob
  MessageContextBenchmarks.CompoundOperation_FullHotPathAccess: DefaultJob
  MessageContextBenchmarks.CreateChildContext_Basic: DefaultJob
