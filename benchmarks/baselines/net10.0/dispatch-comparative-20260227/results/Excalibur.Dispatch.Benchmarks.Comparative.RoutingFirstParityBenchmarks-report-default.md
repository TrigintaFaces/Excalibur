
BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7840)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.103
  [Host] : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=comparative-inproc  Toolchain=InProcessEmitToolchain  

 Method                                                                        | Mean     | Error   | StdDev   | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
------------------------------------------------------------------------------ |---------:|--------:|---------:|------:|--------:|-------:|----------:|------------:|
 'Dispatch: pre-routed local command'                                          | 114.5 ns | 2.26 ns |  2.11 ns |  1.00 |    0.03 | 0.0033 |     192 B |        1.00 |
 'Dispatch: pre-routed local query'                                            | 166.2 ns | 3.21 ns |  5.96 ns |  1.45 |    0.06 | 0.0055 |     424 B |        2.21 |
 'Dispatch: pre-routed remote event (AWS SQS)'                                 | 216.0 ns | 4.26 ns |  5.39 ns |  1.89 |    0.06 | 0.0050 |     392 B |        2.04 |
 'Dispatch: pre-routed remote event (Azure Service Bus)'                       | 216.9 ns | 4.27 ns |  3.99 ns |  1.90 |    0.05 | 0.0050 |     392 B |        2.04 |
 'Dispatch: pre-routed remote event (AWS SQS) throughput profile'              | 278.3 ns | 5.60 ns |  8.72 ns |  2.43 |    0.09 | 0.0057 |     440 B |        2.29 |
 'Dispatch: pre-routed remote event (AWS SQS) retry profile'                   | 274.9 ns | 5.47 ns |  8.01 ns |  2.40 |    0.08 | 0.0057 |     464 B |        2.42 |
 'Dispatch: pre-routed remote event (AWS SQS) poison profile'                  | 260.0 ns | 5.10 ns |  8.24 ns |  2.27 |    0.08 | 0.0052 |     416 B |        2.17 |
 'Dispatch: pre-routed remote event (AWS SQS) observability profile'           | 344.8 ns | 6.56 ns |  6.13 ns |  3.01 |    0.07 | 0.0057 |     464 B |        2.42 |
 'Dispatch: pre-routed remote event (Azure Service Bus) throughput profile'    | 291.7 ns | 5.86 ns |  9.13 ns |  2.55 |    0.09 | 0.0057 |     440 B |        2.29 |
 'Dispatch: pre-routed remote event (Azure Service Bus) retry profile'         | 279.7 ns | 5.58 ns |  8.69 ns |  2.44 |    0.09 | 0.0072 |     464 B |        2.42 |
 'Dispatch: pre-routed remote event (Azure Service Bus) poison profile'        | 262.3 ns | 5.15 ns |  7.71 ns |  2.29 |    0.08 | 0.0052 |     416 B |        2.17 |
 'Dispatch: pre-routed remote event (Azure Service Bus) observability profile' | 352.4 ns | 5.07 ns |  4.50 ns |  3.08 |    0.07 | 0.0057 |     464 B |        2.42 |
 'Dispatch: pre-routed remote event (Kafka)'                                   | 215.7 ns | 4.25 ns |  5.67 ns |  1.88 |    0.06 | 0.0050 |     392 B |        2.04 |
 'Dispatch: pre-routed remote event (RabbitMQ)'                                | 225.9 ns | 4.55 ns |  4.26 ns |  1.97 |    0.05 | 0.0050 |     392 B |        2.04 |
 'Dispatch: pre-routed Kafka throughput profile'                               | 289.9 ns | 5.74 ns |  8.04 ns |  2.53 |    0.08 | 0.0057 |     440 B |        2.29 |
 'Dispatch: pre-routed Kafka retry profile'                                    | 277.8 ns | 5.54 ns | 10.13 ns |  2.43 |    0.10 | 0.0072 |     464 B |        2.42 |
 'Dispatch: pre-routed Kafka poison profile'                                   | 261.2 ns | 5.26 ns | 10.97 ns |  2.28 |    0.10 | 0.0052 |     416 B |        2.17 |
 'Dispatch: pre-routed Kafka observability profile'                            | 348.6 ns | 6.77 ns |  9.49 ns |  3.05 |    0.10 | 0.0057 |     464 B |        2.42 |
 'Dispatch: pre-routed RabbitMQ throughput profile'                            | 284.7 ns | 5.72 ns |  8.38 ns |  2.49 |    0.08 | 0.0057 |     440 B |        2.29 |
 'Dispatch: pre-routed RabbitMQ retry profile'                                 | 282.5 ns | 5.67 ns | 10.07 ns |  2.47 |    0.10 | 0.0072 |     464 B |        2.42 |
 'Dispatch: pre-routed RabbitMQ poison profile'                                | 265.4 ns | 5.29 ns |  9.13 ns |  2.32 |    0.09 | 0.0052 |     416 B |        2.17 |
 'Dispatch: pre-routed RabbitMQ observability profile'                         | 355.8 ns | 6.58 ns |  8.08 ns |  3.11 |    0.09 | 0.0057 |     464 B |        2.42 |
