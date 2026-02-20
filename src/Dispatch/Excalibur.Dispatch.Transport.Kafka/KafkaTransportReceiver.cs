// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Text;

using Confluent.Kafka;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// Kafka implementation of <see cref="ITransportReceiver"/>.
/// Uses Confluent.Kafka's <see cref="IConsumer{TKey,TValue}"/> for native message consumption.
/// </summary>
/// <remarks>
/// Acknowledgment commits the next offset (current + 1) via the consumer.
/// The receipt handle is stored in <see cref="TransportReceivedMessage.ProviderData"/>
/// as <c>"kafka.topic"</c>, <c>"kafka.partition"</c>, and <c>"kafka.offset"</c>.
/// </remarks>
internal sealed partial class KafkaTransportReceiver : ITransportReceiver
{
	private readonly IConsumer<string, byte[]> _consumer;
	private readonly ILogger _logger;
	private readonly ConcurrentDictionary<string, TopicPartitionOffset> _offsetCache = new(StringComparer.Ordinal);
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="KafkaTransportReceiver"/> class.
	/// </summary>
	/// <param name="consumer">The Kafka consumer instance.</param>
	/// <param name="source">The source topic name.</param>
	/// <param name="logger">The logger instance.</param>
	public KafkaTransportReceiver(
		IConsumer<string, byte[]> consumer,
		string source,
		ILogger<KafkaTransportReceiver> logger)
	{
		_consumer = consumer ?? throw new ArgumentNullException(nameof(consumer));
		Source = source ?? throw new ArgumentNullException(nameof(source));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc />
	public string Source { get; }

	/// <inheritdoc />
	public Task<IReadOnlyList<TransportReceivedMessage>> ReceiveAsync(int maxMessages, CancellationToken cancellationToken)
	{
		try
		{
			var messages = new List<TransportReceivedMessage>();

			for (var i = 0; i < maxMessages && !cancellationToken.IsCancellationRequested; i++)
			{
				var consumeResult = _consumer.Consume(TimeSpan.FromSeconds(1));
				if (consumeResult?.Message == null)
				{
					break;
				}

				var received = ConvertToReceivedMessage(consumeResult);
				messages.Add(received);
				LogMessageReceived(received.Id, Source);
			}

			return Task.FromResult<IReadOnlyList<TransportReceivedMessage>>(messages);
		}
		catch (Exception ex)
		{
			LogReceiveError(Source, ex);
			throw;
		}
	}

	/// <inheritdoc />
	public Task AcknowledgeAsync(TransportReceivedMessage message, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);

		var receiptHandle = GetReceiptHandle(message);
		try
		{
			if (_offsetCache.TryRemove(receiptHandle, out var tpo))
			{
				_consumer.Commit([new TopicPartitionOffset(tpo.Topic, tpo.Partition, tpo.Offset + 1)]);
				LogMessageAcknowledged(message.Id, Source);
			}
			else
			{
				throw new InvalidOperationException(
					$"Message with receipt handle '{receiptHandle}' not found in offset cache. It may have already been processed.");
			}
		}
		catch (InvalidOperationException)
		{
			throw;
		}
		catch (Exception ex)
		{
			LogAcknowledgeError(message.Id, Source, ex);
			throw;
		}

		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task RejectAsync(TransportReceivedMessage message, string? reason, bool requeue, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);

		var receiptHandle = GetReceiptHandle(message);

		if (requeue)
		{
			// Don't commit — Kafka will redeliver after session timeout
			_ = _offsetCache.TryRemove(receiptHandle, out _);
			LogMessageRejectedRequeue(message.Id, Source, reason ?? "no reason");
		}
		else
		{
			// Commit to skip this message (DLQ routing handled by decorator)
			if (_offsetCache.TryRemove(receiptHandle, out var tpo))
			{
				_consumer.Commit([new TopicPartitionOffset(tpo.Topic, tpo.Partition, tpo.Offset + 1)]);
			}

			LogMessageRejected(message.Id, Source, reason ?? "no reason");
		}

		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public object? GetService(Type serviceType)
	{
		ArgumentNullException.ThrowIfNull(serviceType);
		if (serviceType == typeof(IConsumer<string, byte[]>))
		{
			return _consumer;
		}

		return null;
	}

	/// <inheritdoc />
	public ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return ValueTask.CompletedTask;
		}

		_disposed = true;
		LogDisposed(Source);
		GC.SuppressFinalize(this);
		return ValueTask.CompletedTask;
	}

	private TransportReceivedMessage ConvertToReceivedMessage(global::Confluent.Kafka.ConsumeResult<string, byte[]> consumeResult)
	{
		var receiptHandle = $"{consumeResult.Topic}:{consumeResult.Partition.Value}:{consumeResult.Offset.Value}";
		_offsetCache[receiptHandle] = consumeResult.TopicPartitionOffset;

		var properties = new Dictionary<string, object>(StringComparer.Ordinal);
		if (consumeResult.Message.Headers is not null)
		{
			foreach (var header in consumeResult.Message.Headers)
			{
				properties[header.Key] = Encoding.UTF8.GetString(header.GetValueBytes());
			}
		}

		var contentType = properties.TryGetValue("content-type", out var ct) ? ct as string : null;
		var messageType = properties.TryGetValue("message-type", out var mt) ? mt as string : null;
		var correlationId = properties.TryGetValue("correlation-id", out var ci) ? ci as string : null;
		var messageId = properties.TryGetValue("message-id", out var mi) ? mi as string : null;

		return new TransportReceivedMessage
		{
			Id = messageId ?? consumeResult.Message.Key ?? receiptHandle,
			Body = consumeResult.Message.Value ?? [],
			ContentType = contentType,
			MessageType = messageType,
			CorrelationId = correlationId,
			Source = Source,
			PartitionKey = consumeResult.Message.Key,
			EnqueuedAt = consumeResult.Message.Timestamp.UtcDateTime != DateTime.MinValue
				? new DateTimeOffset(consumeResult.Message.Timestamp.UtcDateTime, TimeSpan.Zero)
				: DateTimeOffset.UtcNow,
			Properties = properties,
			ProviderData = new Dictionary<string, object>
			{
				["kafka.topic"] = consumeResult.Topic,
				["kafka.partition"] = consumeResult.Partition.Value,
				["kafka.offset"] = consumeResult.Offset.Value,
				["kafka.receipt_handle"] = receiptHandle,
			},
		};
	}

	private static string GetReceiptHandle(TransportReceivedMessage message)
	{
		if (message.ProviderData.TryGetValue("kafka.receipt_handle", out var handle) && handle is string handleStr)
		{
			return handleStr;
		}

		throw new InvalidOperationException("Message does not contain a Kafka receipt handle in ProviderData.");
	}

	[LoggerMessage(KafkaEventId.TransportReceiverMessageReceived, LogLevel.Debug,
		"Kafka transport receiver: message {MessageId} received from {Source}")]
	private partial void LogMessageReceived(string messageId, string source);

	[LoggerMessage(KafkaEventId.TransportReceiverReceiveError, LogLevel.Error,
		"Kafka transport receiver: failed to receive messages from {Source}")]
	private partial void LogReceiveError(string source, Exception exception);

	[LoggerMessage(KafkaEventId.TransportReceiverMessageAcknowledged, LogLevel.Debug,
		"Kafka transport receiver: message {MessageId} acknowledged from {Source}")]
	private partial void LogMessageAcknowledged(string messageId, string source);

	[LoggerMessage(KafkaEventId.TransportReceiverAcknowledgeError, LogLevel.Error,
		"Kafka transport receiver: failed to acknowledge message {MessageId} from {Source}")]
	private partial void LogAcknowledgeError(string messageId, string source, Exception exception);

	[LoggerMessage(KafkaEventId.TransportReceiverMessageRejected, LogLevel.Warning,
		"Kafka transport receiver: message {MessageId} rejected from {Source}: {Reason}")]
	private partial void LogMessageRejected(string messageId, string source, string reason);

	[LoggerMessage(KafkaEventId.TransportReceiverMessageRejectedRequeue, LogLevel.Debug,
		"Kafka transport receiver: message {MessageId} rejected (requeue) from {Source}: {Reason}")]
	private partial void LogMessageRejectedRequeue(string messageId, string source, string reason);

	[LoggerMessage(KafkaEventId.TransportReceiverDisposed, LogLevel.Debug,
		"Kafka transport receiver disposed for {Source}")]
	private partial void LogDisposed(string source);
}
