// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;

using RabbitMQ.Client;

using Tests.Shared.Fixtures;

using Testcontainers.RabbitMq;

namespace Excalibur.Dispatch.Integration.Tests.Transport.RabbitMQ;

/// <summary>
/// Integration tests for RabbitMQ transport sender operations.
/// Verifies message publishing to a real RabbitMQ container, including
/// single sends, batch sends, routing key support, and message property mapping.
/// </summary>
[Collection(ContainerCollections.RabbitMQ)]
[Trait("Category", "Integration")]
[Trait("Provider", "RabbitMQ")]
[Trait("Component", "Transport")]
public sealed class RabbitMqTransportSenderIntegrationShould : IAsyncLifetime
{
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
        try
        {
            if (_channel is not null)
            {
                await _channel.CloseAsync().ConfigureAwait(false);
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
                await _connection.CloseAsync().ConfigureAwait(false);
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
                await _container.DisposeAsync().ConfigureAwait(false);
            }
        }
        catch
        {
            // Best effort cleanup
        }
    }

    [SkippableFact]
    public async Task SendMessageToQueue_AndVerifyArrival()
    {
        Skip.IfNot(_dockerAvailable, "Docker is not available");

        // Arrange
        var queueName = $"test-queue-{Guid.NewGuid():N}";
        await _channel!.QueueDeclareAsync(
            queue: queueName,
            durable: false,
            exclusive: false,
            autoDelete: true).ConfigureAwait(false);

        var messageBody = Encoding.UTF8.GetBytes("Hello, RabbitMQ!");
        var properties = new BasicProperties
        {
            MessageId = Guid.NewGuid().ToString(),
            ContentType = "text/plain",
            Persistent = false,
        };

        // Act - publish directly to the default exchange with queue name as routing key
        await _channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: queueName,
            mandatory: false,
            basicProperties: properties,
            body: messageBody).ConfigureAwait(false);

        // Allow message to be routed
        await Task.Delay(500).ConfigureAwait(false);

        // Assert - consume the message and verify
        var result = await _channel.BasicGetAsync(queueName, autoAck: true).ConfigureAwait(false);

        result.ShouldNotBeNull();
        Encoding.UTF8.GetString(result.Body.ToArray()).ShouldBe("Hello, RabbitMQ!");
        result.BasicProperties.MessageId.ShouldBe(properties.MessageId);
        result.BasicProperties.ContentType.ShouldBe("text/plain");
    }

    [SkippableFact]
    public async Task SendMessageWithRoutingKey_ToTopicExchange()
    {
        Skip.IfNot(_dockerAvailable, "Docker is not available");

        // Arrange
        var exchangeName = $"test-exchange-{Guid.NewGuid():N}";
        var queueName = $"test-queue-{Guid.NewGuid():N}";
        var routingKey = "orders.created";

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
            routingKey: "orders.*").ConfigureAwait(false);

        var messageBody = Encoding.UTF8.GetBytes("{\"orderId\": 42}");
        var properties = new BasicProperties
        {
            MessageId = Guid.NewGuid().ToString(),
            ContentType = "application/json",
            Type = "OrderCreated",
            Persistent = true,
        };

        // Act
        await _channel.BasicPublishAsync(
            exchange: exchangeName,
            routingKey: routingKey,
            mandatory: false,
            basicProperties: properties,
            body: messageBody).ConfigureAwait(false);

        await Task.Delay(500).ConfigureAwait(false);

        // Assert
        var result = await _channel.BasicGetAsync(queueName, autoAck: true).ConfigureAwait(false);

        result.ShouldNotBeNull();
        result.RoutingKey.ShouldBe(routingKey);
        result.Exchange.ShouldBe(exchangeName);
        Encoding.UTF8.GetString(result.Body.ToArray()).ShouldBe("{\"orderId\": 42}");
        result.BasicProperties.Type.ShouldBe("OrderCreated");
    }

    [SkippableFact]
    public async Task SendBatchOfMessages_AllArrive()
    {
        Skip.IfNot(_dockerAvailable, "Docker is not available");

        // Arrange
        var queueName = $"test-batch-{Guid.NewGuid():N}";
        await _channel!.QueueDeclareAsync(
            queue: queueName,
            durable: false,
            exclusive: false,
            autoDelete: true).ConfigureAwait(false);

        const int batchSize = 10;

        // Act - send multiple messages
        for (var i = 0; i < batchSize; i++)
        {
            var properties = new BasicProperties
            {
                MessageId = $"batch-msg-{i}",
                ContentType = "text/plain",
            };

            await _channel.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: queueName,
                mandatory: false,
                basicProperties: properties,
                body: Encoding.UTF8.GetBytes($"Message {i}")).ConfigureAwait(false);
        }

        await Task.Delay(1000).ConfigureAwait(false);

        // Assert - all messages should be in the queue
        var receivedCount = 0;
        for (var i = 0; i < batchSize; i++)
        {
            var result = await _channel.BasicGetAsync(queueName, autoAck: true).ConfigureAwait(false);
            if (result is not null)
            {
                receivedCount++;
            }
        }

        receivedCount.ShouldBe(batchSize);
    }

    [SkippableFact]
    public async Task SendMessageWithHeaders_HeadersPreserved()
    {
        Skip.IfNot(_dockerAvailable, "Docker is not available");

        // Arrange
        var queueName = $"test-headers-{Guid.NewGuid():N}";
        await _channel!.QueueDeclareAsync(
            queue: queueName,
            durable: false,
            exclusive: false,
            autoDelete: true).ConfigureAwait(false);

        var properties = new BasicProperties
        {
            MessageId = Guid.NewGuid().ToString(),
            ContentType = "application/json",
            CorrelationId = "corr-123",
            Headers = new Dictionary<string, object?>
            {
                ["subject"] = "test-subject",
                ["custom-header"] = "custom-value",
            },
        };

        // Act
        await _channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: queueName,
            mandatory: false,
            basicProperties: properties,
            body: Encoding.UTF8.GetBytes("{}")).ConfigureAwait(false);

        await Task.Delay(500).ConfigureAwait(false);

        // Assert
        var result = await _channel.BasicGetAsync(queueName, autoAck: true).ConfigureAwait(false);

        result.ShouldNotBeNull();
        result.BasicProperties.CorrelationId.ShouldBe("corr-123");
        result.BasicProperties.Headers.ShouldNotBeNull();
        result.BasicProperties.Headers.ShouldContainKey("subject");
        result.BasicProperties.Headers.ShouldContainKey("custom-header");
    }

    [SkippableFact]
    public async Task SendMessageWithPriority_PriorityPreserved()
    {
        Skip.IfNot(_dockerAvailable, "Docker is not available");

        // Arrange - priority queues need x-max-priority argument
        var queueName = $"test-priority-{Guid.NewGuid():N}";
        await _channel!.QueueDeclareAsync(
            queue: queueName,
            durable: false,
            exclusive: false,
            autoDelete: true,
            arguments: new Dictionary<string, object?>
            {
                ["x-max-priority"] = 10,
            }).ConfigureAwait(false);

        var properties = new BasicProperties
        {
            MessageId = Guid.NewGuid().ToString(),
            Priority = 5,
        };

        // Act
        await _channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: queueName,
            mandatory: false,
            basicProperties: properties,
            body: Encoding.UTF8.GetBytes("priority message")).ConfigureAwait(false);

        await Task.Delay(500).ConfigureAwait(false);

        // Assert
        var result = await _channel.BasicGetAsync(queueName, autoAck: true).ConfigureAwait(false);

        result.ShouldNotBeNull();
        result.BasicProperties.Priority.ShouldBe((byte)5);
    }

    [SkippableFact]
    public async Task SendMessageWithTtl_ExpirationSet()
    {
        Skip.IfNot(_dockerAvailable, "Docker is not available");

        // Arrange
        var queueName = $"test-ttl-{Guid.NewGuid():N}";
        await _channel!.QueueDeclareAsync(
            queue: queueName,
            durable: false,
            exclusive: false,
            autoDelete: true).ConfigureAwait(false);

        var properties = new BasicProperties
        {
            MessageId = Guid.NewGuid().ToString(),
            Expiration = "60000", // 60 seconds TTL
        };

        // Act
        await _channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: queueName,
            mandatory: false,
            basicProperties: properties,
            body: Encoding.UTF8.GetBytes("ttl message")).ConfigureAwait(false);

        await Task.Delay(500).ConfigureAwait(false);

        // Assert
        var result = await _channel.BasicGetAsync(queueName, autoAck: true).ConfigureAwait(false);

        result.ShouldNotBeNull();
        result.BasicProperties.Expiration.ShouldBe("60000");
    }

    [SkippableFact]
    public async Task ConnectionFactory_EstablishesConnection()
    {
        Skip.IfNot(_dockerAvailable, "Docker is not available");

        // Assert - connection should be open from InitializeAsync
        _connection.ShouldNotBeNull();
        _connection!.IsOpen.ShouldBeTrue();
        _channel.ShouldNotBeNull();
        _channel!.IsOpen.ShouldBeTrue();
    }

    [SkippableFact]
    public async Task SendToNonExistentExchange_ChannelRemainsUsable()
    {
        Skip.IfNot(_dockerAvailable, "Docker is not available");

        // Arrange - use a separate channel so we don't break the shared one
        await using var separateChannel = await _connection!.CreateChannelAsync().ConfigureAwait(false);

        // Act & Assert - publishing to non-existent exchange with mandatory=false
        // should not throw (message is silently dropped)
        var properties = new BasicProperties { MessageId = "test-msg" };
        await separateChannel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: "non-existent-queue-name",
            mandatory: false,
            basicProperties: properties,
            body: Encoding.UTF8.GetBytes("test")).ConfigureAwait(false);

        // Channel should still be usable
        separateChannel.IsOpen.ShouldBeTrue();
    }

    [SkippableFact]
    public async Task SendMessageWithMessageType_TypePreserved()
    {
        Skip.IfNot(_dockerAvailable, "Docker is not available");

        // Arrange
        var queueName = $"test-type-{Guid.NewGuid():N}";
        await _channel!.QueueDeclareAsync(
            queue: queueName,
            durable: false,
            exclusive: false,
            autoDelete: true).ConfigureAwait(false);

        var properties = new BasicProperties
        {
            MessageId = Guid.NewGuid().ToString(),
            ContentType = "application/json",
            Type = "Excalibur.Dispatch.Tests.OrderCreatedEvent",
        };

        // Act
        await _channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: queueName,
            mandatory: false,
            basicProperties: properties,
            body: Encoding.UTF8.GetBytes("{\"type\":\"OrderCreated\"}")).ConfigureAwait(false);

        await Task.Delay(500).ConfigureAwait(false);

        // Assert
        var result = await _channel.BasicGetAsync(queueName, autoAck: true).ConfigureAwait(false);

        result.ShouldNotBeNull();
        result.BasicProperties.Type.ShouldBe("Excalibur.Dispatch.Tests.OrderCreatedEvent");
    }
}
