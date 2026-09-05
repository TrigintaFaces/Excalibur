```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9168/25H2/2025Update/HudsonValley2)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.400
  [Host] : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3

Job=warmpath-inproc  PowerPlanMode=00000000-0000-0000-0000-000000000000  Toolchain=InProcessEmitToolchain  

```
| Method                                                                        | Mean      | Error    | StdDev   | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------------------------------------------------ |----------:|---------:|---------:|------:|--------:|-------:|----------:|------------:|
| &#39;Dispatch: pre-routed local command&#39;                                          |  94.34 ns | 1.695 ns | 1.502 ns |  1.00 |    0.02 | 0.0148 |     280 B |        1.00 |
| &#39;Dispatch: pre-routed local query&#39;                                            |  96.01 ns | 1.543 ns | 1.444 ns |  1.02 |    0.02 | 0.0250 |     472 B |        1.69 |
| &#39;Dispatch: pre-routed remote event (AWS SQS)&#39;                                 | 167.33 ns | 2.149 ns | 2.010 ns |  1.77 |    0.03 | 0.0160 |     304 B |        1.09 |
| &#39;Dispatch: pre-routed remote event (Azure Service Bus)&#39;                       | 172.91 ns | 1.240 ns | 1.036 ns |  1.83 |    0.03 | 0.0160 |     304 B |        1.09 |
| &#39;Dispatch: pre-routed remote event (AWS SNS)&#39;                                 | 168.50 ns | 1.280 ns | 1.134 ns |  1.79 |    0.03 | 0.0160 |     304 B |        1.09 |
| &#39;Dispatch: pre-routed remote event (AWS EventBridge)&#39;                         | 168.58 ns | 1.339 ns | 1.252 ns |  1.79 |    0.03 | 0.0160 |     304 B |        1.09 |
| &#39;Dispatch: pre-routed remote event (Azure Event Hubs)&#39;                        | 171.58 ns | 1.053 ns | 0.933 ns |  1.82 |    0.03 | 0.0160 |     304 B |        1.09 |
| &#39;Dispatch: pre-routed remote event (gRPC)&#39;                                    | 167.69 ns | 1.312 ns | 1.227 ns |  1.78 |    0.03 | 0.0160 |     304 B |        1.09 |
| &#39;Dispatch: pre-routed remote event (AWS SQS) throughput profile&#39;              | 223.10 ns | 1.094 ns | 1.024 ns |  2.37 |    0.04 | 0.0186 |     352 B |        1.26 |
| &#39;Dispatch: pre-routed remote event (AWS SQS) retry profile&#39;                   | 224.26 ns | 1.924 ns | 1.705 ns |  2.38 |    0.04 | 0.0198 |     376 B |        1.34 |
| &#39;Dispatch: pre-routed remote event (AWS SQS) poison profile&#39;                  | 208.91 ns | 2.842 ns | 2.918 ns |  2.21 |    0.05 | 0.0174 |     328 B |        1.17 |
| &#39;Dispatch: pre-routed remote event (AWS SQS) observability profile&#39;           | 303.20 ns | 1.766 ns | 1.474 ns |  3.21 |    0.05 | 0.0196 |     376 B |        1.34 |
| &#39;Dispatch: pre-routed remote event (Azure Service Bus) throughput profile&#39;    | 235.22 ns | 2.215 ns | 1.850 ns |  2.49 |    0.04 | 0.0186 |     352 B |        1.26 |
| &#39;Dispatch: pre-routed remote event (Azure Service Bus) retry profile&#39;         | 233.31 ns | 3.032 ns | 2.836 ns |  2.47 |    0.05 | 0.0198 |     376 B |        1.34 |
| &#39;Dispatch: pre-routed remote event (Azure Service Bus) poison profile&#39;        | 215.74 ns | 3.616 ns | 3.020 ns |  2.29 |    0.05 | 0.0174 |     328 B |        1.17 |
| &#39;Dispatch: pre-routed remote event (Azure Service Bus) observability profile&#39; | 309.32 ns | 2.376 ns | 1.984 ns |  3.28 |    0.05 | 0.0196 |     376 B |        1.34 |
| &#39;Dispatch: pre-routed remote event (AWS SNS) throughput profile&#39;              | 226.34 ns | 2.525 ns | 2.239 ns |  2.40 |    0.04 | 0.0186 |     352 B |        1.26 |
| &#39;Dispatch: pre-routed remote event (AWS SNS) retry profile&#39;                   | 224.95 ns | 2.843 ns | 2.520 ns |  2.39 |    0.04 | 0.0198 |     376 B |        1.34 |
| &#39;Dispatch: pre-routed remote event (AWS SNS) poison profile&#39;                  | 208.49 ns | 2.956 ns | 2.765 ns |  2.21 |    0.04 | 0.0174 |     328 B |        1.17 |
| &#39;Dispatch: pre-routed remote event (AWS SNS) observability profile&#39;           | 302.57 ns | 3.491 ns | 3.265 ns |  3.21 |    0.06 | 0.0196 |     376 B |        1.34 |
| &#39;Dispatch: pre-routed remote event (AWS EventBridge) throughput profile&#39;      | 231.12 ns | 3.420 ns | 2.856 ns |  2.45 |    0.05 | 0.0186 |     352 B |        1.26 |
| &#39;Dispatch: pre-routed remote event (AWS EventBridge) retry profile&#39;           | 223.26 ns | 1.883 ns | 1.762 ns |  2.37 |    0.04 | 0.0198 |     376 B |        1.34 |
| &#39;Dispatch: pre-routed remote event (AWS EventBridge) poison profile&#39;          | 210.78 ns | 1.999 ns | 1.669 ns |  2.23 |    0.04 | 0.0174 |     328 B |        1.17 |
| &#39;Dispatch: pre-routed remote event (AWS EventBridge) observability profile&#39;   | 306.07 ns | 2.208 ns | 1.958 ns |  3.25 |    0.05 | 0.0196 |     376 B |        1.34 |
| &#39;Dispatch: pre-routed remote event (Azure Event Hubs) throughput profile&#39;     | 235.49 ns | 1.940 ns | 1.620 ns |  2.50 |    0.04 | 0.0186 |     352 B |        1.26 |
| &#39;Dispatch: pre-routed remote event (Azure Event Hubs) retry profile&#39;          | 228.31 ns | 3.506 ns | 3.280 ns |  2.42 |    0.05 | 0.0198 |     376 B |        1.34 |
| &#39;Dispatch: pre-routed remote event (Azure Event Hubs) poison profile&#39;         | 211.79 ns | 1.226 ns | 1.147 ns |  2.25 |    0.04 | 0.0174 |     328 B |        1.17 |
| &#39;Dispatch: pre-routed remote event (Azure Event Hubs) observability profile&#39;  | 310.57 ns | 4.093 ns | 3.418 ns |  3.29 |    0.06 | 0.0196 |     376 B |        1.34 |
| &#39;Dispatch: pre-routed remote event (gRPC) throughput profile&#39;                 | 223.21 ns | 2.548 ns | 2.616 ns |  2.37 |    0.05 | 0.0186 |     352 B |        1.26 |
| &#39;Dispatch: pre-routed remote event (gRPC) retry profile&#39;                      | 218.59 ns | 2.148 ns | 2.009 ns |  2.32 |    0.04 | 0.0198 |     376 B |        1.34 |
| &#39;Dispatch: pre-routed remote event (gRPC) poison profile&#39;                     | 206.06 ns | 1.254 ns | 1.111 ns |  2.18 |    0.04 | 0.0174 |     328 B |        1.17 |
| &#39;Dispatch: pre-routed remote event (gRPC) observability profile&#39;              | 308.23 ns | 5.235 ns | 4.640 ns |  3.27 |    0.07 | 0.0196 |     376 B |        1.34 |
| &#39;Dispatch: pre-routed remote event (Kafka)&#39;                                   | 169.71 ns | 1.838 ns | 1.629 ns |  1.80 |    0.03 | 0.0160 |     304 B |        1.09 |
| &#39;Dispatch: pre-routed remote event (RabbitMQ)&#39;                                | 169.79 ns | 1.359 ns | 1.272 ns |  1.80 |    0.03 | 0.0160 |     304 B |        1.09 |
| &#39;Dispatch: pre-routed Kafka throughput profile&#39;                               | 226.89 ns | 2.456 ns | 2.178 ns |  2.41 |    0.04 | 0.0186 |     352 B |        1.26 |
| &#39;Dispatch: pre-routed Kafka retry profile&#39;                                    | 225.02 ns | 2.913 ns | 2.582 ns |  2.39 |    0.05 | 0.0198 |     376 B |        1.34 |
| &#39;Dispatch: pre-routed Kafka poison profile&#39;                                   | 210.00 ns | 4.230 ns | 3.749 ns |  2.23 |    0.05 | 0.0174 |     328 B |        1.17 |
| &#39;Dispatch: pre-routed Kafka observability profile&#39;                            | 310.48 ns | 5.652 ns | 5.287 ns |  3.29 |    0.07 | 0.0196 |     376 B |        1.34 |
| &#39;Dispatch: pre-routed RabbitMQ throughput profile&#39;                            | 225.19 ns | 2.290 ns | 2.142 ns |  2.39 |    0.04 | 0.0186 |     352 B |        1.26 |
| &#39;Dispatch: pre-routed RabbitMQ retry profile&#39;                                 | 220.34 ns | 2.362 ns | 2.210 ns |  2.34 |    0.04 | 0.0198 |     376 B |        1.34 |
| &#39;Dispatch: pre-routed RabbitMQ poison profile&#39;                                | 207.79 ns | 2.334 ns | 2.069 ns |  2.20 |    0.04 | 0.0174 |     328 B |        1.17 |
| &#39;Dispatch: pre-routed RabbitMQ observability profile&#39;                         | 300.81 ns | 1.889 ns | 1.675 ns |  3.19 |    0.05 | 0.0196 |     376 B |        1.34 |
