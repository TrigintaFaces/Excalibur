```

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7705)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.102
  [Host]     : .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3
  DefaultJob : .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3


```
| Method                                    | Mean | Error | Ratio | RatioSD | Alloc Ratio |
|------------------------------------------ |-----:|------:|------:|--------:|------------:|
| InvokeSingleActionHandler                 |   NA |    NA |     ? |       ? |           ? |
| InvokeSingleEventHandler                  |   NA |    NA |     ? |       ? |           ? |
| InvokeSingleQueryHandler                  |   NA |    NA |     ? |       ? |           ? |
| InvokeMultipleEventHandlers               |   NA |    NA |     ? |       ? |           ? |
| InvokeHandlerWithDI                       |   NA |    NA |     ? |       ? |           ? |
| &#39;Handler Activation (DI resolution only)&#39; |   NA |    NA |     ? |       ? |           ? |
| &#39;Transient Handler Resolution&#39;            |   NA |    NA |     ? |       ? |           ? |
| &#39;Scoped Handler Resolution&#39;               |   NA |    NA |     ? |       ? |           ? |
| &#39;Singleton Handler Resolution&#39;            |   NA |    NA |     ? |       ? |           ? |
| &#39;Batch Handlers (50 handlers)&#39;            |   NA |    NA |     ? |       ? |           ? |

Benchmarks with issues:
  HandlerInvocationBenchmarks.InvokeSingleActionHandler: DefaultJob
  HandlerInvocationBenchmarks.InvokeSingleEventHandler: DefaultJob
  HandlerInvocationBenchmarks.InvokeSingleQueryHandler: DefaultJob
  HandlerInvocationBenchmarks.InvokeMultipleEventHandlers: DefaultJob
  HandlerInvocationBenchmarks.InvokeHandlerWithDI: DefaultJob
  HandlerInvocationBenchmarks.'Handler Activation (DI resolution only)': DefaultJob
  HandlerInvocationBenchmarks.'Transient Handler Resolution': DefaultJob
  HandlerInvocationBenchmarks.'Scoped Handler Resolution': DefaultJob
  HandlerInvocationBenchmarks.'Singleton Handler Resolution': DefaultJob
  HandlerInvocationBenchmarks.'Batch Handlers (50 handlers)': DefaultJob
