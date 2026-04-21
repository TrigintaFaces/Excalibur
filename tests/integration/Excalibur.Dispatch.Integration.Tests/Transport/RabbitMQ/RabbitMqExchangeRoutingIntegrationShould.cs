// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;

using RabbitMQ.Client;

using Tests.Shared.Fixtures;
using Tests.Shared.Infrastructure;

using Testcontainers.RabbitMq;

namespace Excalibur.Dispatch.Integration.Tests.Transport.RabbitMQ;

/// <summary>
/// Integration tests for RabbitMQ exchange types and routing key patterns.
/// Verifies direct, topic, fanout, and headers exchange routing behaviors
/// against a real RabbitMQ container.
/// </summary>
[Collection(ContainerCollections.RabbitMQ)]
[Trait(TraitNames.Category, TestCategories.Integration)]
[Trait("Database", "RabbitMQ")]
[Trait(TraitNames.Component, TestComponents.Transport)]
public sealed class RabbitMqExchangeRoutingIntegrationShould : IAsyncLifetime
{
    private static readonly TimeSpan MessageWaitTimeout = TestTimeouts.Scale(TimeSpan.FromSeconds(20));

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

    #region Direct Exchange

    [SkippableFact]
    public async Task DirectExchange_RoutesToExactMatchQueue()
    {
        Skip.IfNot(_dockerAvailable, "Docker is not available");

        // Arrange
        var exchangeName = $"direct-ex-{Guid.NewGuid():N}";
        var queueOrders = $"q-orders-{Guid.NewGuid():N}";
        var queuePayments = $"q-payments-{Guid.NewGuid():N}";

        await _channel!.ExchangeDeclareAsync(exchange: exchangeName, type: ExchangeType.Direct, durable: false, autoDelete: true).ConfigureAwait(false);
        await _channel.QueueDeclareAsync(queue: queueOrders, durable: false, exclusive: false, autoDelete: true).ConfigureAwait(false);
        await _channel.QueueDeclareAsync(queue: queuePayments, durable: false, exclusive: false, autoDelete: true).ConfigureAwait(false);
        await _channel.QueueBindAsync(queue: queueOrders, exchange: exchangeName, routingKey: "orders").ConfigureAwait(false);
        await _channel.QueueBindAsync(queue: queuePayments, exchange: exchangeName, routingKey: "payments").ConfigureAwait(false);

        // Act - send to "orders" routing key
        await _channel.BasicPublishAsync(
            exchange: exchangeName,
            routingKey: "orders",
            mandatory: false,
            basicProperties: new BasicProperties { MessageId = "order-msg" },
            body: Encoding.UTF8.GetBytes("order data")).ConfigureAwait(false);

        // Assert - message should be in orders queue, not payments
        var orderResult = await WaitForBasicGetAsync(_channel, queueOrders, autoAck: true, MessageWaitTimeout).ConfigureAwait(false);
        orderResult.ShouldNotBeNull();
        orderResult.BasicProperties.MessageId.ShouldBe("order-msg");

        await AssertQueueRemainsEmptyAsync(_channel, queuePayments, MessageWaitTimeout).ConfigureAwait(false);
    }

    [SkippableFact]
    public async Task DirectExchange_UnmatchedRoutingKey_MessageDropped()
    {
        Skip.IfNot(_dockerAvailable, "Docker is not available");

        // Arrange
        var exchangeName = $"direct-nomatch-{Guid.NewGuid():N}";
        var queueName = $"q-nomatch-{Guid.NewGuid():N}";

        await _channel!.ExchangeDeclareAsync(exchange: exchangeName, type: ExchangeType.Direct, durable: false, autoDelete: true).ConfigureAwait(false);
        await _channel.QueueDeclareAsync(queue: queueName, durable: false, exclusive: false, autoDelete: true).ConfigureAwait(false);
        await _channel.QueueBindAsync(queue: queueName, exchange: exchangeName, routingKey: "orders").ConfigureAwait(false);

        // Act - send to non-matching routing key
        await _channel.BasicPublishAsync(
            exchange: exchangeName,
            routingKey: "unknown",
            mandatory: false,
            basicProperties: new BasicProperties { MessageId = "lost-msg" },
            body: Encoding.UTF8.GetBytes("lost")).ConfigureAwait(false);

        // Assert - queue should be empty
        await AssertQueueRemainsEmptyAsync(_channel, queueName, MessageWaitTimeout).ConfigureAwait(false);
    }

    #endregion

    #region Topic Exchange

    [SkippableFact]
    public async Task TopicExchange_WildcardStar_MatchesSingleWord()
    {
        Skip.IfNot(_dockerAvailable, "Docker is not available");

        // Arrange
        var exchangeName = $"topic-star-{Guid.NewGuid():N}";
        var queueName = $"q-star-{Guid.NewGuid():N}";

        await _channel!.ExchangeDeclareAsync(exchange: exchangeName, type: ExchangeType.Topic, durable: false, autoDelete: true).ConfigureAwait(false);
        await _channel.QueueDeclareAsync(queue: queueName, durable: false, exclusive: false, autoDelete: true).ConfigureAwait(false);
        await _channel.QueueBindAsync(queue: queueName, exchange: exchangeName, routingKey: "events.*.created").ConfigureAwait(false);

        // Act - matching routing key
        await _channel.BasicPublishAsync(
            exchange: exchangeName,
            routingKey: "events.order.created",
            mandatory: false,
            basicProperties: new BasicProperties { MessageId = "star-match" },
            body: Encoding.UTF8.GetBytes("match")).ConfigureAwait(false);

        // Non-matching: too many segments
        await _channel.BasicPublishAsync(
            exchange: exchangeName,
            routingKey: "events.order.item.created",
            mandatory: false,
            basicProperties: new BasicProperties { MessageId = "star-nomatch" },
            body: Encoding.UTF8.GetBytes("no match")).ConfigureAwait(false);

        // Assert - only the matching message should arrive
        var result = await WaitForBasicGetAsync(_channel, queueName, autoAck: true, MessageWaitTimeout).ConfigureAwait(false);
        result.ShouldNotBeNull();
        result.BasicProperties.MessageId.ShouldBe("star-match");

        await AssertQueueRemainsEmptyAsync(_channel, queueName, MessageWaitTimeout).ConfigureAwait(false);
    }

    [SkippableFact]
    public async Task TopicExchange_WildcardHash_MatchesZeroOrMoreWords()
    {
        Skip.IfNot(_dockerAvailable, "Docker is not available");

        // Arrange
        var exchangeName = $"topic-hash-{Guid.NewGuid():N}";
        var queueName = $"q-hash-{Guid.NewGuid():N}";

        await _channel!.ExchangeDeclareAsync(exchange: exchangeName, type: ExchangeType.Topic, durable: false, autoDelete: true).ConfigureAwait(false);
        await _channel.QueueDeclareAsync(queue: queueName, durable: false, exclusive: false, autoDelete: true).ConfigureAwait(false);
        await _channel.QueueBindAsync(queue: queueName, exchange: exchangeName, routingKey: "events.#").ConfigureAwait(false);

        // Act - all should match
        await _channel.BasicPublishAsync(
            exchange: exchangeName, routingKey: "events",
            mandatory: false, basicProperties: new BasicProperties { MessageId = "h1" },
            body: Encoding.UTF8.GetBytes("1")).ConfigureAwait(false);

        await _channel.BasicPublishAsync(
            exchange: exchangeName, routingKey: "events.order",
            mandatory: false, basicProperties: new BasicProperties { MessageId = "h2" },
            body: Encoding.UTF8.GetBytes("2")).ConfigureAwait(false);

        await _channel.BasicPublishAsync(
            exchange: exchangeName, routingKey: "events.order.created",
            mandatory: false, basicProperties: new BasicProperties { MessageId = "h3" },
            body: Encoding.UTF8.GetBytes("3")).ConfigureAwait(false);

        await _channel.BasicPublishAsync(
            exchange: exchangeName, routingKey: "events.order.item.added",
            mandatory: false, basicProperties: new BasicProperties { MessageId = "h4" },
            body: Encoding.UTF8.GetBytes("4")).ConfigureAwait(false);

        // Should NOT match
        await _channel.BasicPublishAsync(
            exchange: exchangeName, routingKey: "commands.order.created",
            mandatory: false, basicProperties: new BasicProperties { MessageId = "nomatch" },
            body: Encoding.UTF8.GetBytes("no")).ConfigureAwait(false);

        // Assert - 4 messages should match
        var receivedIds = new List<string>();
        for (var i = 0; i < 4; i++)
        {
            var result = await WaitForBasicGetAsync(_channel, queueName, autoAck: true, MessageWaitTimeout).ConfigureAwait(false);
            result.ShouldNotBeNull();
            receivedIds.Add(result.BasicProperties.MessageId!);
        }

        await AssertQueueRemainsEmptyAsync(_channel, queueName, MessageWaitTimeout).ConfigureAwait(false);

        receivedIds.Count.ShouldBe(4);
        receivedIds.ShouldContain("h1");
        receivedIds.ShouldContain("h2");
        receivedIds.ShouldContain("h3");
        receivedIds.ShouldContain("h4");
        receivedIds.ShouldNotContain("nomatch");
    }

    [SkippableFact]
    public async Task TopicExchange_MultipleBindings_MessageDuplicated()
    {
        Skip.IfNot(_dockerAvailable, "Docker is not available");

        // Arrange - two queues bound to same exchange with overlapping patterns
        var exchangeName = $"topic-multi-{Guid.NewGuid():N}";
        var queueAll = $"q-all-{Guid.NewGuid():N}";
        var queueOrders = $"q-orders-{Guid.NewGuid():N}";

        await _channel!.ExchangeDeclareAsync(exchange: exchangeName, type: ExchangeType.Topic, durable: false, autoDelete: true).ConfigureAwait(false);
        await _channel.QueueDeclareAsync(queue: queueAll, durable: false, exclusive: false, autoDelete: true).ConfigureAwait(false);
        await _channel.QueueDeclareAsync(queue: queueOrders, durable: false, exclusive: false, autoDelete: true).ConfigureAwait(false);

        await _channel.QueueBindAsync(queue: queueAll, exchange: exchangeName, routingKey: "#").ConfigureAwait(false);
        await _channel.QueueBindAsync(queue: queueOrders, exchange: exchangeName, routingKey: "orders.*").ConfigureAwait(false);

        // Act
        await _channel.BasicPublishAsync(
            exchange: exchangeName,
            routingKey: "orders.created",
            mandatory: false,
            basicProperties: new BasicProperties { MessageId = "multi-msg" },
            body: Encoding.UTF8.GetBytes("order created")).ConfigureAwait(false);

        // Assert - both queues should receive the message
        var allResult = await WaitForBasicGetAsync(_channel, queueAll, autoAck: true, MessageWaitTimeout).ConfigureAwait(false);
        allResult.ShouldNotBeNull();
        allResult.BasicProperties.MessageId.ShouldBe("multi-msg");

        var ordersResult = await WaitForBasicGetAsync(_channel, queueOrders, autoAck: true, MessageWaitTimeout).ConfigureAwait(false);
        ordersResult.ShouldNotBeNull();
        ordersResult.BasicProperties.MessageId.ShouldBe("multi-msg");
    }

    #endregion

    #region Fanout Exchange

    [SkippableFact]
    public async Task FanoutExchange_BroadcastsToAllBoundQueues()
    {
        Skip.IfNot(_dockerAvailable, "Docker is not available");

        // Arrange
        var exchangeName = $"fanout-{Guid.NewGuid():N}";
        var queue1 = $"q-fan1-{Guid.NewGuid():N}";
        var queue2 = $"q-fan2-{Guid.NewGuid():N}";
        var queue3 = $"q-fan3-{Guid.NewGuid():N}";

        await _channel!.ExchangeDeclareAsync(exchange: exchangeName, type: ExchangeType.Fanout, durable: false, autoDelete: true).ConfigureAwait(false);
        await _channel.QueueDeclareAsync(queue: queue1, durable: false, exclusive: false, autoDelete: true).ConfigureAwait(false);
        await _channel.QueueDeclareAsync(queue: queue2, durable: false, exclusive: false, autoDelete: true).ConfigureAwait(false);
        await _channel.QueueDeclareAsync(queue: queue3, durable: false, exclusive: false, autoDelete: true).ConfigureAwait(false);

        await _channel.QueueBindAsync(queue: queue1, exchange: exchangeName, routingKey: string.Empty).ConfigureAwait(false);
        await _channel.QueueBindAsync(queue: queue2, exchange: exchangeName, routingKey: string.Empty).ConfigureAwait(false);
        await _channel.QueueBindAsync(queue: queue3, exchange: exchangeName, routingKey: string.Empty).ConfigureAwait(false);

        // Act - routing key is ignored for fanout
        await _channel.BasicPublishAsync(
            exchange: exchangeName,
            routingKey: "ignored-key",
            mandatory: false,
            basicProperties: new BasicProperties { MessageId = "fanout-msg" },
            body: Encoding.UTF8.GetBytes("broadcast")).ConfigureAwait(false);

        // Assert - all 3 queues should get the message
        var r1 = await WaitForBasicGetAsync(_channel, queue1, autoAck: true, MessageWaitTimeout).ConfigureAwait(false);
        var r2 = await WaitForBasicGetAsync(_channel, queue2, autoAck: true, MessageWaitTimeout).ConfigureAwait(false);
        var r3 = await WaitForBasicGetAsync(_channel, queue3, autoAck: true, MessageWaitTimeout).ConfigureAwait(false);

        r1.ShouldNotBeNull();
        r2.ShouldNotBeNull();
        r3.ShouldNotBeNull();

        r1.BasicProperties.MessageId.ShouldBe("fanout-msg");
        r2.BasicProperties.MessageId.ShouldBe("fanout-msg");
        r3.BasicProperties.MessageId.ShouldBe("fanout-msg");
    }

    [SkippableFact]
    public async Task FanoutExchange_UnboundQueue_DoesNotReceive()
    {
        Skip.IfNot(_dockerAvailable, "Docker is not available");

        // Arrange
        var exchangeName = $"fanout-unbound-{Guid.NewGuid():N}";
        var boundQueue = $"q-bound-{Guid.NewGuid():N}";
        var unboundQueue = $"q-unbound-{Guid.NewGuid():N}";

        await _channel!.ExchangeDeclareAsync(exchange: exchangeName, type: ExchangeType.Fanout, durable: false, autoDelete: true).ConfigureAwait(false);
        await _channel.QueueDeclareAsync(queue: boundQueue, durable: false, exclusive: false, autoDelete: true).ConfigureAwait(false);
        await _channel.QueueDeclareAsync(queue: unboundQueue, durable: false, exclusive: false, autoDelete: true).ConfigureAwait(false);

        // Only bind one queue
        await _channel.QueueBindAsync(queue: boundQueue, exchange: exchangeName, routingKey: string.Empty).ConfigureAwait(false);

        // Act
        await _channel.BasicPublishAsync(
            exchange: exchangeName,
            routingKey: string.Empty,
            mandatory: false,
            basicProperties: new BasicProperties { MessageId = "fanout-bound-only" },
            body: Encoding.UTF8.GetBytes("bound only")).ConfigureAwait(false);

        // Assert
        var boundResult = await WaitForBasicGetAsync(_channel, boundQueue, autoAck: true, MessageWaitTimeout).ConfigureAwait(false);
        boundResult.ShouldNotBeNull();

        await AssertQueueRemainsEmptyAsync(_channel, unboundQueue, MessageWaitTimeout).ConfigureAwait(false);
    }

    #endregion

    #region Headers Exchange

    [SkippableFact]
    public async Task HeadersExchange_MatchesOnHeaders()
    {
        Skip.IfNot(_dockerAvailable, "Docker is not available");

        // Arrange
        var exchangeName = $"headers-{Guid.NewGuid():N}";
        var matchQueue = $"q-headers-match-{Guid.NewGuid():N}";

        await _channel!.ExchangeDeclareAsync(exchange: exchangeName, type: ExchangeType.Headers, durable: false, autoDelete: true).ConfigureAwait(false);
        await _channel.QueueDeclareAsync(queue: matchQueue, durable: false, exclusive: false, autoDelete: true).ConfigureAwait(false);

        // Bind with header matching criteria (x-match: all means ALL headers must match)
        await _channel.QueueBindAsync(
            queue: matchQueue,
            exchange: exchangeName,
            routingKey: string.Empty,
            arguments: new Dictionary<string, object?>
            {
                ["x-match"] = "all",
                ["region"] = "us-east",
                ["priority"] = "high",
            }).ConfigureAwait(false);

        // Act - matching headers
        await _channel.BasicPublishAsync(
            exchange: exchangeName,
            routingKey: string.Empty,
            mandatory: false,
            basicProperties: new BasicProperties
            {
                MessageId = "headers-match",
                Headers = new Dictionary<string, object?>
                {
                    ["region"] = "us-east",
                    ["priority"] = "high",
                },
            },
            body: Encoding.UTF8.GetBytes("matching")).ConfigureAwait(false);

        // Non-matching headers (partial match)
        await _channel.BasicPublishAsync(
            exchange: exchangeName,
            routingKey: string.Empty,
            mandatory: false,
            basicProperties: new BasicProperties
            {
                MessageId = "headers-nomatch",
                Headers = new Dictionary<string, object?>
                {
                    ["region"] = "us-east",
                    ["priority"] = "low", // doesn't match
                },
            },
            body: Encoding.UTF8.GetBytes("not matching")).ConfigureAwait(false);

        // Assert - only matching message should arrive
        var result = await WaitForBasicGetAsync(_channel, matchQueue, autoAck: true, MessageWaitTimeout).ConfigureAwait(false);
        result.ShouldNotBeNull();
        result.BasicProperties.MessageId.ShouldBe("headers-match");

        await AssertQueueRemainsEmptyAsync(_channel, matchQueue, MessageWaitTimeout).ConfigureAwait(false);
    }

    [SkippableFact]
    public async Task HeadersExchange_AnyMatch_MatchesPartialHeaders()
    {
        Skip.IfNot(_dockerAvailable, "Docker is not available");

        // Arrange
        var exchangeName = $"headers-any-{Guid.NewGuid():N}";
        var matchQueue = $"q-headers-any-{Guid.NewGuid():N}";

        await _channel!.ExchangeDeclareAsync(exchange: exchangeName, type: ExchangeType.Headers, durable: false, autoDelete: true).ConfigureAwait(false);
        await _channel.QueueDeclareAsync(queue: matchQueue, durable: false, exclusive: false, autoDelete: true).ConfigureAwait(false);

        // x-match: any means ANY header match is sufficient
        await _channel.QueueBindAsync(
            queue: matchQueue,
            exchange: exchangeName,
            routingKey: string.Empty,
            arguments: new Dictionary<string, object?>
            {
                ["x-match"] = "any",
                ["region"] = "us-east",
                ["priority"] = "critical",
            }).ConfigureAwait(false);

        // Act - partial match (only region matches)
        await _channel.BasicPublishAsync(
            exchange: exchangeName,
            routingKey: string.Empty,
            mandatory: false,
            basicProperties: new BasicProperties
            {
                MessageId = "any-match",
                Headers = new Dictionary<string, object?>
                {
                    ["region"] = "us-east",
                    ["priority"] = "low", // doesn't match but that's ok with "any"
                },
            },
            body: Encoding.UTF8.GetBytes("any match")).ConfigureAwait(false);

        // Assert - should match because "region" matches
        var result = await WaitForBasicGetAsync(_channel, matchQueue, autoAck: true, MessageWaitTimeout).ConfigureAwait(false);
        result.ShouldNotBeNull();
        result.BasicProperties.MessageId.ShouldBe("any-match");
    }

    #endregion

    #region Default Exchange

    [SkippableFact]
    public async Task DefaultExchange_RoutesDirectlyToQueueName()
    {
        Skip.IfNot(_dockerAvailable, "Docker is not available");

        // Arrange
        var queueName = $"default-ex-{Guid.NewGuid():N}";
        await _channel!.QueueDeclareAsync(queue: queueName, durable: false, exclusive: false, autoDelete: true).ConfigureAwait(false);

        // Act - publish to default exchange (empty string) with queue name as routing key
        await _channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: queueName,
            mandatory: false,
            basicProperties: new BasicProperties { MessageId = "default-ex-msg" },
            body: Encoding.UTF8.GetBytes("default exchange")).ConfigureAwait(false);

        // Assert
        var result = await WaitForBasicGetAsync(_channel, queueName, autoAck: true, MessageWaitTimeout).ConfigureAwait(false);
        result.ShouldNotBeNull();
        result.BasicProperties.MessageId.ShouldBe("default-ex-msg");
    }

    #endregion

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

    private static async Task AssertQueueRemainsEmptyAsync(
        IChannel channel,
        string queueName,
        TimeSpan observationWindow)
    {
        var observedMessage = await WaitHelpers.WaitUntilAsync(
                async () =>
                {
                    var result = await channel.BasicGetAsync(queueName, autoAck: true).ConfigureAwait(false);
                    return result is not null;
                },
                observationWindow,
                TimeSpan.FromMilliseconds(100))
            .ConfigureAwait(false);

        observedMessage.ShouldBeFalse($"Expected queue '{queueName}' to remain empty.");
    }
}
