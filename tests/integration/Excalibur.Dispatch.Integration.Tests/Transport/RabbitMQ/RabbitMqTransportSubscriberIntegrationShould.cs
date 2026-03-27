// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Text;

using RabbitMQ.Client;
using RabbitMQ.Client.Events;

using Tests.Shared.Fixtures;
using Tests.Shared.Infrastructure;

using Testcontainers.RabbitMq;

namespace Excalibur.Dispatch.Integration.Tests.Transport.RabbitMQ;

/// <summary>
/// Integration tests for RabbitMQ transport subscriber (push-based consumption).
/// Verifies push-based message delivery via <see cref="AsyncEventingBasicConsumer"/>,
/// message acknowledgment, rejection, requeue, and cancellation semantics against
/// a real RabbitMQ container.
/// </summary>
[Collection(ContainerCollections.RabbitMQ)]
[Trait("Category", "Integration")]
[Trait("Provider", "RabbitMQ")]
[Trait("Component", "Transport")]
public sealed class RabbitMqTransportSubscriberIntegrationShould : IAsyncLifetime
{
    private static readonly TimeSpan MessageWaitTimeout = TestTimeouts.Scale(TimeSpan.FromSeconds(15));

    private RabbitMqContainer? _container;
    private IConnection? _connection;
    private IChannel? _channel;
    private bool _dockerAvailable;

    public async Task InitializeAsync()
    {
        try
        {
            _container = new RabbitMqBuilder()
                .WithImage("rabbitmq:3.12-management-alpine")
                .Build();
            await _container.StartAsync().ConfigureAwait(false);

            var factory = new ConnectionFactory
            {
                Uri = new Uri(_container.GetConnectionString()),
            };
            _connection = await factory.CreateConnectionAsync().ConfigureAwait(false);
            _channel = await _connection.CreateChannelAsync().ConfigureAwait(false);
            _dockerAvailable = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Docker initialization failed: {ex.Message}");
            _dockerAvailable = false;
        }
    }

    public async Task DisposeAsync()
    {
        var timeout = TimeSpan.FromSeconds(30);

        try
        {
            if (_channel is not null)
            {
                var task = _channel.CloseAsync();
                var completed = await Task.WhenAny(task, Task.Delay(timeout)).ConfigureAwait(false);
                if (completed == task) await task.ConfigureAwait(false);
                _channel.Dispose();
            }
        }
        catch
        {
            // Best effort cleanup
        }

        try
        {
            if (_connection is not null)
            {
                var task = _connection.CloseAsync();
                var completed = await Task.WhenAny(task, Task.Delay(timeout)).ConfigureAwait(false);
                if (completed == task) await task.ConfigureAwait(false);
                _connection.Dispose();
            }
        }
        catch
        {
            // Best effort cleanup
        }

        try
        {
            if (_container is not null)
            {
                var task = _container.DisposeAsync().AsTask();
                var completed = await Task.WhenAny(task, Task.Delay(timeout)).ConfigureAwait(false);
                if (completed == task) await task.ConfigureAwait(false);
            }
        }
        catch
        {
            // Best effort cleanup
        }
    }

    [SkippableFact]
    public async Task SubscribeAsync_ReceivesPublishedMessages()
    {
        Skip.IfNot(_dockerAvailable, "Docker is not available");

        // Arrange
        var queueName = $"test-sub-{Guid.NewGuid():N}";
        await _channel!.QueueDeclareAsync(
            queue: queueName,
            durable: false,
            exclusive: false,
            autoDelete: true).ConfigureAwait(false);

        // IMPORTANT: In RabbitMQ.Client 7.x, BasicDeliverEventArgs.Body is a ReadOnlyMemory<byte>
        // backed by a pooled PipeReader buffer. The buffer is recycled after the callback returns,
        // so we must copy body data inside the callback -- not store args and read Body later.
        var receivedMessages = new ConcurrentBag<(string MessageId, byte[] Body)>();
        var messageReceived = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += (_, args) =>
        {
            receivedMessages.Add((args.BasicProperties.MessageId ?? string.Empty, args.Body.ToArray()));
            messageReceived.TrySetResult(true);
            return Task.CompletedTask;
        };

        var consumerTag = await _channel.BasicConsumeAsync(
            queue: queueName,
            autoAck: true,
            consumer: consumer).ConfigureAwait(false);

        // Act - publish a message
        await _channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: queueName,
            mandatory: false,
            basicProperties: new BasicProperties { MessageId = "sub-msg-1", ContentType = "text/plain" },
            body: Encoding.UTF8.GetBytes("subscriber test")).ConfigureAwait(false);

        // Wait for message delivery
        await WaitHelpers.AwaitSignalAsync(messageReceived.Task, MessageWaitTimeout).ConfigureAwait(false);

        // Cleanup
        await _channel.BasicCancelAsync(consumerTag).ConfigureAwait(false);

        // Assert
        receivedMessages.Count.ShouldBe(1);

        var msg = receivedMessages.First();
        msg.MessageId.ShouldBe("sub-msg-1");
        Encoding.UTF8.GetString(msg.Body).ShouldBe("subscriber test");
    }

    [SkippableFact]
    public async Task SubscribeAsync_ReceivesMultipleMessages()
    {
        Skip.IfNot(_dockerAvailable, "Docker is not available");

        // Arrange
        var queueName = $"test-sub-multi-{Guid.NewGuid():N}";
        await _channel!.QueueDeclareAsync(
            queue: queueName,
            durable: false,
            exclusive: false,
            autoDelete: true).ConfigureAwait(false);

        const int expectedCount = 5;
        // Copy body inside callback -- RabbitMQ.Client 7.x Body is pooled ReadOnlyMemory<byte>
        var receivedCount = 0;
        var allReceived = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += (_, args) =>
        {
            // Touch body inside callback to ensure it's valid
            _ = args.Body.ToArray();
            var count = Interlocked.Increment(ref receivedCount);
            if (count >= expectedCount)
            {
                allReceived.TrySetResult(true);
            }

            return Task.CompletedTask;
        };

        var consumerTag = await _channel.BasicConsumeAsync(
            queue: queueName,
            autoAck: true,
            consumer: consumer).ConfigureAwait(false);

        // Act - publish multiple messages
        for (var i = 0; i < expectedCount; i++)
        {
            await _channel.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: queueName,
                mandatory: false,
                basicProperties: new BasicProperties { MessageId = $"sub-multi-{i}" },
                body: Encoding.UTF8.GetBytes($"message-{i}")).ConfigureAwait(false);
        }

        await WaitHelpers.AwaitSignalAsync(allReceived.Task, MessageWaitTimeout).ConfigureAwait(false);

        await _channel.BasicCancelAsync(consumerTag).ConfigureAwait(false);

        // Assert
        receivedCount.ShouldBe(expectedCount);
    }

    [SkippableFact]
    public async Task SubscribeAsync_AcknowledgesMessageManually()
    {
        Skip.IfNot(_dockerAvailable, "Docker is not available");

        // Use a separate channel to avoid shared channel state issues
        await using var ackChannel = await _connection!.CreateChannelAsync().ConfigureAwait(false);

        // Arrange - do NOT use autoDelete since consumer cancel would remove the queue
        var queueName = $"test-sub-ack-{Guid.NewGuid():N}";
        await ackChannel.QueueDeclareAsync(
            queue: queueName,
            durable: false,
            exclusive: false,
            autoDelete: false).ConfigureAwait(false);

        var messageProcessed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var consumer = new AsyncEventingBasicConsumer(ackChannel);
        consumer.ReceivedAsync += async (_, args) =>
        {
            // Manually acknowledge
            await ackChannel.BasicAckAsync(args.DeliveryTag, multiple: false).ConfigureAwait(false);
            messageProcessed.TrySetResult(true);
        };

        var consumerTag = await ackChannel.BasicConsumeAsync(
            queue: queueName,
            autoAck: false,
            consumer: consumer).ConfigureAwait(false);

        // Act
        await ackChannel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: queueName,
            mandatory: false,
            basicProperties: new BasicProperties { MessageId = "ack-test" },
            body: Encoding.UTF8.GetBytes("ack body")).ConfigureAwait(false);

        await WaitHelpers.AwaitSignalAsync(messageProcessed.Task, MessageWaitTimeout).ConfigureAwait(false);
        await ackChannel.BasicCancelAsync(consumerTag).ConfigureAwait(false);

        // Assert
        // Queue should be empty after ack
        var remaining = await ackChannel.BasicGetAsync(queueName, autoAck: true).ConfigureAwait(false);
        remaining.ShouldBeNull();

        // Cleanup
        await ackChannel.QueueDeleteAsync(queueName).ConfigureAwait(false);
    }

    [SkippableFact]
    public async Task SubscribeAsync_RejectsMessageWithRequeue()
    {
        Skip.IfNot(_dockerAvailable, "Docker is not available");

        // Arrange
        var queueName = $"test-sub-requeue-{Guid.NewGuid():N}";
        await _channel!.QueueDeclareAsync(
            queue: queueName,
            durable: false,
            exclusive: false,
            autoDelete: true).ConfigureAwait(false);

        var deliveryCount = 0;
        var secondDelivery = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += async (_, args) =>
        {
            var count = Interlocked.Increment(ref deliveryCount);
            if (count == 1)
            {
                // First delivery: nack with requeue
                await _channel.BasicNackAsync(args.DeliveryTag, multiple: false, requeue: true).ConfigureAwait(false);
            }
            else
            {
                // Second delivery: ack it
                await _channel.BasicAckAsync(args.DeliveryTag, multiple: false).ConfigureAwait(false);
                secondDelivery.TrySetResult(true);
            }
        };

        var consumerTag = await _channel.BasicConsumeAsync(
            queue: queueName,
            autoAck: false,
            consumer: consumer).ConfigureAwait(false);

        // Act
        await _channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: queueName,
            mandatory: false,
            basicProperties: new BasicProperties { MessageId = "requeue-test" },
            body: Encoding.UTF8.GetBytes("requeue body")).ConfigureAwait(false);

        await WaitHelpers.AwaitSignalAsync(secondDelivery.Task, MessageWaitTimeout).ConfigureAwait(false);
        await _channel.BasicCancelAsync(consumerTag).ConfigureAwait(false);

        // Assert
        deliveryCount.ShouldBeGreaterThanOrEqualTo(2);
    }

    [SkippableFact]
    public async Task SubscribeAsync_CancellationStopsConsumer()
    {
        Skip.IfNot(_dockerAvailable, "Docker is not available");

        // Use a separate channel to avoid poisoning the shared one
        await using var separateChannel = await _connection!.CreateChannelAsync().ConfigureAwait(false);

        // Arrange - do NOT use autoDelete so the queue survives consumer cancellation
        var queueName = $"test-sub-cancel-{Guid.NewGuid():N}";
        await separateChannel.QueueDeclareAsync(
            queue: queueName,
            durable: false,
            exclusive: false,
            autoDelete: false).ConfigureAwait(false);

        var consumer = new AsyncEventingBasicConsumer(separateChannel);
        consumer.ReceivedAsync += (_, _) => Task.CompletedTask;

        var consumerTag = await separateChannel.BasicConsumeAsync(
            queue: queueName,
            autoAck: true,
            consumer: consumer).ConfigureAwait(false);

        // Act - cancel the consumer
        await separateChannel.BasicCancelAsync(consumerTag).ConfigureAwait(false);

        // Assert - publish after cancel, message should stay in queue
        await separateChannel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: queueName,
            mandatory: false,
            basicProperties: new BasicProperties { MessageId = "after-cancel" },
            body: Encoding.UTF8.GetBytes("post-cancel")).ConfigureAwait(false);

        // Message should still be in queue since consumer was cancelled
        var result = await WaitForBasicGetAsync(
                separateChannel,
                queueName,
                autoAck: true,
                MessageWaitTimeout)
            .ConfigureAwait(false);
        result.ShouldNotBeNull();
        result.BasicProperties.MessageId.ShouldBe("after-cancel");

        // Cleanup
        await separateChannel.QueueDeleteAsync(queueName).ConfigureAwait(false);
    }

    [SkippableFact]
    public async Task SubscribeAsync_MessagePropertiesPreserved()
    {
        Skip.IfNot(_dockerAvailable, "Docker is not available");

        // Arrange
        var queueName = $"test-sub-props-{Guid.NewGuid():N}";
        await _channel!.QueueDeclareAsync(
            queue: queueName,
            durable: false,
            exclusive: false,
            autoDelete: true).ConfigureAwait(false);

        // Copy all needed data inside callback -- RabbitMQ.Client 7.x Body is pooled ReadOnlyMemory<byte>
        string? messageId = null;
        string? contentType = null;
        string? correlationId = null;
        string? type = null;
        IDictionary<string, object?>? headers = null;
        byte[]? body = null;
        var messageReceived = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += (_, args) =>
        {
            messageId = args.BasicProperties.MessageId;
            contentType = args.BasicProperties.ContentType;
            correlationId = args.BasicProperties.CorrelationId;
            type = args.BasicProperties.Type;
            headers = args.BasicProperties.Headers != null
                ? new Dictionary<string, object?>(args.BasicProperties.Headers)
                : null;
            body = args.Body.ToArray();
            messageReceived.TrySetResult(true);
            return Task.CompletedTask;
        };

        var consumerTag = await _channel.BasicConsumeAsync(
            queue: queueName,
            autoAck: true,
            consumer: consumer).ConfigureAwait(false);

        // Act - publish with rich properties
        await _channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: queueName,
            mandatory: false,
            basicProperties: new BasicProperties
            {
                MessageId = "props-msg",
                ContentType = "application/json",
                CorrelationId = "corr-456",
                Type = "OrderCreated",
                Headers = new Dictionary<string, object?>
                {
                    ["subject"] = "test-subject",
                    ["custom-key"] = "custom-value",
                },
            },
            body: Encoding.UTF8.GetBytes("{\"orderId\": 1}")).ConfigureAwait(false);

        await WaitHelpers.AwaitSignalAsync(messageReceived.Task, MessageWaitTimeout).ConfigureAwait(false);
        await _channel.BasicCancelAsync(consumerTag).ConfigureAwait(false);

        // Assert
        messageId.ShouldBe("props-msg");
        contentType.ShouldBe("application/json");
        correlationId.ShouldBe("corr-456");
        type.ShouldBe("OrderCreated");
        headers.ShouldNotBeNull();
        headers!.ShouldContainKey("subject");
        headers.ShouldContainKey("custom-key");
        body.ShouldNotBeNull();
        Encoding.UTF8.GetString(body!).ShouldBe("{\"orderId\": 1}");
    }

    [SkippableFact]
    public async Task SubscribeAsync_FromTopicExchange_ReceivesRoutedMessages()
    {
        Skip.IfNot(_dockerAvailable, "Docker is not available");

        // Arrange
        var exchangeName = $"test-sub-exchange-{Guid.NewGuid():N}";
        var queueName = $"test-sub-queue-{Guid.NewGuid():N}";

        await _channel!.ExchangeDeclareAsync(
            exchange: exchangeName,
            type: ExchangeType.Topic,
            durable: false,
            autoDelete: true).ConfigureAwait(false);

        await _channel.QueueDeclareAsync(
            queue: queueName,
            durable: false,
            exclusive: false,
            autoDelete: true).ConfigureAwait(false);

        await _channel.QueueBindAsync(
            queue: queueName,
            exchange: exchangeName,
            routingKey: "events.#").ConfigureAwait(false);

        // Copy data inside callback -- RabbitMQ.Client 7.x uses pooled buffers
        string? receivedExchange = null;
        string? receivedRoutingKey = null;
        var messageReceived = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += (_, args) =>
        {
            receivedExchange = args.Exchange;
            receivedRoutingKey = args.RoutingKey;
            messageReceived.TrySetResult(true);
            return Task.CompletedTask;
        };

        var consumerTag = await _channel.BasicConsumeAsync(
            queue: queueName,
            autoAck: true,
            consumer: consumer).ConfigureAwait(false);

        // Act
        await _channel.BasicPublishAsync(
            exchange: exchangeName,
            routingKey: "events.order.created",
            mandatory: false,
            basicProperties: new BasicProperties { MessageId = "routed-sub-msg" },
            body: Encoding.UTF8.GetBytes("routed message")).ConfigureAwait(false);

        await WaitHelpers.AwaitSignalAsync(messageReceived.Task, MessageWaitTimeout).ConfigureAwait(false);
        await _channel.BasicCancelAsync(consumerTag).ConfigureAwait(false);

        // Assert
        receivedExchange.ShouldBe(exchangeName);
        receivedRoutingKey.ShouldBe("events.order.created");
    }

    private static Task<BasicGetResult?> WaitForBasicGetAsync(
        IChannel channel,
        string queueName,
        bool autoAck,
        TimeSpan timeout)
    {
        return WaitHelpers.WaitForValueAsync(
            () => channel.BasicGetAsync(queueName, autoAck),
            timeout,
            TimeSpan.FromMilliseconds(100));
    }
}
