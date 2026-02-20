---
sidebar_position: 3
title: Value Objects
description: Model immutable domain concepts with structural equality
---

# Value Objects

Value objects are immutable objects that represent concepts in your domain. Unlike entities, they have no identity - two value objects with the same attributes are considered equal.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- Install the required package:
  ```bash
  dotnet add package Excalibur.Domain
  ```
- Familiarity with [domain modeling concepts](./index.md)

## Key Characteristics

| Characteristic | Description |
|----------------|-------------|
| **No Identity** | Defined by attributes, not by a unique identifier |
| **Immutability** | Once created, cannot be changed |
| **Structural Equality** | Equal if all attributes are equal |
| **Self-Validation** | Validate invariants at construction |
| **Side-Effect Free** | Operations return new instances |

## The ValueObjectBase Class

Excalibur provides `ValueObjectBase` for creating value objects:

```csharp
using Excalibur.Domain.Model.ValueObjects;

public class Money : ValueObjectBase
{
    public decimal Amount { get; }
    public string Currency { get; }

    public Money(decimal amount, string currency)
    {
        if (amount < 0)
            throw new ArgumentException("Amount cannot be negative");

        if (string.IsNullOrWhiteSpace(currency))
            throw new ArgumentException("Currency is required");

        Amount = amount;
        Currency = currency.ToUpperInvariant();
    }

    // Required: Define what makes two instances equal
    public override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Amount;
        yield return Currency;
    }
}
```

## Implementing Equality

The `GetEqualityComponents()` method defines structural equality:

```csharp
public class Address : ValueObjectBase
{
    public string Street { get; }
    public string City { get; }
    public string State { get; }
    public string PostalCode { get; }
    public string Country { get; }

    public Address(string street, string city, string state,
                   string postalCode, string country)
    {
        Street = street ?? throw new ArgumentNullException(nameof(street));
        City = city ?? throw new ArgumentNullException(nameof(city));
        State = state ?? throw new ArgumentNullException(nameof(state));
        PostalCode = postalCode ?? throw new ArgumentNullException(nameof(postalCode));
        Country = country ?? throw new ArgumentNullException(nameof(country));
    }

    public override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Street;
        yield return City;
        yield return State;
        yield return PostalCode;
        yield return Country;
    }

    public override string ToString() =>
        $"{Street}, {City}, {State} {PostalCode}, {Country}";
}
```

### Equality in Action

```csharp
var money1 = new Money(100.00m, "USD");
var money2 = new Money(100.00m, "USD");
var money3 = new Money(100.00m, "EUR");

money1 == money2; // true (same amount and currency)
money1 == money3; // false (different currency)

var address1 = new Address("123 Main St", "Seattle", "WA", "98101", "USA");
var address2 = new Address("123 Main St", "Seattle", "WA", "98101", "USA");
address1.Equals(address2); // true (all components match)
```

## Immutable Operations

Value objects should return new instances instead of mutating:

```csharp
public class Money : ValueObjectBase
{
    public decimal Amount { get; }
    public string Currency { get; }

    public Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency.ToUpperInvariant();
    }

    // Returns NEW instance - doesn't modify 'this'
    public Money Add(Money other)
    {
        if (Currency != other.Currency)
            throw new InvalidOperationException("Cannot add different currencies");

        return new Money(Amount + other.Amount, Currency);
    }

    public Money Subtract(Money other)
    {
        if (Currency != other.Currency)
            throw new InvalidOperationException("Cannot subtract different currencies");

        var result = Amount - other.Amount;
        if (result < 0)
            throw new InvalidOperationException("Result cannot be negative");

        return new Money(result, Currency);
    }

    public Money MultiplyBy(decimal factor)
    {
        if (factor < 0)
            throw new ArgumentException("Factor cannot be negative");

        return new Money(Amount * factor, Currency);
    }

    public override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Amount;
        yield return Currency;
    }
}
```

### Using Immutable Operations

```csharp
var price = new Money(100.00m, "USD");
var tax = new Money(8.50m, "USD");

// Each operation returns a new Money instance
var total = price.Add(tax);              // new Money(108.50, "USD")
var discounted = total.MultiplyBy(0.9m); // new Money(97.65, "USD")

// Original instances unchanged
Console.WriteLine(price.Amount);  // 100.00
Console.WriteLine(tax.Amount);    // 8.50
```

## Common Value Object Patterns

### Date Range

```csharp
public class DateRange : ValueObjectBase
{
    public DateTime Start { get; }
    public DateTime End { get; }

    public DateRange(DateTime start, DateTime end)
    {
        if (end < start)
            throw new ArgumentException("End date must be after start date");

        Start = start;
        End = end;
    }

    public TimeSpan Duration => End - Start;
    public bool Contains(DateTime date) => date >= Start && date <= End;
    public bool Overlaps(DateRange other) => Start < other.End && End > other.Start;

    public DateRange ExtendBy(TimeSpan duration) =>
        new DateRange(Start, End.Add(duration));

    public override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Start;
        yield return End;
    }
}
```

### Email Address

```csharp
public class EmailAddress : ValueObjectBase
{
    public string Value { get; }
    public string LocalPart => Value.Split('@')[0];
    public string Domain => Value.Split('@')[1];

    public EmailAddress(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email cannot be empty");

        if (!IsValidEmail(email))
            throw new ArgumentException("Invalid email format");

        Value = email.ToLowerInvariant();
    }

    private static bool IsValidEmail(string email)
    {
        // Simple validation - use proper regex in production
        return email.Contains('@') &&
               email.Split('@').Length == 2 &&
               email.Split('@')[1].Contains('.');
    }

    public override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;

    // Implicit conversion for convenience
    public static implicit operator string(EmailAddress email) => email.Value;
}
```

### Percentage

```csharp
public class Percentage : ValueObjectBase
{
    public decimal Value { get; }

    public Percentage(decimal value)
    {
        if (value < 0 || value > 100)
            throw new ArgumentOutOfRangeException(nameof(value),
                "Percentage must be between 0 and 100");

        Value = value;
    }

    public decimal AsDecimal => Value / 100;
    public decimal ApplyTo(decimal amount) => amount * AsDecimal;

    public static Percentage Zero => new(0);
    public static Percentage Full => new(100);

    public override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => $"{Value}%";
}
```

## Value Objects in Aggregates

Use value objects to express domain concepts clearly:

```csharp
public class Order : AggregateRoot<Guid>
{
    public CustomerId CustomerId { get; private set; }
    public Money Total { get; private set; }
    public Address ShippingAddress { get; private set; }
    public DateRange DeliveryWindow { get; private set; }

    public void UpdateShippingAddress(Address newAddress)
    {
        if (Status != OrderStatus.Draft)
            throw new InvalidOperationException("Cannot update shipped order");

        RaiseEvent(new ShippingAddressUpdated(Id, newAddress));
    }

    private bool Apply(ShippingAddressUpdated e)
    {
        // Value object replaced entirely - immutability preserved
        ShippingAddress = e.NewAddress;
        return true;
    }
}
```

## Using Records as Value Objects

C# records provide built-in value semantics for simple cases:

```csharp
// Simple value object using record
public record Money(decimal Amount, string Currency)
{
    public Money Add(Money other)
    {
        if (Currency != other.Currency)
            throw new InvalidOperationException("Currency mismatch");
        return this with { Amount = Amount + other.Amount };
    }
}

// Records have built-in equality
var m1 = new Money(100, "USD");
var m2 = new Money(100, "USD");
m1 == m2; // true
```

When to use `ValueObjectBase` vs records:

| Use ValueObjectBase | Use Records |
|---------------------|-------------|
| Complex validation logic | Simple validation |
| Custom equality rules | Standard equality |
| Inheritance needed | No inheritance |
| Framework integration | Standalone use |

## Value Object vs Entity Decision

```
Is the concept defined by its attributes?
    │
    ├── YES → Does it have a lifecycle with changes
    │         tracked over time?
    │             │
    │             ├── YES → Consider Entity
    │             │
    │             └── NO → Use Value Object
    │                      Examples: Money, Address, DateRange
    │
    └── NO → Use Entity
             Examples: Order, Customer, Product
```

## Best Practices

### 1. Validate at Construction

```csharp
public class Money : ValueObjectBase
{
    public Money(decimal amount, string currency)
    {
        // Fail fast with invalid data
        if (amount < 0)
            throw new ArgumentException("Amount cannot be negative");

        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
            throw new ArgumentException("Currency must be 3-letter ISO code");

        Amount = amount;
        Currency = currency.ToUpperInvariant();
    }
}
```

### 2. Make All Properties Read-Only

```csharp
public class Address : ValueObjectBase
{
    // All properties have private or no setters
    public string Street { get; }
    public string City { get; }

    // No methods that modify state
}
```

### 3. Include All State in Equality

```csharp
public override IEnumerable<object?> GetEqualityComponents()
{
    // Include ALL properties that define the value
    yield return Amount;
    yield return Currency;
    // Don't forget calculated or derived properties if relevant
}
```

### 4. Override ToString for Debugging

```csharp
public override string ToString() =>
    $"{Amount:N2} {Currency}";  // "100.00 USD"
```

## Next Steps

- **[Aggregates](aggregates.md)** - Use value objects within aggregates
- **[Entities](entities.md)** - Understand the difference from entities
- **[Event Sourcing](../event-sourcing/index.md)** - Serialize value objects in events

## See Also

- [Aggregates](./aggregates.md) - Using value objects within aggregate roots to express domain concepts
- [Entities](./entities.md) - Objects defined by identity rather than attributes
- [Domain Modeling Overview](./index.md) - Introduction to DDD building blocks in Excalibur
