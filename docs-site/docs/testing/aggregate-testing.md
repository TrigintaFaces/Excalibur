---
sidebar_position: 2
title: Aggregate Testing
description: Unit testing aggregates with the Given-When-Then pattern using AggregateTestFixture
---

# Aggregate Testing

The `AggregateTestFixture<T>` class provides a fluent Given-When-Then API for testing event-sourced aggregates. Tests are fast (in-memory), isolated, and framework-agnostic.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- Familiarity with [event sourcing concepts](../event-sourcing/index.md) and [domain modeling](../domain-modeling/entities.md)

## Installation

```bash
dotnet add package Excalibur.Testing
```

## Given-When-Then Pattern

Event-sourced aggregates are tested using the Given-When-Then pattern:

| Phase | Purpose | Method |
|-------|---------|--------|
| **Given** | Set up historical events (prior state) | `.Given(events)` |
| **When** | Execute a command on the aggregate | `.When(action)` |
| **Then** | Assert events raised or exceptions thrown | `.Then().ShouldRaise<T>()` |

```csharp
new AggregateTestFixture<OrderAggregate>()
    .Given(/* historical events */)
    .When(/* command to execute */)
    .Then()
    .ShouldRaise<ExpectedEvent>();
```

## Basic Examples

### Testing Event Generation

```csharp
[Fact]
public void Create_order_raises_OrderCreated()
{
    new AggregateTestFixture<Order>()
        .Given()  // No prior events
        .When(order => order.Create("customer-123"))
        .Then()
        .ShouldRaise<OrderCreated>();
}
```

### Testing with Prior State

```csharp
[Fact]
public void Add_item_to_existing_order()
{
    new AggregateTestFixture<Order>()
        .Given(new OrderCreated { OrderId = "123", CustomerId = "C1" })
        .When(order => order.AddItem("SKU-001", quantity: 2, price: 29.99m))
        .Then()
        .ShouldRaise<OrderItemAdded>(e =>
            e.Sku == "SKU-001" &&
            e.Quantity == 2);
}
```

### Testing State After Events

```csharp
[Fact]
public void Order_total_calculated_correctly()
{
    new AggregateTestFixture<Order>()
        .Given(
            new OrderCreated { OrderId = "123", CustomerId = "C1" },
            new OrderItemAdded { Sku = "A", Quantity = 2, UnitPrice = 10.00m },
            new OrderItemAdded { Sku = "B", Quantity = 1, UnitPrice = 25.00m })
        .When(order => { /* no action - just verify state */ })
        .Then()
        .StateShould(order => order.TotalAmount == 45.00m);
}
```

## Assertion Methods

### ShouldRaise&lt;TEvent&gt;()

Asserts that an event of the specified type was raised:

```csharp
.Then().ShouldRaise<OrderShipped>();
```

### ShouldRaise&lt;TEvent&gt;(predicate)

Asserts an event matching a predicate was raised:

```csharp
.Then().ShouldRaise<OrderShipped>(e =>
    e.OrderId == "123" &&
    e.ShippedAt.Date == DateTime.Today);
```

### ShouldRaiseNoEvents()

Asserts no events were raised (useful for no-op scenarios):

```csharp
.Then().ShouldRaiseNoEvents();
```

### StateShould(predicate)

Asserts the aggregate state matches a condition:

```csharp
.Then().StateShould(order => order.Status == OrderStatus.Shipped);
```

### AssertAggregate(action)

Provides direct access for custom assertions:

```csharp
.Then().AssertAggregate(order =>
{
    Assert.Equal(3, order.Items.Count);
    Assert.Equal("customer-123", order.CustomerId);
    Assert.True(order.TotalAmount > 0);
});
```

### ShouldThrow&lt;TException&gt;()

Asserts a specific exception was thrown:

```csharp
.When(order => order.Ship())
.ShouldThrow<InvalidOperationException>();
```

### ShouldThrow&lt;TException&gt;(messageContains)

Asserts an exception with a specific message was thrown:

```csharp
.ShouldThrow<InvalidOperationException>("already shipped");
```

### ShouldNotThrow()

Asserts no exception was thrown:

```csharp
.Then().ShouldNotThrow();
```

## Testing Exception Scenarios

### Domain Rule Violations

```csharp
[Fact]
public void Cannot_add_item_to_submitted_order()
{
    new AggregateTestFixture<Order>()
        .Given(
            new OrderCreated { OrderId = "123", CustomerId = "C1" },
            new OrderSubmitted { OrderId = "123" })
        .When(order => order.AddItem("SKU-001", 1, 10.00m))
        .ShouldThrow<InvalidOperationException>("submitted");
}
```

### Business Invariant Enforcement

```csharp
[Fact]
public void Order_item_quantity_must_be_positive()
{
    new AggregateTestFixture<Order>()
        .Given(new OrderCreated { OrderId = "123", CustomerId = "C1" })
        .When(order => order.AddItem("SKU-001", quantity: 0, price: 10.00m))
        .ShouldThrow<ArgumentOutOfRangeException>();
}
```

## Method Chaining

Assertions can be chained to verify multiple conditions:

```csharp
new AggregateTestFixture<Order>()
    .Given(new OrderCreated { OrderId = "123", CustomerId = "C1" })
    .When(order => order.Submit())
    .Then()
    .ShouldRaise<OrderSubmitted>()
    .ShouldRaise<PaymentRequested>()
    .StateShould(order => order.Status == OrderStatus.PendingPayment)
    .StateShould(order => order.SubmittedAt.HasValue);
```

## Testing Multiple Events

When a command raises multiple events:

```csharp
[Fact]
public void Complete_order_raises_multiple_events()
{
    new AggregateTestFixture<Order>()
        .Given(
            new OrderCreated { OrderId = "123", CustomerId = "C1" },
            new PaymentReceived { OrderId = "123", Amount = 100.00m })
        .When(order => order.Complete())
        .Then()
        .ShouldRaise<OrderCompleted>()
        .ShouldRaise<InventoryReserved>()
        .ShouldRaise<CustomerNotified>();
}
```

## Parameterized Tests

Use xUnit's `[Theory]` for data-driven tests:

```csharp
[Theory]
[InlineData(0)]
[InlineData(-1)]
[InlineData(-100)]
public void Quantity_must_be_positive(int invalidQuantity)
{
    new AggregateTestFixture<Order>()
        .Given(new OrderCreated { OrderId = "123", CustomerId = "C1" })
        .When(order => order.AddItem("SKU", invalidQuantity, 10.00m))
        .ShouldThrow<ArgumentOutOfRangeException>();
}

[Theory]
[InlineData(1, 10.00, 10.00)]
[InlineData(2, 10.00, 20.00)]
[InlineData(3, 15.50, 46.50)]
public void Item_total_calculated_correctly(int qty, decimal price, decimal expected)
{
    new AggregateTestFixture<Order>()
        .Given(new OrderCreated { OrderId = "123", CustomerId = "C1" })
        .When(order => order.AddItem("SKU", qty, price))
        .Then()
        .ShouldRaise<OrderItemAdded>(e => e.LineTotal == expected);
}
```

## Testing Idempotency

Some operations should be idempotent (safe to repeat):

```csharp
[Fact]
public void Cancel_already_cancelled_order_is_idempotent()
{
    new AggregateTestFixture<Order>()
        .Given(
            new OrderCreated { OrderId = "123", CustomerId = "C1" },
            new OrderCancelled { OrderId = "123", Reason = "First cancellation" })
        .When(order => order.Cancel("Second cancellation attempt"))
        .Then()
        .ShouldRaiseNoEvents();  // No duplicate event
}
```

## Organizing Test Classes

Structure tests by aggregate and scenario:

```csharp
public class OrderTests
{
    public class Creation
    {
        [Fact]
        public void Creates_with_customer_id() { }

        [Fact]
        public void Requires_valid_customer_id() { }
    }

    public class AddingItems
    {
        [Fact]
        public void Adds_item_to_order() { }

        [Fact]
        public void Cannot_add_to_submitted_order() { }
    }

    public class Submission
    {
        [Fact]
        public void Submits_order_with_items() { }

        [Fact]
        public void Cannot_submit_empty_order() { }
    }
}
```

## Best Practices

| Practice | Reason |
|----------|--------|
| One behavior per test | Clear failure messages |
| Descriptive test names | Self-documenting tests |
| Minimal Given events | Only what's needed for the test |
| Test edge cases | Empty collections, boundaries, nulls |
| Don't test implementation | Test behavior, not how it's implemented |
| Avoid test interdependence | Each test should be isolated |

## Troubleshooting

### "Expected event X was not raised"

- Verify the command actually calls `RaiseEvent()`
- Check the event type matches exactly (including namespace)
- Ensure Given events don't put aggregate in wrong state

### "Aggregate state did not match"

- Debug with `.AssertAggregate()` to inspect actual state
- Verify Apply methods are correctly implemented
- Check that historical events are complete

### Tests pass individually but fail together

- Ensure aggregates don't share static state
- Each test creates a new `AggregateTestFixture` instance

## See Also

- [Aggregates (Event Sourcing)](../event-sourcing/aggregates.md) -- Aggregate root implementation and event application patterns
- [Repository Testing](./repository-testing.md) -- Testing event-sourced repository persistence and retrieval
- [Test Harness](./test-harness.md) -- DispatchTestHarness for full-pipeline testing with DI
- [Domain Modeling](../domain-modeling/index.md) -- Entities, value objects, and aggregate design guidance
