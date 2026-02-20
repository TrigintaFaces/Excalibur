// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Data.Abstractions;
using Excalibur.Data.Postgres.Diagnostics;
using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Diagnostics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Npgsql;

using OutboxMessage = Excalibur.Dispatch.Delivery.OutboxMessage;

namespace Excalibur.Data.Postgres.Outbox;

/// <summary>
/// Postgres implementation of the outbox store for message persistence and processing.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="PostgresOutboxStore" /> class. </remarks>
/// <param name="db"> Database connection interface. </param>
/// <param name="options"> Configuration options for the Postgres outbox store. </param>
/// <param name="logger"> Logger for diagnostic output. </param>
/// <param name="metrics"> Metrics collector for performance monitoring. </param>
public sealed partial class PostgresOutboxStore(
	IDb db,
	IOptions<PostgresOutboxStoreOptions> options,
	ILogger<PostgresOutboxStore> logger,
	PostgresOutboxStoreMetrics? metrics = null) : IOutboxStore, IOutboxStoreAdmin, IDisposable
{
	private readonly IDb _db = db ?? throw new ArgumentNullException(nameof(db));
	private readonly PostgresOutboxStoreOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
	private readonly ILogger<PostgresOutboxStore> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
	private readonly PostgresOutboxStoreMetrics _metrics = metrics ?? new PostgresOutboxStoreMetrics();
	private volatile bool _disposed;

	/// <summary>
	/// Saves multiple outbox messages to the database.
	/// </summary>
	/// <param name="outboxMessages"> Collection of outbox messages to save. </param>
	/// <param name="cancellationToken"> Cancellation token for the operation. </param>
	/// <returns> Number of messages successfully saved. </returns>
	public async Task<int> SaveMessagesAsync(ICollection<IOutboxMessage> outboxMessages, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(outboxMessages);

		if (outboxMessages.Count == 0)
		{
			return 0;
		}

		LogSaveMessages(outboxMessages.Count);

		var stopwatch = ValueStopwatch.StartNew();
		try
		{
			// Process messages individually for transaction isolation
			foreach (var message in outboxMessages)
			{
				try
				{
					var req = new InsertOutboxMessage(
						message.MessageId,
						message.MessageType,
						message.MessageMetadata,
						message.MessageBody,
						_options.QualifiedOutboxTableName,
						DbTimeouts.RegularTimeoutSeconds,
						cancellationToken);

					_ = await _db.Connection.ResolveAsync(req).ConfigureAwait(false);
				}
				catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
				{
					throw new InvalidOperationException(
						$"Outbox message '{message.MessageId}' already exists.",
						ex);
				}
			}

			return outboxMessages.Count;
		}
		finally
		{
			var durationMs = stopwatch.Elapsed.TotalMilliseconds;
			_metrics.RecordSaveMessages(durationMs, outboxMessages.Count);
			LogOperationCompleted(durationMs, "SaveMessages");
		}
	}

	/// <summary>
	/// Releases reservation on outbox messages for a specific dispatcher.
	/// </summary>
	/// <param name="dispatcherId"> Identifier of the dispatcher to unreserve messages for. </param>
	/// <param name="cancellationToken"> Cancellation token for the operation. </param>
	/// <returns> Number of messages unreserved. </returns>
	public async Task<int> UnReserveOutboxMessagesAsync(string dispatcherId, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(dispatcherId);

		LogUnreserveMessages(dispatcherId);

		var stopwatch = ValueStopwatch.StartNew();
		try
		{
			var req = new ResetOutboxMessageReservation(
				dispatcherId,
				_options.QualifiedOutboxTableName,
				DbTimeouts.RegularTimeoutSeconds,
				cancellationToken);

			var result = await _db.Connection.ResolveAsync(req).ConfigureAwait(false);
			return result;
		}
		finally
		{
			var durationMs = stopwatch.Elapsed.TotalMilliseconds;
			_metrics.RecordUnreserveMessages(durationMs, 0); // Count not available from operation
			LogOperationCompleted(durationMs, "UnReserveMessages");
		}
	}

	/// <summary>
	/// Reserves a batch of outbox messages for processing by a specific dispatcher.
	/// </summary>
	/// <param name="dispatcherId"> Identifier of the dispatcher reserving messages. </param>
	/// <param name="batchSize"> Maximum number of messages to reserve. </param>
	/// <param name="cancellationToken"> Cancellation token for the operation. </param>
	/// <returns> Collection of reserved outbox messages. </returns>
	public async Task<IEnumerable<IOutboxMessage>> ReserveOutboxMessagesAsync(
		string dispatcherId,
		int batchSize,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(dispatcherId);
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchSize);

		LogReserveMessages(dispatcherId, batchSize);

		var stopwatch = ValueStopwatch.StartNew();
		try
		{
			var req = new ReserveOutboxMessages(
				dispatcherId,
				batchSize,
				_options.ReservationTimeout,
				_options.QualifiedOutboxTableName,
				DbTimeouts.RegularTimeoutSeconds,
				cancellationToken);

			var result = await _db.Connection.ResolveAsync(req).ConfigureAwait(false);
			return result;
		}
		finally
		{
			var durationMs = stopwatch.Elapsed.TotalMilliseconds;
			_metrics.RecordReserveMessages(durationMs, 0); // Count determined post-execution
			LogOperationCompleted(durationMs, "ReserveMessages");
		}
	}

	/// <summary>
	/// Deletes a specific outbox record by message ID.
	/// </summary>
	/// <param name="messageId"> Unique identifier of the message to delete. </param>
	/// <param name="cancellationToken"> Cancellation token for the operation. </param>
	/// <returns> Number of records deleted (0 or 1). </returns>
	public async Task<int> DeleteOutboxRecord(string messageId, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);

		LogDeleteRecord(messageId);

		var stopwatch = ValueStopwatch.StartNew();
		try
		{
			var req = new DeleteOutboxMessage(
				messageId,
				_options.QualifiedOutboxTableName,
				DbTimeouts.RegularTimeoutSeconds,
				cancellationToken);

			return await _db.Connection.ResolveAsync(req).ConfigureAwait(false);
		}
		finally
		{
			var durationMs = stopwatch.Elapsed.TotalMilliseconds;
			_metrics.RecordDeleteRecord(durationMs);
			LogOperationCompleted(durationMs, "DeleteRecord");
		}
	}

	/// <summary>
	/// Increments the attempt count for a specific outbox message.
	/// </summary>
	/// <param name="messageId"> Unique identifier of the message to update. </param>
	/// <param name="cancellationToken"> Cancellation token for the operation. </param>
	/// <returns> Number of records updated (0 or 1). </returns>
	public async Task<int> IncreaseAttempts(string messageId, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);

		LogIncreaseAttempts(messageId);

		var stopwatch = ValueStopwatch.StartNew();
		try
		{
			var req = new IncrementOutboxMessageAttempts(
				messageId,
				_options.QualifiedOutboxTableName,
				DbTimeouts.RegularTimeoutSeconds,
				cancellationToken);

			return await _db.Connection.ResolveAsync(req).ConfigureAwait(false);
		}
		finally
		{
			var durationMs = stopwatch.Elapsed.TotalMilliseconds;
			_metrics.RecordIncreaseAttempts(durationMs);
			LogOperationCompleted(durationMs, "IncreaseAttempts");
		}
	}

	/// <summary>
	/// Moves a specific outbox message to the dead letter table.
	/// </summary>
	/// <param name="messageId"> Unique identifier of the message to move. </param>
	/// <param name="cancellationToken"> Cancellation token for the operation. </param>
	/// <returns> Number of records moved (0 or 1). </returns>
	public async Task<int> MoveToDeadLetter(string messageId, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);

		LogMoveToDeadLetter(messageId);

		var stopwatch = ValueStopwatch.StartNew();
		try
		{
			var req = new MoveOutboxMessageToDeadLetter(
				messageId,
				_options.QualifiedOutboxTableName,
				_options.QualifiedDeadLetterTableName,
				DbTimeouts.RegularTimeoutSeconds,
				cancellationToken);

			return await _db.Connection.ResolveAsync(req).ConfigureAwait(false);
		}
		finally
		{
			var durationMs = stopwatch.Elapsed.TotalMilliseconds;
			_metrics.RecordMoveToDeadLetter(durationMs);
			LogOperationCompleted(durationMs, "MoveToDeadLetter");
		}
	}

	/// <summary>
	/// Deletes multiple outbox records in a batch operation.
	/// </summary>
	/// <param name="messageIds"> Collection of message IDs to delete. </param>
	/// <param name="cancellationToken"> Cancellation token for the operation. </param>
	/// <returns> Total number of records deleted. </returns>
	public async Task<int> DeleteOutboxRecordsBatchAsync(IEnumerable<string> messageIds, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(messageIds);

		var messageIdList = messageIds.ToList();
		if (messageIdList.Count == 0)
		{
			return 0;
		}

		LogBatchOperation(messageIdList.Count);

		var stopwatch = ValueStopwatch.StartNew();
		var totalDeleted = 0;

		try
		{
			// Process each deletion individually to maintain transaction integrity
			foreach (var messageId in messageIdList)
			{
				totalDeleted += await DeleteOutboxRecord(messageId, cancellationToken).ConfigureAwait(false);
			}

			return totalDeleted;
		}
		finally
		{
			var durationMs = stopwatch.Elapsed.TotalMilliseconds;
			_metrics.RecordBatchDelete(durationMs, totalDeleted);
			LogOperationCompleted(durationMs, "BatchDelete");
		}
	}

	/// <summary>
	/// Increases attempt counts for multiple outbox messages in a batch operation.
	/// </summary>
	/// <param name="messageIds"> Collection of message IDs to update. </param>
	/// <param name="cancellationToken"> Cancellation token for the operation. </param>
	/// <returns> Total number of records updated. </returns>
	public async Task<int> IncreaseAttemptsBatchAsync(IEnumerable<string> messageIds, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(messageIds);

		var messageIdList = messageIds.ToList();
		if (messageIdList.Count == 0)
		{
			return 0;
		}

		LogBatchOperation(messageIdList.Count);

		var stopwatch = ValueStopwatch.StartNew();
		var totalUpdated = 0;

		try
		{
			// Process each update individually to maintain transaction integrity
			foreach (var messageId in messageIdList)
			{
				totalUpdated += await IncreaseAttempts(messageId, cancellationToken).ConfigureAwait(false);
			}

			return totalUpdated;
		}
		finally
		{
			var durationMs = stopwatch.Elapsed.TotalMilliseconds;
			_metrics.RecordBatchIncreaseAttempts(durationMs, totalUpdated);
			LogOperationCompleted(durationMs, "BatchIncreaseAttempts");
		}
	}

	/// <summary>
	/// Moves multiple outbox messages to the dead letter table in a batch operation.
	/// </summary>
	/// <param name="messageIds"> Collection of message IDs to move. </param>
	/// <param name="cancellationToken"> Cancellation token for the operation. </param>
	/// <returns> Total number of records moved. </returns>
	public async Task<int> MoveToDeadLetterBatchAsync(IEnumerable<string> messageIds, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(messageIds);

		var messageIdList = messageIds.ToList();
		if (messageIdList.Count == 0)
		{
			return 0;
		}

		LogBatchOperation(messageIdList.Count);

		var stopwatch = ValueStopwatch.StartNew();
		var totalMoved = 0;

		try
		{
			// Process each move individually to maintain transaction integrity
			foreach (var messageId in messageIdList)
			{
				totalMoved += await MoveToDeadLetter(messageId, cancellationToken).ConfigureAwait(false);
			}

			return totalMoved;
		}
		finally
		{
			var durationMs = stopwatch.Elapsed.TotalMilliseconds;
			_metrics.RecordBatchMoveToDeadLetter(durationMs, totalMoved);
			LogOperationCompleted(durationMs, "BatchMoveToDeadLetter");
		}
	}

	/// <summary>
	/// Stages a message for outbound delivery using the standard IOutboxStore interface.
	/// </summary>
	/// <param name="message"> Message to stage for delivery. </param>
	/// <param name="cancellationToken"> Cancellation token for the operation. </param>
	/// <returns> A task representing the asynchronous operation. </returns>
	public async ValueTask StageMessageAsync(OutboundMessage message, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);

		// Convert OutboundMessage to IOutboxMessage for the existing Postgres implementation
		var outboxMessage = new OutboxMessage(
			message.Id,
			message.MessageType,
			System.Text.Json.JsonSerializer.Serialize(message.Headers ?? new Dictionary<string, object>(StringComparer.Ordinal)),
			System.Text.Encoding.UTF8.GetString(message.Payload),
			DateTimeOffset.UtcNow);

		_ = await SaveMessagesAsync(new[] { outboxMessage }, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Enqueues a dispatch message for outbound delivery using the standard IOutboxStore interface.
	/// </summary>
	/// <param name="message"> Dispatch message to enqueue. </param>
	/// <param name="context"> Message context with metadata. </param>
	/// <param name="cancellationToken"> Cancellation token for the operation. </param>
	/// <returns> A task representing the asynchronous operation. </returns>
	[RequiresUnreferencedCode(
		"JSON serialization of message payload and metadata may reference types not preserved during trimming. Ensure all serialized types are annotated with DynamicallyAccessedMembers.")]
	[RequiresDynamicCode(
		"JSON serialization of message payload and metadata requires dynamic code generation for reflection-based property access and value conversion.")]
	public async ValueTask EnqueueAsync(IDispatchMessage message, IMessageContext context, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(context);

		// Create OutboundMessage from IDispatchMessage and context
		var serializedPayload = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(message);
		var headers = new Dictionary<string, object>
			(StringComparer.Ordinal)
		{
			["MessageId"] = context.MessageId ?? string.Empty,
			["CorrelationId"] = context.CorrelationId ?? string.Empty,
			["CausationId"] = context.CausationId ?? string.Empty,
			["MessageType"] = message.GetType().Name,
			["Timestamp"] = DateTimeOffset.UtcNow.ToString("O"),
		};

		var outboundMessage = new OutboundMessage(
			message.GetType().Name,
			serializedPayload,
			"default", // Destination - could be enhanced to use context metadata
			headers)
		{ Id = context.MessageId ?? string.Empty };

		await StageMessageAsync(outboundMessage, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Gets unsent messages from the outbox using the standard IOutboxStore interface.
	/// </summary>
	/// <param name="batchSize"> Maximum number of messages to retrieve. </param>
	/// <param name="cancellationToken"> Cancellation token for the operation. </param>
	/// <returns> Collection of unsent outbound messages. </returns>
	[RequiresUnreferencedCode(
		"JSON deserialization of message payload and metadata may reference types not preserved during trimming. Ensure all deserialized types are annotated with DynamicallyAccessedMembers.")]
	[RequiresDynamicCode(
		"JSON deserialization of message payload and metadata requires dynamic code generation for reflection-based type construction and property setting.")]
	public async ValueTask<IEnumerable<OutboundMessage>> GetUnsentMessagesAsync(
		int batchSize,
		CancellationToken cancellationToken)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchSize);

		// Use a fixed dispatcher ID for this interface method - in practice this would need proper session management
		var dispatcherId = $"dispatcher-{Environment.MachineName}-{Environment.ProcessId}";
		var reservedMessages = await ReserveOutboxMessagesAsync(dispatcherId, batchSize, cancellationToken).ConfigureAwait(false);

		// Convert IOutboxMessage to OutboundMessage
		var outboundMessages = new List<OutboundMessage>();
		foreach (var msg in reservedMessages)
		{
			try
			{
				var headers = string.IsNullOrEmpty(msg.MessageMetadata)
					? []
					: System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(msg.MessageMetadata) ??
					  [];

				var outboundMessage = new OutboundMessage(
					msg.MessageType,
					System.Text.Encoding.UTF8.GetBytes(msg.MessageBody),
					"default", // Destination - would need to be stored in metadata
					headers)
				{ Id = msg.MessageId };

				outboundMessages.Add(outboundMessage);
			}
			catch (Exception ex)
			{
				LogConvertMessageFailed(msg.MessageId, ex);
			}
		}

		return outboundMessages;
	}

	/// <summary>
	/// Marks a message as sent using the standard IOutboxStore interface.
	/// </summary>
	/// <param name="messageId"> Unique identifier of the message to mark as sent. </param>
	/// <param name="cancellationToken"> Cancellation token for the operation. </param>
	/// <returns> A task representing the asynchronous operation. </returns>
	public async ValueTask MarkSentAsync(string messageId, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);

		// For Postgres implementation, we delete the message when it's sent
		var deleted = await DeleteOutboxRecord(messageId, cancellationToken).ConfigureAwait(false);

		if (deleted == 0)
		{
			throw new InvalidOperationException($"Message {messageId} not found or already sent.");
		}
	}

	/// <summary>
	/// Marks a message as failed using the standard IOutboxStore interface.
	/// </summary>
	/// <param name="messageId"> Unique identifier of the message to mark as failed. </param>
	/// <param name="errorMessage"> Error message describing the failure. </param>
	/// <param name="retryCount"> Current retry count for the message. </param>
	/// <param name="cancellationToken"> Cancellation token for the operation. </param>
	/// <returns> A task representing the asynchronous operation. </returns>
	public async ValueTask MarkFailedAsync(string messageId, string errorMessage, int retryCount, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);
		ArgumentOutOfRangeException.ThrowIfNegative(retryCount);

		// Increase attempts first
		_ = await IncreaseAttempts(messageId, cancellationToken).ConfigureAwait(false);

		// If max retries exceeded, move to dead letter
		if (retryCount >= _options.MaxAttempts)
		{
			_ = await MoveToDeadLetter(messageId, cancellationToken).ConfigureAwait(false);
		}

		// Otherwise, unreserve so it can be retried
		// Note: This is a simplified implementation - in practice you'd need better session management
	}

	/// <summary>
	/// Gets failed messages using the standard IOutboxStore interface.
	/// </summary>
	/// <param name="maxRetries"> Maximum number of retries to filter by. </param>
	/// <param name="olderThan"> Optional timestamp to filter messages older than this time. </param>
	/// <param name="batchSize"> Maximum number of messages to retrieve. </param>
	/// <param name="cancellationToken"> Cancellation token for the operation. </param>
	/// <returns> Collection of failed outbound messages. </returns>
	public async ValueTask<IEnumerable<OutboundMessage>> GetFailedMessagesAsync(int maxRetries, DateTimeOffset? olderThan,
		int batchSize, CancellationToken cancellationToken)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);

		LogGetFailedMessages(maxRetries, batchSize);

		var stopwatch = ValueStopwatch.StartNew();
		try
		{
			var req = new GetDeadLetterMessages(
				_options.QualifiedDeadLetterTableName,
				maxRetries,
				olderThan,
				batchSize,
				offset: 0,
				DbTimeouts.RegularTimeoutSeconds,
				cancellationToken);

			var records = await _db.Connection.ResolveAsync(req).ConfigureAwait(false);

			var messages = records.Select(static r => new OutboundMessage
			{
				Id = r.MessageId,
				MessageType = r.MessageType,
				Payload = System.Text.Encoding.UTF8.GetBytes(r.MessageBody),
				CreatedAt = r.OccurredOn,
				RetryCount = r.Attempts,
				LastError = r.ErrorMessage,
				Status = OutboxStatus.Failed,
			}).ToList();

			return messages;
		}
		finally
		{
			var durationMs = stopwatch.Elapsed.TotalMilliseconds;
			_metrics.RecordSaveMessages(durationMs, batchSize);
			LogOperationCompleted(durationMs, "GetFailedMessages");
		}
	}

	/// <summary>
	/// Gets scheduled messages using the standard IOutboxStore interface.
	/// </summary>
	/// <param name="scheduledBefore"> Timestamp to filter messages scheduled before this time. </param>
	/// <param name="batchSize"> Maximum number of messages to retrieve. </param>
	/// <param name="cancellationToken"> Cancellation token for the operation. </param>
	/// <returns> Collection of scheduled outbound messages. </returns>
	public async ValueTask<IEnumerable<OutboundMessage>> GetScheduledMessagesAsync(DateTimeOffset scheduledBefore, int batchSize,
		CancellationToken cancellationToken)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(batchSize, 1);

		LogGetScheduledMessages(batchSize);

		var stopwatch = ValueStopwatch.StartNew();
		try
		{
			var req = new GetScheduledOutboxMessages(
				scheduledBefore,
				batchSize,
				_options.QualifiedOutboxTableName,
				DbTimeouts.RegularTimeoutSeconds,
				cancellationToken);

			var result = await _db.Connection.ResolveAsync(req).ConfigureAwait(false);
			return result;
		}
		finally
		{
			var durationMs = stopwatch.Elapsed.TotalMilliseconds;
			LogOperationCompleted(durationMs, "GetScheduledMessages");
		}
	}

	/// <summary>
	/// Schedules a message for future delivery at the specified time.
	/// </summary>
	/// <param name="message"> The outbox message to schedule. </param>
	/// <param name="scheduledAt"> The time at which the message should be delivered. </param>
	/// <param name="cancellationToken"> Cancellation token for the operation. </param>
	/// <returns> A task representing the asynchronous schedule operation. </returns>
	public async Task ScheduleMessageAsync(
		OutboxMessage message,
		DateTimeOffset scheduledAt,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);

		LogScheduleMessage(message.MessageId, scheduledAt);

		var stopwatch = ValueStopwatch.StartNew();
		try
		{
			var req = new ScheduleOutboxMessage(
				message.MessageId,
				message.MessageType,
				message.MessageMetadata,
				message.MessageBody,
				scheduledAt,
				_options.QualifiedOutboxTableName,
				DbTimeouts.RegularTimeoutSeconds,
				cancellationToken);

			_ = await _db.Connection.ResolveAsync(req).ConfigureAwait(false);
		}
		catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
		{
			throw new InvalidOperationException(
				$"Outbox message '{message.MessageId}' already exists.",
				ex);
		}
		finally
		{
			var durationMs = stopwatch.Elapsed.TotalMilliseconds;
			LogOperationCompleted(durationMs, "ScheduleMessage");
		}
	}

	/// <summary>
	/// Cleans up sent messages using the standard IOutboxStore interface.
	/// </summary>
	/// <param name="olderThan"> Timestamp to filter messages older than this time for cleanup. </param>
	/// <param name="batchSize"> Maximum number of messages to clean up in one operation. </param>
	/// <param name="cancellationToken"> Cancellation token for the operation. </param>
	/// <returns> Number of messages cleaned up. </returns>
	public ValueTask<int> CleanupSentMessagesAsync(DateTimeOffset olderThan, int batchSize, CancellationToken cancellationToken)
	{
		// Postgres implementation automatically deletes sent messages via MarkSentAsync So there are no sent messages to clean up in the
		// outbox table
		LogCleanupSentMessagesNotNeeded();
		return new ValueTask<int>(0);
	}

	/// <summary>
	/// Gets outbox statistics using actual database queries against the outbox and dead letter tables.
	/// </summary>
	/// <param name="cancellationToken"> Cancellation token for the operation. </param>
	/// <returns> Statistics about the outbox store including message counts and oldest pending age. </returns>
	public async ValueTask<OutboxStatistics> GetStatisticsAsync(CancellationToken cancellationToken)
	{
		LogGetStatistics();

		var stopwatch = ValueStopwatch.StartNew();
		try
		{
			var req = new GetOutboxStatistics(
				_options.QualifiedOutboxTableName,
				_options.QualifiedDeadLetterTableName,
				DbTimeouts.RegularTimeoutSeconds,
				cancellationToken);

			var statistics = await _db.Connection.ResolveAsync(req).ConfigureAwait(false);
			return statistics;
		}
		finally
		{
			var durationMs = stopwatch.Elapsed.TotalMilliseconds;
			LogOperationCompleted(durationMs, "GetStatistics");
		}
	}

	/// <summary>
	/// Disposes the outbox store and releases associated resources.
	/// </summary>
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_metrics?.Dispose();
		_disposed = true;
	}

	// Source-generated logging methods
	[LoggerMessage(DataPostgresEventId.OutboxSaveMessages, LogLevel.Debug,
		"Saving {MessageCount} outbox messages")]
	private partial void LogSaveMessages(int messageCount);

	[LoggerMessage(DataPostgresEventId.OutboxReserveMessages, LogLevel.Debug,
		"Reserving up to {BatchSize} outbox messages for dispatcher {DispatcherId}")]
	private partial void LogReserveMessages(string dispatcherId, int batchSize);

	[LoggerMessage(DataPostgresEventId.OutboxUnreserveMessages, LogLevel.Debug,
		"Unreserving outbox messages for dispatcher {DispatcherId}")]
	private partial void LogUnreserveMessages(string dispatcherId);

	[LoggerMessage(DataPostgresEventId.OutboxDeleteRecord, LogLevel.Debug,
		"Deleting outbox record {MessageId}")]
	private partial void LogDeleteRecord(string messageId);

	[LoggerMessage(DataPostgresEventId.OutboxIncreaseAttempts, LogLevel.Debug,
		"Increasing attempts for outbox message {MessageId}")]
	private partial void LogIncreaseAttempts(string messageId);

	[LoggerMessage(DataPostgresEventId.OutboxMoveToDeadLetter, LogLevel.Debug,
		"Moving outbox message {MessageId} to dead letter table")]
	private partial void LogMoveToDeadLetter(string messageId);

	[LoggerMessage(DataPostgresEventId.OutboxBatchOperation, LogLevel.Debug,
		"Processing batch operation for {MessageCount} messages")]
	private partial void LogBatchOperation(int messageCount);

	[LoggerMessage(DataPostgresEventId.OutboxOperationCompleted, LogLevel.Information,
		"Outbox operation {Operation} completed in {Duration:F2}ms")]
	private partial void LogOperationCompleted(double duration, string operation);

	[LoggerMessage(DataPostgresEventId.OutboxConvertMessageFailed, LogLevel.Warning,
		"Failed to convert reserved message {MessageId} to OutboundMessage")]
	private partial void LogConvertMessageFailed(string messageId, Exception ex);

	[LoggerMessage(DataPostgresEventId.OutboxGetFailedMessagesNotSupported, LogLevel.Debug,
		"Querying dead letter table for failed messages with maxRetries={MaxRetries}, batchSize={BatchSize}")]
	private partial void LogGetFailedMessages(int maxRetries, int batchSize);

	[LoggerMessage(DataPostgresEventId.OutboxGetScheduledMessagesNotSupported, LogLevel.Debug,
		"Retrieving scheduled outbox messages with batchSize={BatchSize}")]
	private partial void LogGetScheduledMessages(int batchSize);

	[LoggerMessage(DataPostgresEventId.OutboxCleanupSentMessagesNotNeeded, LogLevel.Debug,
		"CleanupSentMessagesAsync called but Postgres implementation automatically deletes sent messages")]
	private partial void LogCleanupSentMessagesNotNeeded();

	[LoggerMessage(DataPostgresEventId.OutboxGetStatisticsBasic, LogLevel.Debug,
		"Retrieving outbox statistics from database")]
	private partial void LogGetStatistics();

	[LoggerMessage(DataPostgresEventId.OutboxGetStatisticsBasic + 1, LogLevel.Debug,
		"Scheduling outbox message {MessageId} for delivery at {ScheduledAt}")]
	private partial void LogScheduleMessage(string messageId, DateTimeOffset scheduledAt);
}
