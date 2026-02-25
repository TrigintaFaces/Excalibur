// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;
using System.Text;

using Confluent.Kafka;

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Kafka;

using Microsoft.Extensions.Logging.Abstractions;

using Testcontainers.Kafka;

namespace Excalibur.Dispatch.Integration.Tests.Transport.Kafka;

/// <summary>
/// Integration tests for Kafka dead letter queue (DLQ) produce and consume with a real Kafka container.
/// Both KafkaDeadLetterProducer and KafkaDeadLetterConsumer are internal, so we use explicit
/// reflection (MethodInfo.Invoke) to invoke their methods — dynamic dispatch cannot resolve
/// methods on internal types across assembly boundaries.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Provider", "Kafka")]
[Trait("Component", "Transport")]
public sealed class KafkaDeadLetterIntegrationShould : IAsyncLifetime
{
	private KafkaContainer? _container;
	private string? _bootstrapServers;

	public async Task InitializeAsync()
	{
		_container = new KafkaBuilder()
			.WithImage("confluentinc/cp-kafka:7.5.0")
			.WithName($"kafka-dlq-test-{Guid.NewGuid():N}")
			.Build();

		await _container.StartAsync().ConfigureAwait(false);
		_bootstrapServers = _container.GetBootstrapAddress();
	}

	public async Task DisposeAsync()
	{
		if (_container is not null)
		{
			await _container.DisposeAsync().ConfigureAwait(false);
		}
	}

	[Fact]
	public async Task ProduceAsync_SendsMessageToDlqTopic()
	{
		// Arrange
		var sourceTopic = $"test-source-{Guid.NewGuid():N}";
		var dlqTopic = $"{sourceTopic}.dead-letter";

		using var producer = BuildProducer();
		var (dlqProducer, produceAsync, _) = CreateDlqProducerWithMethods(producer);

		var originalMessage = new TransportMessage
		{
			Id = "original-msg-1",
			Body = Encoding.UTF8.GetBytes("""{"data":"test"}"""),
			ContentType = "application/json",
		};

		// Act - throw and catch to populate the stack trace
		Exception capturedException;
		try
		{
			throw new TimeoutException("Operation timed out");
		}
		catch (TimeoutException ex)
		{
			capturedException = ex;
		}

		var task = (Task<string>)produceAsync.Invoke(dlqProducer, [
			originalMessage,
			sourceTopic,
			"Processing failed: timeout",
			3,
			CancellationToken.None,
			capturedException,
		])!;
		var messageId = await task.ConfigureAwait(false);

		// Assert
		messageId.ShouldNotBeNullOrEmpty();
		messageId.ShouldContain(dlqTopic);

		// Verify the message is on the DLQ topic via raw consumer
		using var consumer = BuildRawConsumer(dlqTopic, $"verify-dlq-{Guid.NewGuid():N}");
		consumer.Subscribe(dlqTopic);

		var consumed = ConsumeWithRetry(consumer, TimeSpan.FromSeconds(15));
		consumed.ShouldNotBeNull();
		consumed.Message.Value.ShouldBe(originalMessage.Body.ToArray());

		// Verify DLQ headers
		GetHeaderValue(consumed.Message.Headers, "dlq_reason").ShouldBe("Processing failed: timeout");
		GetHeaderValue(consumed.Message.Headers, "dlq_original_topic").ShouldBe(sourceTopic);
		GetHeaderValue(consumed.Message.Headers, "dlq_attempt_count").ShouldBe("3");
		GetHeaderValue(consumed.Message.Headers, "dlq_moved_at").ShouldNotBeNull();
		GetHeaderValue(consumed.Message.Headers, "dlq_exception_type").ShouldContain("TimeoutException");
		GetHeaderValue(consumed.Message.Headers, "dlq_exception_message").ShouldBe("Operation timed out");
		GetHeaderValue(consumed.Message.Headers, "dlq_stack_trace").ShouldNotBeNull();
	}

	[Fact]
	public async Task ProduceAsync_WithoutException_OmitsExceptionHeaders()
	{
		// Arrange
		var sourceTopic = $"test-no-exc-{Guid.NewGuid():N}";
		var dlqTopic = $"{sourceTopic}.dead-letter";

		using var producer = BuildProducer();
		var (dlqProducer, produceAsync, _) = CreateDlqProducerWithMethods(producer);

		var originalMessage = new TransportMessage
		{
			Id = "no-exc-msg",
			Body = Encoding.UTF8.GetBytes("test-body"),
		};

		// Act - call without exception (pass null for the optional parameter)
		var task = (Task<string>)produceAsync.Invoke(dlqProducer, [
			originalMessage,
			sourceTopic,
			"Max retries exceeded",
			5,
			CancellationToken.None,
			null,
		])!;
		await task.ConfigureAwait(false);

		// Assert - verify no exception headers
		using var consumer = BuildRawConsumer(dlqTopic, $"verify-no-exc-{Guid.NewGuid():N}");
		consumer.Subscribe(dlqTopic);

		var consumed = ConsumeWithRetry(consumer, TimeSpan.FromSeconds(15));
		consumed.ShouldNotBeNull();

		GetHeaderValue(consumed.Message.Headers, "dlq_reason").ShouldBe("Max retries exceeded");
		GetHeaderValue(consumed.Message.Headers, "dlq_exception_type").ShouldBeNull();
		GetHeaderValue(consumed.Message.Headers, "dlq_exception_message").ShouldBeNull();
	}

	[Fact]
	public async Task DlqConsumer_ConsumesFromDlqTopic()
	{
		// Arrange
		var sourceTopic = $"test-consume-dlq-{Guid.NewGuid():N}";
		var dlqTopic = $"{sourceTopic}.dead-letter";

		// Produce a DLQ message
		using var producer = BuildProducer();
		var (dlqProducer, produceAsync, _) = CreateDlqProducerWithMethods(producer);

		var originalMessage = new TransportMessage
		{
			Id = "consume-msg-1",
			Body = Encoding.UTF8.GetBytes("consume-body"),
		};

		var produceTask = (Task<string>)produceAsync.Invoke(dlqProducer, [
			originalMessage,
			sourceTopic,
			"Handler failed",
			2,
			CancellationToken.None,
			null,
		])!;
		await produceTask.ConfigureAwait(false);

		producer.Flush(TimeSpan.FromSeconds(5));

		// Act - consume via KafkaDeadLetterConsumer (internal, accessed via reflection)
		var (dlqConsumer, consumeMethod, _) = CreateDlqConsumerWithMethods($"dlq-consumer-{Guid.NewGuid():N}");
		using var disposable = (IDisposable)dlqConsumer;

		// Retry consuming with polling for partition assignment
		IReadOnlyList<DeadLetterMessage>? messages = null;
		var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
		while (DateTime.UtcNow < deadline)
		{
			messages = (IReadOnlyList<DeadLetterMessage>)consumeMethod.Invoke(
				dlqConsumer, [dlqTopic, 10, CancellationToken.None])!;
			if (messages.Count > 0)
			{
				break;
			}

			await Task.Delay(200).ConfigureAwait(false);
		}

		// Assert
		messages.ShouldNotBeNull();
		messages.Count.ShouldBe(1);

		var dlqMessage = messages[0];
		dlqMessage.Reason.ShouldBe("Handler failed");
		dlqMessage.OriginalSource.ShouldBe(sourceTopic);
		dlqMessage.DeliveryAttempts.ShouldBe(2);
		dlqMessage.OriginalMessage.ShouldNotBeNull();
		dlqMessage.OriginalMessage.Body.ToArray().ShouldBe(Encoding.UTF8.GetBytes("consume-body"));
		dlqMessage.Metadata.ShouldContainKey("kafka_topic");
		dlqMessage.Metadata.ShouldContainKey("kafka_partition");
		dlqMessage.Metadata.ShouldContainKey("kafka_offset");
	}

	[Fact]
	public async Task DlqConsumer_PeekDoesNotCommitOffset()
	{
		// Arrange
		var sourceTopic = $"test-peek-dlq-{Guid.NewGuid():N}";
		var dlqTopic = $"{sourceTopic}.dead-letter";
		var dlqGroupId = $"dlq-peek-{Guid.NewGuid():N}";

		using var producer = BuildProducer();
		var (dlqProducer, produceAsync, _) = CreateDlqProducerWithMethods(producer);

		var produceTask = (Task<string>)produceAsync.Invoke(dlqProducer, [
			new TransportMessage { Id = "peek-msg", Body = Encoding.UTF8.GetBytes("peek-body") },
			sourceTopic,
			"Peek test",
			1,
			CancellationToken.None,
			null,
		])!;
		await produceTask.ConfigureAwait(false);

		producer.Flush(TimeSpan.FromSeconds(5));

		// Act - peek (should not commit)
		IReadOnlyList<DeadLetterMessage>? peeked = null;
		{
			var (dlqConsumer1, _, peekMethod1) = CreateDlqConsumerWithMethods(dlqGroupId);
			using var disposable1 = (IDisposable)dlqConsumer1;

			var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
			while (DateTime.UtcNow < deadline)
			{
				peeked = (IReadOnlyList<DeadLetterMessage>)peekMethod1.Invoke(
					dlqConsumer1, [dlqTopic, 10, CancellationToken.None])!;
				if (peeked.Count > 0)
				{
					break;
				}

				await Task.Delay(200).ConfigureAwait(false);
			}

			peeked.ShouldNotBeNull();
			peeked.Count.ShouldBe(1);
		}

		// Act - consume with same group should still see the message (offset not committed)
		IReadOnlyList<DeadLetterMessage>? consumed = null;
		{
			var (dlqConsumer2, consumeMethod2, _) = CreateDlqConsumerWithMethods(dlqGroupId);
			using var disposable2 = (IDisposable)dlqConsumer2;

			var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
			while (DateTime.UtcNow < deadline)
			{
				consumed = (IReadOnlyList<DeadLetterMessage>)consumeMethod2.Invoke(
					dlqConsumer2, [dlqTopic, 10, CancellationToken.None])!;
				if (consumed.Count > 0)
				{
					break;
				}

				await Task.Delay(200).ConfigureAwait(false);
			}
		}

		// Assert - message should still be available since peek doesn't commit
		consumed.ShouldNotBeNull();
		consumed.Count.ShouldBe(1);
		consumed[0].Reason.ShouldBe("Peek test");
	}

	[Fact]
	public async Task DlqTopicNaming_UsesConfiguredSuffix()
	{
		// Arrange
		var options = new KafkaDeadLetterOptions
		{
			TopicSuffix = ".dead-letter",
		};

		// Act
		var dlqTopicName = options.GetDeadLetterTopicName("orders");

		// Assert
		dlqTopicName.ShouldBe("orders.dead-letter");

		await Task.CompletedTask.ConfigureAwait(false);
	}

	[Fact]
	public async Task ProduceToOriginalTopicAsync_ReprocessesMessage()
	{
		// Arrange
		var originalTopic = $"test-reprocess-{Guid.NewGuid():N}";

		using var producer = BuildProducer();
		var (dlqProducer, _, produceToOriginalAsync) = CreateDlqProducerWithMethods(producer);

		var message = new TransportMessage
		{
			Id = "reprocess-msg",
			Body = Encoding.UTF8.GetBytes("reprocess-body"),
		};

		// Act - produce directly to original topic (reprocessing)
		var task = (Task<string>)produceToOriginalAsync.Invoke(dlqProducer, [
			message, originalTopic, CancellationToken.None,
		])!;
		var messageId = await task.ConfigureAwait(false);

		// Assert
		messageId.ShouldNotBeNullOrEmpty();
		messageId.ShouldContain(originalTopic);

		// Verify on the original topic
		using var consumer = BuildRawConsumer(originalTopic, $"verify-reprocess-{Guid.NewGuid():N}");
		consumer.Subscribe(originalTopic);

		var consumed = ConsumeWithRetry(consumer, TimeSpan.FromSeconds(15));
		consumed.ShouldNotBeNull();
		consumed.Message.Value.ShouldBe(Encoding.UTF8.GetBytes("reprocess-body"));
	}

	#region Helpers

	private IProducer<string, byte[]> BuildProducer()
	{
		var config = new ProducerConfig
		{
			BootstrapServers = _bootstrapServers,
			AllowAutoCreateTopics = true,
		};

		return new ProducerBuilder<string, byte[]>(config).Build();
	}

	private IConsumer<string, byte[]> BuildRawConsumer(string topic, string groupId)
	{
		var config = new ConsumerConfig
		{
			BootstrapServers = _bootstrapServers,
			GroupId = groupId,
			AutoOffsetReset = AutoOffsetReset.Earliest,
			EnableAutoCommit = true,
		};

		return new ConsumerBuilder<string, byte[]>(config).Build();
	}

	/// <summary>
	/// Creates a KafkaDeadLetterProducer (internal) via reflection and returns the instance
	/// plus MethodInfo handles for ProduceAsync and ProduceToOriginalTopicAsync.
	/// </summary>
	private (object Instance, MethodInfo ProduceAsync, MethodInfo ProduceToOriginalAsync) CreateDlqProducerWithMethods(
		IProducer<string, byte[]> producer)
	{
		var producerType = typeof(KafkaOptions).Assembly.GetType(
			"Excalibur.Dispatch.Transport.Kafka.KafkaDeadLetterProducer")!;

		var loggerOfT = typeof(Logger<>).MakeGenericType(producerType);
		var logger = Activator.CreateInstance(loggerOfT, NullLoggerFactory.Instance)!;

		var options = Microsoft.Extensions.Options.Options.Create(new KafkaDeadLetterOptions
		{
			IncludeStackTrace = true,
		});

		var ctor = producerType.GetConstructors()[0];
		var instance = ctor.Invoke([producer, options, logger]);

		var produceAsync = producerType.GetMethod("ProduceAsync")!;
		var produceToOriginalAsync = producerType.GetMethod("ProduceToOriginalTopicAsync")!;

		return (instance, produceAsync, produceToOriginalAsync);
	}

	/// <summary>
	/// Creates a KafkaDeadLetterConsumer (internal) via reflection and returns the instance
	/// plus MethodInfo handles for Consume and Peek.
	/// </summary>
	private (object Instance, MethodInfo Consume, MethodInfo Peek) CreateDlqConsumerWithMethods(string groupId)
	{
		var consumerType = typeof(KafkaOptions).Assembly.GetType(
			"Excalibur.Dispatch.Transport.Kafka.KafkaDeadLetterConsumer")!;

		var loggerOfT = typeof(Logger<>).MakeGenericType(consumerType);
		var logger = Activator.CreateInstance(loggerOfT, NullLoggerFactory.Instance)!;

		var kafkaOptions = Microsoft.Extensions.Options.Options.Create(new KafkaOptions
		{
			BootstrapServers = _bootstrapServers!,
		});

		var dlqOptions = Microsoft.Extensions.Options.Options.Create(new KafkaDeadLetterOptions
		{
			ConsumerGroupId = groupId,
			ConsumeTimeout = TimeSpan.FromSeconds(2),
		});

		var ctor = consumerType.GetConstructors()[0];
		var instance = ctor.Invoke([kafkaOptions, dlqOptions, logger]);

		var consumeMethod = consumerType.GetMethod("Consume")!;
		var peekMethod = consumerType.GetMethod("Peek")!;

		return (instance, consumeMethod, peekMethod);
	}

	private static global::Confluent.Kafka.ConsumeResult<string, byte[]>? ConsumeWithRetry(IConsumer<string, byte[]> consumer, TimeSpan timeout)
	{
		var deadline = DateTime.UtcNow + timeout;
		while (DateTime.UtcNow < deadline)
		{
			var result = consumer.Consume(TimeSpan.FromSeconds(2));
			if (result?.Message is not null)
			{
				return result;
			}
		}

		return null;
	}

	private static string? GetHeaderValue(Headers headers, string key)
	{
		if (headers.TryGetLastBytes(key, out var bytes))
		{
			return Encoding.UTF8.GetString(bytes);
		}

		return null;
	}

	#endregion
}
