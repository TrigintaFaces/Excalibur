---
sidebar_position: 1.5
title: EventStore walkthrough
description: Follow one application from its first event through saves, projections, versioning, recovery, and upgrades.
---

# EventStore: from first save to a safe upgrade

Use this guide as your starting point. It explains the order of work and the decisions you need to
make; the linked reference pages provide additional options when you need them.

We will follow an order that receives two amounts, `10` and `5`, and a read model whose total becomes
`15`. The runnable SQL Server example also rejects a third amount to demonstrate rollback.

:::info Release availability
`TransactionalInline` is available starting in `10.0.0-alpha.21`. Ordinary Inline and Async retain
their existing behavior. The upgrade section below separately covers the checkpoint schema
introduced in `10.0.0-alpha.20`.
:::

## 1. Understand what gets saved

Your application changes an **aggregate** by calling its business methods. Those methods raise
**domain events**. The repository persists the pending events and later reconstructs the aggregate
by replaying its history. A **projection** builds a separate model for queries.

For our example:

| Part | Responsibility |
|---|---|
| `Order` | Accept an operation and raise `AmountAdded`. |
| `AmountAdded` | Record which order received which amount. |
| Event store | Persist the stream and detect conflicting writes. |
| `OrderSummary` | Hold the query-side total. |
| Projection handler | Add each event's amount to that total. |

The sample aggregate restores its identifier and total from events. `OrderSummary` separately holds
the query-side total. State needed to enforce business rules belongs in the aggregate and must be
restored by its event-application methods.

### What consumers implement

For a complete application service with request metadata, ETag handling, and failure behavior,
follow the [consumer implementation guide](consumer-repository.md).

Inject the registered `IEventSourcedRepository<Order>` rather than writing a provider-specific
repository. Call `GetByIdAsync`, invoke a business method on the aggregate, then call `SaveAsync`.
For creation, construct a new aggregate and raise its initial event before saving. The repository
uses the committed aggregate version to distinguish a new stream from an append and detect conflicts.
Consumers do not need a separate stream-state query or a start-versus-append branch.

`GetByIdAsync` reconstructs the aggregate from snapshots and events; it does not require an aggregate
document projection. `ExistsAsync` supplies the existence check. Query screens use separate projection
stores rather than querying the aggregate repository.

For an HTTP precondition, pass the client's opaque ETag to `SaveAsync(aggregate, expectedETag, ct)`.
After a successful save, return `aggregate.ETag`. Do not parse it as an event count or calculate the
next ETag yourself. `SaveResult.CommittedVersion` is the aggregate version; `SessionToken` is a
different, global event-position coordinate. `SaveResult` has no affected-row count. If needed,
`aggregate.GetUncommittedEvents().Count` gives the number pending before saving, not a receipt proving
how many new rows a retry inserted.

Outbox staging is configured once and selects integration events; application-specific actor/audit
metadata still needs an explicit mapping. Correlation enrichment uses the current `Activity` when
event metadata exists. Multiple repository saves do not implicitly become one shared session
transaction. TransactionalInline joins one aggregate append and its registered models.

## 2. Choose what a save must guarantee

Choose this before configuring your read-model store:

| Requirement | Mode | After saving |
|---|---|---|
| Reads can catch up later, including in another database | `Async()` | Events are durable; the processor updates models independently. |
| Wait briefly for models, including in another database | `Inline()` | Events commit first, then the save waits within a bound for processing. |
| This aggregate's SQL model must commit or roll back with its events | `TransactionalInline(nameof(Order))` | Events and dedicated model tables commit in the same database transaction. |

Using the same database for both stores does not make ordinary Inline transactional. The transactional mode
requires the same database and tenant, registered repository writers, and synchronous deterministic
handlers without external effects. It cannot include an Elasticsearch index or an HTTP request in
that SQL transaction.

The next three steps run the transactional example so you can observe both commit and rollback.
If your model belongs in Elasticsearch or another store, use the host-processed branch in step 6.

For PostgreSQL, SQLite, or Oracle, use the same flow with the
[provider-specific registration and provisioning](transactional-inline.md).

## 3. Run the complete SQL Server example

You need the repository's selected .NET SDK and an **existing disposable SQL Server database**.
The sample creates its tables, but does not create the database. Its database login needs permission
to create tables. Run from the repository root:

```powershell
$env:TRANSACTIONAL_INLINE_SQL = 'Server=localhost;Database=TransactionalInlineSample;Integrated Security=True;TrustServerCertificate=True'
dotnet run --project samples/02-event-sourcing/TransactionalInline
```

Replace the connection string for your local SQL Server. `TrustServerCertificate=True` in this local
example is not a production TLS configuration.

The final line should be:

```text
PASS: two events and total 15 committed; rejected third event and model update rolled back.
```

An error log for the deliberately rejected third save is expected. The program verifies that the
reloaded aggregate still has version `2` and the stored summary still has total `15`. Each run creates
a new aggregate identifier and retains its data so you can inspect the result.

The complete files are in `samples/02-event-sourcing/TransactionalInline`: `Domain.cs` defines the
types, `Program.cs` wires them, `OrderApplicationService.cs` implements consumer operations,
`ConsumerScenario.cs` verifies separate requests, and `SampleSchema.cs` provisions the sample tables.
For production, run the provider DDL and projection table creation through your deployment migration
process. Registration itself does not create the SQL tables.

## 4. Follow the event from the aggregate to the model

These types show the event-application flow, identifier validation, and event metadata initialization:

```csharp
using Excalibur.Dispatch;
using Excalibur.Domain.Model;

[MessageName("Sample.TransactionalInline.AmountAdded")]
public sealed record AmountAdded(string OrderId, decimal Amount) : DomainEvent;

public sealed class Order : AggregateRoot
{
    public decimal Total { get; private set; }

    public void Add(string id, decimal amount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (!string.IsNullOrEmpty(Id) && !string.Equals(Id, id, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("An existing order cannot change its identifier.");
        }
        RaiseEvent(new AmountAdded(id, amount) { Metadata = new Dictionary<string, object>() });
    }

    protected override bool ApplyEventInternal(IDomainEvent @event)
    {
        if (@event is not AmountAdded added) { return false; }
        Id = added.OrderId;
        Total += added.Amount;
        return true;
    }
}

public sealed class OrderSummary
{
    public decimal Total { get; set; }
}
```

`MessageName` is the stored event identity. Keep it stable when refactoring a CLR namespace or class.
`ApplyEventInternal` restores state; it must not send messages, call remote services, or perform other
external effects when history is replayed.

The sample's registration connects those types:

```csharp
builder.Services.AddSqlServerProjectionStore<OrderSummary>(options =>
{
    options.ConnectionString = connectionString;
    options.TableName = "TransactionalOrderSummary";
});
builder.Services.AddExcalibur(x => x.AddEventSourcing(es =>
{
    es.UseSqlServer(sql => sql.ConnectionString(connectionString));
    es.AddRepository<Order>(_ => new Order());
    es.RegisterEventTypesFromAssembly(typeof(Order).Assembly);
    es.AddProjection<OrderSummary>(p => p.TransactionalInline(nameof(Order))
        .When<AmountAdded>((view, e) =>
        {
            if (e.Amount < 0)
            {
                throw new InvalidOperationException("Sample projection refuses negative amounts.");
            }
            view.Total += e.Amount;
        }));
}));
```

This excerpt belongs inside the sample's host setup. `Program.cs` includes the required namespaces,
host start/stop, and service resolution. `SampleSchema.cs` uses
`SqlServerProjectionSchema.CreateTransactionalTableScript` for the dedicated model table. Do not
reuse an ordinary Inline/Async destination or write to it with unrelated upserts.

## 5. Save, load, and handle a rejected operation

Use the registered repository for every writer that needs transactional projection coordination:

```csharp
var repository = host.Services.GetRequiredService<IEventSourcedRepository<Order>>();
var models = host.Services.GetRequiredService<IProjectionStore<OrderSummary>>();
var id = Guid.NewGuid().ToString("N");
var order = new Order();

order.Add(id, 10);
var firstSave = await repository.SaveAsync(order, CancellationToken.None);
order.Add(id, 5);
await repository.SaveAsync(order, CancellationToken.None);

var reloaded = await repository.GetByIdAsync(id, CancellationToken.None);
var summary = await models.GetByIdAsync(id, CancellationToken.None);
// reloaded.Version == 2; summary.Total == 15
```

The transactional handler folds the complete aggregate history plus pending events into a fresh
model on each coordinated save. The work grows with stream length; aggregate snapshots do not
replace this projection replay. Direct `IEventStore.AppendAsync` calls bypass the coordinator.

After a transactional projection failure, the append and model writes roll back, but your in-memory
aggregate still contains its pending event. Discard or reload that instance before attempting a
different business operation. The sample demonstrates this with a negative amount.

`SaveResult.Projections` and its attestation describe **post-commit Inline processing**, not the
transactional model. A transactional-only save can report `NotRequested` even though its model
committed successfully. Read that model through `IProjectionStore<OrderSummary>`; global session-token
reads are not supported for this aggregate-local mode. An ordinary read can observe a subsequent
writer's committed update; it is not an immutable snapshot of your save.

For competing writers, distinguish a confirmed concurrency conflict from an uncertain acknowledgement:

| Outcome | Next action |
|---|---|
| Confirmed concurrency conflict | Reload current state, re-evaluate the business command, then attempt a new save if still valid. |
| Inline completion bound expired | Events already committed. Retry or wait for the read; do not repeat the command. |
| Append acknowledgement is indeterminate | Reconcile the original operation or retry its original event identities under the documented append contract. Do not generate a second business operation. |
| Transactional handler rejected the save | Fix the command/handler as appropriate and reload before another command. |

See [repository concurrency](repositories.md#optimistic-concurrency) and
[append retry rules](event-store.md#two-different-failures-two-different-retries) for the precise APIs.

## 6. Use host-processed models when stores are separate

This is an **alternative configuration**, not a change to the transactional sample's existing table.
For an order summary in Elasticsearch, for example, register its provider store and choose `.Inline()`
or `.Async()` for the same event-folding logic. Provision a fresh destination suitable for positioned
writes. The [Inline quick start](projections.md#quick-start-inline-in-one-application-or-several)
shows the complete registration shape.

- **Inline:** registered recovery runs while the host is running. A save commits events first and
  then waits within its completion bound. `Attested` means completion was observed at save time;
  `BoundExpired` means it was not confirmed, with the events still committed.
- **Async:** configure the projection-processing host with `EnableProjectionProcessing`, its durable
  checkpoint store, and stable subscription identity. Registering an Async projection alone does not
  start its processor. See [Async configuration](projections.md#async-configuration-and-explicit-processing)
  for the registration and explicit processing options.

When a read must observe a previous save, carry its session token through `IProjectionSessionReader`
and require `Fresh` for the returned data; see [session consistency](projections.md#reading-your-own-writes-session-consistency).
A save-time `Attested` receipt is not a permanent freshness
promise. For Elasticsearch searches, also configure the store's search-visibility barrier; fetching
one document and searching an index have different visibility behavior.

To try both modes and session reads without external infrastructure, run the in-memory projections
sample from the repository root:

```powershell
dotnet run --project samples/09-advanced/persistence-patterns/ProjectionsSample
```

It uses a product catalog and category summary to demonstrate Inline, explicitly driven Async, and
token-aware reads. Its in-memory stores are for learning and tests; configure durable provider stores
and checkpoints before deploying that pattern.

The rest of this guide calls these **host-processed projections**. Their ordered checkpoints,
background catch-up, session reads, and generation cutover do not automatically apply to
TransactionalInline.

## 7. Change event data without losing old history

First identify which kind of change you are making:

| Change | Mechanism |
|---|---|
| Another event was appended to an order | The stream version advances; concurrency uses the expected stream version. |
| `AmountAdded` gains a compatible optional field | Keep its stored name; verify deserialization of old payloads and the new default. |
| The payload changes incompatibly | Retain a representation of the historical shape and register an upcaster to the current shape. |
| A CLR type or namespace is renamed | Preserve `MessageName`; if its stored name also changes, retain the old name as an alias. |
| The query model's fields or meaning change | Create a new projection generation/destination; see step 8. |
| A table or index definition changes | Apply a database migration; it does not upcast events or populate models. |

For example, adding a currency with a deliberate default can keep the existing stored identity:

```csharp
[MessageName("Sample.TransactionalInline.AmountAdded")]
public sealed record AmountAdded(
    string OrderId, decimal Amount, string Currency = "USD") : DomainEvent;
```

This replaces the earlier event declaration; do not register both declarations together. Test old
JSON without `Currency`, new JSON, and actual replay through your configured serializer. Ignoring
unknown fields alone does not prove that missing-field defaults or business meaning are correct.

For an incompatible change, follow the [upcaster registration example](versioning.md#upcasting-pipeline-registration).
Keep the logical message type stable across its payload versions, preserve event identity and metadata,
and test every historical shape still present in storage. Upcasting transforms data while reading;
it does not rewrite stored events. Keep old type mappings and aliases for as long as history needs them.

## 8. Change a read model and switch readers

### Host-processed Inline/Async projections

Suppose `OrderSummary` needs new fields or a different mapping:

1. Declare a new projection version and provision its new physical destination. Do not reset the old
   cursor over an incompatible table or Elasticsearch mapping.
2. Register the new generation with its handlers while keeping the subscription name stable. It
   receives its own binding and catches up from the retained event history.
3. Keep the old generation available while comparing the new model's counts, representative values,
   error state, and lag. A green connection health check is not evidence that replay completed.
4. Use a compatible generation reader and the checked cutover API to switch the application's read
   boundary. Re-read administrative state after a contested cutover; do not blindly repeat stale decisions.
5. Retain the old destination and history through the rollback window; explicitly retire them later.

The [projection-evolution guide](projection-evolution.md) supplies the version attribute, reader
registration, cutover calls, and rollback procedure. Ordinary `IProjectionStore<T>` calls and native
Elasticsearch queries do not automatically follow the framework's reader pointer.

### TransactionalInline projections

Use a new dedicated transactional table/generation and a deliberate deployment/backfill plan.
An existing stream is projected on its next successful coordinated save; dormant aggregates are
**not automatically backfilled**. Do not apply the host-processed catch-up procedure above and assume
it populated them. Verify every model required by the application before switching reads.

Keep every writer on the intended model generation. This mode does not register automatic projection
erasure/recovery callbacks; applications requiring those operations need an explicit, validated workflow.
See [transactional projection limits](projections.md#transactionalinline-in-one-sql-server-database).

## 9. Recover without repeating committed business operations

For host-processed projections, start with the failed member and its error, checkpoint, destination,
and source position. Correct the handler, permissions, unavailable store, or incompatible destination.
Recovery retries missing work while a host runs, including after restart. Healthy members can keep
progressing independently.

A position gap is different from a temporarily slow projection. Missing events may represent lost
history or an append still in progress. Do not renumber events or advance all cursors to make the lag
disappear. Establish the provenance of missing positions and restore history or use a reviewed repair.

For an incompatible model or mapping, populate a fresh generation and validate it before cutover.
In-place repair requires stopped and drained writers/readers. Snapshots optimize aggregate loading;
they are not a replacement source for rebuilding projections.

## 10. Upgrade an existing deployment

Read the release notes for the versions you cross, then separate three activities: database schema
migration, event payload compatibility, and read-model population/cutover. Passing one does not prove
the other two are safe.

For **alpha.19 to the checkpoint schema introduced in alpha.20**, use the
[incremental migration guide](../migration/alpha19-to-alpha20-sqlserver.md). Its script archives the
legacy checkpoints and rejects malformed event-position sequences before changing checkpoint tables.
It does not rewrite events, snapshots, or CDC data, and does not invent new binding progress from old
cursor values. Rehearse against a restored backup, stop old hosts, migrate, replay into fresh
destinations, validate, and only then cut over.

If that schema is already deployed, do not reset working bindings just to adopt alpha.21's repository
and registration fixes. TransactionalInline remains an explicit opt-in with separately provisioned
tables. Keep the current Inline/Async behavior unless you deliberately choose a mode migration.

For PostgreSQL, SQLite, Oracle, or custom checkpoint names, follow the
[legacy checkpoint replacement procedure](../migration/checkpoint-schema-upgrade.md). It retains
old checkpoint and definition rows and requires replay into fresh projection destinations.

Before serving traffic, verify event appends and aggregate reloads, model values and lag, expected
failure handling, and the chosen read-consistency guarantee. Keep the backup and previous projection
destinations until the rollback window ends.

## Where to go next

| Task | Detailed reference |
|---|---|
| Provider registration, append outcomes, storage operations | [Event store](event-store.md) and [providers](providers.md) |
| Aggregate loading, saves, ETags and concurrency | [Repositories](repositories.md) |
| Stored names, aliases and payload upcasting | [Domain events](domain-events.md) and [event versioning](versioning.md) |
| Projection modes, DI handlers and session reads | [Projections](projections.md) |
| New destinations, checked cutover and rollback | [Projection evolution](projection-evolution.md) |
| Aggregate load performance | [Snapshots](snapshots.md) |
| Schema deployment and upgrade scripts | [Migrations](migrations.md) |
