```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.400
  [Host] : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3

Job=warmpath-inproc  PowerPlanMode=00000000-0000-0000-0000-000000000000  Toolchain=InProcessEmitToolchain  

```
| Method                                                                        | Mean     | Error   | StdDev  | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------------------------------------------------ |---------:|--------:|--------:|------:|--------:|-------:|----------:|------------:|
| &#39;Dispatch: pre-routed local command&#39;                                          | 115.0 ns | 0.54 ns | 0.48 ns |  1.00 |    0.01 | 0.0148 |     280 B |        1.00 |
| &#39;Dispatch: pre-routed local query&#39;                                            | 124.7 ns | 1.66 ns | 1.38 ns |  1.08 |    0.01 | 0.0250 |     472 B |        1.69 |
| &#39;Dispatch: pre-routed remote event (AWS SQS)&#39;                                 | 173.2 ns | 0.81 ns | 0.76 ns |  1.51 |    0.01 | 0.0160 |     304 B |        1.09 |
| &#39;Dispatch: pre-routed remote event (Azure Service Bus)&#39;                       | 180.5 ns | 2.39 ns | 2.12 ns |  1.57 |    0.02 | 0.0160 |     304 B |        1.09 |
| &#39;Dispatch: pre-routed remote event (AWS SNS)&#39;                                 | 174.2 ns | 2.60 ns | 2.17 ns |  1.52 |    0.02 | 0.0160 |     304 B |        1.09 |
| &#39;Dispatch: pre-routed remote event (AWS EventBridge)&#39;                         | 182.9 ns | 3.69 ns | 4.80 ns |  1.59 |    0.04 | 0.0160 |     304 B |        1.09 |
| &#39;Dispatch: pre-routed remote event (Azure Event Hubs)&#39;                        | 180.9 ns | 3.35 ns | 2.97 ns |  1.57 |    0.03 | 0.0160 |     304 B |        1.09 |
| &#39;Dispatch: pre-routed remote event (gRPC)&#39;                                    | 176.9 ns | 1.47 ns | 1.31 ns |  1.54 |    0.01 | 0.0160 |     304 B |        1.09 |
| &#39;Dispatch: pre-routed remote event (AWS SQS) throughput profile&#39;              | 242.8 ns | 2.94 ns | 2.45 ns |  2.11 |    0.02 | 0.0186 |     352 B |        1.26 |
| &#39;Dispatch: pre-routed remote event (AWS SQS) retry profile&#39;                   | 235.6 ns | 1.21 ns | 1.01 ns |  2.05 |    0.01 | 0.0196 |     376 B |        1.34 |
| &#39;Dispatch: pre-routed remote event (AWS SQS) poison profile&#39;                  | 223.1 ns | 2.42 ns | 2.15 ns |  1.94 |    0.02 | 0.0174 |     328 B |        1.17 |
| &#39;Dispatch: pre-routed remote event (AWS SQS) observability profile&#39;           | 318.5 ns | 2.96 ns | 2.47 ns |  2.77 |    0.02 | 0.0196 |     376 B |        1.34 |
| &#39;Dispatch: pre-routed remote event (Azure Service Bus) throughput profile&#39;    | 251.0 ns | 1.64 ns | 1.53 ns |  2.18 |    0.02 | 0.0186 |     352 B |        1.26 |
| &#39;Dispatch: pre-routed remote event (Azure Service Bus) retry profile&#39;         | 241.8 ns | 2.89 ns | 2.56 ns |  2.10 |    0.02 | 0.0196 |     376 B |        1.34 |
| &#39;Dispatch: pre-routed remote event (Azure Service Bus) poison profile&#39;        | 230.2 ns | 1.27 ns | 1.12 ns |  2.00 |    0.01 | 0.0174 |     328 B |        1.17 |
| &#39;Dispatch: pre-routed remote event (Azure Service Bus) observability profile&#39; | 324.0 ns | 4.13 ns | 3.86 ns |  2.82 |    0.03 | 0.0196 |     376 B |        1.34 |
| &#39;Dispatch: pre-routed remote event (AWS SNS) throughput profile&#39;              | 238.5 ns | 0.95 ns | 0.89 ns |  2.07 |    0.01 | 0.0186 |     352 B |        1.26 |
| &#39;Dispatch: pre-routed remote event (AWS SNS) retry profile&#39;                   | 233.7 ns | 2.81 ns | 2.63 ns |  2.03 |    0.02 | 0.0198 |     376 B |        1.34 |
| &#39;Dispatch: pre-routed remote event (AWS SNS) poison profile&#39;                  | 223.4 ns | 4.22 ns | 3.95 ns |  1.94 |    0.03 | 0.0174 |     328 B |        1.17 |
| &#39;Dispatch: pre-routed remote event (AWS SNS) observability profile&#39;           | 323.1 ns | 2.00 ns | 1.67 ns |  2.81 |    0.02 | 0.0196 |     376 B |        1.34 |
| &#39;Dispatch: pre-routed remote event (AWS EventBridge) throughput profile&#39;      | 247.6 ns | 2.26 ns | 1.88 ns |  2.15 |    0.02 | 0.0186 |     352 B |        1.26 |
| &#39;Dispatch: pre-routed remote event (AWS EventBridge) retry profile&#39;           | 240.3 ns | 0.88 ns | 0.74 ns |  2.09 |    0.01 | 0.0196 |     376 B |        1.34 |
| &#39;Dispatch: pre-routed remote event (AWS EventBridge) poison profile&#39;          | 225.4 ns | 1.72 ns | 1.43 ns |  1.96 |    0.01 | 0.0174 |     328 B |        1.17 |
| &#39;Dispatch: pre-routed remote event (AWS EventBridge) observability profile&#39;   | 322.6 ns | 1.34 ns | 1.12 ns |  2.81 |    0.01 | 0.0196 |     376 B |        1.34 |
| &#39;Dispatch: pre-routed remote event (Azure Event Hubs) throughput profile&#39;     | 252.0 ns | 4.18 ns | 3.70 ns |  2.19 |    0.03 | 0.0186 |     352 B |        1.26 |
| &#39;Dispatch: pre-routed remote event (Azure Event Hubs) retry profile&#39;          | 242.4 ns | 1.55 ns | 1.29 ns |  2.11 |    0.01 | 0.0196 |     376 B |        1.34 |
| &#39;Dispatch: pre-routed remote event (Azure Event Hubs) poison profile&#39;         | 222.3 ns | 1.07 ns | 0.84 ns |  1.93 |    0.01 | 0.0174 |     328 B |        1.17 |
| &#39;Dispatch: pre-routed remote event (Azure Event Hubs) observability profile&#39;  | 322.4 ns | 2.10 ns | 1.86 ns |  2.80 |    0.02 | 0.0196 |     376 B |        1.34 |
| &#39;Dispatch: pre-routed remote event (gRPC) throughput profile&#39;                 | 238.8 ns | 2.13 ns | 2.00 ns |  2.08 |    0.02 | 0.0186 |     352 B |        1.26 |
| &#39;Dispatch: pre-routed remote event (gRPC) retry profile&#39;                      | 238.3 ns | 2.37 ns | 2.10 ns |  2.07 |    0.02 | 0.0196 |     376 B |        1.34 |
| &#39;Dispatch: pre-routed remote event (gRPC) poison profile&#39;                     | 223.1 ns | 4.49 ns | 6.29 ns |  1.94 |    0.05 | 0.0174 |     328 B |        1.17 |
| &#39;Dispatch: pre-routed remote event (gRPC) observability profile&#39;              | 321.2 ns | 5.09 ns | 4.52 ns |  2.79 |    0.04 | 0.0196 |     376 B |        1.34 |
| &#39;Dispatch: pre-routed remote event (Kafka)&#39;                                   | 175.4 ns | 0.68 ns | 0.53 ns |  1.53 |    0.01 | 0.0160 |     304 B |        1.09 |
| &#39;Dispatch: pre-routed remote event (RabbitMQ)&#39;                                | 177.5 ns | 1.98 ns | 1.85 ns |  1.54 |    0.02 | 0.0160 |     304 B |        1.09 |
| &#39;Dispatch: pre-routed Kafka throughput profile&#39;                               | 237.4 ns | 2.35 ns | 2.19 ns |  2.06 |    0.02 | 0.0186 |     352 B |        1.26 |
| &#39;Dispatch: pre-routed Kafka retry profile&#39;                                    | 238.5 ns | 4.21 ns | 3.94 ns |  2.07 |    0.03 | 0.0196 |     376 B |        1.34 |
| &#39;Dispatch: pre-routed Kafka poison profile&#39;                                   | 220.5 ns | 3.13 ns | 2.93 ns |  1.92 |    0.03 | 0.0174 |     328 B |        1.17 |
| &#39;Dispatch: pre-routed Kafka observability profile&#39;                            | 326.4 ns | 5.13 ns | 5.91 ns |  2.84 |    0.05 | 0.0196 |     376 B |        1.34 |
| &#39;Dispatch: pre-routed RabbitMQ throughput profile&#39;                            | 242.4 ns | 3.63 ns | 3.56 ns |  2.11 |    0.03 | 0.0186 |     352 B |        1.26 |
| &#39;Dispatch: pre-routed RabbitMQ retry profile&#39;                                 | 234.6 ns | 2.45 ns | 2.17 ns |  2.04 |    0.02 | 0.0196 |     376 B |        1.34 |
| &#39;Dispatch: pre-routed RabbitMQ poison profile&#39;                                | 219.4 ns | 2.80 ns | 2.62 ns |  1.91 |    0.02 | 0.0174 |     328 B |        1.17 |
| &#39;Dispatch: pre-routed RabbitMQ observability profile&#39;                         | 326.0 ns | 6.32 ns | 7.76 ns |  2.84 |    0.07 | 0.0196 |     376 B |        1.34 |
