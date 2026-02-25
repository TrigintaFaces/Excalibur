---
sidebar_position: 4
title: Integration Tests
description: End-to-end testing with real infrastructure using TestContainers
---

# Integration Testing

Integration tests verify that all components work together with real infrastructure. Use TestContainers to spin up real databases in Docker for high-fidelity testing.

## Before You Start

- **.NET 8.0+** (or .NET 9/10 for latest features)
- **Docker** installed and running (required for TestContainers)
- Install the required packages:
  ```bash
  dotnet add package Excalibur.Dispatch.Testing
  dotnet add package Testcontainers.MsSql      # SQL Server
  dotnet add package Testcontainers.Postgres # Postgres
  dotnet add package Testcontainers.MongoDb    # MongoDB
  dotnet add package Testcontainers.CosmosDb   # CosmosDB Emulator
  ```
- Familiarity with [test harness](./test-harness.md) and [testing handlers](./testing-handlers.md)

## SQL Server Integration Tests

```csharp
using Testcontainers.MsSql;
using Xunit;

public class SqlServerIntegrationTests : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    private ServiceProvider _provider = null!;

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
        services.AddExcaliburOutbox(outbox =>
        {
            outbox.UseSqlServer(_container.GetConnectionString());
        });

        _provider = services.BuildServiceProvider();
    }

    public async Task DisposeAsync()
    {
        _provider.Dispose();
        await _container.DisposeAsync();
    }

    [Fact]
    public async Task Full_aggregate_lifecycle()
    {
        var repository = _provider.GetRequiredService<IEventSourcedRepository<Order, OrderId>>();

        // Create
        var order = Order.Create("customer-123");
        order.AddItem("SKU-001", 2, 29.99m);
        await repository.SaveAsync(order, CancellationToken.None);

        // Modify
        var loaded = await repository.GetByIdAsync(order.Id, CancellationToken.None);
        loaded!.Submit();
        await repository.SaveAsync(loaded, CancellationToken.None);

        // Verify
        var final = await repository.GetByIdAsync(order.Id, CancellationToken.None);
        Assert.Equal(OrderStatus.Submitted, final!.Status);
        Assert.Equal(3, final.Version);
    }
}
```

## Postgres Integration Tests

```csharp
using Testcontainers.Postgres;

public class PostgresIntegrationTests : IAsyncLifetime
{
    private readonly PostgresContainer _container = new PostgresBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var services = new ServiceCollection();
        services.AddDispatch();
        services.AddExcaliburEventSourcing(builder =>
        {
            builder.AddRepository<Order, OrderId>();
        });
        services.AddPostgresEventStore(_container.GetConnectionString());

        // ... setup
    }

    // ... tests
}
```

## MongoDB Integration Tests

```csharp
using Testcontainers.MongoDb;

public class MongoDbIntegrationTests : IAsyncLifetime
{
    private readonly MongoDbContainer _container = new MongoDbBuilder()
        .WithImage("mongo:7.0")
        .Build();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        var services = new ServiceCollection();
        services.AddDispatch();
        services.AddExcaliburEventSourcing(builder =>
        {
            builder.AddRepository<Order, OrderId>();
        });
        services.AddMongoDbEventStore(_container.GetConnectionString(), "test-db");

        // ... setup
    }
}
```

## Testing the Full Pipeline

### Handler Integration Test

```csharp
[Fact]
public async Task CreateOrder_handler_persists_aggregate()
{
    var dispatcher = _provider.GetRequiredService<IDispatcher>();
    var repository = _provider.GetRequiredService<IEventSourcedRepository<Order, OrderId>>();

    // Dispatch command through full pipeline
    var command = new CreateOrderCommand { CustomerId = "C1" };
    var result = await dispatcher.DispatchAsync(command, CancellationToken.None);

    // Verify result
    Assert.True(result.IsSuccess);

    // Verify persistence
    var order = await repository.GetByIdAsync(result.ReturnValue.OrderId, CancellationToken.None);
    Assert.NotNull(order);
    Assert.Equal("C1", order.CustomerId);
}
```

### Outbox Integration Test

```csharp
[Fact]
public async Task Events_published_through_outbox()
{
    var repository = _provider.GetRequiredService<IEventSourcedRepository<Order, OrderId>>();
    var outboxStore = _provider.GetRequiredService<IOutboxStore>();

    // Create and save aggregate
    var order = Order.Create("customer-123");
    await repository.SaveAsync(order, CancellationToken.None);

    // Verify outbox has pending messages
    var pending = await outboxStore.GetPendingAsync(10, CancellationToken.None);
    Assert.Contains(pending, m => m.Type == nameof(OrderCreated));
}
```

### Projection Integration Test

```csharp
[Fact]
public async Task Projection_updates_on_event()
{
    var repository = _provider.GetRequiredService<IEventSourcedRepository<Order, OrderId>>();
    var projectionStore = _provider.GetRequiredService<IProjectionStore<OrderSummary>>();
    var projector = _provider.GetRequiredService<OrderSummaryProjector>();

    // Create order
    var order = Order.Create("customer-123");
    order.AddItem("SKU-001", 2, 29.99m);
    await repository.SaveAsync(order, CancellationToken.None);

    // Process projection
    await projector.ProcessAsync(CancellationToken.None);

    // Verify projection
    var summary = await projectionStore.GetByIdAsync(order.Id.Value.ToString(), CancellationToken.None);
    Assert.NotNull(summary);
    Assert.Equal(59.98m, summary.TotalAmount);
}
```

## Saga Integration Tests

```csharp
[Fact]
public async Task Saga_completes_order_fulfillment()
{
    var dispatcher = _provider.GetRequiredService<IDispatcher>();
    var sagaStore = _provider.GetRequiredService<ISagaStore>();

    // Start saga
    var command = new StartOrderFulfillmentCommand { OrderId = "123" };
    await dispatcher.DispatchAsync(command, CancellationToken.None);

    // Simulate external events
    await dispatcher.DispatchAsync(new PaymentReceived { OrderId = "123" }, CancellationToken.None);
    await dispatcher.DispatchAsync(new InventoryReserved { OrderId = "123" }, CancellationToken.None);

    // Verify saga completed
    var sagaId = Guid.Parse("00000000-0000-0000-0000-000000000123");
    var saga = await sagaStore.LoadAsync<OrderFulfillmentSaga>(sagaId, CancellationToken.None);
    Assert.True(saga!.Completed);
}
```

## Test Organization

### Shared Fixtures

Create a shared fixture for expensive setup:

```csharp
public class IntegrationTestFixture : IAsyncLifetime
{
    public MsSqlContainer SqlServer { get; } = new MsSqlBuilder().Build();
    public ServiceProvider Services { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await SqlServer.StartAsync();

        var serviceCollection = new ServiceCollection();
        ConfigureServices(serviceCollection);
        Services = serviceCollection.BuildServiceProvider();

        await MigrateAsync();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        services.AddDispatch();
        services.AddExcaliburEventSourcing(builder =>
        {
            builder.AddRepository<Order, OrderId>();
        });
        services.AddSqlServerEventSourcing(SqlServer.GetConnectionString());
        services.AddExcaliburOutbox(outbox =>
        {
            outbox.UseSqlServer(SqlServer.GetConnectionString());
        });
        // ... other services
    }

    public async Task DisposeAsync()
    {
        Services.Dispose();
        await SqlServer.DisposeAsync();
    }
}

[CollectionDefinition("Integration")]
public class IntegrationCollection : ICollectionFixture<IntegrationTestFixture> { }
```

### Test Isolation

Reset database state between tests:

```csharp
[Collection("Integration")]
public class OrderIntegrationTests : IAsyncLifetime
{
    private readonly IntegrationTestFixture _fixture;

    public OrderIntegrationTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        // Clean up test data
        await using var connection = new SqlConnection(_fixture.SqlServer.GetConnectionString());
        await connection.ExecuteAsync("DELETE FROM Events; DELETE FROM Outbox;");
    }
}
```

## CI/CD Configuration

The main CI pipeline runs integration tests as a dedicated gate after unit shards pass.

Current gate command shape:

```bash
dotnet test Excalibur.sln \
  --configuration Release \
  --no-build \
  --blame-hang-timeout 5m \
  --filter "Category=Integration|Category=EndToEnd" \
  -- RunConfiguration.TestSessionTimeout=1200000
```

Related test gates in the same workflow:

- Unit tests (6 shard filters + Windows cross-platform unit pass)
- Functional tests (`Category=Functional`)
- Contract tests (project-scoped)
- Architecture and boundary tests (project-scoped)
- Transport conformance tests (project-scoped)

Recommended local reproduction sequence:

```bash
dotnet restore Excalibur.sln
dotnet build Excalibur.sln -c Release --no-restore
dotnet test Excalibur.sln -c Release --no-build --filter "Category=Integration|Category=EndToEnd" -- RunConfiguration.TestSessionTimeout=1200000
```

## Best Practices

| Practice | Reason |
|----------|--------|
| Use TestContainers | Real behavior, no mocking |
| Share containers when possible | Faster test suites |
| Clean up between tests | Prevent test pollution |
| Run in CI with Docker | Consistent environments |
| Tag integration tests | Run separately from unit tests |
| Test error scenarios | Network failures, timeouts |

## Troubleshooting

### Container fails to start

- Ensure Docker is running
- Check available memory (SQL Server needs 2GB+)
- Verify port availability

### Tests timeout

- Increase container startup timeout
- Use `WithWaitStrategy` for readiness

```csharp
var container = new MsSqlBuilder()
    .WithWaitStrategy(Wait.ForUnixContainer()
        .UntilPortIsAvailable(1433)
        .UntilMessageIsLogged("SQL Server is now ready"))
    .Build();
```

### Flaky tests

- Add retry policies for transient failures
- Ensure proper test isolation
- Check for race conditions in async code

## See Also

- [Test Harness](./test-harness.md) -- DispatchTestHarness for in-memory pipeline testing without Docker
- [Transport Test Doubles](./transport-test-doubles.md) -- In-memory transport implementations for fast, isolated tests
- [Testing Dispatch Handlers](./testing-handlers.md) -- Unit testing handlers and middleware
- [Docker Deployment](../deployment/docker.md) -- Containerization guide for production deployments
