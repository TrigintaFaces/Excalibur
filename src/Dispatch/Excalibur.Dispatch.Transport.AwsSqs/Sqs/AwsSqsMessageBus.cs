// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using Amazon.SQS;
using Amazon.SQS.Model;

using Excalibur.Dispatch;
using Excalibur.Dispatch.CloudEvents;
using Excalibur.Dispatch.Extensions;
using Excalibur.Dispatch.Features;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Serialization;
using Excalibur.Dispatch.Transport.AwsSqs;
using Excalibur.Dispatch.Transport.Diagnostics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using AwsMessageContextSerializer = Excalibur.Dispatch.Transport.Aws.MessageContextSerializer;

namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// AWS SQS implementation of the message bus for publishing dispatch actions, events, and documents.
/// </summary>
/// <param name="client"> The AWS SQS client for sending messages. </param>
/// <param name="serializer"> Payload serializer for message body serialization with pluggable format support. </param>
/// <param name="options"> The SQS specific configuration options. </param>
/// <param name="fifoOptions"> The FIFO queue configuration supplying the message group id and deduplication id selectors. </param>
/// <param name="logger"> The logger instance for diagnostic information. </param>
/// <param name="cloudEventBridge"> Optional envelope-to-CloudEvent bridge; when supplied with <paramref name="cloudEventEncoder"/>, every publish is emitted as a CloudEvent instead of the native envelope format. </param>
/// <param name="cloudEventEncoder"> Optional CloudEvents encoder for SQS <see cref="SendMessageRequest"/> messages. </param>
/// <remarks>
/// <para>
/// This message bus uses <see cref="IPayloadSerializer"/> for message body serialization,
/// which prepends a magic byte to identify the serializer format. This enables:
/// </para>
/// <list type="bullet">
///   <item>Automatic format detection during deserialization</item>
///   <item>Seamless migration between serializers</item>
///   <item>Multi-format support within the same queue</item>
/// </list>
/// <para>
/// See the pluggable serialization architecture documentation for details.
/// </para>
/// </remarks>
internal sealed partial class AwsSqsMessageBus(
	IAmazonSQS client,
	IPayloadSerializer serializer,
	IOptions<AwsSqsOptions> options,
	IOptions<AwsSqsFifoOptions> fifoOptions,
	ILogger<AwsSqsMessageBus> logger,
	IEnvelopeCloudEventBridge? cloudEventBridge = null,
	ICloudEventEncoder<SendMessageRequest>? cloudEventEncoder = null) : IMessageBus, IAsyncDisposable
{
	private const string QueueUrlNotConfiguredMessage = "QueueUrl is not configured";

	// SQS configuration, captured once from the options monitor.
	private readonly AwsSqsOptions _options =
		(options ?? throw new ArgumentNullException(nameof(options))).Value;

	// FIFO group/dedup configuration, captured once. When no selectors are configured
	// (standard queues), application is a no-op and the request is left untouched.
	private readonly AwsSqsFifoOptions _fifoOptions =
		(fifoOptions ?? throw new ArgumentNullException(nameof(fifoOptions))).Value;

	/// <summary>
	/// Initializes a new instance of the <see cref="AwsSqsMessageBus"/> class without FIFO configuration.
	/// </summary>
	/// <param name="client"> The AWS SQS client for sending messages. </param>
	/// <param name="serializer"> Payload serializer for message body serialization. </param>
	/// <param name="options"> The SQS specific configuration options. </param>
	/// <param name="logger"> The logger instance for diagnostic information. </param>
	/// <remarks>
	/// Convenience overload for standard (non-FIFO) queues. Equivalent to supplying empty
	/// <see cref="AwsSqsFifoOptions"/>, leaving <c>MessageGroupId</c>/<c>MessageDeduplicationId</c> unset.
	/// </remarks>
	public AwsSqsMessageBus(
		IAmazonSQS client,
		IPayloadSerializer serializer,
		AwsSqsOptions options,
		ILogger<AwsSqsMessageBus> logger)
		: this(
			client,
			serializer,
			Microsoft.Extensions.Options.Options.Create(options),
			Microsoft.Extensions.Options.Options.Create(new AwsSqsFifoOptions()),
			logger)
	{
	}

	/// <inheritdoc/>
	public async Task PublishAsync(IDispatchAction action, IMessageContext context, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(action);
		ArgumentNullException.ThrowIfNull(context);

		using var publishActivity = MessagingProducerInstrumentation.StartPublishActivity(
			TransportTelemetryConstants.MessagingConventions.Systems.AwsSqs, _options.QueueUrl?.ToString(), context.MessageId);

		if (cloudEventBridge is not null && cloudEventEncoder is not null)
		{
			await PublishWithCloudEventsAsync(action, context, LogSentAction, cancellationToken).ConfigureAwait(false);
			return;
		}

		// Use SerializeObject with runtime type to ensure proper concrete type serialization
		var bytes = serializer.SerializeObject(action, action.GetType());
		var body = Convert.ToBase64String(bytes);

		var request = new SendMessageRequest(
			_options.QueueUrl?.ToString() ?? throw new InvalidOperationException(QueueUrlNotConfiguredMessage), body);
		request.MessageAttributes ??= new Dictionary<string, MessageAttributeValue>(StringComparer.Ordinal);

		// Serialize ALL context fields to preserve complete message context
		AwsMessageContextSerializer.SerializeToMessageAttributes(context, request.MessageAttributes);

		// Apply FIFO ordering/deduplication identifiers when a FIFO queue is configured.
		ApplyFifo(request, action);

		_ = await client.SendMessageAsync(request, cancellationToken).ConfigureAwait(false);

		LogSentAction(action.GetType().Name);
	}

	/// <inheritdoc/>
	public async Task PublishAsync(IDispatchEvent evt, IMessageContext context, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(evt);
		ArgumentNullException.ThrowIfNull(context);

		using var publishActivity = MessagingProducerInstrumentation.StartPublishActivity(
			TransportTelemetryConstants.MessagingConventions.Systems.AwsSqs, _options.QueueUrl?.ToString(), context.MessageId);

		if (cloudEventBridge is not null && cloudEventEncoder is not null)
		{
			await PublishWithCloudEventsAsync(evt, context, LogPublishedEvent, cancellationToken).ConfigureAwait(false);
			return;
		}

		// Use SerializeObject with runtime type to ensure proper concrete type serialization
		var bytes = serializer.SerializeObject(evt, evt.GetType());
		var body = Convert.ToBase64String(bytes);

		var requestEvt =
			new SendMessageRequest(_options.QueueUrl?.ToString() ?? throw new InvalidOperationException(QueueUrlNotConfiguredMessage), body);
		requestEvt.MessageAttributes ??= new Dictionary<string, MessageAttributeValue>(StringComparer.Ordinal);

		// Serialize ALL context fields to preserve complete message context
		AwsMessageContextSerializer.SerializeToMessageAttributes(context, requestEvt.MessageAttributes);

		// Apply FIFO ordering/deduplication identifiers when a FIFO queue is configured.
		ApplyFifo(requestEvt, evt);

		_ = await client.SendMessageAsync(requestEvt, cancellationToken).ConfigureAwait(false);

		LogPublishedEvent(evt.GetType().Name);
	}

	/// <inheritdoc/>
	public async Task PublishAsync(IDispatchDocument doc, IMessageContext context, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(doc);
		ArgumentNullException.ThrowIfNull(context);

		using var publishActivity = MessagingProducerInstrumentation.StartPublishActivity(
			TransportTelemetryConstants.MessagingConventions.Systems.AwsSqs, _options.QueueUrl?.ToString(), context.MessageId);

		if (cloudEventBridge is not null && cloudEventEncoder is not null)
		{
			await PublishWithCloudEventsAsync(doc, context, LogSentDocument, cancellationToken).ConfigureAwait(false);
			return;
		}

		// Use SerializeObject with runtime type to ensure proper concrete type serialization
		var bytes = serializer.SerializeObject(doc, doc.GetType());
		var body = Convert.ToBase64String(bytes);

		var requestDoc =
			new SendMessageRequest(_options.QueueUrl?.ToString() ?? throw new InvalidOperationException(QueueUrlNotConfiguredMessage), body);
		requestDoc.MessageAttributes ??= new Dictionary<string, MessageAttributeValue>(StringComparer.Ordinal);

		// Serialize ALL context fields to preserve complete message context
		AwsMessageContextSerializer.SerializeToMessageAttributes(context, requestDoc.MessageAttributes);

		// Apply FIFO ordering/deduplication identifiers when a FIFO queue is configured.
		ApplyFifo(requestDoc, doc);

		_ = await client.SendMessageAsync(requestDoc, cancellationToken).ConfigureAwait(false);

		LogSentDocument(doc.GetType().Name);
	}

	private static MessageEnvelope CreateEnvelope(IDispatchMessage message, IMessageContext context)
	{
		// The declared name, not the CLR FullName -- this becomes the CloudEvent type attribute on a
		// message leaving the process, so a consumer refactoring their own code must not change the
		// string the far side matches on. Mirrors RabbitMqMessageBus.CreateEnvelope for the same reason.
		var messageClrType = message.GetType();

		var envelope = new MessageEnvelope(message)
		{
			MessageId = context.MessageId ?? Uuid7Extensions.GenerateString(),
			ExternalId = context.GetExternalId(),
			UserId = context.GetUserId(),
			CorrelationId = context.CorrelationId,
			CausationId = context.CausationId,
			TraceParent = context.GetTraceParent(),
			TenantId = context.GetTenantId(),
			MessageType = context.GetMessageType()
				?? MessageNameHelper.GetDeclaredName(messageClrType)
				?? messageClrType.FullName,
			ContentType = context.GetContentType() ?? "application/json",
			DeliveryCount = context.GetDeliveryCount(),
			ReceivedTimestampUtc = context.GetReceivedTimestampUtc() ?? DateTimeOffset.UtcNow,
			SentTimestampUtc = context.GetSentTimestampUtc(),
		};

		foreach (var item in context.Items)
		{
			envelope.SetItem(item.Key, item.Value);
		}

		return envelope;
	}

	private async Task PublishWithCloudEventsAsync(
		IDispatchMessage message,
		IMessageContext context,
		Action<string> logAction,
		CancellationToken cancellationToken)
	{
		var envelope = CreateEnvelope(message, context);
		try
		{
			var request = await cloudEventBridge!
				.ToTransportAsync<SendMessageRequest>(envelope, cloudEventEncoder!.Options.DefaultMode, cancellationToken)
				.ConfigureAwait(false);

			request.QueueUrl ??= _options.QueueUrl?.ToString()
				?? throw new InvalidOperationException(QueueUrlNotConfiguredMessage);

			// FIFO ordering/deduplication still applies -- the CloudEvents wire shape does not
			// change which queue semantics govern delivery.
			ApplyFifo(request, message);

			_ = await client.SendMessageAsync(request, cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			envelope.Dispose();
		}

		if (logger.IsEnabled(LogLevel.Information))
		{
			logAction(message.GetType().Name);
		}
	}

	/// <summary>
	/// Applies FIFO ordering and deduplication identifiers to the outgoing request when a FIFO
	/// queue is configured via <c>ConfigureFifo</c>.
	/// </summary>
	/// <param name="request"> The SQS send request being populated. </param>
	/// <param name="message"> The message instance the selectors are evaluated against. </param>
	/// <remarks>
	/// <para>
	/// <c>MessageGroupId</c> is required for FIFO queues and is set only when a
	/// <see cref="AwsSqsFifoOptions.MessageGroupIdSelector"/> is configured. For standard queues
	/// (no selector), this method is a no-op and leaves the request unchanged.
	/// </para>
	/// <para>
	/// When the queue uses content-based deduplication, SQS derives the deduplication id from a
	/// SHA-256 hash of the message body, so <c>MessageDeduplicationId</c> is intentionally left unset.
	/// Otherwise it is populated from the configured
	/// <see cref="AwsSqsFifoOptions.DeduplicationIdSelector"/>.
	/// </para>
	/// </remarks>
	private void ApplyFifo(SendMessageRequest request, object message)
	{
		if (_fifoOptions.MessageGroupIdSelector is { } groupSelector)
		{
			request.MessageGroupId = groupSelector(message);
		}

		if (!_fifoOptions.ContentBasedDeduplication && _fifoOptions.DeduplicationIdSelector is { } dedupSelector)
		{
			request.MessageDeduplicationId = dedupSelector(message);
		}
	}

	/// <inheritdoc/>
	public ValueTask DisposeAsync()
	{
		client.Dispose();
		return ValueTask.CompletedTask;
	}

	// Source-generated logging methods
	[LoggerMessage(AwsSqsEventId.SqsSentAction, LogLevel.Information,
		"Sent action to SQS: {Action}")]
	private partial void LogSentAction(string action);

	[LoggerMessage(AwsSqsEventId.SqsPublishedEvent, LogLevel.Information,
		"Published event to SQS: {Event}")]
	private partial void LogPublishedEvent(string @event);

	[LoggerMessage(AwsSqsEventId.SqsSentDocument, LogLevel.Information,
		"Sent document to SQS: {Doc}")]
	private partial void LogSentDocument(string doc);
}
