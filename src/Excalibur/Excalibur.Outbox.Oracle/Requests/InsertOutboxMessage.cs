// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Dapper;

using Excalibur.Data;
using Excalibur.Dispatch;

namespace Excalibur.Outbox.Oracle;

/// <summary>
/// Represents a data request to insert a new outbox message into the Oracle database.
/// </summary>
public sealed class InsertOutboxMessage : DataRequest<int>
{

	/// <summary>
	/// Initializes a new instance of the <see cref="InsertOutboxMessage"/> class.
	/// </summary>
	/// <param name="messageId">The unique identifier of the message.</param>
	/// <param name="messageType">The type of the message.</param>
	/// <param name="messageMetadata">The metadata associated with the message.</param>
	/// <param name="messageBody">The serialized body content of the message as raw bytes (persisted to a <c>BLOB</c> column).</param>
	/// <param name="createdAt">The time the message was created; persisted to <c>occurred_on</c> so the caller's created-at survives the stage→reload round-trip (rather than being overwritten with the database server clock).</param>
	/// <param name="tenantId">The tenant identifier the message was produced under, or <see langword="null"/> when no tenant scope applies.</param>
	/// <param name="destination">The delivery destination the message is routed to, or <see langword="null"/> when none was carried.</param>
	/// <param name="correlationId">The correlation identifier for distributed tracing, or <see langword="null"/> when none was carried.</param>
	/// <param name="causationId">The causation identifier linking to the triggering message, or <see langword="null"/> when none was carried.</param>
	/// <param name="priority">The delivery priority (higher values indicate higher priority); <c>0</c> when none was specified.</param>
	/// <param name="scheduledAt">The time before which the message must not be delivered, or <see langword="null"/> when it is deliverable immediately.</param>
	/// <param name="partitionKey">The partition key preserving ordered delivery within a partition, or <see langword="null"/> when none was carried.</param>
	/// <param name="groupKey">The logical grouping key, or <see langword="null"/> when none was carried.</param>
	/// <param name="sequenceNumber">The strictly-increasing per-partition sequence number enforcing ascending in-partition delivery order; <c>0</c> when none was carried.</param>
	/// <param name="targetTransports">The comma-separated set of target transports, or <see langword="null"/> when none was carried.</param>
	/// <param name="isMultiTransport"><see langword="true"/> when the message targets multiple transports; otherwise <see langword="false"/>.</param>
	/// <param name="outboxTableName">The name of the outbox table.</param>
	/// <param name="sqlTimeOutSeconds">The SQL command timeout in seconds.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public InsertOutboxMessage(
		string messageId,
		string messageType,
		string messageMetadata,
		byte[] messageBody,
		DateTimeOffset createdAt,
		string? tenantId,
		string? destination,
		string? correlationId,
		string? causationId,
		int priority,
		DateTimeOffset? scheduledAt,
		string? partitionKey,
		string? groupKey,
		long sequenceNumber,
		string? targetTransports,
		bool isMultiTransport,
		string outboxTableName,
		int sqlTimeOutSeconds,
		CancellationToken cancellationToken)
	{
		// The tenant term is supplied by the caller and stamped into the row below, so the
		// declaration and the INSERT agree. FromStoredValue (not Scoped) is total: it maps null,
		// empty and the reserved sentinel alike onto the untenanted partition, which is this
		// column's contract and keeps an untenanted stage from throwing.
		var tenantPartition = KeyedTenantPartition.FromStoredValue(tenantId);

		// ODP.NET binds positionally (this store does not set BindByName); parameters are added in the exact
		// textual order their placeholders appear. correlation_id/causation_id/priority/scheduled_at plus the
		// routing fields (partition_key/group_key/sequence_number/target_transports/is_multi_transport) are
		// dedicated columns (not folded into the metadata blob) so every consumer-supplied field survives the
		// stage→reload round-trip. occurred_on binds the caller's created-at (:CreatedAt), NOT the server clock
		// (SYSTIMESTAMP), so created-at aligns with SqlServer, which preserves it. is_multi_transport is a
		// NUMBER(1) 0/1 flag (Oracle has no native boolean).
		var sql = $"""
		   INSERT INTO {outboxTableName} (message_id, message_type, message_metadata, message_body, tenant_id, destination, correlation_id, causation_id, priority, scheduled_at, partition_key, group_key, sequence_number, target_transports, is_multi_transport, occurred_on, attempts, dispatcher_id, dispatcher_timeout)
		           VALUES (:MessageId, :MessageType, :MessageMetadata, :MessageBody, :TenantId, :Destination, :CorrelationId, :CausationId, :Priority, :ScheduledAt, :PartitionKey, :GroupKey, :SequenceNumber, :TargetTransports, :IsMultiTransport, :CreatedAt, 0, NULL, NULL)
		   """;

		var parameters = new DynamicParameters();
		parameters.Add("MessageId", messageId, direction: ParameterDirection.Input);
		parameters.Add("MessageType", messageType, direction: ParameterDirection.Input);
		parameters.Add("MessageMetadata", messageMetadata, direction: ParameterDirection.Input);
		parameters.Add("MessageBody", messageBody, direction: ParameterDirection.Input);
		// Bind the PARTITION's term, never the raw argument. The partition is total -- null, empty and the
		// reserved sentinel all resolve to the untenanted term -- so an untenanted stage binds a concrete
		// value rather than NULL, and the column can be NOT NULL. Binding `tenantId` here instead would
		// reject every untenanted write the moment the column became total, and would let the declared
		// disposition and the value actually stored disagree. One local feeds both, so they cannot.
		parameters.Add("TenantId", tenantPartition.TenantId, direction: ParameterDirection.Input);
		parameters.Add("Destination", destination, direction: ParameterDirection.Input);
		parameters.Add("CorrelationId", correlationId, direction: ParameterDirection.Input);
		parameters.Add("CausationId", causationId, direction: ParameterDirection.Input);
		parameters.Add("Priority", priority, direction: ParameterDirection.Input);
		parameters.Add("ScheduledAt", scheduledAt, direction: ParameterDirection.Input);
		parameters.Add("PartitionKey", partitionKey, direction: ParameterDirection.Input);
		parameters.Add("GroupKey", groupKey, direction: ParameterDirection.Input);
		parameters.Add("SequenceNumber", sequenceNumber, direction: ParameterDirection.Input);
		parameters.Add("TargetTransports", targetTransports, direction: ParameterDirection.Input);
		parameters.Add("IsMultiTransport", isMultiTransport ? 1 : 0, direction: ParameterDirection.Input);
		parameters.Add("CreatedAt", createdAt, direction: ParameterDirection.Input);

		Command = CreateCommand(sql, (DynamicParameters?)parameters, commandTimeout: sqlTimeOutSeconds, cancellationToken: cancellationToken);
		ResolveAsync = async conn => await conn.ExecuteAsync(Command).ConfigureAwait(false);
	}
}
