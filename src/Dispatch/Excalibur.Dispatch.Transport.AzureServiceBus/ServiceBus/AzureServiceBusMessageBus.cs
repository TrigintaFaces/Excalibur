// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Azure.Messaging.ServiceBus;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Extensions;
using Excalibur.Dispatch.Transport.AzureServiceBus;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Transport.Azure;

/// <summary>
/// Azure Service Bus implementation of the message bus for publishing dispatch actions, events, and documents.
/// </summary>
/// <param name="client"> The Azure Service Bus client for sending messages. </param>
/// <param name="serializer"> Payload serializer for message body serialization with pluggable format support. </param>
/// <param name="serviceBusOptions"> The Service Bus specific configuration options. </param>
/// <param name="logger"> The logger instance for diagnostic information. </param>
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
public sealed partial class AzureServiceBusMessageBus(
	ServiceBusClient client,
	IPayloadSerializer serializer,
	AzureServiceBusOptions serviceBusOptions,
	ILogger<AzureServiceBusMessageBus> logger) : IMessageBus, IAsyncDisposable
{
	private readonly ServiceBusSender _sender = client.CreateSender(serviceBusOptions.QueueName);

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

		// Use SerializeObject with runtime type to ensure proper concrete type serialization
		var payload = serializer.SerializeObject(action, action.GetType());
		ReadOnlyMemory<byte> body = payload;

		var message = new ServiceBusMessage(body) { CorrelationId = context.CorrelationId };
		var traceParent = context.GetTraceParent();
		if (!string.IsNullOrEmpty(traceParent) && message.ApplicationProperties != null)
		{
			message.ApplicationProperties["trace-parent"] = traceParent;
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

		// Use SerializeObject with runtime type to ensure proper concrete type serialization
		var payload = serializer.SerializeObject(evt, evt.GetType());
		ReadOnlyMemory<byte> body = payload;

		var message = new ServiceBusMessage(body) { CorrelationId = context.CorrelationId };
		var traceParent = context.GetTraceParent();
		if (!string.IsNullOrEmpty(traceParent) && message.ApplicationProperties != null)
		{
			message.ApplicationProperties["trace-parent"] = traceParent;
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

		// Use SerializeObject with runtime type to ensure proper concrete type serialization
		var payload = serializer.SerializeObject(doc, doc.GetType());
		ReadOnlyMemory<byte> body = payload;

		var message = new ServiceBusMessage(body) { CorrelationId = context.CorrelationId };
		var traceParent = context.GetTraceParent();
		if (!string.IsNullOrEmpty(traceParent) && message.ApplicationProperties != null)
		{
			message.ApplicationProperties["trace-parent"] = traceParent;
		}

		await _sender.SendMessageAsync(message, cancellationToken).ConfigureAwait(false);

		LogSentDocument(doc.GetType().Name);
	}

	/// <summary>
	/// Asynchronously disposes the message bus resources.
	/// </summary>
	/// <returns> A task representing the asynchronous disposal operation. </returns>
	public ValueTask DisposeAsync() => _sender.DisposeAsync();

	// Source-generated logging methods (Sprint 362 - EventId Migration)
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
