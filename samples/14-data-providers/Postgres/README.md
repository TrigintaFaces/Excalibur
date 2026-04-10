# PostgreSQL Data Provider Sample

Demonstrates **all** Excalibur.Data.Postgres capabilities in a single console application.

## Capabilities Covered

| # | Capability | API |
|---|-----------|-----|
| 1 | **DI Registration** | `AddPostgresDataExecutors(Func<IDbConnection>)` |
| 2 | **Dapper Integration** | `AddPostgresPersistence` registers `ISqlPersistenceProvider` (Dapper-based) |
| 3 | **Dead Letter Store** | `AddPostgresDeadLetterStore(connectionString)` |
| 4 | **Connection Pooling** | `PostgresPersistenceOptions.Pooling` / `PostgresProviderOptions.Pool` |
| 5 | **JSONB Support** | `PostgresProviderOptions.Advanced.EnableJsonb` via `NpgsqlDataSourceBuilder.EnableDynamicJson()` |
| 6 | **Prepared Statements** | `PostgresPersistenceOptions.Statements` / `PostgresProviderOptions.Advanced.PrepareStatements` |
| 7 | **SSL Configuration** | `PostgresProviderOptions.Advanced.UseSsl` / `SslMode` |
| 8 | **Health Check** | Auto-registered `Postgres_persistence` health check |

## Prerequisites

**PostgreSQL 14+** running on `localhost:5432`.

### Docker (quickest)

```bash
docker run -d --name postgres \
  -p 5432:5432 \
  -e POSTGRES_PASSWORD=postgres \
  -e POSTGRES_DB=excalibur_sample \
  postgres:16
```

### Verify connectivity

```bash
docker exec -it postgres psql -U postgres -d excalibur_sample -c "SELECT version();"
```

## Running the Sample

```bash
dotnet run --project samples/14-data-providers/Postgres
```

## Configuration

Settings are loaded from `appsettings.json`:

```json
{
  "Postgres": {
    "ConnectionString": "Host=localhost;Port=5432;Database=excalibur_sample;Username=postgres;Password=postgres",
    "CommandTimeout": 30,
    "Advanced": {
      "EnableJsonb": true,
      "PrepareStatements": true
    }
  }
}
```

## Registration Patterns

The sample shows three registration approaches:

### Option A: Connection string + inline configuration (shown in sample)

```csharp
services.AddPostgresPersistence(connectionString, options =>
{
    options.Pooling.MaxPoolSize = 50;
    options.Statements.EnablePreparedStatementCaching = true;
});
```

### Option B: Builder pattern

```csharp
services.AddPostgresPersistence(pgBuilder =>
{
    pgBuilder
        .WithConnectionString(connectionString)
        .WithConnectionPooling(enabled: true, minSize: 2, maxSize: 50)
        .WithRetryPolicy(maxAttempts: 3, delayMilliseconds: 1000)
        .WithTimeouts(connectionTimeout: 15, commandTimeout: 30);
});
```

### Option C: Configuration section binding

```csharp
services.AddPostgresPersistenceFromSection("Postgres");
```

## Services Registered

| Service | Lifetime | Description |
|---------|----------|-------------|
| `IDbConnection` | Transient | Via `AddPostgresDataExecutors` factory |
| `ISqlPersistenceProvider` | Singleton | Dapper-based SQL execution with retries |
| `IPersistenceProvider` (keyed: `postgres`, `default`) | Singleton | General persistence provider |
| `ITransactionScope` | Transient | `ReadCommitted` isolation by default |
| `IDeadLetterStore` | Singleton | Failed message storage |
| `IDeadLetterStoreAdmin` | Singleton | Admin operations (stats, cleanup) |
| `IHealthCheck` | Singleton | `Postgres_persistence` health check |
| `PostgresPersistenceMetrics` | Singleton | OpenTelemetry metrics |

## Health Check Details

The auto-registered health check monitors:

- **Connectivity**: Opens a connection and executes `SELECT 1`
- **Server version**: Reports PostgreSQL version
- **Database statistics**: Size, connection counts, cache hit ratio, transaction stats
- **Blocking queries**: Detects queries waiting on locks
- **Performance thresholds**: Connection > 5s or query > 1s triggers `Degraded` status
