---
sidebar_position: 1.6
title: Implement a consumer application
description: Use the registered aggregate repository to create, load, update, and query orders with concurrency protection.
---

# Implement a consumer application

Inject `IEventSourcedRepository<Order>` into your application service. The framework implements the
repository; your code defines business operations, maps request metadata, and handles save outcomes.
Use the [walkthrough](walkthrough.md) to set up your first application and
[TransactionalInline setup](transactional-inline.md) for provider registration and table provisioning.

The complete runnable example is in `samples/02-event-sourcing/TransactionalInline`. It supports
SQL Server, PostgreSQL, SQLite, and Oracle, starting with `10.0.0-alpha.21`.

## Register the aggregate and application service

After selecting the event-store provider, register the repository and event types inside
`AddEventSourcing`:

```csharp
es.AddRepository<Order>(_ => new Order());
es.RegisterEventTypesFromAssembly(typeof(Order).Assembly);
```

Register your application service with `services.AddScoped<OrderApplicationService>()`.
Resolve it within a request scope. The console sample uses a fresh async scope for each simulated
request, so its checks exercise persistence rather than retained aggregate instances.

## Create, load, and update

Creation starts with a new aggregate and its initial event. There is no separate stream-start call
or existence check required before saving:

```csharp
var order = new Order();
order.Add(id, amount);
var saved = await repository.SaveAsync(order, cancellationToken);
// Return order.ETag and saved.CommittedVersion to the caller.
```

`GetByIdAsync` reconstructs aggregate state from its snapshot and event history. It returns `null`
when the aggregate does not exist. An aggregate document projection is not required.

For updates, load the aggregate, invoke its business method, and pass the client's ETag:

```csharp
ArgumentException.ThrowIfNullOrWhiteSpace(expectedETag);
var order = await repository.GetByIdAsync(id, cancellationToken)
    ?? throw new KeyNotFoundException($"Order '{id}' was not found.");
order.Add(id, amount);
var saved = await repository.SaveAsync(order, expectedETag, cancellationToken);
```

Treat `expectedETag` as an opaque value previously returned to the client. Do not parse it as a
number or calculate the next value. This precondition protects an older client request even when
the server loads the latest aggregate. The event store also checks the loaded stream version
atomically, protecting against another writer racing the save.

After success, return the aggregate's updated `ETag`. `SaveResult.CommittedVersion` identifies the
aggregate version; `SessionToken` is a global event-position coordinate with a different purpose.
Neither is an affected-row count. `GetUncommittedEvents().Count` measures pending events before a
save, not the number of newly inserted rows proven by a retry.

Use `ExistsAsync` when existence itself is the question. Do not use a check-then-create sequence as
a concurrency guarantee. Multiple `SaveAsync` calls do not implicitly share one transaction.

## Read aggregate state and query models

Return aggregate details such as `Id`, `Total`, `Version`, and `ETag` from a read operation. State
used by business rules must be restored in the aggregate's event-application method. The sample
restores both its identifier and total.

Query screens use `IProjectionStore<OrderSummary>`. With TransactionalInline, a successful save
commits this aggregate's dedicated read model and events together. Reads use ordinary
`GetByIdAsync`; global session-token reads are not supported for this aggregate-local mode.
For Async and Inline models, follow the [projection consistency guide](projections.md).

## Carry request metadata deliberately

The sample's `RequestMetadata` carries actor and correlation identifiers. `Order.Add` initializes
the event's metadata dictionary, then the service adds `actor-id` and `correlation-id` before saving.
Store audit identifiers, not bearer tokens. Activity-based enrichment requires existing event
metadata; it does not replace your application's audit mapping.

Outbox delivery is a separate configuration decision. The built-in selection targets integration
events; saving a domain event does not automatically publish it externally. Atomic event/outbox
staging requires a compatible transactional writer. The sample intentionally verifies database
persistence without configuring an outbox. See [repository outbox integration](repositories.md).

## Handle failed saves

On a concurrency conflict, return a conflict/precondition response appropriate to your API and
let the caller reload before deciding whether to retry the business operation. Do not silently
replace the client's ETag and replay an outdated command.

A TransactionalInline handler failure rolls back both the events and model writes. Discard the
failed aggregate instance and reload for a later operation. The sample deliberately rejects a
negative amount in its projection to demonstrate rollback; enforce normal business validation in
the aggregate before raising events.

`AppendOutcomeUnknownException` means the client could not establish the commit outcome. It is
not proof of rollback. Reconcile the original operation using your application operation identity
and durable state before submitting a new command. A read showing some later state does not alone
prove which request committed. The sample reports this condition and fails rather than retrying
with newly generated event identities.

## Run and inspect the complete sample

```powershell
$env:TRANSACTIONAL_INLINE_PROVIDER = 'sqlite'
$env:TRANSACTIONAL_INLINE_SQL = 'Data Source=transactional-inline.db'
dotnet run --project samples/02-event-sourcing/TransactionalInline
```

The sample verifies create, reload, ETag-protected update, stale-client refusal, persisted audit
metadata, and rollback after a projection failure. It retains the database for inspection and uses
a new order identifier on every run.

| File | Responsibility |
|---|---|
| `Domain.cs` | Aggregate state, business operation, event identity, and query model. |
| `OrderApplicationService.cs` | Complete consumer service, request metadata, and response records. |
| `ConsumerScenario.cs` | Assertions across separate request scopes, including failure cases. |
| `Program.cs` | Host, repository, event registration, and projection handlers. |
| `SampleProviders.cs` | Provider-specific event/model registrations. |
| `SampleSchema.cs` | Disposable sample schema creation; use deployment migrations in production. |

The `excalibur-cqrs` template keeps its Async/DI-handler defaults. Its README explains the explicit
changes required to opt into TransactionalInline; changing only the mode is insufficient because
the destination schema, handler shape, and read semantics also differ.
