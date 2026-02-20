// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Azure.Messaging.EventHubs;
using Azure.Messaging.EventHubs.Producer;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Extensions;
using Excalibur.Dispatch.Transport.AzureServiceBus;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Transport.Azure;

/// <summary>
/// Azure Event Hubs implementation of the message bus for publishing dispatch actions, events, and documents.
/// </summary>
/// <param name="producer"> The Azure Event Hub producer client for sending messages. </param>
/// <param name="serializer"> Payload serializer for message body serialization with pluggable format support. </param>
/// <param name="options"> The Event Hubs specific configuration options. </param>
/// <param name="logger"> The logger instance for diagnostic information. </param>
/// <remarks>
/// <para>
/// This message bus uses <see cref="IPayloadSerializer"/> for message body serialization,
/// which prepends a magic byte to identify the serializer format. This enables:
/// </para>
/// <list type="bullet">
///   <item>Automatic format detection during deserialization</item>
///   <item>Seamless migration between serializers</item>
///   <item>Multi-format support within the same event hub</item>
/// </list>
/// <para>
/// See the pluggable serialization architecture documentation for details.
/// </para>
/// </remarks>
public sealed partial class AzureEventHubMessageBus(
	EventHubProducerClient producer,
	IPayloadSerializer serializer,
	AzureEventHubOptions options,
	ILogger<AzureEventHubMessageBus> logger) : IMessageBus, IAsyncDisposable
{

	/// <summary>
	/// Publishes a dispatch action to the Event Hub.
	/// </summary>
	/// <param name="action"> The dispatch action to publish. </param>
	/// <param name="context"> The message context containing correlation and tracing information. </param>
	/// <param name="cancellationToken"> The cancellation token to observe. </param>
	/// <returns> A task representing the asynchronous publish operation. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when action or context is null. </exception>
	public Task PublishAsync(IDispatchAction action, IMessageContext context, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(action);
		ArgumentNullException.ThrowIfNull(context);

		LogSendingAction(action.GetType().Name);

		return SendCoreAsync(action, context, cancellationToken);
	}

	/// <summary>
	/// Publishes a dispatch event to the Event Hub.
	/// </summary>
	/// <param name="evt"> The dispatch event to publish. </param>
	/// <param name="context"> The message context containing correlation and tracing information. </param>
	/// <param name="cancellationToken"> The cancellation token to observe. </param>
	/// <returns> A task representing the asynchronous publish operation. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when evt or context is null. </exception>
	public Task PublishAsync(IDispatchEvent evt, IMessageContext context, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(evt);
		ArgumentNullException.ThrowIfNull(context);

		LogPublishingEvent(evt.GetType().Name);

		return SendCoreAsync(evt, context, cancellationToken);
	}

	/// <summary>
	/// Publishes a dispatch document to the Event Hub.
	/// </summary>
	/// <param name="doc"> The dispatch document to publish. </param>
	/// <param name="context"> The message context containing correlation and tracing information. </param>
	/// <param name="cancellationToken"> The cancellation token to observe. </param>
	/// <returns> A task representing the asynchronous publish operation. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when doc or context is null. </exception>
	public Task PublishAsync(IDispatchDocument doc, IMessageContext context, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(doc);
		ArgumentNullException.ThrowIfNull(context);

		LogSendingDocument(doc.GetType().Name);

		return SendCoreAsync(doc, context, cancellationToken);
	}

	/// <summary>
	/// Asynchronously disposes the message bus resources.
	/// </summary>
	/// <returns> A task representing the asynchronous disposal operation. </returns>
	public ValueTask DisposeAsync() => producer.DisposeAsync();

	private async Task SendCoreAsync(object messageObj, IMessageContext context, CancellationToken cancellationToken)
	{
		// Use SerializeObject with runtime type to ensure proper concrete type serialization
		var payload = serializer.SerializeObject(messageObj, messageObj.GetType());
		ReadOnlyMemory<byte> body = payload;

		using var batch = await producer.CreateBatchAsync(cancellationToken).ConfigureAwait(false);
		var evt = new EventData(body);
		if (context.CorrelationId != null)
		{
			evt.Properties["correlation-id"] = context.CorrelationId;
		}

		var traceParent = context.GetTraceParent();
		if (!string.IsNullOrEmpty(traceParent))
		{
			evt.Properties["trace-parent"] = traceParent;
		}

		_ = batch.TryAdd(evt);
		await producer.SendAsync(batch, cancellationToken).ConfigureAwait(false);
	}

	// Source-generated logging methods (Sprint 362 - EventId Migration)
	[LoggerMessage(AzureServiceBusEventId.EventHubsActionSent, LogLevel.Information,
		"Sending action via Azure Event Hub: {Action}")]
	private partial void LogSendingAction(string action);

	[LoggerMessage(AzureServiceBusEventId.EventHubsEventSent, LogLevel.Information,
		"Publishing event via Azure Event Hub: {Event}")]
	private partial void LogPublishingEvent(string @event);

	[LoggerMessage(AzureServiceBusEventId.EventHubsDocumentSent, LogLevel.Information,
		"Sending document via Azure Event Hub: {Doc}")]
	private partial void LogSendingDocument(string doc);
}
