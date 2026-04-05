---
sidebar_position: 7
title: Shouldly Assertions
description: Fluent Shouldly assertion extensions for Dispatch testing types
---

# Shouldly Assertions

## Before You Start

- Install **both** testing packages:

  ```bash
  dotnet add package Excalibur.Dispatch.Testing
  dotnet add package Excalibur.Dispatch.Testing.Shouldly
  dotnet add package Shouldly
  ```

- `Excalibur.Dispatch.Testing.Shouldly` is a **separate package** from `Excalibur.Dispatch.Testing`. The Shouldly extensions are kept separate so projects that prefer a different assertion library do not take a dependency on Shouldly.

- Both packages share the `Excalibur.Dispatch.Testing` namespace, so the extension methods are automatically available once the package is installed.

## Overview

Seventeen extension methods provide fluent assertions for `IDispatchedMessageLog`, `InMemoryTransportSender`, `InMemoryTransportReceiver`, `IMessageResult`, `DispatchTestHarness`, `ISaga`, and `ISaga<TSagaState>`. All methods are defined in `DispatchTestingShouldlyExtensions`.

```csharp
// Defined in: Excalibur.Dispatch.Testing.Shouldly/DispatchTestingShouldlyExtensions.cs
// Namespace: Excalibur.Dispatch.Testing
```

## Dispatch Message Assertions

These methods operate on `IDispatchedMessageLog` (accessible via `harness.Dispatched`).

### ShouldHaveDispatched&lt;T&gt;()

Asserts that at least one message of type `T` was dispatched:

```csharp
harness.Dispatched.ShouldHaveDispatched<CreateOrderAction>();
```

Failure message: `"Expected at least one CreateOrderAction to be dispatched, but none were found."`

### ShouldHaveDispatched&lt;T&gt;(count)

Asserts that exactly `count` messages of type `T` were dispatched:

```csharp
harness.Dispatched.ShouldHaveDispatched<CreateOrderAction>(2);
```

Failure message: `"Expected 2 CreateOrderAction message(s) to be dispatched."`

### ShouldNotHaveDispatched&lt;T&gt;()

Asserts that no messages of type `T` were dispatched:

```csharp
harness.Dispatched.ShouldNotHaveDispatched<CancelOrderAction>();
```

Failure message: `"Expected no CancelOrderAction to be dispatched, but found 1."`

### ShouldHaveDispatchedCount(count)

Asserts that the total number of dispatched messages (all types) equals `count`:

```csharp
harness.Dispatched.ShouldHaveDispatchedCount(3);
```

## Transport Sender Assertions

These methods operate on `InMemoryTransportSender`.

### ShouldHaveSent(count)

Asserts that exactly `count` messages were sent through the sender:

```csharp
sender.ShouldHaveSent(2);
```

### ShouldHaveSentTo(destination)

Asserts that the sender is configured for the specified destination:

```csharp
sender.ShouldHaveSentTo("orders-topic");
```

This checks the `Destination` property, not individual message destinations.

### ShouldHaveSentMessageMatching(predicate)

Asserts that at least one sent message matches the predicate:

```csharp
sender.ShouldHaveSentMessageMatching(m => m.CorrelationId == "corr-001");

sender.ShouldHaveSentMessageMatching(m =>
    m.ContentType == "application/json" &&
    m.Subject == "OrderCreated");
```

## Transport Receiver Assertions

These methods operate on `InMemoryTransportReceiver`.

### ShouldHaveAcknowledged(count)

Asserts that exactly `count` messages were acknowledged:

```csharp
receiver.ShouldHaveAcknowledged(1);
```

### ShouldHaveRejected(count)

Asserts that exactly `count` messages were rejected:

```csharp
receiver.ShouldHaveRejected(0);
```

## Message Result Assertions

These methods operate on `IMessageResult`.

### ShouldHaveCompleted()

Asserts that the result indicates success:

```csharp
var result = await harness.Dispatcher.DispatchAsync<PlaceOrderAction, OrderDto>(action, ct);
result.ShouldHaveCompleted();
```

### ShouldHaveFailed()

Asserts that the result indicates failure:

```csharp
var result = await harness.Dispatcher.DispatchAsync<PlaceOrderAction, OrderDto>(action, ct);
result.ShouldHaveFailed();
```

### ShouldHaveFailedWithError(substring?)

Asserts failure with a non-null error message, optionally checking for a substring:

```csharp
result.ShouldHaveFailedWithError();
result.ShouldHaveFailedWithError("not found");
```

## Test Harness Assertions

Shorthand methods on `DispatchTestHarness` that delegate to `Dispatched`.

### ShouldHavePublished&lt;T&gt;()

Asserts that at least one message of type `T` was dispatched through the harness:

```csharp
harness.ShouldHavePublished<OrderCreatedEvent>();
```

### ShouldHavePublished&lt;T&gt;(count)

Asserts exactly `count` messages of type `T` were dispatched:

```csharp
harness.ShouldHavePublished<OrderCreatedEvent>(2);
```

## Saga Assertions

These methods operate on `ISaga` and `ISaga<TSagaState>`.

### SagaShouldBeCompleted()

Asserts that the saga has completed:

```csharp
saga.SagaShouldBeCompleted();
```

### SagaShouldBeActive()

Asserts that the saga is still active (not completed):

```csharp
saga.SagaShouldBeActive();
```

### SagaShouldBeInState(bool)

Asserts the saga's completion state matches the expected value:

```csharp
saga.SagaShouldBeInState(expectedCompleted: true);
```

### SagaShouldHaveState&lt;TSagaState&gt;(predicate)

Asserts that the typed saga state matches a predicate:

```csharp
saga.SagaShouldHaveState<OrderSagaState>(s => s.OrderId == expectedOrderId);
```

## Complete Example

A test combining the harness, transport test double, and Shouldly assertions:

```csharp
using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Testing;
using Excalibur.Dispatch.Testing.Transport;
using Excalibur.Dispatch.Transport;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

public class OrderFlowShould : IAsyncDisposable
{
    private readonly DispatchTestHarness _harness;
    private readonly InMemoryTransportSender _sender;

    public OrderFlowShould()
    {
        _sender = new InMemoryTransportSender("orders-topic");

        _harness = new DispatchTestHarness()
            .ConfigureServices(services =>
            {
                services.AddSingleton<ITransportSender>(_sender);
            })
            .ConfigureDispatch(dispatch =>
            {
                dispatch.AddHandlersFromAssembly(typeof(PlaceOrderHandler).Assembly);
            });
    }

    [Fact]
    public async Task Place_order_dispatches_and_sends_to_transport()
    {
        // Arrange
        var context = new MessageContextBuilder()
            .WithCorrelationId("corr-001")
            .Build();

        // Act
        await _harness.Dispatcher.DispatchAsync(
            new PlaceOrderAction("customer-123", 49.99m),
            context,
            CancellationToken.None);

        // Assert with Shouldly extensions
        _harness.Dispatched.ShouldHaveDispatched<PlaceOrderAction>();
        _harness.Dispatched.ShouldHaveDispatchedCount(1);
        _harness.Dispatched.ShouldNotHaveDispatched<CancelOrderAction>();

        _sender.ShouldHaveSentTo("orders-topic");
        _sender.ShouldHaveSent(1);
        _sender.ShouldHaveSentMessageMatching(m => m.CorrelationId == "corr-001");
    }

    public async ValueTask DisposeAsync()
    {
        await _harness.DisposeAsync();
        await _sender.DisposeAsync();
    }
}
```

## Comparison: Shouldly vs Raw Assertions

The Shouldly extensions reduce boilerplate and produce better failure messages:

**With Shouldly extensions:**

```csharp
harness.Dispatched.ShouldHaveDispatched<CreateOrderAction>(2);
sender.ShouldHaveSentMessageMatching(m => m.CorrelationId == "corr-001");
receiver.ShouldHaveAcknowledged(1);
```

**Without (raw assertions):**

```csharp
harness.Dispatched.Select<CreateOrderAction>().Count.ShouldBe(2);
sender.SentMessages.ShouldContain(m => m.CorrelationId == "corr-001");
receiver.AcknowledgedMessages.Count.ShouldBe(1);
```

Both approaches work. The extensions provide domain-specific failure messages (e.g., `"Expected at least one CreateOrderAction to be dispatched"` instead of `"Expected True but was False"`).

## API Reference

| Method | Target Type | Description |
|--------|-------------|-------------|
| `ShouldHaveDispatched<T>()` | `IDispatchedMessageLog` | At least one `T` dispatched |
| `ShouldHaveDispatched<T>(int)` | `IDispatchedMessageLog` | Exactly N of type `T` dispatched |
| `ShouldNotHaveDispatched<T>()` | `IDispatchedMessageLog` | Zero of type `T` dispatched |
| `ShouldHaveDispatchedCount(int)` | `IDispatchedMessageLog` | Total dispatched count equals N |
| `ShouldHaveSent(int)` | `InMemoryTransportSender` | Exactly N messages sent |
| `ShouldHaveSentTo(string)` | `InMemoryTransportSender` | Sender destination matches |
| `ShouldHaveSentMessageMatching(Func)` | `InMemoryTransportSender` | At least one message matches predicate |
| `ShouldHaveAcknowledged(int)` | `InMemoryTransportReceiver` | Exactly N messages acknowledged |
| `ShouldHaveRejected(int)` | `InMemoryTransportReceiver` | Exactly N messages rejected |
| `ShouldHaveCompleted()` | `IMessageResult` | Result indicates success |
| `ShouldHaveFailed()` | `IMessageResult` | Result indicates failure |
| `ShouldHaveFailedWithError(string?)` | `IMessageResult` | Failure with error message (optional substring match) |
| `ShouldHavePublished<T>()` | `DispatchTestHarness` | Shorthand: at least one `T` dispatched |
| `ShouldHavePublished<T>(int)` | `DispatchTestHarness` | Shorthand: exactly N of type `T` dispatched |
| `SagaShouldBeCompleted()` | `ISaga` | Saga has completed |
| `SagaShouldBeActive()` | `ISaga` | Saga is still active |
| `SagaShouldBeInState(bool)` | `ISaga` | Saga completion state matches |
| `SagaShouldHaveState<T>(Func)` | `ISaga<TSagaState>` | Typed saga state matches predicate |

## See Also

- [Test Harness](./test-harness.md) -- DispatchTestHarness and IDispatchedMessageLog
- [Testing Dispatch Handlers](./testing-handlers.md) -- Unit testing handlers with Shouldly assertions
- [Transport Test Doubles](./transport-test-doubles.md) -- InMemoryTransportSender, Receiver, and Subscriber
