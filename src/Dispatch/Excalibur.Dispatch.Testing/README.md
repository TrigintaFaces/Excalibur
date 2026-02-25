# Excalibur.Dispatch.Testing

Test infrastructure for the Dispatch messaging pipeline. Provides a lightweight, framework-agnostic test harness with message tracking, fluent builders, and spy middleware.

## Installation

```bash
dotnet add package Excalibur.Dispatch.Testing
```

## Features

- **DispatchTestHarness** - Lazy-build DI container for testing Dispatch pipelines
- **MessageContextBuilder** - Fluent API for creating test message contexts
- **IDispatchedMessageLog/DispatchedMessageLog** - In-memory message tracking
- **TestTrackingMiddleware** - Spy middleware for observing pipeline execution
- **Framework-agnostic** - Works with xUnit, NUnit, MSTest, or any test framework

## Quick Start

```csharp
// Create harness and configure pipeline
var harness = new DispatchTestHarness();
harness.ConfigureDispatch(d => d.AddHandlersFromAssembly(typeof(MyHandler).Assembly));

// Dispatch messages
await harness.Dispatcher.DispatchAsync(new MyCommand(), CancellationToken.None);

// Verify dispatched messages
harness.Dispatched.Count.ShouldBe(1);
harness.Dispatched.All[0].Message.ShouldBeOfType<MyCommand>();

// Clean up
await harness.DisposeAsync();
```

## DispatchTestHarness

Lazy-build service provider that accumulates DI registrations and builds the container on first access.

```csharp
var harness = new DispatchTestHarness();

// Configure services (accumulates until first access)
harness.ConfigureServices(services =>
{
    services.AddSingleton<IMyService, MyService>();
});

// Configure Dispatch pipeline
harness.ConfigureDispatch(d =>
{
    d.AddHandlersFromAssembly(typeof(MyHandler).Assembly);
    d.AddMiddleware<ValidationMiddleware>();
});

// ServiceProvider built on first property access
var dispatcher = harness.Dispatcher;
var log = harness.Dispatched;
```

## MessageContextBuilder

Fluent API for creating test message contexts with custom properties, cancellation tokens, and metadata.

```csharp
var context = new MessageContextBuilder()
    .WithMessage(new MyCommand { Id = 123 })
    .WithUserId("user-123")
    .WithTenantId("tenant-42")
    .WithCorrelationId("corr-abc")
    .Build();

await dispatcher.DispatchAsync(context);
```

## IDispatchedMessageLog

In-memory log that captures all messages dispatched during tests.

```csharp
// Access via harness
var log = harness.Dispatched;

// Query dispatched messages
log.Count.ShouldBe(3);
log.Select<OrderCreated>().Count.ShouldBe(1);
log.All.Count(m => m.Message is OrderUpdated).ShouldBe(2);

// Clear log between test phases
log.Clear();
```

## TestTrackingMiddleware

Spy middleware automatically registered by `DispatchTestHarness`. Captures all messages passing through the pipeline.

```csharp
// Automatically registered - no manual setup needed
var harness = new DispatchTestHarness();
harness.ConfigureDispatch(d => d.AddHandlersFromAssembly(typeof(MyHandler).Assembly));

// Middleware captures messages as they flow through pipeline
await harness.Dispatcher.DispatchAsync(new MyCommand());

// Verify via Dispatched log
harness.Dispatched.All.ShouldContain(m => m.Message is MyCommand);
```

## Integration with Testing.Shouldly

For fluent assertions, add the companion package:

```bash
dotnet add package Excalibur.Dispatch.Testing.Shouldly
```

```csharp
// Fluent Shouldly extensions
harness.Dispatched.ShouldHaveDispatched<OrderCreated>();
harness.Dispatched.ShouldNotHaveDispatched<OrderCancelled>();
sender.ShouldHaveSent(3);
receiver.ShouldHaveAcknowledged(2);
```

See [Excalibur.Dispatch.Testing.Shouldly](../Excalibur.Dispatch.Testing.Shouldly/README.md) for details.

## Complete Example

```csharp
public class OrderHandlerTests : IAsyncDisposable
{
    private readonly DispatchTestHarness _harness = new();

    public OrderHandlerTests()
    {
        _harness.ConfigureServices(services =>
        {
            services.AddSingleton<IOrderRepository, InMemoryOrderRepository>();
        });

        _harness.ConfigureDispatch(d =>
        {
            d.AddHandlersFromAssembly(typeof(OrderHandler).Assembly);
        });
    }

    [Fact]
    public async Task CreateOrder_ShouldDispatchOrderCreatedEvent()
    {
        // Arrange
        var command = new CreateOrderCommand { ProductId = 123, Quantity = 2 };

        // Act
        await _harness.Dispatcher.DispatchAsync(command, CancellationToken.None);

        // Assert
        _harness.Dispatched.Count.ShouldBe(1);
        var evt = _harness.Dispatched.All[0].Message.ShouldBeOfType<OrderCreated>();
        evt.ProductId.ShouldBe(123);
        evt.Quantity.ShouldBe(2);
    }

    public async ValueTask DisposeAsync()
    {
        await _harness.DisposeAsync();
    }
}
```

## License

MIT

