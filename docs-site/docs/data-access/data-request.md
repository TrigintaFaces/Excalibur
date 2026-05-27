---
sidebar_position: 2
title: IDataRequest
description: Encapsulated, testable data access with Dapper and automatic connection lifecycle
---

# IDataRequest

`IDataRequest` is Excalibur's abstraction for encapsulated, reusable database operations using Dapper. Each request class packages its SQL, parameters, and execution logic into a single, testable unit.

## Before You Start

- **.NET 10.0**
- Install the required packages:
  ```bash
  dotnet add package Excalibur.Data.Abstractions   # IDataRequest, DataRequest<T>, IDataRequestResolver<T>
  dotnet add package Excalibur.Data.SqlServer       # SqlDataRequestResolver, SqlDb
  ```
- Familiarity with [Dapper](https://github.com/DapperLib/Dapper) and SQL

## When to Use IDataRequest

| Approach | Best For |
|----------|----------|
| **Direct Dapper** via `IDb` | Simple, one-off queries; ad-hoc access |
| **IDataRequest** | Structured queries you want to reuse, test, and correlate |
| **IDataRequestResolver** | Single-query scenarios without `IDb` (serverless, CQRS reads) |

Use `IDataRequest` when you want encapsulation, testability, retry policies, or correlation tracking. For simple ad-hoc queries, use [IDb](./idb-interface.md) with Dapper directly.

## API Reference

### IDataRequest (Non-Generic Base)

Every data request carries metadata for tracing and diagnostics:

| Property | Type | Description |
|----------|------|-------------|
| `RequestId` | `string` | Auto-generated unique identifier |
| `RequestType` | `string` | Type name (e.g., `"GetOrderByIdQuery"`) |
| `CreatedAt` | `DateTimeOffset` | Timestamp when the request was created |
| `CorrelationId` | `string?` | Optional correlation ID for distributed tracing |
| `Metadata` | `IDictionary<string, object>?` | Additional context data |

### IDataRequest&lt;TConnection, TModel&gt;

The generic interface adds the Dapper execution contract:

```csharp
public interface IDataRequest<in TConnection, TModel> : IDataRequest
{
    CommandDefinition Command { get; }
    DynamicParameters Parameters { get; }
    Func<TConnection, Task<TModel>> ResolveAsync { get; }
}
```

- **`Command`** — Dapper `CommandDefinition` (SQL text, parameters, timeout, cancellation)
- **`Parameters`** — Dapper `DynamicParameters` for parameterized queries
- **`ResolveAsync`** — A delegate that executes the query against the provided connection

### DataRequest&lt;TModel&gt; Base Class

The recommended way to implement data requests. Extends `DataRequestBase<IDbConnection, TModel>` and provides the `CreateCommand` helper:

```csharp
public abstract class DataRequest<TModel> : DataRequestBase<IDbConnection, TModel>;
```

`CreateCommand` builds a `CommandDefinition` with proper defaults:

```csharp
protected CommandDefinition CreateCommand(
    string commandText,
    DynamicParameters? parameters = null,
    IDbTransaction? transaction = null,
    int? commandTimeout = null,
    CommandType? commandType = null,
    CancellationToken cancellationToken = default)
```

## Writing Data Requests

### Query (Read)

```csharp
using Dapper;
using Excalibur.Data.Abstractions;

public class GetOrderByIdQuery : DataRequest<Order?>
{
    public GetOrderByIdQuery(Guid orderId, CancellationToken cancellationToken)
    {
        Command = CreateCommand(
            "SELECT Id, CustomerId, Status, CreatedAt FROM Orders WHERE Id = @OrderId",
            parameters: new DynamicParameters(new { OrderId = orderId }),
            cancellationToken: cancellationToken);

        ResolveAsync = connection =>
            connection.QuerySingleOrDefaultAsync<Order>(Command);
    }
}
```

### Command (Write)

```csharp
public class CreateOrderCommand : DataRequest<int>
{
    public CreateOrderCommand(Order order, CancellationToken cancellationToken)
    {
        Command = CreateCommand(
            """
            INSERT INTO Orders (Id, CustomerId, Status, CreatedAt)
            VALUES (@Id, @CustomerId, @Status, @CreatedAt)
            """,
            parameters: new DynamicParameters(new
            {
                order.Id, order.CustomerId, order.Status, order.CreatedAt
            }),
            cancellationToken: cancellationToken);

        ResolveAsync = connection =>
            connection.ExecuteAsync(Command);
    }
}
```

### Query with Multiple Parameters

```csharp
public class GetOrdersByStatusQuery : DataRequest<IEnumerable<Order>>
{
    public GetOrdersByStatusQuery(
        OrderStatus status,
        DateTimeOffset fromDate,
        int limit,
        CancellationToken cancellationToken)
    {
        Command = CreateCommand(
            """
            SELECT TOP(@Limit) Id, CustomerId, Status, CreatedAt
            FROM Orders
            WHERE Status = @Status AND CreatedAt >= @FromDate
            ORDER BY CreatedAt DESC
            """,
            parameters: new DynamicParameters(new
            {
                Status = status, FromDate = fromDate, Limit = limit
            }),
            cancellationToken: cancellationToken);

        ResolveAsync = connection =>
            connection.QueryAsync<Order>(Command);
    }
}
```

### Stored Procedure

```csharp
public class CalculateOrderTotalQuery : DataRequest<decimal>
{
    public CalculateOrderTotalQuery(Guid orderId, CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters(new { OrderId = orderId });
        parameters.Add("@Total", dbType: DbType.Decimal, direction: ParameterDirection.Output);

        Command = CreateCommand(
            "sp_CalculateOrderTotal",
            parameters,
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken);

        ResolveAsync = async connection =>
        {
            await connection.ExecuteAsync(Command).ConfigureAwait(false);
            return parameters.Get<decimal>("@Total");
        };
    }
}
```

### With Correlation and Metadata

```csharp
var query = new GetOrderByIdQuery(orderId, ct)
{
    CorrelationId = HttpContext.TraceIdentifier,
    Metadata = new Dictionary<string, object>
    {
        ["UserId"] = currentUser.Id,
        ["Source"] = "WebApi"
    }
};
```

## Executing Data Requests

There are three ways to execute a data request, depending on your scenario.

### 1. Via IDb (Connection You Manage)

Use `IDb.Connection.ResolveAsync()` when you already have a connection from DI — typically in repositories or services with transactional needs:

```csharp
public class OrderRepository
{
    private readonly IDb _db;

    public OrderRepository(IDb db) => _db = db;

    public async Task<Order?> GetByIdAsync(Guid orderId, CancellationToken ct)
    {
        var query = new GetOrderByIdQuery(orderId, ct);
        return await _db.Connection.ResolveAsync(query);
    }
}
```

The `ResolveAsync` extension method on `IDbConnection`:
- Opens the connection if needed (via `Ready()`)
- Wraps database exceptions in `OperationFailedException`
- Preserves the original exception as `InnerException`

### 2. Via IDataRequestResolver (Connection-Per-Call)

Use `IDataRequestResolver<TConnection>` for simple, focused scenarios (CQRS read-side, serverless, ad-hoc reporting) where you want automatic connection lifecycle without managing `IDb`:

```csharp
public interface IDataRequestResolver<TConnection>
{
    Task<TModel> QueryAsync<TModel>(
        IDataRequest<TConnection, TModel> request,
        CancellationToken cancellationToken);

    Task ExecuteAsync(
        IDataRequest<TConnection, int> request,
        CancellationToken cancellationToken);
}
```

Method naming follows the Dapper convention:
- **`QueryAsync`** — returns a result (SELECT, scalar)
- **`ExecuteAsync`** — performs a side effect (INSERT, UPDATE, DELETE)

The resolver creates, opens, and disposes a connection per call:

```
QueryAsync(request, ct)
  1. new SqlConnection(...)         // Create
  2. connection.OpenAsync(ct)       // Open
  3. request.ResolveAsync(conn)     // Execute
  4. connection.DisposeAsync()      // Dispose (returns to ADO.NET pool)
```

### 3. Manual Execution (Advanced)

For full control, invoke the `ResolveAsync` delegate directly:

```csharp
public async Task<IEnumerable<Order>> ExecuteManuallyAsync(
    GetOrdersByStatusQuery request,
    CancellationToken ct)
{
    _db.Open();
    try
    {
        return await request.ResolveAsync(_db.Connection);
    }
    finally
    {
        _db.Close();
    }
}
```

## SqlDataRequestResolver

`Excalibur.Data.SqlServer` provides `SqlDataRequestResolver`, the SQL Server implementation of `IDataRequestResolver<SqlConnection>`.

### Registration

```csharp
using Microsoft.Extensions.DependencyInjection;

// Option 1: Connection string
services.AddSqlDataRequestResolver("Server=.;Database=MyDb;Trusted_Connection=true");

// Option 2: Connection factory (for advanced scenarios like Azure token-based auth)
services.AddSqlDataRequestResolver(() => new SqlConnection(connectionString));
```

Both overloads register a singleton `IDataRequestResolver<SqlConnection>`.

:::note TryAdd Semantics

All `AddSqlDataRequestResolver` overloads use `TryAdd*`, so the first registration wins. This lets host applications override the resolver in test or staging environments.
:::

### Keyed Registration (Multiple Databases)

When your application connects to more than one SQL Server database, use keyed registrations:

```csharp
services.AddSqlDataRequestResolver("reporting", reportingConnectionString);
services.AddSqlDataRequestResolver("operational", operationalConnectionString);
```

Inject with `[FromKeyedServices]`:

```csharp
public class ReportingQueryService
{
    private readonly IDataRequestResolver<SqlConnection> _resolver;

    public ReportingQueryService(
        [FromKeyedServices("reporting")] IDataRequestResolver<SqlConnection> resolver)
        => _resolver = resolver;
}
```

### Usage

```csharp
public class OrderQueryService
{
    private readonly IDataRequestResolver<SqlConnection> _resolver;

    public OrderQueryService(IDataRequestResolver<SqlConnection> resolver)
        => _resolver = resolver;

    public Task<Order?> GetOrderAsync(Guid orderId, CancellationToken ct)
    {
        var request = new GetOrderByIdQuery(orderId, ct);
        return _resolver.QueryAsync(request, ct);
    }

    public Task CreateOrderAsync(Order order, CancellationToken ct)
    {
        var request = new CreateOrderCommand(order, ct);
        return _resolver.ExecuteAsync(request, ct);
    }
}
```

### Serverless Example (Azure Functions)

`IDataRequestResolver` is ideal for serverless scenarios where each invocation is short-lived:

```csharp
public class GetOrderFunction
{
    private readonly IDataRequestResolver<SqlConnection> _resolver;

    public GetOrderFunction(IDataRequestResolver<SqlConnection> resolver)
        => _resolver = resolver;

    [Function("GetOrder")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get")] HttpRequestData req,
        Guid orderId,
        CancellationToken ct)
    {
        var request = new GetOrderByIdQuery(orderId, ct);
        var order = await _resolver.QueryAsync(request, ct);

        var response = req.CreateResponse();
        await response.WriteAsJsonAsync(order, ct);
        return response;
    }
}
```

## Choosing an Execution Strategy

| Scenario | Use | Why |
|----------|-----|-----|
| Single query, no transaction | `IDataRequestResolver` | Connection-per-call, zero ceremony |
| CQRS read-side queries | `IDataRequestResolver` | Lightweight, no shared state |
| Serverless / Azure Functions | `IDataRequestResolver` | Connection-per-call fits invocation model |
| Multiple queries in a transaction | `IDb` + `IUnitOfWork` | Shared connection + transaction |
| Full persistence with event sourcing | `IPersistenceProvider` | Manages aggregates and events |
| Long-lived connection with pooling | `IDb` | Scoped connection lifecycle |

## Timeout Constants

Use predefined timeout constants for consistency across data requests:

```csharp
public static class DbTimeouts
{
    public const int RegularTimeoutSeconds = 60;             // Standard operations
    public const int LongRunningTimeoutSeconds = 600;        // Batch operations (10 min)
    public const int ExtraLongRunningTimeoutSeconds = 1200;  // Heavy processing (20 min)
}
```

Pass timeouts via `CreateCommand`:

```csharp
Command = CreateCommand(
    commandText,
    parameters: new DynamicParameters(new { ... }),
    commandTimeout: DbTimeouts.RegularTimeoutSeconds,
    cancellationToken: cancellationToken);
```

## Best Practices

- **Parameterize everything** — Always use `DynamicParameters` or anonymous objects. Never concatenate user input into SQL strings.
- **Keep requests immutable** — Set all properties in the constructor. Create a new instance per execution.
- **Use specific return types** — Prefer `Order?` over `dynamic` for type safety.
- **Pass CancellationToken** — Both execution paths accept a `CancellationToken`. Always propagate from the caller.
- **One query per resolver call** — `IDataRequestResolver` is designed for single-operation use. For multi-statement transactions, use `IDb` + `IUnitOfWork`.
- **Use `CommandDefinition`** — Always wrap SQL in `CommandDefinition` for proper cancellation support. Avoid the plain string overloads.

## Related Documentation

- [IDb Interface](./idb-interface.md) — Database connection abstraction with direct Dapper usage and transactions
- [Data Access Overview](./index.md) — Repository patterns and data access strategy
- [SQL Server Provider](../data-providers/sqlserver.md) — SQL Server data provider implementation
- Source: [`IDataRequest.cs`](https://github.com/nicholascallee/Excalibur.Dispatch/blob/main/src/Excalibur/Excalibur.Data.Abstractions/IDataRequest.cs), [`IDataRequestResolver.cs`](https://github.com/nicholascallee/Excalibur.Dispatch/blob/main/src/Excalibur/Excalibur.Data.Abstractions/IDataRequestResolver.cs), [`SqlDataRequestResolver.cs`](https://github.com/nicholascallee/Excalibur.Dispatch/blob/main/src/Excalibur/Excalibur.Data.SqlServer/SqlDataRequestResolver.cs)

