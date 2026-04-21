# Benchmark Runbook

Operational guide for running the comparative benchmark suite. Captures lessons learned from S812 refresh. Read this before starting any benchmark session.

> For **what** each benchmark class measures, see `benchmarks/README.md`.
> For **reporting conventions** (baseline paths, allocation consistency), see `docs/performance/competitor-benchmarks.md`.

---

## TL;DR — the canonical run

```bash
# 1. Kill any orphaned dotnet hosts (they skew measurements)
taskkill /F /IM dotnet.exe 2>&1 || true   # Windows
pkill -9 dotnet 2>&1 || true               # Linux/macOS

# 2. Build (Release, no diagnostics, no examples)
dotnet build benchmarks/Excalibur.Dispatch.Benchmarks/Excalibur.Dispatch.Benchmarks.csproj -c Release --nologo

# 3. Run BOTH configs in one pass (detached so shell reaper can't kill child)
nohup dotnet run -c Release --project benchmarks/Excalibur.Dispatch.Benchmarks -- \
  --filter "*Comparative.*ComparisonBenchmarks*|*Comparative.*ParityBenchmarks" \
  --artifacts benchmarks/runs/$(date +%Y%m%d)-artifacts \
  > benchmarks/runs/$(date +%Y%m%d)-run.log 2>&1 &
disown

# 4. Post-run cleanup
taskkill /F /IM dotnet.exe 2>&1 || true

# 5. Snapshot into baseline
mkdir -p benchmarks/baselines/net10.0/dispatch-comparative-$(date +%Y%m%d)/results
cp benchmarks/runs/$(date +%Y%m%d)-artifacts/results/*.{md,csv,html} \
   benchmarks/baselines/net10.0/dispatch-comparative-$(date +%Y%m%d)/results/
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
