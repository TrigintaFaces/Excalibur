// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Confluent.Kafka;

using Testcontainers.Kafka;

using Xunit;

namespace Excalibur.Dispatch.Tests.Conformance.Transport.Implementations;

/// <summary>
/// Conformance tests for Kafka transport using TestContainers.
/// Automatically provisions a Kafka container for testing.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Transport", "Kafka")]
public sealed class KafkaTransportConformanceTests
	: TransportConformanceTestBase<KafkaChannelSender, KafkaChannelReceiver>
{
	private const string TopicName = "conformance-test-topic";

	private KafkaContainer? _kafkaContainer;
	private IProducer<string, string>? _producer;
	private IConsumer<string, string>? _consumer;
	private KafkaDeadLetterQueueManager? _dlqManager;

	protected override async Task<KafkaChannelSender> CreateSenderAsync()
	{
		// Start Kafka container
		_kafkaContainer = new KafkaBuilder()
			.WithImage("confluentinc/cp-kafka:latest")
			.Build();

		await _kafkaContainer.StartAsync();

		// Create producer
		var producerConfig = new ProducerConfig
		{
			BootstrapServers = _kafkaContainer.GetBootstrapAddress()
		};

		_producer = new ProducerBuilder<string, string>(producerConfig).Build();

		return new KafkaChannelSender(_producer, TopicName);
	}

	protected override async Task<KafkaChannelReceiver> CreateReceiverAsync()
	{
		if (_kafkaContainer == null)
		{
			throw new InvalidOperationException("Kafka container not initialized. Ensure sender is created first.");
		}

		// Create consumer
		var consumerConfig = new ConsumerConfig
		{
			BootstrapServers = _kafkaContainer.GetBootstrapAddress(),
			GroupId = "conformance-test-group",
			AutoOffsetReset = AutoOffsetReset.Earliest,
			EnableAutoCommit = false
		};

		_consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
		_consumer.Subscribe(TopicName);

		return await Task.FromResult(new KafkaChannelReceiver(_consumer));
	}

	protected override async Task<IDeadLetterQueueManager?> CreateDlqManagerAsync()
	{
		if (_producer == null)
		{
			throw new InvalidOperationException("Kafka producer not initialized.");
		}

		_dlqManager = new KafkaDeadLetterQueueManager(_producer, $"{TopicName}-dlq");
		return await Task.FromResult<IDeadLetterQueueManager?>(_dlqManager);
	}

	protected override Task DisposeTransportAsync()
	{
		_producer?.Dispose();
		_consumer?.Dispose();

		if (_kafkaContainer != null)
		{
			return _kafkaContainer.DisposeAsync().AsTask();
		}

		return Task.CompletedTask;
	}
}

/// <summary>
/// Kafka implementation of IChannelSender for conformance testing.
/// </summary>
public sealed class KafkaChannelSender : IChannelSender
{
	private readonly IProducer<string, string> _producer;
	private readonly string _topic;

	public KafkaChannelSender(IProducer<string, string> producer, string topic)
	{
		_producer = producer ?? throw new ArgumentNullException(nameof(producer));
		_topic = topic ?? throw new ArgumentNullException(nameof(topic));
	}

	public async Task SendAsync<T>(T message, CancellationToken cancellationToken)
	{
		if (message == null)
		{
			throw new ArgumentNullException(nameof(message));
		}

		var json = System.Text.Json.JsonSerializer.Serialize(message);
		var kafkaMessage = new Message<string, string>
		{
			Key = Guid.NewGuid().ToString(),
			Value = json,
			Headers = new Headers()
		};

		// Extract metadata if available
		var messageType = typeof(T);
		if (messageType.GetProperty("MessageId") != null)
		{
			var messageId = messageType.GetProperty("MessageId").GetValue(message)?.ToString();
			if (!string.IsNullOrEmpty(messageId))
			{
				kafkaMessage.Headers.Add("MessageId", System.Text.Encoding.UTF8.GetBytes(messageId));
			}
		}

		if (messageType.GetProperty("CorrelationId") != null)
		{
			var correlationId = messageType.GetProperty("CorrelationId").GetValue(message)?.ToString();
			if (!string.IsNullOrEmpty(correlationId))
			{
				kafkaMessage.Headers.Add("CorrelationId", System.Text.Encoding.UTF8.GetBytes(correlationId));
			}
		}

		_ = await _producer.ProduceAsync(_topic, kafkaMessage, cancellationToken).ConfigureAwait(false);
	}
}

/// <summary>
/// Kafka implementation of IChannelReceiver for conformance testing.
/// </summary>
public sealed class KafkaChannelReceiver : IChannelReceiver
{
	private readonly IConsumer<string, string> _consumer;

	public KafkaChannelReceiver(IConsumer<string, string> consumer)
	{
		_consumer = consumer ?? throw new ArgumentNullException(nameof(consumer));
	}

	public async Task<T?> ReceiveAsync<T>(CancellationToken cancellationToken)
	{
		var consumeResult = _consumer.Consume(TimeSpan.FromSeconds(30));

		if (consumeResult == null)
		{
			return default;
		}

		try
		{
			var result = System.Text.Json.JsonSerializer.Deserialize<T>(consumeResult.Message.Value);

			// Commit offset after successful processing
			_consumer.Commit(consumeResult);

			return await Task.FromResult(result);
		}
		catch
		{
			// Don't commit offset on failure - message will be re-consumed
			throw;
		}
	}
}

/// <summary>
/// Kafka implementation of IDeadLetterQueueManager for conformance testing.
/// </summary>
public sealed class KafkaDeadLetterQueueManager : IDeadLetterQueueManager
{
	private readonly IProducer<string, string> _producer;
	private readonly string _dlqTopic;
	private readonly List<DeadLetterMessage> _dlqMessages = new();

	public KafkaDeadLetterQueueManager(IProducer<string, string> producer, string dlqTopic)
	{
		_producer = producer ?? throw new ArgumentNullException(nameof(producer));
		_dlqTopic = dlqTopic ?? throw new ArgumentNullException(nameof(dlqTopic));
	}

	public async Task<string> MoveToDeadLetterAsync(
		TransportMessage message,
		string reason,
		Exception? exception,
		CancellationToken cancellationToken)
	{
		var json = System.Text.Json.JsonSerializer.Serialize(message);
		var kafkaMessage = new Message<string, string>
		{
			Key = message.Id,
			Value = json,
			Headers = new Headers
			{
				{ "Reason", System.Text.Encoding.UTF8.GetBytes(reason) },
				{ "DeadLetteredAt", System.Text.Encoding.UTF8.GetBytes(DateTimeOffset.UtcNow.ToString("O")) }
			}
		};

		if (exception != null)
		{
			kafkaMessage.Headers.Add("Exception", System.Text.Encoding.UTF8.GetBytes(exception.Message));
		}

		var result = await _producer.ProduceAsync(_dlqTopic, kafkaMessage, cancellationToken).ConfigureAwait(false);

		// Track locally for testing
		_dlqMessages.Add(new DeadLetterMessage
		{
			OriginalMessage = message,
			Reason = reason,
			Exception = exception,
			DeadLetteredAt = DateTimeOffset.UtcNow
		});

		return result.Offset.Value.ToString();
	}

	public Task<IReadOnlyList<DeadLetterMessage>> GetDeadLetterMessagesAsync(
		int maxMessages,
		CancellationToken cancellationToken)
	{
		return Task.FromResult<IReadOnlyList<DeadLetterMessage>>(
			_dlqMessages.Take(maxMessages).ToList());
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

	public Task<DeadLetterStatistics> GetStatisticsAsync(
		CancellationToken cancellationToken)
	{
		return Task.FromResult(new DeadLetterStatistics
		{
			MessageCount = _dlqMessages.Count,
			OldestMessageAge = _dlqMessages.Count > 0
				? DateTimeOffset.UtcNow - _dlqMessages.Min(m => m.DeadLetteredAt)
				: TimeSpan.Zero
		});
	}

	public Task<int> PurgeDeadLetterQueueAsync(CancellationToken cancellationToken)
	{
		var count = _dlqMessages.Count;
		_dlqMessages.Clear();
		return Task.FromResult(count);
	}
}
