# MySQL Data Provider Sample

Demonstrates all capabilities of the `Excalibur.Data.MySql` package.

## Capabilities Demonstrated

| Capability | Description |
|---|---|
| **DI Registration** | `AddExcaliburMySql(configure)` with delegate, config section, or `IConfiguration` binding |
| **Dapper Integration** | `IDataRequest<MySqlConnection, T>` with `CommandDefinition`, `DynamicParameters`, and resolver functions |
| **Connection Pooling** | `MySqlPoolingOptions` -- min/max pool size, enable/disable, clear-on-dispose |
| **SSL Configuration** | `UseSsl` option sets `MySqlSslMode.Required` on the connection |
| **Retry Policy** | Polly-based exponential backoff for transient MySQL errors (deadlock, timeout, connection lost) |
| **Health Check** | `IPersistenceProviderHealth` -- connection test, server metrics, pool statistics |
| **Transaction Support** | `IPersistenceProviderTransaction` -- `CreateTransactionScope` with isolation level and timeout |

## Prerequisites

### MySQL via Docker

```bash
docker run -d --name mysql \
  -p 3306:3306 \
  -e MYSQL_ROOT_PASSWORD=root \
  -e MYSQL_DATABASE=excalibur_sample \
  mysql:8.0
```

### MariaDB via Docker (also supported)

```bash
docker run -d --name mariadb \
  -p 3306:3306 \
  -e MARIADB_ROOT_PASSWORD=root \
  -e MARIADB_DATABASE=excalibur_sample \
  mariadb:11
```

## Running the Sample

```bash
dotnet run --project samples/09-advanced/querying/MySql
```

## Configuration

The sample shows three ways to configure the MySQL provider:

### Option A: Delegate configuration (shown in sample)

```csharp
services.AddExcaliburMySql(options =>
{
    options.ConnectionString = "Server=localhost;...";
    options.CommandTimeout = 30;
    options.Pooling.MaxPoolSize = 50;
});
```

### Option B: Configuration section name

```csharp
services.AddExcaliburMySql("MySql");
```

### Option C: IConfiguration section

```csharp
services.AddExcaliburMySql(configuration.GetSection("MySql"));
```

## Key Concepts

### IDataRequest Pattern

All database operations are encapsulated in `IDataRequest<TConnection, TResult>` implementations. Each request defines:

- **Command** -- A Dapper `CommandDefinition` with SQL and parameters
- **Parameters** -- Dapper `DynamicParameters` for safe parameterized queries
- **ResolveAsync** -- A function that executes the query via Dapper on the connection

### ISP Sub-Interfaces

The MySQL provider implements optional capabilities via the Interface Segregation Principle:

- `IPersistenceProviderHealth` -- health checks, metrics, pool stats
- `IPersistenceProviderTransaction` -- transaction scopes, transactional execution

Access them through `provider.GetService(typeof(IPersistenceProviderHealth))`.

### Transient Error Handling

The provider automatically retries these MySQL error codes with exponential backoff:

| Code | Error |
|---|---|
| 1040 | Too many connections |
| 1205 | Lock wait timeout |
| 1213 | Deadlock |
| 2002 | Cannot connect (socket) |
| 2003 | Cannot connect (TCP) |
| 2006 | Server has gone away |
| 2013 | Lost connection |
