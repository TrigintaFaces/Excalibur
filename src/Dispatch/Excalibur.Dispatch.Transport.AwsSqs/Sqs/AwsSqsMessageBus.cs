// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Amazon.SQS;
using Amazon.SQS.Model;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Transport.AwsSqs;

using Microsoft.Extensions.Logging;

using AwsMessageContextSerializer = Excalibur.Dispatch.Transport.Aws.MessageContextSerializer;

namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// AWS SQS implementation of the message bus for publishing dispatch actions, events, and documents.
/// </summary>
/// <param name="client"> The AWS SQS client for sending messages. </param>
/// <param name="serializer"> Payload serializer for message body serialization with pluggable format support. </param>
/// <param name="options"> The SQS specific configuration options. </param>
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
public sealed partial class AwsSqsMessageBus(
	IAmazonSQS client,
	IPayloadSerializer serializer,
	AwsSqsOptions options,
	ILogger<AwsSqsMessageBus> logger) : IMessageBus, IAsyncDisposable
{
	private const string QueueUrlNotConfiguredMessage = "QueueUrl is not configured";

	/// <inheritdoc/>
	public async Task PublishAsync(IDispatchAction action, IMessageContext context, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(action);
		ArgumentNullException.ThrowIfNull(context);

		// Use SerializeObject with runtime type to ensure proper concrete type serialization
		var bytes = serializer.SerializeObject(action, action.GetType());
		var body = Convert.ToBase64String(bytes);

		var request = new SendMessageRequest(
			options.QueueUrl?.ToString() ?? throw new InvalidOperationException(QueueUrlNotConfiguredMessage), body);
		request.MessageAttributes ??= new Dictionary<string, MessageAttributeValue>(StringComparer.Ordinal);

		// Serialize ALL context fields to preserve complete message context
		AwsMessageContextSerializer.SerializeToMessageAttributes(context, request.MessageAttributes);

		_ = await client.SendMessageAsync(request, cancellationToken).ConfigureAwait(false);

		LogSentAction(action.GetType().Name);
	}

	/// <inheritdoc/>
	public async Task PublishAsync(IDispatchEvent evt, IMessageContext context, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(evt);
		ArgumentNullException.ThrowIfNull(context);

		// Use SerializeObject with runtime type to ensure proper concrete type serialization
		var bytes = serializer.SerializeObject(evt, evt.GetType());
		var body = Convert.ToBase64String(bytes);

		var requestEvt =
			new SendMessageRequest(options.QueueUrl?.ToString() ?? throw new InvalidOperationException(QueueUrlNotConfiguredMessage), body);
		requestEvt.MessageAttributes ??= new Dictionary<string, MessageAttributeValue>(StringComparer.Ordinal);

		// Serialize ALL context fields to preserve complete message context
		AwsMessageContextSerializer.SerializeToMessageAttributes(context, requestEvt.MessageAttributes);

		_ = await client.SendMessageAsync(requestEvt, cancellationToken).ConfigureAwait(false);

		LogPublishedEvent(evt.GetType().Name);
	}

	/// <inheritdoc/>
	public async Task PublishAsync(IDispatchDocument doc, IMessageContext context, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(doc);
		ArgumentNullException.ThrowIfNull(context);

		// Use SerializeObject with runtime type to ensure proper concrete type serialization
		var bytes = serializer.SerializeObject(doc, doc.GetType());
		var body = Convert.ToBase64String(bytes);

		var requestDoc =
			new SendMessageRequest(options.QueueUrl?.ToString() ?? throw new InvalidOperationException(QueueUrlNotConfiguredMessage), body);
		requestDoc.MessageAttributes ??= new Dictionary<string, MessageAttributeValue>(StringComparer.Ordinal);

		// Serialize ALL context fields to preserve complete message context
		AwsMessageContextSerializer.SerializeToMessageAttributes(context, requestDoc.MessageAttributes);

		_ = await client.SendMessageAsync(requestDoc, cancellationToken).ConfigureAwait(false);

		LogSentDocument(doc.GetType().Name);
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
