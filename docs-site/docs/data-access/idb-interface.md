---
sidebar_position: 1
title: IDb Interface
description: Database connection abstraction with Dapper integration
---

# IDb Interface

The `IDb` interface provides a lightweight abstraction over database connections, designed to work with Dapper for all SQL operations. This approach keeps dependencies minimal while maintaining full control over SQL queries.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- Install the required packages:
  ```bash
  dotnet add package Excalibur.Data.Abstractions
  dotnet add package Excalibur.Data.SqlServer  # or your provider
  ```
- Familiarity with [Dapper](https://github.com/DapperLib/Dapper) and SQL

## Why Not Entity Framework?

Excalibur intentionally avoids Entity Framework Core:

| Concern | EF Core | IDb + Dapper |
|---------|---------|--------------|
| **Dependencies** | Heavy (~15 packages) | Minimal (Dapper only) |
| **Performance** | Tracking overhead | Direct SQL execution |
| **Control** | Generated queries | Explicit SQL |
| **Complexity** | Change tracking, migrations | Simple connection management |
| **AOT Support** | Limited | Full Native AOT |

:::note Design Decision
For a framework distributed as NuGet packages, minimal dependencies and maximum performance are critical. Dapper provides the best balance of usability and efficiency.
:::

## IDb Interface

The `IDb` interface is intentionally simple:

```csharp
public interface IDb
{
    /// <summary>
    /// Gets the underlying database connection.
    /// </summary>
    IDbConnection Connection { get; }

    /// <summary>
    /// Opens the database connection.
    /// </summary>
    void Open();

    /// <summary>
    /// Closes the database connection.
    /// </summary>
    void Close();

    /// <summary>
    /// Opens the database connection asynchronously.
    /// Default implementation delegates to the synchronous Open() method.
    /// </summary>
    Task OpenAsync(CancellationToken cancellationToken) => /* default impl */;

    /// <summary>
    /// Closes the database connection asynchronously.
    /// Default implementation delegates to the synchronous Close() method.
    /// </summary>
    Task CloseAsync() => /* default impl */;
}
```

## Using IDb with Dapper

### Basic Queries

```csharp
public class OrderRepository
{
    private readonly IDb _db;

    public OrderRepository(IDb db) => _db = db;

    public async Task<Order?> GetByIdAsync(Guid orderId, CancellationToken ct)
    {
        const string sql = "SELECT * FROM Orders WHERE Id = @OrderId";

        return await _db.Connection.QuerySingleOrDefaultAsync<Order>(
            new CommandDefinition(sql, new { OrderId = orderId }, cancellationToken: ct));
    }

    public async Task<IEnumerable<Order>> GetByCustomerAsync(
        string customerId,
        CancellationToken ct)
    {
        const string sql = @"
            SELECT o.*, oi.*
            FROM Orders o
            LEFT JOIN OrderItems oi ON o.Id = oi.OrderId
            WHERE o.CustomerId = @CustomerId
            ORDER BY o.CreatedAt DESC";

        return await _db.Connection.QueryAsync<Order>(
            new CommandDefinition(sql, new { CustomerId = customerId }, cancellationToken: ct));
    }
}
```

### Insert Operations

```csharp
public async Task<int> CreateAsync(Order order, CancellationToken ct)
{
    const string sql = @"
        INSERT INTO Orders (Id, CustomerId, TotalAmount, Status, CreatedAt)
        VALUES (@Id, @CustomerId, @TotalAmount, @Status, @CreatedAt)";

    return await _db.Connection.ExecuteAsync(
        new CommandDefinition(sql, order, cancellationToken: ct));
}
```

### Update Operations

```csharp
public async Task<int> UpdateStatusAsync(
    Guid orderId,
    OrderStatus status,
    CancellationToken ct)
{
    const string sql = @"
        UPDATE Orders
        SET Status = @Status, UpdatedAt = @UpdatedAt
        WHERE Id = @OrderId";

    return await _db.Connection.ExecuteAsync(
        new CommandDefinition(
            sql,
            new { OrderId = orderId, Status = status, UpdatedAt = DateTimeOffset.UtcNow },
            cancellationToken: ct));
}
```

### Transaction Management

```csharp
public async Task ProcessOrderAsync(Order order, CancellationToken ct)
{
    _db.Open();

    using var transaction = _db.Connection.BeginTransaction();

    try
    {
        // Insert order
        await _db.Connection.ExecuteAsync(
            new CommandDefinition(InsertOrderSql, order, transaction, cancellationToken: ct));

        // Insert order items
        foreach (var item in order.Items)
        {
            await _db.Connection.ExecuteAsync(
                new CommandDefinition(InsertItemSql, item, transaction, cancellationToken: ct));
        }

        // Commit transaction
        transaction.Commit();
    }
    catch
    {
        transaction.Rollback();
        throw;
    }
}
```

## IDataRequest Pattern

For encapsulated, reusable queries, use the `IDataRequest` pattern which combines query definition and execution:

```csharp
public interface IDataRequest<in TConnection, TModel> : IDataRequest
{
    /// <summary>
    /// Gets the command definition for executing the request.
    /// </summary>
    CommandDefinition Command { get; }

    /// <summary>
    /// Gets the parameters associated with the request.
    /// </summary>
    DynamicParameters Parameters { get; }

    /// <summary>
    /// Gets the function responsible for resolving the result.
    /// </summary>
    Func<TConnection, Task<TModel>> ResolveAsync { get; }
}
```

### DataRequest Base Class (Recommended)

The simplest approach is to extend `DataRequest<TModel>`, which uses `IDbConnection` by default:

```csharp
using Excalibur.Data.Abstractions;
using Dapper;

public class CreateActivityGroupRequest : DataRequest<int>
{
    public CreateActivityGroupRequest(
        string? tenantId,
        string name,
        string activityName,
        CancellationToken cancellationToken)
    {
        const string CommandText = """
            INSERT INTO authz.ActivityGroup (
                TenantId,
                Name,
                ActivityName
            ) VALUES (
                @TenantId,
                @ActivityGroupName,
                @ActivityName
            );
            """;

        Command = CreateCommand(
            CommandText,
            parameters: new DynamicParameters(new
            {
                TenantId = tenantId,
                ActivityGroupName = name,
                ActivityName = activityName
            }),
            commandTimeout: DbTimeouts.RegularTimeoutSeconds,
            cancellationToken: cancellationToken);

        ResolveAsync = async connection =>
            await connection.ExecuteAsync(Command).ConfigureAwait(false);
    }
}
```

### Query Example

```csharp
public class GetOrdersByDateRangeRequest : DataRequest<IEnumerable<Order>>
{
    public GetOrdersByDateRangeRequest(
        DateTimeOffset startDate,
        DateTimeOffset endDate,
        OrderStatus? status,
        CancellationToken cancellationToken)
    {
        const string CommandText = """
            SELECT * FROM Orders
            WHERE CreatedAt >= @StartDate
              AND CreatedAt <= @EndDate
              AND (@Status IS NULL OR Status = @Status)
            ORDER BY CreatedAt DESC
            """;

        Command = CreateCommand(
            CommandText,
            parameters: new DynamicParameters(new
            {
                StartDate = startDate,
                EndDate = endDate,
                Status = status
            }),
            commandTimeout: DbTimeouts.RegularTimeoutSeconds,
            cancellationToken: cancellationToken);

        ResolveAsync = connection => connection.QueryAsync<Order>(Command);
    }
}
```

### DbTimeouts Constants

Use predefined timeout constants for consistency:

```csharp
public static class DbTimeouts
{
    public const int RegularTimeoutSeconds = 60;        // Standard operations
    public const int LongRunningTimeoutSeconds = 600;   // Batch operations (10 min)
    public const int ExtraLongRunningTimeoutSeconds = 1200;  // Heavy processing (20 min)
}
```

### Generic IDataRequest (Advanced)

For custom connection types, use the full generic interface:

```csharp
public class GetOrdersByDateRangeRequest : IDataRequest<IDbConnection, IEnumerable<Order>>
{
    public string RequestId { get; } = Guid.NewGuid().ToString();
    public string RequestType => nameof(GetOrdersByDateRangeRequest);
    public DateTimeOffset CreatedAt { get; } = DateTimeOffset.UtcNow;
    public string? CorrelationId { get; init; }
    public IDictionary<string, object>? Metadata { get; init; }

    public DateTimeOffset StartDate { get; init; }
    public DateTimeOffset EndDate { get; init; }
    public OrderStatus? Status { get; init; }

    public CommandDefinition Command => new(
        @"SELECT * FROM Orders
          WHERE CreatedAt >= @StartDate
            AND CreatedAt <= @EndDate
            AND (@Status IS NULL OR Status = @Status)
          ORDER BY CreatedAt DESC",
        Parameters);

    public DynamicParameters Parameters
    {
        get
        {
            var p = new DynamicParameters();
            p.Add("StartDate", StartDate);
            p.Add("EndDate", EndDate);
            p.Add("Status", Status);
            return p;
        }
    }

    public Func<IDbConnection, Task<IEnumerable<Order>>> ResolveAsync =>
        conn => conn.QueryAsync<Order>(Command);
}
```

### Executing DataRequests

Use the `ResolveAsync` extension method on `IDbConnection` for clean execution with built-in error handling:

```csharp
using Excalibur.Data.Abstractions;

public class OrderQueryService
{
    private readonly IDb _db;

    public OrderQueryService(IDb db) => _db = db;

    public async Task<IEnumerable<Order>> GetOrdersAsync(
        DateTimeOffset startDate,
        DateTimeOffset endDate,
        CancellationToken ct)
    {
        var request = new GetOrdersByDateRangeRequest(startDate, endDate, status: null, ct);

        // ResolveAsync extension wraps exceptions in OperationFailedException
        return await _db.Connection.ResolveAsync(request);
    }
}
```

The `ResolveAsync` extension method:
- Automatically opens the connection if needed (via `Ready()`)
- Wraps database exceptions in `OperationFailedException` for consistent error handling
- Preserves the original exception as `InnerException`

### Connection Extension Methods

```csharp
public static class DbConnectionExtensions
{
    /// <summary>
    /// Executes a data request with consistent error handling.
    /// </summary>
    public static Task<TModel> ResolveAsync<TModel>(
        this IDbConnection connection,
        IDataRequest<IDbConnection, TModel> dataRequest);

    /// <summary>
    /// Ensures the connection is open and ready. Reopens broken connections.
    /// </summary>
    public static IDbConnection Ready(this IDbConnection connection);
}
```

### Manual Execution (Advanced)

For cases where you need more control:

```csharp
public async Task<IEnumerable<Order>> ExecuteManuallyAsync(
    GetOrdersByDateRangeRequest request,
    CancellationToken ct)
{
    _db.Open();
    try
    {
        // Direct invocation of the ResolveAsync delegate
        return await request.ResolveAsync(_db.Connection);
    }
    finally
    {
        _db.Close();
    }
}
```

## Multi-Mapping (Joins)

Dapper's multi-mapping maps joined tables to related objects:

```csharp
public async Task<IEnumerable<Order>> GetOrdersWithItemsAsync(CancellationToken ct)
{
    const string sql = @"
        SELECT o.*, oi.*
        FROM Orders o
        LEFT JOIN OrderItems oi ON o.Id = oi.OrderId
        ORDER BY o.CreatedAt DESC";

    var orderDictionary = new Dictionary<Guid, Order>();

    await _db.Connection.QueryAsync<Order, OrderItem, Order>(
        new CommandDefinition(sql, cancellationToken: ct),
        (order, item) =>
        {
            if (!orderDictionary.TryGetValue(order.Id, out var existingOrder))
            {
                existingOrder = order;
                existingOrder.Items = new List<OrderItem>();
                orderDictionary.Add(order.Id, existingOrder);
            }

            if (item != null)
            {
                existingOrder.Items.Add(item);
            }

            return existingOrder;
        },
        splitOn: "Id");

    return orderDictionary.Values;
}
```

## Stored Procedures

Execute stored procedures with Dapper:

```csharp
public async Task<OrderSummary> GetOrderSummaryAsync(
    Guid customerId,
    CancellationToken ct)
{
    return await _db.Connection.QuerySingleAsync<OrderSummary>(
        new CommandDefinition(
            "sp_GetCustomerOrderSummary",
            new { CustomerId = customerId },
            commandType: CommandType.StoredProcedure,
            cancellationToken: ct));
}
```

## Bulk Operations

For high-performance bulk inserts:

```csharp
public async Task BulkInsertOrdersAsync(
    IEnumerable<Order> orders,
    CancellationToken ct)
{
    const string sql = @"
        INSERT INTO Orders (Id, CustomerId, TotalAmount, Status, CreatedAt)
        VALUES (@Id, @CustomerId, @TotalAmount, @Status, @CreatedAt)";

    // Dapper handles IEnumerable automatically
    await _db.Connection.ExecuteAsync(
        new CommandDefinition(sql, orders, cancellationToken: ct));
}
```

## Best Practices

### Use CommandDefinition

Always use `CommandDefinition` for proper cancellation support:

```csharp
// Good: Supports cancellation
await connection.QueryAsync<Order>(
    new CommandDefinition(sql, parameters, cancellationToken: ct));

// Avoid: No cancellation support
await connection.QueryAsync<Order>(sql, parameters);
```

### Parameterize Queries

Always use parameters to prevent SQL injection:

```csharp
// Good: Parameterized
const string sql = "SELECT * FROM Orders WHERE CustomerId = @CustomerId";
await connection.QueryAsync<Order>(sql, new { CustomerId = customerId });

// NEVER: String concatenation
var sql = $"SELECT * FROM Orders WHERE CustomerId = '{customerId}'"; // SQL INJECTION!
```

### Connection Lifecycle

Let the DI container manage connection lifecycle:

```csharp
// Registration
services.AddScoped<IDb, SqlDb>(sp =>
    new SqlDb(new SqlConnection(configuration.GetConnectionString("Default"))));

// Usage - connection is scoped to request
public class OrderService
{
    private readonly IDb _db;

    public OrderService(IDb db) => _db = db;

    // IDb is disposed at end of request scope
}
```

### Handle Null Results

Check for null when expecting optional results:

```csharp
public async Task<Order?> GetByIdAsync(Guid id, CancellationToken ct)
{
    var order = await _db.Connection.QuerySingleOrDefaultAsync<Order>(
        new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));

    if (order is null)
    {
        // Handle not found - return null, throw, or return Result.Failed()
    }

    return order;
}
```

## Provider-Specific Implementations

| Package | Provider | IDb Implementation | Notes |
|---------|----------|-------------------|-------|
| `Excalibur.Data.SqlServer` | SQL Server | `SqlDb` | Uses `SqlConnection`, full Dapper integration |
| `Excalibur.Data.Postgres` | Postgres | Via `IDataExecutor` | Uses `NpgsqlConnection`, executor pattern |
| `Excalibur.Data.MongoDB` | MongoDB | N/A | Document-based, uses `IMongoCollection<T>` |
| `Excalibur.Data.CosmosDb` | Azure Cosmos DB | N/A | Document-based, uses `CosmosClient` |
| `Excalibur.Data.InMemory` | In-Memory | N/A | Testing only, uses in-memory storage |

### SQL Server Registration

```csharp
services.AddExcaliburSqlServices();  // Registers Dapper type handlers
services.AddScoped<IDb, SqlDb>(sp =>
    new SqlDb(new SqlConnection(configuration.GetConnectionString("SqlServer"))));
```

### Postgres Registration

Postgres uses the executor pattern instead of `IDb`:

```csharp
services.AddPostgresDataExecutors(() =>
    new NpgsqlConnection(configuration.GetConnectionString("Postgres")));

// Then inject IDataExecutor or IQueryExecutor
public class OrderService
{
    private readonly IQueryExecutor _queryExecutor;

    public OrderService(IQueryExecutor queryExecutor) => _queryExecutor = queryExecutor;
}
```

## Related Documentation

- [Data Access Overview](./) - Repository patterns
- [Event Sourcing](/docs/event-sourcing/) - Event-sourced aggregate persistence
- [Domain Modeling](/docs/domain-modeling/) - Aggregate design

## See Also

- [Data Access Overview](./index.md) — Repository patterns and data access abstractions for Excalibur applications
- [SQL Server Provider](../data-providers/sqlserver.md) — Enterprise SQL Server data provider with full Dapper integration
- [Postgres Provider](../data-providers/postgres.md) — Postgres data provider with executor pattern


