// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Dapper;

using Excalibur.Data;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Diagnostics;
using Excalibur.Dispatch.Metadata;
using Excalibur.Outbox.Postgres.Diagnostics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Npgsql;

using OutboxMessage = Excalibur.Outbox.OutboxMessage;

namespace Excalibur.Outbox.Postgres;

/// <summary>
/// Postgres implementation of the outbox store for message persistence and processing.
/// </summary>
/// <remarks>
/// Tenant isolation is enforced on the write/stage path (each message carries and persists its own
/// <c>tenant_id</c>) and on tenant-facing queries; the drain/mark-by-Id path (mark-sent, increment-attempts,
/// backoff, dead-letter, delete) is cross-tenant infrastructure and stays global (it addresses the
/// globally-unique outbox <c>message_id</c>). The store reads no ambient tenant context — fail-closed enforcement
/// is provided by the multi-tenancy composition.
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Maintainability", "CA1506:Avoid excessive class coupling",
	Justification = "Store class coordinates the Dapper request set, outbox message mapping, tenant scoping, and leadership-fencing control-table CAS by design (parity with SqlServerOutboxStore).")]
public sealed partial class PostgresOutboxStore : IOutboxStore, IFencedOutboxStore, IOutboxStoreCapabilities, IOutboxStoreAdmin, IDeadLetterableOutboxStore, IBackoffSchedulableOutboxStore, ITransactionalOutboxWriter, IDisposable
{
	private readonly IDb _db;

	/// <inheritdoc />
	/// <remarks>
	/// The Postgres outbox deletes a message the instant it is marked sent (<see cref="MarkSentAsync(string, CancellationToken)"/>
	/// issues a DELETE), so there is no terminal sent row to count and cleanup is a no-op.
	/// </remarks>
	public bool SupportsSentTracking => false;

	/// <summary>
	/// Gets the identity this process presents when it reserves outbox messages.
	/// </summary>
	/// <remarks>
	/// Reserving a message and later reporting it failed must present the SAME identity, or the mark-failed
	/// reservation guard cannot recognise the caller as the lease holder. Deriving it once here — rather than
	/// rebuilding the string at each call site — is what makes that agreement structural: the two paths cannot
	/// drift apart, because there is only one expression.
	/// </remarks>
	private static string DispatcherId { get; } = $"dispatcher-{Environment.MachineName}-{Environment.ProcessId}";

	private readonly PostgresOutboxStoreOptions _options;
	private readonly ILogger<PostgresOutboxStore> _logger;
	private readonly PostgresOutboxStoreMetrics _metrics;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="PostgresOutboxStore"/> class. The constructor is
	/// <see langword="internal"/>: consumers register the store via <c>AddPostgresOutboxStore()</c> and resolve it
	/// through its public interfaces; the framework's own DI factory is the only constructor caller.
	/// </summary>
	/// <param name="db"> Database connection interface. </param>
	/// <param name="options"> Configuration options for the Postgres outbox store. </param>
	/// <param name="logger"> Logger for diagnostic output. </param>
	/// <param name="metrics"> Metrics collector for performance monitoring. </param>
	internal PostgresOutboxStore(
		IDb db,
		IOptions<PostgresOutboxStoreOptions> options,
		ILogger<PostgresOutboxStore> logger,
		PostgresOutboxStoreMetrics? metrics = null)
	{
		_db = db ?? throw new ArgumentNullException(nameof(db));
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_metrics = metrics ?? new PostgresOutboxStoreMetrics();
	}

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
					// CorrelationId/CausationId plus the routing fields (PartitionKey/GroupKey/TargetTransports/
					// IsMultiTransport) are first-class on the concrete OutboxMessage (the canonical
					// FromOutboundMessage seam) but not on the IOutboxMessage contract; read them off the concrete
					// type when present so the stage→reload round-trip preserves every consumer-supplied field
					// (null/false for other IOutboxMessage shapes, which never carried them).
					var contextual = message as OutboxMessage;
					var req = new InsertOutboxMessage(
						message.MessageId,
						message.MessageType,
						message.MessageMetadata,
						message.MessageBody,
						message.CreatedAt,
						message.TenantId,
						message.Destination,
						contextual?.CorrelationId,
						contextual?.CausationId,
						message.Priority,
						message.ScheduledAt,
						contextual?.PartitionKey,
						contextual?.GroupKey,
						contextual?.SequenceNumber ?? 0L,
						contextual?.TargetTransports,
						contextual?.IsMultiTransport ?? false,
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
				// The IDb resolve layer (DbConnectionExtensions.ResolveAsync) wraps any non-ApiException in an
				// OperationFailedException, so the underlying unique-violation arrives wrapped rather than as a bare
				// PostgresException. Unwrap it so a duplicate MessageId surfaces as the uniform
				// InvalidOperationException the IOutboxStore contract specifies (parity with SqlServer), not the
				// provider-internal OperationFailedException.
				catch (OperationFailedException ex)
					when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } inner)
				{
					throw new InvalidOperationException(
						$"Outbox message '{message.MessageId}' already exists.",
						inner);
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

		// Canonical conversion (OutboxMessage.FromOutboundMessage) so no stage path can silently drop a persisted
		// field — TenantId in particular, which the bare ctor omits (bd-cd8h8t).
		var outboxMessage = OutboxMessage.FromOutboundMessage(message);

		_ = await SaveMessagesAsync(new[] { outboxMessage }, cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	/// <remarks>
	/// Stages a message within an externally owned transaction. The caller is responsible
	/// for committing or rolling back the transaction. This enables atomic consistency
	/// between event store appends and outbox writes in event sourcing scenarios.
	/// </remarks>
	public async ValueTask StageMessageAsync(
		OutboundMessage message,
		System.Data.IDbTransaction transaction,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(transaction);

		if (transaction is not Npgsql.NpgsqlTransaction)
		{
			throw new ArgumentException(
				$"Expected NpgsqlTransaction but received {transaction.GetType().Name}. " +
				"The Postgres outbox store requires an NpgsqlTransaction for transactional staging.",
				nameof(transaction));
		}

		var connection = transaction.Connection
			?? throw new InvalidOperationException("The transaction's connection is null or has been disposed.");

		var stopwatch = ValueStopwatch.StartNew();
		try
		{
			// Same single conversion seam as the non-transactional path so TenantId can't be dropped here either.
			var outboxMsg = OutboxMessage.FromOutboundMessage(message);

			var req = new InsertOutboxMessage(
				outboxMsg.MessageId,
				outboxMsg.MessageType,
				outboxMsg.MessageMetadata,
				outboxMsg.MessageBody,
				outboxMsg.CreatedAt,
				outboxMsg.TenantId,
				outboxMsg.Destination,
				outboxMsg.CorrelationId,
				outboxMsg.CausationId,
				outboxMsg.Priority,
				outboxMsg.ScheduledAt,
				outboxMsg.PartitionKey,
				outboxMsg.GroupKey,
				outboxMsg.SequenceNumber,
				outboxMsg.TargetTransports,
				outboxMsg.IsMultiTransport,
				_options.QualifiedOutboxTableName,
				DbTimeouts.RegularTimeoutSeconds,
				cancellationToken);

			// Execute using the externally provided transaction's connection
			_ = await connection.ExecuteAsync(
				new Dapper.CommandDefinition(
					req.Command.CommandText,
					req.Command.Parameters,
					transaction,
					req.Command.CommandTimeout,
					cancellationToken: cancellationToken)).ConfigureAwait(false);

			_logger.LogDebug("Staged outbox message {MessageId} within external transaction", message.Id);
		}
		catch (Npgsql.PostgresException ex) when (ex.SqlState == Npgsql.PostgresErrorCodes.UniqueViolation)
		{
			_logger.LogWarning(ex, "Duplicate outbox message detected for {MessageId}", message.Id);
			throw new InvalidOperationException($"Outbox message '{message.Id}' already exists.", ex);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to stage outbox message {MessageId} within external transaction", message.Id);
			throw;
		}
		finally
		{
			var durationMs = stopwatch.Elapsed.TotalMilliseconds;
			_metrics.RecordSaveMessages(durationMs, 1);
			LogOperationCompleted(durationMs, "StageTransactional");
		}
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
	[UnconditionalSuppressMessage("Trimming", "IL2046", Justification = "Implementation inherently uses reflection-based serialization; interface intentionally omits attribute for clean consumer API.")]
	[UnconditionalSuppressMessage("AOT", "IL3051", Justification = "Implementation inherently uses reflection-based serialization; interface intentionally omits attribute for clean consumer API.")]
	public async ValueTask EnqueueAsync(IDispatchMessage message, IMessageContext context, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(context);

		// Create OutboundMessage from IDispatchMessage and context. Serialize the RUNTIME type (not the
		// IDispatchMessage compile-time type, which would drop every derived member) via the canonical event
		// contract so the payload round-trips byte-for-byte with the default serializer on read.
		var serializedPayload = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(
			message, message.GetType(), EventSerializationDefaults.Canonical);
		var headers = new Dictionary<string, object>
			(StringComparer.Ordinal)
		{
			["MessageId"] = context.MessageId ?? string.Empty,
			["CorrelationId"] = context.CorrelationId ?? string.Empty,
			["CausationId"] = context.CausationId ?? string.Empty,
			["MessageType"] = message.GetType().Name,
			["Timestamp"] = DateTimeOffset.UtcNow.ToString("O"),
		};

		// Uniform construction via the single context->message factory so TenantId/Correlation/Causation
		// are never dropped on a convenience path (xl56kb). The propagated TenantId is persisted to the
		// outbox table's tenant_id column on both the direct (InsertOutboxMessage) and scheduled
		// (ScheduleOutboxMessage) paths and read back on reserve/get-scheduled (bd-cd8h8t).
		// Derive the routing destination from the message context (cys98n) — falling back to the message
		// type name (the convention the other outbox providers use) rather than a hardcoded "default", so
		// a consumer's configured destination is persisted and honored on dispatch.
		var destination = context.ExtractMetadata().GetDestination() ?? message.GetType().Name;
		var outboundMessage = OutboundMessage.FromContext(
			message.GetType().Name,
			serializedPayload,
			destination,
			context,
			headers);
		outboundMessage.Id = context.MessageId ?? string.Empty;

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
	[UnconditionalSuppressMessage("Trimming", "IL2046", Justification = "Implementation inherently uses reflection-based serialization; interface intentionally omits attribute for clean consumer API.")]
	[UnconditionalSuppressMessage("AOT", "IL3051", Justification = "Implementation inherently uses reflection-based serialization; interface intentionally omits attribute for clean consumer API.")]
	[RequiresDynamicCode(
		"JSON deserialization of message payload and metadata requires dynamic code generation for reflection-based type construction and property setting.")]
	public async ValueTask<IEnumerable<OutboundMessage>> GetUnsentMessagesAsync(
		int batchSize,
		CancellationToken cancellationToken)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchSize);

		// Use a fixed dispatcher ID for this interface method - in practice this would need proper session management
		var dispatcherId = DispatcherId;
		var reservedMessages = await ReserveOutboxMessagesAsync(dispatcherId, batchSize, cancellationToken).ConfigureAwait(false);

		return ConvertReservedToOutbound(reservedMessages);
	}

	/// <summary>
	/// Converts reserved <see cref="IOutboxMessage"/> rows into <see cref="OutboundMessage"/> instances,
	/// rehydrating the metadata header blob. A row that fails conversion is logged and skipped.
	/// </summary>
	[RequiresUnreferencedCode(
		"JSON deserialization of message metadata may reference types not preserved during trimming. Ensure all deserialized types are annotated with DynamicallyAccessedMembers.")]
	[RequiresDynamicCode(
		"JSON deserialization of message metadata requires dynamic code generation for reflection-based type construction and property setting.")]
	private List<OutboundMessage> ConvertReservedToOutbound(IEnumerable<IOutboxMessage> reservedMessages)
	{
		// Convert IOutboxMessage to OutboundMessage
		var outboundMessages = new List<OutboundMessage>();
		foreach (var msg in reservedMessages)
		{
			try
			{
				var headers = string.IsNullOrEmpty(msg.MessageMetadata)
					? []
					: System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(msg.MessageMetadata, EventSerializationDefaults.Canonical) ??
					  [];

				// CorrelationId/CausationId + the routing fields (PartitionKey/GroupKey/TargetTransports/
				// IsMultiTransport) are first-class on the concrete OutboxMessage the reserve RETURNING
				// hydrates, but not on the IOutboxMessage contract; read them off the concrete type so every
				// consumer-supplied field survives the reload (null/false for other IOutboxMessage shapes).
				var contextual = msg as OutboxMessage;
				var outboundMessage = new OutboundMessage(
					msg.MessageType,
					msg.MessageBody,
					msg.Destination ?? string.Empty, // round-tripped from the persisted destination column
					headers)
				{
					Id = msg.MessageId,
					TenantId = msg.TenantId,
					CorrelationId = contextual?.CorrelationId,
					CausationId = contextual?.CausationId,
					Priority = msg.Priority,
					ScheduledAt = msg.ScheduledAt,
					PartitionKey = contextual?.PartitionKey,
					GroupKey = contextual?.GroupKey,
					SequenceNumber = contextual?.SequenceNumber ?? 0,
					TargetTransports = contextual?.TargetTransports,
					IsMultiTransport = contextual?.IsMultiTransport ?? false,
					// Preserve the persisted created-at (occurred_on) end-to-end: the OutboundMessage ctor
					// re-stamps CreatedAt to UtcNow, so the drain reload must restore it or the caller's
					// timestamp (and its ordering/audit fidelity) is lost on the consumer path.
					CreatedAt = msg.CreatedAt,
				};

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

	/// <inheritdoc />
	/// <remarks>
	/// The delete-on-sent Postgres outbox deletes the winning rows on mark-sent, so a live-row
	/// <c>MAX(fencing_token)</c> cannot survive the drain. The fencing high-water is therefore kept in a
	/// dedicated single-row-per-scope control table (<see cref="PostgresOutboxStoreOptions.FenceTableName"/>)
	/// and enforced by an atomic <c>INSERT … ON CONFLICT DO UPDATE … RETURNING</c> compare-and-swap before
	/// the claim runs. A stale token yields zero claimable rows — a set-based, non-throwing signal — while
	/// the high-water advances monotonically to the maximum of its current value and the presented token.
	/// </remarks>
	[RequiresUnreferencedCode(
		"JSON deserialization of message payload and metadata may reference types not preserved during trimming. Ensure all deserialized types are annotated with DynamicallyAccessedMembers.")]
	[UnconditionalSuppressMessage("Trimming", "IL2046", Justification = "Implementation inherently uses reflection-based serialization; interface intentionally omits attribute for clean consumer API.")]
	[UnconditionalSuppressMessage("AOT", "IL3051", Justification = "Implementation inherently uses reflection-based serialization; interface intentionally omits attribute for clean consumer API.")]
	[RequiresDynamicCode(
		"JSON deserialization of message payload and metadata requires dynamic code generation for reflection-based type construction and property setting.")]
	public async ValueTask<IEnumerable<OutboundMessage>> GetUnsentMessagesAsync(
		int batchSize,
		long fencingToken,
		CancellationToken cancellationToken)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchSize);

		// The fence-CAS and the claim are folded into ONE atomic statement (the FencedReserveOutboxMessages
		// wCTE): the fence INSERT … ON CONFLICT DO UPDATE takes the per-scope fence row lock and advances the
		// high-water, and the SKIP-LOCKED claim is gated on the fence returning EXACTLY the presented token —
		// all under the fence row lock held through the single statement's implicit transaction. A fresher
		// leader cannot advance the high-water between this caller's fence check and its reservation (the
		// check-then-act window is closed by construction). A superseded (stale) token makes the gate false
		// for every row, so the claim yields a set-based empty result — it MUST NOT throw here
		// (IFencedOutboxStore claim contract). No explicit BeginTransaction is held on the shared
		// _db.Connection (which would monopolize/poison it for the rest of the drain).
		var dispatcherId = DispatcherId;
		var request = new FencedReserveOutboxMessages(
			dispatcherId,
			batchSize,
			_options.ReservationTimeout,
			fencingToken,
			_options.QualifiedOutboxTableName,
			_options.QualifiedFenceTableName,
			DbTimeouts.RegularTimeoutSeconds,
			cancellationToken);

		var reservedMessages = await _db.Connection.ResolveAsync(request).ConfigureAwait(false);
		return ConvertReservedToOutbound(reservedMessages);
	}

	/// <inheritdoc />
	/// <remarks>
	/// Enforces the fencing high-water and deletes the row in a SINGLE atomic statement (fence
	/// compare-and-swap folded with the delete under the fence row lock), so no fresher leader can advance
	/// the high-water between the fence check and the delete. A superseded (stale) token is rejected
	/// fail-closed with <see cref="StaleOutboxFencingTokenException"/> and nothing is deleted.
	/// </remarks>
	public async ValueTask MarkSentAsync(string messageId, long fencingToken, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);

		var request = new FencedDeleteOutboxMessage(
			messageId,
			_options.QualifiedOutboxTableName,
			fencingToken,
			_options.QualifiedFenceTableName,
			DbTimeouts.RegularTimeoutSeconds,
			cancellationToken);

		var result = await _db.Connection.ResolveAsync(request).ConfigureAwait(false);

		// GREATEST(existing, token) == token exactly when the token was >= the previous high-water (accepted).
		// A strictly greater stored high-water means a successor already advanced past this token (stale leader):
		// the folded statement deleted nothing, and we reject fail-closed.
		if (result.HighWaterToken != fencingToken)
		{
			throw new StaleOutboxFencingTokenException(
				$"The presented outbox fencing token ({fencingToken}) is below the recorded high-water mark ({result.HighWaterToken}) for scope '{_options.QualifiedOutboxTableName}' (superseded leader).")
			{
				PresentedToken = fencingToken,
				HighWaterToken = result.HighWaterToken,
			};
		}

		if (result.DeletedCount == 0)
		{
			throw new InvalidOperationException($"Message {messageId} not found or already sent.");
		}
	}

	/// <summary>
	/// Durably transitions the specified message to the terminal dead-lettered state.
	/// </summary>
	/// <remarks>
	/// The Postgres outbox uses a separate dead-letter table as its terminal state rather than a
	/// status column. This method moves the message into the dead-letter table with the supplied
	/// <paramref name="reason"/> as the error message and deletes it from the main outbox table,
	/// making it structurally non-claimable by any delivery predicate.
	/// </remarks>
	/// <param name="messageId"> Unique identifier of the message to dead-letter. </param>
	/// <param name="reason"> Human-readable reason the message was dead-lettered. </param>
	/// <param name="cancellationToken"> Cancellation token for the operation. </param>
	/// <returns> A value task representing the asynchronous operation. </returns>
	public async ValueTask MarkDeadLetteredAsync(string messageId, string reason, CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentNullException.ThrowIfNull(reason);

		_logger.LogWarning("Dead-lettering outbox message {MessageId}: {Reason}", messageId, reason);

		var stopwatch = ValueStopwatch.StartNew();
		try
		{
			var req = new MarkMessageDeadLetteredRequest(
				messageId,
				reason,
				_options.QualifiedOutboxTableName,
				_options.QualifiedDeadLetterTableName,
				DbTimeouts.RegularTimeoutSeconds,
				cancellationToken);

			_ = await _db.Connection.ResolveAsync(req).ConfigureAwait(false);
		}
		finally
		{
			var durationMs = stopwatch.Elapsed.TotalMilliseconds;
			_metrics.RecordMoveToDeadLetter(durationMs);
			LogOperationCompleted(durationMs, "MarkDeadLettered");
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

		// Persist the failure on the row (authoritative retry count + last error) so a sub-ceiling failure is
		// observable via GetFailedMessagesAsync/GetStatistics BEFORE it dead-letters — the delete-on-sent
		// design's failed-state signal (error_message IS NOT NULL), matching the Oracle outbox. This replaces
		// the bare attempt-increment, which recorded no error and left the failure invisible until the ceiling.
		// Presenting this dispatcher's identity restricts the unreserve to a message this caller actually holds
		// (or one nobody holds). Reporting a failure against a lease another dispatcher has since taken is a
		// no-op rather than a silent theft of its reservation — see SetOutboxMessageFailed for why an
		// unconditional unreserve is a double-delivery bug.
		var req = new SetOutboxMessageFailed(
			messageId,
			retryCount,
			errorMessage,
			DispatcherId,
			_options.FailureBackoffFloorSeconds,
			_options.QualifiedOutboxTableName,
			DbTimeouts.RegularTimeoutSeconds,
			cancellationToken);

		_ = await _db.Connection.ResolveAsync(req).ConfigureAwait(false);

		// The message stays a RETRIEVABLE failed message (error_message set); the dead-letter transition at the
		// retry ceiling is the OutboxProcessor's job (RouteToDeadLetterQueueAsync, attempts >= MaxAttempts), NOT
		// the store's — matching the InMemory reference contract (one load-bearing postcondition per family).
	}

	/// <summary>
	/// Marks a message as failed and records an exponential-backoff schedule so it is not re-claimed for retry
	/// until <paramref name="nextAttemptAt"/> has elapsed (the Postgres half of the outbox mark-failed-with-backoff path).
	/// </summary>
	/// <param name="messageId"> Unique identifier of the message that failed. </param>
	/// <param name="errorMessage"> Error message describing the failure. </param>
	/// <param name="retryCount"> Current retry attempt count. </param>
	/// <param name="nextAttemptAt"> Absolute time before which the message must not be re-claimed. </param>
	/// <param name="cancellationToken"> Cancellation token for the operation. </param>
	/// <returns> A task representing the asynchronous operation. </returns>
	public async ValueTask MarkFailedWithBackoffAsync(
		string messageId,
		string errorMessage,
		int retryCount,
		DateTimeOffset nextAttemptAt,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
		ArgumentException.ThrowIfNullOrWhiteSpace(errorMessage);
		ArgumentOutOfRangeException.ThrowIfNegative(retryCount);

		// Increment attempts, persist next_attempt_at, and free the reservation in one atomic statement so the
		// reservation claim re-evaluates the message -- gated by next_attempt_at, so the computed backoff delay
		// genuinely throttles re-delivery rather than the coarse reservation timeout.
		// Presenting this dispatcher's identity restricts the unreserve to a message this caller actually
		// holds (or one nobody holds) — see SetOutboxMessageBackoff for why an unconditional unreserve is a
		// double-delivery bug on this path too, not just the mark-failed one.
		var req = new SetOutboxMessageBackoff(
			messageId,
			nextAttemptAt,
			DispatcherId,
			_options.QualifiedOutboxTableName,
			DbTimeouts.RegularTimeoutSeconds,
			cancellationToken);

		_ = await _db.Connection.ResolveAsync(req).ConfigureAwait(false);

		// The message stays a RETRIEVABLE failed message; the dead-letter transition at the retry ceiling is the
		// OutboxProcessor's job (RouteToDeadLetterQueueAsync), NOT the store's — matching the InMemory reference.
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
			// Read failed-but-retryable messages from the MAIN table (error_message IS NOT NULL, attempts within
			// the ceiling), not the terminal dead-letter table — so a sub-ceiling failure is observable here
			// before it dead-letters (delete-on-sent failed-state semantics, Oracle-parity).
			var req = new GetFailedOutboxMessages(
				_options.QualifiedOutboxTableName,
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
				Payload = r.MessageBody,
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
				message.TenantId,
				message.Destination,
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
	public ValueTask<int> CleanupAllTenantsSentMessagesAsync(DateTimeOffset olderThan, int batchSize, CancellationToken cancellationToken)
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
	[LoggerMessage(OutboxPostgresEventId.OutboxSaveMessages, LogLevel.Debug,
		"Saving {MessageCount} outbox messages")]
	private partial void LogSaveMessages(int messageCount);

	[LoggerMessage(OutboxPostgresEventId.OutboxReserveMessages, LogLevel.Debug,
		"Reserving up to {BatchSize} outbox messages for dispatcher {DispatcherId}")]
	private partial void LogReserveMessages(string dispatcherId, int batchSize);

	[LoggerMessage(OutboxPostgresEventId.OutboxUnreserveMessages, LogLevel.Debug,
		"Unreserving outbox messages for dispatcher {DispatcherId}")]
	private partial void LogUnreserveMessages(string dispatcherId);

	[LoggerMessage(OutboxPostgresEventId.OutboxDeleteRecord, LogLevel.Debug,
		"Deleting outbox record {MessageId}")]
	private partial void LogDeleteRecord(string messageId);

	[LoggerMessage(OutboxPostgresEventId.OutboxIncreaseAttempts, LogLevel.Debug,
		"Increasing attempts for outbox message {MessageId}")]
	private partial void LogIncreaseAttempts(string messageId);

	[LoggerMessage(OutboxPostgresEventId.OutboxMoveToDeadLetter, LogLevel.Debug,
		"Moving outbox message {MessageId} to dead letter table")]
	private partial void LogMoveToDeadLetter(string messageId);

	[LoggerMessage(OutboxPostgresEventId.OutboxBatchOperation, LogLevel.Debug,
		"Processing batch operation for {MessageCount} messages")]
	private partial void LogBatchOperation(int messageCount);

	[LoggerMessage(OutboxPostgresEventId.OutboxOperationCompleted, LogLevel.Information,
		"Outbox operation {Operation} completed in {Duration:F2}ms")]
	private partial void LogOperationCompleted(double duration, string operation);

	[LoggerMessage(OutboxPostgresEventId.OutboxConvertMessageFailed, LogLevel.Warning,
		"Failed to convert reserved message {MessageId} to OutboundMessage")]
	private partial void LogConvertMessageFailed(string messageId, Exception ex);

	[LoggerMessage(OutboxPostgresEventId.OutboxGetFailedMessagesNotSupported, LogLevel.Debug,
		"Querying dead letter table for failed messages with maxRetries={MaxRetries}, batchSize={BatchSize}")]
	private partial void LogGetFailedMessages(int maxRetries, int batchSize);

	[LoggerMessage(OutboxPostgresEventId.OutboxGetScheduledMessagesNotSupported, LogLevel.Debug,
		"Retrieving scheduled outbox messages with batchSize={BatchSize}")]
	private partial void LogGetScheduledMessages(int batchSize);

	[LoggerMessage(OutboxPostgresEventId.OutboxCleanupSentMessagesNotNeeded, LogLevel.Debug,
		"CleanupAllTenantsSentMessagesAsync called but Postgres implementation automatically deletes sent messages")]
	private partial void LogCleanupSentMessagesNotNeeded();

	[LoggerMessage(OutboxPostgresEventId.OutboxGetStatisticsBasic, LogLevel.Debug,
		"Retrieving outbox statistics from database")]
	private partial void LogGetStatistics();

	[LoggerMessage(OutboxPostgresEventId.OutboxGetStatisticsBasic + 1, LogLevel.Debug,
		"Scheduling outbox message {MessageId} for delivery at {ScheduledAt}")]
	private partial void LogScheduleMessage(string messageId, DateTimeOffset scheduledAt);
}
