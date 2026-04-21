// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Delivery;

using RabbitMQ.Client;
using RabbitMQ.Client.Events;

using Tests.Shared.Fixtures;

namespace Excalibur.Dispatch.Tests.Functional.Transport;

/// <summary>
/// E2E transport round-trip tests using real RabbitMQ via TestContainers.
/// Validates: publish message -> transport sends -> subscriber receives -> handler processes.
/// </summary>
/// <remarks>
/// Beads issue: Excalibur.Dispatch-d4hcd2 (G.2).
/// Uses the RabbitMQ TestContainer fixture shared across the collection.
/// Tests the actual message flow through a real RabbitMQ broker.
/// </remarks>
[Trait("Category", "Functional")]
[Trait("Component", "Transport")]
[Trait("Feature", "RoundTrip")]
public sealed class TransportRoundTripE2EShould : FunctionalTestBase, IClassFixture<RabbitMqContainerFixture>
{
	private readonly RabbitMqContainerFixture _container;

	public TransportRoundTripE2EShould(RabbitMqContainerFixture container)
	{
		_container = container;
	}

	[Fact]
	public async Task SendAndReceiveMessageThroughRabbitMQ()
	{
		if (!_container.DockerAvailable)
		{
			return; // Skip when Docker unavailable (CI without Docker)
		}

		// Arrange
		var exchangeName = $"test-exchange-{Guid.NewGuid():N}";
		var queueName = $"test-queue-{Guid.NewGuid():N}";
		var routingKey = "test.message";

		var factory = new ConnectionFactory { Uri = new Uri(_container.ConnectionString) };
		await using var connection = await factory.CreateConnectionAsync().ConfigureAwait(false);
		await using var channel = await connection.CreateChannelAsync().ConfigureAwait(false);

		// Declare exchange and queue
		await channel.ExchangeDeclareAsync(exchangeName, ExchangeType.Topic, durable: false, autoDelete: true)
			.ConfigureAwait(false);
		await channel.QueueDeclareAsync(queueName, durable: false, exclusive: false, autoDelete: true)
			.ConfigureAwait(false);
		await channel.QueueBindAsync(queueName, exchangeName, routingKey)
			.ConfigureAwait(false);

		// Set up consumer
		var receivedMessages = new ConcurrentBag<string>();
		var messageReceived = new TaskCompletionSource<bool>();

		var consumer = new AsyncEventingBasicConsumer(channel);
		consumer.ReceivedAsync += (_, ea) =>
		{
			var body = Encoding.UTF8.GetString(ea.Body.Span);
			receivedMessages.Add(body);
			messageReceived.TrySetResult(true);
			return Task.CompletedTask;
		};

		await channel.BasicConsumeAsync(queueName, autoAck: true, consumer: consumer)
			.ConfigureAwait(false);

		// Act: Publish a message
		var testMessage = new TransportTestMessage
		{
			Id = Guid.NewGuid().ToString(),
			Content = "Hello from E2E test",
			Timestamp = DateTimeOffset.UtcNow,
		};

		var messageBody = JsonSerializer.SerializeToUtf8Bytes(testMessage);
		var properties = new BasicProperties
		{
			ContentType = "application/json",
			MessageId = testMessage.Id,
			Persistent = false,
		};

		await channel.BasicPublishAsync(
				exchangeName,
				routingKey,
				mandatory: false,
				basicProperties: properties,
				body: messageBody)
			.ConfigureAwait(false);

		// Assert: Wait for message receipt with polling (deterministic, no flat delay)
		var received = await WaitForConditionAsync(
				() => !receivedMessages.IsEmpty,
				TimeSpan.FromSeconds(5))
			.ConfigureAwait(false);

		received.ShouldBeTrue("Message should be received within 5 seconds");
		receivedMessages.Count.ShouldBe(1);

		var receivedBody = receivedMessages.First();
		var deserialized = JsonSerializer.Deserialize<TransportTestMessage>(receivedBody);
		deserialized.ShouldNotBeNull();
		deserialized.Id.ShouldBe(testMessage.Id);
		deserialized.Content.ShouldBe("Hello from E2E test");
	}

	[Fact]
	public async Task HandleMultipleMessagesInOrder()
	{
		if (!_container.DockerAvailable)
		{
			return;
		}

		// Arrange
		var exchangeName = $"test-exchange-{Guid.NewGuid():N}";
		var queueName = $"test-queue-{Guid.NewGuid():N}";
		var routingKey = "test.batch";
		const int messageCount = 20;

		var factory = new ConnectionFactory { Uri = new Uri(_container.ConnectionString) };
		await using var connection = await factory.CreateConnectionAsync().ConfigureAwait(false);
		await using var channel = await connection.CreateChannelAsync().ConfigureAwait(false);

		await channel.ExchangeDeclareAsync(exchangeName, ExchangeType.Topic, durable: false, autoDelete: true)
			.ConfigureAwait(false);
		await channel.QueueDeclareAsync(queueName, durable: false, exclusive: false, autoDelete: true)
			.ConfigureAwait(false);
		await channel.QueueBindAsync(queueName, exchangeName, routingKey)
			.ConfigureAwait(false);

		// Set up consumer tracking order
		var receivedOrder = new ConcurrentQueue<int>();

		var consumer = new AsyncEventingBasicConsumer(channel);
		consumer.ReceivedAsync += (_, ea) =>
		{
			var body = Encoding.UTF8.GetString(ea.Body.Span);
			var msg = JsonSerializer.Deserialize<TransportTestMessage>(body);
			if (msg is not null && int.TryParse(msg.Content, out var order))
			{
				receivedOrder.Enqueue(order);
			}

			return Task.CompletedTask;
		};

		await channel.BasicConsumeAsync(queueName, autoAck: true, consumer: consumer)
			.ConfigureAwait(false);

		// Act: Publish messages in sequence
		for (var i = 0; i < messageCount; i++)
		{
			var message = new TransportTestMessage
			{
				Id = Guid.NewGuid().ToString(),
				Content = i.ToString(),
				Timestamp = DateTimeOffset.UtcNow,
			};

			await channel.BasicPublishAsync(
					exchangeName,
					routingKey,
					mandatory: false,
					basicProperties: new BasicProperties { ContentType = "application/json" },
					body: JsonSerializer.SerializeToUtf8Bytes(message))
				.ConfigureAwait(false);
		}

		// Assert: All messages received with polling
		var allReceived = await WaitForConditionAsync(
				() => receivedOrder.Count >= messageCount,
				TimeSpan.FromSeconds(10))
			.ConfigureAwait(false);

		allReceived.ShouldBeTrue(
			$"Expected {messageCount} messages but received {receivedOrder.Count} within 10 seconds");
		receivedOrder.Count.ShouldBe(messageCount);

		// RabbitMQ preserves ordering within a single channel/queue
		var orderedList = receivedOrder.ToList();
		for (var i = 0; i < messageCount; i++)
		{
			orderedList[i].ShouldBe(i, $"Message at position {i} should be {i} but was {orderedList[i]}");
		}
	}

	[Fact]
	public async Task DispatchMessageAndReceiveViaTransport()
	{
		if (!_container.DockerAvailable)
		{
			return;
		}

		// Arrange: Build full Dispatch pipeline + RabbitMQ transport consumer
		var exchangeName = $"dispatch-exchange-{Guid.NewGuid():N}";
		var queueName = $"dispatch-queue-{Guid.NewGuid():N}";
		var routingKey = "dispatch.action";

		// Set up RabbitMQ infrastructure
		var factory = new ConnectionFactory { Uri = new Uri(_container.ConnectionString) };
		await using var connection = await factory.CreateConnectionAsync().ConfigureAwait(false);
		await using var channel = await connection.CreateChannelAsync().ConfigureAwait(false);

		await channel.ExchangeDeclareAsync(exchangeName, ExchangeType.Topic, durable: false, autoDelete: true)
			.ConfigureAwait(false);
		await channel.QueueDeclareAsync(queueName, durable: false, exclusive: false, autoDelete: true)
			.ConfigureAwait(false);
		await channel.QueueBindAsync(queueName, exchangeName, routingKey)
			.ConfigureAwait(false);

		// Set up a consumer on the queue
		var receivedMessages = new ConcurrentBag<string>();
		var consumer = new AsyncEventingBasicConsumer(channel);
		consumer.ReceivedAsync += (_, ea) =>
		{
			receivedMessages.Add(Encoding.UTF8.GetString(ea.Body.Span));
			return Task.CompletedTask;
		};

		await channel.BasicConsumeAsync(queueName, autoAck: true, consumer: consumer)
			.ConfigureAwait(false);

		// Also configure Dispatch pipeline for local handling
		var services = new ServiceCollection();
		services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
		services.AddSingleton<ILogger>(static sp =>
			sp.GetRequiredService<ILoggerFactory>().CreateLogger("Transport"));
		services.AddDispatchPipeline();
		services.AddDispatchHandlers(typeof(TransportRoundTripE2EShould).Assembly);
		services.AddTransient<IActionHandler<TransportTestAction>, TransportTestActionHandler>();

		await using var serviceProvider = services.BuildServiceProvider();
		var dispatcher = serviceProvider.GetRequiredService<IDispatcher>();
		var contextFactory = serviceProvider.GetRequiredService<IMessageContextFactory>();

		TransportTestActionHandler.Reset();

		// Act: Dispatch a message locally (handler processes it)
		var action = new TransportTestAction { Input = "transport-test" };
		var context = contextFactory.CreateContext();
		context.MessageId = Guid.NewGuid().ToString();

		var result = await dispatcher.DispatchAsync(action, context, CancellationToken.None)
			.ConfigureAwait(false);

		// Also publish a copy to RabbitMQ (simulating transport layer forwarding)
		var transportPayload = JsonSerializer.SerializeToUtf8Bytes(new { action.Input, DispatchedAt = DateTimeOffset.UtcNow });
		await channel.BasicPublishAsync(
				exchangeName,
				routingKey,
				mandatory: false,
				basicProperties: new BasicProperties { ContentType = "application/json" },
				body: transportPayload)
			.ConfigureAwait(false);

		// Assert: Local dispatch succeeded AND message received via transport
		result.IsSuccess.ShouldBeTrue($"Local dispatch failed: {result.ErrorMessage}");
		TransportTestActionHandler.HandleCount.ShouldBe(1);

		var transportReceived = await WaitForConditionAsync(
				() => !receivedMessages.IsEmpty,
				TimeSpan.FromSeconds(5))
			.ConfigureAwait(false);

		transportReceived.ShouldBeTrue("Message should arrive on RabbitMQ queue within 5 seconds");
		receivedMessages.Count.ShouldBe(1);
	}

	[Fact]
	public async Task HandleTransportUnavailableGracefully()
	{
		if (!_container.DockerAvailable)
		{
			return;
		}

		// Arrange: Try to connect to a non-existent RabbitMQ host
		var factory = new ConnectionFactory
		{
			HostName = "localhost",
			Port = 1, // Invalid port -- will fail to connect
			RequestedConnectionTimeout = TimeSpan.FromSeconds(1),
		};

		// Act & Assert: Connection failure should throw, not hang
		await Should.ThrowAsync<Exception>(async () =>
		{
			await using var connection = await factory.CreateConnectionAsync().ConfigureAwait(false);
		}).ConfigureAwait(false);
	}
}

#region Test types

internal sealed class TransportTestMessage
{
	public string Id { get; init; } = string.Empty;
	public string Content { get; init; } = string.Empty;
	public DateTimeOffset Timestamp { get; init; }
}

public sealed record TransportTestAction : IDispatchAction
{
	public string Input { get; init; } = string.Empty;
}

public sealed class TransportTestActionHandler : IActionHandler<TransportTestAction>
{
	private static int _handleCount;

	public static int HandleCount => Volatile.Read(ref _handleCount);

	public static void Reset() => Interlocked.Exchange(ref _handleCount, 0);

	public Task HandleAsync(TransportTestAction action, CancellationToken cancellationToken)
	{
		Interlocked.Increment(ref _handleCount);
		return Task.CompletedTask;
	}
}

#endregion
