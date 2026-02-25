// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;

using Azure.Storage.Queues.Models;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Transport.AzureServiceBus;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Transport.Azure;

/// <summary>
/// Handles message processing logic for Azure Storage Queue messages.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="MessageProcessor" /> class.
/// </remarks>
/// <param name="cloudEventProcessor"> The cloud event processor. </param>
/// <param name="messageEnvelopeFactory"> The message envelope factory. </param>
/// <param name="deadLetterQueueHandler"> The dead letter queue handler. </param>
/// <param name="logger"> The logger instance. </param>
public sealed partial class MessageProcessor(
	ICloudEventProcessor cloudEventProcessor,
	IMessageEnvelopeFactory messageEnvelopeFactory,
	IDeadLetterQueueHandler deadLetterQueueHandler,
	ILogger<MessageProcessor> logger) : IMessageProcessor
{
	private readonly ICloudEventProcessor _cloudEventProcessor =
		cloudEventProcessor ?? throw new ArgumentNullException(nameof(cloudEventProcessor));

	private readonly IMessageEnvelopeFactory _messageEnvelopeFactory =
		messageEnvelopeFactory ?? throw new ArgumentNullException(nameof(messageEnvelopeFactory));

	private readonly IDeadLetterQueueHandler _deadLetterQueueHandler =
		deadLetterQueueHandler ?? throw new ArgumentNullException(nameof(deadLetterQueueHandler));

	private readonly ILogger<MessageProcessor> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

	/// <inheritdoc />
	[RequiresUnreferencedCode("Message processing uses reflection-based deserialization that may require unreferenced types")]
	[RequiresDynamicCode("Message processing uses reflection-based deserialization that requires runtime code generation")]
	public Task<IDispatchEvent> ProcessMessageAsync(QueueMessage queueMessage, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(queueMessage);

		var messageText = queueMessage.Body.ToString();
		var messageContext = _messageEnvelopeFactory.CreateContext(queueMessage);

		// Try to parse as CloudEvent first
		if (_cloudEventProcessor.TryParseCloudEvent(messageText, out var cloudEvent) && cloudEvent != null)
		{
			LogParsedAsCloudEvent(queueMessage.MessageId, cloudEvent.Type ?? "null");

			// Update context with CloudEvent data
			_cloudEventProcessor.UpdateContextFromCloudEvent(messageContext, cloudEvent);

			// Convert to dispatch event
			var dispatchEvent = _cloudEventProcessor.ConvertToDispatchEvent(cloudEvent, queueMessage, messageContext);
			return Task.FromResult(dispatchEvent);
		}

		// Fall back to parsing as message envelope
		var parseResult = _messageEnvelopeFactory.ParseMessage(queueMessage);
		LogParsedAsEnvelope(queueMessage.MessageId, parseResult.MessageType.Name);

		// Convert the parsed message to a dispatch event
		if (parseResult.Message is IDispatchEvent parsedDispatchEvent)
		{
			LogSuccessfullyConverted(queueMessage.MessageId, parsedDispatchEvent.GetType().Name);
			return Task.FromResult(parsedDispatchEvent);
		}

		// If the message is not an IDispatchEvent, it's likely a data-only message that needs wrapping
		LogInvalidMessageType(queueMessage.MessageId, parseResult.MessageType.Name);
		throw new InvalidOperationException($"Message of type {parseResult.MessageType.Name} is not an IDispatchEvent");
	}

	/// <inheritdoc />
	public async Task HandleMessageRejectionAsync(QueueMessage queueMessage, string reason, CancellationToken cancellationToken, Exception? exception = null)
	{
		ArgumentNullException.ThrowIfNull(queueMessage);
		ArgumentException.ThrowIfNullOrWhiteSpace(reason);

		var context = _messageEnvelopeFactory.CreateContext(queueMessage);

		// Check if message should be dead lettered
		if (_deadLetterQueueHandler.ShouldDeadLetter(queueMessage, context, exception))
		{
			await _deadLetterQueueHandler.SendToDeadLetterAsync(queueMessage, context, reason, cancellationToken, exception)
				.ConfigureAwait(false);
			LogMessageSentToDeadLetter(queueMessage.MessageId, reason);
		}
		else
		{
			LogMessageRejectedButNotDeadLettered(queueMessage.MessageId, reason);
		}
	}

	// Source-generated logging methods (Sprint 362 - EventId Migration)
	[LoggerMessage(AzureServiceBusEventId.StorageQueueCloudEventParsed, LogLevel.Debug,
		"Parsed message {MessageId} as CloudEvent with type {CloudEventType}")]
	private partial void LogParsedAsCloudEvent(string messageId, string cloudEventType);

	[LoggerMessage(AzureServiceBusEventId.StorageQueueEnvelopeParsed, LogLevel.Debug,
		"Parsed message {MessageId} as envelope with type {MessageType}")]
	private partial void LogParsedAsEnvelope(string messageId, string messageType);

	[LoggerMessage(AzureServiceBusEventId.StorageQueueMessageConverted, LogLevel.Debug,
		"Successfully converted message {MessageId} to dispatch event of type {EventType}")]
	private partial void LogSuccessfullyConverted(string messageId, string eventType);

	[LoggerMessage(AzureServiceBusEventId.StorageQueueInvalidMessageType, LogLevel.Error,
		"Message {MessageId} of type {MessageType} is not an IDispatchEvent and cannot be processed")]
	private partial void LogInvalidMessageType(string messageId, string messageType);

	[LoggerMessage(AzureServiceBusEventId.StorageQueueDeadLettered, LogLevel.Warning,
		"Message {MessageId} sent to dead letter queue. Reason: {Reason}")]
	private partial void LogMessageSentToDeadLetter(string messageId, string reason);

	[LoggerMessage(AzureServiceBusEventId.StorageQueueRejectedNotDeadLettered, LogLevel.Warning,
		"Message {MessageId} rejected but not dead lettered. Reason: {Reason}")]
	private partial void LogMessageRejectedButNotDeadLettered(string messageId, string reason);
}
