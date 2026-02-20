---
sidebar_position: 6
title: Transport Test Doubles
description: In-memory transport implementations for testing sender, receiver, and subscriber flows
---

# Transport Test Doubles

## Before You Start

- Install the testing package:

  ```bash
  dotnet add package Excalibur.Dispatch.Testing
  ```

- Familiarity with the transport abstractions: `ITransportSender`, `ITransportReceiver`, `ITransportSubscriber`

## Overview

The `Excalibur.Dispatch.Testing.Transport` namespace provides three in-memory transport implementations that replace real infrastructure (RabbitMQ, Kafka, Azure Service Bus, etc.) in tests. Each implementation records interactions for assertions and provides test-controllable behavior.

| Test Double                  | Replaces             | Key Feature                           |
|------------------------------|----------------------|---------------------------------------|
| `InMemoryTransportSender`    | `ITransportSender`   | Records sent messages, configurable send behavior |
| `InMemoryTransportReceiver`  | `ITransportReceiver`  | Enqueue-then-receive, tracks ack/reject |
| `InMemoryTransportSubscriber`| `ITransportSubscriber` | Push messages to handler, inspect results |

All three are thread-safe, implement `IAsyncDisposable`, and follow the `InMemoryChatClient` pattern from `Microsoft.Extensions.AI`.

## InMemoryTransportSender

### Recording Sent Messages

Every call to `SendAsync` or `SendBatchAsync` records the message in `SentMessages`:

```csharp
using Excalibur.Dispatch.Testing.Transport;
using Excalibur.Dispatch.Transport;

// Defined in: Excalibur.Dispatch.Testing/Transport/InMemoryTransportSender.cs
var sender = new InMemoryTransportSender("orders-topic");

await sender.SendAsync(
    TransportMessage.FromString("order-created"),
    CancellationToken.None);

sender.SentMessages.Count.ShouldBe(1);
sender.SentMessages[0].ContentType.ShouldBe("text/plain");
sender.Destination.ShouldBe("orders-topic");
```

### Custom Send Behavior (OnSend)

By default, every send returns `SendResult.Success`. Use `OnSend` to control the result per message -- simulate failures, test retry logic, or validate message content:

```csharp
var sender = new InMemoryTransportSender("orders-topic")
    .OnSend(message =>
    {
        // Simulate failure for messages without a correlation ID
        if (message.CorrelationId is null)
        {
            return SendResult.Failure("Missing correlation ID");
        }

        return SendResult.Success(message.Id);
    });

var result = await sender.SendAsync(
    TransportMessage.FromString("no-correlation"),
    CancellationToken.None);

result.IsSuccess.ShouldBeFalse();
```

The `OnSend` callback receives each `TransportMessage` and returns a `SendResult`. The message is still recorded in `SentMessages` regardless of the result.

### Batch Sending

`SendBatchAsync` invokes the `OnSend` callback for each message individually and returns a `BatchSendResult` with per-message results:

```csharp
var sender = new InMemoryTransportSender("events-topic");
var messages = new List<TransportMessage>
{
    TransportMessage.FromString("event-1"),
    TransportMessage.FromString("event-2"),
    TransportMessage.FromString("event-3"),
};

var batchResult = await sender.SendBatchAsync(messages, CancellationToken.None);

batchResult.TotalMessages.ShouldBe(3);
batchResult.SuccessCount.ShouldBe(3);
batchResult.FailureCount.ShouldBe(0);
sender.SentMessages.Count.ShouldBe(3);
```

### Clearing State

Call `Clear()` to reset the sender between tests:

```csharp
sender.Clear();
sender.SentMessages.Count.ShouldBe(0);
```

## InMemoryTransportReceiver

### Enqueuing Test Messages

Tests control what messages are available by calling `Enqueue` before `ReceiveAsync`:

```csharp
using Excalibur.Dispatch.Testing.Transport;
using Excalibur.Dispatch.Transport;

// Defined in: Excalibur.Dispatch.Testing/Transport/InMemoryTransportReceiver.cs
var receiver = new InMemoryTransportReceiver("orders-queue");

receiver.Enqueue(new TransportReceivedMessage
{
    Id = "msg-1",
    Body = System.Text.Encoding.UTF8.GetBytes("order-data"),
    ContentType = "application/json",
});
```

Enqueue multiple messages at once:

```csharp
receiver.Enqueue(
    new TransportReceivedMessage { Id = "msg-1", Body = body1 },
    new TransportReceivedMessage { Id = "msg-2", Body = body2 }
);
```

### Non-Blocking ReceiveAsync

`ReceiveAsync` returns immediately with whatever messages are currently in the queue, up to `maxMessages`. It does not block or wait for messages:

```csharp
receiver.Enqueue(new TransportReceivedMessage { Id = "msg-1" });
receiver.Enqueue(new TransportReceivedMessage { Id = "msg-2" });
receiver.Enqueue(new TransportReceivedMessage { Id = "msg-3" });

var batch = await receiver.ReceiveAsync(2, CancellationToken.None);
batch.Count.ShouldBe(2);

var remaining = await receiver.ReceiveAsync(10, CancellationToken.None);
remaining.Count.ShouldBe(1);

var empty = await receiver.ReceiveAsync(10, CancellationToken.None);
empty.Count.ShouldBe(0);
```

### Settlement Tracking (Acknowledged, Rejected)

After receiving messages, call `AcknowledgeAsync` or `RejectAsync` to settle them. The receiver tracks all settlements:

```csharp
receiver.Enqueue(new TransportReceivedMessage { Id = "msg-1" });
var messages = await receiver.ReceiveAsync(10, CancellationToken.None);

// Acknowledge the message
await receiver.AcknowledgeAsync(messages[0], CancellationToken.None);
receiver.AcknowledgedMessages.Count.ShouldBe(1);

// Or reject with a reason
receiver.Enqueue(new TransportReceivedMessage { Id = "msg-2" });
var batch2 = await receiver.ReceiveAsync(10, CancellationToken.None);
await receiver.RejectAsync(batch2[0], "Invalid format", requeue: false, CancellationToken.None);

receiver.RejectedMessages.Count.ShouldBe(1);
receiver.RejectedMessages[0].Reason.ShouldBe("Invalid format");
receiver.RejectedMessages[0].Requeue.ShouldBeFalse();
```

The `RejectedMessage` record captures the message, reason, and whether requeue was requested:

```csharp
// Defined in: Excalibur.Dispatch.Testing/Transport/InMemoryTransportReceiver.cs
public sealed record RejectedMessage(
    TransportReceivedMessage Message,
    string? Reason,
    bool Requeue);
```

### Clearing State

`Clear()` drains pending, acknowledged, and rejected messages:

```csharp
receiver.Clear();
```

## InMemoryTransportSubscriber

### Push-Based Testing

Unlike the receiver (pull-based), the subscriber uses a push model. First register a handler via `SubscribeAsync`, then push messages from test code via `PushAsync`:

```csharp
using Excalibur.Dispatch.Testing.Transport;
using Excalibur.Dispatch.Transport;

// Defined in: Excalibur.Dispatch.Testing/Transport/InMemoryTransportSubscriber.cs
var subscriber = new InMemoryTransportSubscriber("orders-topic");

// Start subscription in background
using var cts = new CancellationTokenSource();
var subscribeTask = subscriber.SubscribeAsync(
    (message, cancellationToken) =>
    {
        // Process the message and return an action
        return Task.FromResult(MessageAction.Acknowledge);
    },
    cts.Token);

// Push a test message
var action = await subscriber.PushAsync(
    new TransportReceivedMessage { Id = "msg-1" },
    CancellationToken.None);

action.ShouldBe(MessageAction.Acknowledge);

// Stop subscription
cts.Cancel();
```

### Subscription Lifecycle

`SubscribeAsync` stores the handler and blocks (via `Task.Delay(Infinite)`) until the cancellation token is cancelled. This mirrors the real subscriber behavior where subscription is a long-running operation:

```csharp
subscriber.IsSubscribed.ShouldBeFalse();

using var cts = new CancellationTokenSource();
var subscribeTask = subscriber.SubscribeAsync(
    (msg, ct) => Task.FromResult(MessageAction.Acknowledge),
    cts.Token);

subscriber.IsSubscribed.ShouldBeTrue();

cts.Cancel();
await subscribeTask; // completes without throwing

subscriber.IsSubscribed.ShouldBeFalse();
```

:::note
`PushAsync` throws `InvalidOperationException` if called before `SubscribeAsync`. Always start the subscription first.
:::

### Processing Results

Every message pushed through the subscriber is recorded in `ProcessedMessages` along with the handler's returned `MessageAction`:

```csharp
// After pushing several messages...
subscriber.ProcessedMessages.Count.ShouldBe(3);

// Defined in: Excalibur.Dispatch.Testing/Transport/InMemoryTransportSubscriber.cs
// public sealed record ProcessedMessage(TransportReceivedMessage Message, MessageAction Action);
var first = subscriber.ProcessedMessages[0];
first.Message.Id.ShouldBe("msg-1");
first.Action.ShouldBe(MessageAction.Acknowledge);
```

## Integration Test Example

Combine the test harness with transport test doubles to test a full pipeline that sends messages to a transport:

```csharp
using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Testing;
using Excalibur.Dispatch.Testing.Transport;
using Excalibur.Dispatch.Transport;
using Microsoft.Extensions.DependencyInjection;

public class OrderPipelineWithTransportShould : IAsyncDisposable
{
    private readonly DispatchTestHarness _harness;
    private readonly InMemoryTransportSender _sender;

    public OrderPipelineWithTransportShould()
    {
        _sender = new InMemoryTransportSender("orders-topic");

        _harness = new DispatchTestHarness()
            .ConfigureServices(services =>
            {
                // Register the test double as the transport sender
                services.AddSingleton<ITransportSender>(_sender);
            })
            .ConfigureDispatch(dispatch =>
            {
                dispatch.AddHandlersFromAssembly(typeof(PublishOrderHandler).Assembly);
            });
    }

    [Fact]
    public async Task Send_order_event_to_transport()
    {
        // Arrange
        var context = new MessageContextBuilder()
            .WithCorrelationId("corr-001")
            .Build();

        // Act
        await _harness.Dispatcher.DispatchAsync(
            new PlaceOrderAction("customer-123", 99.99m),
            context,
            CancellationToken.None);

        // Assert -- message was dispatched through pipeline
        _harness.Dispatched.Any<PlaceOrderAction>().ShouldBeTrue();

        // Assert -- handler sent to transport
        _sender.SentMessages.Count.ShouldBe(1);
    }

    public async ValueTask DisposeAsync()
    {
        await _harness.DisposeAsync();
        await _sender.DisposeAsync();
    }
}
```

## See Also

- [Test Harness](./test-harness.md) -- DispatchTestHarness and MessageContextBuilder
- [Shouldly Assertions](./shouldly-assertions.md) -- Fluent assertions for transport test doubles
- [Testing Dispatch Handlers](./testing-handlers.md) -- Unit testing handlers directly
- [Transports Overview](../transports/index.md) -- Production transport implementations (RabbitMQ, Kafka, Azure Service Bus, etc.)
- [Integration Tests](./integration-tests.md) -- End-to-end testing with real infrastructure using TestContainers
