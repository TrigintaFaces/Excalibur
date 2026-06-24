# Azure Cosmos DB Sample

Demonstrates all Excalibur CosmosDB data provider capabilities using `Excalibur.Data.CosmosDb`.

## Capabilities Demonstrated

| # | Capability | API |
|---|-----------|-----|
| 1 | DI Registration | `AddCosmosDb(Action<CosmosDbOptions>)` |
| 2 | Connection Test | `TestConnectionAsync(CancellationToken)` |
| 3 | Create Document | `CreateAsync<T>(document, partitionKey, ct)` |
| 4 | Read by ID | `GetByIdAsync<T>(id, partitionKey, consistencyOptions, ct)` |
| 5 | Query Documents | `QueryAsync<T>(queryText, partitionKey, parameters, consistencyOptions, ct)` |
| 6 | Delete Document | `DeleteAsync(id, partitionKey, etag, ct)` |
| 7 | Transactional Batch | `ExecuteBatchAsync(partitionKey, operations, ct)` |
| 8 | Collection Info | `GetCollectionInfoAsync(collectionName, ct)` |
| 9 | Store Statistics | `GetDocumentStoreStatisticsAsync(ct)` |
| 10 | Health Check | `AddCosmosDb()` on `IHealthChecksBuilder` |
| 11 | Multi-Region Config | `CosmosDbClientOptions.PreferredRegions`, `UseDirectMode` |
| 12 | Provider Capabilities | `GetService(Type)`, `GetSupportedOperationTypes()` |

## Prerequisites

### Azure Cosmos DB Emulator

**Option A -- Windows Emulator (native):**

Download and install from [Azure Cosmos DB Emulator](https://learn.microsoft.com/en-us/azure/cosmos-db/local-emulator).

Default endpoint: `https://localhost:8081`

**Option B -- Docker (Linux/macOS/Windows):**

```bash
docker run -d --name cosmosdb \
  -p 8081:8081 \
  -p 10250-10255:10250-10255 \
  mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator:latest
```

### Create the Database

The emulator does not auto-create databases. Use the Data Explorer at `https://localhost:8081/_explorer/index.html` or the Azure CLI:

```bash
# Using the emulator Data Explorer UI:
# 1. Navigate to https://localhost:8081/_explorer/index.html
# 2. Create database: ExcaliburSample
# 3. Create container: Items (partition key: /category)
```

## Configuration

The sample uses the Cosmos DB Emulator well-known key. For production, configure via `appsettings.json`:

```json
{
  "CosmosDb": {
    "Client": {
      "AccountEndpoint": "https://your-account.documents.azure.com:443/",
      "AccountKey": "your-account-key",
      "PreferredRegions": ["West US", "East US"],
      "UseDirectMode": true
    },
    "DatabaseName": "YourDatabase",
    "DefaultContainerName": "YourContainer"
  }
}
```

Alternatively, use a connection string:

```json
{
  "CosmosDb": {
    "Client": {
      "ConnectionString": "AccountEndpoint=https://...;AccountKey=..."
    },
    "DatabaseName": "YourDatabase",
    "DefaultContainerName": "YourContainer"
  }
}
```

## Running

```bash
dotnet run --project samples/09-advanced/querying/CosmosDb/CosmosDb.csproj
```

## Key Types

| Type | Purpose |
|------|---------|
| `CosmosDbPersistenceProvider` | Main provider -- CRUD, query, batch, change feed |
| `CosmosDbOptions` | Database, container, and connection configuration |
| `CosmosDbClientOptions` | Client-level settings: regions, connection mode, resilience |
| `CosmosDbHealthCheck` | `IHealthCheck` implementation for Cosmos DB connectivity |
| `PartitionKey` | Simple string-based partition key record |
| `CloudBatchCreateOperation` | Batch create operation |
| `CloudBatchDeleteOperation` | Batch delete operation |
| `CloudOperationResult<T>` | Result of a CRUD operation with RU charge and ETag |
| `CloudQueryResult<T>` | Result of a query with documents, RU charge, and continuation token |
| `CloudBatchResult` | Result of a transactional batch execution |
