# Excalibur.Data.Abstractions

Data access abstractions for the Excalibur framework.

## Installation

```bash
dotnet add package Excalibur.Data.Abstractions
```

## Purpose

This package provides database-agnostic data access abstractions using the Data Request pattern. Use it when building data access layers with Dapper or raw ADO.NET while maintaining clean separation between domain and infrastructure concerns.

## Key Types

- `IDb` - Database abstraction interface
- `IDataRequest` / `IDataRequest<TConnection, TModel>` - Data request pattern
- `DataRequest<TModel>` - SQL data request base class
- `IUnitOfWork` - Unit of work pattern
- `ResourceException` - Resource operation exception
- `ConcurrencyException` - Optimistic concurrency exception
- `IQueryExecutor` - Query execution abstraction
- `IDocumentDb` - Document database abstraction

## Quick Start

```csharp
// Define a data request
public class GetOrderById : DataRequest<Order>
{
    public string OrderId { get; }

    public GetOrderById(string orderId)
    {
        OrderId = orderId;
    }

    public override async Task<Order?> ExecuteAsync(
        IDbConnection connection,
        CancellationToken cancellationToken)
    {
        return await connection.QuerySingleOrDefaultAsync<Order>(
            "SELECT * FROM Orders WHERE Id = @OrderId",
            new { OrderId });
    }
}

// Execute via IDb
public class OrderRepository
{
    private readonly IDb _db;

    public OrderRepository(IDb db) => _db = db;

    public Task<Order?> GetByIdAsync(string orderId, CancellationToken ct)
        => _db.ExecuteAsync(new GetOrderById(orderId), ct);
}
```

## Documentation

Full documentation: https://github.com/TrigintaFaces/Excalibur

## License

This project is multi-licensed under:
- [Excalibur License 1.0](..\..\..\licenses\LICENSE-EXCALIBUR.txt)
- [AGPL-3.0-or-later](..\..\..\licenses\LICENSE-AGPL-3.0.txt)
- [SSPL-1.0](..\..\..\licenses\LICENSE-SSPL-1.0.txt)
- [Apache-2.0](..\..\..\licenses\LICENSE-APACHE-2.0.txt)

See [LICENSE](..\..\..\LICENSE) for details.
