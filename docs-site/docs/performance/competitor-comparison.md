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

## Reading these results with newer component measurements

The tables below retain their original source and runtime epochs. They do not describe
performance after subsequent framework changes. The [November 4 pooled metadata query
measurement](./index.md#november-4-component-evidence) uses a different, untyped managed
WarmPath fixture and supports a scoped before/after finding only. It cannot replace these
typed-query rows or establish a current competitive ranking. The linked report records
commands, source hashes, uncertainty, raw evidence and separate NativeAOT correctness checks.

## Historical command observations, 19 October 2026 {/* #current-command-observations-19-october-2026 */}

Three independent original warm-path processes measured the single-command pair
against the runtime selected for that epoch, with the configuration described below.

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

## Historical query observations, 19 October 2026 {/* #current-query-observations-19-october-2026 */}

Three independent original `MediatRWarmPathComparisonBenchmarks` processes used
the same runtime selected for that epoch after the fixture rental-lifecycle repair.
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

Four separate controls for that October 19 runtime verified 256 original calls, sixteen input
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
query experiment. Its command and query latency values precede the later October
epochs documented above; the allocation experiment remains valid
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

The fixtures use trivial handlers: command completion and query arithmetic. They measure dispatch and completion overhead rather than
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

These are warm, in-process, single-operation fixtures with trivial command completion and query arithmetic. They measure the cost of
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
costs. The allocation values also belong to that historical workload and configuration.

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
- **Microbenchmarks measure framework overhead, not production latency.** Profile actual database,
  handler and dispatch work in your application before choosing what to optimize. These fixtures
  do not establish their relative costs in production.

The separately dated MassTransit query fixture has its own allocation ranges and distribution
warnings above. Its original MassTransit method is async and retains request-client construction.

:::warning Do not cross-diff across the April 2026 methodology boundary

Historical records on opposite sides of this boundary used different tooling and
reported job behavior. They do not establish a matched comparison of the current
source. Preserve their epochs and avoid direct differences; the tool version alone
does not prove the cause of the timing shift. Ratios within a report still require
equivalent completed work and an uncertainty assessment. See `benchmarks/RUNBOOK.md`.

:::

## Benchmark configurations

Two configurations serve different purposes:

- **WarmPath** (`WarmPathBenchmarkConfig`) -- BenchmarkDotNet defaults with auto-calibrated
  `InvocationCount` and `UnrollFactor`. Measures steady-state cost with warm JIT and caches. This is
  the configuration behind the published figures above.
- **Single-invocation diagnostics** (`ComparativeBenchmarkConfig`) -- `InvocationCount=1`,
  `UnrollFactor=1`, `IterationCount=10`, `WarmupCount=3`, using `InProcessEmitToolchain`.
  Used by CI regression gates; this configuration does not establish process isolation or cold startup.

WarmPath uses warmed managed execution for these fixtures; the profile name alone does not establish
stationarity, reproducibility or production latency. The separately configured single-invocation path is also managed execution.

## Running the benchmarks

```powershell
# Build before measuring, from the directory containing the scoped solution.
Push-Location benchmarks/Excalibur.Dispatch.Benchmarks
try {
    dotnet build -c Release -m:1 -p:BuildExamplesAndTests=true -p:UseSharedCompilation=false -nodeReuse:false
    if ($LASTEXITCODE -ne 0) { throw "Benchmark build failed." }
    dotnet bin/Release/net10.0/Excalibur.Dispatch.Benchmarks.dll --list flat
    if ($LASTEXITCODE -ne 0) { throw "Benchmark discovery failed." }
    $runId = [Guid]::NewGuid().ToString("N")
    $artifacts = "BenchmarkDotNet.Artifacts/notification-$runId"

    # One original managed WarmPath case; repeat in independent processes.
    dotnet bin/Release/net10.0/Excalibur.Dispatch.Benchmarks.dll --filter "Excalibur.Dispatch.Benchmarks.Comparative.MediatRWarmPathComparisonBenchmarks.Dispatch_NotificationMultipleHandlers" --exporters fulljson --artifacts $artifacts
    if ($LASTEXITCODE -ne 0) { throw "Benchmark process failed; preserve its artifacts." }
}
finally {
    Pop-Location
}
```

Each invocation writes to a distinct `BenchmarkDotNet.Artifacts/notification-<runId>` directory.
A successful process exit still requires exact-case and raw-result reconciliation.
Discover and run other exact names in small sequential subsets. The former
`--filter "*WarmPath*" --join --anyCategories WarmPath` command selects zero current
cases: those fixtures do not declare that category. Removing the category restriction
selects the existing WarmPath family without changing its jobs. Likewise,
`*ComparisonBenchmarks*` matches both warm and cold classes; it is not a cold-only filter.
Historical commands and results do not automatically describe the current source epoch.

The repository matrix runner uses `benchmarks/runs/BenchmarkDotNet.Artifacts` by
default and accepts `-ArtifactsPath`. Its `CiSmoke` switch selects classes without
shortening their original jobs. Reconcile exact completed cases before treating any
runner output as evidence.

Additional comparative fixtures remain unpublished or unverified; the dated observations above
retain their stated scope.
