# Azure Cosmos DB Sample

Demonstrates selected Excalibur CosmosDB data provider operations using `Excalibur.Data.CosmosDb`.

## Capabilities Demonstrated

| # | Capability | API |
|---|-----------|-----|
| 1 | DI Registration | `AddExcaliburCosmosDb(Action<ICosmosDbDataBuilder>)` with `ClientFactory` |
| 2 | Connection Test | `TestConnectionAsync(CancellationToken)` |
| 3 | Create Document | `CreateAsync<T>(document, partitionKey, ct)` |
| 4 | Read by ID | `GetByIdAsync<T>(id, partitionKey, consistencyOptions, ct)` |
| 5 | Query Documents | `QueryAsync<T>(CloudQueryRequest, ct)` |
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

The [Microsoft Linux emulator guide](https://learn.microsoft.com/en-us/azure/cosmos-db/emulator-linux) states that vNext supports gateway mode only. Keep `UseDirectMode` false for this emulator; HTTPS alone does not enable direct mode.

The gateway answers before the emulator will actually serve a request, so wait for the readiness probe
before starting the sample:

```bash
curl http://localhost:8080/ready
```

### Trust the emulator's issuer chain

The .NET process must trust the issuer/root CA that signed the emulator's HTTPS certificate and
connect with a matching hostname. Trusting only the presented `CN=localhost` leaf is not sufficient:
a local SDK run reported `PartialChain` even while curl succeeded. In the pinned owned vNext image,
the actual issuer root was `/scripts/certs/rootCA.crt`; verify the issuer and fingerprint for the
image you run rather than assuming every emulator exposes the same file. Install that development
CA only in the dedicated test runtime/container's trust store, and verify the presented chain there.
Do not disable certificate validation or copy an unrelated root. The sample adds no TLS bypass or
host trust-store changes. These are local development prerequisites, not production certificate
management instructions; follow the emulator's documented certificate procedure for your version.

### Create the Database

The sample does not provision databases or containers: initialization reads the configured database and selects an existing container. Create the configured database and container before running. Open the Data Explorer at `http://localhost:1234` and:

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
      "UseDirectMode": false
    },
    "DatabaseName": "YourDatabase",
    "DefaultContainerName": "YourContainer"
  }
}
```

The public `ClientFactory` creates the host-owned SDK client with the sample-generated System.Text.Json document metadata and maps the displayed mode, region, rate-limit retry, timeout, bulk and connection settings onto that actual client. The provider borrows it. This sample-local mapping is not a claim that every framework client-construction path maps all provider options.

The sample reads the endpoint/key/database/container and `CosmosDb:Client:UseDirectMode` settings above from the standard Host
configuration providers, including environment variables and command-line configuration arguments.
The sample keeps its emulator defaults when those settings are absent; gateway mode is the default. Cloud deployments may explicitly set `UseDirectMode` to `true`. This executable does not
consume the alternative `ConnectionString` setting; use the shown endpoint/key settings.

## Running

```bash
dotnet run --project samples/09-advanced/querying/CosmosDb/CosmosDb.csproj
```

This querying sample always performs real operations; it has no illustrative fallback or mode parser.
For a nonpersistent illustration, see the [event-store sample](../../persistence-patterns/CosmosDbEventStore/README.md).
A failed connection, false CRUD/batch outcome, failed health check or mismatched readback exits nonzero.
The final success line is emitted only after actual create/read/query/delete/batch assertions and cleanup.
Each invocation uses unique document IDs in the `electronics` partition and deletes only those IDs;
use a dedicated test container with partition key `/category`. Cleanup failures fail a healthy run or
are attached to its original failure. Cancellation and infrastructure exceptions remain failures.

The local managed-process rejection control proves only failure signaling. Successful real Cosmos
persistence and the separate event-store sample's business guards require an actual supported service
run; neither console text nor this sample claims NativeAOT or whole-release certification.

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
