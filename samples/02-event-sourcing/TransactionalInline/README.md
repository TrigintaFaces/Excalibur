# Relational TransactionalInline

TransactionalInline is available starting in `10.0.0-alpha.21`. This sample uses project references
to the matching framework source.

This console sample saves an aggregate and its read model in one database transaction.
It uses the registered `IEventSourcedRepository<Order>`, a dedicated transactional projection
table, and the framework's default single-tenant context.

Requirements: .NET 10. SQL Server, PostgreSQL, and Oracle need an existing disposable database; SQLite uses a local file. Set a connection string
with permission to create tables, then run from the repository root:

```powershell
$env:TRANSACTIONAL_INLINE_SQL = 'Server=localhost;Database=TransactionalInlineSample;Integrated Security=True;TrustServerCertificate=True'
dotnet run --project samples/02-event-sourcing/TransactionalInline
```

Select a provider with `TRANSACTIONAL_INLINE_PROVIDER`: `sqlserver` (default), `postgres`, `sqlite`, or `oracle`.
`TRANSACTIONAL_INLINE_SQL` holds that provider's connection string. For SQLite, the default is
`Data Source=transactional-inline.db` when no connection string is supplied:

```powershell
$env:TRANSACTIONAL_INLINE_PROVIDER = 'sqlite'
$env:TRANSACTIONAL_INLINE_SQL = 'Data Source=transactional-inline.db'
dotnet run --project samples/02-event-sourcing/TransactionalInline
```

For PostgreSQL use the `public` schema. Oracle uses the connected user's current schema and requires
table-creation permission and quota; its DDL runs before starting the application transaction.
Use an empty disposable database for the first run. This sample provisions schemas; it does not upgrade
existing event tables from earlier versions.

The sample creates event tables from the provider's shipped SQL script and the model table from
the selected provider's `*ProjectionSchema.CreateTransactionalTableScript`. For production, run these through
your deployment migration tooling instead of creating schemas at application startup.

The program checks that:

1. Creation and an ETag-protected update commit two events and a model total of 15 across separate request scopes.
2. An update using the initial, stale ETag is rejected.
3. A negative amount throws from the projection handler and rolls back.
4. Reloading returns aggregate version 2, aggregate/model total 15, and the unchanged ETag.
5. Both persisted events contain the expected actor and correlation metadata.

Expected final output:

```text
PASS: create, reload, ETag update, stale-client refusal and transactional rollback verified.
PASS: two events and total 15 committed; rejected third event and model update rolled back.
```

The expected rejection also produces an event-store error log. Every run uses a new aggregate id.
The database is retained so you can inspect the results.

`TransactionalInline(nameof(Order))` folds complete aggregate history plus pending events into a
new `OrderSummary` on each save. Keep handlers deterministic, synchronous, and free of external
effects. Work grows with stream length. This mode does not support cross-aggregate keys, async
DI handlers, or deletion callbacks. Reads use `IProjectionStore<OrderSummary>`; global session
token reads are not supported for this aggregate-local mode.

Use the registered repository for all writers. Direct `IEventStore.AppendAsync` calls bypass this
coordination. Do not share the destination table with ordinary upserts or Inline/Async processors.
Those modes retain their existing behavior and remain suitable when event and model stores are separate.

Start with `OrderApplicationService.cs` for the consumer service and response records, then
`ConsumerScenario.cs` for assertions across request scopes. `Domain.cs` defines aggregate/event/model
types; `Program.cs` and `SampleProviders.cs` configure DI; `SampleSchema.cs` provisions disposable tables.
See the [consumer guide](../../../docs-site/docs/event-sourcing/consumer-repository.md) for the full flow.

See [projection documentation](../../../docs-site/docs/event-sourcing/transactional-inline.md)
for outbox staging, table generations, failure semantics, and provider requirements.
