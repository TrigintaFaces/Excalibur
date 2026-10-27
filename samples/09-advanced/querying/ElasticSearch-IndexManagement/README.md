# ElasticSearch Index Management Sample

Demonstrates Elasticsearch index management capabilities provided by `Excalibur.Data.ElasticSearch`.

## Prerequisites

- Elasticsearch 8.x running locally:

```bash
docker run -d --name es -p 9200:9200 \
  -e "discovery.type=single-node" \
  -e "xpack.security.enabled=false" \
  elasticsearch:8.15.0
```

## What This Sample Covers

| Capability | Interface | Methods Demonstrated |
|---|---|---|
| **Index Operations** | `IIndexOperationsManager` | `CreateIndexAsync`, `IndexExistsAsync`, `GetIndexHealthAsync`, `UpdateIndexSettingsAsync`, `DeleteIndexAsync` |
| **Index Templates** | `IIndexTemplateManager` | `CreateOrUpdateTemplateAsync`, `TemplateExistsAsync`, `ValidateTemplateAsync`, `GetTemplatesAsync`, `DeleteTemplateAsync` |
| **ILM Policies** | `IIndexLifecycleManager` | `CreateLifecyclePolicyAsync`, `GetIndexLifecycleStatusAsync`, `DeleteLifecyclePolicyAsync` |
| **Alias Management** | `IIndexAliasManager` | `CreateAliasAsync`, `AliasExistsAsync`, `GetAliasesAsync`, `UpdateAliasesAsync`, `DeleteAliasAsync` |

## When to Use Each Capability

| Scenario | Capability |
|---|---|
| Create indices with specific shard/replica settings | Index Operations |
| Monitor cluster and index health | Index Operations (`GetIndexHealthAsync`) |
| Dynamically adjust replicas or refresh intervals | Index Operations (`UpdateIndexSettingsAsync`) |
| Ensure new indices follow a consistent schema | Index Templates |
| Validate template configuration before applying | Index Templates (`ValidateTemplateAsync`) |
| Automate index rollover based on size, age, or doc count | ILM Policies (hot phase rollover) |
| Tier data across hot/warm/cold/delete phases | ILM Policies |
| Zero-downtime index swaps (reindex, schema migration) | Alias Management (atomic `UpdateAliasesAsync`) |
| Route reads to one index and writes to another | Alias Management with write index flag |

## Running

```bash
dotnet run --project samples/09-advanced/querying/ElasticSearch-IndexManagement
```

## DI Registration

```csharp
// Register the Elasticsearch client
builder.Services.AddElasticsearchServices(builder.Configuration, registry: null);

// Register all four index management services
builder.Services.AddElasticsearchIndexManagement(builder.Configuration);
```

This registers:
- `IIndexOperationsManager` -- index CRUD and health
- `IIndexTemplateManager` -- composable index templates
- `IIndexLifecycleManager` -- ILM policies and rollover
- `IIndexAliasManager` -- alias CRUD and atomic swaps
