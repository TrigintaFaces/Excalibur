```

BenchmarkDotNet v0.15.4, Windows 11 (10.0.26200.7705)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.102
  [Host]    : .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3
  .NET 10.0 : .NET 10.0.2 (10.0.2, 10.0.225.61305), X64 RyuJIT x86-64-v3

Job=.NET 10.0  Runtime=.NET 10.0  

```
| Type                            | Method                                    | Mean | Error | Ratio | RatioSD | Alloc Ratio |
|-------------------------------- |------------------------------------------ |-----:|------:|------:|--------:|------------:|
| HandlerInvocationBenchmarks     | InvokeSingleActionHandler                 |   NA |    NA |     ? |       ? |           ? |
| MiddlewareChainBenchmarks       | NoMiddleware                              |   NA |    NA |     ? |       ? |           ? |
| PipelineOrchestrationBenchmarks | FullActionPipeline                        |   NA |    NA |     ? |       ? |           ? |
| HandlerInvocationBenchmarks     | InvokeSingleEventHandler                  |   NA |    NA |     ? |       ? |           ? |
| MiddlewareChainBenchmarks       | OneMiddleware                             |   NA |    NA |     ? |       ? |           ? |
| PipelineOrchestrationBenchmarks | FullEventPipelineMultipleHandlers         |   NA |    NA |     ? |       ? |           ? |
| HandlerInvocationBenchmarks     | InvokeSingleQueryHandler                  |   NA |    NA |     ? |       ? |           ? |
| MiddlewareChainBenchmarks       | FiveMiddleware                            |   NA |    NA |     ? |       ? |           ? |
| PipelineOrchestrationBenchmarks | FullQueryPipeline                         |   NA |    NA |     ? |       ? |           ? |
| HandlerInvocationBenchmarks     | InvokeMultipleEventHandlers               |   NA |    NA |     ? |       ? |           ? |
| MiddlewareChainBenchmarks       | TenMiddleware                             |   NA |    NA |     ? |       ? |           ? |
| PipelineOrchestrationBenchmarks | PipelineWithContextPropagation            |   NA |    NA |     ? |       ? |           ? |
| HandlerInvocationBenchmarks     | InvokeHandlerWithDI                       |   NA |    NA |     ? |       ? |           ? |
| MiddlewareChainBenchmarks       | MiddlewareShortCircuit                    |   NA |    NA |     ? |       ? |           ? |
| PipelineOrchestrationBenchmarks | ComplexPipelineWorkflow                   |   NA |    NA |     ? |       ? |           ? |
| HandlerInvocationBenchmarks     | &#39;Handler Activation (DI resolution only)&#39; |   NA |    NA |     ? |       ? |           ? |
| HandlerInvocationBenchmarks     | &#39;Transient Handler Resolution&#39;            |   NA |    NA |     ? |       ? |           ? |
| HandlerInvocationBenchmarks     | &#39;Scoped Handler Resolution&#39;               |   NA |    NA |     ? |       ? |           ? |
| HandlerInvocationBenchmarks     | &#39;Singleton Handler Resolution&#39;            |   NA |    NA |     ? |       ? |           ? |
| HandlerInvocationBenchmarks     | &#39;Batch Handlers (50 handlers)&#39;            |   NA |    NA |     ? |       ? |           ? |

Benchmarks with issues:
  HandlerInvocationBenchmarks.InvokeSingleActionHandler: .NET 10.0(Runtime=.NET 10.0)
  MiddlewareChainBenchmarks.NoMiddleware: .NET 10.0(Runtime=.NET 10.0)
  PipelineOrchestrationBenchmarks.FullActionPipeline: .NET 10.0(Runtime=.NET 10.0)
  HandlerInvocationBenchmarks.InvokeSingleEventHandler: .NET 10.0(Runtime=.NET 10.0)
  MiddlewareChainBenchmarks.OneMiddleware: .NET 10.0(Runtime=.NET 10.0)
  PipelineOrchestrationBenchmarks.FullEventPipelineMultipleHandlers: .NET 10.0(Runtime=.NET 10.0)
  HandlerInvocationBenchmarks.InvokeSingleQueryHandler: .NET 10.0(Runtime=.NET 10.0)
  MiddlewareChainBenchmarks.FiveMiddleware: .NET 10.0(Runtime=.NET 10.0)
  PipelineOrchestrationBenchmarks.FullQueryPipeline: .NET 10.0(Runtime=.NET 10.0)
  HandlerInvocationBenchmarks.InvokeMultipleEventHandlers: .NET 10.0(Runtime=.NET 10.0)
  MiddlewareChainBenchmarks.TenMiddleware: .NET 10.0(Runtime=.NET 10.0)
  PipelineOrchestrationBenchmarks.PipelineWithContextPropagation: .NET 10.0(Runtime=.NET 10.0)
  HandlerInvocationBenchmarks.InvokeHandlerWithDI: .NET 10.0(Runtime=.NET 10.0)
  MiddlewareChainBenchmarks.MiddlewareShortCircuit: .NET 10.0(Runtime=.NET 10.0)
  PipelineOrchestrationBenchmarks.ComplexPipelineWorkflow: .NET 10.0(Runtime=.NET 10.0)
  HandlerInvocationBenchmarks.'Handler Activation (DI resolution only)': .NET 10.0(Runtime=.NET 10.0)
  HandlerInvocationBenchmarks.'Transient Handler Resolution': .NET 10.0(Runtime=.NET 10.0)
  HandlerInvocationBenchmarks.'Scoped Handler Resolution': .NET 10.0(Runtime=.NET 10.0)
  HandlerInvocationBenchmarks.'Singleton Handler Resolution': .NET 10.0(Runtime=.NET 10.0)
  HandlerInvocationBenchmarks.'Batch Handlers (50 handlers)': .NET 10.0(Runtime=.NET 10.0)
