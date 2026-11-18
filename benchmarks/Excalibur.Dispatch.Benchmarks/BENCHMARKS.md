# Running Dispatch benchmarks

Benchmark inclusion is not a measured pass. Record the exact expanded methods,
parameters and jobs, then reconcile completed measurements with that selection.
Failures, zero-match invocations, no-op cases and unavailable infrastructure remain
separate outcomes. Published observations in `docs/performance/` retain their own
source epoch, runtime and verification scope.

## Build and select a bounded subset

Run from the benchmark project directory so BenchmarkDotNet selects its local
solution rather than unrelated projects with duplicate names elsewhere in the repository.
Build before measurement, under the repository's serialized build/test reservations.
The switcher can prompt for selection when no filters are supplied. A census of
available default-config cases is not evidence that an unfiltered invocation ran
the entire suite. CLI jobs can also add to attribute-defined jobs: inspect the
expanded selection instead of assuming `--job short` shortens every selected job.

```powershell
Push-Location benchmarks/Excalibur.Dispatch.Benchmarks
try {
    dotnet build -c Release -m:1 -p:BuildExamplesAndTests=true -p:UseSharedCompilation=false -nodeReuse:false
    if ($LASTEXITCODE -ne 0) { throw "Benchmark build failed." }
    dotnet bin/Release/net10.0/Excalibur.Dispatch.Benchmarks.dll --list flat
    if ($LASTEXITCODE -ne 0) { throw "Benchmark discovery failed." }
    $runId = [Guid]::NewGuid().ToString("N")
    $artifacts = "BenchmarkDotNet.Artifacts/notification-$runId"
    dotnet bin/Release/net10.0/Excalibur.Dispatch.Benchmarks.dll --filter "Excalibur.Dispatch.Benchmarks.Comparative.MediatRWarmPathComparisonBenchmarks.Dispatch_NotificationMultipleHandlers" --exporters fulljson --artifacts $artifacts
    if ($LASTEXITCODE -ne 0) { throw "Benchmark process failed; preserve its artifacts." }
}
finally {
    Pop-Location
}
```

The example measures the original managed WarmPath fixture with three empty
notification handlers. It does not measure actual NativeAOT, cold start, broker
throughput or application work. Validate handler effects separately before drawing
semantic comparisons.

Use compiled discovery to choose other exact names. A class filter can expand to
many parameter/job cases; split it into small sequential selections without
changing the workload. The former positional aliases (`envelope`, `memory`,
`validation`, `all`, and similar names) and `benchmarks/Dispatch.Benchmarks` path
are obsolete instructions, not supported selections for this entrypoint.

## Evidence and interpretation

Method names and descriptions such as `NoAllocation`, `no alloc`, and `fast path`
identify scenarios or historical hypotheses; they are not measured guarantees.
For example, `MessageContextLazyInitBenchmarks` includes context construction and
simulated property access, not calls through `IDispatcher`. Its absent-key and
unused-result cases need separate interpretation from application dispatch.
`MiddlewareChainBuilderBenchmarks` also includes direct-handler and empty-chain
controls; frozen execution includes lookup while unfrozen execution uses an
already selected executor, so their difference does not isolate freezing alone.

Preserve commands, source revision and dirty-input hashes, binary hashes, SDK,
runtime, package versions, configuration, environment, exits and raw full JSON.
Freeze inputs and avoid concurrent builds, tests, profiling, source edits and other
benchmarks. Use bounded process-tree execution and logs; verify bounded memory
before running fixtures with retention or background work. Do not change machine
power/security settings to obtain a number.

Report allocations, GC counts, retained and excluded iterations, distribution
warnings and independent-process variation. BenchmarkDotNet's error describes
within-process uncertainty; it does not establish between-process reproducibility.
An exit code of zero or an exported report alone does not establish completion.

Keep managed hot-path, cold-path and actual native execution separate. A class
whose name contains `Aot` can still execute under the managed JIT. Compare peers
only with equivalent completed work and disclosed versions/settings. A small timing
difference inside observed variation is inconclusive.

The repository matrix runners select fixture families and record their own
configuration. `CiSmoke` is a selection subset, not a promise of `Job.Dry` duration.
CLI job selections can add jobs alongside attribute-defined jobs; discover the
resulting population before estimating coverage or runtime.

## Historical targets

These retained targets are aspirations, not measurements or universal acceptance
thresholds. In particular, current dispatch semantics can require allocations.

| Metric | Historical target |
|---|---:|
| Hot-path allocations | 0 B |
| Message throughput | > 1M messages/s |
| Routing latency P99 | < 1 ms |
| Memory-pool hit rate | > 95% |
| Cross-provider round trip | < 10 ms |

Microbenchmark means do not establish production P99 or sustainable throughput.
Use the actual workload and a predeclared comparison protocol for regression
claims; preserve historical targets without presenting them as achieved results.
