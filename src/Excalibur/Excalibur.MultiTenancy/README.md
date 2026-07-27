# Excalibur.MultiTenancy

First-class, free multi-tenancy composition for Excalibur. One `AddMultiTenancy(...)` call selects a tenant-isolation strategy and wires fail-closed tenant scoping consistently across the event store, projections, and sagas.

## Strategies

- **Row-discriminator** — a single shared store per subsystem with a `TenantId` predicate applied inside every query. `AddMultiTenancy` wraps each registered `IEventStore`, `IProjectionStore<T>`, and `ISagaStore` with its fail-closed tenant-scoping decorator: an operation with no ambient tenant throws rather than running unscoped.
- **Sharding** — a dedicated store per tenant, selected per operation by tenant-aware routing. `AddMultiTenancy(Sharding)` registers tenant routing (the same wiring `EnableTenantSharding(...)` uses) and enforces the ambient-tenant requirement. The consumer supplies the shard map and provider-specific store resolvers.

## Usage

Register your persistence stores first, then compose multi-tenancy:

```csharp
services.AddExcalibur(x => x.AddEventSourcing(es =>
{
    es.UseSqlServer(sql => sql.ConnectionString(connectionString));
    es.AddRepository<Order, Guid>(id => new Order(id));
}));

// Row-discriminator: shared store, TenantId predicate, fail-closed decorators.
services.AddMultiTenancy(o => o.Strategy = TenantIsolationStrategy.RowDiscriminator);
```

The ambient tenant is required on every tenant-facing operation. Establish it per logical operation from the host — for example, a request middleware:

```csharp
app.Use(async (context, next) =>
{
    var tenant = context.Request.Headers["X-Tenant-Id"].FirstOrDefault();
    using (TenantContextHolder.BeginScope(tenant))
    {
        await next().ConfigureAwait(false);
    }
});
```

For the sharding strategy, register the shard map and a provider-specific store resolver, then compose. `AddMultiTenancy(Sharding)` registers tenant routing for you:

```csharp
services.AddExcalibur(x => x.AddEventSourcing(es =>
{
    es.UseSqlServerTenantEventStore(); // registers ITenantStoreResolver<IEventStore> + shard map
}));

services.AddMultiTenancy(o => o.Strategy = TenantIsolationStrategy.Sharding);
```
