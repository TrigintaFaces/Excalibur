---
sidebar_position: 3
title: Testing Guide
description: Comprehensive testing patterns for Dispatch handlers, Excalibur aggregates, and conformance testing
---

# Testing Guide

This guide covers testing patterns for Excalibur applications, from unit testing handlers to integration testing with real infrastructure.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- Install the required packages:
  ```bash
  dotnet add package Excalibur.Dispatch.Testing
  dotnet add package Excalibur.Testing
  ```
- A test framework of your choice (xUnit, NUnit, or MSTest)
- Familiarity with [actions and handlers](../core-concepts/actions-and-handlers.md) and the [test harness](../testing/test-harness.md)

## Testing Packages

| Package | Purpose |
|---------|---------|
| `Excalibur.Testing` | Aggregate test fixtures, conformance test kits |
| Your test framework | xUnit, NUnit, or MSTest (framework-agnostic) |
| Assertion library | Shouldly, FluentAssertions, or built-in |
| Mocking library | FakeItEasy, Moq, or NSubstitute |

```bash
dotnet add package Excalibur.Testing
dotnet add package xunit
dotnet add package Shouldly
dotnet add package FakeItEasy
```

## Unit Testing Handlers

### Action Handler Without Return Value

```csharp
using FakeItEasy;
using Shouldly;
using Xunit;

public class CreateOrderHandlerTests
{
    private readonly IOrderRepository _repository;
    private readonly ILogger<CreateOrderHandler> _logger;
    private readonly CreateOrderHandler _handler;

    public CreateOrderHandlerTests()
    {
        _repository = A.Fake<IOrderRepository>();
        _logger = A.Fake<ILogger<CreateOrderHandler>>();
        _handler = new CreateOrderHandler(_repository, _logger);
    }

    [Fact]
    public async Task HandleAsync_ValidAction_SavesOrder()
    {
        // Arrange
        var action = new CreateOrderAction("customer-123", new List<string> { "item-1" });

        // Act
        await _handler.HandleAsync(action, CancellationToken.None);

        // Assert
        A.CallTo(() => _repository.SaveAsync(
            A<Order>.That.Matches(o => o.CustomerId == "customer-123"),
            A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    [Fact]
    public async Task HandleAsync_EmptyItems_ThrowsValidationException()
    {
        // Arrange
        var action = new CreateOrderAction("customer-123", new List<string>());

        // Act & Assert
        await Should.ThrowAsync<ValidationException>(
            () => _handler.HandleAsync(action, CancellationToken.None));
    }
}
```

### Action Handler With Return Value

```csharp
public class GetOrderHandlerTests
{
    private readonly IOrderRepository _repository;
    private readonly GetOrderHandler _handler;

    public GetOrderHandlerTests()
    {
        _repository = A.Fake<IOrderRepository>();
        _handler = new GetOrderHandler(_repository);
    }

    [Fact]
    public async Task HandleAsync_ExistingOrder_ReturnsOrder()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var expected = new Order { Id = orderId, CustomerId = "C1" };

        A.CallTo(() => _repository.GetByIdAsync(orderId, A<CancellationToken>._))
            .Returns(expected);

        var action = new GetOrderAction(orderId);

        // Act
        var result = await _handler.HandleAsync(action, CancellationToken.None);

        // Assert
        result.ShouldBe(expected);
    }

    [Fact]
    public async Task HandleAsync_NonExistentOrder_ThrowsNotFoundException()
    {
        // Arrange
        var orderId = Guid.NewGuid();

        A.CallTo(() => _repository.GetByIdAsync(orderId, A<CancellationToken>._))
            .Returns((Order?)null);

        var action = new GetOrderAction(orderId);

        // Act & Assert
        await Should.ThrowAsync<NotFoundException>(
            () => _handler.HandleAsync(action, CancellationToken.None));
    }
}
```

## Testing Event-Sourced Aggregates

The `Excalibur.Testing` package provides a fluent Given-When-Then API for testing aggregates.

### Basic Aggregate Testing

```csharp
using Excalibur.Testing;
using Xunit;

public class OrderAggregateTests
{
    [Fact]
    public void Create_ValidOrder_RaisesOrderCreatedEvent()
    {
        new AggregateTestFixture<OrderAggregate>()
            .When(order => order.Create("order-123", "customer-456", 99.99m))
            .Then()
            .ShouldRaise<OrderCreatedEvent>()
            .StateShould(order => order.Id == "order-123")
            .StateShould(order => order.Status == OrderStatus.Created);
    }

    [Fact]
    public void Ship_CreatedOrder_RaisesOrderShippedEvent()
    {
        new AggregateTestFixture<OrderAggregate>()
            .Given(new OrderCreatedEvent
            {
                AggregateId = "order-123",
                CustomerId = "customer-456",
                Amount = 99.99m
            })
            .When(order => order.Ship("tracking-789"))
            .Then()
            .ShouldRaise<OrderShippedEvent>(e => e.TrackingNumber == "tracking-789")
            .StateShould(order => order.Status == OrderStatus.Shipped);
    }

    [Fact]
    public void Ship_AlreadyShippedOrder_ThrowsInvalidOperationException()
    {
        new AggregateTestFixture<OrderAggregate>()
            .Given(
                new OrderCreatedEvent { AggregateId = "order-123" },
                new OrderShippedEvent { AggregateId = "order-123" })
            .When(order => order.Ship("tracking-999"))
            .ShouldThrow<InvalidOperationException>("already shipped");
    }

    [Fact]
    public void Cancel_ShippedOrder_RaisesNoEvents()
    {
        new AggregateTestFixture<OrderAggregate>()
            .Given(
                new OrderCreatedEvent { AggregateId = "order-123" },
                new OrderShippedEvent { AggregateId = "order-123" })
            .When(order => order.Cancel("customer request"))
            .Then()
            .ShouldRaiseNoEvents(); // Cannot cancel shipped orders
    }
}
```

### Testing Complex Aggregate State

```csharp
[Fact]
public void AddItem_MultipleItems_CalculatesTotalCorrectly()
{
    new AggregateTestFixture<OrderAggregate>()
        .Given(new OrderCreatedEvent { AggregateId = "order-123" })
        .When(order =>
        {
            order.AddItem("product-1", 2, 10.00m);
            order.AddItem("product-2", 1, 25.00m);
        })
        .Then()
        .ShouldRaise<OrderItemAddedEvent>()
        .AssertAggregate(order =>
        {
            order.Items.Count.ShouldBe(2);
            order.TotalAmount.ShouldBe(45.00m); // (2 * 10) + (1 * 25)
        });
}
```

## Integration Testing

### Testing with WebApplicationFactory

```csharp
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http.Json;

public class OrderApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public OrderApiTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Replace real services with test doubles
                services.AddScoped<IOrderRepository, InMemoryOrderRepository>();
            });
        }).CreateClient();
    }

    [Fact]
    public async Task CreateOrder_ValidRequest_ReturnsCreated()
    {
        // Arrange
        var request = new { CustomerId = "C123", Items = new[] { "item-1" } };

        // Act
        var response = await _client.PostAsJsonAsync("/api/orders", request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task GetOrder_ExistingOrder_ReturnsOk()
    {
        // Arrange - Create an order first
        var createRequest = new { CustomerId = "C123", Items = new[] { "item-1" } };
        var createResponse = await _client.PostAsJsonAsync("/api/orders", createRequest);
        var location = createResponse.Headers.Location;

        // Act
        var response = await _client.GetAsync(location);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var order = await response.Content.ReadFromJsonAsync<OrderDto>();
        order.ShouldNotBeNull();
        order.CustomerId.ShouldBe("C123");
    }

    [Fact]
    public async Task GetOrder_NonExistent_ReturnsNotFound()
    {
        // Act
        var response = await _client.GetAsync($"/api/orders/{Guid.NewGuid()}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
```

### Testing with TestContainers

For integration tests that require real infrastructure:

```csharp
using Testcontainers.MsSql;

public class SqlServerIntegrationTests : IAsyncLifetime
{
    private readonly MsSqlContainer _sqlContainer;
    private IServiceProvider _services = null!;

    public SqlServerIntegrationTests()
    {
        _sqlContainer = new MsSqlBuilder()
            .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _sqlContainer.StartAsync();

        var services = new ServiceCollection();
        services.AddDispatch(dispatch =>
        {
            dispatch.AddHandlersFromAssembly(typeof(SqlServerIntegrationTests).Assembly);
        });
        services.AddSqlServerEventSourcing(_sqlContainer.GetConnectionString());

        _services = services.BuildServiceProvider();

        // Run migrations
        await _services.GetRequiredService<IMigrator>().MigrateAsync(CancellationToken.None);
    }

    public async Task DisposeAsync()
    {
        await _sqlContainer.DisposeAsync();
    }

    [Fact]
    public async Task EventStore_AppendAndLoad_RoundTrips()
    {
        // Arrange
        var eventStore = _services.GetRequiredService<IEventStore>();
        var aggregateId = Guid.NewGuid().ToString();
        var events = new[]
        {
            new OrderCreatedEvent { AggregateId = aggregateId, Version = 1 }
        };

        // Act
        await eventStore.AppendAsync(aggregateId, events, 0, CancellationToken.None);
        var loaded = await eventStore.LoadAsync(aggregateId, CancellationToken.None);

        // Assert
        loaded.ShouldHaveSingleItem();
        loaded[0].ShouldBeOfType<OrderCreatedEvent>();
    }
}
```

## Conformance Testing

The `Excalibur.Testing` package includes conformance test kits for verifying custom implementations.

### Event Store Conformance

```csharp
using Excalibur.Testing.Conformance;

public class CustomEventStoreConformanceTests : EventStoreConformanceTestKit
{
    protected override IEventStore CreateEventStore()
    {
        return new CustomEventStore(/* your configuration */);
    }

    // All conformance tests are inherited and run automatically
    // Override specific tests if your implementation has special behavior
}
```

### Snapshot Store Conformance

```csharp
public class CustomSnapshotStoreConformanceTests : SnapshotStoreConformanceTestKit
{
    protected override ISnapshotStore CreateSnapshotStore()
    {
        return new CustomSnapshotStore(/* your configuration */);
    }
}
```

### Available Conformance Test Kits

| Test Kit | Tests |
|----------|-------|
| `EventStoreConformanceTestKit` | Append, load, concurrency, versioning |
| `SnapshotStoreConformanceTestKit` | Save, load, delete snapshots |
| `OutboxStoreConformanceTestKit` | Publish, mark sent, cleanup |
| `InboxStoreConformanceTestKit` | Deduplication, expiry |
| `SagaStoreConformanceTestKit` | State persistence, timeout handling |
| `LeaderElectionConformanceTestKit` | Acquire, renew, release leadership |
| `DeadLetterStoreConformanceTestKit` | Store, retrieve, reprocess |

## Testing Middleware

### Custom Middleware Unit Tests

```csharp
public class LoggingMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_LogsBeforeAndAfter()
    {
        // Arrange
        var logger = A.Fake<ILogger<LoggingMiddleware>>();
        var middleware = new LoggingMiddleware(logger);
        var message = A.Fake<IDispatchMessage>();
        var context = new TestMessageContext();
        var nextCalled = false;

        ValueTask<IMessageResult> Next(
            IDispatchMessage msg,
            IMessageContext ctx,
            CancellationToken ct)
        {
            nextCalled = true;
            return new ValueTask<IMessageResult>(MessageResult.Success());
        }

        // Act
        await middleware.InvokeAsync(message, context, Next, CancellationToken.None);

        // Assert
        nextCalled.ShouldBeTrue();
        A.CallTo(logger).MustHaveHappenedTwiceExactly(); // Before and after
    }
}
```

## Testing Best Practices

### Arrange-Act-Assert Pattern

```csharp
[Fact]
public async Task ProcessPayment_ValidCard_ReturnsSuccess()
{
    // Arrange - Set up test data and dependencies
    var handler = new ProcessPaymentHandler(_paymentGateway);
    var action = new ProcessPaymentAction(orderId, 99.99m, "USD");

    // Act - Execute the behavior under test
    var result = await handler.HandleAsync(action, CancellationToken.None);

    // Assert - Verify the expected outcome
    result.IsSuccess.ShouldBeTrue();
    result.ReturnValue.TransactionId.ShouldNotBeNullOrEmpty();
}
```

### Test Data Builders

```csharp
public class OrderBuilder
{
    private string _customerId = "default-customer";
    private List<string> _items = new() { "default-item" };
    private decimal _amount = 100m;

    public OrderBuilder WithCustomer(string customerId)
    {
        _customerId = customerId;
        return this;
    }

    public OrderBuilder WithItems(params string[] items)
    {
        _items = items.ToList();
        return this;
    }

    public OrderBuilder WithAmount(decimal amount)
    {
        _amount = amount;
        return this;
    }

    public CreateOrderAction BuildAction() =>
        new(_customerId, _items);

    public Order Build() =>
        new()
        {
            Id = Guid.NewGuid(),
            CustomerId = _customerId,
            Items = _items,
            TotalAmount = _amount
        };
}

// Usage
[Fact]
public async Task Handler_HighValueOrder_RequiresApproval()
{
    var action = new OrderBuilder()
        .WithCustomer("VIP-001")
        .WithAmount(10_000m)
        .BuildAction();

    // ...
}
```

### Avoid Test Pollution

```csharp
public class OrderHandlerTests : IDisposable
{
    private readonly ServiceProvider _services;

    public OrderHandlerTests()
    {
        var services = new ServiceCollection();
        services.AddDispatch(dispatch =>
        {
            dispatch.AddHandlersFromAssembly(typeof(OrderHandlerTests).Assembly);
        });
        services.AddScoped<IOrderRepository, InMemoryOrderRepository>();
        _services = services.BuildServiceProvider();
    }

    public void Dispose()
    {
        _services.Dispose();
    }

    [Fact]
    public async Task Test1()
    {
        using var scope = _services.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<CreateOrderHandler>();
        // Each test gets a fresh scope
    }
}
```

## Running Tests

```bash
# Run all tests
dotnet test

# Run specific test project
dotnet test tests/functional/Excalibur.Dispatch.Tests.Functional

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run specific test
dotnet test --filter "FullyQualifiedName~CreateOrderHandler"
```

## Related Documentation

- [Getting Started](../getting-started/) - Quick start guide
- [Actions and Handlers](../core-concepts/actions-and-handlers.md) - Handler implementation
- [Event Sourcing](/docs/event-sourcing) - Aggregate patterns

## See Also

- [Test Harness](../testing/test-harness.md) — DispatchTestHarness for integration testing with real DI containers
- [Testing Handlers](../testing/testing-handlers.md) — Detailed patterns for unit testing action, event, and document handlers
- [Transport Test Doubles](../testing/transport-test-doubles.md) — InMemoryTransportSender, Receiver, and Subscriber for transport testing
- [Shouldly Assertions](../testing/shouldly-assertions.md) — Dispatch-specific Shouldly assertion extensions

