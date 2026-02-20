// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics;
using System.Text;

using Confluent.Kafka;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Transport;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// Kafka message bus that produces messages in Confluent wire format with Schema Registry integration.
/// </summary>
/// <remarks>
/// <para>
/// This message bus serializes messages using <see cref="IConfluentFormatSerializer"/>,
/// which prepends the 5-byte Confluent wire format header (magic byte + schema ID).
/// </para>
/// <para>
/// Headers preserved:
/// </para>
/// <list type="bullet">
///   <item><description><c>correlation-id</c>: From IMessageContext.CorrelationId</description></item>
///   <item><description><c>traceparent</c>: From Activity.Current.Id (W3C Trace Context)</description></item>
/// </list>
/// </remarks>
public sealed partial class ConfluentKafkaMessageBus : IMessageBus, IAsyncDisposable
{
	private readonly IProducer<string, byte[]> _producer;
	private readonly IConfluentFormatSerializer _serializer;
	private readonly string _topic;
	private readonly ILogger<ConfluentKafkaMessageBus> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="ConfluentKafkaMessageBus"/> class.
	/// </summary>
	/// <param name="producer">The Kafka producer.</param>
	/// <param name="serializer">The Confluent format serializer.</param>
	/// <param name="options">The Kafka options.</param>
	/// <param name="logger">The logger instance.</param>
	public ConfluentKafkaMessageBus(
		IProducer<string, byte[]> producer,
		IConfluentFormatSerializer serializer,
		KafkaOptions options,
		ILogger<ConfluentKafkaMessageBus> logger)
	{
		_producer = producer ?? throw new ArgumentNullException(nameof(producer));
		_serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
		ArgumentNullException.ThrowIfNull(options);
		_topic = options.Topic;
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc/>
	public async Task PublishAsync(IDispatchAction action, IMessageContext context, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(action);
		ArgumentNullException.ThrowIfNull(context);

		var actionType = action.GetType();
		LogPublishingMessage("Action", actionType.Name, _topic);

		var payload = await _serializer.SerializeAsync(_topic, action, actionType, cancellationToken).ConfigureAwait(false);
		var message = CreateMessage(context, payload);

		_ = await _producer.ProduceAsync(_topic, message, cancellationToken).ConfigureAwait(false);

		LogPublishedMessage("Action", actionType.Name, _topic, payload.Length);
	}

	/// <inheritdoc/>
	public async Task PublishAsync(IDispatchEvent evt, IMessageContext context, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(evt);
		ArgumentNullException.ThrowIfNull(context);

		var eventType = evt.GetType();
		LogPublishingMessage("Event", eventType.Name, _topic);

		var payload = await _serializer.SerializeAsync(_topic, evt, eventType, cancellationToken).ConfigureAwait(false);
		var message = CreateMessage(context, payload);

		_ = await _producer.ProduceAsync(_topic, message, cancellationToken).ConfigureAwait(false);

		LogPublishedMessage("Event", eventType.Name, _topic, payload.Length);
	}

	/// <inheritdoc/>
	public async Task PublishAsync(IDispatchDocument doc, IMessageContext context, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(doc);
		ArgumentNullException.ThrowIfNull(context);

		var docType = doc.GetType();
		LogPublishingMessage("Document", docType.Name, _topic);

		var payload = await _serializer.SerializeAsync(_topic, doc, docType, cancellationToken).ConfigureAwait(false);
		var message = CreateMessage(context, payload);

		_ = await _producer.ProduceAsync(_topic, message, cancellationToken).ConfigureAwait(false);

		LogPublishedMessage("Document", docType.Name, _topic, payload.Length);
	}

	/// <inheritdoc/>
	public ValueTask DisposeAsync()
	{
		_ = _producer.Flush(TimeSpan.FromSeconds(5));
		_producer.Dispose();
		return ValueTask.CompletedTask;
	}

	private static Message<string, byte[]> CreateMessage(IMessageContext context, byte[] payload)
	{
		var headers = BuildHeaders(context);

		return new Message<string, byte[]>
		{
			Key = context.CorrelationId ?? string.Empty,
			Value = payload,
			Headers = headers
		};
	}

	private static Headers BuildHeaders(IMessageContext context)
	{
		var headers = new Headers();

		// Correlation ID header
		if (!string.IsNullOrEmpty(context.CorrelationId))
		{
			headers.Add("correlation-id", Encoding.UTF8.GetBytes(context.CorrelationId));
		}

		// W3C Trace Context header
		if (Activity.Current?.Id is { } traceParent)
		{
			headers.Add("traceparent", Encoding.UTF8.GetBytes(traceParent));
		}

		return headers;
	}

	// Source-generated logging methods
	[LoggerMessage(KafkaEventId.PublishingMessage, LogLevel.Debug,
		"Publishing {MessageKind} {MessageType} to topic {Topic}")]
	private partial void LogPublishingMessage(string messageKind, string messageType, string topic);

	[LoggerMessage(KafkaEventId.MessagePublishedWithSize, LogLevel.Information,
		"Published {MessageKind} {MessageType} to topic {Topic} ({PayloadBytes} bytes)")]
	private partial void LogPublishedMessage(string messageKind, string messageType, string topic, int payloadBytes);
}
