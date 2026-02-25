---
sidebar_position: 1
title: Testing Overview
description: Testing strategies for event-sourced applications built with Excalibur
---

# Testing Event-Sourced Applications

:::tip Start here
If you use Dispatch without event sourcing (e.g., as a MediatR replacement), the **[Testing Dispatch Handlers](testing-handlers.md)** guide covers unit testing handlers, verifying middleware behavior, and integration testing the pipeline.
:::

Testing event-sourced applications requires different strategies than traditional CRUD applications. Instead of asserting database state, you verify that the correct events are raised and that aggregates behave correctly when replaying historical events.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- Install the testing packages:
  ```bash
  dotnet add package Excalibur.Dispatch.Testing
  dotnet add package Excalibur.Dispatch.Testing.Shouldly  # optional
  ```
- Familiarity with [handlers](../handlers.md) and [event sourcing concepts](../event-sourcing/concepts.md)

## Testing Pyramid for Event Sourcing

```
        ┌─────────────────┐
        │   E2E Tests     │  ← Full system with real transport
        │   (few)         │
        ├─────────────────┤
        │  Integration    │  ← Real database, real event store
        │   (some)        │
        ├─────────────────┤
        │   Unit Tests    │  ← In-memory, fast, isolated
        │   (many)        │
        └─────────────────┘
```

## What to Test

| Layer | What to Test | How |
|-------|--------------|-----|
| **Aggregates** | Business rules, state transitions, event generation | `AggregateTestFixture` |
| **Event Handlers** | Projections update correctly, side effects triggered | Mock event store |
| **Repositories** | Load/save roundtrips, concurrency handling | Integration tests with real store |
| **Sagas** | State machine transitions, compensating actions | `AggregateTestFixture` pattern |

## Excalibur.Testing Package

Install the testing utilities:

```bash
dotnet add package Excalibur.Testing
```

This package provides:

- **`AggregateTestFixture<T>`** — Fluent Given-When-Then API for aggregate testing
- **Conformance Test Kits** — Verify custom provider implementations against contracts

## Test Framework Compatibility

Excalibur.Testing is framework-agnostic. Use it with:

- xUnit (recommended)
- NUnit
- MSTest
- Any other .NET test framework

The fixture throws `TestFixtureAssertionException` on failures, which all test runners recognize as test failures.

## Quick Example

```csharp
using Excalibur.Testing;
using Xunit;

public class OrderTests
{
    [Fact]
    public void Create_order_raises_OrderCreated_event()
    {
        new AggregateTestFixture<Order>()
            .Given()  // No prior events - new aggregate
            .When(order => order.Create("customer-123"))
            .Then()
            .ShouldRaise<OrderCreated>(e => e.CustomerId == "customer-123");
    }

    [Fact]
    public void Cannot_ship_cancelled_order()
    {
        new AggregateTestFixture<Order>()
            .Given(
                new OrderCreated { OrderId = "123", CustomerId = "C1" },
                new OrderCancelled { OrderId = "123", Reason = "Out of stock" })
            .When(order => order.Ship())
            .ShouldThrow<InvalidOperationException>("cancelled");
    }
}
```

## Testing Categories

### [Aggregate Testing](./aggregate-testing.md)

Unit test your aggregates using the Given-When-Then pattern. Fast, isolated, no external dependencies.

### [Repository Testing](./repository-testing.md)

Integration test your event store interactions. Verify load/save roundtrips and concurrency.

### [Integration Tests](./integration-tests.md)

End-to-end testing with real infrastructure using TestContainers.

## Dispatch Pipeline Testing

The `Excalibur.Dispatch.Testing` package provides purpose-built test infrastructure for the Dispatch pipeline. These tools let you test handlers, middleware, and transport interactions without real message brokers.

```bash
dotnet add package Excalibur.Dispatch.Testing
dotnet add package Excalibur.Dispatch.Testing.Shouldly  # optional, for fluent assertions
```

### [Test Harness](./test-harness.md)

`DispatchTestHarness` builds a real Dispatch pipeline with DI, automatically tracks all dispatched messages, and provides `MessageContextBuilder` for creating test contexts with sensible defaults.

### [Transport Test Doubles](./transport-test-doubles.md)

In-memory implementations of `ITransportSender`, `ITransportReceiver`, and `ITransportSubscriber` that record all interactions for assertions. Test transport flows without RabbitMQ, Kafka, or any real infrastructure.

### [Shouldly Assertions](./shouldly-assertions.md)

Nine fluent assertion extensions for `IDispatchedMessageLog`, `InMemoryTransportSender`, and `InMemoryTransportReceiver`. Produces domain-specific failure messages.

### [Testing Dispatch Handlers](./testing-handlers.md)

Unit test handlers directly with FakeItEasy, test middleware behavior, and integration test the full pipeline.

## Best Practices

### DO

- Test one behavior per test method
- Use descriptive test names that explain the scenario
- Test both happy paths and error cases
- Verify events are raised with correct data
- Test idempotency where applicable

### DON'T

- Test implementation details (private methods)
- Skip negative test cases
- Couple tests to event serialization format
- Test the framework itself (trust Excalibur's tests)

## Next Steps

1. Start with [Aggregate Testing](./aggregate-testing.md) for unit tests
2. Add [Repository Testing](./repository-testing.md) for integration coverage
3. See [Integration Tests](./integration-tests.md) for full system tests
4. Use the [Test Harness](./test-harness.md) for pipeline-level testing

## See Also

- [Testing Handlers](./testing-handlers.md) - Unit testing handler implementations
- [Test Harness](./test-harness.md) - DispatchTestHarness for pipeline-level testing
- [Integration Tests](./integration-tests.md) - Full system integration testing
