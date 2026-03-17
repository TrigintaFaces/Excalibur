# Auto-Optimize: DispatchHotPath Gate

**Date**: 2026-03-16
**Branch**: auto-optimize/dispatch-hotpath-20260316-153017
**Goal**: Pass DispatchHotPath performance gate
**Status**: SUCCESS -- Gate already passes on baseline

## Gate Thresholds

| Check | Threshold | Baseline | Status |
|-------|-----------|----------|--------|
| Lookup ratio | <= 0.25 | 0.079 | PASS |
| Invoker ratio | <= 0.60 | 0.522 | PASS |
| Dispatch alloc | <= 512 B | 160 B | PASS |
| Invoker alloc | <= 128 B | 64 B | PASS |
| Middleware growth (0->10) | >= 2.00 | 6.979 | PASS |

## Baseline Metrics (DispatchHotPathBreakdownBenchmarks)

| Method | Mean | StdDev | Allocated |
|--------|------|--------|-----------|
| Dispatcher: Single command | 96.537 ns | 1.561 ns | 160 B |
| Dispatcher: Query with response | 120.553 ns | 1.158 ns | 352 B |
| MiddlewareInvoker: Direct invoke | 88.201 ns | 2.657 ns | 280 B |
| FinalDispatchHandler: Action | 176.470 ns | 2.010 ns | 296 B |
| LocalMessageBus: Send action | 142.889 ns | 2.097 ns | 152 B |
| HandlerActivator: Activate | 51.379 ns | 0.558 ns | 24 B |
| HandlerActivator: Activate (precreated) | 39.420 ns | 0.331 ns | 24 B |
| HandlerInvoker: Invoke | 50.414 ns | 0.663 ns | 64 B |
| HandlerRegistry: Lookup | 7.654 ns | 0.193 ns | 0 B |

## Conclusion

No experiments needed. All 5 sub-checks of the DispatchHotPath gate pass on the current main branch baseline. The dispatch hot path is well within performance thresholds.
