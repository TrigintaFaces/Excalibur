// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Dapper;

using Excalibur.Data;

namespace Excalibur.Outbox.Postgres;

/// <summary>
/// Represents a data request to insert a new outbox message into the Postgres database.
/// </summary>
public sealed class InsertOutboxMessage : DataRequest<int>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="InsertOutboxMessage"/> class.
	/// </summary>
	/// <param name="messageId">The unique identifier of the message.</param>
	/// <param name="messageType">The type of the message.</param>
	/// <param name="messageMetadata">The metadata associated with the message.</param>
	/// <param name="messageBody">The serialized body content of the message as raw bytes (persisted to a <c>bytea</c> column).</param>
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
		// correlation_id/causation_id/priority/scheduled_at plus the routing fields
		// (partition_key/group_key/sequence_number/target_transports/is_multi_transport) are dedicated columns
		// (not folded into the metadata blob) so every consumer-supplied field survives the stage→reload
		// round-trip, matching what SqlServer persists. occurred_on binds the caller's created-at (@CreatedAt),
		// NOT the server clock (NOW()), so created-at aligns with SqlServer, which preserves it. Npgsql binds by
		// name, so parameter order is not load-bearing. is_multi_transport is a native BOOLEAN (Postgres, unlike
		// Oracle, has one).
		var sql = $"""
		   INSERT INTO {outboxTableName} (message_id, message_type, message_metadata, message_body, tenant_id, destination, correlation_id, causation_id, priority, scheduled_at, partition_key, group_key, sequence_number, target_transports, is_multi_transport, occurred_on, attempts, dispatcher_id, dispatcher_timeout)
		           VALUES (@MessageId, @MessageType, @MessageMetadata, @MessageBody, @TenantId, @Destination, @CorrelationId, @CausationId, @Priority, @ScheduledAt, @PartitionKey, @GroupKey, @SequenceNumber, @TargetTransports, @IsMultiTransport, @CreatedAt, 0, NULL, NULL);
		   """;

		var parameters = new DynamicParameters();
		parameters.Add("MessageId", messageId, direction: ParameterDirection.Input);
		parameters.Add("MessageType", messageType, direction: ParameterDirection.Input);
		parameters.Add("MessageMetadata", messageMetadata, direction: ParameterDirection.Input);
		parameters.Add("MessageBody", messageBody, direction: ParameterDirection.Input);
		parameters.Add("TenantId", tenantId, direction: ParameterDirection.Input);
		parameters.Add("Destination", destination, direction: ParameterDirection.Input);
		parameters.Add("CorrelationId", correlationId, direction: ParameterDirection.Input);
		parameters.Add("CausationId", causationId, direction: ParameterDirection.Input);
		parameters.Add("Priority", priority, direction: ParameterDirection.Input);
		// Bind scheduled_at AND occurred_on (created-at) with an EXPLICIT timestamptz type. Passing a CLR DateTime
		// (even Kind=Utc) makes Dapper infer DbType.DateTime, which Npgsql maps to timestamp WITHOUT time zone, so
		// a session-timezone conversion shifts the value by the host's local UTC offset on reload.
		// TimestampTzParameter binds the original DateTimeOffset as a true timestamptz, preserving the exact
		// instant across the round-trip.
		parameters.Add("ScheduledAt", new TimestampTzParameter(scheduledAt));
		parameters.Add("PartitionKey", partitionKey, direction: ParameterDirection.Input);
		parameters.Add("GroupKey", groupKey, direction: ParameterDirection.Input);
		parameters.Add("SequenceNumber", sequenceNumber, direction: ParameterDirection.Input);
		parameters.Add("TargetTransports", targetTransports, direction: ParameterDirection.Input);
		parameters.Add("IsMultiTransport", isMultiTransport, direction: ParameterDirection.Input);
		parameters.Add("CreatedAt", new TimestampTzParameter(createdAt));

		Command = CreateCommand(sql, (DynamicParameters?)parameters, commandTimeout: sqlTimeOutSeconds, cancellationToken: cancellationToken);
		ResolveAsync = async conn => await conn.ExecuteAsync(Command).ConfigureAwait(false);
	}
}
