# Excalibur.EventSourcing

Event sourcing infrastructure for the Excalibur framework.

## Installation

```bash
dotnet add package Excalibur.EventSourcing
```

## Features

- `IEventStore` - Event stream persistence abstraction
- `IEventSourcedRepository<T, TKey>` - Repository pattern for event-sourced aggregates
- `ISnapshotManager` and `ISnapshotStrategy` - Snapshot support for performance optimization
- `IUpcastingPipeline` - Event versioning and migration support
- `InMemoryEventStore` - In-memory implementation for testing
- AOT-compatible with full Native AOT support

## Usage

```csharp
// Register event sourcing with a provider (SQL Server example)
services.AddSqlServerEventSourcing(connectionString);

// Or use in-memory for testing
services.AddInMemoryEventStore();

// Use with aggregates
public class OrderAggregate : AggregateRoot<Guid>
{
    public void PlaceOrder(OrderDetails details)
    {
        Apply(new OrderPlacedEvent(Id, details));
    }
}
```

## Related Packages

- `Excalibur.EventSourcing.SqlServer` - SQL Server event store implementation
- `Excalibur.Domain` - Domain building blocks (AggregateRoot, entities)
- `Excalibur.Dispatch.Abstractions` - Domain event interfaces

## License

This project is multi-licensed under:
- [Excalibur License 1.0](..\..\..\licenses\LICENSE-EXCALIBUR.txt)
- [AGPL-3.0-or-later](..\..\..\licenses\LICENSE-AGPL-3.0.txt)
- [SSPL-1.0](..\..\..\licenses\LICENSE-SSPL-1.0.txt)
- [Apache-2.0](..\..\..\licenses\LICENSE-APACHE-2.0.txt)

See [LICENSE](..\..\..\LICENSE) for details.
