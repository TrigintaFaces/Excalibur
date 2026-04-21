```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8117/25H2/2025Update/HudsonValley2)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.202
  [Host] : .NET 10.0.6 (10.0.6, 10.0.626.17701), X64 RyuJIT x86-64-v3

Job=comparative-inproc  PowerPlanMode=00000000-0000-0000-0000-000000000000  Toolchain=InProcessEmitToolchain  
InvocationCount=1  IterationCount=10  UnrollFactor=1  
WarmupCount=3  

```
| Method                                                                        | Mean     | Error     | StdDev    | Median   | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------------------------------------------------ |---------:|----------:|----------:|---------:|------:|--------:|----------:|------------:|
| &#39;Dispatch: pre-routed local command&#39;                                          | 12.97 μs |  1.996 μs |  1.320 μs | 13.20 μs |  1.01 |    0.15 |     504 B |        1.00 |
| &#39;Dispatch: pre-routed local query&#39;                                            | 14.33 μs |  1.933 μs |  1.150 μs | 14.80 μs |  1.12 |    0.15 |     984 B |        1.95 |
| &#39;Dispatch: pre-routed remote event (AWS SQS)&#39;                                 | 25.72 μs |  8.819 μs |  5.248 μs | 26.30 μs |  2.00 |    0.45 |    1192 B |        2.37 |
| &#39;Dispatch: pre-routed remote event (Azure Service Bus)&#39;                       | 26.07 μs |  7.187 μs |  4.277 μs | 24.30 μs |  2.03 |    0.39 |    5944 B |       11.79 |
| &#39;Dispatch: pre-routed remote event (AWS SNS)&#39;                                 | 28.63 μs |  6.616 μs |  3.937 μs | 29.80 μs |  2.23 |    0.38 |    4888 B |        9.70 |
| &#39;Dispatch: pre-routed remote event (AWS EventBridge)&#39;                         | 24.49 μs |  3.781 μs |  2.501 μs | 23.50 μs |  1.91 |    0.28 |    5896 B |       11.70 |
| &#39;Dispatch: pre-routed remote event (Azure Event Hubs)&#39;                        | 24.27 μs |  7.084 μs |  4.686 μs | 23.70 μs |  1.89 |    0.41 |    5944 B |       11.79 |
| &#39;Dispatch: pre-routed remote event (gRPC)&#39;                                    | 37.81 μs |  6.340 μs |  3.773 μs | 37.90 μs |  2.95 |    0.42 |    5944 B |       11.79 |
| &#39;Dispatch: pre-routed remote event (AWS SQS) throughput profile&#39;              | 24.70 μs |  8.556 μs |  5.659 μs | 22.15 μs |  1.92 |    0.47 |    5992 B |       11.89 |
| &#39;Dispatch: pre-routed remote event (AWS SQS) retry profile&#39;                   | 24.48 μs |  6.208 μs |  3.695 μs | 23.60 μs |  1.91 |    0.34 |    5680 B |       11.27 |
| &#39;Dispatch: pre-routed remote event (AWS SQS) poison profile&#39;                  | 21.81 μs |  5.092 μs |  3.030 μs | 21.00 μs |  1.70 |    0.29 |     928 B |        1.84 |
| &#39;Dispatch: pre-routed remote event (AWS SQS) observability profile&#39;           | 22.54 μs |  8.154 μs |  5.393 μs | 21.85 μs |  1.76 |    0.45 |    2320 B |        4.60 |
| &#39;Dispatch: pre-routed remote event (Azure Service Bus) throughput profile&#39;    | 30.22 μs |  2.696 μs |  1.783 μs | 30.40 μs |  2.35 |    0.29 |     952 B |        1.89 |
| &#39;Dispatch: pre-routed remote event (Azure Service Bus) retry profile&#39;         | 20.73 μs |  9.264 μs |  6.128 μs | 20.40 μs |  1.61 |    0.49 |    5680 B |       11.27 |
| &#39;Dispatch: pre-routed remote event (Azure Service Bus) poison profile&#39;        | 36.32 μs |  7.882 μs |  5.213 μs | 36.55 μs |  2.83 |    0.50 |    6256 B |       12.41 |
| &#39;Dispatch: pre-routed remote event (Azure Service Bus) observability profile&#39; | 29.19 μs |  9.696 μs |  6.414 μs | 29.45 μs |  2.27 |    0.54 |    5968 B |       11.84 |
| &#39;Dispatch: pre-routed remote event (AWS SNS) throughput profile&#39;              | 35.03 μs |  4.699 μs |  3.108 μs | 35.80 μs |  2.73 |    0.38 |    6280 B |       12.46 |
| &#39;Dispatch: pre-routed remote event (AWS SNS) retry profile&#39;                   | 24.08 μs |  6.255 μs |  3.722 μs | 23.40 μs |  1.88 |    0.34 |    6304 B |       12.51 |
| &#39;Dispatch: pre-routed remote event (AWS SNS) poison profile&#39;                  | 32.05 μs |  7.909 μs |  5.231 μs | 31.55 μs |  2.50 |    0.47 |    5584 B |       11.08 |
| &#39;Dispatch: pre-routed remote event (AWS SNS) observability profile&#39;           | 40.32 μs |  5.811 μs |  3.844 μs | 39.40 μs |  3.14 |    0.44 |    6016 B |       11.94 |
| &#39;Dispatch: pre-routed remote event (AWS EventBridge) throughput profile&#39;      | 36.12 μs |  6.015 μs |  3.978 μs | 34.95 μs |  2.81 |    0.43 |    5608 B |       11.13 |
| &#39;Dispatch: pre-routed remote event (AWS EventBridge) retry profile&#39;           | 32.60 μs |  4.377 μs |  2.895 μs | 33.50 μs |  2.54 |    0.35 |    6304 B |       12.51 |
| &#39;Dispatch: pre-routed remote event (AWS EventBridge) poison profile&#39;          | 30.51 μs |  4.272 μs |  2.825 μs | 29.95 μs |  2.38 |    0.33 |    5968 B |       11.84 |
| &#39;Dispatch: pre-routed remote event (AWS EventBridge) observability profile&#39;   | 30.11 μs | 11.063 μs |  7.318 μs | 29.05 μs |  2.35 |    0.60 |    5008 B |        9.94 |
| &#39;Dispatch: pre-routed remote event (Azure Event Hubs) throughput profile&#39;     | 25.75 μs |  5.717 μs |  3.782 μs | 23.65 μs |  2.01 |    0.36 |    5992 B |       11.89 |
| &#39;Dispatch: pre-routed remote event (Azure Event Hubs) retry profile&#39;          | 22.62 μs |  6.165 μs |  4.078 μs | 21.50 μs |  1.76 |    0.36 |    6304 B |       12.51 |
| &#39;Dispatch: pre-routed remote event (Azure Event Hubs) poison profile&#39;         | 24.27 μs | 21.795 μs | 14.416 μs | 17.15 μs |  1.89 |    1.10 |    5632 B |       11.17 |
| &#39;Dispatch: pre-routed remote event (Azure Event Hubs) observability profile&#39;  | 44.48 μs |  5.583 μs |  3.322 μs | 43.20 μs |  3.47 |    0.45 |    6016 B |       11.94 |
| &#39;Dispatch: pre-routed remote event (gRPC) throughput profile&#39;                 | 20.16 μs |  5.513 μs |  3.646 μs | 20.05 μs |  1.57 |    0.32 |    5944 B |       11.79 |
| &#39;Dispatch: pre-routed remote event (gRPC) retry profile&#39;                      | 25.36 μs |  4.575 μs |  3.026 μs | 25.25 μs |  1.98 |    0.31 |    6016 B |       11.94 |
| &#39;Dispatch: pre-routed remote event (gRPC) poison profile&#39;                     | 22.66 μs | 12.192 μs |  8.064 μs | 20.10 μs |  1.77 |    0.63 |    5968 B |       11.84 |
| &#39;Dispatch: pre-routed remote event (gRPC) observability profile&#39;              | 22.09 μs |  4.697 μs |  2.795 μs | 22.60 μs |  1.72 |    0.28 |    4624 B |        9.17 |
| &#39;Dispatch: pre-routed remote event (Kafka)&#39;                                   | 20.46 μs | 10.716 μs |  7.088 μs | 18.50 μs |  1.59 |    0.56 |    4936 B |        9.79 |
| &#39;Dispatch: pre-routed remote event (RabbitMQ)&#39;                                | 25.79 μs |  5.794 μs |  3.030 μs | 25.95 μs |  2.01 |    0.31 |    5608 B |       11.13 |
| &#39;Dispatch: pre-routed Kafka throughput profile&#39;                               | 29.34 μs |  8.180 μs |  5.410 μs | 27.80 μs |  2.29 |    0.47 |    5608 B |       11.13 |
| &#39;Dispatch: pre-routed Kafka retry profile&#39;                                    | 24.08 μs |  4.545 μs |  2.705 μs | 23.60 μs |  1.88 |    0.29 |    5296 B |       10.51 |
| &#39;Dispatch: pre-routed Kafka poison profile&#39;                                   | 20.68 μs |  4.056 μs |  2.413 μs | 20.90 μs |  1.61 |    0.25 |    1216 B |        2.41 |
| &#39;Dispatch: pre-routed Kafka observability profile&#39;                            | 27.81 μs |  2.886 μs |  1.509 μs | 27.55 μs |  2.17 |    0.26 |    5008 B |        9.94 |
| &#39;Dispatch: pre-routed RabbitMQ throughput profile&#39;                            | 29.20 μs |  3.447 μs |  2.280 μs | 28.75 μs |  2.27 |    0.30 |    1240 B |        2.46 |
| &#39;Dispatch: pre-routed RabbitMQ retry profile&#39;                                 | 38.43 μs |  5.368 μs |  3.550 μs | 39.50 μs |  2.99 |    0.42 |    5968 B |       11.84 |
| &#39;Dispatch: pre-routed RabbitMQ poison profile&#39;                                | 26.18 μs | 12.374 μs |  8.185 μs | 23.40 μs |  2.04 |    0.65 |    5968 B |       11.84 |
| &#39;Dispatch: pre-routed RabbitMQ observability profile&#39;                         | 32.50 μs |  7.002 μs |  4.167 μs | 32.70 μs |  2.53 |    0.41 |    5968 B |       11.84 |
