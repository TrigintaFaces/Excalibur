// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;

using RabbitMQ.Client;

using Tests.Shared.Fixtures;

using Testcontainers.RabbitMq;

namespace Excalibur.Dispatch.Integration.Tests.Transport.RabbitMQ;

/// <summary>
/// Integration tests for RabbitMQ dead letter exchange (DLX) functionality.
/// Verifies DLX routing, DLQ message consumption, header metadata preservation,
/// and purge operations against a real RabbitMQ container.
/// </summary>
[Collection(ContainerCollections.RabbitMQ)]
[Trait("Category", "Integration")]
[Trait("Provider", "RabbitMQ")]
[Trait("Component", "Transport")]
public sealed class RabbitMqDeadLetterIntegrationShould : IAsyncLifetime
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
    public async Task RejectedMessage_RoutedToDeadLetterExchange()
    {
        Skip.IfNot(_dockerAvailable, "Docker is not available");

        // Arrange - set up DLX infrastructure
        var dlxExchange = $"dlx-{Guid.NewGuid():N}";
        var dlqQueue = $"dlq-{Guid.NewGuid():N}";
        var mainQueue = $"main-{Guid.NewGuid():N}";

        // Declare DLX exchange and DLQ queue
        await _channel!.ExchangeDeclareAsync(
            exchange: dlxExchange,
            type: ExchangeType.Fanout,
            durable: false,
            autoDelete: true).ConfigureAwait(false);

        await _channel.QueueDeclareAsync(
            queue: dlqQueue,
            durable: false,
            exclusive: false,
            autoDelete: true).ConfigureAwait(false);

        await _channel.QueueBindAsync(
            queue: dlqQueue,
            exchange: dlxExchange,
            routingKey: string.Empty).ConfigureAwait(false);

        // Declare main queue with DLX argument
        await _channel.QueueDeclareAsync(
            queue: mainQueue,
            durable: false,
            exclusive: false,
            autoDelete: true,
            arguments: new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = dlxExchange,
            }).ConfigureAwait(false);

        // Publish to main queue
        await _channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: mainQueue,
            mandatory: false,
            basicProperties: new BasicProperties
            {
                MessageId = "dlx-msg-001",
                ContentType = "text/plain",
            },
            body: Encoding.UTF8.GetBytes("dead letter test")).ConfigureAwait(false);

        await Task.Delay(500).ConfigureAwait(false);

        // Act - receive and reject (nack without requeue)
        var mainResult = await _channel.BasicGetAsync(mainQueue, autoAck: false).ConfigureAwait(false);
        mainResult.ShouldNotBeNull();
        await _channel.BasicNackAsync(mainResult.DeliveryTag, multiple: false, requeue: false).ConfigureAwait(false);

        await Task.Delay(1000).ConfigureAwait(false);

        // Assert - message should be in DLQ
        var dlqResult = await _channel.BasicGetAsync(dlqQueue, autoAck: true).ConfigureAwait(false);
        dlqResult.ShouldNotBeNull();
        dlqResult.BasicProperties.MessageId.ShouldBe("dlx-msg-001");
        Encoding.UTF8.GetString(dlqResult.Body.ToArray()).ShouldBe("dead letter test");
    }

    [SkippableFact]
    public async Task DeadLetteredMessage_ContainsXDeathHeaders()
    {
        Skip.IfNot(_dockerAvailable, "Docker is not available");

        // Arrange
        var dlxExchange = $"dlx-xdeath-{Guid.NewGuid():N}";
        var dlqQueue = $"dlq-xdeath-{Guid.NewGuid():N}";
        var mainQueue = $"main-xdeath-{Guid.NewGuid():N}";

        await _channel!.ExchangeDeclareAsync(
            exchange: dlxExchange,
            type: ExchangeType.Fanout,
            durable: false,
            autoDelete: true).ConfigureAwait(false);

        await _channel.QueueDeclareAsync(
            queue: dlqQueue,
            durable: false,
            exclusive: false,
            autoDelete: true).ConfigureAwait(false);

        await _channel.QueueBindAsync(
            queue: dlqQueue,
            exchange: dlxExchange,
            routingKey: string.Empty).ConfigureAwait(false);

        await _channel.QueueDeclareAsync(
            queue: mainQueue,
            durable: false,
            exclusive: false,
            autoDelete: true,
            arguments: new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = dlxExchange,
            }).ConfigureAwait(false);

        await _channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: mainQueue,
            mandatory: false,
            basicProperties: new BasicProperties
            {
                MessageId = "xdeath-msg",
                ContentType = "application/json",
                Type = "TestEvent",
            },
            body: Encoding.UTF8.GetBytes("{\"data\":1}")).ConfigureAwait(false);

        await Task.Delay(500).ConfigureAwait(false);

        // Act - reject to trigger DLX routing
        var mainResult = await _channel.BasicGetAsync(mainQueue, autoAck: false).ConfigureAwait(false);
        mainResult.ShouldNotBeNull();
        await _channel.BasicNackAsync(mainResult.DeliveryTag, multiple: false, requeue: false).ConfigureAwait(false);

        await Task.Delay(1000).ConfigureAwait(false);

        // Assert - DLQ message should contain x-death header (added by RabbitMQ)
        var dlqResult = await _channel.BasicGetAsync(dlqQueue, autoAck: true).ConfigureAwait(false);
        dlqResult.ShouldNotBeNull();
        dlqResult.BasicProperties.Headers.ShouldNotBeNull();
        dlqResult.BasicProperties.Headers.ShouldContainKey("x-death");

        // Original properties should be preserved
        dlqResult.BasicProperties.MessageId.ShouldBe("xdeath-msg");
        dlqResult.BasicProperties.ContentType.ShouldBe("application/json");
        dlqResult.BasicProperties.Type.ShouldBe("TestEvent");
    }

    [SkippableFact]
    public async Task DirectPublishToDlq_MessageArrives()
    {
        Skip.IfNot(_dockerAvailable, "Docker is not available");

        // Arrange - DLX exchange + DLQ queue
        var dlxExchange = $"dlx-direct-{Guid.NewGuid():N}";
        var dlqQueue = $"dlq-direct-{Guid.NewGuid():N}";

        await _channel!.ExchangeDeclareAsync(
            exchange: dlxExchange,
            type: ExchangeType.Fanout,
            durable: false,
            autoDelete: true).ConfigureAwait(false);

        await _channel.QueueDeclareAsync(
            queue: dlqQueue,
            durable: false,
            exclusive: false,
            autoDelete: true).ConfigureAwait(false);

        await _channel.QueueBindAsync(
            queue: dlqQueue,
            exchange: dlxExchange,
            routingKey: string.Empty).ConfigureAwait(false);

        // Act - publish directly to DLX (simulates programmatic DLQ move)
        var properties = new BasicProperties
        {
            MessageId = "direct-dlq-msg",
            ContentType = "text/plain",
            Headers = new Dictionary<string, object?>
            {
                ["dlq_reason"] = "Processing failure",
                ["dlq_moved_at"] = DateTimeOffset.UtcNow.ToString("O"),
                ["dlq_original_source"] = "order-queue",
            },
        };

        await _channel.BasicPublishAsync(
            exchange: dlxExchange,
            routingKey: string.Empty,
            mandatory: false,
            basicProperties: properties,
            body: Encoding.UTF8.GetBytes("failed message body")).ConfigureAwait(false);

        await Task.Delay(500).ConfigureAwait(false);

        // Assert
        var result = await _channel.BasicGetAsync(dlqQueue, autoAck: true).ConfigureAwait(false);
        result.ShouldNotBeNull();
        result.BasicProperties.MessageId.ShouldBe("direct-dlq-msg");
        result.BasicProperties.Headers.ShouldNotBeNull();
        result.BasicProperties.Headers.ShouldContainKey("dlq_reason");
        result.BasicProperties.Headers.ShouldContainKey("dlq_moved_at");
        result.BasicProperties.Headers.ShouldContainKey("dlq_original_source");
    }

    [SkippableFact]
    public async Task MessageTtlExpiry_RoutedToDeadLetterExchange()
    {
        Skip.IfNot(_dockerAvailable, "Docker is not available");

        // Arrange - main queue with per-message TTL and DLX
        var dlxExchange = $"dlx-ttl-{Guid.NewGuid():N}";
        var dlqQueue = $"dlq-ttl-{Guid.NewGuid():N}";
        var mainQueue = $"main-ttl-{Guid.NewGuid():N}";

        await _channel!.ExchangeDeclareAsync(
            exchange: dlxExchange,
            type: ExchangeType.Fanout,
            durable: false,
            autoDelete: true).ConfigureAwait(false);

        await _channel.QueueDeclareAsync(
            queue: dlqQueue,
            durable: false,
            exclusive: false,
            autoDelete: true).ConfigureAwait(false);

        await _channel.QueueBindAsync(
            queue: dlqQueue,
            exchange: dlxExchange,
            routingKey: string.Empty).ConfigureAwait(false);

        // Main queue with DLX and short per-queue TTL (1 second)
        await _channel.QueueDeclareAsync(
            queue: mainQueue,
            durable: false,
            exclusive: false,
            autoDelete: true,
            arguments: new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = dlxExchange,
                ["x-message-ttl"] = 1000, // 1 second TTL
            }).ConfigureAwait(false);

        // Act - publish a message and wait for TTL expiry
        await _channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: mainQueue,
            mandatory: false,
            basicProperties: new BasicProperties { MessageId = "ttl-expire-msg" },
            body: Encoding.UTF8.GetBytes("ttl expired")).ConfigureAwait(false);

        // Wait for TTL to expire and DLX routing
        await Task.Delay(3000).ConfigureAwait(false);

        // Assert - message should be in DLQ after TTL expiry
        var dlqResult = await _channel.BasicGetAsync(dlqQueue, autoAck: true).ConfigureAwait(false);
        dlqResult.ShouldNotBeNull();
        dlqResult.BasicProperties.MessageId.ShouldBe("ttl-expire-msg");
        Encoding.UTF8.GetString(dlqResult.Body.ToArray()).ShouldBe("ttl expired");

        // Main queue should be empty
        var mainResult = await _channel.BasicGetAsync(mainQueue, autoAck: true).ConfigureAwait(false);
        mainResult.ShouldBeNull();
    }

    [SkippableFact]
    public async Task PurgeDlqQueue_RemovesAllMessages()
    {
        Skip.IfNot(_dockerAvailable, "Docker is not available");

        // Arrange
        var dlqQueue = $"dlq-purge-{Guid.NewGuid():N}";
        await _channel!.QueueDeclareAsync(
            queue: dlqQueue,
            durable: false,
            exclusive: false,
            autoDelete: true).ConfigureAwait(false);

        // Publish 5 messages
        for (var i = 0; i < 5; i++)
        {
            await _channel.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: dlqQueue,
                mandatory: false,
                basicProperties: new BasicProperties { MessageId = $"purge-msg-{i}" },
                body: Encoding.UTF8.GetBytes($"purge-{i}")).ConfigureAwait(false);
        }

        await Task.Delay(500).ConfigureAwait(false);

        // Act
        var purgedCount = await _channel.QueuePurgeAsync(dlqQueue).ConfigureAwait(false);

        // Assert
        purgedCount.ShouldBe(5u);

        var result = await _channel.BasicGetAsync(dlqQueue, autoAck: true).ConfigureAwait(false);
        result.ShouldBeNull();
    }

    [SkippableFact]
    public async Task DlqWithRoutingKey_RoutesToCorrectQueue()
    {
        Skip.IfNot(_dockerAvailable, "Docker is not available");

        // Arrange - DLX with direct exchange and routing key
        var dlxExchange = $"dlx-routed-{Guid.NewGuid():N}";
        var dlqHighPriority = $"dlq-high-{Guid.NewGuid():N}";
        var dlqLowPriority = $"dlq-low-{Guid.NewGuid():N}";
        var mainQueue = $"main-routed-{Guid.NewGuid():N}";

        await _channel!.ExchangeDeclareAsync(
            exchange: dlxExchange,
            type: ExchangeType.Direct,
            durable: false,
            autoDelete: true).ConfigureAwait(false);

        await _channel.QueueDeclareAsync(queue: dlqHighPriority, durable: false, exclusive: false, autoDelete: true).ConfigureAwait(false);
        await _channel.QueueDeclareAsync(queue: dlqLowPriority, durable: false, exclusive: false, autoDelete: true).ConfigureAwait(false);

        await _channel.QueueBindAsync(queue: dlqHighPriority, exchange: dlxExchange, routingKey: "high").ConfigureAwait(false);
        await _channel.QueueBindAsync(queue: dlqLowPriority, exchange: dlxExchange, routingKey: "low").ConfigureAwait(false);

        // Main queue with DLX and specific routing key
        await _channel.QueueDeclareAsync(
            queue: mainQueue,
            durable: false,
            exclusive: false,
            autoDelete: true,
            arguments: new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = dlxExchange,
                ["x-dead-letter-routing-key"] = "high",
            }).ConfigureAwait(false);

        // Act - publish and reject
        await _channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: mainQueue,
            mandatory: false,
            basicProperties: new BasicProperties { MessageId = "routed-dlq-msg" },
            body: Encoding.UTF8.GetBytes("routed dlq")).ConfigureAwait(false);

        await Task.Delay(500).ConfigureAwait(false);

        var mainResult = await _channel.BasicGetAsync(mainQueue, autoAck: false).ConfigureAwait(false);
        mainResult.ShouldNotBeNull();
        await _channel.BasicNackAsync(mainResult.DeliveryTag, multiple: false, requeue: false).ConfigureAwait(false);

        await Task.Delay(1000).ConfigureAwait(false);

        // Assert - should route to high-priority DLQ
        var highResult = await _channel.BasicGetAsync(dlqHighPriority, autoAck: true).ConfigureAwait(false);
        highResult.ShouldNotBeNull();
        highResult.BasicProperties.MessageId.ShouldBe("routed-dlq-msg");

        // Low-priority DLQ should be empty
        var lowResult = await _channel.BasicGetAsync(dlqLowPriority, autoAck: true).ConfigureAwait(false);
        lowResult.ShouldBeNull();
    }

    [SkippableFact]
    public async Task QueueDeclarePassive_ReportsMessageCount()
    {
        Skip.IfNot(_dockerAvailable, "Docker is not available");

        // Arrange
        var queueName = $"dlq-stats-{Guid.NewGuid():N}";
        await _channel!.QueueDeclareAsync(
            queue: queueName,
            durable: false,
            exclusive: false,
            autoDelete: true).ConfigureAwait(false);

        for (var i = 0; i < 3; i++)
        {
            await _channel.BasicPublishAsync(
                exchange: string.Empty,
                routingKey: queueName,
                mandatory: false,
                basicProperties: new BasicProperties { MessageId = $"stats-{i}" },
                body: Encoding.UTF8.GetBytes($"stats-{i}")).ConfigureAwait(false);
        }

        await Task.Delay(500).ConfigureAwait(false);

        // Act
        var queueInfo = await _channel.QueueDeclarePassiveAsync(queueName).ConfigureAwait(false);

        // Assert
        queueInfo.MessageCount.ShouldBe(3u);
    }
}
