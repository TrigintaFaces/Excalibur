# ElasticSearch -- Getting Started

Demonstrates DI registration, repository creation with index initialization, and basic CRUD operations.

## Prerequisites

Elasticsearch 8.x running locally:

```bash
docker run -d --name elasticsearch -p 9200:9200 \
  -e "discovery.type=single-node" \
  -e "xpack.security.enabled=false" \
  elasticsearch:8.15.0
```

## What This Shows

| Operation | Method | Description |
|-----------|--------|-------------|
| Add | `AddOrUpdateAsync` | Index a new document |
| Get | `GetByIdAsync` | Retrieve by document ID |
| Update | `AddOrUpdateAsync` | Full document replace |
| Partial Update | `UpdateAsync` | Update specific fields only |
| Bulk Upsert | `BulkAddOrUpdateAsync` | Batch index multiple documents |
| Delete | `RemoveAsync` | Remove a document |

## Key Concepts

- **`ElasticRepositoryBase<T>`** -- Abstract base class providing CRUD operations
- **`IInitializeElasticIndex`** -- Implemented by repositories to create indexes at startup
- **`AddRepository<TInterface, TImpl>()`** -- Registers scoped repository + singleton initializer
- **`InitializeElasticsearchIndexesAsync()`** -- Host extension to run all index initializers

## Run

```bash
dotnet run
```
