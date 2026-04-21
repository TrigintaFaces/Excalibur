# MultiDatabase Sample

Demonstrates how to register multiple `Func<SqlConnection>` factories for different databases in one application using .NET 8+ keyed services.

## What This Shows

- **Keyed services** for multiple database connection factories
- Default (non-keyed) factory for framework components
- `[FromKeyedServices("key")]` attribute injection in handlers
- Configuration-driven connection strings

## Pattern

```csharp
// Register keyed factories
services.AddKeyedSingleton<Func<SqlConnection>>(
    "Orders", (_, _) => () => new SqlConnection(ordersConnStr));

services.AddKeyedSingleton<Func<SqlConnection>>(
    "Inventory", (_, _) => () => new SqlConnection(inventoryConnStr));

// Register default factory for framework components
services.AddSingleton<Func<SqlConnection>>(
    () => new SqlConnection(ordersConnStr));

// Inject in handler
public class TransferHandler(
    [FromKeyedServices("Orders")] Func<SqlConnection> ordersDb,
    [FromKeyedServices("Inventory")] Func<SqlConnection> inventoryDb)
{
    // Each handler gets the correct database
}
```

## Running

```bash
dotnet run
```

## See Also

- `docs-site/docs/data-providers/multi-database.md` -- Full multi-database guide
- `samples/01-getting-started/DataAccessIntro` -- Single database setup
- `samples/09-advanced/TransactionalHandlers` -- Transactions with connection factories
