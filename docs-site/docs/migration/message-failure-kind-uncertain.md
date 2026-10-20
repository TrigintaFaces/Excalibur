---
title: A delivery failure can be Uncertain
sidebar_label: Failure kind can be Uncertain
description: MessageFailureKind gained an Uncertain member. What stops compiling, what the kind means, and why a transport reports it instead of guessing.
---

# A delivery failure can be Uncertain

`MessageFailureKind` has a fourth member:

```csharp
public enum MessageFailureKind
{
    Transient = 0,
    Permanent = 1,
    Poison = 2,
    Uncertain = 3,   // new
}
```

## What stops compiling

An **exhaustive** `switch` over the enum in your own code — one with no `default` arm and no discard
pattern — no longer compiles, because it does not handle the new member. That break is deliberate and is
the point of adding a member rather than overloading an existing one: the compiler shows you every place
that has to decide what `Uncertain` means for your application.

```csharp
// Now incomplete
var action = kind switch
{
    MessageFailureKind.Transient => Retry(),
    MessageFailureKind.Permanent => Stop(),
    MessageFailureKind.Poison    => Quarantine(),
};
```

A `switch` that already has a `default` arm keeps compiling, and `Uncertain` falls into it. **Check that
the default is the safe direction for you** before relying on that — see below.

## What the kind means

`Uncertain` says the send **may or may not have reached the broker**, and the transport genuinely cannot
tell. It is not a milder `Transient` and it is not a quiet `Permanent`: it is the honest third answer to
"did this go out?".

The shipped case is AWS EventBridge. `PutEvents` can return an acknowledgement that does not establish
whether the entry was ingested, and `PutEventsRequestEntry` carries no client token, so a retry emits a
**distinct event** rather than replaying one. A transport in that position has no way to report
`Transient` honestly.

## What to do with it

**Treat it as "possibly delivered".** A retry may duplicate; abandoning may lose. Which risk you prefer is
an application decision the framework cannot make for you:

- if your consumers are idempotent (the obligation the outbox already states), retrying is safe and
  usually right;
- if a duplicate is more costly than a delay, route it for human inspection rather than retrying.

**Do not fold it into `Permanent`.** A permanent failure asserts the message did not go out; `Uncertain`
asserts the opposite is possible, and the handling differs.

## Known gap

The outbox currently records an `Uncertain` exhaustion as `MaxRetriesExceeded`, which is
indistinguishable in the dead-letter record from a message that provably never left. The exception text
survives, but the kind itself is not carried onto the dead-letter entry. If you redrive from the queue,
you cannot currently filter the uncertain ones out.
