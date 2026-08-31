# Benchmark Runbook

Operational guide for running the comparative benchmark suite. Captures lessons learned from S812 refresh. Read this before starting any benchmark session.

> For **what** each benchmark class measures, see `benchmarks/README.md`.
> For **reporting conventions** (baseline paths, allocation consistency), see `docs/performance/competitor-benchmarks.md`.

---

## TL;DR — the canonical run

```bash
# 0. VERIFY THE FILTER MATCHES BEFORE RUNNING ANYTHING.
# A filter that matches nothing exits in seconds and prints the available-benchmark
# list plus usage help -- which reads like output. This runbook previously carried a
# pipe-joined filter that returned 0 benchmarks; the run "completed" with an empty
# artifacts directory and nothing said so.
./.dotnet/dotnet.exe run -c Release --project benchmarks/Excalibur.Dispatch.Benchmarks --no-build --   --list flat --filter "*Comparative*" | grep -c Comparative     # expect a non-zero count

# 1. Reap orphaned hosts (they skew measurements). Parent-gone only -- do NOT blanket-kill
#    dotnet.exe, which also kills whatever else is running on this box.
pwsh -NoProfile -File eng/ci/Reap-OrphanTestHosts.ps1

# 2. Build (Release). Use the REPO-LOCAL SDK: global.json pins a version that a bare
#    `dotnet` on PATH does not have, and bare `dotnet` fails with "A compatible .NET SDK
#    was not found" before any benchmark runs.
./.dotnet/dotnet.exe build benchmarks/Excalibur.Dispatch.Benchmarks/Excalibur.Dispatch.Benchmarks.csproj -c Release --nologo

# 3. Run detached. ONE --filter: BenchmarkDotNet does not treat '|' as alternation, and
#    repeating --filter did not match either. "*Comparative*" selects the whole
#    comparative suite (268 benchmarks at time of writing).
nohup ./.dotnet/dotnet.exe run -c Release --project benchmarks/Excalibur.Dispatch.Benchmarks --   --filter "*Comparative*"   --artifacts benchmarks/runs/$(date +%Y%m%d)-artifacts   > benchmarks/runs/$(date +%Y%m%d)-run.log 2>&1 &
disown

# 4. CONFIRM IT IS ACTUALLY RUNNING -- the launcher's exit code is nohup's, not the run's.
head -3 benchmarks/runs/$(date +%Y%m%d)-run.log     # "returned 0 benchmarks" = it did NOT start

# 5. READ THE RIGHT REPORT. "*Comparative*" matches the NAMESPACE, so this one run
#    produces BOTH configs, and their means differ by ~1000x for the same operation
#    (9.2 us cold vs 0.074 us warm, measured). One report file per CLASS:
#      *.MediatRComparisonBenchmarks-report.csv          <- COLD. CI regression gate.
#      *.MediatRWarmPathComparisonBenchmarks-report.csv  <- WARM. Publish from THIS one.
#    The warm subclass INHERITS every method from the cold class, so the two files share
#    identical method names. Extracting by benchmark name across files silently blends
#    them. Always select the file first, or key on the 'Job' column
#    (comparative-inproc vs warmpath-inproc).

# 6. Snapshot into a DATED baseline (never overwrite a prior one -- the series is what
#    makes a later regression visible).
mkdir -p benchmarks/baselines/net10.0/dispatch-comparative-$(date +%Y%m%d)/results
cp benchmarks/runs/$(date +%Y%m%d)-artifacts/results/*.{md,csv,html}    benchmarks/baselines/net10.0/dispatch-comparative-$(date +%Y%m%d)/results/

# NOTE: local numbers update DOCUMENTATION and baselines. They must NOT tighten the
# thresholds in tests/performance/** -- those are deliberately loose because CI runs on
# shared GitHub runners alongside other jobs and will never reach these numbers.
```

Expected runtime on i9-14900K: **30-90 min** depending on class count and auto-tune warmup depth.

---

## Two configs, two purposes — run BOTH

The suite contains classes built on two different BDN configs. **Both must run** if you're updating consumer-facing docs.

| Config | Scale | Used for | Classes |
|---|---|---|---|
| `ComparativeBenchmarkConfig` | μs wall-time (`InvocationCount=1`, literal) | **Regression gate** — detects real framework slowdowns | `*ComparisonBenchmarks`, `*ParityBenchmarks` |
| `WarmPathBenchmarkConfig` | ns per-call (auto-tuned InvocationCount, amortized) | **Published throughput numbers** — what `docs/benchmarks/results/current/performance-report.md` and `docs/performance/framework-performance-review-spec-sheet.md` cite | `*WarmPath*` variants |

**If you skip WarmPath**, the DOCS phase of your sprint can only refresh `competitor-benchmarks.md` — the headline ns-scale numbers in `performance-report.md` will be stale. S812 IMPLEMENT had to be amended mid-sprint because WarmPath was scoped out.

> ### ⚠️ WIDE ERROR BARS ON THE COLD CONFIG ARE THE DESIGN, NOT A DEFECT — DO NOT CHASE THEM
>
> `ComparativeBenchmarkConfig` pins `InvocationCount=1, UnrollFactor=1`, so **each iteration
> measures ONE call**. At ~50-150 ns of real work per call, a single Gen0 collection or one
> scheduler slice lands inside a measurement window and dominates it. Measured on the same
> host, same commit, same benchmarks:
>
> ```
> COLD (ComparativeBenchmarkConfig)   error 11.9% - 36.1%   <- normal. Not noise to fix.
> WARM (WarmPathBenchmarkConfig)      error  0.6% -  2.0%   <- publication quality
> ```
>
> **The cold config is a regression gate: it answers "did this get materially slower",
> not "what is the number".** Trying to tighten it by re-running on an idle host, reaping
> harder, or running benchmarks solo does not work and cannot work — the variance is
> structural.
>
> **Allocation is the exception — but ONLY on the warm config.** On `WarmPathBenchmarkConfig`,
> `Allocated` is effectively deterministic: measured across a loaded host and an idle host it was
> **byte-identical on all 17 rows** while means moved ±7%, and across a code change it moved on
> exactly the rows the change touched. That makes it the one property in this suite strong enough
> to gate on.
>
> **On the cold config it is NOT deterministic, and an earlier revision of this file said it was.**
> With `InvocationCount=1` each iteration is a single call, so one-time costs — first-call JIT,
> lazy init, cache population, connection setup — land inside the measurement window
> unpredictably. Measured over a full 266-row comparative sweep, same commit pair:
>
> ```
> WARM  121 rows   112 unchanged    7 changed    2 moved by +7 B and +3 B
> COLD  145 rows    40 unchanged   105 changed   swings into the hundreds of KB
> ```
>
> **The proof that this is the instrument and not the code: 41 of the changed rows are
> COMPETITOR-ONLY benchmarks** — NServiceBus, Wolverine, MassTransit — measuring third-party code
> paths that no change of ours can reach. `NServiceBus: 100 concurrent commands` moved 4,830,800 →
> 5,104,800 B between two runs of the same unmodified library.
>
> **So: gate on warm-config allocation. Never read a cold-config allocation delta as a finding
> without a warm-config confirmation.**
>
> *Cost of not knowing this: a full session spent diagnosing "query benchmark instability"
> that was the cold config behaving exactly as documented, while the publishable warm
> numbers sat in the same artifacts directory the whole time.*

**Canonical filter that catches everything** (both configs, all 15+ classes):

```
--filter "*Comparative.*"
```

---

## Detached run — USE `disown`

BenchmarkDotNet spawns many short-lived child processes (one per iteration for isolation). If the shell that launched the run has job control active and the shell process is reaped (which happens when Claude agents complete their session), **the dotnet children go with it — the BDN suite dies mid-run, writing partial results**.

### The fix

```bash
nohup dotnet run ... > logfile 2>&1 &
disown
```

- `nohup` — ignore SIGHUP
- `& disown` — remove from shell's job table so parent reaper skips the dotnet child

### How to tell if you have a detached run

- `tasklist | grep dotnet` shows the dotnet processes
- Your shell's job table is empty (`jobs` returns nothing)
- Writing to the log file continues even after the launching shell exits

### Companion: a "waiter" process

Launch a lightweight sidecar that polls for the run's exit, so you get a signal when it's done:

```bash
waitpid() {
  while kill -0 "$1" 2>/dev/null; do sleep 30; done
  echo "DONE"
}
waitpid $RUN_PID > waiter.out &
disown
```

Or use the pattern FORGE used in S812 — a background bash task with an `until` poll that writes `DONE` + a summary line when finished.

---

## Pre- AND post-run dotnet cleanup

**Mandatory** to reduce measurement noise and avoid file-lock conflicts on build outputs:

```bash
# Before:
taskkill /F /IM dotnet.exe 2>&1 || true
tasklist | grep -iE "^dotnet" | wc -l   # MUST be 0

# Run the suite...

# After:
taskkill /F /IM dotnet.exe 2>&1 || true
tasklist | grep -iE "^dotnet" | wc -l   # MUST be 0
```

**Why:** VS Code/Rider/dotnet-watch/MSBuild-server leaves idle dotnet hosts that consume CPU cycles, hold port reservations, or keep DLLs locked. Any one of these can:
- Skew a p99 latency measurement by 5-15%
- Cause a cold build to fail with `locked by another process`
- Pollute the Windows ETW scheduler under WarmPath's parallel pressure

---

## Baseline snapshot conventions

```
benchmarks/baselines/net10.0/
├── dispatch-comparative-YYYYMMDD/       ← one snapshot per run date
│   └── results/
│       ├── *.ComparisonBenchmarks-report-github.md       (ComparativeConfig)
│       ├── *.WarmPathComparisonBenchmarks-report-github.md (WarmPathConfig)
│       ├── *-report.csv                  (for programmatic diff)
│       └── *-report.html                 (for humans)
```

- **Never delete old baselines.** Preserve as superseded for historical diff.
- **Baseline folder name** uses the date the benchmarks ran, NOT the date of the src commit. The commit SHA and BDN version live in the report headers.
- **Don't mix configs across snapshot dirs.** Put `Comparative` + `WarmPath` reports side-by-side under the same dated snapshot — they describe the same framework build under two measurement regimes.

---

## When the baseline diff looks like a regression but isn't

### BDN version bumps can shift numbers by orders of magnitude

**S812 hit this** — upgraded BDN 0.15.4 → 0.15.8. Same config, same hardware, same commit — but numbers shifted ~100×. Root cause: BDN 0.15.4 auto-tuned `InvocationCount` even with `WarmPathBenchmarkConfig` declared; 0.15.8 honors the literal config. Result: old baseline = amortized ns, new baseline = per-iteration μs.

**Rule:** if BDN version changed since the prior baseline, **treat the new baseline as a new epoch**, not a refresh. Do not cross-diff — just publish the new numbers with a clear note explaining the version shift.

### Harness floor dominates at μs scale

Under literal `InvocationCount=1`, measurements below ~5 μs are dominated by the measurement harness itself, not the code under test. That's why some "fast" rows show `0 ns` allocated — the mean iteration cost is so low the rounding nukes the alloc column.

**When this happens:** apply the Allocation Consistency Rule (normalize against `*-report.run1.csv` where per-iteration overhead is visible) — see `docs/performance/competitor-benchmarks.md` §Allocation Consistency.

### Ratio-inversion is the hardest blocker

A Dispatch row that *loses its lead* over MediatR/Wolverine/MassTransit is a ship-blocker even if absolute numbers look fine. The narrative in `performance-report.md` claims Dispatch wins in-process tiers — if that flips on any tier, stop and investigate before publishing.

In S812, one tier flipped this way: `MediatR Notification (3 handlers)` went from tied (1.58/1.58) to MediatR-leads-20%. That WARN is tracked as a follow-up for dedicated perf-recovery investigation.

---

## Regression thresholds (from COMPASS S812 msg 2297)

Diff each Dispatch row in the new baseline against the prior baseline (same config, same BDN version):

| Metric | Noise floor | WARN | BLOCKER |
|---|---|---|---|
| Mean latency | ≤ ±3% | 3-8% regression | **>8% regression** |
| Allocated bytes | ≤ +2% | +2-5% growth | **>5% growth, or 0-B → N-B appearance** |
| Ratio vs competitor | still leading same tiers | lost lead by ≤5% | **any tier inversion** |
| Competitor row drift | not our problem | — | — |

BLOCKERs should escalate to REVIEW_ARCH (ORACLE) for adjudication: is this a real regression that needs fixing in-sprint, or an explainable harness/dep shift?

---

## Canonical run script gaps (as of S812)

`eng/run-comparative-benchmarks.ps1` currently hard-codes 7 of the 8 comparative classes in its filter + expected-reports arrays — it's missing `RoutingFirstParityBenchmarks`. Tracked for fix in a future sprint. Until then, invoke `dotnet run` directly with the `--filter "*Comparative.*"` pattern from the TL;DR.

---

## Checklist for a clean run (every time)

- [ ] No outstanding uncommitted changes (or you'll diff against mixed state)
- [ ] `git log --oneline -1` noted in the sprint review for traceability
- [ ] BDN version noted (grep `BenchmarkDotNet v` in any `.log` after build)
- [ ] `.NET SDK` + `.NET Runtime` noted (from the first run's report header)
- [ ] Other IDEs / dotnet-watch / VS Test Explorer / Rider closed
- [ ] Laptop plugged in (not on battery — CPU throttles affect measurements)
- [ ] Pre-run `taskkill` → 0 dotnet
- [ ] Detached via `nohup + disown`
- [ ] Log file actively growing (`tail -f <logfile>` briefly to confirm)
- [ ] Post-run `taskkill` → 0 dotnet
- [ ] Baseline snapshot dir created with `YYYYMMDD` naming
- [ ] Both ComparativeConfig AND WarmPathConfig reports captured (if you want to update headline docs)

---

## Related docs

- `benchmarks/README.md` — what each benchmark class measures
- `docs/performance/competitor-benchmarks.md` — reporting conventions + allocation consistency rule
- `docs/benchmarks/results/current/performance-report.md` — the canonical published perf numbers (WarmPath-derived)
- `docs/performance/framework-performance-review-spec-sheet.md` — headline spec sheet
- `benchmarks/Excalibur.Dispatch.Benchmarks/Comparative/ComparativeBenchmarkConfig.cs` — config source
- `benchmarks/Excalibur.Dispatch.Benchmarks/Comparative/WarmPathBenchmarkConfig.cs` — config source
- `eng/run-comparative-benchmarks.ps1` — canonical runner (note: see gap above)

## Change log

| Date | What | Why |
|---|---|---|
| 2026-04-20 | Initial — captured S812 operational lessons | `nohup+disown` fix for run-1 shell-reap kill; WarmPath inclusion gap; BDN version-shift methodology divergence |
