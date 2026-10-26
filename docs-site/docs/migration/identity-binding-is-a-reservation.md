---
title: An identity binding is a reservation, and its flag is renamed to say so
sidebar_label: Identity binding is a reservation
description: IdentityBindResult.WasCreated is renamed MappingWasCreated. It reports that a MAPPING was created, never that an aggregate exists, and code using it as an insert-versus-update discriminator is wrong in a way that does not self-heal. CreateOrUpdateAsync removes the need to branch at all.
---

# An identity binding is a reservation, and its flag is renamed to say so

`IdentityBindResult.WasCreated` is renamed **`MappingWasCreated`**. The value and its meaning are
unchanged; the name was the problem.

```csharp
var bind = await identityMap.TryBindAsync(system, externalId, "Client", NewId(), cancellationToken);

bind.WasCreated          // was: reads as "the aggregate was created"
bind.MappingWasCreated   // now: reads as what it reports
```

## Why the rename is worth a break

A binding and an event stream are **two writes to two stores with nothing atomic between them**. The flag
answers *"did I win the race to reserve this id"* — a question about this call, against the identity map.
It does not answer *"does a stream exist for this aggregate"* — a question about a different store at a
different time.

Under the old name that distinction had to be read out of the documentation. Under the new one it is in
the expression.

## The failure the old name invited

```csharp
// WRONG, and it does not self-heal
if (bind.MappingWasCreated) { await InsertAsync(id, data, ct); }
else                        { await UpdateAsync(id, data, ct); }
```

If anything interrupts between the bind and the append — a cancellation at a processing deadline, a
crash, a pod eviction — the mapping is committed and the stream never arrives. From then on every call
returns `MappingWasCreated = false` for that external id, the update path loads the aggregate, finds
nothing, and the record **fails forever**. The only branch that would have created the stream is the one
the flag no longer selects.

Branch on whether the **stream** exists instead, which is correct at every interruption point:

```csharp
var client = await repository.GetByIdAsync(id, cancellationToken);
if (client is null) { await InsertAsync(id, data, cancellationToken); }
else                { await UpdateAsync(client, data, cancellationToken); }
```

**Do not add a compensating unbind.** It only runs if the process survives to run it — precisely the case
that produced the tear — and it races any concurrent writer that has already resolved the same mapping.

## Better: stop branching

`CreateOrUpdateAsync` does bind, load, create-or-update and save in one call, deciding from the store that
owns the answer. There is no flag to misread, and a torn reservation heals on the next call:

```csharp
await identityMap.CreateOrUpdateAsync(
    repository, "eVAS", clientNumber.ToString(), "Client",
    candidateId: Uuid7Extensions.GenerateGuid(),
    create: id => Client.Create(id, data.Name, data.Email),
    update: existing => existing.UpdateClientData(data.Name, data.Email),
    cancellationToken);
```

It is at-least-once, so `update` must be idempotent on the event identifier.

## Where the flag is still right

`MappingWasCreated` remains correct for what it reports: telling a first sighting from a repeat, emitting a
created-versus-updated metric, logging. Those readings were never wrong, and nothing about them changes.

## If you bind as a "have I imported this?" marker

Some integrations use the identity map as a dedupe marker rather than an id reservation. If you do, **the
ordering matters more than the flag**: resolve first, do the work, and bind only once the work has
succeeded.

```csharp
if (await identityMap.ResolveAsync<Guid>(system, externalId, "Order", ct) is not null) { return; }

var orderId = await dispatcher.DispatchAsync<CreateOrderCommand, Guid>(command, context, ct);

await identityMap.BindAsync(system, externalId, "Order", orderId, ct);
```

Binding first strands the import permanently if the work fails. Binding last means a failure is retried as
a duplicate **attempt** — which your handler must be idempotent against, and which is recoverable.
