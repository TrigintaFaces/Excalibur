```

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7705)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.102
  [Host]     : .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3


```
| Method                                        | Mean        | Error     | StdDev    | Median      | Ratio | RatioSD | Gen0   | Code Size | Allocated | Alloc Ratio |
|---------------------------------------------- |------------:|----------:|----------:|------------:|------:|--------:|-------:|----------:|----------:|------------:|
| DirectProperty_CorrelationId                  |   0.0035 ns | 0.0087 ns | 0.0081 ns |   0.0000 ns |     ? |       ? |      - |       9 B |         - |           ? |
| DirectProperty_UserId                         |   0.0000 ns | 0.0000 ns | 0.0000 ns |   0.0000 ns |     ? |       ? |      - |       9 B |         - |           ? |
| DirectProperty_TenantId                       |   0.0000 ns | 0.0000 ns | 0.0000 ns |   0.0000 ns |     ? |       ? |      - |      12 B |         - |           ? |
| DirectProperty_MessageId                      |   0.1421 ns | 0.0098 ns | 0.0087 ns |   0.1410 ns |     ? |       ? |      - |      85 B |         - |           ? |
| DirectProperty_Source                         |   0.0000 ns | 0.0000 ns | 0.0000 ns |   0.0000 ns |     ? |       ? |      - |      12 B |         - |           ? |
| DirectProperty_MessageType                    |   0.0000 ns | 0.0000 ns | 0.0000 ns |   0.0000 ns |     ? |       ? |      - |      12 B |         - |           ? |
| ItemsDictionary_CorrelationId                 |   5.1444 ns | 0.1219 ns | 0.1081 ns |   5.0942 ns |     ? |       ? |      - |   2,219 B |         - |           ? |
| ItemsDictionary_UserId                        |   3.6322 ns | 0.1055 ns | 0.1215 ns |   3.5904 ns |     ? |       ? |      - |   2,225 B |         - |           ? |
| ItemsDictionary_TenantId                      |   4.4761 ns | 0.0253 ns | 0.0224 ns |   4.4713 ns |     ? |       ? |      - |   2,225 B |         - |           ? |
| ItemsDictionary_CustomItem                    |   4.0566 ns | 0.0562 ns | 0.0526 ns |   4.0532 ns |     ? |       ? |      - |   2,225 B |         - |           ? |
| ItemsDictionary_TransportSpecific_SQS         |   5.1453 ns | 0.0927 ns | 0.0774 ns |   5.1353 ns |     ? |       ? |      - |   2,225 B |         - |           ? |
| ItemsDictionary_TransportSpecific_RabbitMQ    |   5.3341 ns | 0.1434 ns | 0.3975 ns |   5.1789 ns |     ? |       ? |      - |   2,225 B |         - |           ? |
| ItemsDictionary_TryGetValue_Exists            |   5.5720 ns | 0.1051 ns | 0.0932 ns |   5.5576 ns |     ? |       ? |      - |   1,075 B |         - |           ? |
| ItemsDictionary_TryGetValue_NotExists         |   3.6273 ns | 0.0399 ns | 0.0333 ns |   3.6305 ns |     ? |       ? |      - |   1,075 B |         - |           ? |
| ItemsDictionary_ContainsKey_Exists            |   4.6369 ns | 0.0648 ns | 0.0541 ns |   4.6419 ns |     ? |       ? |      - |   1,978 B |         - |           ? |
| ItemsDictionary_ContainsKey_NotExists         |   3.5970 ns | 0.0456 ns | 0.0381 ns |   3.5890 ns |     ? |       ? |      - |   1,615 B |         - |           ? |
| DirectProperty_Write_CorrelationId            |   0.0024 ns | 0.0025 ns | 0.0022 ns |   0.0018 ns |     ? |       ? |      - |      19 B |         - |           ? |
| ItemsDictionary_Write_NewKey                  |  13.5440 ns | 0.0829 ns | 0.0776 ns |  13.5628 ns |     ? |       ? |      - |   3,748 B |         - |           ? |
| ItemsDictionary_Write_ExistingKey             |  14.2428 ns | 0.2374 ns | 0.2221 ns |  14.1888 ns |     ? |       ? |      - |   3,852 B |         - |           ? |
| GetItem_Typed_String                          |   2.6047 ns | 0.0249 ns | 0.0208 ns |   2.6065 ns |     ? |       ? |      - |   1,235 B |         - |           ? |
| GetItem_Typed_Bool                            |   4.2106 ns | 0.0332 ns | 0.0294 ns |   4.2150 ns |     ? |       ? |      - |   1,267 B |         - |           ? |
| SetItem_Typed                                 |  14.3020 ns | 0.1646 ns | 0.1539 ns |  14.2263 ns |     ? |       ? |      - |   7,757 B |         - |           ? |
| ContainsItem_Exists                           |   3.8646 ns | 0.0284 ns | 0.0265 ns |   3.8535 ns |     ? |       ? |      - |     484 B |         - |           ? |
| ContainsItem_NotExists                        |   3.2735 ns | 0.0524 ns | 0.0465 ns |   3.2783 ns |     ? |       ? |      - |     484 B |         - |           ? |
| CompoundOperation_CachingMiddlewarePattern    |  32.9557 ns | 0.4311 ns | 0.3821 ns |  32.8524 ns |     ? |       ? |      - |   4,280 B |         - |           ? |
| CompoundOperation_ValidationMiddlewarePattern |  55.7786 ns | 0.6566 ns | 0.5821 ns |  55.5324 ns |     ? |       ? | 0.0029 |   4,626 B |      56 B |           ? |
| CompoundOperation_TransportReceiverPattern    |  72.9099 ns | 0.3029 ns | 0.2365 ns |  72.8131 ns |     ? |       ? |      - |   5,075 B |         - |           ? |
| CompoundOperation_FullHotPathAccess           |   0.4250 ns | 0.0097 ns | 0.0086 ns |   0.4252 ns |     ? |       ? |      - |     114 B |         - |           ? |
| CreateChildContext_Basic                      | 321.9526 ns | 3.4956 ns | 3.0988 ns | 320.7600 ns |     ? |       ? | 0.0916 |  11,778 B |    1728 B |           ? |
