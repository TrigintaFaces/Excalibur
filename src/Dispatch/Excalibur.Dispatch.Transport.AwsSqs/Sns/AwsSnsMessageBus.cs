// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Extensions;
using Excalibur.Dispatch.Transport.AwsSqs;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// AWS SNS implementation of the message bus for publishing dispatch actions, events, and documents.
/// </summary>
/// <param name="client"> The AWS SNS client for publishing messages. </param>
/// <param name="serializer"> Payload serializer for message body serialization with pluggable format support. </param>
/// <param name="options"> The SNS specific configuration options. </param>
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
public sealed partial class AwsSnsMessageBus(
	IAmazonSimpleNotificationService client,
	IPayloadSerializer serializer,
	AwsSnsOptions options,
	ILogger<AwsSnsMessageBus> logger) : IMessageBus, IAsyncDisposable
{
	private string TopicArn => options.TopicArn;

	public async Task PublishAsync(IDispatchAction action, IMessageContext context, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(action);
		ArgumentNullException.ThrowIfNull(context);

		// Use SerializeObject with runtime type to ensure proper concrete type serialization
		var payload = serializer.SerializeObject(action, action.GetType());
		var body = Convert.ToBase64String(payload);

		var traceParent = context.GetTraceParent();
		var request = new PublishRequest { TopicArn = TopicArn, Message = body };
		if (!string.IsNullOrEmpty(traceParent))
		{
			request.MessageAttributes["trace-parent"] = new MessageAttributeValue { StringValue = traceParent, DataType = "String" };
		}

		_ = await client.PublishAsync(request, cancellationToken).ConfigureAwait(false);

		LogSentAction(action.GetType().Name);
	}

	public async Task PublishAsync(IDispatchEvent evt, IMessageContext context, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(evt);
		ArgumentNullException.ThrowIfNull(context);

		// Use SerializeObject with runtime type to ensure proper concrete type serialization
		var payload = serializer.SerializeObject(evt, evt.GetType());
		var body = Convert.ToBase64String(payload);

		var traceParent = context.GetTraceParent();
		var request = new PublishRequest { TopicArn = TopicArn, Message = body };
		if (!string.IsNullOrEmpty(traceParent))
		{
			request.MessageAttributes["trace-parent"] = new MessageAttributeValue { StringValue = traceParent, DataType = "String" };
		}

		_ = await client.PublishAsync(request, cancellationToken).ConfigureAwait(false);

		LogPublishedEvent(evt.GetType().Name);
	}

	public async Task PublishAsync(IDispatchDocument doc, IMessageContext context, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(doc);
		ArgumentNullException.ThrowIfNull(context);

		// Use SerializeObject with runtime type to ensure proper concrete type serialization
		var payload = serializer.SerializeObject(doc, doc.GetType());
		var body = Convert.ToBase64String(payload);

		var traceParent = context.GetTraceParent();
		var request = new PublishRequest { TopicArn = TopicArn, Message = body };
		if (!string.IsNullOrEmpty(traceParent))
		{
			request.MessageAttributes["trace-parent"] = new MessageAttributeValue { StringValue = traceParent, DataType = "String" };
		}

		_ = await client.PublishAsync(request, cancellationToken).ConfigureAwait(false);

		LogSentDocument(doc.GetType().Name);
	}

	public ValueTask DisposeAsync()
	{
		client.Dispose();
		return ValueTask.CompletedTask;
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
