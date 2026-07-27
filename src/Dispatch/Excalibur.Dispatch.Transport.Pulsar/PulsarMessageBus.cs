// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using DotPulsar;
using DotPulsar.Abstractions;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Features;
using Excalibur.Dispatch.Serialization;
using Excalibur.Dispatch.Transport.Diagnostics;
using Excalibur.Dispatch.Transport.Pulsar.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Transport.Pulsar;

/// <summary>
/// Provides an Apache Pulsar-based <see cref="IMessageBus"/> implementation for publishing dispatch
/// messages (actions, events, documents) to a Pulsar topic via a DotPulsar producer.
/// </summary>
/// <remarks>
/// <para>
/// This message bus uses <see cref="IPayloadSerializer"/> for message body serialization, which prepends
/// a magic byte identifying the serializer format. This enables automatic format detection on
/// deserialization, seamless migration between serializers, and multi-format support within the same topic.
/// </para>
/// <para>
/// The runtime concrete type is passed to <see cref="IPayloadSerializer.SerializeObject(object, System.Type)"/>
/// so derived message types serialize their full shape rather than the static interface view.
/// </para>
/// </remarks>
internal sealed partial class PulsarMessageBus : IMessageBus, IAsyncDisposable
{
	private readonly IProducer<byte[]> _producer;
	private readonly IPayloadSerializer _serializer;
	private readonly string _topic;
	private readonly ILogger<PulsarMessageBus> _logger;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="PulsarMessageBus"/> class.
	/// </summary>
	/// <param name="producer">The DotPulsar producer bound to the destination topic.</param>
	/// <param name="serializer">The payload serializer for message body serialization.</param>
	/// <param name="topic">The destination topic name (used for diagnostics/telemetry).</param>
	/// <param name="logger">The logger for diagnostics.</param>
	public PulsarMessageBus(
		IProducer<byte[]> producer,
		IPayloadSerializer serializer,
		string topic,
		ILogger<PulsarMessageBus> logger)
	{
		_producer = producer ?? throw new ArgumentNullException(nameof(producer));
		_serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
		_topic = topic ?? throw new ArgumentNullException(nameof(topic));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc />
	public async Task PublishAsync(
		IDispatchAction action,
		IMessageContext context,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(action);
		ArgumentNullException.ThrowIfNull(context);

		// Use SerializeObject with runtime type to ensure proper concrete type serialization.
		var payload = _serializer.SerializeObject(action, action.GetType());
		await PublishInternalAsync(payload, context, cancellationToken).ConfigureAwait(false);

		if (_logger.IsEnabled(LogLevel.Information))
		{
			LogSentAction(action.GetType().Name);
		}
	}

	/// <inheritdoc />
	public async Task PublishAsync(
		IDispatchEvent evt,
		IMessageContext context,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(evt);
		ArgumentNullException.ThrowIfNull(context);

		var payload = _serializer.SerializeObject(evt, evt.GetType());
		await PublishInternalAsync(payload, context, cancellationToken).ConfigureAwait(false);

		if (_logger.IsEnabled(LogLevel.Information))
		{
			LogPublishedEvent(evt.GetType().Name);
		}
	}

	/// <inheritdoc />
	public async Task PublishAsync(
		IDispatchDocument doc,
		IMessageContext context,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(doc);
		ArgumentNullException.ThrowIfNull(context);

		var payload = _serializer.SerializeObject(doc, doc.GetType());
		await PublishInternalAsync(payload, context, cancellationToken).ConfigureAwait(false);

		if (_logger.IsEnabled(LogLevel.Information))
		{
			LogSentDocument(doc.GetType().Name);
		}
	}

	/// <inheritdoc />
	public async ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return;
		}
		_disposed = true;
		await _producer.DisposeAsync().ConfigureAwait(false);
		GC.SuppressFinalize(this);
	}

	private async Task PublishInternalAsync(
		byte[] payload,
		IMessageContext context,
		CancellationToken cancellationToken)
	{
		using var publishActivity = MessagingProducerInstrumentation.StartPublishActivity(
			TransportTelemetryConstants.MessagingConventions.Systems.Pulsar, _topic, context.MessageId);

		var metadata = new MessageMetadata();

		if (!string.IsNullOrEmpty(context.MessageId))
		{
			metadata["message-id"] = context.MessageId;
		}

		if (!string.IsNullOrEmpty(context.CorrelationId))
		{
			metadata.Key = context.CorrelationId;
		}

		var traceParent = context.GetTraceParent();
		if (!string.IsNullOrEmpty(traceParent))
		{
			metadata["traceparent"] = traceParent;
		}

		_ = await _producer.Send(metadata, payload, cancellationToken).ConfigureAwait(false);
	}

	// Source-generated logging methods
	[LoggerMessage(PulsarEventId.MessageBusActionSent, LogLevel.Information,
		"Sent action to Pulsar: {Action}")]
	private partial void LogSentAction(string action);

	[LoggerMessage(PulsarEventId.MessageBusEventPublished, LogLevel.Information,
		"Published event to Pulsar: {Event}")]
	private partial void LogPublishedEvent(string @event);

	[LoggerMessage(PulsarEventId.MessageBusDocumentSent, LogLevel.Information,
		"Sent document to Pulsar: {Doc}")]
	private partial void LogSentDocument(string doc);
}
