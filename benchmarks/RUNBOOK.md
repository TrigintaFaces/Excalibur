# Benchmark runbook

Use the [bounded Release subset guide](Excalibur.Dispatch.Benchmarks/BENCHMARKS.md)
for build, discovery and exact-case execution commands. Coordinate repository
reservations and serialized build/test slots before work. Build and verify inputs
before measuring; keep builds, tests, profiling, source edits and other benchmarks
outside each measurement window.

Record exact commands, source revision and dirty-source hashes, the complete runtime
binary bundle, SDK/runtime/package versions, effective jobs and parameters, environment,
process exits and raw reports. Use unique run identifiers and bounded memory, duration
and output. Preserve failed and interrupted runs. A clean process exit, an empty
selection, an NA row or a no-op is not a measured pass.

Inspect process ownership and coordinate with the owning session. Terminate only
processes belonging to the measured job when its bounds require it. Do not use
process-name cleanup, change machine power/security settings, or close unrelated
release verification tools to obtain quieter results.

## Keep measurement regimes separate

`WarmPathBenchmarkConfig` is the managed steady-state configuration used by the
historical published comparisons. Match the exact workload and configuration when
comparing with those observations.

`ComparativeBenchmarkConfig` currently uses `InProcessEmitToolchain`, one invocation
and unroll factor, three warmups and ten measured iterations. It is a single-invocation
in-process diagnostic, not process isolation or proof of cold startup. Actual startup
and NativeAOT execution require their separate instruments and evidence. A fixture
name containing `Aot` does not establish native execution.

Discover the compiled population before selecting a family. Wildcards can include
inherited methods, parameters and multiple jobs. CLI jobs may add to attributed jobs.
The comparative runner includes `RoutingFirstParityBenchmarks`; its former omission
is resolved. Runner names and report-name lists are not a complete census.

## Interpret observations without rewriting them

Preserve each run's timing, allocation and GC values together. Investigate zero,
nonfinite, NA, missing and below-resolution results. Never replace one run's allocation
with another run's median while keeping its timing. Preserve historical normalized
rows with their original provenance and qualification; do not create new composite rows.

Use independent process repetitions and matched controls. Report within-process and
between-process uncertainty separately. There is no universal noise percentage or
allocation floor. A competitor's movement is relevant control evidence, even when
its package version is unchanged. Investigate fixture, runtime, shared-state and
machine differences before attributing a change to framework code.

Predeclare practical comparison limits. A change in who leads is not by itself proof
of a regression or an advantage. Keep inconclusive results inconclusive, and do not
weaken workload, correctness, retention or acceptance thresholds to obtain a pass.

## Historical S812 record

The previous runbook, originally dated 2026-04-20 and amended later, is preserved
verbatim in `artifacts/benchmarks/current-refresh-20261101/historical-RUNBOOK-before-claim-audit.md`.
That local archive contains obsolete operational instructions and unverified causal
explanations; do not execute its commands or treat them as current policy.

Its reported observations are retained here as history, without a current-source claim:

| Historical observation | Reported value |
|---|---|
| Comparative family size at the time | 268 cases; another sweep recorded 266 rows |
| Warm/cold example | 0.074 µs warm versus 9.2 µs single-invocation |
| Reported cold/warm error ranges | 11.9–36.1% / 0.6–2.0% |
| Warm allocation observations | 17 rows byte-identical while means moved ±7%; a later 121-row comparison had 112 unchanged, seven changed and two increases of 7 B and 3 B |
| Cold allocation observations | 145 rows: 40 unchanged and 105 changed; 41 changed competitor-only rows |
| NServiceBus 100-command allocation example | 4,830,800 → 5,104,800 B |
| Notification comparison example | Prior 1.58/1.58 report followed by a reported 20% MediatR lead |

The old policy used ±3% latency as noise, 3–8% as warning and >8% as blocker;
allocation bands were +2%/+5%, with any 0-B-to-positive change or competitor-tier
inversion blocking. These are historical policy thresholds, not measurements or
universal uncertainty bounds. The old 30–90 minute runtime estimate, ~5 µs harness-floor
explanation and package-version attribution were not verified for the current suite.

Historical snapshots remain under `benchmarks/baselines/net10.0/`. Retain their original
epoch, toolchain and workload. The current full refresh is incomplete and does not
replace those snapshots with a new published baseline.

See [comparison evidence and historical records](../docs/performance/competitor-benchmarks.md)
and [benchmark family descriptions](README.md). Do not publish packages, baselines or
documentation as a side effect of local verification.
