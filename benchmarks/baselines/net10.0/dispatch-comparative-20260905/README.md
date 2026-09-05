# WarmPath epoch 2026-09-05 (post dispatch-correctness work)

Supersedes `dispatch-comparative-20260903-2230/` for every current claim. That epoch is not
wrong -- it measured different code, and it is kept for that reason.

```
BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.9168/25H2)
Intel Core i9-14900K 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.400
  [Host] : .NET 10.0.11 (10.0.11, 10.0.1126.37416), X64 RyuJIT x86-64-v3
Job=warmpath-inproc  Toolchain=InProcessEmitToolchain
```

## What changed in the code between the epochs

`IDirectLocalDispatcher` and both `DispatchLocalAsync` overloads were removed; a context-pooling
leak was fixed (a rented context was never returned, so pooling never engaged); the ambient message
context is now published on the fast-path arms so a nested dispatch inherits causation, correlation,
tenant and user instead of silently starting a fresh root; and two `ConcurrentDictionary` caches that
cost more than the work they replaced were removed.

**The ambient publication is why the single-command figure moved.** It costs one `ExecutionContext`
copy-on-write -- 72 B where the caller has no other `AsyncLocal` live, and more where it does, since
the copy is of the whole async-local value map. That is the price of sibling isolation between
concurrent dispatches, and it is not recoverable: the paired restore is already free at zero extras,
so there is no second write to remove.

## THIS EPOCH IS ASSEMBLED FROM THREE RUNS, AND YOU SHOULD KNOW WHICH

| classes | run | why |
|---|---|---|
| MassTransit, MassTransitMediator, Pipeline, RoutingFirstParity, TransportQueueParity | first | unchanged since |
| Wolverine, WolverineInProcess | second | re-run after a label correction |
| MediatR | third | re-run after a second label correction |

Six benchmark arms were still *named* after the deleted API while measuring the public context-less
2-arg overload. A true number under a false label is worse than a stale one, because nothing about it
looks wrong. The arms were renamed and the affected classes re-run rather than relabelled in place --
a report has to agree with the source that produced it. The renames change names only, not behaviour.

## RUN-TO-RUN VARIANCE, MEASURED HERE RATHER THAN ASSUMED

Same arm, same binary shape, three runs:

```
Dispatch: Single command handler   45.51 / 47.34 / 45.58 ns    spread ~4%
MediatR:  Single command handler   44.88 / 41.35 / 41.32 ns    spread ~8.6%
```

Both inside the RUNBOOK's structural 6-10%. **Do not read a single-run difference smaller than that
as a finding** -- an earlier reading of "parity with MediatR" was taken from the first run alone and
did not survive the other two.

## WHAT THE NUMBERS SAY, STATED AGAINST WHAT CONSUMERS ACTUALLY HAVE

The 30.49 ns / 24 B figure from the 22:30 epoch shipped in no released package: it existed only in a
tag that never published. Measured against the last **published** alpha's code, every tier is faster.

Against MediatR, honestly: **MediatR is about 1.10x faster on a single command** (45.58 against
41.32 ns) and Dispatch allocates **1.58x less** (96 against 152 B). MediatR leads notification
fan-out and allocates 6.4x more to do it. Dispatch leads the concurrency tiers on allocation.

Against Wolverine, Dispatch is roughly 4x faster on a single command with about 6x less allocation.

## ONE ARM NOT TO CITE

MediatR's own query row moved from 39.34 ns in the previous epoch to 47.55-50.30 ns across all three
runs here. That is MediatR's number, not ours, it is consistent across runs rather than a one-off,
and nothing in this work touches it. Until someone explains it, **the query comparison should not be
published** -- an unexplained 21% shift in the baseline makes the ratio meaningless in either
direction.
