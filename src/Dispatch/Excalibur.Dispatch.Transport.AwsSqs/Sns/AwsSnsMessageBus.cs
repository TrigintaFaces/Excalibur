// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;

using Excalibur.Dispatch;
using Excalibur.Dispatch.CloudEvents;
using Excalibur.Dispatch.Features;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Serialization;
using Excalibur.Dispatch.Transport.AwsSqs;
using Excalibur.Dispatch.Transport.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// AWS SNS implementation of the message bus for publishing dispatch actions, events, and documents.
/// </summary>
/// <param name="client"> The AWS SNS client for publishing messages. </param>
/// <param name="serializer"> Payload serializer for message body serialization with pluggable format support. </param>
/// <param name="options"> The SNS specific configuration options. </param>
/// <param name="logger"> The logger instance for diagnostic information. </param>
/// <param name="cloudEventBridge"> Optional envelope-to-CloudEvent bridge; when supplied with <paramref name="cloudEventEncoder"/>, every publish is emitted as a CloudEvent instead of the native envelope format. </param>
/// <param name="cloudEventEncoder"> Optional CloudEvents encoder for SNS <see cref="PublishRequest"/> messages. </param>
/// <param name="ownsClient">Whether this bus owns and disposes its client.</param>
/// <param name="failureClassifier">Optional consumer classifier that can veto foreground transient retries.</param>
/// <remarks>
/// <para>
/// This message bus uses <see cref="IPayloadSerializer"/> for message body serialization,
/// which prepends a magic byte to identify the serializer format. This enables:
/// </para>
/// <list type="bullet">
///   <item>Automatic format detection during deserialization</item>
///   <item>Seamless migration between serializers</item>
///   <item>Multi-format support within the same topic</item>
/// </list>
/// <para>
/// See the pluggable serialization architecture documentation for details.
/// </para>
/// </remarks>
internal sealed partial class AwsSnsMessageBus(
	IAmazonSimpleNotificationService client,
	IPayloadSerializer serializer,
	AwsSnsOptions options,
	ILogger<AwsSnsMessageBus> logger,
	IEnvelopeCloudEventBridge? cloudEventBridge = null,
	ICloudEventEncoder<PublishRequest>? cloudEventEncoder = null,
	bool ownsClient = false,
	IMessageFailureClassifier? failureClassifier = null) : IMessageBus, IAsyncDisposable
{
	private int _disposed;
	private readonly AwsSnsSendClient _sendClient = new(client, options, logger, failureClassifier);
	private string TopicArn => options.TopicArn;

	public async Task PublishAsync(IDispatchAction action, IMessageContext context, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(action);
		ArgumentNullException.ThrowIfNull(context);

		using var publishActivity = MessagingProducerInstrumentation.StartPublishActivity(
			TransportTelemetryConstants.MessagingConventions.Systems.AwsSns, TopicArn, context.MessageId);

		if (cloudEventBridge is not null && cloudEventEncoder is not null)
		{
			await PublishWithCloudEventsAsync(action, context, LogSentAction, cancellationToken).ConfigureAwait(false);
			return;
		}

		// Use SerializeObject with runtime type to ensure proper concrete type serialization
		var payload = serializer.SerializeObject(action, action.GetType());
		var body = Convert.ToBase64String(payload);

		var traceParent = context.GetTraceParent();
		var request = new PublishRequest { TopicArn = TopicArn, Message = body };
		if (!string.IsNullOrEmpty(traceParent))
		{
			request.MessageAttributes["traceparent"] = new MessageAttributeValue { StringValue = traceParent, DataType = "String" };
		}

		await _sendClient.SendAsync(request, action, context, cancellationToken).ConfigureAwait(false);

		LogSentAction(action.GetType().Name);
	}

	public async Task PublishAsync(IDispatchEvent evt, IMessageContext context, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(evt);
		ArgumentNullException.ThrowIfNull(context);

		using var publishActivity = MessagingProducerInstrumentation.StartPublishActivity(
			TransportTelemetryConstants.MessagingConventions.Systems.AwsSns, TopicArn, context.MessageId);

		if (cloudEventBridge is not null && cloudEventEncoder is not null)
		{
			await PublishWithCloudEventsAsync(evt, context, LogPublishedEvent, cancellationToken).ConfigureAwait(false);
			return;
		}

		// Use SerializeObject with runtime type to ensure proper concrete type serialization
		var payload = serializer.SerializeObject(evt, evt.GetType());
		var body = Convert.ToBase64String(payload);

		var traceParent = context.GetTraceParent();
		var request = new PublishRequest { TopicArn = TopicArn, Message = body };
		if (!string.IsNullOrEmpty(traceParent))
		{
			request.MessageAttributes["traceparent"] = new MessageAttributeValue { StringValue = traceParent, DataType = "String" };
		}

		await _sendClient.SendAsync(request, evt, context, cancellationToken).ConfigureAwait(false);

		LogPublishedEvent(evt.GetType().Name);
	}

	public async Task PublishAsync(IDispatchDocument doc, IMessageContext context, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(doc);
		ArgumentNullException.ThrowIfNull(context);

		using var publishActivity = MessagingProducerInstrumentation.StartPublishActivity(
			TransportTelemetryConstants.MessagingConventions.Systems.AwsSns, TopicArn, context.MessageId);

		if (cloudEventBridge is not null && cloudEventEncoder is not null)
		{
			await PublishWithCloudEventsAsync(doc, context, LogSentDocument, cancellationToken).ConfigureAwait(false);
			return;
		}

		// Use SerializeObject with runtime type to ensure proper concrete type serialization
		var payload = serializer.SerializeObject(doc, doc.GetType());
		var body = Convert.ToBase64String(payload);

		var traceParent = context.GetTraceParent();
		var request = new PublishRequest { TopicArn = TopicArn, Message = body };
		if (!string.IsNullOrEmpty(traceParent))
		{
			request.MessageAttributes["traceparent"] = new MessageAttributeValue { StringValue = traceParent, DataType = "String" };
		}

		await _sendClient.SendAsync(request, doc, context, cancellationToken).ConfigureAwait(false);

		LogSentDocument(doc.GetType().Name);
	}

	public ValueTask DisposeAsync()
	{
		if (Interlocked.Exchange(ref _disposed, 1) == 0 && ownsClient) { client.Dispose(); }
		return ValueTask.CompletedTask;
	}

	private static MessageEnvelope CreateEnvelope(IDispatchMessage message, IMessageContext context)
	{
		// The declared name, not the CLR FullName -- mirrors the other transports' CreateEnvelope for
		// the same reason (it becomes the outgoing CloudEvent type attribute).
		var messageClrType = message.GetType();
		var messageName = context.GetMessageType() ?? MessageNameHelper.GetName(messageClrType);

		var envelope = new MessageEnvelope(message)
		{
			MessageId = context.MessageId ?? Uuid7Extensions.GenerateString(),
			ExternalId = context.GetExternalId(),
			UserId = context.GetUserId(),
			CorrelationId = context.CorrelationId,
			CausationId = context.CausationId,
			TraceParent = context.GetTraceParent(),
			TenantId = context.GetTenantId(),
			MessageType = messageName,
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
				.ToTransportAsync<PublishRequest>(envelope, cloudEventEncoder!.Options.DefaultMode, cancellationToken)
				.ConfigureAwait(false);

			request.TopicArn = TopicArn;

			await _sendClient.SendAsync(request, message, context, cancellationToken).ConfigureAwait(false);
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

	// Source-generated logging methods
	[LoggerMessage(AwsSqsEventId.SnsSentAction, LogLevel.Information,
		"Sent action via SNS: {Action}")]
	private partial void LogSentAction(string action);

	[LoggerMessage(AwsSqsEventId.SnsPublishedEvent, LogLevel.Information,
		"Published event via SNS: {Event}")]
	private partial void LogPublishedEvent(string @event);

	[LoggerMessage(AwsSqsEventId.SnsSentDocument, LogLevel.Information,
		"Sent document via SNS: {Doc}")]
	private partial void LogSentDocument(string doc);
}
