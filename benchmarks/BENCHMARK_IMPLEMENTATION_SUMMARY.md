# Benchmark implementation summary

> **This document previously described a benchmark suite that does not exist in this repository.**
>
> It named three projects (`benchmarks/Dispatch.Benchmarks/`,
> `benchmarks/MiddlewarePipeline.Benchmarks/`, `benchmarks/Serialization.Benchmarks/`), three runner
> scripts under `eng/`, and five benchmark classes. Checked 18 October 2026: none of the six paths and
> none of the five classes are present in the tree, while a control lookup for a class that does exist
> (`MediatRComparisonBenchmarks`) resolved normally.
>
> It also carried twelve quantitative claims -- "25-40% faster", "90% reduction in heap allocations",
> "10-50x improvement in message throughput" and others -- each marked with a green check. No
> measurement in this repository supports any of them. They were validation *targets* written as
> though they were results.
>
> The content is removed rather than corrected, because there is nothing behind it to correct.

For the benchmark suite that does exist:

- **How to run it:** `benchmarks/Excalibur.Dispatch.Benchmarks/BENCHMARKS.md`
- **Measured results, with hardware, runtime and variance:** `docs/performance/`
- **Preserved baselines by epoch:** `benchmarks/BENCHMARKS.md`
