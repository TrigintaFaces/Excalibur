```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8117/25H2/2025Update/HudsonValley2)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.202
  [Host] : .NET 10.0.6 (10.0.6, 10.0.626.17701), X64 RyuJIT x86-64-v3

Job=comparative-inproc  PowerPlanMode=00000000-0000-0000-0000-000000000000  Toolchain=InProcessEmitToolchain  
InvocationCount=1  IterationCount=10  UnrollFactor=1  
WarmupCount=3  

```
| Method                                                                        | Mean     | Error     | StdDev   | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------------------------------------------------ |---------:|----------:|---------:|------:|--------:|----------:|------------:|
| &#39;Dispatch: pre-routed local command&#39;                                          | 28.04 μs |  7.766 μs | 5.137 μs |  1.03 |    0.26 |   10488 B |        1.00 |
| &#39;Dispatch: pre-routed local query&#39;                                            | 29.94 μs |  1.115 μs | 0.583 μs |  1.10 |    0.20 |   11064 B |        1.05 |
| &#39;Dispatch: pre-routed remote event (AWS SQS)&#39;                                 | 38.86 μs |  9.298 μs | 6.150 μs |  1.43 |    0.34 |   11272 B |        1.07 |
| &#39;Dispatch: pre-routed remote event (Azure Service Bus)&#39;                       | 38.09 μs |  3.475 μs | 1.818 μs |  1.40 |    0.27 |   10984 B |        1.05 |
| &#39;Dispatch: pre-routed remote event (AWS SNS)&#39;                                 | 36.90 μs |  6.516 μs | 4.310 μs |  1.36 |    0.29 |   11272 B |        1.07 |
| &#39;Dispatch: pre-routed remote event (AWS EventBridge)&#39;                         | 38.31 μs |  7.252 μs | 4.797 μs |  1.41 |    0.31 |     904 B |        0.09 |
| &#39;Dispatch: pre-routed remote event (Azure Event Hubs)&#39;                        | 34.68 μs |  5.161 μs | 3.071 μs |  1.28 |    0.26 |   10984 B |        1.05 |
| &#39;Dispatch: pre-routed remote event (gRPC)&#39;                                    | 36.84 μs |  2.642 μs | 1.573 μs |  1.36 |    0.26 |    3592 B |        0.34 |
| &#39;Dispatch: pre-routed remote event (AWS SQS) throughput profile&#39;              | 42.52 μs |  3.302 μs | 1.965 μs |  1.57 |    0.30 |    1960 B |        0.19 |
| &#39;Dispatch: pre-routed remote event (AWS SQS) retry profile&#39;                   | 38.49 μs | 10.904 μs | 7.212 μs |  1.42 |    0.37 |    2320 B |        0.22 |
| &#39;Dispatch: pre-routed remote event (AWS SQS) poison profile&#39;                  | 43.20 μs |  5.277 μs | 3.491 μs |  1.59 |    0.32 |    3904 B |        0.37 |
| &#39;Dispatch: pre-routed remote event (AWS SQS) observability profile&#39;           | 51.42 μs |  5.474 μs | 3.621 μs |  1.89 |    0.37 |   11344 B |        1.08 |
| &#39;Dispatch: pre-routed remote event (Azure Service Bus) throughput profile&#39;    | 42.59 μs |  8.161 μs | 4.856 μs |  1.57 |    0.34 |    5320 B |        0.51 |
| &#39;Dispatch: pre-routed remote event (Azure Service Bus) retry profile&#39;         | 40.66 μs |  3.321 μs | 2.197 μs |  1.50 |    0.29 |   11056 B |        1.05 |
| &#39;Dispatch: pre-routed remote event (Azure Service Bus) poison profile&#39;        | 37.00 μs |  4.449 μs | 2.943 μs |  1.36 |    0.27 |   11008 B |        1.05 |
| &#39;Dispatch: pre-routed remote event (Azure Service Bus) observability profile&#39; | 45.02 μs |  3.757 μs | 1.965 μs |  1.66 |    0.31 |   11056 B |        1.05 |
| &#39;Dispatch: pre-routed remote event (AWS SNS) throughput profile&#39;              | 38.27 μs |  3.191 μs | 2.110 μs |  1.41 |    0.27 |   11320 B |        1.08 |
| &#39;Dispatch: pre-routed remote event (AWS SNS) retry profile&#39;                   | 40.14 μs |  6.175 μs | 4.084 μs |  1.48 |    0.31 |   11344 B |        1.08 |
| &#39;Dispatch: pre-routed remote event (AWS SNS) poison profile&#39;                  | 35.98 μs |  2.903 μs | 1.518 μs |  1.32 |    0.25 |     928 B |        0.09 |
| &#39;Dispatch: pre-routed remote event (AWS SNS) observability profile&#39;           | 39.92 μs |  2.717 μs | 1.617 μs |  1.47 |    0.28 |     976 B |        0.09 |
| &#39;Dispatch: pre-routed remote event (AWS EventBridge) throughput profile&#39;      | 39.71 μs |  4.223 μs | 2.793 μs |  1.46 |    0.29 |   11320 B |        1.08 |
| &#39;Dispatch: pre-routed remote event (AWS EventBridge) retry profile&#39;           | 39.74 μs |  4.943 μs | 2.941 μs |  1.46 |    0.29 |     976 B |        0.09 |
| &#39;Dispatch: pre-routed remote event (AWS EventBridge) poison profile&#39;          | 40.51 μs |  3.710 μs | 2.454 μs |  1.49 |    0.29 |   11008 B |        1.05 |
| &#39;Dispatch: pre-routed remote event (AWS EventBridge) observability profile&#39;   | 41.09 μs |  4.476 μs | 2.664 μs |  1.51 |    0.29 |    3664 B |        0.35 |
| &#39;Dispatch: pre-routed remote event (Azure Event Hubs) throughput profile&#39;     | 36.38 μs |  4.127 μs | 2.730 μs |  1.34 |    0.26 |     952 B |        0.09 |
| &#39;Dispatch: pre-routed remote event (Azure Event Hubs) retry profile&#39;          | 39.47 μs |  3.486 μs | 2.306 μs |  1.45 |    0.28 |   11344 B |        1.08 |
| &#39;Dispatch: pre-routed remote event (Azure Event Hubs) poison profile&#39;         | 36.32 μs |  5.383 μs | 3.203 μs |  1.34 |    0.27 |    3280 B |        0.31 |
| &#39;Dispatch: pre-routed remote event (Azure Event Hubs) observability profile&#39;  | 36.22 μs |  8.685 μs | 5.745 μs |  1.33 |    0.32 |   11344 B |        1.08 |
| &#39;Dispatch: pre-routed remote event (gRPC) throughput profile&#39;                 | 35.25 μs |  8.987 μs | 5.944 μs |  1.30 |    0.32 |    1960 B |        0.19 |
| &#39;Dispatch: pre-routed remote event (gRPC) retry profile&#39;                      | 35.91 μs |  4.399 μs | 2.910 μs |  1.32 |    0.26 |    6352 B |        0.61 |
| &#39;Dispatch: pre-routed remote event (gRPC) poison profile&#39;                     | 37.66 μs |  6.306 μs | 3.753 μs |  1.39 |    0.29 |   11008 B |        1.05 |
| &#39;Dispatch: pre-routed remote event (gRPC) observability profile&#39;              | 47.54 μs |  8.743 μs | 5.783 μs |  1.75 |    0.38 |   11344 B |        1.08 |
| &#39;Dispatch: pre-routed remote event (Kafka)&#39;                                   | 34.90 μs |  3.487 μs | 1.824 μs |  1.29 |    0.25 |   10984 B |        1.05 |
| &#39;Dispatch: pre-routed remote event (RabbitMQ)&#39;                                | 28.99 μs | 14.259 μs | 9.431 μs |  1.07 |    0.39 |    1864 B |        0.18 |
| &#39;Dispatch: pre-routed Kafka throughput profile&#39;                               | 33.66 μs |  6.369 μs | 3.790 μs |  1.24 |    0.26 |     952 B |        0.09 |
| &#39;Dispatch: pre-routed Kafka retry profile&#39;                                    | 38.25 μs |  9.654 μs | 6.385 μs |  1.41 |    0.35 |    3328 B |        0.32 |
| &#39;Dispatch: pre-routed Kafka poison profile&#39;                                   | 34.34 μs |  5.158 μs | 3.412 μs |  1.26 |    0.26 |   11296 B |        1.08 |
| &#39;Dispatch: pre-routed Kafka observability profile&#39;                            | 38.50 μs |  3.576 μs | 2.128 μs |  1.42 |    0.27 |    4960 B |        0.47 |
| &#39;Dispatch: pre-routed RabbitMQ throughput profile&#39;                            | 46.22 μs |  9.867 μs | 5.871 μs |  1.70 |    0.38 |     952 B |        0.09 |
| &#39;Dispatch: pre-routed RabbitMQ retry profile&#39;                                 | 37.44 μs |  3.263 μs | 2.158 μs |  1.38 |    0.26 |   11344 B |        1.08 |
| &#39;Dispatch: pre-routed RabbitMQ poison profile&#39;                                | 36.21 μs |  4.822 μs | 3.190 μs |  1.33 |    0.27 |     928 B |        0.09 |
| &#39;Dispatch: pre-routed RabbitMQ observability profile&#39;                         | 40.24 μs |  7.881 μs | 4.690 μs |  1.48 |    0.32 |   11344 B |        1.08 |
