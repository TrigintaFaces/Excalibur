---
title: Marking a message dead-lettered states its outcome
sidebar_label: Dead-letter mark states its outcome
description: IDeadLetterableOutboxStore.MarkDeadLetteredAsync now returns OutboxCompletionOutcome instead of ValueTask. What stops compiling, why a refused mark could not be seen before, and what a custom store must now report.
---

# Marking a message dead-lettered states its outcome

`IDeadLetterableOutboxStore.MarkDeadLetteredAsync` now returns an outcome instead of nothing:

```csharp
// Before
ValueTask MarkDeadLetteredAsync(
    string messageId, string reason, int attemptCount, CancellationToken cancellationToken);

// Now
ValueTask<OutboxCompletionOutcome> MarkDeadLetteredAsync(
    string messageId, string reason, int attemptCount, CancellationToken cancellationToken);
```

**This only affects you if you implement an outbox store.** If you consume one — staging messages and
letting the framework drain them — nothing in your code changes, and the behaviour you get is strictly
better.

## Why it changed

The drain writes the dead-letter **entry** first and marks the outbox **row** second. Every store can
refuse that second step: the row may be gone, it may already be sent or dead-lettered, or another drain
may hold its lease. With a `ValueTask` return, "I refused" and "I applied it" were the same observation,
so the processor reported success either way.

The consequence was not a lost error message. It was a message left **simultaneously delivered and sitting
unreplayed in the dead-letter queue** — the live claimant goes on to deliver it, while the entry the drain
already wrote stays pending. The shipped redrive path selects pending entries by filter, so an operator
draining the queue re-executes a message that already succeeded, from the framework's own bookkeeping
rather than any fault of theirs.

The fenced variant, `IFencedDeadLetterableOutboxStore.MarkDeadLetteredAsync`, already returned an outcome
and already withdrew the entry on a refusal. The unfenced path — which is the **default**, used whenever
leader election is not configured — could not, because it could not see the refusal. The two now behave
the same way.

## What stops compiling

Only a custom `IDeadLetterableOutboxStore`. Return the verdict your implementation already computes:

```csharp
public async ValueTask<OutboxCompletionOutcome> MarkDeadLetteredAsync(
    string messageId, string reason, int attemptCount, CancellationToken cancellationToken)
{
    var updated = await UpdateRowAsync(messageId, reason, attemptCount, cancellationToken);
    return updated > 0
        ? OutboxCompletionOutcome.Applied
        : OutboxCompletionOutcome.MessageNotFound;
}
```

A decorator must **forward** the value rather than await and discard it. A decorator that swallows the
outcome reinstates the original defect for every host that installs it, and does so invisibly — the
decorated path still compiles and still logs.

## What to report

| outcome | when |
|---|---|
| `Applied` | the row was mutated — **the only success value** |
| `MessageNotFound` | no row matched the identifier |
| `AlreadyTerminal` | the row was already sent or already dead-lettered |
| `ClaimLost` | the store holds a delivery lease for a different owner |
| `FenceRefused` | **not reachable here** — this member carries no fencing token |
| `Unknown` | never return this deliberately; it exists so an unset value cannot read as success |

Two rules the framework relies on:

**Test for `Applied` by name, never by excluding the refusals.** Deciding success by exclusion makes every
member added to the enum later start life as a success, which is the direction this contract must not fail.

**Report the refusal; do not throw it.** The drain reaches this member from inside its own exception
handling, where a sibling handler cannot catch — an exception raised here abandons every other message in
the batch.

### If your store cannot distinguish the refusals

Report the weakest one that is true rather than guessing. The shipped Postgres and Oracle providers *move*
the row into a dead-letter table in a single statement, so a completed dead-letter leaves nothing behind
and "already terminal" is genuinely indistinguishable from "no such row"; both report `MessageNotFound`.
That is honest, and the caller's handling is identical for every non-`Applied` value — it withdraws the
entry either way. The distinction is for your operators, not for the framework's control flow.

## Related

`MessageFailureKind` gained an `Uncertain` member in the same release. An exhaustive `switch` over that
enum in your own code will no longer compile — see
[the failure-kind note](./message-failure-kind-uncertain.md).
