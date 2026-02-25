// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Transport.GooglePubSub;

using Google.Cloud.PubSub.V1;
using Google.Protobuf;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Google Cloud Pub/Sub implementation of the message bus for publishing dispatch actions, events, and documents.
/// </summary>
/// <param name="client"> The Google Pub/Sub publisher client for sending messages. </param>
/// <param name="serializer"> Payload serializer for message body serialization with pluggable format support. </param>
/// <param name="options"> The Pub/Sub specific configuration options. </param>
/// <param name="logger"> The logger instance for diagnostic information. </param>
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
public sealed partial class GooglePubSubMessageBus(
	PublisherClient client,
	IPayloadSerializer serializer,
	GooglePubSubOptions options,
	ILogger<GooglePubSubMessageBus> logger) : IMessageBus
{
	/// <inheritdoc/>
	public async Task PublishAsync(IDispatchAction action, IMessageContext context, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(action);
		ArgumentNullException.ThrowIfNull(context);

		// Use SerializeObject with runtime type to ensure proper concrete type serialization
		var payload = serializer.SerializeObject(action, action.GetType());
		var body = Convert.ToBase64String(payload);

		var traceParent = context.TraceParent;
		var message = new PubsubMessage { Data = ByteString.CopyFromUtf8(body) };
		if (!string.IsNullOrEmpty(traceParent))
		{
			message.Attributes["trace-parent"] = traceParent;
		}

		_ = await client.PublishAsync(message).ConfigureAwait(false);

		if (logger.IsEnabled(LogLevel.Information))
		{
			LogSentAction(action.GetType().Name);
		}
	}

	/// <inheritdoc/>
	public async Task PublishAsync(IDispatchEvent evt, IMessageContext context, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(evt);
		ArgumentNullException.ThrowIfNull(context);

		// Use SerializeObject with runtime type to ensure proper concrete type serialization
		var payload = serializer.SerializeObject(evt, evt.GetType());
		var body = Convert.ToBase64String(payload);

		var traceParent = context.TraceParent;
		var message = new PubsubMessage { Data = ByteString.CopyFromUtf8(body) };
		if (!string.IsNullOrEmpty(traceParent))
		{
			message.Attributes["trace-parent"] = traceParent;
		}

		_ = await client.PublishAsync(message).ConfigureAwait(false);

		if (logger.IsEnabled(LogLevel.Information))
		{
			LogPublishedEvent(evt.GetType().Name);
		}
	}

	/// <inheritdoc/>
	public async Task PublishAsync(IDispatchDocument doc, IMessageContext context, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(doc);
		ArgumentNullException.ThrowIfNull(context);

		// Use SerializeObject with runtime type to ensure proper concrete type serialization
		var payload = serializer.SerializeObject(doc, doc.GetType());
		var body = Convert.ToBase64String(payload);

		var traceParent = context.TraceParent;
		var message = new PubsubMessage { Data = ByteString.CopyFromUtf8(body) };
		if (!string.IsNullOrEmpty(traceParent))
		{
			message.Attributes["trace-parent"] = traceParent;
		}

		_ = await client.PublishAsync(message).ConfigureAwait(false);

		if (logger.IsEnabled(LogLevel.Information))
		{
			LogSentDocument(doc.GetType().Name);
		}
	}

	// Source-generated logging methods
	[LoggerMessage(GooglePubSubEventId.SentAction, LogLevel.Information,
		"Sent action via PubSub: {Action}")]
	private partial void LogSentAction(string action);

	[LoggerMessage(GooglePubSubEventId.PublishedEvent, LogLevel.Information,
		"Published event via PubSub: {Event}")]
	private partial void LogPublishedEvent(string @event);

	[LoggerMessage(GooglePubSubEventId.SentDocument, LogLevel.Information,
		"Sent document via PubSub: {Doc}")]
	private partial void LogSentDocument(string doc);
}
