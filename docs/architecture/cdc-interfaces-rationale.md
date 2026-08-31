# CDC Interfaces: Two-Tier ISP Hierarchy

## Architecture (Sprint 820 — ADR-327)

CDC interfaces follow a two-tier hierarchy based on the Interface Segregation Principle (ISP):

### Base Interfaces (`Excalibur.Cdc`)

```csharp
// Tier 1: Poll-based batch processing (1 method)
public interface ICdcProcessor<TEvent> : IAsyncDisposable, IDisposable
{
    Task<int> ProcessBatchAsync(
        Func<TEvent, CancellationToken, Task> eventHandler,
        CancellationToken cancellationToken);
}

// Tier 2: Streaming with position tracking (3 methods, extends Tier 1)
public interface ICdcStreamProcessor<TEvent, TPosition> : ICdcProcessor<TEvent>
{
    Task StartAsync(Func<TEvent, CancellationToken, Task> eventHandler, CancellationToken cancellationToken);
    Task<TPosition> GetCurrentPositionAsync(CancellationToken cancellationToken);
    Task ConfirmPositionAsync(TPosition position, CancellationToken cancellationToken);
}
```

### Provider Marker Interfaces

Each provider has a marker interface with zero additional methods:

| Provider | Marker Interface | Base | Position Type |
|----------|-----------------|------|---------------|
| SQL Server | `ISqlServerCdcProcessor` | `ICdcProcessor<DataChangeEvent>` | N/A (batch-only) |
| InMemory | `IInMemoryCdcProcessor` | `ICdcProcessor<InMemoryCdcChange>` | N/A (batch-only) |
| Postgres | `IPostgresCdcProcessor` | `ICdcStreamProcessor<T, ulong>` | WAL position |
| MongoDB | `IMongoDbCdcProcessor` | `ICdcStreamProcessor<T, BsonDocument>` | Resume token |
| CosmosDB | `ICosmosDbCdcProcessor` | `ICdcStreamProcessor<T, string>` | Continuation token |
| DynamoDB | `IDynamoDbCdcProcessor` | `ICdcStreamProcessor<T, string>` | Sequence number |
| Firestore | `IFirestoreCdcProcessor` | `ICdcStreamProcessor<T, Timestamp>` | Document snapshot |

### Why Two Tiers

- **Compile-time safety:** Injecting a poll-only processor (SqlServer/InMemory) where streaming is required fails at compile time instead of runtime `NotSupportedException`
- **Generic consumption:** Consumers can write code against `ICdcProcessor<T>` to handle any provider, or `ICdcStreamProcessor<T,TPos>` for streaming-specific features
- **ISP compliance:** ICdcProcessor has 1 method, ICdcStreamProcessor has 3 — both under the 5-method limit

## Shared Infrastructure

### Shared Base Types (`Excalibur.Cdc`)

- `CdcChangeType` — unified enum: None, Insert, Update, Delete, Replace, Truncate, Invalidate, Drop, DropDatabase, Rename
- `CdcStalePositionException` — the stale-checkpoint exception every provider raises. SQL Server subclasses it (`SqlServerCdcStalePositionException`) to carry the capture instance; the other providers raise the base type directly.
- `CdcDataChange` — column-level change record with OldValue/NewValue

### State Store Interface

`ICdcStateStore` is provider-specific because checkpoint data differs (LSN vs token vs position).
Each provider registers its own `Add{Provider}CdcStateStore()` DI extension.

## SQL Server CdcProcessor Decomposition

The monolithic `CdcProcessor` was decomposed into focused collaborators:

- **`CdcChangeDetector`** — Queries CDC tables for new changes within an LSN range
- **`CdcChangeApplier`** — Applies detected changes to registered event handlers
- **`CdcCheckpointManager`** — Persists processing position (LSN checkpoints)
- **`CdcRepository`** — Wraps the CDC `SqlConnection` and owns disposal

All database-touching collaborators accept `IDataAccessPolicyFactory` for automatic retry and circuit breaker wrapping when `Excalibur.Data.SqlServer` is registered. See `docs/patterns/cdc-patterns.md` for details.

## DI Forwarding (Sprint 821 — ADR-328)

Each provider's DI extension registers forwarding so consumers can resolve any level of the hierarchy:

```csharp
// Streaming providers register all three levels:
services.TryAddSingleton<ICosmosDbCdcProcessor, CosmosDbCdcProcessor>();      // marker
services.TryAddSingleton<ICdcStreamProcessor<T, TPos>>(
    sp => sp.GetRequiredService<ICosmosDbCdcProcessor>());                     // streaming base
services.TryAddSingleton<ICdcProcessor<T>>(
    sp => sp.GetRequiredService<ICosmosDbCdcProcessor>());                     // poll base

// Poll-only providers register two levels:
services.TryAddSingleton<ISqlServerCdcProcessor, CdcProcessor>();             // marker
services.TryAddSingleton<ICdcProcessor<DataChangeEvent>>(
    sp => sp.GetRequiredService<ISqlServerCdcProcessor>());                    // poll base
```

This enables consumers to depend on the abstraction level they need:
- **`ICdcProcessor<T>`** — works with any CDC provider
- **`ICdcStreamProcessor<T, TPos>`** — only streaming providers
- **`ICosmosDbCdcProcessor`** — specific provider (compile-time type safety)

## History

- **Pre-S820:** Each provider had independent, incompatible interfaces with duplicated method declarations
- **Sprint 820 (ADR-327):** Unified into two-tier ISP hierarchy, ~200 lines of duplicated interface code removed
- **Sprint 821 (ADR-328):** DI forwarding added for all 7 providers — consumers can now resolve base interfaces
- **Method renames:** `ProcessCdcChangesAsync` (SqlServer) and `ProcessChangesAsync` (InMemory) unified to `ProcessBatchAsync`
