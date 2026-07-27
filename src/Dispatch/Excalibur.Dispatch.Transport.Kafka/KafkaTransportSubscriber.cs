// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Text;

using Confluent.Kafka;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// Kafka implementation of <see cref="ITransportSubscriber"/>.
/// Uses Confluent.Kafka's <see cref="IConsumer{TKey,TValue}"/> for push-based message delivery
/// via a consume loop.
/// </summary>
/// <remarks>
/// <para>
/// The consumer poll loop calls <see cref="IConsumer{TKey,TValue}.Consume(CancellationToken)"/> and invokes the
/// handler callback for each received message. Message settlement is determined by the returned <see cref="MessageAction"/>:
/// </para>
/// <list type="bullet">
/// <item><see cref="MessageAction.Acknowledge"/> commits the offset (auto-commit or explicit).</item>
/// <item><see cref="MessageAction.Reject"/> logs the rejection (offset advances).</item>
/// <item><see cref="MessageAction.Requeue"/> seeks back to the current offset for redelivery.</item>
/// </list>
/// <para>
/// Provider-specific data is stored in <see cref="TransportReceivedMessage.ProviderData"/>:
/// <c>"kafka.partition"</c>, <c>"kafka.offset"</c>, and <c>"kafka.topic"</c>.
/// </para>
/// </remarks>
[RequiresUnreferencedCode("Schema Registry uses Activator.CreateInstance for custom subject name strategy types.")]
[RequiresDynamicCode("Schema Registry uses Activator.CreateInstance for custom subject name strategy types.")]
internal sealed partial class KafkaTransportSubscriber : ITransportSubscriber
{
	private readonly IConsumer<string, byte[]> _consumer;
	private readonly ILogger _logger;
	private readonly int? _maxPayloadBytes;
	private readonly bool _decodeConfluentFraming;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="KafkaTransportSubscriber"/> class.
	/// </summary>
	/// <param name="consumer">The Kafka consumer instance.</param>
	/// <param name="source">The source topic name.</param>
	/// <param name="logger">The logger instance.</param>
	/// <param name="maxPayloadBytes">
	/// The maximum inbound-payload length, in bytes, enforced before the body is materialized, or
	/// <see langword="null"/> to opt out (unbounded). Defaults to 4 MiB.
	/// </param>
	/// <param name="decodeConfluentFraming">
	/// When <see langword="true"/> (a Confluent Schema Registry-configured transport), a Confluent-framed
	/// inbound payload (magic byte + schema id) has its 5-byte header stripped so the downstream deserializer
	/// receives the raw payload. The .NET message type is carried in the <c>message-type</c> header, so the
	/// schema id itself is not needed to deserialize. Non-framed payloads are passed through untouched.
	/// </param>
	public KafkaTransportSubscriber(
		IConsumer<string, byte[]> consumer,
		string source,
		ILogger<KafkaTransportSubscriber> logger,
		int? maxPayloadBytes = PayloadSizeGuard.DefaultMaxPayloadBytes,
		bool decodeConfluentFraming = false)
	{
		_consumer = consumer ?? throw new ArgumentNullException(nameof(consumer));
		Source = source ?? throw new ArgumentNullException(nameof(source));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_maxPayloadBytes = maxPayloadBytes;
		_decodeConfluentFraming = decodeConfluentFraming;
	}

	/// <summary>
	/// Materializes the transport body, stripping Confluent Schema Registry framing when this transport is
	/// schema-registry-configured and the payload carries the Confluent wire-format header.
	/// </summary>
	private byte[] MaterializeBody(byte[]? value)
	{
		if (value is null || value.Length == 0)
		{
			return [];
		}

		if (_decodeConfluentFraming && ConfluentWireFormat.TryReadSchemaId(value, out _))
		{
			return ConfluentWireFormat.GetPayload(value).ToArray();
		}

		return value;
	}

	/// <inheritdoc />
	public string Source { get; }

	/// <inheritdoc />
	public async Task SubscribeAsync(
		Func<TransportReceivedMessage, CancellationToken, Task<MessageAction>> handler,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(handler);

		// Use a separate CTS for in-flight message processing so that handlers can
		// complete commit even after the subscription token is cancelled.
		using var messageProcessingCts = new CancellationTokenSource();

		_consumer.Subscribe(Source);
		LogSubscriptionStarted(Source);

		try
		{
			while (!cancellationToken.IsCancellationRequested)
			{
				global::Confluent.Kafka.ConsumeResult<string, byte[]> consumeResult;
				try
				{
					consumeResult = _consumer.Consume(cancellationToken);
				}
				catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
				{
					break;
				}

				if (consumeResult?.Message == null)
				{
					continue;
				}

				TransportReceivedMessage received;
				try
				{
					received = ConvertToReceivedMessage(consumeResult);
				}
				catch (PayloadTooLargeException ex)
				{
					// Oversized poison message: commit past it (offset + 1) to skip — mirrors the Reject
					// branch — before the body is materialized, so it can never enter a redelivery loop.
					// DLQ routing (if configured) is handled by the decorator.
					LogPayloadTooLargeRejected(Source, consumeResult.Message.Value?.Length ?? 0, ex);
					_consumer.Commit([new TopicPartitionOffset(
						consumeResult.Topic,
						consumeResult.Partition,
						consumeResult.Offset + 1)]);
					continue;
				}

				LogMessageReceived(received.Id, Source);

				try
				{
					var action = await handler(received, messageProcessingCts.Token).ConfigureAwait(false);

					switch (action)
					{
						case MessageAction.Acknowledge:
							_consumer.Commit([new TopicPartitionOffset(
								consumeResult.Topic,
								consumeResult.Partition,
								consumeResult.Offset + 1)]);
							LogMessageAcknowledged(received.Id, Source);
							break;

						case MessageAction.Reject:
							// Commit to skip this message — DLQ routing handled by decorator
							_consumer.Commit([new TopicPartitionOffset(
								consumeResult.Topic,
								consumeResult.Partition,
								consumeResult.Offset + 1)]);
							LogMessageRejected(received.Id, Source);
							break;

						case MessageAction.Requeue:
							// Seek back to redeliver the same message
							_consumer.Seek(consumeResult.TopicPartitionOffset);
							LogMessageRequeued(received.Id, Source);
							break;
					}
				}
				catch (OperationCanceledException ex) when (ex.CancellationToken.IsCancellationRequested)
				{
					break;
				}
				catch (Exception ex)
				{
					LogError(received.Id, Source, ex);
				}
			}
		}
		finally
		{
			// Cancel in-flight message processing
			await messageProcessingCts.CancelAsync().ConfigureAwait(false);

			_consumer.Close();
			LogSubscriptionStopped(Source);
		}
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
		_consumer.Dispose();
		LogDisposed(Source);
		GC.SuppressFinalize(this);
		return ValueTask.CompletedTask;
	}

	private TransportReceivedMessage ConvertToReceivedMessage(global::Confluent.Kafka.ConsumeResult<string, byte[]> consumeResult)
	{
		// Defense-in-depth DoS guard: reject an oversized payload BEFORE the body is materialized below.
		// Fail-closed — throws PayloadTooLargeException, which the consume loop catches to commit past
		// (skip) the poison message; it never truncates or silently drops.
		PayloadSizeGuard.EnsureWithinLimit(consumeResult.Message.Value?.Length ?? 0, _maxPayloadBytes);

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
		var correlationId = properties.TryGetValue(OutboxHeaderNames.CorrelationId, out var ci) ? ci as string : null;
		var messageId = properties.TryGetValue("message-id", out var mi) ? mi as string : null;

		return new TransportReceivedMessage
		{
			Id = messageId ?? consumeResult.Message.Key ?? $"{consumeResult.Topic}:{consumeResult.Partition.Value}:{consumeResult.Offset.Value}",
			Body = MaterializeBody(consumeResult.Message.Value),
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
				["kafka.partition"] = consumeResult.Partition.Value,
				["kafka.offset"] = consumeResult.Offset.Value,
				["kafka.topic"] = consumeResult.Topic,
			},
		};
	}

	[LoggerMessage(KafkaEventId.TransportSubscriberStarted, LogLevel.Information,
		"Kafka transport subscriber: subscription started for {Source}")]
	private partial void LogSubscriptionStarted(string source);

	[LoggerMessage(KafkaEventId.TransportSubscriberMessageReceived, LogLevel.Debug,
		"Kafka transport subscriber: message {MessageId} received from {Source}")]
	private partial void LogMessageReceived(string messageId, string source);

	[LoggerMessage(KafkaEventId.TransportSubscriberMessageAcknowledged, LogLevel.Debug,
		"Kafka transport subscriber: message {MessageId} acknowledged from {Source}")]
	private partial void LogMessageAcknowledged(string messageId, string source);

	[LoggerMessage(KafkaEventId.TransportSubscriberMessageRejected, LogLevel.Warning,
		"Kafka transport subscriber: message {MessageId} rejected from {Source}")]
	private partial void LogMessageRejected(string messageId, string source);

	[LoggerMessage(KafkaEventId.TransportSubscriberMessageRequeued, LogLevel.Debug,
		"Kafka transport subscriber: message {MessageId} requeued (seek back) from {Source}")]
	private partial void LogMessageRequeued(string messageId, string source);

	[LoggerMessage(KafkaEventId.TransportSubscriberError, LogLevel.Error,
		"Kafka transport subscriber: error processing message {MessageId} from {Source}")]
	private partial void LogError(string messageId, string source, Exception exception);

	[LoggerMessage(KafkaEventId.TransportSubscriberStopped, LogLevel.Information,
		"Kafka transport subscriber: subscription stopped for {Source}")]
	private partial void LogSubscriptionStopped(string source);

	[LoggerMessage(KafkaEventId.TransportSubscriberDisposed, LogLevel.Debug,
		"Kafka transport subscriber disposed for {Source}")]
	private partial void LogDisposed(string source);

	[LoggerMessage(KafkaEventId.TransportSubscriberPayloadTooLarge, LogLevel.Warning,
		"Kafka transport subscriber: rejected an oversized inbound payload ({PayloadBytes} bytes) from {Source} before materialization; committed past the poison message.")]
	private partial void LogPayloadTooLargeRejected(string source, int payloadBytes, Exception exception);
}
