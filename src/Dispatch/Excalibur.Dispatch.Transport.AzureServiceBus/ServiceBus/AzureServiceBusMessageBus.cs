// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


using Azure.Messaging.ServiceBus;

using Excalibur.Dispatch;
using Excalibur.Dispatch.CloudEvents;
using Excalibur.Dispatch.Features;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Serialization;
using Excalibur.Dispatch.Transport.AzureServiceBus;
using Excalibur.Dispatch.Transport.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Transport.Azure;

/// <summary>
/// Azure Service Bus implementation of the message bus for publishing dispatch actions, events, and documents.
/// </summary>
/// <param name="client"> The Azure Service Bus client for sending messages. </param>
/// <param name="serializer"> Payload serializer for message body serialization with pluggable format support. </param>
/// <param name="serviceBusOptions"> The Service Bus specific configuration options. </param>
/// <param name="logger"> The logger instance for diagnostic information. </param>
/// <param name="cloudEventBridge"> Optional envelope-to-CloudEvent bridge; when supplied with <paramref name="cloudEventEncoder"/>, every publish is emitted as a CloudEvent instead of the native envelope format. </param>
/// <param name="cloudEventEncoder"> Optional CloudEvents encoder for <see cref="ServiceBusMessage"/>. </param>
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
internal sealed partial class AzureServiceBusMessageBus(
	ServiceBusClient client,
	IPayloadSerializer serializer,
	AzureServiceBusOptions serviceBusOptions,
	ILogger<AzureServiceBusMessageBus> logger,
	IEnvelopeCloudEventBridge? cloudEventBridge = null,
	ICloudEventEncoder<ServiceBusMessage>? cloudEventEncoder = null) : IMessageBus, IAsyncDisposable
{
	private readonly ServiceBusSender _sender = client.CreateSender(serviceBusOptions.Sender.DefaultEntityName ?? string.Empty);

	private readonly string _entityName = serviceBusOptions.Sender.DefaultEntityName ?? string.Empty;

	/// <summary>
	/// Publishes a dispatch action to the Service Bus queue.
	/// </summary>
	/// <param name="action"> The dispatch action to publish. </param>
	/// <param name="context"> The message context containing correlation and tracing information. </param>
	/// <param name="cancellationToken"> The cancellation token to observe. </param>
	/// <returns> A task representing the asynchronous publish operation. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when action or context is null. </exception>
	public async Task PublishAsync(IDispatchAction action, IMessageContext context, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(action);
		ArgumentNullException.ThrowIfNull(context);

		using var publishActivity = MessagingProducerInstrumentation.StartPublishActivity(
			TransportTelemetryConstants.MessagingConventions.Systems.AzureServiceBus, _entityName, context.MessageId);

		if (cloudEventBridge is not null && cloudEventEncoder is not null)
		{
			await PublishWithCloudEventsAsync(action, context, LogSentAction, cancellationToken).ConfigureAwait(false);
			return;
		}

		// Use SerializeObject with runtime type to ensure proper concrete type serialization
		var payload = serializer.SerializeObject(action, action.GetType());
		ReadOnlyMemory<byte> body = payload;

		var message = new ServiceBusMessage(body) { CorrelationId = context.CorrelationId };
		var traceParent = context.GetTraceParent();
		if (!string.IsNullOrEmpty(traceParent) && message.ApplicationProperties != null)
		{
			message.ApplicationProperties["traceparent"] = traceParent;
		}

		await _sender.SendMessageAsync(message, cancellationToken).ConfigureAwait(false);

		LogSentAction(action.GetType().Name);
	}

	/// <summary>
	/// Publishes a dispatch event to the Service Bus queue.
	/// </summary>
	/// <param name="evt"> The dispatch event to publish. </param>
	/// <param name="context"> The message context containing correlation and tracing information. </param>
	/// <param name="cancellationToken"> The cancellation token to observe. </param>
	/// <returns> A task representing the asynchronous publish operation. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when evt or context is null. </exception>
	public async Task PublishAsync(IDispatchEvent evt, IMessageContext context, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(evt);
		ArgumentNullException.ThrowIfNull(context);

		using var publishActivity = MessagingProducerInstrumentation.StartPublishActivity(
			TransportTelemetryConstants.MessagingConventions.Systems.AzureServiceBus, _entityName, context.MessageId);

		if (cloudEventBridge is not null && cloudEventEncoder is not null)
		{
			await PublishWithCloudEventsAsync(evt, context, LogSentEvent, cancellationToken).ConfigureAwait(false);
			return;
		}

		// Use SerializeObject with runtime type to ensure proper concrete type serialization
		var payload = serializer.SerializeObject(evt, evt.GetType());
		ReadOnlyMemory<byte> body = payload;

		var message = new ServiceBusMessage(body) { CorrelationId = context.CorrelationId };
		var traceParent = context.GetTraceParent();
		if (!string.IsNullOrEmpty(traceParent) && message.ApplicationProperties != null)
		{
			message.ApplicationProperties["traceparent"] = traceParent;
		}

		await _sender.SendMessageAsync(message, cancellationToken).ConfigureAwait(false);

		LogSentEvent(evt.GetType().Name);
	}

	/// <summary>
	/// Publishes a dispatch document to the Service Bus queue.
	/// </summary>
	/// <param name="doc"> The dispatch document to publish. </param>
	/// <param name="context"> The message context containing correlation and tracing information. </param>
	/// <param name="cancellationToken"> The cancellation token to observe. </param>
	/// <returns> A task representing the asynchronous publish operation. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when doc or context is null. </exception>
	public async Task PublishAsync(IDispatchDocument doc, IMessageContext context, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(doc);
		ArgumentNullException.ThrowIfNull(context);

		using var publishActivity = MessagingProducerInstrumentation.StartPublishActivity(
			TransportTelemetryConstants.MessagingConventions.Systems.AzureServiceBus, _entityName, context.MessageId);

		if (cloudEventBridge is not null && cloudEventEncoder is not null)
		{
			await PublishWithCloudEventsAsync(doc, context, LogSentDocument, cancellationToken).ConfigureAwait(false);
			return;
		}

		// Use SerializeObject with runtime type to ensure proper concrete type serialization
		var payload = serializer.SerializeObject(doc, doc.GetType());
		ReadOnlyMemory<byte> body = payload;

		var message = new ServiceBusMessage(body) { CorrelationId = context.CorrelationId };
		var traceParent = context.GetTraceParent();
		if (!string.IsNullOrEmpty(traceParent) && message.ApplicationProperties != null)
		{
			message.ApplicationProperties["traceparent"] = traceParent;
		}

		await _sender.SendMessageAsync(message, cancellationToken).ConfigureAwait(false);

		LogSentDocument(doc.GetType().Name);
	}

	private static MessageEnvelope CreateEnvelope(IDispatchMessage message, IMessageContext context)
	{
		// The declared name, not the CLR FullName -- mirrors RabbitMqMessageBus/AwsSqsMessageBus's
		// CreateEnvelope for the same reason (it becomes the outgoing CloudEvent type attribute).
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
			var transportMessage = await cloudEventBridge!
				.ToTransportAsync<ServiceBusMessage>(envelope, cloudEventEncoder!.Options.DefaultMode, cancellationToken)
				.ConfigureAwait(false);

			await _sender.SendMessageAsync(transportMessage, cancellationToken).ConfigureAwait(false);
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
	/// Asynchronously disposes the message bus resources.
	/// </summary>
	/// <returns> A task representing the asynchronous disposal operation. </returns>
	public ValueTask DisposeAsync() => _sender.DisposeAsync();

	// Source-generated logging methods
	[LoggerMessage(AzureServiceBusEventId.ActionSent, LogLevel.Information,
		"Sent action via Azure Service Bus: {Action}")]
	private partial void LogSentAction(string action);

	[LoggerMessage(AzureServiceBusEventId.EventSent, LogLevel.Information,
		"Sent event via Azure Service Bus: {Event}")]
	private partial void LogSentEvent(string @event);

	[LoggerMessage(AzureServiceBusEventId.DocumentSent, LogLevel.Information,
		"Sent document via Azure Service Bus: {Doc}")]
	private partial void LogSentDocument(string doc);
}
