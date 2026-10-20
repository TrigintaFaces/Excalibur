// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text;

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.RabbitMQ;

using Microsoft.Extensions.Logging.Abstractions;

using RabbitMQ.Client;

using BasicProperties = RabbitMQ.Client.BasicProperties;

using Tests.Shared.Fixtures;
using Tests.Shared.Infrastructure;

using Testcontainers.RabbitMq;

namespace Excalibur.Dispatch.Integration.Tests.Transport.RabbitMQ;

/// <summary>
/// Integration tests for RabbitMQ transport receiver operations.
/// Verifies message consumption from a real RabbitMQ container, including
/// single receives, acknowledgment, rejection with requeue, and empty queue behavior.
/// </summary>
[Collection(ContainerCollections.RabbitMQ)]
[Trait(TraitNames.Category, TestCategories.Integration)]
[Trait("Database", "RabbitMQ")]
[Trait(TraitNames.Component, TestComponents.Transport)]
public sealed class RabbitMqTransportReceiverIntegrationShould : IAsyncLifetime
{
    private static readonly TimeSpan MessageWaitTimeout = TestTimeouts.Scale(TimeSpan.FromSeconds(15));

    private RabbitMqContainer? _container;
    private IConnection? _connection;
    private IChannel? _channel;
    private bool _dockerAvailable;

    public async ValueTask InitializeAsync()
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
            _channel = await _connection!.CreateChannelAsync().ConfigureAwait(false);
            _dockerAvailable = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Docker initialization failed: {ex.Message}");
            _dockerAvailable = false;
        }
    }

    public async ValueTask DisposeAsync()
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

    [Fact]
    public async Task ReceiveMessage_FromPublishedQueue()
    {
        Assert.SkipUnless(_dockerAvailable, "Docker is not available");

        // Arrange
        var queueName = $"test-receive-{Guid.NewGuid():N}";
        await _channel!.QueueDeclareAsync(
            queue: queueName,
            durable: false,
            exclusive: false,
            autoDelete: true).ConfigureAwait(false);

        var expectedBody = "Received message body";
        var properties = new BasicProperties
        {
            MessageId = "recv-msg-001",
            ContentType = "text/plain",
            CorrelationId = "corr-recv-001",
            Type = "TestReceivedEvent",
        };

        await _channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: queueName,
            mandatory: false,
            basicProperties: properties,
            body: Encoding.UTF8.GetBytes(expectedBody)).ConfigureAwait(false);

        // Act
        var result = await WaitForBasicGetAsync(_channel, queueName, autoAck: false, MessageWaitTimeout).ConfigureAwait(false);

        // Assert
        result.ShouldNotBeNull();
        Encoding.UTF8.GetString(result.Body.ToArray()).ShouldBe(expectedBody);
        result.BasicProperties.MessageId.ShouldBe("recv-msg-001");
        result.BasicProperties.ContentType.ShouldBe("text/plain");
        result.BasicProperties.CorrelationId.ShouldBe("corr-recv-001");
        result.BasicProperties.Type.ShouldBe("TestReceivedEvent");

        // Acknowledge the message
        await _channel.BasicAckAsync(result.DeliveryTag, multiple: false).ConfigureAwait(false);
    }

    [Fact]
    public async Task ReceiveFromEmptyQueue_ReturnsNull()
    {
        Assert.SkipUnless(_dockerAvailable, "Docker is not available");

        // Arrange
        var queueName = $"test-empty-{Guid.NewGuid():N}";
        await _channel!.QueueDeclareAsync(
            queue: queueName,
            durable: false,
            exclusive: false,
            autoDelete: true).ConfigureAwait(false);

        // Act - no messages published
        var result = await _channel.BasicGetAsync(queueName, autoAck: true).ConfigureAwait(false);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task AcknowledgeMessage_RemovesFromQueue()
    {
        Assert.SkipUnless(_dockerAvailable, "Docker is not available");
        await VerifySettlementAsync("acknowledge", noOp: false).ConfigureAwait(false);
        await VerifySettlementAsync("acknowledge", noOp: true).ConfigureAwait(false);
    }

    [Fact]
    public async Task RejectMessageWithRequeue_MessageRedelivered()
    {
        Assert.SkipUnless(_dockerAvailable, "Docker is not available");
        await VerifySettlementAsync("requeue", noOp: false).ConfigureAwait(false);
        await VerifySettlementAsync("requeue", noOp: true).ConfigureAwait(false);
    }

    [Fact]
    public async Task RejectMessageWithoutRequeue_MessageRemoved()
    {
        Assert.SkipUnless(_dockerAvailable, "Docker is not available");
        await VerifySettlementAsync("reject", noOp: false).ConfigureAwait(false);
        await VerifySettlementAsync("reject", noOp: true).ConfigureAwait(false);
    }

    [Fact]
    public async Task ReceiveMultipleMessages_InOrder()
    {
        Assert.SkipUnless(_dockerAvailable, "Docker is not available");

        // Arrange
        var queueName = $"test-multi-{Guid.NewGuid():N}";
        await _channel!.QueueDeclareAsync(
            queue: queueName,
            durable: false,
            exclusive: false,
            autoDelete: true).ConfigureAwait(false);

        const int messageCount = 5;
        for (var i = 0; i < messageCount; i++)
        {
            var properties = new BasicProperties
            {
                MessageId = $"multi-msg-{i}",
                ContentType = "text/plain",
            };
            await _channel.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: queueName,
                mandatory: false,
                basicProperties: properties,
                body: Encoding.UTF8.GetBytes($"Message {i}")).ConfigureAwait(false);
        }

        // Act & Assert - receive messages in FIFO order
        for (var i = 0; i < messageCount; i++)
        {
            var result = await WaitForBasicGetAsync(_channel, queueName, autoAck: true, MessageWaitTimeout).ConfigureAwait(false);
            result.ShouldNotBeNull();
            result.BasicProperties.MessageId.ShouldBe($"multi-msg-{i}");
            Encoding.UTF8.GetString(result.Body.ToArray()).ShouldBe($"Message {i}");
        }

        // Queue should be empty
        var empty = await _channel.BasicGetAsync(queueName, autoAck: true).ConfigureAwait(false);
        empty.ShouldBeNull();
    }

    [Fact]
    public async Task ReceiveMessageWithHeaders_HeadersPreserved()
    {
        Assert.SkipUnless(_dockerAvailable, "Docker is not available");

        // Arrange
        var queueName = $"test-recv-headers-{Guid.NewGuid():N}";
        await _channel!.QueueDeclareAsync(
            queue: queueName,
            durable: false,
            exclusive: false,
            autoDelete: true).ConfigureAwait(false);

        var properties = new BasicProperties
        {
            MessageId = "header-msg",
            Headers = new Dictionary<string, object?>
            {
                ["x-custom-header"] = "header-value",
                ["x-retry-count"] = 3,
            },
        };

        await _channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: queueName,
            mandatory: false,
            basicProperties: properties,
            body: Encoding.UTF8.GetBytes("header-test")).ConfigureAwait(false);

        // Act
        var result = await WaitForBasicGetAsync(_channel, queueName, autoAck: true, MessageWaitTimeout).ConfigureAwait(false);

        // Assert
        result.ShouldNotBeNull();
        result.BasicProperties.Headers.ShouldNotBeNull();
        result.BasicProperties.Headers.ShouldContainKey("x-custom-header");
        result.BasicProperties.Headers.ShouldContainKey("x-retry-count");
    }

    [Fact]
    public async Task ReceiveMessageFromTopicExchange_RoutingKeyPreserved()
    {
        Assert.SkipUnless(_dockerAvailable, "Docker is not available");

        // Arrange
        var exchangeName = $"test-recv-exchange-{Guid.NewGuid():N}";
        var queueName = $"test-recv-queue-{Guid.NewGuid():N}";
        var routingKey = "events.order.created";

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
            routingKey: "events.order.*").ConfigureAwait(false);

        var properties = new BasicProperties
        {
            MessageId = "routed-msg",
            ContentType = "application/json",
        };

        await _channel.BasicPublishAsync(
            exchange: exchangeName,
            routingKey: routingKey,
            mandatory: false,
            basicProperties: properties,
            body: Encoding.UTF8.GetBytes("{\"eventType\":\"OrderCreated\"}")).ConfigureAwait(false);

        // Act
        var result = await WaitForBasicGetAsync(_channel, queueName, autoAck: true, MessageWaitTimeout).ConfigureAwait(false);

        // Assert
        result.ShouldNotBeNull();
        result.Exchange.ShouldBe(exchangeName);
        result.RoutingKey.ShouldBe(routingKey);
        result.BasicProperties.MessageId.ShouldBe("routed-msg");
    }

    [Fact]
    public async Task PrefetchLimit_RespectedByConsumer()
    {
        Assert.SkipUnless(_dockerAvailable, "Docker is not available");

        // Arrange - set prefetch count to 2
        var queueName = $"test-prefetch-{Guid.NewGuid():N}";
        await _channel!.QueueDeclareAsync(
            queue: queueName,
            durable: false,
            exclusive: false,
            autoDelete: true).ConfigureAwait(false);

        await _channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 2, global: false).ConfigureAwait(false);

        // Publish 5 messages
        for (var i = 0; i < 5; i++)
        {
            await _channel.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: queueName,
                mandatory: false,
                basicProperties: new BasicProperties { MessageId = $"prefetch-{i}" },
                body: Encoding.UTF8.GetBytes($"msg-{i}")).ConfigureAwait(false);
        }

        // Act - get messages without acking (should respect prefetch)
        var msg1 = await WaitForBasicGetAsync(_channel, queueName, autoAck: false, MessageWaitTimeout).ConfigureAwait(false);
        var msg2 = await WaitForBasicGetAsync(_channel, queueName, autoAck: false, MessageWaitTimeout).ConfigureAwait(false);

        // Assert - first two should be received
        msg1.ShouldNotBeNull();
        msg2.ShouldNotBeNull();

        // Ack both and get more
        await _channel.BasicAckAsync(msg1.DeliveryTag, multiple: true).ConfigureAwait(false);
        await _channel.BasicAckAsync(msg2.DeliveryTag, multiple: false).ConfigureAwait(false);

        // Should now be able to get the remaining messages
        var msg3 = await _channel.BasicGetAsync(queueName, autoAck: true).ConfigureAwait(false);
        msg3.ShouldNotBeNull();
    }

    [Fact]
    public async Task CancellationToken_StopsReceiving()
    {
        Assert.SkipUnless(_dockerAvailable, "Docker is not available");

        // Arrange
        var queueName = $"test-cancel-{Guid.NewGuid():N}";
        await _channel!.QueueDeclareAsync(
            queue: queueName,
            durable: false,
            exclusive: false,
            autoDelete: true).ConfigureAwait(false);

        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        // Act & Assert - operation should respect cancellation
        await Should.ThrowAsync<OperationCanceledException>(
            async () => await _channel.BasicGetAsync(queueName, autoAck: true, cts.Token).ConfigureAwait(false));
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

    private async Task VerifySettlementAsync(string action, bool noOp)
    {
        using var bounded = new CancellationTokenSource(TimeSpan.FromSeconds(25));
        var queueName = $"settlement-{Guid.NewGuid():N}";
        var targetId = $"target-{Guid.NewGuid():N}";
        var body = $"body-{targetId}";
        await _channel!.QueueDeclareAsync(queueName, durable: false, exclusive: false,
            autoDelete: false, cancellationToken: bounded.Token).ConfigureAwait(false);
        try
        {
            await _channel.BasicPublishAsync(string.Empty, queueName, mandatory: false,
                basicProperties: new global::RabbitMQ.Client.BasicProperties { MessageId = targetId },
                body: Encoding.UTF8.GetBytes(body), cancellationToken: bounded.Token).ConfigureAwait(false);
            await using var original = await _connection!.CreateChannelAsync(cancellationToken: bounded.Token).ConfigureAwait(false);
            await using var realReceiver = new RabbitMqTransportReceiver(original, queueName, queueName,
                NullLogger<RabbitMqTransportReceiver>.Instance);
            ITransportReceiver receiver = noOp ? new NoOpSettlementReceiver(realReceiver) : realReceiver;
            var received = await WaitHelpers.WaitForValueAsync(async () =>
                (await receiver.ReceiveAsync(1, bounded.Token).ConfigureAwait(false)).SingleOrDefault(),
                TimeSpan.FromSeconds(5), cancellationToken: bounded.Token).ConfigureAwait(false);
            received.ShouldNotBeNull();
            received.Id.ShouldBe(targetId);
            Encoding.UTF8.GetString(received.Body.Span).ShouldBe(body);
            if (action == "acknowledge")
                await receiver.AcknowledgeAsync(received, bounded.Token).ConfigureAwait(false);
            else
                await receiver.RejectAsync(received, "integration rejection", action == "requeue", bounded.Token).ConfigureAwait(false);

            if (action == "requeue")
            {
                // While the original channel stays open, a no-op cannot release its unacked target.
                var redelivered = await WaitHelpers.WaitForValueAsync(
                    () => original.BasicGetAsync(queueName, autoAck: true, bounded.Token),
                    TimeSpan.FromSeconds(5), cancellationToken: bounded.Token).ConfigureAwait(false);
                if (noOp)
                    redelivered.ShouldBeNull();
                else
                {
                    redelivered.ShouldNotBeNull();
                    redelivered.BasicProperties.MessageId.ShouldBe(targetId);
                    Encoding.UTF8.GetString(redelivered.Body.Span).ShouldBe(body);
                    redelivered.Redelivered.ShouldBeTrue();
                }
            }

            // Awaiting close distinguishes an ACK/drop from a target merely hidden as unacked.
            await original.CloseAsync(bounded.Token).ConfigureAwait(false);
            await using var probe = await _connection!.CreateChannelAsync(cancellationToken: bounded.Token).ConfigureAwait(false);
            await probe.QueueDeclarePassiveAsync(queueName, bounded.Token).ConfigureAwait(false);
            var afterClose = await WaitHelpers.WaitForValueAsync(
                () => probe.BasicGetAsync(queueName, autoAck: true, bounded.Token),
                TimeSpan.FromSeconds(5), cancellationToken: bounded.Token).ConfigureAwait(false);
            if (noOp)
            {
                afterClose.ShouldNotBeNull();
                afterClose.BasicProperties.MessageId.ShouldBe(targetId);
                Encoding.UTF8.GetString(afterClose.Body.Span).ShouldBe(body);
                afterClose.Redelivered.ShouldBeTrue();
            }
            else
                afterClose.ShouldBeNull();
        }
        finally
        {
            await _channel!.QueueDeleteAsync(queueName, cancellationToken: CancellationToken.None).ConfigureAwait(false);
        }
    }

    private sealed class NoOpSettlementReceiver(ITransportReceiver inner) : DelegatingTransportReceiver(inner)
    {
        public override Task AcknowledgeAsync(TransportReceivedMessage message, CancellationToken cancellationToken) => Task.CompletedTask;
        public override Task RejectAsync(TransportReceivedMessage message, string? reason, bool requeue, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
