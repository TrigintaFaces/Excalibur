// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

using Azure;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;

using Excalibur.Dispatch.Abstractions;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Transport.Azure;

/// <summary>
/// Handles dead letter queue operations for Azure Storage Queue messages.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="DeadLetterQueueHandler" /> class. </remarks>
/// <param name="deadLetterQueueClient"> The dead letter queue client. </param>
/// <param name="mainQueueClient"> The main queue client. </param>
/// <param name="logger"> The logger instance. </param>
/// <param name="options"> The dead letter queue options. </param>
public sealed class DeadLetterQueueHandler(
	QueueClient deadLetterQueueClient,
	QueueClient mainQueueClient,
	ILogger<DeadLetterQueueHandler> logger,
	DeadLetterQueueOptions? options = null) : IDeadLetterQueueHandler
{
	private readonly QueueClient _deadLetterQueueClient =
		deadLetterQueueClient ?? throw new ArgumentNullException(nameof(deadLetterQueueClient));

	private readonly QueueClient _mainQueueClient = mainQueueClient ?? throw new ArgumentNullException(nameof(mainQueueClient));
	private readonly ILogger<DeadLetterQueueHandler> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
	private readonly DeadLetterQueueOptions _options = options ?? new DeadLetterQueueOptions();
	private readonly ConcurrentDictionary<string, int> _messageRetryCount = new(StringComparer.Ordinal);
	private readonly ConcurrentDictionary<string, long> _reasonCounts = new(StringComparer.Ordinal);

	/// <inheritdoc />
	public bool ShouldDeadLetter(QueueMessage message, IMessageContext context, Exception? exception = null)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(context);

		// Check dequeue count
		if (message.DequeueCount >= _options.MaxDequeueCount)
		{
			_logger.LogWarning(
				"Message {MessageId} exceeded max dequeue count ({MaxDequeueCount})",
				message.MessageId, _options.MaxDequeueCount);
			return true;
		}

		// Check if this is a poison message based on exception type
		if (exception != null && IsPoisonException(exception))
		{
			_logger.LogWarning(
				"Message {MessageId} has poison exception: {ExceptionType}",
				message.MessageId, exception.GetType().Name);
			return true;
		}

		// Check retry count from our tracking
		var retryCount = _messageRetryCount.GetValueOrDefault(message.MessageId, 0);
		if (retryCount >= _options.MaxRetryAttempts)
		{
			_logger.LogWarning(
				"Message {MessageId} exceeded max retry attempts ({MaxRetryAttempts})",
				message.MessageId, _options.MaxRetryAttempts);
			return true;
		}

		// Check if message has been in queue too long
		var messageAge = DateTimeOffset.UtcNow - message.InsertedOn;
		if (messageAge > _options.MaxMessageAge)
		{
			_logger.LogWarning(
				"Message {MessageId} exceeded max age ({MaxMessageAge})",
				message.MessageId, _options.MaxMessageAge);
			return true;
		}

		return false;
	}

	/// <inheritdoc />
	[RequiresUnreferencedCode("Dead letter envelope serialization may require unreferenced types for reflection-based operations")]
	[RequiresDynamicCode("Dead letter envelope serialization uses reflection to dynamically access and serialize types")]
	public async Task SendToDeadLetterAsync(QueueMessage message, IMessageContext context, string reason, CancellationToken cancellationToken, Exception? exception = null)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(context);
		ArgumentException.ThrowIfNullOrWhiteSpace(reason);

		try
		{
			// Create dead letter message envelope
			var deadLetterEnvelope = new DeadLetterMessageEnvelope
			{
				OriginalMessageId = message.MessageId,
				OriginalMessage = message.Body.ToString(),
				DeadLetterReason = reason,
				DeadLetterTimestamp = DateTimeOffset.UtcNow,
				OriginalDequeueCount = (int)message.DequeueCount,
				ExceptionDetails = exception?.ToString(),
				CorrelationId = context.CorrelationId,
				MessageType = context.GetItem<string>("MessageType"),
				Properties = context.Items?.ToDictionary(static kv => kv.Key, static kv => kv.Value?.ToString()) ?? [],
			};

			var deadLetterJson = JsonSerializer.Serialize(deadLetterEnvelope);

			// Send to dead letter queue
			_ = await _deadLetterQueueClient.SendMessageAsync(deadLetterJson, cancellationToken: cancellationToken).ConfigureAwait(false);

			// Update reason counts
			_ = _reasonCounts.AddOrUpdate(reason, 1, static (key, value) =>
			{
				_ = key;
				return value + 1;
			});

			// Remove from retry tracking
			_ = _messageRetryCount.TryRemove(message.MessageId, out _);

			_logger.LogWarning(
				"Message {MessageId} sent to dead letter queue. Reason: {Reason}",
				message.MessageId, reason);
		}
		catch (RequestFailedException ex)
		{
			_logger.LogError(ex, "Failed to send message {MessageId} to dead letter queue", message.MessageId);
			throw;
		}
	}

	/// <inheritdoc />
	[RequiresUnreferencedCode("This method uses reflection and may not work correctly with trimming")]
	[RequiresDynamicCode("This method uses dynamic code generation and may not work correctly with AOT")]
	public async Task HandlePoisonMessageAsync(QueueMessage message, IMessageContext context, Exception exception,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(exception);

		// Increment retry count
		_ = _messageRetryCount.AddOrUpdate(message.MessageId, 1, static (key, value) =>
		{
			_ = key;
			return value + 1;
		});

		var retryCount = _messageRetryCount[message.MessageId];
		var reason = $"Poison message after {retryCount} attempts: {exception.GetType().Name}";

		if (ShouldDeadLetter(message, context, exception))
		{
			await SendToDeadLetterAsync(message, context, reason, cancellationToken, exception).ConfigureAwait(false);
		}
		else
		{
			_logger.LogWarning(
				"Poison message {MessageId} will be retried. Attempt {RetryCount}/{MaxRetryAttempts}. Exception: {Exception}",
				message.MessageId, retryCount, _options.MaxRetryAttempts, exception.Message);
		}
	}

	/// <inheritdoc />
	public async Task<DeadLetterQueueStatistics> GetStatisticsAsync(CancellationToken cancellationToken)
	{
		try
		{
			var properties = await _deadLetterQueueClient.GetPropertiesAsync(cancellationToken).ConfigureAwait(false);
			var totalMessages = properties.Value.ApproximateMessagesCount;

			// For now, we can't easily get time-based statistics from Azure Storage Queues This would require more sophisticated tracking
			// or external storage
			var isHealthy = totalMessages < _options.MaxDeadLetterQueueSize;

			return new DeadLetterQueueStatistics
			{
				TotalMessages = totalMessages,
				MessagesLastHour = 0, // Would need external tracking
				MessagesLastDay = 0, // Would need external tracking
				ReasonCounts = _reasonCounts.ToDictionary(static kv => kv.Key, static kv => kv.Value, StringComparer.Ordinal),
				IsHealthy = isHealthy,
			};
		}
		catch (RequestFailedException ex)
		{
			_logger.LogError(ex, "Failed to get dead letter queue statistics");
			return new DeadLetterQueueStatistics
			{
				ReasonCounts = _reasonCounts.ToDictionary(static kv => kv.Key, static kv => kv.Value, StringComparer.Ordinal),
				IsHealthy = false,
			};
		}
	}

	/// <inheritdoc />
	[RequiresUnreferencedCode("Dead letter envelope deserialization may require unreferenced types for reflection-based operations")]
	[RequiresDynamicCode("Dead letter envelope deserialization uses reflection to dynamically create and populate types")]
	public async Task<int> RecoverMessagesAsync(CancellationToken cancellationToken, int maxMessages = 10)
	{
		if (maxMessages <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(maxMessages), "Max messages must be greater than zero");
		}

		var recoveredCount = 0;

		try
		{
			var response = await _deadLetterQueueClient.ReceiveMessagesAsync(maxMessages, cancellationToken: cancellationToken)
				.ConfigureAwait(false);
			foreach (var message in response.Value)
			{
				try
				{
					var envelope = JsonSerializer.Deserialize<DeadLetterMessageEnvelope>(message.Body.ToString());
					if (envelope != null && ShouldRecover(envelope))
					{
						// Send back to main queue
						_ = await _mainQueueClient.SendMessageAsync(envelope.OriginalMessage, cancellationToken: cancellationToken)
							.ConfigureAwait(false);

						// Delete from dead letter queue
						_ = await _deadLetterQueueClient.DeleteMessageAsync(message.MessageId, message.PopReceipt, cancellationToken)
							.ConfigureAwait(false);

						recoveredCount++;
						_logger.LogInformation("Recovered message {OriginalMessageId} from dead letter queue", envelope.OriginalMessageId);
					}
					else
					{
						_logger.LogDebug("Message {MessageId} not eligible for recovery", message.MessageId);
					}
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "Failed to recover message {MessageId}", message.MessageId);
				}
			}
		}
		catch (RequestFailedException ex)
		{
			_logger.LogError(ex, "Failed to receive messages from dead letter queue for recovery");
		}

		_logger.LogInformation("Recovered {RecoveredCount} messages from dead letter queue", recoveredCount);
		return recoveredCount;
	}

	private static bool ShouldRecover(DeadLetterMessageEnvelope envelope)
	{
		// Simple recovery logic - could be enhanced with more sophisticated rules
		var age = DateTimeOffset.UtcNow - envelope.DeadLetterTimestamp;

		// Only recover messages that have been in DLQ for more than 1 hour and were not dead lettered due to poison exceptions
		return age > TimeSpan.FromHours(1) &&
			   !envelope.DeadLetterReason.Contains("Poison", StringComparison.OrdinalIgnoreCase);
	}

	private static bool IsPoisonException(Exception exception) =>

		// Define poison exception types that indicate the message should not be retried
		exception is JsonException or
			ArgumentException or
			InvalidOperationException or
			NotSupportedException;
}
