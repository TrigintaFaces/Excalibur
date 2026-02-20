# Excalibur.Dispatch.Testing.Shouldly

Shouldly assertion extensions for Excalibur testing. Provides fluent, readable assertions for message dispatch verification.

## Installation

```bash
dotnet add package Excalibur.Dispatch.Testing.Shouldly
```

**Requires:**
- `Excalibur.Dispatch.Testing` (core test infrastructure)
- `Shouldly` (assertion library)

## Features

- **9 extension methods** for IDispatchedMessageLog, InMemoryTransportSender, InMemoryTransportReceiver
- **Fluent assertions** aligned with Shouldly conventions
- **Clear failure messages** that describe what was expected vs. what actually happened
- **Same namespace as core Testing package** - no extra `using` needed

## Quick Start

```csharp
using Excalibur.Dispatch.Testing; // Extensions auto-discovered

// Arrange
var harness = new DispatchTestHarness();
harness.ConfigureDispatch(d => d.AddHandlersFromAssembly(typeof(MyHandler).Assembly));

// Act
await harness.Dispatcher.DispatchAsync(new OrderCreated { Id = 123 });

// Assert - fluent Shouldly extensions
harness.Dispatched.ShouldHaveDispatched<OrderCreated>();
harness.Dispatched.ShouldNotHaveDispatched<OrderCancelled>();
```

## IDispatchedMessageLog Extensions

Assert on messages captured during pipeline execution.

```csharp
// Verify message was dispatched
harness.Dispatched.ShouldHaveDispatched<OrderCreated>();

// Verify message was NOT dispatched
harness.Dispatched.ShouldNotHaveDispatched<OrderCancelled>();

// Verify exact count of a message type
harness.Dispatched.ShouldHaveDispatched<OrderCreated>(expectedCount: 2);

// Verify total dispatched count
harness.Dispatched.ShouldHaveDispatchedCount(3);
```

## InMemoryTransportSender Extensions

Assert on transport send operations.

```csharp
var sender = new InMemoryTransportSender();

// Send messages
await sender.SendAsync(new TransportMessage(...), CancellationToken.None);
await sender.SendAsync(new TransportMessage(...), CancellationToken.None);
await sender.SendAsync(new TransportMessage(...), CancellationToken.None);

// Verify send count
sender.ShouldHaveSent(3);

// Verify message was sent
sender.ShouldHaveSent(message);

// Verify message NOT sent
sender.ShouldNotHaveSent(message);
```

## InMemoryTransportReceiver Extensions

Assert on transport receive operations.

```csharp
var receiver = new InMemoryTransportReceiver();

// Queue and process messages
receiver.QueueMessage(new TransportMessage(...));
var message = await receiver.ReceiveAsync(CancellationToken.None);
await receiver.AcknowledgeAsync(message.MessageId, CancellationToken.None);

// Verify acknowledgments
receiver.ShouldHaveAcknowledged(1);
receiver.ShouldHaveAcknowledged(message.MessageId);

// Verify rejections
receiver.ShouldHaveRejected(0);
```

## Complete Example

```csharp
public class OrderWorkflowTests : IAsyncDisposable
{
    private readonly DispatchTestHarness _harness = new();
    private readonly InMemoryTransportSender _sender = new();

    public OrderWorkflowTests()
    {
        _harness.ConfigureServices(services =>
        {
            services.AddSingleton<ITransportSender>(_sender);
        });

        _harness.ConfigureDispatch(d =>
        {
            d.AddHandlersFromAssembly(typeof(OrderHandler).Assembly);
        });
    }

    [Fact]
    public async Task CreateOrder_ShouldPublishEvent()
    {
        // Arrange
        var command = new CreateOrderCommand { ProductId = 123, Quantity = 2 };

        // Act
        await _harness.Dispatcher.DispatchAsync(command, CancellationToken.None);

        // Assert - fluent Shouldly assertions
        _harness.Dispatched.ShouldHaveDispatched<OrderCreated>(
            evt => evt.ProductId == 123 && evt.Quantity == 2);

        _sender.ShouldHaveSent(1);
        _sender.ShouldHaveSent(msg =>
            msg.Body.Contains("OrderCreated") && msg.Body.Contains("123"));
    }

    [Fact]
    public async Task CancelOrder_ShouldNotPublishCreatedEvent()
    {
        // Arrange
        var command = new CancelOrderCommand { OrderId = 456 };

        // Act
        await _harness.Dispatcher.DispatchAsync(command, CancellationToken.None);

        // Assert
        _harness.Dispatched.ShouldNotHaveDispatched<OrderCreated>();
        _harness.Dispatched.ShouldHaveDispatched<OrderCancelled>();
    }

    public async ValueTask DisposeAsync()
    {
        await _harness.DisposeAsync();
    }
}
```

## Extension Methods Reference

| Method | Target | Description |
|--------|--------|-------------|
| `ShouldHaveDispatched<T>()` | IDispatchedMessageLog | Verify message of type T was dispatched |
| `ShouldHaveDispatched<T>(predicate)` | IDispatchedMessageLog | Verify message matching predicate |
| `ShouldHaveDispatched(message)` | IDispatchedMessageLog | Verify specific message instance |
| `ShouldNotHaveDispatched<T>()` | IDispatchedMessageLog | Verify message of type T was NOT dispatched |
| `ShouldHaveSent(count)` | InMemoryTransportSender | Verify exact send count |
| `ShouldHaveSent(message)` | InMemoryTransportSender | Verify specific message sent |
| `ShouldNotHaveSent(message)` | InMemoryTransportSender | Verify message NOT sent |
| `ShouldHaveAcknowledged(count)` | InMemoryTransportReceiver | Verify acknowledgment count |
| `ShouldHaveRejected(count)` | InMemoryTransportReceiver | Verify rejection count |

## Failure Messages

Shouldly extensions provide clear failure messages:

```csharp
// Assertion failure
harness.Dispatched.ShouldHaveDispatched<OrderCreated>();

// Output:
// harness.Dispatched
//     should have dispatched
// Excalibur.Dispatch.Messages.OrderCreated
//     but did not.
// Dispatched messages:
//   [0] Excalibur.Dispatch.Messages.OrderUpdated
//   [1] Excalibur.Dispatch.Messages.OrderCancelled
```

## Documentation

- [Shouldly Documentation](https://shouldly.readthedocs.io/)

## License

MIT

