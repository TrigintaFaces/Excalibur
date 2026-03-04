```

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7840)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.103
  [Host] : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=comparative-inproc  Toolchain=InProcessEmitToolchain  

```
| Method                                                                        | Mean      | Error    | StdDev   | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------------------------------------------------ |----------:|---------:|---------:|------:|--------:|-------:|----------:|------------:|
| &#39;Dispatch: pre-routed local command&#39;                                          |  78.17 ns | 0.684 ns | 0.534 ns |  1.00 |    0.01 | 0.0101 |     192 B |        1.00 |
| &#39;Dispatch: pre-routed local query&#39;                                            |  93.86 ns | 0.777 ns | 0.689 ns |  1.20 |    0.01 | 0.0216 |     408 B |        2.12 |
| &#39;Dispatch: pre-routed remote event (AWS SQS)&#39;                                 | 157.17 ns | 1.061 ns | 0.940 ns |  2.01 |    0.02 | 0.0174 |     328 B |        1.71 |
| &#39;Dispatch: pre-routed remote event (Azure Service Bus)&#39;                       | 167.66 ns | 1.597 ns | 1.494 ns |  2.14 |    0.02 | 0.0174 |     328 B |        1.71 |
| &#39;Dispatch: pre-routed remote event (AWS SNS)&#39;                                 | 154.70 ns | 1.975 ns | 1.847 ns |  1.98 |    0.03 | 0.0174 |     328 B |        1.71 |
| &#39;Dispatch: pre-routed remote event (AWS EventBridge)&#39;                         | 162.46 ns | 1.511 ns | 1.413 ns |  2.08 |    0.02 | 0.0174 |     328 B |        1.71 |
| &#39;Dispatch: pre-routed remote event (Azure Event Hubs)&#39;                        | 164.17 ns | 2.311 ns | 2.162 ns |  2.10 |    0.03 | 0.0174 |     328 B |        1.71 |
| &#39;Dispatch: pre-routed remote event (gRPC)&#39;                                    | 154.13 ns | 1.428 ns | 1.266 ns |  1.97 |    0.02 | 0.0174 |     328 B |        1.71 |
| &#39;Dispatch: pre-routed remote event (AWS SQS) throughput profile&#39;              | 221.39 ns | 4.356 ns | 4.842 ns |  2.83 |    0.06 | 0.0196 |     376 B |        1.96 |
| &#39;Dispatch: pre-routed remote event (AWS SQS) retry profile&#39;                   | 204.95 ns | 1.364 ns | 1.209 ns |  2.62 |    0.02 | 0.0212 |     400 B |        2.08 |
| &#39;Dispatch: pre-routed remote event (AWS SQS) poison profile&#39;                  | 197.95 ns | 2.382 ns | 2.228 ns |  2.53 |    0.03 | 0.0186 |     352 B |        1.83 |
| &#39;Dispatch: pre-routed remote event (AWS SQS) observability profile&#39;           | 290.59 ns | 4.979 ns | 4.414 ns |  3.72 |    0.06 | 0.0210 |     400 B |        2.08 |
| &#39;Dispatch: pre-routed remote event (Azure Service Bus) throughput profile&#39;    | 235.23 ns | 1.472 ns | 1.229 ns |  3.01 |    0.02 | 0.0196 |     376 B |        1.96 |
| &#39;Dispatch: pre-routed remote event (Azure Service Bus) retry profile&#39;         | 216.57 ns | 1.796 ns | 1.680 ns |  2.77 |    0.03 | 0.0212 |     400 B |        2.08 |
| &#39;Dispatch: pre-routed remote event (Azure Service Bus) poison profile&#39;        | 206.47 ns | 1.398 ns | 1.092 ns |  2.64 |    0.02 | 0.0186 |     352 B |        1.83 |
| &#39;Dispatch: pre-routed remote event (Azure Service Bus) observability profile&#39; | 299.11 ns | 4.557 ns | 4.262 ns |  3.83 |    0.06 | 0.0210 |     400 B |        2.08 |
| &#39;Dispatch: pre-routed remote event (AWS SNS) throughput profile&#39;              | 220.93 ns | 1.218 ns | 1.017 ns |  2.83 |    0.02 | 0.0198 |     376 B |        1.96 |
| &#39;Dispatch: pre-routed remote event (AWS SNS) retry profile&#39;                   | 206.55 ns | 1.858 ns | 1.738 ns |  2.64 |    0.03 | 0.0212 |     400 B |        2.08 |
| &#39;Dispatch: pre-routed remote event (AWS SNS) poison profile&#39;                  | 199.14 ns | 2.593 ns | 2.425 ns |  2.55 |    0.03 | 0.0186 |     352 B |        1.83 |
| &#39;Dispatch: pre-routed remote event (AWS SNS) observability profile&#39;           | 288.08 ns | 1.584 ns | 1.404 ns |  3.69 |    0.03 | 0.0210 |     400 B |        2.08 |
| &#39;Dispatch: pre-routed remote event (AWS EventBridge) throughput profile&#39;      | 233.61 ns | 2.072 ns | 1.837 ns |  2.99 |    0.03 | 0.0198 |     376 B |        1.96 |
| &#39;Dispatch: pre-routed remote event (AWS EventBridge) retry profile&#39;           | 212.37 ns | 2.277 ns | 2.019 ns |  2.72 |    0.03 | 0.0212 |     400 B |        2.08 |
| &#39;Dispatch: pre-routed remote event (AWS EventBridge) poison profile&#39;          | 204.96 ns | 1.279 ns | 1.068 ns |  2.62 |    0.02 | 0.0186 |     352 B |        1.83 |
| &#39;Dispatch: pre-routed remote event (AWS EventBridge) observability profile&#39;   | 298.97 ns | 2.041 ns | 1.704 ns |  3.82 |    0.03 | 0.0210 |     400 B |        2.08 |
| &#39;Dispatch: pre-routed remote event (Azure Event Hubs) throughput profile&#39;     | 229.38 ns | 2.361 ns | 2.209 ns |  2.93 |    0.03 | 0.0198 |     376 B |        1.96 |
| &#39;Dispatch: pre-routed remote event (Azure Event Hubs) retry profile&#39;          | 217.57 ns | 3.473 ns | 3.249 ns |  2.78 |    0.04 | 0.0212 |     400 B |        2.08 |
| &#39;Dispatch: pre-routed remote event (Azure Event Hubs) poison profile&#39;         | 203.09 ns | 0.832 ns | 0.650 ns |  2.60 |    0.02 | 0.0186 |     352 B |        1.83 |
| &#39;Dispatch: pre-routed remote event (Azure Event Hubs) observability profile&#39;  | 301.52 ns | 2.279 ns | 2.132 ns |  3.86 |    0.04 | 0.0210 |     400 B |        2.08 |
| &#39;Dispatch: pre-routed remote event (gRPC) throughput profile&#39;                 | 213.31 ns | 1.741 ns | 1.544 ns |  2.73 |    0.03 | 0.0198 |     376 B |        1.96 |
| &#39;Dispatch: pre-routed remote event (gRPC) retry profile&#39;                      | 204.46 ns | 2.240 ns | 2.096 ns |  2.62 |    0.03 | 0.0212 |     400 B |        2.08 |
| &#39;Dispatch: pre-routed remote event (gRPC) poison profile&#39;                     | 196.06 ns | 1.188 ns | 1.053 ns |  2.51 |    0.02 | 0.0186 |     352 B |        1.83 |
| &#39;Dispatch: pre-routed remote event (gRPC) observability profile&#39;              | 298.66 ns | 2.612 ns | 2.315 ns |  3.82 |    0.04 | 0.0210 |     400 B |        2.08 |
| &#39;Dispatch: pre-routed remote event (Kafka)&#39;                                   | 163.22 ns | 2.170 ns | 2.030 ns |  2.09 |    0.03 | 0.0174 |     328 B |        1.71 |
| &#39;Dispatch: pre-routed remote event (RabbitMQ)&#39;                                | 159.09 ns | 2.815 ns | 2.633 ns |  2.04 |    0.04 | 0.0174 |     328 B |        1.71 |
| &#39;Dispatch: pre-routed Kafka throughput profile&#39;                               | 221.63 ns | 1.292 ns | 1.145 ns |  2.84 |    0.02 | 0.0198 |     376 B |        1.96 |
| &#39;Dispatch: pre-routed Kafka retry profile&#39;                                    | 211.68 ns | 2.405 ns | 2.008 ns |  2.71 |    0.03 | 0.0212 |     400 B |        2.08 |
| &#39;Dispatch: pre-routed Kafka poison profile&#39;                                   | 203.24 ns | 3.220 ns | 2.855 ns |  2.60 |    0.04 | 0.0186 |     352 B |        1.83 |
| &#39;Dispatch: pre-routed Kafka observability profile&#39;                            | 292.44 ns | 2.405 ns | 2.008 ns |  3.74 |    0.03 | 0.0210 |     400 B |        2.08 |
| &#39;Dispatch: pre-routed RabbitMQ throughput profile&#39;                            | 218.09 ns | 2.481 ns | 2.200 ns |  2.79 |    0.03 | 0.0198 |     376 B |        1.96 |
| &#39;Dispatch: pre-routed RabbitMQ retry profile&#39;                                 | 210.44 ns | 4.012 ns | 3.753 ns |  2.69 |    0.05 | 0.0212 |     400 B |        2.08 |
| &#39;Dispatch: pre-routed RabbitMQ poison profile&#39;                                | 196.04 ns | 2.574 ns | 2.407 ns |  2.51 |    0.03 | 0.0186 |     352 B |        1.83 |
| &#39;Dispatch: pre-routed RabbitMQ observability profile&#39;                         | 287.99 ns | 2.046 ns | 1.813 ns |  3.68 |    0.03 | 0.0210 |     400 B |        2.08 |
