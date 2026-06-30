# Multi-Tenant Event Sourcing

**Location:** `samples/11-real-world/MultiTenantEventSourcing/`

> **Canonical tenant-aware pipeline :**
>
> ```
> POST /orders  (X-Tenant-Id: tenant-acme)
>   -> scoped ITenantId resolver reads the header
>   -> IDispatcher.DispatchAsync(CreateTenantOrderCommand)
>   -> CreateTenantOrderHandler
>        -> IEventSourcedRepository<TenantScopedOrder, Guid>.SaveAsync(...)
>            -> TenantRoutingEventStore resolves ITenantShardMap[tenantId]
>            -> events appended to the matching shard
>        -> logs "shard-per-operation: {tenantId} -> {shardId} ({region})"
> ```
>
> The shard-map introspection endpoints (`GET /shards`, `GET /shards/{tenantId}`)
> remain available for wiring inspection. The new `POST /orders` endpoint is the
> canonical path that actually exercises `TenantRoutingEventStore`.

Demonstrates tenant-aware event sourcing using Excalibur's sharding primitives:

- `ITenantShardMap` -- resolves `tenantId -> ShardInfo`
- `ShardInfo` -- per-shard connection string, schema, region
- `.EnableTenantSharding(opts => ...)` -- scoped decorator for `IEventStore`
- `.UseSqlServerTenantEventStore()` -- SQL Server resolver that materializes
  a shard-specific event store per tenant
- `TryAddTenantId(sp => ...)` -- resolves the per-request tenant from the
  `X-Tenant-Id` HTTP header (plug in a JWT claim / subdomain / gRPC metadata
  resolver in production)
- `CreateTenantOrderCommand` + `CreateTenantOrderHandler` — the canonical
  write-side path that triggers `TenantRoutingEventStore` per operation

Example call:

```bash
curl -X POST http://localhost:5000/orders \
  -H 'Content-Type: application/json' \
  -H 'X-Tenant-Id: tenant-acme' \
  -d '{"total": 125.50}'
```

Logs show `shard-per-operation: tenant-acme -> shard-us-1 (us-east-1)`.

## Isolation models (and when to pick each)

| Model | How | Pros | Cons |
|-------|-----|------|------|
| **Database per tenant** | One connection string per tenant (or per shard) | Strongest isolation, simplest compliance & backup story, easy geo-pinning | Most infrastructure to operate; row scans across tenants are hard |
| **Schema per tenant** | Same DB, different schema names | Single DB to run, still gives per-tenant tables | Mixing tenants in one DB breaks regulatory boundaries in EU-resident scenarios |
| **Row-level (shared schema)** | Single schema, `TenantId` discriminator column | Lowest infra cost, easy analytics across tenants | Weakest isolation; every query must filter on `TenantId`; noisy-neighbor risk |

The sample uses **database per tenant** via the shard map: `shard-eu-1` and
`shard-us-1` map to separate SQL Server databases.

## Architecture

```
  HTTP request
      |
      v
  ITenantId (resolved from JWT / header / subdomain)
      |
      v
  TenantRoutingEventStore (scoped IEventStore decorator)
      |
      v
  ITenantShardMap.GetShardInfo(tenantId) --> ShardInfo
      |
      v
  ITenantStoreResolver<IEventStore> --> SqlServerEventStore (per-shard)
      |
      v
  SQL Server shard (EU or US)
```

## Configuration

Shards and tenant mappings are declared at startup in `Program.cs`:

```csharp
var shards = new Dictionary<string, ShardInfo>
{
    ["shard-eu-1"] = new(ShardId: "shard-eu-1",
        ConnectionString: "Server=...;Database=EventStore_EU;...",
        Region: "eu-west-1"),
    ["shard-us-1"] = new(ShardId: "shard-us-1",
        ConnectionString: "Server=...;Database=EventStore_US;...",
        Region: "us-east-1")
};

var tenantToShardId = new Dictionary<string, string>
{
    ["tenant-acme"]    = "shard-us-1",
    ["tenant-contoso"] = "shard-eu-1",
};
```

Production deployments typically bind this from `appsettings.json` or fetch it
from a control-plane service. See `appsettings.json` for the shape.

## Run the sample

```bash
dotnet run

# List all shards
curl http://localhost:5000/shards

# Look up a tenant's shard
curl http://localhost:5000/shards/tenant-acme
curl http://localhost:5000/shards/tenant-contoso
```

## Per-tenant projections

Projections inherit the same tenant routing. For Elasticsearch read-sides,
`Excalibur.Data.ElasticSearch` ships an `ElasticSearchTenantProjectionStoreResolver`
so each tenant's documents live in a dedicated index (e.g. `tenant-acme-orders`,
`tenant-contoso-orders`).

## Tradeoff summary

- Pick **db-per-tenant** when compliance, regionalization, or noisy-neighbor
  performance are top priorities.
- Pick **schema-per-tenant** when you want physical isolation of tables but
  want to operate one DB instance.
- Pick **row-level isolation** only for small, homogeneous SaaS tenants where
  per-tenant scale is predictable.
