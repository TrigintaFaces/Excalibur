# Data Access Intro Sample

Demonstrates the `IDataRequest` pattern -- the core data access abstraction in the Excalibur framework.

## Pattern

```csharp
// 1. Register connection factory
services.AddSingleton<Func<SqlConnection>>(() => new SqlConnection(connectionString));

// 2. Create a DataRequest<T> subclass
public sealed class GetProductById : DataRequest<Product?>
{
    public GetProductById(int productId)
    {
        Command = CreateCommand("SELECT ... WHERE Id = @Id", parameters);
        ResolveAsync = async conn => await conn.QuerySingleOrDefaultAsync<Product?>(Command);
    }
}

// 3. Execute the request
using var connection = connectionFactory();
var product = await connection.Ready().ResolveAsync(new GetProductById(42));
```

## Key Concepts

| Concept | Description |
|---------|-------------|
| `DataRequest<T>` | Base class for database requests (aliases `DataRequestBase<IDbConnection, T>`) |
| `Func<SqlConnection>` | Connection factory -- ADO.NET pooling is automatic |
| `Ready()` | Extension method that opens the connection if closed/broken |
| `ResolveAsync()` | Extension method that executes the request via Dapper |

## Files

- `Product.cs` -- Simple model class
- `Requests/GetProductById.cs` -- SELECT single record
- `Requests/GetAllProducts.cs` -- SELECT multiple records
- `Requests/InsertProduct.cs` -- INSERT record

## No EntityFramework

This framework uses Dapper for all data access. See the `IDataRequest` guide for details.
