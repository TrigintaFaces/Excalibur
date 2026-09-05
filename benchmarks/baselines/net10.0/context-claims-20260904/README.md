# Context-access and handler-resolution measurements, 2026-09-04

Taken to replace three sets of nanosecond figures that had been asserted in the documentation with
no run, date, or benchmark class behind them.

```
BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9168/25H2)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.400
  [Host] : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3
```

## What each file backs

| File | Backs |
|---|---|
| `...MessageContextBenchmarks-report-github.md` | the access-tier tables in `docs-site/docs/architecture/messagecontext-items-usage.md` and `docs-site/docs/performance/messagecontext-best-practices.md` |
| `...HandlerResolutionBenchmarks-report-github.md` | the lookup figures in `docs-site/docs/performance/auto-freeze.md` |
| `...CacheOptimizationBenchmarks-report-github.md` | kept as evidence for what it CANNOT answer -- see below |

## Read the reports with these three caveats

**The `DirectProperty_*` method names are misleading for four of six methods.** `GetUserId()`,
`GetTenantId()`, `GetSource()` and `GetMessageType()` are feature extensions defined in
`MessageContextFeatureExtensions.cs`, not properties on the interface. Only `CorrelationId` and
`MessageId` are direct property reads. Mapping the docs by method name would publish the feature
cost as the property cost.

**The direct-property figure is at the resolution limit.** ~0.19 ns is under one cycle at 3.2 GHz.
It is a field read the JIT can hoist. Quote it as effectively free, not as a precise value.

**`CacheOptimizationBenchmarks` cannot answer the auto-freeze question.** Its warm arm runs under
the default job and its frozen arm under `InvocationCount=1, UnrollFactor=1`, which reports
1,341 ns with a 396 ns error. The two arms are not comparable to each other, so the long-standing
"handler lookup 50 ns to 5 ns, 10x" claim remains unmeasured rather than confirmed or refuted. A
like-for-like arm would settle it.

Also visible in that report and not yet explained: `TypeName: raw reflection` measures 0.90 ns
against `TypeName: cached` at 2.66 ns, and `MessageKind: cached` (4.33 ns) is level with the
uncached `string.Contains` (4.37 ns). The reflection figure is too fast to be real reflection, so
the likelier reading is that the JIT folds that arm away rather than that the caches are useless --
but both rows deserve a look before anyone cites them.

## Two classes here needed an in-process toolchain to run at all

`MessageContextBenchmarks` and `CacheOptimizationBenchmarks` use the default (CsProj) toolchain,
which searches the tree for the benchmark project file, finds a second copy inside a leftover agent
worktree under `.claude/worktrees/`, and refuses to generate. They were re-run with `--inProcess`.
The reports here carry both arms; the `DefaultJob` rows are the failed ones and read `NA`.
