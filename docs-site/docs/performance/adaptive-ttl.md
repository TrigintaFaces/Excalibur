---
sidebar_position: 6
title: Adaptive TTL access history
---

`AdaptiveTtlCache` estimates access frequency using minute aggregates rather than
retaining a timestamp for every read. Each tracked key retains at most 60 completed
nonempty minute buckets plus the current minute. History storage therefore stays
bounded as the request rate rises. The queue backing array can retain spare capacity.

Frequency is the retained access count divided by 60, expressed as accesses per
minute averaged over an hour. The current UTC minute and the preceding 60 minutes
are included. Compared with an exact timestamp window, an access can remain for
less than one additional minute. Frequency reads advance the window even when
there have been no new accesses. TTL decisions near a frequency threshold can
therefore differ at minute boundaries.

The window uses the injected `TimeProvider` UTC clock. Its minute high-water mark
never moves backward: delayed samples and backward clock adjustments count in the
current window, and expiration resumes when UTC catches up. Lifetime hit counts
and hit-rate calculations remain independent of window expiration.

Configured retention options remain unchanged; history aggregation does not
directly evict or refresh cache values. It bounds access history per tracked key;
it does not bound the number
of distinct keys retained by the cache or adaptive strategy. Applications still
need an appropriate underlying cache capacity and expiration policy.

## Bounded strategy diagnostics

`RuleBasedTtlOptions.MaxTrackedKeys` limits the keys retained by
`RuleBasedAdaptiveTtlStrategy` for diagnostics. The default is **1,024**. Set a
positive value before constructing the strategy; its capacity is captured once.

```csharp
services.AddDistributedMemoryCache();
services.AddAdaptiveTtlCache(options => options.MaxTrackedKeys = 1024);
```

When full, tracking evicts the oldest admitted key. Reading an existing key does
not change that order. Readmission starts a new diagnostic history for that key,
including its per-key rule logging flags. The strategy's TTL rules continue to
use the supplied context and options; diagnostic eviction does not evict or
refresh cached values or change the TTL returned for identical inputs.

Calculation totals, rule-group totals and boundary-hit totals remain cumulative
for the strategy instance, independent of eviction. The existing rule groups are
rules 1–3 and rules 4–6; their labels do not describe the sign of every multiplier.
Already-applied rules remain counted if later TTL arithmetic fails. Calculation
totals advance after successful rule evaluation, before diagnostic logging.

`AverageResponseTimeMs` and `StaleHitRate` summarize lifetime **accepted** feedback.
Feedback for a key absent from tracking is ignored and does not admit the key.
After readmission, feedback with that key belongs to its current diagnostic
history; the feedback API carries no historical generation identifier.
`AverageHitRate` is the unweighted mean for currently retained keys with feedback,
and `TotalKeys` counts the retained admission cohort. Metrics remain approximate
while concurrent operations update them.

This limit bounds strategy diagnostics. Underlying values and the cache wrapper's
metadata have their own retention policies; a stream of fresh keys can still grow
those populations. Fresh-key writer benchmarks therefore require separate
resource-growth classification rather than a stationary latency claim.

## Verified observations — October 18, 2026

Original `AdaptiveTtlBenchmarks` HostProcess jobs on .NET 10.0.12,
BenchmarkDotNet 0.15.8, Release, with unchanged adaptive iteration settings and
1 KB payloads produced these observations:

These measurements cover the access-history repair **before** the strategy key
capacity change. The bounded-strategy follow-up below contains fresh measurements
for these ten cache-operation cases. This table preserves their earlier code epoch.

| Method | Cache size 100, mean | Cache size 1,000, mean | Allocated per invocation |
|---|---:|---:|---:|
| CacheHit | 228.85 ns | 229.66 ns | 280 B |
| CacheMiss | 208.41 ns | 209.50 ns | 64 B |
| SyncGet | 191.87 ns | 191.93 ns | 64 B |
| ConcurrentReads10 | 2.452 μs | 2.444 μs | 2,480 B |
| MixedOperations | 5.376 μs | 5.439 μs | 6,016 B |

Each row measures a whole method invocation. `ConcurrentReads10` performs ten
reads; `MixedOperations` performs sixteen reads and four writes to fixed keys.
These in-memory operations do not establish actual concurrent overlap or remote
cache throughput. The ten cases retained 175 of 178 actual measurements under the
original outlier policy. These are individual dated observations, not a matched
latency improvement, stability certificate, competitor comparison, or NativeAOT
performance result.

A separate finite allocation diagnostic ran two ABBA blocks: four independent
processes per implementation, both cache sizes, and 100,000 original `CacheHit`
calls after 1,000 warmup calls. Runtime files matched except for the caching DLL
and PDB. The candidate saved at least **41.55 current-thread allocated bytes per
call** in every predeclared within-block comparison. This amortizes the old
timestamp queue's growth over that fixed population; it is not a universal
per-call saving or total-process allocation measurement. No comparison crossed
the predeclared 10% elapsed-time regression trigger, but the short diagnostic does
not establish steady-state latency improvement.

Access-history epoch behavior verification includes 226 passing caching tests and an executed
win-x64 NativeAOT public-path probe with 100,000 fixed-key reads. The native probe
verifies behavior, not the table's timing. Evidence and preserved failed attempts
are under
`artifacts/benchmarks/full-refresh-20261015/hot-current-controls/adaptive-history-r1/`.
The broader benchmark campaign remains incomplete. The bounded-strategy follow-up
below has a separate source epoch and verification scope.

## Bounded-strategy observations — October 19, 2026 UTC

After bounding strategy diagnostics and replacing per-key rule counters with
cumulative atomic logging flags, sixteen original HostProcess cases completed in
two sequential subsets on the same .NET 10.0.12 / BenchmarkDotNet 0.15.8
configuration. Benchmark bodies,
parameters, setup, cleanup and adaptive iteration settings were unchanged.

| Method | Cache size 100, mean | Cache size 1,000, mean | Allocated per invocation |
|---|---:|---:|---:|
| CalculateTtl | 42.60 ns | 41.81 ns | 192 B |
| CalculateTtlHighLoad | 47.96 ns | 48.25 ns | 192 B |
| GetMetrics | 339.02 ns | 344.65 ns | 528 B |
| CacheHit | 140.49 ns | 140.11 ns | 280 B |
| CacheMiss | 126.36 ns | 126.96 ns | 64 B |
| SyncGet | 123.42 ns | 122.65 ns | 64 B |
| ConcurrentReads10 | 1.525 μs | 1.523 μs | 2,480 B |
| MixedOperations | 3.256 μs | 3.233 μs | 6,016 B |

The calculation methods include constructing the original context and reading
UTC time. `GetMetrics` uses the original empty strategy: the underlying cache is
seeded, but the strategy has no calculated keys or feedback. Its results do not
measure scanning a populated diagnostic cohort. The six calculation/metrics cases retained 104 of 106
actual measurements under the original outlier policy; both removed measurements
belong to high-load calculation at cache size 100. The ten cache-operation cases
retained 160 of 164 actual measurements. Concurrent and mixed rows remain whole
method costs: ten reads and sixteen reads plus four fixed-key writes respectively.
They do not establish actual concurrent overlap or remote-cache throughput.
These are single-capture managed observations, not matched latency gains,
competitor comparisons or NativeAOT timing. The lower values relative to the
earlier table do not establish a causal framework speedup.

Separate matched finite diagnostics used four fresh processes per arm in two ABBA
blocks, with identical runtime assets except the caching provider DLL/PDB. The
baseline predates the strategy capacity change; the candidate includes bounded
tracking and atomic flags. Calculation allocation stayed at **192 current-thread
bytes per call**. Empty metrics retrieval used **528 versus 592 bytes per call** in
both protocols. This allocation comparison concerns the empty original metrics
workload, not populated-cohort memory or total process allocation.

Calculation elapsed time remains inconclusive. The short protocol measured one
million calls after one million warmup calls; two of sixteen predeclared calculation
comparisons exceeded the 10% investigation threshold. A separately declared longer
protocol measured ten million calls after the same warmup; four comparisons exceeded
that threshold, including one candidate process that slowed at both cache sizes.
All results, including the earlier pre-optimization regression observations, remain
preserved. Longer measurements did not establish stable regression resolution or a
calculation speedup. The bounded tracking contract is supported by behavior tests;
it is not justified by claiming an unproven latency improvement.

The original BenchmarkDotNet job policy was retained. Actual core placement and
migration were not traced. BenchmarkDotNet 0.15.8 resolves unspecified affinity
from its current process ([pinned resolver source](https://github.com/dotnet/BenchmarkDotNet/blob/v0.15.8/src/BenchmarkDotNet/Environments/EnvironmentResolver.cs#L19),
[current-affinity implementation](https://github.com/dotnet/BenchmarkDotNet/blob/v0.15.8/src/BenchmarkDotNet/Portability/RuntimeInformation.cs#L256)).
Its maintainers discuss hybrid-core scheduling as a possible source of multimodal
timings and the effect of changing defaults on historical comparisons
([discussion](https://github.com/dotnet/BenchmarkDotNet/issues/3266)). That is one
possible contributor here, not an established explanation for the observed variance.

The current implementation passes **241 caching tests**, including cumulative
logging, concurrent rule application, partial arithmetic failure and diagnostic
eviction/readmission. An executed win-x64 NativeAOT consumer verifies 10,000 unique
strategy contexts and 10,000 public cache set/get/remove cycles, including real DI
and options validation. This verifies the exercised behavior, not native timing or
whole-framework AOT compatibility.

All four unchanged fresh-GUID writer cases were also exercised for 4,096 calls each.
Strategy tracking stayed at its 1,024-key limit; all 4,096 written values and the
original seeded values remained available. The 4 MiB sum of written payload lengths
per case is logical payload volume, not a physical heap measurement. The underlying
default memory cache has no explicit size limit, and the writer still requests
five-minute value retention. Those writers therefore retain a nonstationary
resource-growth disposition: finite behavior passed, but original adaptive
steady-state benchmark timing is unavailable under the campaign's resource bounds.
Workloads and production retention were not changed to obtain a measured pass.

Evidence and preserved failed attempts are under
`artifacts/benchmarks/full-refresh-20261015/hot-current-controls/adaptive-key-tracking-r1/`.
The full benchmark campaign and release validation remain incomplete.
