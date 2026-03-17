# Auto-Optimize Experiment Index

| Commit | Branch | Description | Mean Delta | Alloc Delta | Benchmark | Files Changed |
|--------|--------|-------------|--------|---------|-----------|---------------|
| b7736437b | transport-queue-parity-20260316 | Cache routing decision from Features dict | within noise | -380B (inconsistent) | TransportQueueParity | RoutingDecisionAccessor.cs |
| 841a17d7b | transport-queue-parity-20260316 | CreateSuccessResult MessageContext fast-path | within noise | -7.6KB (inconsistent) | TransportQueueParity | FinalDispatchHandler.cs |
| bc8a73021 | transport-queue-parity-20260316 | Cache bus resolution + policy per bus name | -22% (one run) | within noise | TransportQueueParity | FinalDispatchHandler.cs |
| b049eafc6 | mediatr-query-20260316 | Sync fast-path for ConvertTaskToObjectValueTask | within noise | -168B (theoretical) | MediatR | HandlerInvoker.cs |
| ce5959302 | mediatr-query-20260316 | AggressiveInlining for ConvertTaskToObjectValueTask | within noise | within noise | MediatR | HandlerInvoker.cs |
| 4e52a999e | mediatr-query-20260316 | ThreadStatic cache for TryGetDirectActionDispatchPlan | within noise | within noise | MediatR | LocalMessageBus.cs |
| ae6741ad3 | mediatr-query-20260316 | typeof(TMessage) for sealed types in DispatchAsync | within noise | within noise | MediatR | Dispatcher.cs |
| 71b11be0c | mediatr-query-20260316 | Skip redundant volatile RequestServices write in pool Rent | -11.5% | within noise | MediatR | MessageContextPool.cs |
| 5cdeb5e13 | mediatr-query-20260316 | Remove redundant Message/Result assignments in Reset | -2.4% | within noise | MediatR | MessageContextPool.cs |
| b9c367317 | mediatr-query-20260316 | Skip redundant volatile writes in Reset for unchanged fields | within noise | within noise | MediatR | MessageContext.cs |
| ef6cce541 | mediatr-query-20260316 | Guard Reset field writes to avoid cache-line dirtying | -15.5% | within noise | MediatR | MessageContext.cs |
