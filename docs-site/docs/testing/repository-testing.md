---
sidebar_position: 3
title: Repository Testing
description: Integration testing for event-sourced repositories and event stores
---

# Repository Testing

Repository tests verify that aggregates can be saved to and loaded from the event store correctly. These are integration tests that use a real (or in-memory) event store.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- Install the required packages:
  ```bash
  dotnet add package Excalibur.Dispatch.Testing
  dotnet add package Excalibur.EventSourcing
  ```
- Familiarity with [aggregates](../event-sourcing/aggregates.md) and the [test harness](./test-harness.md)

## When to Use Repository Tests

| Scenario | Test Type |
|----------|-----------|
| Aggregate business logic | Unit test with `AggregateTestFixture` |
| Save/load roundtrips | Repository integration test |
| Concurrency handling | Repository integration test |
| Snapshot behavior | Repository integration test |

## Test Setup

### Using In-Memory Event Store

For fast integration tests without external dependencies:

```csharp
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.Abstractions;

public class OrderRepositoryTests : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly IEventSourcedRepository<Order, OrderId> _repository;

    public OrderRepositoryTests()
    {
        var services = new ServiceCollection();
        services.AddDispatch();
        services.AddExcaliburEventSourcing(builder =>
        {
            builder.AddRepository<Order, OrderId>();
        });
        services.AddInMemoryEventStore();

        _provider = services.BuildServiceProvider();
        _repository = _provider.GetRequiredService<IEventSourcedRepository<Order, OrderId>>();
    }

    public void Dispose() => _provider.Dispose();
}
```

### Using Real Database (TestContainers)

For tests that need real database behavior:

```csharp
using Testcontainers.MsSql;

public class SqlServerRepositoryTests : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder().Build();
    private ServiceProvider _provider = null!;
    private IEventSourcedRepository<Order, OrderId> _repository = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var services = new ServiceCollection();
        services.AddDispatch();
        services.AddExcaliburEventSourcing(builder =>
        {
            builder.AddRepository<Order, OrderId>();
        });
        services.AddSqlServerEventSourcing(_container.GetConnectionString());

        _provider = services.BuildServiceProvider();
        _repository = _provider.GetRequiredService<IEventSourcedRepository<Order, OrderId>>();
    }

    public async Task DisposeAsync()
    {
        _provider.Dispose();
        await _container.DisposeAsync();
    }
}
```

## Basic Repository Tests

### Save and Load Roundtrip

```csharp
[Fact]
public async Task Can_save_and_load_aggregate()
{
    // Arrange
    var order = Order.Create("customer-123");
    order.AddItem("SKU-001", 2, 29.99m);

    // Act
    await _repository.SaveAsync(order, CancellationToken.None);
    var loaded = await _repository.GetByIdAsync(order.Id, CancellationToken.None);

    // Assert
    Assert.NotNull(loaded);
    Assert.Equal(order.Id, loaded.Id);
    Assert.Equal("customer-123", loaded.CustomerId);
    Assert.Single(loaded.Items);
    Assert.Equal(2, loaded.Version);
}
```

### Loading Non-Existent Aggregate

```csharp
[Fact]
public async Task Load_returns_null_for_nonexistent_aggregate()
{
    var id = new OrderId(Guid.NewGuid());

    var result = await _repository.GetByIdAsync(id, CancellationToken.None);

    Assert.Null(result);
}
```

### Multiple Save Operations

```csharp
[Fact]
public async Task Can_save_multiple_times()
{
    // First save
    var order = Order.Create("customer-123");
    await _repository.SaveAsync(order, CancellationToken.None);

    // Load and modify
    var loaded = await _repository.GetByIdAsync(order.Id, CancellationToken.None);
    loaded!.AddItem("SKU-001", 1, 10.00m);
    await _repository.SaveAsync(loaded, CancellationToken.None);

    // Load again and verify
    var reloaded = await _repository.GetByIdAsync(order.Id, CancellationToken.None);
    Assert.Equal(2, reloaded!.Version);
    Assert.Single(reloaded.Items);
}
```

## Concurrency Testing

### Optimistic Concurrency Conflict

```csharp
[Fact]
public async Task Concurrent_modifications_throw_concurrency_exception()
{
    // Setup: Create and save an order
    var order = Order.Create("customer-123");
    await _repository.SaveAsync(order, CancellationToken.None);

    // Load the same aggregate twice (simulating two concurrent users)
    var instance1 = await _repository.GetByIdAsync(order.Id, CancellationToken.None);
    var instance2 = await _repository.GetByIdAsync(order.Id, CancellationToken.None);

    // First modification succeeds
    instance1!.AddItem("SKU-001", 1, 10.00m);
    await _repository.SaveAsync(instance1, CancellationToken.None);

    // Second modification conflicts
    instance2!.AddItem("SKU-002", 2, 20.00m);

    await Assert.ThrowsAsync<ConcurrencyException>(
        () => _repository.SaveAsync(instance2, CancellationToken.None));
}
```

### Version Tracking

```csharp
[Fact]
public async Task Version_increments_with_each_event()
{
    var order = Order.Create("customer-123"); // Event 1
    Assert.Equal(1, order.Version);

    await _repository.SaveAsync(order, CancellationToken.None);

    var loaded = await _repository.GetByIdAsync(order.Id, CancellationToken.None);
    loaded!.AddItem("SKU-001", 1, 10.00m); // Event 2
    loaded.AddItem("SKU-002", 2, 20.00m);  // Event 3

    await _repository.SaveAsync(loaded, CancellationToken.None);

    var reloaded = await _repository.GetByIdAsync(order.Id, CancellationToken.None);
    Assert.Equal(3, reloaded!.Version);
}
```

## Testing with Snapshots

### Snapshot Creation

```csharp
[Fact]
public async Task Snapshot_created_at_threshold()
{
    var services = new ServiceCollection();
    services.AddDispatch();
    services.AddExcaliburEventSourcing(builder =>
    {
        builder.AddRepository<Order, OrderId>();
        builder.UseIntervalSnapshots(5); // Snapshot every 5 events
    });
    services.AddInMemoryEventStore();

    using var provider = services.BuildServiceProvider();
    var repository = provider.GetRequiredService<IEventSourcedRepository<Order, OrderId>>();

    // Create order with 6 events (triggers snapshot at 5)
    var order = Order.Create("customer-123");
    for (int i = 0; i < 5; i++)
    {
        order.AddItem($"SKU-{i}", 1, 10.00m);
    }

    await repository.SaveAsync(order, CancellationToken.None);

    // Load should use snapshot + 1 event
    var loaded = await repository.GetByIdAsync(order.Id, CancellationToken.None);
    Assert.Equal(6, loaded!.Version);
    Assert.Equal(5, loaded.Items.Count);
}
```

### Loading from Snapshot

```csharp
[Fact]
public async Task Load_from_snapshot_produces_same_state()
{
    // Create aggregate with many events
    var order = Order.Create("customer-123");
    for (int i = 0; i < 20; i++)
    {
        order.AddItem($"SKU-{i}", 1, 10.00m);
    }
    await _repository.SaveAsync(order, CancellationToken.None);

    // Force snapshot
    await _snapshotStore.SaveAsync(order, CancellationToken.None);

    // Load (should use snapshot)
    var loaded = await _repository.GetByIdAsync(order.Id, CancellationToken.None);

    // Verify state matches
    Assert.Equal(order.Id, loaded!.Id);
    Assert.Equal(order.Version, loaded.Version);
    Assert.Equal(order.Items.Count, loaded.Items.Count);
    Assert.Equal(order.TotalAmount, loaded.TotalAmount);
}
```

## Testing Event Stream

### Reading Raw Events

```csharp
[Fact]
public async Task Can_read_event_stream()
{
    var order = Order.Create("customer-123");
    order.AddItem("SKU-001", 1, 10.00m);
    order.Submit();
    await _repository.SaveAsync(order, CancellationToken.None);

    var events = await _eventStore.LoadEventsAsync(
        order.Id.Value.ToString(),
        fromVersion: 0,
        CancellationToken.None);

    Assert.Equal(3, events.Count);
    Assert.IsType<OrderCreated>(events[0]);
    Assert.IsType<OrderItemAdded>(events[1]);
    Assert.IsType<OrderSubmitted>(events[2]);
}
```

## Test Utilities

### Test Data Builders

Create builders for complex test scenarios:

```csharp
public class OrderBuilder
{
    private string _customerId = "default-customer";
    private readonly List<(string Sku, int Qty, decimal Price)> _items = new();
    private bool _submitted;

    public OrderBuilder WithCustomer(string customerId)
    {
        _customerId = customerId;
        return this;
    }

    public OrderBuilder WithItem(string sku, int qty, decimal price)
    {
        _items.Add((sku, qty, price));
        return this;
    }

    public OrderBuilder Submitted()
    {
        _submitted = true;
        return this;
    }

    public Order Build()
    {
        var order = Order.Create(_customerId);
        foreach (var (sku, qty, price) in _items)
        {
            order.AddItem(sku, qty, price);
        }
        if (_submitted) order.Submit();
        return order;
    }
}

// Usage
var order = new OrderBuilder()
    .WithCustomer("C1")
    .WithItem("SKU-001", 2, 29.99m)
    .WithItem("SKU-002", 1, 49.99m)
    .Submitted()
    .Build();
```

### Shared Test Fixtures (xUnit)

```csharp
public class DatabaseFixture : IAsyncLifetime
{
    public MsSqlContainer Container { get; } = new MsSqlBuilder().Build();
    public ServiceProvider Provider { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await Container.StartAsync();

        var services = new ServiceCollection();
        services.AddDispatch();
        services.AddExcaliburEventSourcing(builder =>
        {
            builder.AddRepository<Order, OrderId>();
        });
        services.AddSqlServerEventSourcing(Container.GetConnectionString());
        Provider = services.BuildServiceProvider();
    }

    public async Task DisposeAsync()
    {
        Provider.Dispose();
        await Container.DisposeAsync();
    }
}

[CollectionDefinition("Database")]
public class DatabaseCollection : ICollectionFixture<DatabaseFixture> { }

[Collection("Database")]
public class OrderRepositoryTests
{
    private readonly DatabaseFixture _fixture;

    public OrderRepositoryTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }
}
```

## Best Practices

| Practice | Reason |
|----------|--------|
| Use in-memory for speed | Most tests don't need real database |
| Use TestContainers for fidelity | Some behaviors differ between providers |
| Clean up between tests | Prevent test pollution |
| Test concurrency explicitly | Don't assume optimistic concurrency works |
| Verify roundtrip fidelity | Ensure all state survives serialization |

## See Also

- [Testing Overview](index.md) - Testing strategy and framework conventions
- [Integration Tests](integration-tests.md) - Full system integration testing
- [Event Sourcing Repositories](../event-sourcing/repositories.md) - Repository patterns for event-sourced aggregates
