# Auto-Optimize Experiment Log

## Goal
- Benchmark: `MediatRComparisonBenchmarks`
- Method: `Dispatch: Single command handler`
- Target: `ratio <= 0.90` (Dispatch should be ~10% faster than current baseline)
- Max experiments: 10

## Baseline
| Metric | Value |
|--------|-------|
| Mean | 4.867 us |
| StdDev | 1.0263 us |
| Ratio | 1.03 |
| Allocated | 240 B |
| MediatR Mean | 4.900 us |

**Note**: High noise (StdDev ~21% of mean). Previous run showed 4.567us. Target: reduce mean to ~4.1us or below.

## Strategy
Focus on reducing overhead in the default dispatch path. Hot path:
1. `PooledMessageContextFactory.CreateContext()` (pool rent)
2. `Dispatcher.DispatchAsync` -> `GetMessageDispatchInfo` (ThreadStatic cache)
3. Direct-local ultra-local no-response path
4. `TryDispatchUltraLocalNoResponseFast` -> ambient context + handler invocation
5. Context return to pool

## Experiments

| # | Tier | Description | Delta | Decision |
|---|------|-------------|-------|----------|
