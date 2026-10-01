---
title: An append result states its outcome, and its version is nullable
sidebar_label: Append result states its outcome
description: AppendResult and CloudAppendResult gained an Outcome discriminator, and NextExpectedVersion is now long?. What still compiles, what stops, and how to read a conflict correctly.
---

# An append result states its outcome, and its version is nullable

Appending to the event store returns a result that now says **which of four things happened**, rather than
leaving you to infer it from a boolean and an error string:

```csharp
public enum AppendOutcome
{
    Committed = 0,           // the events were written by this call
    AlreadyCommitted = 1,    // they were written by an earlier call whose acknowledgement was lost
    ConcurrencyConflict = 2, // another writer holds the version you expected
    Failed = 3,              // nothing was written
}
```

`AppendResult.Outcome` carries it. The same shape exists for the cloud-native stores as
`CloudAppendResult.Outcome` / `CloudAppendOutcome` — see [Two parallel surfaces](#two-parallel-surfaces).

## What still compiles, and what stops

**`Success` is unchanged and still correct.** It is true for `Committed` **and** `AlreadyCommitted` — both
mean the events the caller asked for are durable — so a host that only asks *did it work* needs no change:

```csharp
public bool Success => Outcome is AppendOutcome.Committed or AppendOutcome.AlreadyCommitted;
```

`IsConcurrencyConflict` is also unchanged.

**What stops compiling is a `long` that receives `NextExpectedVersion`.** The property is now `long?`:

```csharp
// Before
long next = result.NextExpectedVersion;

// Now — the value may be absent, and you must decide what to do when it is
if (result.NextExpectedVersion is { } next)
{
    // safe to pass straight back as the next expected version
}
else
{
    // reload; the store did not measure a version
}
```

`FirstEventPosition` was already `long?` and has not changed.

## Reading a conflict — the part most likely to be got backwards

**A conflict usually still gives you a number.** It is the one failure that *can* state a version, because
the store had to read the stream's actual version in order to detect the conflict at all. It reports that
measured value.

It reports `null` only in the narrower case where **the store detected the conflict by another route and
the version read did not succeed.** So:

| outcome | `NextExpectedVersion` |
| --- | --- |
| `Committed` | the version of the last event written |
| `AlreadyCommitted` | the version the earlier call landed at |
| `ConcurrencyConflict` | the **measured** actual version — including a genuine `-1`; `null` only when no version read succeeded |
| `Failed` | always `null` |

:::warning `-1` and `null` mean different things, and the difference is deliberate
Versions are zero-based, so `-1` is the ordinary value meaning **this stream does not exist**. It is not a
sentinel for failure. Every non-null value this property carries is one the store **measured** — never your
own expected version echoed back, never a bound, never derived from anything but a read.

That is what keeps the two readings separable: a `-1` here always means *the stream measurably does not
exist*, and *not measured* is `null` instead. A `Failed` result reports `null` rather than `-1` precisely
because handing back `-1` would assert the opposite of the truth, and a caller could pass it straight back
as an expected version and create a stream that already holds events.
:::

The conflict's `ErrorMessage` distinguishes the two cases in words as well — it either names the current
version it measured, or states that the store could not determine it.

## `AlreadyCommitted` is a success with one obligation attached

This is the outcome to read the fine print on. It means your events are in the stream, written by an
**earlier** call whose acknowledgement you never received — the store recognised your retry because the
events carry the same identifiers. `Success` is true and a caller that only asks *did it work* is answered
correctly.

It is nevertheless distinct from `Committed`, and the reason is the obligation:

**Present does not imply retrievable.** Those rows were written earlier, so they may have been acted on in
between — projected, archived, erased. **A caller still holding the live payloads must not assume they are
what the store would now return.** If your next step depends on the stored form, read it back rather than
reusing the instances in hand.

This is what makes the distinction worth having: `Committed` means *I wrote these just now, nothing has
happened to them yet*, and `AlreadyCommitted` does not.

**One case of that is handled for you.** If the events were not merely written earlier but **erased** since,
saving through `EventSourcedRepository` throws
[`ErasedStreamRepublicationException`](../compliance/gdpr-erasure.md#saving-an-aggregate-whose-events-were-erased-erasedstreamrepublicationexception)
rather than staging your live payloads to the outbox. You do not have to detect that case from `Outcome`
yourself.

Recognition depends on the identifier contract — the store can only match your retry if the events carry
the same `EventId`. See
[the identifier contract](../event-sourcing/event-store.md#the-identifier-contract). The stores that report
this outcome today are Cosmos DB, DynamoDB, Firestore, MongoDB and the in-memory store.

## Switch on the outcome rather than layering booleans

```csharp
var result = await eventStore.AppendAsync(
    order.Id, nameof(Order), events, expectedVersion, ct);

switch (result.Outcome)
{
    case AppendOutcome.Committed:
        break;

    case AppendOutcome.AlreadyCommitted:
        // Durable, but written earlier. Do not reuse the in-hand payloads as if freshly stored.
        break;

    case AppendOutcome.ConcurrencyConflict:
        // Reload and re-apply the command. result.NextExpectedVersion is the measured
        // current version when the store read one, and null when it did not.
        break;

    case AppendOutcome.Failed:
        throw new InvalidOperationException(result.ErrorMessage);
}
```

## Two parallel surfaces

The cloud-native stores return their own result type, with the same four-member discriminator:

| | event store | cloud-native store |
| --- | --- | --- |
| result | `Excalibur.EventSourcing.AppendResult` | `Excalibur.Data.CloudNative.CloudAppendResult` |
| discriminator | `AppendOutcome` | `CloudAppendOutcome` |
| returned by | `IEventStore.AppendAsync` | `ICloudNativeEventStore.AppendAsync` |

`CloudAppendResult` carries three members its sibling does not — `RequestCharge` (`double`),
`SessionToken` (`string?`) and `FailureKind` (`MessageFailureKind?`). Its `NextExpectedVersion` is `long?`
on the same terms described above.

**If you implement either interface yourself, the factory signatures changed.** Both conflict factories now
take a nullable measured version:

```csharp
AppendResult.CreateConcurrencyConflict(long expectedVersion, long? actualVersion)
CloudAppendResult.CreateConcurrencyConflict(long expectedVersion, long? actualVersion, double requestCharge)
```

Pass `null` only when your store genuinely did not read a version. Passing the caller's own
`expectedVersion` back would break the guarantee that every non-null value was measured.

Both types also gained a factory for the recognised-retry case:

```csharp
AppendResult.CreateAlreadyCommitted(long nextExpectedVersion, long? firstEventPosition)
CloudAppendResult.CreateAlreadyCommitted(long nextExpectedVersion, double requestCharge, string? sessionToken = null)
```

## Related

- [Event store](../event-sourcing/event-store.md#optimistic-concurrency) — the retry patterns these outcomes serve
- [Resolved issues](../resolved-issues.md) — the duplicate-write defect the discriminator closes, and the obligations it creates for a retrying caller
- [What's new](../whats-new.md#before-you-upgrade)
