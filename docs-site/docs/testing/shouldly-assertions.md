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

Nine extension methods provide fluent assertions for `IDispatchedMessageLog`, `InMemoryTransportSender`, and `InMemoryTransportReceiver`. All methods are defined in `DispatchTestingShouldlyExtensions`.

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

## See Also

- [Test Harness](./test-harness.md) -- DispatchTestHarness and IDispatchedMessageLog
- [Testing Dispatch Handlers](./testing-handlers.md) -- Unit testing handlers with Shouldly assertions
- [Transport Test Doubles](./transport-test-doubles.md) -- InMemoryTransportSender, Receiver, and Subscriber
