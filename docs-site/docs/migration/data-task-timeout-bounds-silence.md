---
title: A data task's timeout bounds silence, not duration
sidebar_label: Data task timeout bounds silence
description: DispatcherTimeoutMilliseconds is removed. ProcessingStallTimeout bounds how long a task may make no progress and is restarted on every checkpointed record; ProcessingTotalTimeout is an opt-in absolute cap. What breaks, what to set, and why a total deadline was the wrong shape.
---

# A data task's timeout bounds silence, not duration

`DataProcessingOptions.DispatcherTimeoutMilliseconds` is **removed**. Two options replace it:

```csharp
services.AddDataProcessing(options =>
{
    // How long a claimed task may make NO PROGRESS. Restarted on every checkpointed record.
    options.ProcessingStallTimeout = TimeSpan.FromSeconds(60);   // the default

    // An absolute cap regardless of progress. Null by default — see "Do you actually want a cap?"
    options.ProcessingTotalTimeout = null;
});
```

## Why the old option was the wrong shape

`DispatcherTimeoutMilliseconds` was a total wall-clock deadline, armed once when a claimed task began and
never reset. It could not distinguish a wedged task from a healthy one with a lot to do, so it cancelled
both — and the one it cancelled wrongly was the one doing its job.

Assuming sequential processing at a fixed positive per-record cost, enough records will exceed a total
processing budget. These illustrative counts omit other processing costs and indicate when cancellation
would be requested, not guaranteed task completion or failure:

| per-record cost | records before a 60s total deadline |
| --- | --- |
| 220 ms | ~270 |
| 20 ms | ~3,000 |
| 2 ms | ~30,000 |

Making each record faster increases the number that fits within the budget. A sufficiently large input
can still exhaust that budget while processing continues to make progress.

The stall clock measures the gap between durable progress updates. It restarts after a successful
checkpoint, including a page-boundary checkpoint for an empty page, so:

- batch size alone does not exhaust the stall budget when durable progress occurs within each interval;
- the entire gap between durable progress updates must fit the interval, including startup and fetching,
  record handling, handler-scope disposal, checkpoint time, and record cleanup; any of these operations,
  or their combined duration, can trigger cancellation;
- after progress, the budget starts again. With the same configured duration, this moves the deadline
  later than an absolute deadline armed at task start; it does not generally cancel sooner.

Checkpoints persist counts per record and cursors at page boundaries. Restart can replay the unfinished
page, including already applied records. Use stable record identities for idempotent effects;
checkpointing does not make external handler effects exactly once.

## What breaks

**Configuration.** A `DispatcherTimeoutMilliseconds` key is **refused at start-up**, with the replacement
named in the message:

```json
{
  "Processing": {
    "DispatcherTimeoutMilliseconds": 300000   // start-up now fails
  }
}
```

```json
{
  "Processing": {
    "ProcessingStallTimeout": "00:01:00"      // TimeSpan format, not milliseconds
  }
}
```

The refusal is deliberate. A renamed configuration key is the one breaking change a binder cannot report —
an unknown key is silently ignored — so a host that had set a large deadline would otherwise inherit the
new default without a word and discover it as a production timeout.

**Code.** Both removed members are a compile error:

| removed | use |
| --- | --- |
| `DataProcessingOptions.DispatcherTimeoutMilliseconds` (`int`, ms) | `ProcessingStallTimeout` (`TimeSpan`) |
| `DataProcessorDefaults.DataProcessorDefaultDispatcherTimeout` (`const int`) | `DataProcessorDefaults.ProcessingStallTimeout` (`static readonly TimeSpan`) |

## What to set

Remove the obsolete key and choose `ProcessingStallTimeout` with operating margin above the longest
expected gap between durable progress updates. Include startup, fetching, record handling, scope disposal,
checkpoint latency, and cleanup when sizing that margin. The default is 60 seconds; verify that it fits
your workload and its expected latency variation.

**If you raised the old option to accommodate large batches**, total record count alone no longer requires
a larger stall budget. That does not establish that the default is sufficient: a slow record, fetch, or
checkpoint can still exceed it.

**If you lowered it to detect stalls quickly**, balance detection latency against the longest legitimate
gap between checkpoints. A smaller budget can cancel healthy work; timeout alone does not prove a deadlock.

Both timeouts request cooperative cancellation. They are not exact completion deadlines: handlers,
database operations, or asynchronous disposal can delay completion after cancellation is requested.
A checkpoint that completes later cannot undo a cancellation already requested.

## Do you actually want a cap?

`ProcessingTotalTimeout` is `null` by default. Set it when your operational policy requires a total
processing budget, including for finite work. It can also detect work that never ends while reporting
progress, such as a cursor that never reaches its end. A task can exhaust this budget while still making
healthy progress; cancellation remains cooperative as described above. If you set it, it must not be below
`ProcessingStallTimeout`. Options validation enforces this relationship so the total cap does not cut
short the configured stall allowance. This is a configuration constraint, not a claim that a short task
could never finish under a smaller total cap.

## Reading the failure

The two clocks are reported separately, because they call for different fixes:

- **"made no progress for `<budget>`"** — no durable progress was observed within the stall budget.
  Investigate the full gap between checkpoints, including fetching, handling, disposal, checkpointing,
  and cleanup. Durable counts and page-boundary cursors are retained; restart can replay the unfinished
  page as described above.
- **"exceeded its total processing budget of `<cap>`"** — the absolute cap fired, and the task may well
  have been progressing when it did. Check whether the limit is intentional and whether processing is
  behaving correctly, including whether the input or cursor unexpectedly prevents completion. Raise or
  clear `ProcessingTotalTimeout` only if that change fits your operational policy.

A cancellation coming from the host shutting down is neither, and is not reported as a timeout.
