```

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7922)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.103
  [Host] : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=comparative-inproc  PowerPlanMode=00000000-0000-0000-0000-000000000000  Toolchain=InProcessEmitToolchain  
InvocationCount=1  IterationCount=3  UnrollFactor=1  

```
| Method                                                                        | Mean      | Error      | StdDev    | Median    | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------------------------------------------------ |----------:|-----------:|----------:|----------:|------:|--------:|----------:|------------:|
| &#39;Dispatch: pre-routed local command&#39;                                          | 12.867 μs | 123.928 μs | 6.7929 μs |  9.300 μs |  1.17 |    0.70 |     456 B |        1.00 |
| &#39;Dispatch: pre-routed local query&#39;                                            |  9.733 μs |  45.329 μs | 2.4846 μs |  8.400 μs |  0.88 |    0.37 |    5016 B |       11.00 |
| &#39;Dispatch: pre-routed remote event (AWS SQS)&#39;                                 | 11.400 μs |   6.320 μs | 0.3464 μs | 11.200 μs |  1.03 |    0.37 |    5424 B |       11.89 |
| &#39;Dispatch: pre-routed remote event (Azure Service Bus)&#39;                       | 13.900 μs |  26.998 μs | 1.4799 μs | 13.200 μs |  1.26 |    0.46 |    5424 B |       11.89 |
| &#39;Dispatch: pre-routed remote event (AWS SNS)&#39;                                 | 15.067 μs |  48.349 μs | 2.6502 μs | 13.900 μs |  1.37 |    0.53 |    4464 B |        9.79 |
| &#39;Dispatch: pre-routed remote event (AWS EventBridge)&#39;                         | 12.000 μs |   8.360 μs | 0.4583 μs | 12.100 μs |  1.09 |    0.39 |    5184 B |       11.37 |
| &#39;Dispatch: pre-routed remote event (Azure Event Hubs)&#39;                        | 11.533 μs |   4.213 μs | 0.2309 μs | 11.400 μs |  1.05 |    0.37 |    5424 B |       11.89 |
| &#39;Dispatch: pre-routed remote event (gRPC)&#39;                                    | 11.700 μs |   4.827 μs | 0.2646 μs | 11.600 μs |  1.06 |    0.38 |    5424 B |       11.89 |
| &#39;Dispatch: pre-routed remote event (AWS SQS) throughput profile&#39;              | 13.333 μs |  21.765 μs | 1.1930 μs | 12.800 μs |  1.21 |    0.44 |    5520 B |       12.11 |
| &#39;Dispatch: pre-routed remote event (AWS SQS) retry profile&#39;                   | 14.167 μs |  34.487 μs | 1.8903 μs | 13.500 μs |  1.28 |    0.48 |    5160 B |       11.32 |
| &#39;Dispatch: pre-routed remote event (AWS SQS) poison profile&#39;                  | 19.433 μs |  36.730 μs | 2.0133 μs | 19.700 μs |  1.76 |    0.64 |    5448 B |       11.95 |
| &#39;Dispatch: pre-routed remote event (AWS SQS) observability profile&#39;           | 13.700 μs |   8.360 μs | 0.4583 μs | 13.800 μs |  1.24 |    0.44 |    5544 B |       12.16 |
| &#39;Dispatch: pre-routed remote event (Azure Service Bus) throughput profile&#39;    | 15.333 μs |  21.918 μs | 1.2014 μs | 15.400 μs |  1.39 |    0.50 |    5232 B |       11.47 |
| &#39;Dispatch: pre-routed remote event (Azure Service Bus) retry profile&#39;         | 25.167 μs |  40.562 μs | 2.2234 μs | 26.400 μs |  2.28 |    0.83 |    5496 B |       12.05 |
| &#39;Dispatch: pre-routed remote event (Azure Service Bus) poison profile&#39;        | 12.500 μs |  12.640 μs | 0.6928 μs | 12.100 μs |  1.13 |    0.40 |    5496 B |       12.05 |
| &#39;Dispatch: pre-routed remote event (Azure Service Bus) observability profile&#39; | 12.633 μs |   2.787 μs | 0.1528 μs | 12.600 μs |  1.15 |    0.40 |    5496 B |       12.05 |
| &#39;Dispatch: pre-routed remote event (AWS SNS) throughput profile&#39;              | 11.933 μs |   8.999 μs | 0.4933 μs | 11.700 μs |  1.08 |    0.38 |    5472 B |       12.00 |
| &#39;Dispatch: pre-routed remote event (AWS SNS) retry profile&#39;                   | 13.300 μs |  15.905 μs | 0.8718 μs | 12.900 μs |  1.21 |    0.43 |    5496 B |       12.05 |
| &#39;Dispatch: pre-routed remote event (AWS SNS) poison profile&#39;                  | 13.633 μs |  41.375 μs | 2.2679 μs | 12.800 μs |  1.24 |    0.48 |     792 B |        1.74 |
| &#39;Dispatch: pre-routed remote event (AWS SNS) observability profile&#39;           | 14.333 μs |  67.411 μs | 3.6950 μs | 12.200 μs |  1.30 |    0.55 |    5544 B |       12.16 |
| &#39;Dispatch: pre-routed remote event (AWS EventBridge) throughput profile&#39;      | 12.967 μs |  13.448 μs | 0.7371 μs | 12.700 μs |  1.18 |    0.42 |    5520 B |       12.11 |
| &#39;Dispatch: pre-routed remote event (AWS EventBridge) retry profile&#39;           | 13.800 μs |  37.699 μs | 2.0664 μs | 13.500 μs |  1.25 |    0.47 |     840 B |        1.84 |
| &#39;Dispatch: pre-routed remote event (AWS EventBridge) poison profile&#39;          | 12.433 μs |  12.147 μs | 0.6658 μs | 12.100 μs |  1.13 |    0.40 |    5112 B |       11.21 |
| &#39;Dispatch: pre-routed remote event (AWS EventBridge) observability profile&#39;   | 16.200 μs |  47.714 μs | 2.6153 μs | 17.400 μs |  1.47 |    0.56 |    5496 B |       12.05 |
| &#39;Dispatch: pre-routed remote event (Azure Event Hubs) throughput profile&#39;     | 21.467 μs | 133.682 μs | 7.3276 μs | 22.200 μs |  1.95 |    0.92 |    5520 B |       12.11 |
| &#39;Dispatch: pre-routed remote event (Azure Event Hubs) retry profile&#39;          | 12.100 μs |   5.473 μs | 0.3000 μs | 12.100 μs |  1.10 |    0.39 |    5544 B |       12.16 |
| &#39;Dispatch: pre-routed remote event (Azure Event Hubs) poison profile&#39;         | 12.700 μs |   3.160 μs | 0.1732 μs | 12.600 μs |  1.15 |    0.41 |    5448 B |       11.95 |
| &#39;Dispatch: pre-routed remote event (Azure Event Hubs) observability profile&#39;  | 16.167 μs |  24.769 μs | 1.3577 μs | 16.900 μs |  1.47 |    0.53 |    5496 B |       12.05 |
| &#39;Dispatch: pre-routed remote event (gRPC) throughput profile&#39;                 | 15.200 μs |  62.109 μs | 3.4044 μs | 14.500 μs |  1.38 |    0.56 |    5808 B |       12.74 |
| &#39;Dispatch: pre-routed remote event (gRPC) retry profile&#39;                      | 13.400 μs |  25.865 μs | 1.4177 μs | 12.900 μs |  1.22 |    0.44 |     840 B |        1.84 |
| &#39;Dispatch: pre-routed remote event (gRPC) poison profile&#39;                     | 13.233 μs |  15.729 μs | 0.8622 μs | 13.400 μs |  1.20 |    0.43 |    5160 B |       11.32 |
| &#39;Dispatch: pre-routed remote event (gRPC) observability profile&#39;              | 14.833 μs |  11.147 μs | 0.6110 μs | 14.700 μs |  1.35 |    0.48 |    5208 B |       11.42 |
| &#39;Dispatch: pre-routed remote event (Kafka)&#39;                                   | 11.567 μs |   2.787 μs | 0.1528 μs | 11.600 μs |  1.05 |    0.37 |    5184 B |       11.37 |
| &#39;Dispatch: pre-routed remote event (RabbitMQ)&#39;                                | 14.500 μs |  31.966 μs | 1.7521 μs | 14.400 μs |  1.32 |    0.49 |    5136 B |       11.26 |
| &#39;Dispatch: pre-routed Kafka throughput profile&#39;                               | 12.500 μs |   4.827 μs | 0.2646 μs | 12.400 μs |  1.13 |    0.40 |    5472 B |       12.00 |
| &#39;Dispatch: pre-routed Kafka retry profile&#39;                                    | 12.233 μs |   4.591 μs | 0.2517 μs | 12.200 μs |  1.11 |    0.39 |    5832 B |       12.79 |
| &#39;Dispatch: pre-routed Kafka poison profile&#39;                                   | 11.933 μs |   2.107 μs | 0.1155 μs | 12.000 μs |  1.08 |    0.38 |    5160 B |       11.32 |
| &#39;Dispatch: pre-routed Kafka observability profile&#39;                            | 23.067 μs |  55.165 μs | 3.0238 μs | 21.900 μs |  2.09 |    0.78 |    5832 B |       12.79 |
| &#39;Dispatch: pre-routed RabbitMQ throughput profile&#39;                            | 18.300 μs |   5.473 μs | 0.3000 μs | 18.300 μs |  1.66 |    0.59 |    5808 B |       12.74 |
| &#39;Dispatch: pre-routed RabbitMQ retry profile&#39;                                 | 12.900 μs |  14.481 μs | 0.7937 μs | 12.600 μs |  1.17 |    0.42 |    5496 B |       12.05 |
| &#39;Dispatch: pre-routed RabbitMQ poison profile&#39;                                | 14.867 μs |  35.673 μs | 1.9553 μs | 15.400 μs |  1.35 |    0.50 |    5160 B |       11.32 |
| &#39;Dispatch: pre-routed RabbitMQ observability profile&#39;                         | 17.033 μs |  42.132 μs | 2.3094 μs | 15.700 μs |  1.55 |    0.58 |    5208 B |       11.42 |
