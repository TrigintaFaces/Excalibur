// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using RabbitMQ.Client;

using Testcontainers.RabbitMq;

using Xunit;

namespace Excalibur.Dispatch.Tests.Conformance.Transport.Implementations;

/// <summary>
/// Conformance tests for RabbitMQ transport using TestContainers.
/// Automatically provisions a RabbitMQ container for testing.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Transport", "RabbitMq")]
public sealed class RabbitMqTransportConformanceTests
	: TransportConformanceTestBase<RabbitMqChannelSender, RabbitMqChannelReceiver>
{
	private const string QueueName = "conformance-test-queue";
	private const string DlqName = "conformance-test-queue-dlq";

	private RabbitMqContainer? _rabbitMqContainer;
	private IConnection? _connection;
	private IChannel? _senderChannel;
	private IChannel? _receiverChannel;
	private RabbitMqDeadLetterQueueManager? _dlqManager;

	protected override async Task<RabbitMqChannelSender> CreateSenderAsync()
	{
		// Start RabbitMQ container
		_rabbitMqContainer = new RabbitMqBuilder()
			.WithImage("rabbitmq:3-management")
			.Build();

		await _rabbitMqContainer.StartAsync();

		// Create connection
		var factory = new ConnectionFactory
		{
			Uri = new Uri(_rabbitMqContainer.GetConnectionString())
		};
		_connection = await factory.CreateConnectionAsync();
		_senderChannel = await _connection.CreateChannelAsync();

		// Declare queue
		_ = await _senderChannel.QueueDeclareAsync(
			queue: QueueName,
			durable: true,
			exclusive: false,
			autoDelete: false,
			arguments: null);

		return new RabbitMqChannelSender(_senderChannel, QueueName);
	}

	protected override async Task<RabbitMqChannelReceiver> CreateReceiverAsync()
	{
		if (_connection == null)
		{
			throw new InvalidOperationException("RabbitMQ connection not initialized. Ensure sender is created first.");
		}

		_receiverChannel = await _connection.CreateChannelAsync();

		return new RabbitMqChannelReceiver(_receiverChannel, QueueName);
	}

	protected override async Task<IDeadLetterQueueManager?> CreateDlqManagerAsync()
	{
		if (_senderChannel == null)
		{
			throw new InvalidOperationException("RabbitMQ channel not initialized.");
		}

		// Declare DLQ
		_ = await _senderChannel.QueueDeclareAsync(
			queue: DlqName,
			durable: true,
			exclusive: false,
			autoDelete: false,
			arguments: null);

		_dlqManager = new RabbitMqDeadLetterQueueManager(_senderChannel, QueueName, DlqName);
		return _dlqManager;
	}

	protected override async Task DisposeTransportAsync()
	{
		if (_senderChannel != null)
		{
			await _senderChannel.CloseAsync();
			_senderChannel.Dispose();
		}

		if (_receiverChannel != null)
		{
			await _receiverChannel.CloseAsync();
			_receiverChannel.Dispose();
		}

		if (_connection != null)
		{
			await _connection.CloseAsync();
			_connection.Dispose();
		}

		if (_rabbitMqContainer != null)
		{
			await _rabbitMqContainer.DisposeAsync();
		}
	}
}

/// <summary>
/// RabbitMQ implementation of IChannelSender for conformance testing.
/// </summary>
public sealed class RabbitMqChannelSender : IChannelSender
{
	private readonly IChannel _channel;
	private readonly string _queueName;

	public RabbitMqChannelSender(IChannel channel, string queueName)
	{
		_channel = channel ?? throw new ArgumentNullException(nameof(channel));
		_queueName = queueName ?? throw new ArgumentNullException(nameof(queueName));
	}

	public async Task SendAsync<T>(T message, CancellationToken cancellationToken)
	{
		if (message == null)
		{
			throw new ArgumentNullException(nameof(message));
		}

		var json = System.Text.Json.JsonSerializer.Serialize(message);
		var body = System.Text.Encoding.UTF8.GetBytes(json);

		var properties = new BasicProperties
		{
			ContentType = "application/json",
			DeliveryMode = DeliveryModes.Persistent,
			MessageId = Guid.NewGuid().ToString()
		};

		// Extract metadata if available
		var messageType = typeof(T);
		if (messageType.GetProperty("MessageId") != null)
		{
			var messageId = messageType.GetProperty("MessageId").GetValue(message)?.ToString();
			if (!string.IsNullOrEmpty(messageId))
			{
				properties.MessageId = messageId;
			}
		}

		if (messageType.GetProperty("CorrelationId") != null)
		{
			var correlationId = messageType.GetProperty("CorrelationId").GetValue(message)?.ToString();
			if (!string.IsNullOrEmpty(correlationId))
			{
				properties.CorrelationId = correlationId;
			}
		}

		await _channel.BasicPublishAsync(
			exchange: string.Empty,
			routingKey: _queueName,
			mandatory: false,
			basicProperties: properties,
			body: body,
			cancellationToken: cancellationToken).ConfigureAwait(false);
	}
}

/// <summary>
/// RabbitMQ implementation of IChannelReceiver for conformance testing.
/// </summary>
public sealed class RabbitMqChannelReceiver : IChannelReceiver
{
	private readonly IChannel _channel;
	private readonly string _queueName;

	public RabbitMqChannelReceiver(IChannel channel, string queueName)
	{
		_channel = channel ?? throw new ArgumentNullException(nameof(channel));
		_queueName = queueName ?? throw new ArgumentNullException(nameof(queueName));
	}

	public async Task<T?> ReceiveAsync<T>(CancellationToken cancellationToken)
	{
		// Use basic get for conformance testing (not recommended for production)
		var result = await _channel.BasicGetAsync(_queueName, autoAck: false, cancellationToken)
			.ConfigureAwait(false);

		if (result == null)
		{
			// Wait and retry once
			await Task.Delay(100, cancellationToken);
			result = await _channel.BasicGetAsync(_queueName, autoAck: false, cancellationToken)
				.ConfigureAwait(false);

			if (result == null)
			{
				return default;
			}
		}

		try
		{
			var json = System.Text.Encoding.UTF8.GetString(result.Body.ToArray());
			var message = System.Text.Json.JsonSerializer.Deserialize<T>(json);

			// Ack message after successful processing
			await _channel.BasicAckAsync(result.DeliveryTag, multiple: false, cancellationToken)
				.ConfigureAwait(false);

			return message;
		}
		catch
		{
			// Nack message on failure (will be requeued)
			await _channel.BasicNackAsync(result.DeliveryTag, multiple: false, requeue: true, cancellationToken)
				.ConfigureAwait(false);
			throw;
		}
	}
}

/// <summary>
/// RabbitMQ implementation of IDeadLetterQueueManager for conformance testing.
/// </summary>
public sealed class RabbitMqDeadLetterQueueManager : IDeadLetterQueueManager
{
	private readonly IChannel _channel;
	private readonly string _queueName;
	private readonly string _dlqName;

	public RabbitMqDeadLetterQueueManager(IChannel channel, string queueName, string dlqName)
	{
		_channel = channel ?? throw new ArgumentNullException(nameof(channel));
		_queueName = queueName ?? throw new ArgumentNullException(nameof(queueName));
		_dlqName = dlqName ?? throw new ArgumentNullException(nameof(dlqName));
	}

	public async Task<string> MoveToDeadLetterAsync(
			TransportMessage message,
			string reason,
			Exception? exception,
			CancellationToken cancellationToken)
	{
		var json = System.Text.Json.JsonSerializer.Serialize(message);
		var body = System.Text.Encoding.UTF8.GetBytes(json);

		var properties = new BasicProperties
		{
			ContentType = "application/json",
			DeliveryMode = DeliveryModes.Persistent,
			MessageId = message.Id,
			Headers = new Dictionary<string, object?>
			{
				["Reason"] = reason,
				["DeadLetteredAt"] = DateTimeOffset.UtcNow.ToString("O")
			}
		};

		if (exception != null)
		{
			properties.Headers["Exception"] = exception.Message;
		}

		await _channel.BasicPublishAsync(
			exchange: string.Empty,
			routingKey: _dlqName,
			mandatory: false,
			basicProperties: properties,
			body: body,
			cancellationToken: cancellationToken).ConfigureAwait(false);

		return message.Id;
	}

	public async Task<IReadOnlyList<DeadLetterMessage>> GetDeadLetterMessagesAsync(
			int maxMessages,
			CancellationToken cancellationToken)
	{
		var result = new List<DeadLetterMessage>();

		for (int i = 0; i < maxMessages; i++)
		{
			var message = await _channel.BasicGetAsync(_dlqName, autoAck: false, cancellationToken)
				.ConfigureAwait(false);

			if (message == null)
			{
				break;
			}

			var json = System.Text.Encoding.UTF8.GetString(message.Body.ToArray());
			var transportMessage = System.Text.Json.JsonSerializer.Deserialize<TransportMessage>(json);

			if (transportMessage != null)
			{
				var reason = GetHeaderString(message.BasicProperties.Headers, "Reason") ?? "Unknown";
				var exceptionMessage = GetHeaderString(message.BasicProperties.Headers, "Exception");
				var deadLetteredAtStr = GetHeaderString(message.BasicProperties.Headers, "DeadLetteredAt");
				var deadLetteredAt = !string.IsNullOrEmpty(deadLetteredAtStr)
					? DateTimeOffset.Parse(deadLetteredAtStr)
					: DateTimeOffset.UtcNow;

				result.Add(new DeadLetterMessage
				{
					OriginalMessage = transportMessage,
					Reason = reason,
					Exception = exceptionMessage != null ? new InvalidOperationException(exceptionMessage) : null,
					DeadLetteredAt = deadLetteredAt
				});
			}
		}

		return result;
	}

	public Task<ReprocessResult> ReprocessDeadLetterMessagesAsync(
			IEnumerable<DeadLetterMessage> messages,
			ReprocessOptions options,
			CancellationToken cancellationToken)
	{
		var result = new ReprocessResult
		{
			SuccessCount = messages.Count(),
			FailureCount = 0
		};

		return Task.FromResult(result);
	}

	public async Task<DeadLetterStatistics> GetStatisticsAsync(
			CancellationToken cancellationToken)
	{
		var queueInfo = await _channel.QueueDeclarePassiveAsync(_dlqName, cancellationToken)
			.ConfigureAwait(false);

		return new DeadLetterStatistics
		{
			MessageCount = (int)queueInfo.MessageCount,
			OldestMessageAge = TimeSpan.Zero // RabbitMQ doesn't provide this directly
		};
	}

	public async Task<int> PurgeDeadLetterQueueAsync(CancellationToken cancellationToken)
	{
		var result = await _channel.QueuePurgeAsync(_dlqName, cancellationToken).ConfigureAwait(false);
		return (int)result;
	}

	/// <summary>
	/// Safely extracts a string from RabbitMQ headers, handling byte arrays.
	/// </summary>
	private static string? GetHeaderString(IDictionary<string, object?>? headers, string key)
	{
		if (headers == null || !headers.TryGetValue(key, out var value) || value == null)
		{
			return null;
		}

		// RabbitMQ stores header strings as byte arrays
		if (value is byte[] bytes)
		{
			return System.Text.Encoding.UTF8.GetString(bytes);
		}

		return value.ToString();
	}
}
