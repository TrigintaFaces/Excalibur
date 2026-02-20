
BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7840)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.103
  [Host] : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=comparative-inproc  Toolchain=InProcessEmitToolchain  

 Method                                                                        | Mean     | Error   | StdDev  | Median   | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
------------------------------------------------------------------------------ |---------:|--------:|--------:|---------:|------:|--------:|-------:|----------:|------------:|
 'Dispatch: pre-routed local command'                                          | 106.0 ns | 2.13 ns | 2.09 ns | 105.1 ns |  1.00 |    0.03 | 0.0101 |     192 B |        1.00 |
 'Dispatch: pre-routed local query'                                            | 141.3 ns | 2.19 ns | 1.71 ns | 141.1 ns |  1.33 |    0.03 | 0.0224 |     424 B |        2.21 |
 'Dispatch: pre-routed remote event (AWS SQS)'                                 | 183.3 ns | 1.86 ns | 1.55 ns | 182.9 ns |  1.73 |    0.04 | 0.0207 |     392 B |        2.04 |
 'Dispatch: pre-routed remote event (Azure Service Bus)'                       | 191.8 ns | 0.89 ns | 0.79 ns | 191.7 ns |  1.81 |    0.03 | 0.0207 |     392 B |        2.04 |
 'Dispatch: pre-routed remote event (AWS SQS) throughput profile'              | 248.1 ns | 1.65 ns | 1.38 ns | 247.7 ns |  2.34 |    0.05 | 0.0234 |     440 B |        2.29 |
 'Dispatch: pre-routed remote event (AWS SQS) retry profile'                   | 246.7 ns | 4.84 ns | 7.82 ns | 243.3 ns |  2.33 |    0.08 | 0.0243 |     464 B |        2.42 |
 'Dispatch: pre-routed remote event (AWS SQS) poison profile'                  | 232.5 ns | 3.65 ns | 3.05 ns | 232.0 ns |  2.19 |    0.05 | 0.0219 |     416 B |        2.17 |
 'Dispatch: pre-routed remote event (AWS SQS) observability profile'           | 322.4 ns | 3.93 ns | 4.03 ns | 321.2 ns |  3.04 |    0.07 | 0.0243 |     464 B |        2.42 |
 'Dispatch: pre-routed remote event (Azure Service Bus) throughput profile'    | 263.0 ns | 3.39 ns | 4.29 ns | 261.4 ns |  2.48 |    0.06 | 0.0234 |     440 B |        2.29 |
 'Dispatch: pre-routed remote event (Azure Service Bus) retry profile'         | 256.1 ns | 4.80 ns | 8.89 ns | 252.2 ns |  2.42 |    0.09 | 0.0243 |     464 B |        2.42 |
 'Dispatch: pre-routed remote event (Azure Service Bus) poison profile'        | 243.8 ns | 4.82 ns | 8.45 ns | 241.3 ns |  2.30 |    0.09 | 0.0219 |     416 B |        2.17 |
 'Dispatch: pre-routed remote event (Azure Service Bus) observability profile' | 334.7 ns | 4.77 ns | 3.98 ns | 333.9 ns |  3.16 |    0.07 | 0.0243 |     464 B |        2.42 |
 'Dispatch: pre-routed remote event (Kafka)'                                   | 189.1 ns | 3.67 ns | 5.02 ns | 188.6 ns |  1.78 |    0.06 | 0.0207 |     392 B |        2.04 |
 'Dispatch: pre-routed remote event (RabbitMQ)'                                | 184.1 ns | 1.25 ns | 1.10 ns | 183.9 ns |  1.74 |    0.03 | 0.0207 |     392 B |        2.04 |
 'Dispatch: pre-routed Kafka throughput profile'                               | 253.2 ns | 4.21 ns | 4.85 ns | 252.1 ns |  2.39 |    0.06 | 0.0234 |     440 B |        2.29 |
 'Dispatch: pre-routed Kafka retry profile'                                    | 240.5 ns | 1.56 ns | 1.39 ns | 240.4 ns |  2.27 |    0.04 | 0.0243 |     464 B |        2.42 |
 'Dispatch: pre-routed Kafka poison profile'                                   | 228.3 ns | 2.10 ns | 1.75 ns | 227.9 ns |  2.15 |    0.04 | 0.0219 |     416 B |        2.17 |
 'Dispatch: pre-routed Kafka observability profile'                            | 321.2 ns | 1.82 ns | 1.61 ns | 320.9 ns |  3.03 |    0.06 | 0.0243 |     464 B |        2.42 |
 'Dispatch: pre-routed RabbitMQ throughput profile'                            | 250.9 ns | 1.37 ns | 1.15 ns | 250.9 ns |  2.37 |    0.05 | 0.0234 |     440 B |        2.29 |
 'Dispatch: pre-routed RabbitMQ retry profile'                                 | 242.3 ns | 1.98 ns | 1.76 ns | 241.8 ns |  2.29 |    0.05 | 0.0243 |     464 B |        2.42 |
 'Dispatch: pre-routed RabbitMQ poison profile'                                | 232.2 ns | 3.63 ns | 3.40 ns | 231.5 ns |  2.19 |    0.05 | 0.0219 |     416 B |        2.17 |
 'Dispatch: pre-routed RabbitMQ observability profile'                         | 321.4 ns | 1.92 ns | 1.70 ns | 321.6 ns |  3.03 |    0.06 | 0.0243 |     464 B |        2.42 |
