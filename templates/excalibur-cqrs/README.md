# CQRS application

Run `dotnet restore` and configure `ConnectionStrings:EventStore` for the selected database.

For SQL Server or PostgreSQL, create the database and apply the matching provider package's
event-store, snapshot, and `004_CreateSubscriptionCheckpointSchema.sql` scripts before starting
the application. SQL Server uses `001_CreateEventStoreSchema.sql` and `002_CreateSnapshotSchema.sql`;
PostgreSQL uses `001_CreateSnapshotSchema.sql` and `002_CreateEventStoreSchema.sql`.
The restored `Excalibur.EventSourcing.SqlServer` or `Excalibur.EventSourcing.Postgres` package
contains these scripts; `dotnet nuget locals global-packages --list` locates the package cache.
The application creates the versioned projection table using the provider's schema generator.
For production, run this same schema generation during deployment with migration credentials.

The durable database variants persist both projection rows and checkpoints in the selected database.
Multiple application instances use the same read model. The in-memory variant is for development:
its events, projections, and checkpoints all disappear when the process stops.
Keep these three stores together: pairing an in-memory read model with durable checkpoints is
refused after its instance is replaced, because an empty dictionary cannot inherit completed progress.

Run `dotnet run`. Projection processing runs in the background. Pass a successful command's
`Session` receipt to `new GetOrderQuery(created.OrderId, created.Session)` for reads that must
observe that write. The query returns both `Projection` and `Freshness`; omitting the session
performs an immediate read without waiting. An incomplete projection does not undo an
event commit; retry the read rather than submitting the command again.

Keep `orders-read-models` stable when adding projections. Increment `ProjectionVersion` when
changing existing persisted semantics and use a new physical generation. Do not rename the
subscription to force a replay. Aggregate snapshots are optional and do not replace projection
checkpoints or the retained events needed to populate a new projection.

The sample exposes `POST /api/orders` with `{ "productId": "widget", "quantity": 2 }`,
`POST /api/orders/{id}/ship`, and `GET /api/orders/{id}`. Carry the write response's
`sessionToken` as `?after=<token>` on GET when consistency is required. A bounded read that
cannot confirm freshness returns HTTP 202; a confirmed read returns its projection and freshness.
The create response nests its token under `session`; the ship response is the receipt itself.

Projection storage admission is registered automatically. Defaults allow four reads and four writes
per configured resource, with an application-wide limit of 16 operations. Each host has its own
budget. For smaller shared databases, configure `ProjectionCapacityOptions` with lower limits;
reserve capacity for other application work. These limits govern framework logical operations,
not exact SDK connection counts. See the projection evolution documentation for queue metrics,
resource identity, and deployment limits.

## Repository operations and transactional models

The generated command handlers use the registered `IEventSourcedRepository<Order, Guid>` directly.
Load with `GetByIdAsync`, invoke the aggregate's business method, then call `SaveAsync`. The framework
tracks committed versions and appends pending events; no provider-specific repository implementation
or start-versus-append branch is required. For client preconditions, pass an opaque client ETag to
`SaveAsync(order, expectedETag, cancellationToken)` and return `order.ETag` after success.

This template deliberately defaults to **Async** projections with DI handlers and session-aware reads.
Those semantics remain unchanged. If an aggregate-local read model must commit atomically with its
events in SQL Server or PostgreSQL, use `TransactionalInline` starting in `10.0.0-alpha.21`.
This requires a dedicated transactional table, synchronous deterministic `When` handlers, and reading
that model through `IProjectionStore<T>`. Changing `.Async()` to `.TransactionalInline(...)` alone is
insufficient: `WhenHandledBy` and global session-token reads are not supported for transactional models.
Keep the existing background host for any separate Async/Inline projections you retain. The in-memory
variant does not implement TransactionalInline.

Follow the [consumer repository guide](https://docs.excalibur-dispatch.dev/docs/event-sourcing/consumer-repository)
and [relational provider setup](https://docs.excalibur-dispatch.dev/docs/event-sourcing/transactional-inline).
The complete runnable consumer sample is `samples/02-event-sourcing/TransactionalInline` in the repository;
it covers SQL Server, PostgreSQL, SQLite and Oracle, scoped services, ETags, audit metadata and rollback.
