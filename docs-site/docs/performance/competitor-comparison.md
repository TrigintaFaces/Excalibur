---
sidebar_position: 4
title: Benchmark Results
description: Measured dispatch overhead for Excalibur.Dispatch, with the conditions each figure was taken under
---

# Benchmark Results

Measured dispatch overhead for Excalibur.Dispatch, with the conditions behind each figure.

:::info Comparative coverage is incomplete

The measurements here establish no general competitive ranking. Specific dated observations
below retain their own verification scope; other comparative fixtures in
`benchmarks/Excalibur.Dispatch.Benchmarks` remain unverified. Comparisons must use equivalent completed semantic work and matched runtime,
machine and job settings, document library versions and configuration, and retain failures,
unavailable results and warnings. Separate local framework overhead from end-to-end broker work.

We disclose our affiliation with this framework and provide methodology and raw results for
reproduction and independent review. **Measure your own workload before choosing a framework.**
:::

## Current command observations, 19 October 2026

Three independent original warm-path processes measured the single-command pair
against the selected current runtime, with the configuration described below.

| Original method | Median process mean | Process-mean range | Allocation |
|---|---:|---:|---:|
| Dispatch single command | ~65 ns | 58.10–65.27 ns | 96 B/op |
| MediatR 12.5.0 single command | ~30 ns | 29.94–30.95 ns | 152 B/op |

Dispatch's between-process spread is about 12.3%; these are indicative observations,
not stable latency. Both handlers discard dummy arithmetic. Dispatch returns an
`IMessageResult`, while MediatR returns `Unit`. Completion controls verified
128 original calls and 28 rejecting checks; they do not prove useful work or
message delivery. Different epochs prevent a causal improvement or regression
claim. No actual NativeAOT timing or general competitive ranking is established.
Full provenance and process details: repository report
`docs/performance/hot-command-comparison-2026-10-19.md`.

## Current query observations, 19 October 2026

Three independent original `MediatRWarmPathComparisonBenchmarks` processes used
the same selected current runtime after the fixture rental-lifecycle repair.
BenchmarkDotNet 0.15.8, original adaptive `warmpath-inproc` job, in-process emit,
.NET 10.0.12 / SDK 10.0.401, x64 RyuJIT, Intel Core i9-14900K and Windows 11
build 26300.9711. Each query uses ID `123` and returns `246`.

| Original query profile | Median process mean | Process-mean range | Allocation |
|---|---:|---:|---:|
| Dispatch typed API, default pipeline | ~136 ns | 134–141 ns | 288 B/op |
| Dispatch direct context-less profile | ~146 ns | 144–147 ns | 288 B/op |
| Dispatch with singleton promotion configured | ~149 ns | 143–153 ns | 288 B/op |
| MediatR 12.5.0 query | ~53 ns | 53–60 ns | 224 B/op |

MediatR has lower observed latency and allocation in this fixture. Dispatch returns
an `IMessageResult<int>` envelope, while MediatR returns an integer. Both peers are
slower than the earlier isolated-marker experiment below. Different source/fixture
epochs and shared timing movement prevent a causal regression or improvement claim.
Allocation stayed unchanged. No stable latency, general ranking or actual NativeAOT
performance is established.

Four separate current-runtime controls verified 256 original calls, sixteen input
probes and 48 rejecting checks. Independent review rehashed 6,754 inputs and checked
all twelve means, deviations, iteration counts and GC allocation ratios. All 218
listed runtime files were identical across the three processes, and nine selected
compiled source documents matched. Rental counts, effective singleton promotion,
complete candidate and transitive loaded-module closure remain unverified. Original
outlier hints and earlier failures are preserved.

Per-process measurements and evidence:
`docs/performance/hot-mediatr-query-comparison-2026-10-19.md` in the repository.

## Earlier measurements, 17 October 2026 {/* #current-measurements-17-october-2026 */}

The following table preserves the October 17 command observations and isolated
query experiment. Its command and query latency values are superseded as the latest measured
epochs by the October 19 observations above; the allocation experiment remains valid
within its original scope.

BenchmarkDotNet 0.15.8, adaptive warm-path job (`warmpath-inproc`), in-process emit toolchain.
.NET 10.0.12, SDK 10.0.401, x64 RyuJIT. Intel Core i9-14900K, Windows 11 build 26300.9711.
Each row is the median of independent processes. Latency is rounded to the precision the
between-process spread supports; allocation was byte-identical in every process.

The command row comes from three separate original-observation processes. Query rows
are medians of two candidate-arm processes in an isolated marker comparison that
replaced an assembly. They do not certify the entire rebuilt checkout or actual
NativeAOT performance.

| Path | Latency | Allocation |
|---|---:|---:|
| Single command | **~60 ns** | **96 B** |
| Typed query, default pipeline | **~64 ns** | **288 B** |
| Typed query, direct context-less profile | ~68 ns | 288 B |
| Typed query, singleton promotion configured | ~69 ns | 288 B |

Both fixtures use no-op handlers, so they measure dispatch and completion overhead rather than
application work. One command process reported a multimodal distribution (`mValue = 2.86`); that
observation is retained rather than discarded, and no stable-latency or percentile claim follows
from three processes.

### Query allocation fix

An isolated, counterbalanced comparison measured the routing-marker change across four processes,
two observations per arm:

| Dispatch query profile | Allocation before | Allocation after |
|---|---:|---:|
| Default pipeline | 312 B/op | **288 B/op** |
| Context-less profile | 312 B/op | **288 B/op** |
| Singleton promotion configured | 312 B/op | **288 B/op** |

A 24 B reduction, 7.7%, in each profile. Latency moved within the run-to-run band, so no speedup is
claimed. The change also passed 2,102 Abstractions tests and a focused Native AOT probe.

### Scope of these measurements

These are warm, in-process, single-operation fixtures with no-op handlers. They measure the cost of
the dispatch machinery. They are **not** cold-start figures, Native AOT figures, sustained
throughput, or percentile SLOs.

Per-process values, iteration counts, retained warnings and source verification are recorded in
`docs/performance/hot-command-comparison-2026-10-17.md` and
`docs/performance/hot-query-comparison-2026-10-17.md` in the repository.

## MassTransit Mediator query observations

Three separate original warm-path processes measured this query pair on 19 October 2026:

| Original query fixture | Process-mean range | Managed allocation |
|---|---:|---:|
| Dispatch, standard registration with per-call context rental and return | 86.6–89.9 ns | 376 B/op |
| MassTransit 8.5.9 Mediator, setup-owned ambient scope | 7.63–10.84 µs | 11,606–11,636 B/op |

Both methods construct the original query and return `246`. MassTransit's per-call request-client
construction remains inside its measured method. This is a separate fixture from the MediatR
query table above. Two MassTransit processes were bimodal; these are dated observations, with no
stable ratio, general ranking or before/after improvement established. Earlier failures remain
recorded. Actual NativeAOT performance is unverified.

Per-process results, exact workload, independent source/semantic checks and retained warnings:
`docs/performance/hot-masstransit-query-comparison-2026-10-19.md` in the repository.

## Allocation by dispatch profile

From the September 5 2026 epoch. These values predate a correction to default correlation and
causation initialization, so read the latencies as indicative of shape rather than as current path
costs. The allocation column has been stable across epochs.

| Profile | Allocation | Latency | When to use |
|---------|-----------|---------|-------------|
| Single command, caller-supplied context | **96 B** | ~46 ns | You already hold an `IMessageContext` and want to pass it through |
| Strict direct-local profile | **96 B** | ~46 ns | Explicit no-middleware profile |
| Context-less 2-arg overload | **96 B** | ~53 ns | `DispatchAsync(message, ct)` -- the framework creates the context |
| Singleton-promoted | **96 B** | ~54 ns | Handlers registered as singletons via promotion |
| Query with response | **192 B** | ~64 ns | Untyped query result path |
| Query via the typed API or the 2-arg overload | **288 B** | ~64-70 ns | Typed query APIs that materialise their own context |

## Reading the October 17 local-overhead table

- **Allocation figures in that table were byte-identical across processes; latency figures are indicative.**
  Those observations varied about 4% between processes, which is several times
  BenchmarkDotNet's reported error -- that error describes spread *within* one process, not
  reproducibility *between* processes. A difference inside that band is not a finding.
- **Those Dispatch local fixture calls omit an extra `async` wrapper.** Wrapper allocation depends on the exact
  call shape, completion path and runtime. These observations do not establish a universal byte
  cost for an async frame or your own `await`.
- **Microbenchmarks measure framework overhead, not production latency.** A database round trip is
  roughly ten thousand times the figures on this page. If dispatch overhead looks like your
  bottleneck, confirm that with a profiler before optimising for it.

The separately dated MassTransit query fixture has its own allocation ranges and distribution
warnings above. Its original MassTransit method is async and retains request-client construction.

:::warning Do not cross-diff across the April 2026 methodology boundary

Baselines from before April 2026 used BenchmarkDotNet 0.15.4, which auto-tuned `InvocationCount` and
produced numerically different, qualitatively equivalent ns-scale figures. Individual Mean values are
not comparable across that boundary. See `benchmarks/RUNBOOK.md` for the methodology shift.
:::

## Benchmark configurations

Two configurations serve different purposes:

- **WarmPath** (`WarmPathBenchmarkConfig`) -- BenchmarkDotNet defaults with auto-calibrated
  `InvocationCount` and `UnrollFactor`. Measures steady-state cost with warm JIT and caches. This is
  the configuration behind the published figures above.
- **ColdPath** (`ComparativeBenchmarkConfig`) -- `InvocationCount=1`, `UnrollFactor=1`,
  `IterationCount=3`. Measures single-invocation cost including framework setup. Used by the CI
  regression gates.

WarmPath measures steady-state performance for these workloads; it does not establish production
latency. ColdPath exercises the separately configured single-invocation path.

## Running the benchmarks

```bash
# Build once
dotnet build benchmarks/Excalibur.Dispatch.Benchmarks/Excalibur.Dispatch.Benchmarks.csproj -c Release --nologo -v minimal

# WarmPath -- the configuration behind the published figures
dotnet run -c Release --project benchmarks/Excalibur.Dispatch.Benchmarks -- --filter *WarmPath* --join --anyCategories WarmPath

# ColdPath / CI gates -- single-invocation
dotnet run -c Release --project benchmarks/Excalibur.Dispatch.Benchmarks -- --filter *ComparisonBenchmarks* --join

# The full matrix
pwsh ./eng/run-benchmark-matrix.ps1 -NoRestore -NoBuild
```

Results are written to `benchmarks/runs/BenchmarkDotNet.Artifacts/results/` unless `-ArtifactsPath`
is provided.

The benchmark project also contains fixtures that exercise other messaging libraries. They are used
during development to sanity-check our own path costs, are not maintained as a published
comparison, and their results are not reported here.
