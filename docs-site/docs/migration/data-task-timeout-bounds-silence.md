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

The consequence is arithmetic rather than opinion. Under a total deadline there is always a record count
above which the task fails, no matter how fast each record is:

| per-record cost | records before a 60s total deadline |
| --- | --- |
| 220 ms | ~270 |
| 20 ms | ~3,000 |
| 2 ms | ~30,000 |

Making the work faster moves the wall. It never removes it. A batch pipeline whose input size the
framework does not control cannot be bounded by total duration without failing on size alone.

The stall clock measures the thing that actually distinguishes the two cases. It is restarted every time
the task durably accounts for a record, so:

- a task that keeps completing records is **never** cancelled by it, whatever the batch size;
- a task that goes quiet is cancelled **sooner** than the old deadline usually managed, because the budget
  starts from the last sign of life rather than from the beginning of the task.

Work already checkpointed is kept, and the task resumes from it on the next attempt.

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

**If you left the old option at its default**, do nothing. The stall budget defaults to the same 60
seconds, and it is now 60 seconds of silence rather than 60 seconds of work.

**If you raised the old option to accommodate large batches** — the common case — you no longer need to.
Delete it and take the default. The reason you raised it was that the task had a lot to do, and that is
exactly what no longer causes a failure.

**If you lowered it to fail fast on a wedged task**, set `ProcessingStallTimeout` to roughly that value. It
will now fire only on a genuine stall, so a smaller value is safe where it previously was not.

## Do you actually want a cap?

`ProcessingTotalTimeout` is `null` by default, and leaving it there is the right answer for almost every
host. Setting it re-introduces, deliberately, the failure mode this change removed: a task with more work
than the cap fails regardless of how healthy it is.

It exists for the one case the stall clock cannot catch — work that genuinely never ends while reporting
progress throughout, such as a cursor that never reaches its end. If you set it, it must not be below
`ProcessingStallTimeout`; a cap under the stall budget can never be satisfied, and that composition is
rejected by options validation rather than failing every task at runtime.

## Reading the failure

The two clocks are reported separately, because they call for different fixes:

- **"made no progress for `<budget>`"** — the task stalled. Work already checkpointed is kept and the task
  resumes from it. Look at what the record handler was waiting on, not at how much work the task had.
- **"exceeded its total processing budget of `<cap>`"** — the absolute cap fired, and the task may well
  have been progressing when it did. Raise or clear `ProcessingTotalTimeout`.

A cancellation coming from the host shutting down is neither, and is not reported as a timeout.
