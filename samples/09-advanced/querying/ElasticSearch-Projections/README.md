# ElasticSearch Projections Sample

Demonstrates using ElasticSearch as a **projection store** for CQRS read models via `IProjectionStore<T>` from the Excalibur framework.

## What This Sample Shows

1. **Projection Store Pattern** - Using `IProjectionStore<T>` for read model persistence
2. **Named Options** - Each projection type gets its own `ElasticSearchProjectionStoreOptions` keyed by `typeof(T).Name`, allowing independent index configuration
3. **CRUD Operations** - Full lifecycle: Upsert, GetById, Query, Count, Delete
4. **Dictionary-Based Filtering** - Query projections using property-name filters (e.g., `{ "Status", "Shipped" }`)
5. **Pagination & Sorting** - Use `QueryOptions` to control result ordering and paging

## Prerequisites

Elasticsearch running locally:

```bash
docker run -d --name es -p 9200:9200 \
  -e "discovery.type=single-node" \
  -e "xpack.security.enabled=false" \
  elasticsearch:8.15.0
```

## How It Works

### Registration

```csharp
// Register base ES services
builder.Services.AddElasticsearchServices(builder.Configuration, registry: null);

// Register projection infrastructure
builder.Services.AddElasticsearchProjections(builder.Configuration);

// Register per-type projection stores with named options
builder.Services.AddElasticSearchProjectionStore<OrderSummary>(options =>
{
    options.IndexName = "order-summaries";
});
builder.Services.AddElasticSearchProjectionStore<CustomerDashboard>(options =>
{
    options.IndexName = "customer-dashboards";
});
```

### Usage

Resolve `IProjectionStore<T>` from DI and use its five methods:

| Method | Purpose |
|--------|---------|
| `UpsertAsync(id, projection, ct)` | Create or replace a projection |
| `GetByIdAsync(id, ct)` | Retrieve a projection by ID |
| `QueryAsync(filters, options, ct)` | Query with dictionary filters and pagination |
| `CountAsync(filters, ct)` | Count matching projections |
| `DeleteAsync(id, ct)` | Remove a projection (idempotent) |

### Filter Operators

Filters use property names as keys with optional operator suffixes:

| Filter | Meaning |
|--------|---------|
| `["Status"] = "Active"` | Equality (default) |
| `["Amount:gt"] = 100` | Greater than |
| `["Amount:gte"] = 100` | Greater than or equal |
| `["Amount:lt"] = 1000` | Less than |
| `["Status:neq"] = "Deleted"` | Not equals |
| `["Tags:in"] = new[] { "A", "B" }` | In collection |
| `["Name:contains"] = "test"` | String contains |

## CQRS Context

In a full CQRS system, projections are built by processing domain events:

```
Domain Events --> Projection Handler --> IProjectionStore<T>.UpsertAsync()
                                              |
                                    ElasticSearch Index
                                              |
                       Query API <-- IProjectionStore<T>.QueryAsync()
```

This sample focuses on the **storage side** of projections. For event-driven projection building, see the Event Sourcing samples.

## Running

```bash
dotnet run --project samples/07-data-providers/ElasticSearch-Projections
```
