# WarmPath epoch, 2026-09-05 (post-Lean-deletion)

Three runs of `MediatRWarmPathComparisonBenchmarks`, one after another on a quiet
machine, per the three-run protocol. Built with `-p:BuildExamplesAndTests=true`
and confirmed to emit no "Skipping compile" line, so these measure the tree they
claim to and not a stale binary.

## The deciding arms

| arm | run 1 | run 2 | run 3 | median |
|---|---|---|---|---|
| `Dispatch: Single command handler` | 62.49 | 59.91 | 60.04 | **60.04** |
| `MediatR: Single command handler` | 43.77 | 41.78 | 42.78 | **42.78** |

Allocation is unchanged: Dispatch 96 B, MediatR 152 B -- Dispatch allocates
**1.58x less** on this row.

## THIS EPOCH RECORDS A REGRESSION, AND THE CAUSE IS KNOWN

The previous epoch measured 45.58 ns on the same arm. This one measures 60.04.
That is not drift and not noise:

| | pre-Lean (`95d8ccc8f7`) | this epoch | delta |
|---|---|---|---|
| Dispatch: Single command | 45.93 | 60.04 | +14.1 |
| MediatR: Single command | 39.68 | 42.78 | +3.1 |

**MediatR is the control.** It runs in the same process on the same machine and
moved only 3.1 ns, which is the machine's own drift. Dispatch moved 14.1. Net of
drift the change costs about **10-11 ns**, and the pre-Lean figure of 45.93
reproduces the previous epoch's 45.58, so the baseline is sound.

The cause is the deletion of the `DirectLocalContextInitializationProfile.Lean`
default, which had been skipping context initialization on the fast path. It was
deleted because it silently dropped CorrelationId and CausationId for every
consumer on stock configuration. **The regression is the price of a correctness
property the default path was not keeping**, and it was taken deliberately.

The correlation marks themselves are not the cost -- they are four plain bool
stores behind null checks. The cost is the message-type write, which goes through
the context Items dictionary (a read plus a write per dispatch). That is tracked
as a separate optimization with this measurement attached; it is an optimization,
not a reason to restore the profile.

## How to read the ratio

MediatR leads this row by about 1.40x while allocating 1.58x more. Both figures
belong in any honest citation of it. Do not quote the speed without the
allocation, and do not quote either from a single run -- an earlier "parity with
MediatR" claim came from one run and had to be retracted.
