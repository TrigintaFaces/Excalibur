---
title: Projections are applied by the projection host, not by SaveAsync
sidebar_label: Projections are applied by the host
description: Inline projections are removed. SaveAsync no longer writes read models; it returns a session token, and IProjectionSessionReader honours it at the read. What to change, and why the old model was unsound.
---

# Projections are applied by the projection host, not by `SaveAsync`

**`.Inline()` is removed.** So is `ProjectionMode.Inline`. There is no obsolete stub and no compatibility
shim: a registration that calls `.Inline()` no longer compiles.

Every stored projection is now applied by the projection host, reading the global event stream in order
with positioned compare-and-set writes. `SaveAsync` does not write read models. What replaces read-after-write consistency is
**session consistency**: the save returns a token, and a read that carries that token waits until the
projection has applied it.

## Who is affected

| you | affected? |
|---|---|
| call `.Inline()` anywhere | **yes** — it does not compile |
| read a projection immediately after `SaveAsync` and expect it current | **yes** — and this one still compiles |
| assign the result of `SaveAsync` to something other than `var` | **yes** — it returns `Task<SaveResult>` |
| already use `.Async()` with `EnableProjectionProcessing()` | supply `SubscriptionScope`, use positioned stores, and agree on one definition across cooperating processes |
| use `.Ephemeral()` only | no |
| use no projections | no |

The second row is the one to take seriously. **It compiles, it passes a single-threaded test, and it
returns stale or `null` data under load.**

## What to change

### 1. `.Inline()` becomes `.Async()`

```csharp
// before
builder.AddProjection<OrderSummary>(p => p
    .Inline()
    .When<OrderPlaced>((proj, e) => { proj.Status = "Placed"; }));

// after
builder.AddProjection<OrderSummary>(p => p
    .Async()
    .When<OrderPlaced>((proj, e) => { proj.Status = "Placed"; }));
```

### 2. Register the host — nothing will tell you that you forgot

:::danger This is the step that fails silently

`.Async()` registers a projection. It does not register anything that **writes** one. A missing *store*
registration fails start-up and names the projection; a missing host does not. Your application starts,
reports healthy, serves reads, and every one of them is empty.

:::

```csharp
services.AddExcalibur(excalibur => excalibur.AddEventSourcing(builder =>
{
    builder.AddProjection<OrderSummary>(p => p.Async().When<OrderPlaced>(/* ... */));

    builder.EnableProjectionProcessing(options => options.SubscriptionScope = "orders/events/read-models");   // <-- add this
}));
```

Multiple instances cooperate through positioned projection writes and the shared checkpoint CAS.
Use the same stable subscription name, `SubscriptionScope`, `SubscriptionRevision` and projection set
for API and Jobs. A definition mismatch is refused before effects; ambient leader election is not used.
External handler effects remain at-least-once and require idempotency. Changed semantics require a new
subscription name and a deliberate destination rebuild or cutover.

### 3. Carry the token to any read that must observe its own write

```csharp
// before -- immediately consistent under the old model
await repository.SaveAsync(order, ct);
var summary = await store.GetByIdAsync(order.Id.ToString(), ct);

// after
SaveResult save = await repository.SaveAsync(order, ct);

ProjectionRead<OrderSummary> read =
    await sessionReader.ReadAsync<OrderSummary>(order.Id.ToString(), save, ct);

switch (read.Freshness)
{
    case ProjectionFreshness.Fresh:
        return Results.Ok(read.Projection);          // observed your own write
    case ProjectionFreshness.BoundExpired:
        return Results.Accepted(value: read.Projection);  // may predate it; the save still succeeded
    default:
        return Results.Ok(read.Projection);          // NotRequested: no token, ordinary read
}
```

`IProjectionSessionReader` is registered for you by `AddProjection<T>()`. Inject it like any other
service.

**A caller that does not need the guarantee changes nothing and pays nothing.** Passing no token — or a
`SaveResult` whose `SessionToken` is `null` — reports `NotRequested` and waits for nothing.

### 4. `SaveAsync` returns a value

`IEventSourcedRepository.SaveAsync` now returns `Task<SaveResult>`:

```csharp
public readonly record struct SaveResult(long CommittedVersion, long? SessionToken);
```

Discarding it still compiles, so this is not a change the compiler finds for you everywhere. `await
repository.SaveAsync(order, ct);` is still valid and still correct for a caller that does not read back.

`SessionToken` is the global position of the **last** event the save wrote, and it is `null` when the
event store assigns no global ordering or the save wrote nothing. **`null` is a real answer:** there is no
coordinate to wait on, so a consistent read cannot be requested against this write. It does not mean the
save failed.

### 5. Serverless hosts drive the work themselves

An Azure Function or a Lambda has no long-running process for a `BackgroundService`. Call
`AddProjectionProcessor(options => options.SubscriptionScope = "orders/events/read-models")` and drive it from your trigger:

```csharp
var processor = serviceProvider.GetRequiredService<IProjectionProcessor>();

ProjectionProcessingResult result = await processor.ProcessPendingAsync(ct);

if (result.Outcome == ProjectionProcessingOutcome.Halted)
{
    logger.LogError("Projection processing halted after {Applied} events", result.EventsApplied);
}
```

| `Outcome` | meaning |
|---|---|
| `CaughtUp` | the shared subscription reached the stream head captured when this call began |
| `Halted` | work remains pending after an apply, prefix or checkpoint failure |
| `Cancelled` | the caller cancelled; unacknowledged work may be redelivered |

The built-in pull processor reconciles contested checkpoint writes and does not emit `NotLeader` or
`BackgroundWorkerOwnsCursor`. Read `Outcome`; an event count alone cannot establish freshness.
Manual and background drives can interleave between finite pages. With only manual processing,
nothing advances between invocations.

### 6. Failure handling moves

`EventNotificationOptions.FailurePolicy` now governs only the post-commit **notification handlers**. A
projection apply failure is handled by the host, which **does not advance its checkpoint past the failing
batch** — it re-reads the same events rather than skipping them.

So the pattern of catching `AggregateException` from `SaveAsync` to recover a projection no longer
applies: no projection runs there to fail.

## Why `.Inline()` was removed rather than deprecated

Because the guarantee it advertised was not the guarantee it delivered, and the gap was silent.

An inline projection ran after the events were durable, in-process, as a **second uncoordinated writer**
of the projection row. Two concurrent saves whose events keyed to the same row produced this:

```
save A   reads row  {count: 10}
save B   reads row  {count: 10}
save A   folds its events, writes {count: 12}
save B   folds its events, writes {count: 11}   <- nothing refuses it
```

Both saves returned success. **The events save A committed are durable in the event store and
permanently absent from the read model**, and nothing downstream ever learns — the row is well-formed and
every value in it was written correctly.

The write also carried no global position, so the row could not be advanced by anything afterwards; only
a full rebuild could repair it.

A deprecation would have left that reachable while reading as supported. One writer applying in position
order makes the interleaving inexpressible. The cost is the honest one: a read that needs its own write
may have to wait, bounded, and can say so.

## What is still not guaranteed

**Fold completeness holds on a store that records positions, and nowhere else.** Every persistent
projection store this framework ships implements `IPositionedProjectionStore<T>` — SQL Server,
PostgreSQL, Elasticsearch, OpenSearch, MongoDB, Cosmos DB, DynamoDB and Firestore — and on those the
conditional write refuses a writer working from a stale read.

On a store without that capability, a rebuild can still silently regress a row the host has advanced
past. A rebuild or replay onto such a store is now **refused when the service is constructed**; the
escape hatch is:

```csharp
services.AllowUnpositionedProjectionRebuild();
```

That acknowledges **exclusivity, not safety**. It records that you have stopped the projection host and
every other writer of those rows — which is more than the host: `ProjectionRebuildService`,
`ProjectionRecoveryService`, a second `GlobalStreamProjectionHost` (which performs no leader election)
and any code holding `IProjectionStore<T>` all write the same rows.

## See also

- [`ProjectionsSample`](https://github.com/TrigintaFaces/Excalibur/tree/main/samples/09-advanced/persistence-patterns/ProjectionsSample) — working code
  for the whole sequence, with no infrastructure required: register, save, `ProcessPendingAsync`,
  `ReadAsync`, print `Freshness`
- [Projections](../event-sourcing/projections.md) — the full reference
- [Reading Your Own Writes](../event-sourcing/projections.md#reading-your-own-writes-session-consistency)
- [What a projection's position means](../event-sourcing/projections.md#what-a-projections-position-means)
- [Projection processing](../event-sourcing/projections.md) — shared subscription definitions, positioned writes and API/Jobs cooperation
