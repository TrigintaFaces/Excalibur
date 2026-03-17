```

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7922)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.103
  [Host] : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=warmpath-inproc  PowerPlanMode=00000000-0000-0000-0000-000000000000  Toolchain=InProcessEmitToolchain  

```
| Method                                                                        | Mean      | Error    | StdDev   | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------------------------------------------------ |----------:|---------:|---------:|------:|--------:|-------:|----------:|------------:|
| &#39;Dispatch: pre-routed local command&#39;                                          |  75.42 ns | 0.814 ns | 0.762 ns |  1.00 |    0.01 | 0.0123 |     232 B |        1.00 |
| &#39;Dispatch: pre-routed local query&#39;                                            |  86.58 ns | 1.090 ns | 1.020 ns |  1.15 |    0.02 | 0.0224 |     424 B |        1.83 |
| &#39;Dispatch: pre-routed remote event (AWS SQS)&#39;                                 | 134.53 ns | 2.170 ns | 2.029 ns |  1.78 |    0.03 | 0.0122 |     232 B |        1.00 |
| &#39;Dispatch: pre-routed remote event (Azure Service Bus)&#39;                       | 138.17 ns | 2.762 ns | 2.713 ns |  1.83 |    0.04 | 0.0122 |     232 B |        1.00 |
| &#39;Dispatch: pre-routed remote event (AWS SNS)&#39;                                 | 133.72 ns | 0.855 ns | 0.714 ns |  1.77 |    0.02 | 0.0122 |     232 B |        1.00 |
| &#39;Dispatch: pre-routed remote event (AWS EventBridge)&#39;                         | 139.65 ns | 1.703 ns | 1.593 ns |  1.85 |    0.03 | 0.0122 |     232 B |        1.00 |
| &#39;Dispatch: pre-routed remote event (Azure Event Hubs)&#39;                        | 136.87 ns | 2.047 ns | 1.915 ns |  1.81 |    0.03 | 0.0122 |     232 B |        1.00 |
| &#39;Dispatch: pre-routed remote event (gRPC)&#39;                                    | 128.99 ns | 0.644 ns | 0.571 ns |  1.71 |    0.02 | 0.0122 |     232 B |        1.00 |
| &#39;Dispatch: pre-routed remote event (AWS SQS) throughput profile&#39;              | 189.25 ns | 0.972 ns | 0.861 ns |  2.51 |    0.03 | 0.0148 |     280 B |        1.21 |
| &#39;Dispatch: pre-routed remote event (AWS SQS) retry profile&#39;                   | 183.53 ns | 1.201 ns | 1.123 ns |  2.43 |    0.03 | 0.0160 |     304 B |        1.31 |
| &#39;Dispatch: pre-routed remote event (AWS SQS) poison profile&#39;                  | 176.58 ns | 0.762 ns | 0.676 ns |  2.34 |    0.02 | 0.0136 |     256 B |        1.10 |
| &#39;Dispatch: pre-routed remote event (AWS SQS) observability profile&#39;           | 261.83 ns | 1.147 ns | 1.017 ns |  3.47 |    0.04 | 0.0157 |     304 B |        1.31 |
| &#39;Dispatch: pre-routed remote event (Azure Service Bus) throughput profile&#39;    | 204.64 ns | 3.034 ns | 2.838 ns |  2.71 |    0.04 | 0.0148 |     280 B |        1.21 |
| &#39;Dispatch: pre-routed remote event (Azure Service Bus) retry profile&#39;         | 195.45 ns | 2.891 ns | 2.704 ns |  2.59 |    0.04 | 0.0160 |     304 B |        1.31 |
| &#39;Dispatch: pre-routed remote event (Azure Service Bus) poison profile&#39;        | 180.37 ns | 2.843 ns | 2.659 ns |  2.39 |    0.04 | 0.0136 |     256 B |        1.10 |
| &#39;Dispatch: pre-routed remote event (Azure Service Bus) observability profile&#39; | 272.49 ns | 2.247 ns | 1.992 ns |  3.61 |    0.04 | 0.0157 |     304 B |        1.31 |
| &#39;Dispatch: pre-routed remote event (AWS SNS) throughput profile&#39;              | 188.80 ns | 1.258 ns | 1.050 ns |  2.50 |    0.03 | 0.0148 |     280 B |        1.21 |
| &#39;Dispatch: pre-routed remote event (AWS SNS) retry profile&#39;                   | 180.98 ns | 1.030 ns | 0.964 ns |  2.40 |    0.03 | 0.0160 |     304 B |        1.31 |
| &#39;Dispatch: pre-routed remote event (AWS SNS) poison profile&#39;                  | 171.21 ns | 0.792 ns | 0.618 ns |  2.27 |    0.02 | 0.0136 |     256 B |        1.10 |
| &#39;Dispatch: pre-routed remote event (AWS SNS) observability profile&#39;           | 269.53 ns | 1.898 ns | 1.775 ns |  3.57 |    0.04 | 0.0157 |     304 B |        1.31 |
| &#39;Dispatch: pre-routed remote event (AWS EventBridge) throughput profile&#39;      | 197.75 ns | 3.205 ns | 2.998 ns |  2.62 |    0.05 | 0.0148 |     280 B |        1.21 |
| &#39;Dispatch: pre-routed remote event (AWS EventBridge) retry profile&#39;           | 192.77 ns | 2.680 ns | 2.376 ns |  2.56 |    0.04 | 0.0160 |     304 B |        1.31 |
| &#39;Dispatch: pre-routed remote event (AWS EventBridge) poison profile&#39;          | 174.35 ns | 2.503 ns | 2.678 ns |  2.31 |    0.04 | 0.0136 |     256 B |        1.10 |
| &#39;Dispatch: pre-routed remote event (AWS EventBridge) observability profile&#39;   | 268.65 ns | 4.764 ns | 4.457 ns |  3.56 |    0.07 | 0.0157 |     304 B |        1.31 |
| &#39;Dispatch: pre-routed remote event (Azure Event Hubs) throughput profile&#39;     | 198.86 ns | 3.210 ns | 3.003 ns |  2.64 |    0.05 | 0.0148 |     280 B |        1.21 |
| &#39;Dispatch: pre-routed remote event (Azure Event Hubs) retry profile&#39;          | 192.94 ns | 2.348 ns | 1.960 ns |  2.56 |    0.04 | 0.0160 |     304 B |        1.31 |
| &#39;Dispatch: pre-routed remote event (Azure Event Hubs) poison profile&#39;         | 180.12 ns | 1.344 ns | 1.257 ns |  2.39 |    0.03 | 0.0136 |     256 B |        1.10 |
| &#39;Dispatch: pre-routed remote event (Azure Event Hubs) observability profile&#39;  | 266.36 ns | 1.598 ns | 1.495 ns |  3.53 |    0.04 | 0.0157 |     304 B |        1.31 |
| &#39;Dispatch: pre-routed remote event (gRPC) throughput profile&#39;                 | 187.76 ns | 1.633 ns | 1.528 ns |  2.49 |    0.03 | 0.0148 |     280 B |        1.21 |
| &#39;Dispatch: pre-routed remote event (gRPC) retry profile&#39;                      | 182.57 ns | 1.136 ns | 1.062 ns |  2.42 |    0.03 | 0.0160 |     304 B |        1.31 |
| &#39;Dispatch: pre-routed remote event (gRPC) poison profile&#39;                     | 170.82 ns | 0.783 ns | 0.694 ns |  2.27 |    0.02 | 0.0136 |     256 B |        1.10 |
| &#39;Dispatch: pre-routed remote event (gRPC) observability profile&#39;              | 265.38 ns | 3.126 ns | 2.924 ns |  3.52 |    0.05 | 0.0157 |     304 B |        1.31 |
| &#39;Dispatch: pre-routed remote event (Kafka)&#39;                                   | 132.57 ns | 0.904 ns | 0.801 ns |  1.76 |    0.02 | 0.0122 |     232 B |        1.00 |
| &#39;Dispatch: pre-routed remote event (RabbitMQ)&#39;                                | 131.23 ns | 0.774 ns | 0.724 ns |  1.74 |    0.02 | 0.0122 |     232 B |        1.00 |
| &#39;Dispatch: pre-routed Kafka throughput profile&#39;                               | 190.12 ns | 0.811 ns | 0.719 ns |  2.52 |    0.03 | 0.0148 |     280 B |        1.21 |
| &#39;Dispatch: pre-routed Kafka retry profile&#39;                                    | 186.46 ns | 3.697 ns | 3.796 ns |  2.47 |    0.05 | 0.0160 |     304 B |        1.31 |
| &#39;Dispatch: pre-routed Kafka poison profile&#39;                                   | 175.34 ns | 2.668 ns | 2.496 ns |  2.33 |    0.04 | 0.0136 |     256 B |        1.10 |
| &#39;Dispatch: pre-routed Kafka observability profile&#39;                            | 272.03 ns | 4.669 ns | 4.368 ns |  3.61 |    0.07 | 0.0157 |     304 B |        1.31 |
| &#39;Dispatch: pre-routed RabbitMQ throughput profile&#39;                            | 190.15 ns | 0.829 ns | 0.735 ns |  2.52 |    0.03 | 0.0148 |     280 B |        1.21 |
| &#39;Dispatch: pre-routed RabbitMQ retry profile&#39;                                 | 186.29 ns | 1.107 ns | 1.035 ns |  2.47 |    0.03 | 0.0160 |     304 B |        1.31 |
| &#39;Dispatch: pre-routed RabbitMQ poison profile&#39;                                | 176.35 ns | 0.666 ns | 0.623 ns |  2.34 |    0.02 | 0.0136 |     256 B |        1.10 |
| &#39;Dispatch: pre-routed RabbitMQ observability profile&#39;                         | 268.35 ns | 2.376 ns | 2.222 ns |  3.56 |    0.04 | 0.0157 |     304 B |        1.31 |
