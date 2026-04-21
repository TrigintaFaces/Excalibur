```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8117/25H2/2025Update/HudsonValley2)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.202
  [Host] : .NET 10.0.6 (10.0.6, 10.0.626.17701), X64 RyuJIT x86-64-v3

Job=warmpath-inproc  PowerPlanMode=00000000-0000-0000-0000-000000000000  Toolchain=InProcessEmitToolchain  

```
| Method                                                                        | Mean     | Error   | StdDev  | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------------------------------------------------ |---------:|--------:|--------:|------:|--------:|-------:|----------:|------------:|
| &#39;Dispatch: pre-routed local command&#39;                                          | 113.6 ns | 0.57 ns | 0.53 ns |  1.00 |    0.01 | 0.0148 |     280 B |        1.00 |
| &#39;Dispatch: pre-routed local query&#39;                                            | 120.1 ns | 2.32 ns | 2.49 ns |  1.06 |    0.02 | 0.0250 |     472 B |        1.69 |
| &#39;Dispatch: pre-routed remote event (AWS SQS)&#39;                                 | 177.9 ns | 3.24 ns | 3.33 ns |  1.57 |    0.03 | 0.0160 |     304 B |        1.09 |
| &#39;Dispatch: pre-routed remote event (Azure Service Bus)&#39;                       | 180.6 ns | 1.36 ns | 1.20 ns |  1.59 |    0.01 | 0.0160 |     304 B |        1.09 |
| &#39;Dispatch: pre-routed remote event (AWS SNS)&#39;                                 | 175.3 ns | 1.13 ns | 1.00 ns |  1.54 |    0.01 | 0.0160 |     304 B |        1.09 |
| &#39;Dispatch: pre-routed remote event (AWS EventBridge)&#39;                         | 182.2 ns | 2.99 ns | 2.79 ns |  1.60 |    0.02 | 0.0160 |     304 B |        1.09 |
| &#39;Dispatch: pre-routed remote event (Azure Event Hubs)&#39;                        | 179.9 ns | 0.84 ns | 0.79 ns |  1.58 |    0.01 | 0.0160 |     304 B |        1.09 |
| &#39;Dispatch: pre-routed remote event (gRPC)&#39;                                    | 172.4 ns | 0.43 ns | 0.38 ns |  1.52 |    0.01 | 0.0160 |     304 B |        1.09 |
| &#39;Dispatch: pre-routed remote event (AWS SQS) throughput profile&#39;              | 237.4 ns | 0.94 ns | 0.88 ns |  2.09 |    0.01 | 0.0186 |     352 B |        1.26 |
| &#39;Dispatch: pre-routed remote event (AWS SQS) retry profile&#39;                   | 240.2 ns | 3.13 ns | 2.77 ns |  2.11 |    0.03 | 0.0196 |     376 B |        1.34 |
| &#39;Dispatch: pre-routed remote event (AWS SQS) poison profile&#39;                  | 223.8 ns | 0.88 ns | 0.69 ns |  1.97 |    0.01 | 0.0174 |     328 B |        1.17 |
| &#39;Dispatch: pre-routed remote event (AWS SQS) observability profile&#39;           | 316.3 ns | 1.72 ns | 1.44 ns |  2.78 |    0.02 | 0.0196 |     376 B |        1.34 |
| &#39;Dispatch: pre-routed remote event (Azure Service Bus) throughput profile&#39;    | 252.8 ns | 0.77 ns | 0.68 ns |  2.23 |    0.01 | 0.0186 |     352 B |        1.26 |
| &#39;Dispatch: pre-routed remote event (Azure Service Bus) retry profile&#39;         | 242.9 ns | 4.57 ns | 4.49 ns |  2.14 |    0.04 | 0.0196 |     376 B |        1.34 |
| &#39;Dispatch: pre-routed remote event (Azure Service Bus) poison profile&#39;        | 229.7 ns | 0.80 ns | 0.71 ns |  2.02 |    0.01 | 0.0174 |     328 B |        1.17 |
| &#39;Dispatch: pre-routed remote event (Azure Service Bus) observability profile&#39; | 319.2 ns | 1.01 ns | 0.89 ns |  2.81 |    0.01 | 0.0196 |     376 B |        1.34 |
| &#39;Dispatch: pre-routed remote event (AWS SNS) throughput profile&#39;              | 239.2 ns | 2.25 ns | 1.88 ns |  2.11 |    0.02 | 0.0186 |     352 B |        1.26 |
| &#39;Dispatch: pre-routed remote event (AWS SNS) retry profile&#39;                   | 235.4 ns | 1.20 ns | 1.06 ns |  2.07 |    0.01 | 0.0196 |     376 B |        1.34 |
| &#39;Dispatch: pre-routed remote event (AWS SNS) poison profile&#39;                  | 227.6 ns | 4.10 ns | 3.63 ns |  2.00 |    0.03 | 0.0174 |     328 B |        1.17 |
| &#39;Dispatch: pre-routed remote event (AWS SNS) observability profile&#39;           | 320.1 ns | 1.71 ns | 1.52 ns |  2.82 |    0.02 | 0.0196 |     376 B |        1.34 |
| &#39;Dispatch: pre-routed remote event (AWS EventBridge) throughput profile&#39;      | 247.4 ns | 3.35 ns | 5.31 ns |  2.18 |    0.05 | 0.0186 |     352 B |        1.26 |
| &#39;Dispatch: pre-routed remote event (AWS EventBridge) retry profile&#39;           | 239.0 ns | 1.45 ns | 1.36 ns |  2.10 |    0.01 | 0.0196 |     376 B |        1.34 |
| &#39;Dispatch: pre-routed remote event (AWS EventBridge) poison profile&#39;          | 223.3 ns | 0.97 ns | 0.81 ns |  1.97 |    0.01 | 0.0174 |     328 B |        1.17 |
| &#39;Dispatch: pre-routed remote event (AWS EventBridge) observability profile&#39;   | 324.3 ns | 2.04 ns | 1.91 ns |  2.86 |    0.02 | 0.0196 |     376 B |        1.34 |
| &#39;Dispatch: pre-routed remote event (Azure Event Hubs) throughput profile&#39;     | 251.7 ns | 2.44 ns | 2.29 ns |  2.22 |    0.02 | 0.0186 |     352 B |        1.26 |
| &#39;Dispatch: pre-routed remote event (Azure Event Hubs) retry profile&#39;          | 240.4 ns | 1.65 ns | 1.47 ns |  2.12 |    0.02 | 0.0196 |     376 B |        1.34 |
| &#39;Dispatch: pre-routed remote event (Azure Event Hubs) poison profile&#39;         | 226.3 ns | 1.43 ns | 1.19 ns |  1.99 |    0.01 | 0.0174 |     328 B |        1.17 |
| &#39;Dispatch: pre-routed remote event (Azure Event Hubs) observability profile&#39;  | 323.6 ns | 1.99 ns | 1.86 ns |  2.85 |    0.02 | 0.0196 |     376 B |        1.34 |
| &#39;Dispatch: pre-routed remote event (gRPC) throughput profile&#39;                 | 236.1 ns | 0.81 ns | 0.63 ns |  2.08 |    0.01 | 0.0186 |     352 B |        1.26 |
| &#39;Dispatch: pre-routed remote event (gRPC) retry profile&#39;                      | 236.5 ns | 0.86 ns | 0.76 ns |  2.08 |    0.01 | 0.0196 |     376 B |        1.34 |
| &#39;Dispatch: pre-routed remote event (gRPC) poison profile&#39;                     | 219.3 ns | 2.07 ns | 1.73 ns |  1.93 |    0.02 | 0.0174 |     328 B |        1.17 |
| &#39;Dispatch: pre-routed remote event (gRPC) observability profile&#39;              | 315.9 ns | 1.10 ns | 0.98 ns |  2.78 |    0.02 | 0.0196 |     376 B |        1.34 |
| &#39;Dispatch: pre-routed remote event (Kafka)&#39;                                   | 175.5 ns | 1.01 ns | 0.94 ns |  1.54 |    0.01 | 0.0160 |     304 B |        1.09 |
| &#39;Dispatch: pre-routed remote event (RabbitMQ)&#39;                                | 176.1 ns | 1.71 ns | 1.43 ns |  1.55 |    0.01 | 0.0160 |     304 B |        1.09 |
| &#39;Dispatch: pre-routed Kafka throughput profile&#39;                               | 237.3 ns | 0.87 ns | 0.73 ns |  2.09 |    0.01 | 0.0186 |     352 B |        1.26 |
| &#39;Dispatch: pre-routed Kafka retry profile&#39;                                    | 240.7 ns | 1.15 ns | 0.96 ns |  2.12 |    0.01 | 0.0196 |     376 B |        1.34 |
| &#39;Dispatch: pre-routed Kafka poison profile&#39;                                   | 228.0 ns | 0.86 ns | 0.76 ns |  2.01 |    0.01 | 0.0174 |     328 B |        1.17 |
| &#39;Dispatch: pre-routed Kafka observability profile&#39;                            | 321.9 ns | 1.65 ns | 1.38 ns |  2.83 |    0.02 | 0.0196 |     376 B |        1.34 |
| &#39;Dispatch: pre-routed RabbitMQ throughput profile&#39;                            | 243.1 ns | 1.98 ns | 1.75 ns |  2.14 |    0.02 | 0.0186 |     352 B |        1.26 |
| &#39;Dispatch: pre-routed RabbitMQ retry profile&#39;                                 | 234.4 ns | 1.65 ns | 1.54 ns |  2.06 |    0.02 | 0.0196 |     376 B |        1.34 |
| &#39;Dispatch: pre-routed RabbitMQ poison profile&#39;                                | 221.3 ns | 0.93 ns | 0.73 ns |  1.95 |    0.01 | 0.0174 |     328 B |        1.17 |
| &#39;Dispatch: pre-routed RabbitMQ observability profile&#39;                         | 319.8 ns | 1.45 ns | 1.29 ns |  2.81 |    0.02 | 0.0196 |     376 B |        1.34 |
