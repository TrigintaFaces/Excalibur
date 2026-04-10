# Redis Data Provider Sample

Demonstrates all capabilities of the `Excalibur.Data.Redis` package.

## Capabilities Shown

| # | Capability | Description |
|---|-----------|-------------|
| 1 | **DI Registration** | `AddRedisProvider(configure)` with inline options and `ValidateOnStart` |
| 2 | **Connection Pool** | `RedisConnectionPoolOptions` -- timeouts, retries, abort behavior |
| 3 | **Database Selection** | `DatabaseId` option for multi-database Redis setups |
| 4 | **CRUD Operations** | Strings, hashes, lists, sets, counters, TTL via `IDatabase` |
| 5 | **Transaction Support** | `CreateTransactionScope` with commit/rollback callbacks |
| 6 | **Health Check** | `TestConnectionAsync` with built-in retry policy |
| 7 | **Metrics** | Provider metrics and connection pool statistics |
| 8 | **Pub/Sub** | Publish and subscribe via `GetSubscriber()` |
| 9 | **Retry Policy** | Exponential backoff for `RedisException`, `RedisTimeoutException` |
| 10 | **Server Access** | `GetServer()` for server info and administration |

## Prerequisites

Redis running on `localhost:6379`:

```bash
docker run -d --name redis -p 6379:6379 redis:7-alpine
```

## Run

```bash
dotnet run --project samples/14-data-providers/Redis/Redis.csproj
```

## Configuration

Options can be set via code or `appsettings.json`:

```json
{
  "Redis": {
    "ConnectionString": "localhost:6379",
    "DatabaseId": 0
  }
}
```

### RedisProviderOptions

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `ConnectionString` | `string` | (required) | Redis connection string |
| `DatabaseId` | `int` | `0` | Redis database index |
| `Password` | `string?` | `null` | Password (if not in connection string) |
| `UseSsl` | `bool` | `false` | Enable SSL/TLS |
| `AllowAdmin` | `bool` | `false` | Allow admin commands (INFO, FLUSHDB) |
| `IsReadOnly` | `bool` | `false` | Mark provider as read-only |

### RedisConnectionPoolOptions

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `ConnectTimeout` | `int` | `10` | Connection timeout (seconds) |
| `SyncTimeout` | `int` | `5` | Sync operation timeout (seconds) |
| `AsyncTimeout` | `int` | `5` | Async operation timeout (seconds) |
| `ConnectRetry` | `int` | `3` | Reconnect attempts on connection loss |
| `AbortOnConnectFail` | `bool` | `false` | Abort if initial connection fails |
| `RetryCount` | `int` | `3` | Operation-level retry count |

## Architecture

`RedisPersistenceProvider` implements three interfaces following the ISP pattern:

- **`IPersistenceProvider`** -- core provider (name, type, execute, initialize, GetService)
- **`IPersistenceProviderHealth`** -- health checks, metrics, pool stats (via `GetService`)
- **`IPersistenceProviderTransaction`** -- transactions, retry policy (via `GetService`)

Access optional capabilities through the `GetService(Type)` escape hatch:

```csharp
var health = (IPersistenceProviderHealth?)provider.GetService(typeof(IPersistenceProviderHealth));
var tx = (IPersistenceProviderTransaction?)provider.GetService(typeof(IPersistenceProviderTransaction));
```
