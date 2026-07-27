// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Dapper;

using Excalibur.Data;

namespace Excalibur.Outbox.SqlServer.Requests;

/// <summary>
/// Data request to get unsent messages from the outbox.
/// </summary>
public sealed class GetUnsentMessagesRequest : DataRequestBase<IDbConnection, IEnumerable<OutboxMessageRow>>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="GetUnsentMessagesRequest"/> class.
	/// Uses UPDATE...OUTPUT with lease columns to atomically claim and fetch messages,
	/// preventing double-processing by concurrent processors.
	/// </summary>
	/// <param name="tableName">The qualified outbox table name.</param>
	/// <param name="batchSize">Maximum number of messages to retrieve.</param>
	/// <param name="commandTimeout">Command timeout in seconds.</param>
	/// <param name="leaseTimeoutSeconds">Lease timeout in seconds for stale lease reclamation.</param>
	/// <param name="processorId">Identifier of the claiming processor.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <param name="fencingToken">
	/// The fencing token for the caller's current leadership tenure, or <see langword="null"/> when no
	/// fencing applies. When non-null, rows are claimed only if <paramref name="fencingToken"/> is greater
	/// than or equal to the recorded fencing high-water mark; claimed rows have their <c>FencingToken</c>
	/// column atomically advanced to this value as part of the same claim. A stale (superseded) token
	/// yields zero claimable rows rather than throwing.
	/// <para>
	/// The high-water mark is read from the durable <c>OutboxFence</c> control table (keyed by
	/// <paramref name="fenceScope"/>), not from <c>MAX(FencingToken)</c> over the message rows. Because
	/// cleanup never deletes that control row, the high-water outlives the purge of sent, token-bearing
	/// rows, so a superseded leader's stale token stays rejected across a cleanup. This affects only the
	/// leadership high-water guard; the per-message lease still prevents two processors from claiming the
	/// same row.
	/// </para>
	/// </param>
	/// <param name="fenceTableName">The qualified durable fence control table name.</param>
	/// <param name="fenceScope">The fence scope key — the qualified outbox table name this fence guards.</param>
	public GetUnsentMessagesRequest(
		string tableName,
		int batchSize,
		int commandTimeout,
		int leaseTimeoutSeconds,
		string processorId,
		long? fencingToken,
		string fenceTableName,
		string fenceScope,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(tableName);
		ArgumentException.ThrowIfNullOrWhiteSpace(processorId);
		ArgumentException.ThrowIfNullOrWhiteSpace(fenceTableName);
		ArgumentException.ThrowIfNullOrWhiteSpace(fenceScope);

		// Atomic claim + fetch: an ordered CTE selects the eligible rows in partition/sequence order,
		// then UPDATE sets lease ownership and OUTPUT returns the claimed rows.
		//
		// The ordering MUST be applied at row SELECTION (the CTE's ORDER BY), not on the OUTPUT clause:
		// SQL Server's OUTPUT clause cannot be ordered, so a trailing ORDER BY on UPDATE...OUTPUT does
		// not order the returned rows. Selecting TOP (@BatchSize) in (PartitionKey, SequenceNumber) order
		// guarantees same-partition messages are claimed in ascending SequenceNumber (the advertised
		// partition-FIFO guarantee), and lets processors restore ordering after a batch failure.
		//
		// NextAttemptAt gates retry visibility (the per-message backoff schedule): a failed message is not
		// re-claimed until its computed next-attempt time has elapsed (NULL = immediately eligible).
		var sql = $"""
			WITH Claimable AS (
				SELECT TOP (@BatchSize) *
				FROM {tableName} WITH (READPAST, UPDLOCK, ROWLOCK)
				WHERE Status IN (0, 3, 4) -- Staged, Failed, PartiallyFailed
					AND (ScheduledAt IS NULL OR ScheduledAt <= @Now)
					AND (NextAttemptAt IS NULL OR NextAttemptAt <= @Now)
					AND (LeasedAt IS NULL OR LeasedAt < DATEADD(SECOND, -@LeaseTimeoutSeconds, GETUTCDATE()))
					AND (@FencingToken IS NULL OR @FencingToken >= ISNULL((SELECT HighWaterToken FROM {fenceTableName} WHERE OutboxTable = @FenceScope), 0))
				ORDER BY PartitionKey, SequenceNumber ASC
			)
			UPDATE Claimable
			SET LeasedAt = GETUTCDATE(), LeasedBy = @ProcessorId,
				FencingToken = CASE WHEN @FencingToken IS NULL THEN FencingToken ELSE @FencingToken END
			OUTPUT
				INSERTED.Id, INSERTED.MessageType, INSERTED.Payload, INSERTED.Headers,
				INSERTED.Destination, INSERTED.CreatedAt, INSERTED.ScheduledAt, INSERTED.SentAt,
				INSERTED.Status, INSERTED.RetryCount, INSERTED.LastError, INSERTED.LastAttemptAt,
				INSERTED.CorrelationId, INSERTED.CausationId, INSERTED.TenantId, INSERTED.Priority,
				INSERTED.TargetTransports, INSERTED.IsMultiTransport,
				INSERTED.PartitionKey, INSERTED.GroupKey, INSERTED.SequenceNumber
			""";

		var parameters = new DynamicParameters();
		parameters.Add("@BatchSize", batchSize);
		parameters.Add("@Now", DateTimeOffset.UtcNow);
		parameters.Add("@LeaseTimeoutSeconds", leaseTimeoutSeconds);
		parameters.Add("@ProcessorId", processorId);
		parameters.Add("@FencingToken", fencingToken);
		parameters.Add("@FenceScope", fenceScope);

		Command = CreateCommand(sql, parameters, commandTimeout: commandTimeout, cancellationToken: cancellationToken);

		ResolveAsync = async connection =>
			await connection.QueryAsync<OutboxMessageRow>(Command).ConfigureAwait(false);
	}
}

/// <summary>
/// Internal record for Dapper mapping from outbox database row.
/// </summary>
public sealed class OutboxMessageRow
{
	/// <summary>The message ID.</summary>
	public string Id { get; set; } = string.Empty;

	/// <summary>The message type name.</summary>
	public string MessageType { get; set; } = string.Empty;

	/// <summary>The serialized message payload.</summary>
	public byte[] Payload { get; set; } = [];

	/// <summary>The serialized headers JSON.</summary>
	public string? Headers { get; set; }

	/// <summary>The destination.</summary>
	public string Destination { get; set; } = string.Empty;

	/// <summary>When the message was created.</summary>
	public DateTimeOffset CreatedAt { get; set; }

	/// <summary>Optional scheduled delivery time.</summary>
	public DateTimeOffset? ScheduledAt { get; set; }

	/// <summary>When the message was sent.</summary>
	public DateTimeOffset? SentAt { get; set; }

	/// <summary>Current status.</summary>
	public int Status { get; set; }

	/// <summary>Number of retry attempts.</summary>
	public int RetryCount { get; set; }

	/// <summary>Last error message if failed.</summary>
	public string? LastError { get; set; }

	/// <summary>When the last attempt was made.</summary>
	public DateTimeOffset? LastAttemptAt { get; set; }

	/// <summary>Correlation ID for tracing.</summary>
	public string? CorrelationId { get; set; }

	/// <summary>Causation ID for tracing.</summary>
	public string? CausationId { get; set; }

	/// <summary>Tenant ID for multi-tenancy.</summary>
	public string? TenantId { get; set; }

	/// <summary>Message priority.</summary>
	public int Priority { get; set; }

	/// <summary>Target transports for multi-transport delivery.</summary>
	public string? TargetTransports { get; set; }

	/// <summary>Whether this is a multi-transport message.</summary>
	public bool IsMultiTransport { get; set; }

	/// <summary>The partition key for ordered delivery within the same partition.</summary>
	public string? PartitionKey { get; set; }

	/// <summary>The group key for logical message grouping.</summary>
	public string? GroupKey { get; set; }

	/// <summary>The monotonically increasing sequence number for ordering guarantees.</summary>
	public long SequenceNumber { get; set; }
}
