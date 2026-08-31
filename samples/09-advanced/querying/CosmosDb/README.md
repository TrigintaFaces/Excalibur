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
  -p 1234:1234 \
  -p 8080:8080 \
  mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator:vnext-EN20260706 \
  --protocol https
```

Three details in that command are load-bearing:

- **Name a version.** An unversioned tag such as `latest` can resolve to a different image later. One
  such image becomes ready and answers its readiness probe, then fails on first use because it cannot
  create a database. The version above is the one the package README and this project's own container
  fixtures use.
- **`--protocol https` is required for .NET.** This emulator starts in HTTP mode by default, and the
  .NET SDK does not support HTTP mode against it. Without the flag the sample cannot reach
  `https://localhost:8081` however it is configured.
- **Three ports, and none of them is a range.** `8081` is the gateway endpoint, `1234` is the Data
  Explorer, `8080` serves the health probes. The `10250-10255` range belongs to the legacy emulator's
  direct mode; this emulator runs in gateway mode only, so publishing that range does nothing.

The gateway answers before the emulator will actually serve a request, so wait for the readiness probe
before starting the sample:

```bash
curl http://localhost:8080/ready
```

### Create the Database

The emulator does not auto-create databases. Open the Data Explorer at `http://localhost:1234` and:

```text
1. Create database: ExcaliburSample
2. Create container: Items (partition key: /category)
```

The Data Explorer takes a few seconds longer to become available than the gateway does.


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
