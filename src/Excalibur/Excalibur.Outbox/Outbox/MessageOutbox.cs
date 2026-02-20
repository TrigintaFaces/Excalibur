// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text;
using System.Text.Json;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Messaging;
using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Options.Delivery;

using Excalibur.Outbox.Diagnostics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Delivery;

/// <summary>
/// Primary implementation of the outbox pattern for reliable message delivery with transactional guarantees. This component manages
/// deferred message publishing to ensure consistency between business operations and message delivery, implementing at-least-once delivery
/// semantics with configurable retry logic and error handling.
/// </summary>
/// <param name="outboxStore"> Persistent store for outbox message management and retrieval operations. </param>
/// <param name="outboxProcessor"> Processor responsible for dispatching messages from outbox to message brokers. </param>
/// <param name="serializer"> JSON serializer for message and metadata serialization/deserialization. </param>
/// <param name="options"> Configuration options for outbox behavior including batch sizes and TTL settings. </param>
/// <param name="logger"> Logger for outbox operations, error tracking, and performance monitoring. </param>
public sealed partial class MessageOutbox(
	IOutboxStore outboxStore,
	IOutboxProcessor outboxProcessor,
	IJsonSerializer serializer,
	IOptions<OutboxOptions> options,
	ILogger<MessageOutbox> logger) : IOutboxDispatcher, IDisposable
{
	/// <summary>
	/// Maximum time to wait between polling cycles when no signal is received.
	/// Acts as a fallback to ensure messages are eventually processed even if signaling fails.
	/// </summary>
	private static readonly TimeSpan MaxWaitInterval = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Semaphore used for event-driven signaling when new messages are added to the outbox.
	/// This replaces fixed polling intervals with immediate notification for better latency and reduced CPU usage.
	/// </summary>
	private readonly SemaphoreSlim _messageSignal = new(0, int.MaxValue);

	private readonly OutboxOptions _options = options.Value;

	/// <summary>
	/// Runs the outbox dispatch loop, continuously processing and publishing pending messages to message brokers. This method implements
	/// the core outbox processing logic with configurable polling intervals, error handling, and graceful shutdown support for reliable
	/// message delivery.
	/// </summary>
	/// <param name="dispatcherId"> Unique identifier for this dispatcher instance, used for message ownership and coordination. </param>
	/// <param name="cancellationToken"> Cancellation token to support graceful shutdown and timeout scenarios. </param>
	/// <returns> Task containing the total number of messages processed during the dispatch session. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when dispatcherId is null or empty. </exception>
	/// <exception cref="InvalidOperationException"> Thrown when the outbox processor cannot be initialized. </exception>
	public async Task<int> RunOutboxDispatchAsync(string dispatcherId, CancellationToken cancellationToken)
	{
		LogOutboxStarted();

		outboxProcessor.Init(dispatcherId);

		var processed = 0;

		while (!cancellationToken.IsCancellationRequested)
		{
			try
			{
				processed += await outboxProcessor.DispatchPendingMessagesAsync(cancellationToken).ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				LogOutboxError(ex);
			}

			// Event-driven wait: blocks until signaled or timeout
			// This reduces CPU usage compared to fixed polling while maintaining responsiveness
			try
			{
				_ = await _messageSignal.WaitAsync(MaxWaitInterval, cancellationToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				// Normal shutdown - exit the loop
				break;
			}
		}

		LogOutboxStopped();

		return processed;
	}

	/// <summary>
	/// Signals that new messages have been added to the outbox, waking up the dispatch loop immediately.
	/// This enables event-driven processing instead of relying solely on polling intervals.
	/// </summary>
	public void SignalNewMessage()
	{
		// Release the semaphore to wake up the waiting dispatch loop
		// If multiple signals arrive before the loop processes, they will queue up
		try
		{
			_ = _messageSignal.Release();
		}
		catch (SemaphoreFullException)
		{
			// Semaphore is at max count - the dispatch loop will process anyway
		}
	}

	/// <summary>
	/// Saves integration events to the outbox for deferred publishing with transactional guarantees. This method serializes events and
	/// metadata, applies TTL policies, and stores them for later processing by the outbox dispatcher, ensuring reliable message delivery
	/// with at-least-once semantics.
	/// </summary>
	/// <param name="integrationEvents"> Collection of integration events to save for deferred publishing. </param>
	/// <param name="metadata"> Message metadata containing routing, correlation, and processing information. </param>
	/// <param name="cancellationToken"> Cancellation token for operation timeout and shutdown support. </param>
	/// <returns> Task representing the asynchronous event saving operation. </returns>
	/// <exception cref="ArgumentNullException"> Thrown when integrationEvents is null. </exception>
	/// <exception cref="Exceptions.SerializationException"> Thrown when event or metadata serialization fails. </exception>
	[UnconditionalSuppressMessage(
		"AOT",
		"IL3050:Using RequiresDynamicCode member in AOT",
		Justification = "Outbox serialization uses runtime type metadata for dispatch payloads.")]
	[UnconditionalSuppressMessage(
		"Trimming",
		"IL2026:Members annotated with RequiresUnreferencedCode may break with trimming",
		Justification = "Outbox serialization relies on runtime type discovery.")]
	public async Task SaveEventsAsync(
		IReadOnlyCollection<IIntegrationEvent> integrationEvents,
		IMessageMetadata metadata,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(integrationEvents);

		if (integrationEvents.Count == 0)
		{
			return;
		}

		var created = DateTimeOffset.UtcNow;
		var expires = _options.DefaultMessageTimeToLive.HasValue
			? created.Add(_options.DefaultMessageTimeToLive.Value)
			: null as DateTimeOffset?;

		var outboxMessages = integrationEvents.Select(evt => new OutboxMessage(
			messageId: Uuid7Extensions.GenerateString(),
			messageType: evt.GetType().FullName,
			messageMetadata: serializer.Serialize(metadata),
			messageBody: serializer.Serialize(evt),
			createdAt: created,
			expiresAt: expires)).ToArray();

		foreach (var outboxMessage in outboxMessages)
		{
			var outboundMessage = ConvertToOutboundMessage(outboxMessage);
			await outboxStore.StageMessageAsync(outboundMessage, cancellationToken).ConfigureAwait(false);
		}

		// Signal the dispatch loop that new messages are available
		SignalNewMessage();
	}

	/// <inheritdoc />
	[UnconditionalSuppressMessage(
		"AOT",
		"IL3050:Using RequiresDynamicCode member in AOT",
		Justification = "Outbox serialization uses runtime type metadata for dispatch payloads.")]
	[UnconditionalSuppressMessage(
		"Trimming",
		"IL2026:Members annotated with RequiresUnreferencedCode may break with trimming",
		Justification = "Outbox serialization relies on runtime type discovery.")]
	public async Task<int> SaveMessagesAsync(ICollection<IOutboxMessage> outboxMessages, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(outboxMessages);

		if (outboxMessages.Count == 0)
		{
			LogNoMessagesToSave();
			return 0;
		}

		var now = DateTimeOffset.UtcNow;
		var ttl = _options.DefaultMessageTimeToLive;

		foreach (var message in outboxMessages.Where(m => m.ExpiresAt is null && ttl.HasValue))
		{
			message.ExpiresAt = DateTimeOffset.UtcNow.Add(ttl!.Value);
		}

		var count = 0;
		foreach (var message in outboxMessages)
		{
			var outboundMessage = ConvertIOutboxMessageToOutboundMessage(message);
			await outboxStore.StageMessageAsync(outboundMessage, cancellationToken).ConfigureAwait(false);
			count++;
		}

		// Signal the dispatch loop that new messages are available
		if (count > 0)
		{
			SignalNewMessage();
		}

		return count;
	}

	/// <inheritdoc />
	[UnconditionalSuppressMessage(
		"AOT",
		"IL3050:Using RequiresDynamicCode member in AOT",
		Justification = "Outbox message retrieval uses runtime deserialization to restore message payloads.")]
	[UnconditionalSuppressMessage(
		"Trimming",
		"IL2026:Members annotated with RequiresUnreferencedCode may break with trimming",
		Justification = "Outbox message retrieval uses Type.GetType and JSON deserialization.")]
	public async Task<IEnumerable<IDispatchMessage>> GetPendingMessagesAsync(CancellationToken cancellationToken)
	{
		// Get pending messages from the outbox store
		var pendingMessages = await outboxStore.GetUnsentMessagesAsync(
			_options.ProducerBatchSize,
			cancellationToken).ConfigureAwait(false);

		// Convert IOutboxMessage to IDispatchMessage
		var dispatchMessages = new List<IDispatchMessage>();

		foreach (var message in pendingMessages)
		{
			try
			{
				if (ResolveMessageType(message.MessageType) is { } messageType)
				{
					var payloadString = Encoding.UTF8.GetString(message.Payload);
					var deserializedMessage = await serializer.DeserializeAsync(payloadString, messageType).ConfigureAwait(false);
					if (deserializedMessage is IDispatchMessage dispatchMessage)
					{
						dispatchMessages.Add(dispatchMessage);
					}
				}
				else
				{
					LogCouldNotResolveMessageType(message.MessageType, message.Id);
				}
			}
			catch (Exception ex)
			{
				LogFailedToDeserializeMessage(message.Id, message.MessageType, ex);
			}
		}

		return dispatchMessages;
	}

	private static Type? ResolveMessageType(string typeName)
	{
		if (string.IsNullOrWhiteSpace(typeName))
		{
			return null;
		}

		var lookupName = typeName;
		var separatorIndex = typeName.IndexOf(',', StringComparison.Ordinal);
		if (separatorIndex > 0)
		{
			lookupName = typeName[..separatorIndex].Trim();
		}

		foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
		{
			try
			{
				var resolved = assembly.GetType(typeName, throwOnError: false, ignoreCase: false)
					?? assembly.GetType(lookupName, throwOnError: false, ignoreCase: false);
				if (resolved != null)
				{
					return resolved;
				}
			}
			catch (ReflectionTypeLoadException)
			{
				// Ignore assemblies that cannot be inspected and continue search.
			}
		}

		return null;
	}

	/// <summary>
	/// Performs asynchronous cleanup of outbox resources including processor disposal and connection cleanup. This method ensures proper
	/// resource cleanup and graceful shutdown of the outbox processing infrastructure.
	/// </summary>
	/// <returns> ValueTask representing the asynchronous disposal operation. </returns>
	public ValueTask DisposeAsync()
	{
		_messageSignal.Dispose();

		if (outboxProcessor is IAsyncDisposable asyncDisp)
		{
			return asyncDisp.DisposeAsync();
		}

		return ValueTask.CompletedTask;
	}

	/// <summary>
	/// Performs synchronous cleanup of outbox resources including the message signal semaphore.
	/// </summary>
	public void Dispose()
	{
		_messageSignal.Dispose();

		if (outboxProcessor is IDisposable disp)
		{
			disp.Dispose();
		}
	}

	/// <summary>
	/// Converts an OutboxMessage to OutboundMessage format.
	/// </summary>
	/// <param name="outboxMessage"> The outbox message to convert. </param>
	/// <returns> An OutboundMessage instance. </returns>
	[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Deserialize<TValue>(String, JsonSerializerOptions)")]
	[RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Deserialize<TValue>(String, JsonSerializerOptions)")]
	private static OutboundMessage ConvertToOutboundMessage(OutboxMessage outboxMessage)
	{
		var headers = string.IsNullOrEmpty(outboxMessage.MessageMetadata)
			? []
			: JsonSerializer.Deserialize<Dictionary<string, object>>(outboxMessage.MessageMetadata) ?? [];

		return new OutboundMessage(
			messageType: outboxMessage.MessageType,
			payload: Encoding.UTF8.GetBytes(outboxMessage.MessageBody),
			destination: "default", // OutboxMessage doesn't have destination
			headers: headers)
		{
			Id = outboxMessage.MessageId,
			CreatedAt = outboxMessage.CreatedAt,
			ScheduledAt = outboxMessage.ExpiresAt,
			RetryCount = outboxMessage.Attempts,
			Status = OutboxStatus.Staged,
		};
	}

	/// <summary>
	/// Converts an IOutboxMessage to OutboundMessage format.
	/// </summary>
	/// <param name="outboxMessage"> The outbox message to convert. </param>
	/// <returns> An OutboundMessage instance. </returns>
	[RequiresUnreferencedCode("Calls System.Text.Json.JsonSerializer.Deserialize<TValue>(String, JsonSerializerOptions)")]
	[RequiresDynamicCode("Calls System.Text.Json.JsonSerializer.Deserialize<TValue>(String, JsonSerializerOptions)")]
	private static OutboundMessage ConvertIOutboxMessageToOutboundMessage(IOutboxMessage outboxMessage)
	{
		var headers = string.IsNullOrEmpty(outboxMessage.MessageMetadata)
			? []
			: JsonSerializer.Deserialize<Dictionary<string, object>>(outboxMessage.MessageMetadata) ?? [];

		return new OutboundMessage(
			messageType: outboxMessage.MessageType,
			payload: Encoding.UTF8.GetBytes(outboxMessage.MessageBody),
			destination: "default", // IOutboxMessage doesn't have destination
			headers: headers)
		{
			Id = outboxMessage.MessageId,
			CreatedAt = outboxMessage.CreatedAt,
			ScheduledAt = outboxMessage.ExpiresAt,
			RetryCount = outboxMessage.Attempts,
			Status = OutboxStatus.Staged,
		};
	}

	// Source-generated logging methods
	[LoggerMessage(OutboxEventId.MessageOutboxStarted, LogLevel.Information, "Outbox started.")]
	private partial void LogOutboxStarted();

	[LoggerMessage(OutboxEventId.MessageOutboxError, LogLevel.Error, "Error while processing Outbox.")]
	private partial void LogOutboxError(Exception ex);

	[LoggerMessage(OutboxEventId.MessageOutboxStopped, LogLevel.Information, "Outbox stopped.")]
	private partial void LogOutboxStopped();

	[LoggerMessage(OutboxEventId.NoMessagesToSave, LogLevel.Debug, "No messages to save to the outbox.")]
	private partial void LogNoMessagesToSave();

	[LoggerMessage(OutboxEventId.CouldNotResolveMessageType, LogLevel.Warning,
		"Could not resolve message type {MessageType} for message {MessageId}")]
	private partial void LogCouldNotResolveMessageType(string messageType, string messageId);

	[LoggerMessage(OutboxEventId.FailedToDeserializeMessage, LogLevel.Warning,
		"Failed to deserialize message {MessageId} of type {MessageType}")]
	private partial void LogFailedToDeserializeMessage(string messageId, string messageType, Exception ex);
}
