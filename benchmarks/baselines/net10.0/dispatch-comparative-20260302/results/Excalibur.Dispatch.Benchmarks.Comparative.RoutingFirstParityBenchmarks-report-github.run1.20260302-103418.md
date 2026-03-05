```

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7840)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.103
  [Host] : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=comparative-inproc  Toolchain=InProcessEmitToolchain  

```
| Method                                                                        | Mean      | Error    | StdDev   | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------------------------------------------------ |----------:|---------:|---------:|------:|--------:|-------:|----------:|------------:|
| &#39;Dispatch: pre-routed local command&#39;                                          |  79.98 ns | 1.574 ns | 1.546 ns |  1.00 |    0.03 | 0.0101 |     192 B |        1.00 |
| &#39;Dispatch: pre-routed local query&#39;                                            |  91.87 ns | 1.000 ns | 0.887 ns |  1.15 |    0.02 | 0.0216 |     408 B |        2.12 |
| &#39;Dispatch: pre-routed remote event (AWS SQS)&#39;                                 | 152.55 ns | 1.033 ns | 0.916 ns |  1.91 |    0.04 | 0.0174 |     328 B |        1.71 |
| &#39;Dispatch: pre-routed remote event (Azure Service Bus)&#39;                       | 168.58 ns | 1.493 ns | 1.247 ns |  2.11 |    0.04 | 0.0174 |     328 B |        1.71 |
| &#39;Dispatch: pre-routed remote event (AWS SNS)&#39;                                 | 156.38 ns | 1.164 ns | 1.031 ns |  1.96 |    0.04 | 0.0174 |     328 B |        1.71 |
| &#39;Dispatch: pre-routed remote event (AWS EventBridge)&#39;                         | 162.72 ns | 1.201 ns | 1.065 ns |  2.04 |    0.04 | 0.0174 |     328 B |        1.71 |
| &#39;Dispatch: pre-routed remote event (Azure Event Hubs)&#39;                        | 164.99 ns | 1.738 ns | 1.626 ns |  2.06 |    0.04 | 0.0174 |     328 B |        1.71 |
| &#39;Dispatch: pre-routed remote event (gRPC)&#39;                                    | 150.51 ns | 1.944 ns | 1.623 ns |  1.88 |    0.04 | 0.0174 |     328 B |        1.71 |
| &#39;Dispatch: pre-routed remote event (AWS SQS) throughput profile&#39;              | 218.78 ns | 1.044 ns | 0.872 ns |  2.74 |    0.05 | 0.0198 |     376 B |        1.96 |
| &#39;Dispatch: pre-routed remote event (AWS SQS) retry profile&#39;                   | 207.27 ns | 1.591 ns | 1.411 ns |  2.59 |    0.05 | 0.0212 |     400 B |        2.08 |
| &#39;Dispatch: pre-routed remote event (AWS SQS) poison profile&#39;                  | 198.94 ns | 2.280 ns | 1.904 ns |  2.49 |    0.05 | 0.0186 |     352 B |        1.83 |
| &#39;Dispatch: pre-routed remote event (AWS SQS) observability profile&#39;           | 292.94 ns | 1.467 ns | 1.300 ns |  3.66 |    0.07 | 0.0210 |     400 B |        2.08 |
| &#39;Dispatch: pre-routed remote event (Azure Service Bus) throughput profile&#39;    | 235.26 ns | 0.778 ns | 0.728 ns |  2.94 |    0.06 | 0.0196 |     376 B |        1.96 |
| &#39;Dispatch: pre-routed remote event (Azure Service Bus) retry profile&#39;         | 222.52 ns | 2.081 ns | 1.947 ns |  2.78 |    0.06 | 0.0212 |     400 B |        2.08 |
| &#39;Dispatch: pre-routed remote event (Azure Service Bus) poison profile&#39;        | 204.51 ns | 0.900 ns | 0.702 ns |  2.56 |    0.05 | 0.0186 |     352 B |        1.83 |
| &#39;Dispatch: pre-routed remote event (Azure Service Bus) observability profile&#39; | 304.12 ns | 1.396 ns | 1.237 ns |  3.80 |    0.07 | 0.0210 |     400 B |        2.08 |
| &#39;Dispatch: pre-routed remote event (AWS SNS) throughput profile&#39;              | 219.19 ns | 0.892 ns | 0.697 ns |  2.74 |    0.05 | 0.0198 |     376 B |        1.96 |
| &#39;Dispatch: pre-routed remote event (AWS SNS) retry profile&#39;                   | 211.18 ns | 2.185 ns | 1.937 ns |  2.64 |    0.05 | 0.0212 |     400 B |        2.08 |
| &#39;Dispatch: pre-routed remote event (AWS SNS) poison profile&#39;                  | 192.43 ns | 0.907 ns | 0.848 ns |  2.41 |    0.05 | 0.0186 |     352 B |        1.83 |
| &#39;Dispatch: pre-routed remote event (AWS SNS) observability profile&#39;           | 300.09 ns | 2.317 ns | 1.935 ns |  3.75 |    0.07 | 0.0210 |     400 B |        2.08 |
| &#39;Dispatch: pre-routed remote event (AWS EventBridge) throughput profile&#39;      | 230.44 ns | 2.432 ns | 2.031 ns |  2.88 |    0.06 | 0.0198 |     376 B |        1.96 |
| &#39;Dispatch: pre-routed remote event (AWS EventBridge) retry profile&#39;           | 215.79 ns | 1.731 ns | 1.619 ns |  2.70 |    0.05 | 0.0212 |     400 B |        2.08 |
| &#39;Dispatch: pre-routed remote event (AWS EventBridge) poison profile&#39;          | 201.53 ns | 1.350 ns | 1.197 ns |  2.52 |    0.05 | 0.0186 |     352 B |        1.83 |
| &#39;Dispatch: pre-routed remote event (AWS EventBridge) observability profile&#39;   | 301.22 ns | 2.992 ns | 2.799 ns |  3.77 |    0.08 | 0.0210 |     400 B |        2.08 |
| &#39;Dispatch: pre-routed remote event (Azure Event Hubs) throughput profile&#39;     | 228.63 ns | 0.905 ns | 0.756 ns |  2.86 |    0.05 | 0.0198 |     376 B |        1.96 |
| &#39;Dispatch: pre-routed remote event (Azure Event Hubs) retry profile&#39;          | 217.84 ns | 1.926 ns | 1.609 ns |  2.72 |    0.05 | 0.0212 |     400 B |        2.08 |
| &#39;Dispatch: pre-routed remote event (Azure Event Hubs) poison profile&#39;         | 201.72 ns | 2.211 ns | 1.960 ns |  2.52 |    0.05 | 0.0186 |     352 B |        1.83 |
| &#39;Dispatch: pre-routed remote event (Azure Event Hubs) observability profile&#39;  | 301.73 ns | 1.635 ns | 1.449 ns |  3.77 |    0.07 | 0.0210 |     400 B |        2.08 |
| &#39;Dispatch: pre-routed remote event (gRPC) throughput profile&#39;                 | 214.52 ns | 1.190 ns | 0.993 ns |  2.68 |    0.05 | 0.0198 |     376 B |        1.96 |
| &#39;Dispatch: pre-routed remote event (gRPC) retry profile&#39;                      | 212.30 ns | 2.489 ns | 2.328 ns |  2.66 |    0.06 | 0.0212 |     400 B |        2.08 |
| &#39;Dispatch: pre-routed remote event (gRPC) poison profile&#39;                     | 191.54 ns | 1.048 ns | 0.929 ns |  2.40 |    0.05 | 0.0186 |     352 B |        1.83 |
| &#39;Dispatch: pre-routed remote event (gRPC) observability profile&#39;              | 289.84 ns | 1.375 ns | 1.219 ns |  3.63 |    0.07 | 0.0210 |     400 B |        2.08 |
| &#39;Dispatch: pre-routed remote event (Kafka)&#39;                                   | 159.73 ns | 2.839 ns | 2.656 ns |  2.00 |    0.05 | 0.0174 |     328 B |        1.71 |
| &#39;Dispatch: pre-routed remote event (RabbitMQ)&#39;                                | 153.24 ns | 1.394 ns | 1.164 ns |  1.92 |    0.04 | 0.0174 |     328 B |        1.71 |
| &#39;Dispatch: pre-routed Kafka throughput profile&#39;                               | 215.34 ns | 2.204 ns | 1.954 ns |  2.69 |    0.06 | 0.0198 |     376 B |        1.96 |
| &#39;Dispatch: pre-routed Kafka retry profile&#39;                                    | 206.69 ns | 1.093 ns | 1.022 ns |  2.59 |    0.05 | 0.0212 |     400 B |        2.08 |
| &#39;Dispatch: pre-routed Kafka poison profile&#39;                                   | 195.69 ns | 2.559 ns | 2.394 ns |  2.45 |    0.05 | 0.0186 |     352 B |        1.83 |
| &#39;Dispatch: pre-routed Kafka observability profile&#39;                            | 295.02 ns | 2.348 ns | 2.082 ns |  3.69 |    0.07 | 0.0210 |     400 B |        2.08 |
| &#39;Dispatch: pre-routed RabbitMQ throughput profile&#39;                            | 215.49 ns | 1.211 ns | 1.011 ns |  2.70 |    0.05 | 0.0198 |     376 B |        1.96 |
| &#39;Dispatch: pre-routed RabbitMQ retry profile&#39;                                 | 212.07 ns | 4.255 ns | 4.370 ns |  2.65 |    0.07 | 0.0212 |     400 B |        2.08 |
| &#39;Dispatch: pre-routed RabbitMQ poison profile&#39;                                | 196.65 ns | 1.363 ns | 1.138 ns |  2.46 |    0.05 | 0.0186 |     352 B |        1.83 |
| &#39;Dispatch: pre-routed RabbitMQ observability profile&#39;                         | 297.04 ns | 2.352 ns | 2.200 ns |  3.72 |    0.07 | 0.0210 |     400 B |        2.08 |
