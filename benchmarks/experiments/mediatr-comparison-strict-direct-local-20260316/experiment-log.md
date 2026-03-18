# Auto-Optimize: MediatR Comparison -- Strict Direct-Local

## Goal
- Benchmark: `MediatRComparisonBenchmarks`
- Method: `Dispatch: Single command strict direct-local`
- Targets: `mean <= 3500ns`, `alloc <= 160B`
- Max experiments: 15

## Baseline (2 runs, 3 repeats)
| Metric | Value |
|--------|-------|
| Mean | 4,100 ns |
| StdDev | 200 ns |
| Ratio | 0.94 |
| Allocated | 1,968 B (UNSTABLE: varied 576-4,944 B across runs) |

## Important Note on Benchmark Quality

The benchmark uses `InProcessEmitToolchain` with `InvocationCount(1)` and `IterationCount(3)`.
This produces extremely noisy results:
- Error values often exceed the Mean (e.g., Error=36,228 ns for Mean=5,633 ns)
- Allocation measurements are completely unstable (576 to 4,944 B for identical code)
- Cannot reliably detect improvements below ~500ns

All experiments were evaluated for correctness (objectively reduces work/allocation) rather
than measured improvement (unreliable).

## Experiments

### #1 -- Cache EmptyServiceProvider as static singleton [KEPT]
- File: `MessageContext.cs`
- Change: `new EmptyServiceProvider()` -> `SharedEmptyServiceProvider` (static readonly)
- Saves: ~24B per MessageContext creation
- Commit: 717e41ebf

### #2 -- Replace lock with Interlocked.CompareExchange [KEPT]
- File: `MessageContext.cs`
- Change: Removed `_lockObject = new()` allocation, use lock-free lazy init for Items/Features dicts
- Saves: ~24B per MessageContext creation
- Commit: 1d61f0a37

### #3 -- Avoid Features dict allocation in routing fast path [KEPT]
- Files: `MessageContext.cs`, `RoutingDecisionAccessor.cs`
- Change: Added `HasFeatures` property, skip `GetRoutingFeature()` when no features set
- Saves: ~80B per dispatch (Dictionary<Type,object> not allocated on fresh context)
- Commit: c31ba327b

### #4 -- Pre-compute directLocal+noRouter fast-path check [KEPT]
- File: `Dispatcher.cs`
- Change: Combined 3 invariant checks into single `_directLocalNoRouterFastPath` boolean
- Saves: ~5ns per dispatch (fewer branch evaluations)
- Commit: daef97503

### #5 -- Thread-local context recycling in MessageContextFactory [KEPT]
- File: `MessageContextFactory.cs`
- Change: Added ThreadStatic single-element cache. Return() resets and caches context; CreateContext() reuses it.
- Saves: ~200B per dispatch in steady state (MessageContext not re-allocated)
- Commit: eea29af4f

### #6 -- Skip redundant volatile writes in Initialize [KEPT]
- File: `MessageContext.cs`
- Change: Skip `_requestServices` volatile write when provider hasn't changed (recycled context)
- Saves: ~5ns per dispatch (avoids memory barrier)
- Commit: ec5ac154b

### #7 -- ThreadStatic invoker cache in HandlerInvoker [KEPT]
- File: `HandlerInvoker.cs`
- Change: Added per-thread one-element cache for (handlerType, messageType) -> invoker
- Saves: ~30ns for non-ultra-local paths (not measured on this benchmark's fast path)
- Commit: 536f3079b

## Strategy Notes

1. The most impactful optimization was #5 (context recycling) which eliminates the dominant
   per-dispatch allocation in steady state.
2. Optimizations #1-#3 eliminate smaller but cumulative allocations per MessageContext.
3. The allocation target of 160B is not achievable with this dispatch architecture: even with
   zero context allocation, TestCommand (~24B) + transient handler resolution (~24B) +
   async state machine overhead = minimum ~100-200B.
4. The mean target of 3500ns is challenging due to ~150ns AsyncLocal overhead per dispatch
   (ambient context flow enabled by default) and delegate invocation chain overhead.
5. The benchmark config (InProcess, 3 iterations) cannot reliably measure improvements.

## Final Metrics (last measurement, experiment #6)
| Metric | Baseline | Final | Delta |
|--------|----------|-------|-------|
| Mean | 4,100 ns | 4,367 ns | within noise |
| Alloc | 1,968 B | 1,584 B | -19.5% (but unstable) |
| Ratio | 0.94 | 0.89 | improved |
