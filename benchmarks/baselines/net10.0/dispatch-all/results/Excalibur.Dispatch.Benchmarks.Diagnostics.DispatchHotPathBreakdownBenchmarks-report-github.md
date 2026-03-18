```

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7922)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.103
  [Host] : .NET 10.0.3 (10.0.3, 10.0.326.7603), X64 RyuJIT x86-64-v3

Job=diag-default  PowerPlanMode=00000000-0000-0000-0000-000000000000  Toolchain=InProcessEmitToolchain  
IterationCount=8  LaunchCount=1  UnrollFactor=1  
WarmupCount=3  

```
| Method                                            | Mean      | Error     | StdDev    | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|-------------------------------------------------- |----------:|----------:|----------:|------:|--------:|-------:|----------:|------------:|
| &#39;Dispatcher: Single command&#39;                      | 52.961 ns | 1.1545 ns | 0.6038 ns |  1.00 |    0.02 | 0.0085 |     160 B |        1.00 |
| &#39;Dispatcher: Query with response&#39;                 | 67.111 ns | 4.8347 ns | 2.5287 ns |  1.27 |    0.05 | 0.0186 |     352 B |        2.20 |
| &#39;MiddlewareInvoker: Direct invoke&#39;                | 46.897 ns | 3.2384 ns | 1.4379 ns |  0.89 |    0.03 | 0.0148 |     280 B |        1.75 |
| &#39;FinalDispatchHandler: Action&#39;                    | 91.581 ns | 3.5286 ns | 1.5667 ns |  1.73 |    0.03 | 0.0156 |     296 B |        1.85 |
| &#39;LocalMessageBus: Send action&#39;                    | 71.446 ns | 1.0660 ns | 0.5576 ns |  1.35 |    0.02 | 0.0080 |     152 B |        0.95 |
| &#39;HandlerActivator: Activate&#39;                      | 23.481 ns | 0.8989 ns | 0.4701 ns |  0.44 |    0.01 | 0.0013 |      24 B |        0.15 |
| &#39;HandlerActivator: Activate (precreated context)&#39; | 19.150 ns | 0.2723 ns | 0.1209 ns |  0.36 |    0.00 | 0.0013 |      24 B |        0.15 |
| &#39;HandlerInvoker: Invoke&#39;                          | 51.122 ns | 1.0770 ns | 0.4782 ns |  0.97 |    0.01 | 0.0034 |      64 B |        0.40 |
| &#39;HandlerRegistry: Lookup&#39;                         |  8.240 ns | 0.4219 ns | 0.1873 ns |  0.16 |    0.00 |      - |         - |        0.00 |
