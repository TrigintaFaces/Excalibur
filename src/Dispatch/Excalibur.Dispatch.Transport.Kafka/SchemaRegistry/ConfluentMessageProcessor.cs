// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Kafka;

/// <summary>
/// Processes Confluent wire format messages and dispatches them via <see cref="IDispatcher"/>.
/// </summary>
/// <remarks>
/// <para>
/// This processor handles:
/// </para>
/// <list type="bullet">
///   <item><description>Deserialization from Confluent wire format</description></item>
///   <item><description>Schema type resolution via <see cref="ISchemaTypeResolver"/></description></item>
///   <item><description>Automatic upcasting for versioned messages (when enabled)</description></item>
///   <item><description>Dispatch to registered handlers via <see cref="IDispatcher"/></description></item>
///   <item><description>Configurable error handling strategies</description></item>
/// </list>
/// <para>
/// This is a processing component that integrates with existing Kafka consumer infrastructure,
/// rather than replacing it. It handles message processing but not offset management.
/// </para>
/// </remarks>
public sealed class ConfluentMessageProcessor
{
	private readonly IConfluentFormatDeserializer _deserializer;
	private readonly IDispatcher _dispatcher;
	private readonly ConfluentConsumerOptions _options;
	private readonly ILogger<ConfluentMessageProcessor> _logger;

	/// <summary>
	/// Initializes a new instance of the <see cref="ConfluentMessageProcessor"/> class.
	/// </summary>
	/// <param name="deserializer">The Confluent format deserializer.</param>
	/// <param name="dispatcher">The message dispatcher.</param>
	/// <param name="options">The consumer options.</param>
	/// <param name="logger">The logger.</param>
	public ConfluentMessageProcessor(
		IConfluentFormatDeserializer deserializer,
		IDispatcher dispatcher,
		IOptions<ConfluentConsumerOptions> options,
		ILogger<ConfluentMessageProcessor> logger)
	{
		_deserializer = deserializer ?? throw new ArgumentNullException(nameof(deserializer));
		_dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <summary>
	/// Processes a Confluent wire format message.
	/// </summary>
	/// <param name="topic">The Kafka topic the message was received from.</param>
	/// <param name="data">The raw message bytes in Confluent wire format.</param>
	/// <param name="context">The message context for dispatch.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>
	/// A result indicating whether processing was successful, skipped, or sent to dead letter.
	/// </returns>
	public async Task<ProcessingResult> ProcessAsync(
		string topic,
		ReadOnlyMemory<byte> data,
		IMessageContext context,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(topic);
		ArgumentNullException.ThrowIfNull(context);

		DeserializationResult result;

		try
		{
			result = await _deserializer.DeserializeAsync(topic, data, cancellationToken)
				.ConfigureAwait(false);
		}
		catch (Exception ex) when (ex is SchemaRegistryException or InvalidOperationException)
		{
			return await HandleDeserializationErrorAsync(topic, data, ex, cancellationToken)
				.ConfigureAwait(false);
		}

		_logger.LogDebug(
			"Processing message of type {MessageType} (V{Version}) from topic {Topic}",
			result.MessageType.Name,
			result.Version,
			topic);

		try
		{
			// Dispatch the message via the standard dispatcher
			if (result.Message is IDispatchMessage dispatchMessage)
			{
				_ = await _dispatcher.DispatchAsync(dispatchMessage, context, cancellationToken)
					.ConfigureAwait(false);
			}
			else
			{
				_logger.LogWarning(
					"Message of type {MessageType} does not implement IDispatchMessage, skipping dispatch",
					result.MessageType.Name);
			}

			return ProcessingResult.Success(result.SchemaId, result.MessageType, result.Version);
		}
		catch (Exception ex)
		{
			_logger.LogError(
				ex,
				"Handler failed for message {MessageType} (V{Version}) from topic {Topic}",
				result.MessageType.Name,
				result.Version,
				topic);

			throw;
		}
	}

	/// <summary>
	/// Processes a Confluent wire format message with a known expected type.
	/// </summary>
	/// <typeparam name="T">The expected message type.</typeparam>
	/// <param name="topic">The Kafka topic the message was received from.</param>
	/// <param name="data">The raw message bytes in Confluent wire format.</param>
	/// <param name="context">The message context for dispatch.</param>
	/// <param name="cancellationToken">A token to cancel the operation.</param>
	/// <returns>The deserialized and optionally upcasted message.</returns>
	public async Task<T> ProcessAsync<T>(
		string topic,
		ReadOnlyMemory<byte> data,
		IMessageContext context,
		CancellationToken cancellationToken)
		where T : IDispatchMessage
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(topic);
		ArgumentNullException.ThrowIfNull(context);

		var message = await _deserializer.DeserializeAsync<T>(topic, data, cancellationToken)
			.ConfigureAwait(false);

		_logger.LogDebug(
			"Processing typed message {MessageType} from topic {Topic}",
			typeof(T).Name,
			topic);

		_ = await _dispatcher.DispatchAsync(message, context, cancellationToken)
			.ConfigureAwait(false);

		return message;
	}

	private Task<ProcessingResult> HandleDeserializationErrorAsync(
		string topic,
		ReadOnlyMemory<byte> _,
		Exception exception,
		CancellationToken __)
	{
		switch (_options.ErrorHandling)
		{
			case DeserializationErrorHandling.Skip:
				_logger.LogWarning(
					exception,
					"Skipping message from topic {Topic} due to deserialization error",
					topic);
				return Task.FromResult(ProcessingResult.Skipped(exception.Message));

			case DeserializationErrorHandling.DeadLetter:
				var dlqTopic = _options.DeadLetterTopic ?? $"{topic}.DLQ";
				_logger.LogWarning(
					exception,
					"Sending message from topic {Topic} to dead letter queue {DlqTopic}",
					topic,
					dlqTopic);

				// Note: Actual DLQ publishing would require IMessageBus injection
				// For now, we log the intent and return the result
				// The caller is responsible for DLQ publishing based on this result
				return Task.FromResult(ProcessingResult.DeadLettered(dlqTopic, exception.Message));

			case DeserializationErrorHandling.Throw:
			default:
				_logger.LogError(
					exception,
					"Deserialization failed for message from topic {Topic}, throwing",
					topic);
				throw new SchemaRegistryException(
					$"Deserialization failed for message from topic {topic}: {exception.Message}",
					exception);
		}
	}
}

/// <summary>
/// Represents the result of message processing.
/// </summary>
public sealed class ProcessingResult
{
	private ProcessingResult(
		ProcessingStatus status,
		int schemaId,
		Type? messageType,
		int version,
		string? deadLetterTopic,
		string? reason)
	{
		Status = status;
		SchemaId = schemaId;
		MessageType = messageType;
		Version = version;
		DeadLetterTopic = deadLetterTopic;
		Reason = reason;
	}

	/// <summary>
	/// Gets the processing status.
	/// </summary>
	public ProcessingStatus Status { get; }

	/// <summary>
	/// Gets the schema ID (for successful processing).
	/// </summary>
	public int SchemaId { get; }

	/// <summary>
	/// Gets the message type (for successful processing).
	/// </summary>
	public Type? MessageType { get; }

	/// <summary>
	/// Gets the message version (for successful processing).
	/// </summary>
	public int Version { get; }

	/// <summary>
	/// Gets the dead letter topic (for dead-lettered messages).
	/// </summary>
	public string? DeadLetterTopic { get; }

	/// <summary>
	/// Gets the reason for skipping or dead-lettering.
	/// </summary>
	public string? Reason { get; }

	/// <summary>
	/// Gets a value indicating whether processing was successful.
	/// </summary>
	public bool IsSuccess => Status == ProcessingStatus.Success;

	/// <summary>
	/// Creates a successful processing result.
	/// </summary>
	public static ProcessingResult Success(int schemaId, Type messageType, int version)
	{
		return new ProcessingResult(ProcessingStatus.Success, schemaId, messageType, version, null, null);
	}

	/// <summary>
	/// Creates a skipped processing result.
	/// </summary>
	public static ProcessingResult Skipped(string reason)
	{
		return new ProcessingResult(ProcessingStatus.Skipped, 0, null, 0, null, reason);
	}

	/// <summary>
	/// Creates a dead-lettered processing result.
	/// </summary>
	public static ProcessingResult DeadLettered(string dlqTopic, string reason)
	{
		return new ProcessingResult(ProcessingStatus.DeadLettered, 0, null, 0, dlqTopic, reason);
	}
}

/// <summary>
/// The status of message processing.
/// </summary>
public enum ProcessingStatus
{
	/// <summary>
	/// Message was processed successfully.
	/// </summary>
	Success = 0,

	/// <summary>
	/// Message was skipped due to an error.
	/// </summary>
	Skipped = 1,

	/// <summary>
	/// Message was sent to dead letter queue.
	/// </summary>
	DeadLettered = 2
}
