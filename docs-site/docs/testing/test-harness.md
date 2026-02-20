---
sidebar_position: 5
title: Test Harness
description: Testing dispatch pipelines, handlers, and message flows with DispatchTestHarness
---

# Test Harness

## Before You Start

- .NET 8.0 or later
- Install the testing package:

  ```bash
  dotnet add package Excalibur.Dispatch.Testing
  ```

- Familiarity with the [Dispatch pipeline](../core-concepts/actions-and-handlers.md) and [middleware](../middleware/index.md)

## Overview

`DispatchTestHarness` is the central entry point for testing Dispatch pipelines, handlers, and message flows. It builds a real DI container with the full Dispatch pipeline, registers a tracking middleware to capture every dispatched message, and exposes the results for assertions.

Key characteristics:

- **Framework-agnostic** -- works with xUnit, NUnit, MSTest, or any .NET test runner.
- **Lazy-build pattern** -- call `ConfigureServices` and `ConfigureDispatch` to accumulate configuration. The service provider builds on first access to `Dispatcher` or `Services`.
- **Automatic tracking** -- a `TestTrackingMiddleware` is registered at `DispatchMiddlewareStage.Start` to record all dispatched messages into `Dispatched`.
- **Async disposable** -- implements `IAsyncDisposable` for clean teardown.

## Quick Start

```csharp
using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Testing;
using Excalibur.Dispatch.Testing.Tracking;

// Defined in: Excalibur.Dispatch.Testing/DispatchTestHarness.cs
await using var harness = new DispatchTestHarness()
    .ConfigureServices(services =>
    {
        services.AddSingleton<IOrderRepository, InMemoryOrderRepository>();
    })
    .ConfigureDispatch(dispatch =>
    {
        dispatch.AddHandlersFromAssembly(typeof(CreateOrderHandler).Assembly);
    });

var context = new MessageContextBuilder().Build();
var result = await harness.Dispatcher.DispatchAsync(
    new CreateOrderAction("customer-123", ["item-a"]),
    context,
    CancellationToken.None);

harness.Dispatched.Any<CreateOrderAction>().ShouldBeTrue();
```

## Configuration

### ConfigureServices

Register dependencies, replace services with test doubles, or add logging:

```csharp
// Defined in: Excalibur.Dispatch.Testing/DispatchTestHarness.cs
var harness = new DispatchTestHarness()
    .ConfigureServices(services =>
    {
        // Register your application dependencies
        services.AddSingleton<IOrderRepository, InMemoryOrderRepository>();
        services.AddSingleton<IEmailService>(A.Fake<IEmailService>());

        // Override the default NullLoggerFactory if you need log output
        services.AddLogging(builder => builder.AddConsole());
    });
```

By default, `NullLoggerFactory` and `NullLogger<T>` are registered. Your registrations run after `AddDispatch`, so you can override any default service.

### ConfigureDispatch

Configure the Dispatch pipeline -- register handlers, add middleware, configure serialization:

```csharp
var harness = new DispatchTestHarness()
    .ConfigureDispatch(dispatch =>
    {
        dispatch.AddHandlersFromAssembly(typeof(CreateOrderHandler).Assembly);
    });
```

Multiple calls to `ConfigureDispatch` and `ConfigureServices` accumulate. All lambdas run in order when the container builds.

### Lazy Build Behavior

The service provider does not build until you first access `Dispatcher` or `Services`. This means configuration and build are separated:

```csharp
var harness = new DispatchTestHarness();

// These accumulate configuration -- no build yet
harness.ConfigureServices(services => services.AddSingleton<IMyService, MyService>());
harness.ConfigureDispatch(dispatch => dispatch.AddHandlersFromAssembly(typeof(MyHandler).Assembly));

// First access triggers the build
var dispatcher = harness.Dispatcher; // builds here

// Cannot configure after build -- throws InvalidOperationException
// harness.ConfigureServices(services => { }); // WRONG: already built
```

If you need to resolve services directly:

```csharp
var repository = harness.Services.GetRequiredService<IOrderRepository>();
```

## Asserting Messages

### IDispatchedMessageLog

Every message dispatched through the harness is recorded in `harness.Dispatched`. The log is thread-safe and backed by a `ConcurrentQueue`.

```csharp
// Defined in: Excalibur.Dispatch.Testing/Tracking/IDispatchedMessageLog.cs
IDispatchedMessageLog log = harness.Dispatched;
```

Each entry is a `DispatchedMessage` record containing:

| Property    | Type                | Description                          |
|-------------|---------------------|--------------------------------------|
| `Message`   | `IDispatchMessage`  | The dispatched message instance      |
| `Context`   | `IMessageContext`   | The message context at dispatch time |
| `Timestamp` | `DateTimeOffset`    | When the dispatch occurred (UTC)     |
| `Result`    | `IMessageResult?`   | The pipeline result, if available    |

### Filtering by Type

```csharp
// Check if any message of a specific type was dispatched
bool wasDispatched = harness.Dispatched.Any<CreateOrderAction>();

// Get all dispatched messages of a specific type
IReadOnlyList<DispatchedMessage> creates = harness.Dispatched.Select<CreateOrderAction>();

// Access the underlying message
CreateOrderAction action = (CreateOrderAction)creates[0].Message;
action.CustomerId.ShouldBe("customer-123");
```

### Counting Messages

```csharp
// Total dispatched messages
int total = harness.Dispatched.Count;

// All messages in chronological order
IReadOnlyList<DispatchedMessage> all = harness.Dispatched.All;
```

### Clearing Between Tests

If you reuse a harness across multiple test methods, clear the log between tests:

```csharp
harness.Dispatched.Clear();
```

## MessageContextBuilder

`MessageContextBuilder` creates `IMessageContext` instances with sensible defaults. Unset properties get auto-generated values (GUID for `MessageId` and `CorrelationId`, UTC timestamp for `ReceivedTimestampUtc`).

### Fluent API

```csharp
// Defined in: Excalibur.Dispatch.Testing/MessageContextBuilder.cs
var context = new MessageContextBuilder()
    .WithCorrelationId("corr-001")
    .WithTenantId("acme-corp")
    .WithUserId("user-42")
    .WithSource("OrderService")
    .Build();

context.CorrelationId.ShouldBe("corr-001");
context.TenantId.ShouldBe("acme-corp");
```

### Available Methods

| Method                    | Description                                          |
|---------------------------|------------------------------------------------------|
| `WithMessageId`           | Sets message ID (default: new GUID)                  |
| `WithCorrelationId`       | Sets correlation ID (default: new GUID)              |
| `WithCausationId`         | Sets causation ID                                    |
| `WithTenantId`            | Sets tenant ID                                       |
| `WithUserId`              | Sets user ID                                         |
| `WithSessionId`           | Sets session ID                                      |
| `WithWorkflowId`          | Sets workflow ID                                     |
| `WithPartitionKey`        | Sets partition key                                   |
| `WithSource`              | Sets the source identifier                           |
| `WithMessageType`         | Sets the message type                                |
| `WithContentType`         | Sets the content type                                |
| `WithTraceParent`         | Sets the W3C trace parent                            |
| `WithExternalId`          | Sets the external ID                                 |
| `WithDeliveryCount`       | Sets the delivery count                              |
| `WithRequestServices`     | Sets the `IServiceProvider` for resolving services   |
| `WithMessage`             | Attaches an `IDispatchMessage` to the context        |
| `WithItem`                | Adds a custom key-value item to the context          |

### Custom Items

Pass arbitrary data through the context using `WithItem`:

```csharp
var context = new MessageContextBuilder()
    .WithItem("FeatureFlags", new Dictionary<string, bool> { ["NewPricing"] = true })
    .WithItem("RequestId", "req-abc-123")
    .Build();

context.GetItem<string>("RequestId").ShouldBe("req-abc-123");
```

## Complete Example

A full test showing handler registration, dispatching, and assertion:

```csharp
using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Testing;
using Excalibur.Dispatch.Testing.Tracking;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

// A simple command and handler
public record PlaceOrderAction(string CustomerId, decimal Total) : IDispatchAction;

public class PlaceOrderHandler : IActionHandler<PlaceOrderAction>
{
    private readonly IOrderRepository _repository;

    public PlaceOrderHandler(IOrderRepository repository) => _repository = repository;

    public async Task HandleAsync(PlaceOrderAction action, CancellationToken cancellationToken)
    {
        var order = new Order(action.CustomerId, action.Total);
        await _repository.SaveAsync(order, cancellationToken);
    }
}

// The test
public class PlaceOrderHandlerShould : IAsyncDisposable
{
    private readonly DispatchTestHarness _harness;
    private readonly IOrderRepository _repository;

    public PlaceOrderHandlerShould()
    {
        _repository = A.Fake<IOrderRepository>();

        _harness = new DispatchTestHarness()
            .ConfigureServices(services =>
            {
                services.AddSingleton(_repository);
            })
            .ConfigureDispatch(dispatch =>
            {
                dispatch.AddHandlersFromAssembly(typeof(PlaceOrderHandler).Assembly);
            });
    }

    [Fact]
    public async Task Dispatch_place_order_and_track_message()
    {
        // Arrange
        var context = new MessageContextBuilder()
            .WithUserId("user-42")
            .WithTenantId("acme-corp")
            .Build();

        // Act
        await _harness.Dispatcher.DispatchAsync(
            new PlaceOrderAction("customer-123", 99.99m),
            context,
            CancellationToken.None);

        // Assert -- message was tracked
        _harness.Dispatched.Any<PlaceOrderAction>().ShouldBeTrue();
        _harness.Dispatched.Count.ShouldBe(1);

        // Assert -- handler was invoked
        A.CallTo(() => _repository.SaveAsync(
            A<Order>.That.Matches(o => o.CustomerId == "customer-123"),
            A<CancellationToken>._))
            .MustHaveHappenedOnceExactly();
    }

    public ValueTask DisposeAsync() => _harness.DisposeAsync();
}
```

:::tip
When testing with FakeItEasy, register fakes via `ConfigureServices` so they are resolved by the DI container during handler construction.
:::

## See Also

- [Testing Dispatch Handlers](./testing-handlers.md) -- Unit testing handlers without the harness
- [Transport Test Doubles](./transport-test-doubles.md) -- In-memory transport implementations
- [Shouldly Assertions](./shouldly-assertions.md) -- Fluent assertion extensions for Dispatch types
- [Dependency Injection](../core-concepts/dependency-injection.md) -- Registering services and the DI container the harness builds on
- [Actions and Handlers](../core-concepts/actions-and-handlers.md) -- Handler interface reference
