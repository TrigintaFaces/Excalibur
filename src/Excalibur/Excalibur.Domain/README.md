# Excalibur.Domain

Domain-Driven Design building blocks for the Excalibur framework.

## Installation

```bash
dotnet add package Excalibur.Domain
```

## Purpose

This package provides foundational DDD building blocks for domain modeling. Use it when implementing aggregate roots, entities, value objects, and domain events in your application. Designed for event-sourced and traditional persistence patterns.

## Key Types

- `AggregateRoot` / `AggregateRoot<TKey>` - Base class for aggregate roots
- `EntityBase` / `EntityBase<TKey>` - Base class for entities
- `ValueObjectBase` - Base class for value objects
- `IAggregateRoot` / `IAggregateRoot<TKey>` - Aggregate interface
- `IEntity` / `IEntity<TKey>` - Entity interface
- `IValueObject` - Value object interface
- `DomainException` - Domain-specific exception base
- `ApiException` - API exception with problem details

## Quick Start

```csharp
// Define an aggregate root
public class Order : AggregateRoot<Guid>
{
    public string CustomerId { get; private set; }
    public Money Total { get; private set; }
    public OrderStatus Status { get; private set; }

    public Order(Guid id, string customerId) : base(id)
    {
        CustomerId = customerId;
        Total = Money.Zero("USD");
        Status = OrderStatus.Pending;
    }

    public void AddItem(string productId, decimal price, int quantity)
    {
        // Domain logic
        Total = Total.Add(new Money(price * quantity, "USD"));
    }
}

// Define a value object
public class Money : ValueObjectBase
{
    public decimal Amount { get; }
    public string Currency { get; }

    public Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Amount;
        yield return Currency;
    }
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
