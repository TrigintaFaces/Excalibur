// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using Dapper;

using Excalibur.Data;

namespace Excalibur.Outbox.Postgres;

/// <summary>
/// Represents a data request to insert a scheduled outbox message into the Postgres database.
/// </summary>
internal sealed class ScheduleOutboxMessage : DataRequest<int>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="ScheduleOutboxMessage"/> class.
	/// </summary>
	/// <param name="messageId"> The unique identifier of the message. </param>
	/// <param name="messageType"> The type of the message. </param>
	/// <param name="messageMetadata"> The metadata associated with the message. </param>
	/// <param name="messageBody"> The serialized body content of the message as raw bytes (persisted to a <c>bytea</c> column). </param>
	/// <param name="tenantId"> The tenant identifier the message was produced under, or <see langword="null"/> when no tenant scope applies. </param>
	/// <param name="destination"> The delivery destination the message is routed to, or <see langword="null"/> when none was carried. </param>
	/// <param name="scheduledAt"> The scheduled delivery time. </param>
	/// <param name="outboxTableName"> The name of the outbox table. </param>
	/// <param name="sqlTimeOutSeconds"> The SQL command timeout in seconds. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	public ScheduleOutboxMessage(
		string messageId,
		string messageType,
		string messageMetadata,
		byte[] messageBody,
		string? tenantId,
		string? destination,
		DateTimeOffset scheduledAt,
		string outboxTableName,
		int sqlTimeOutSeconds,
		CancellationToken cancellationToken)
	{
		var sql = $"""
			INSERT INTO {outboxTableName}
				(message_id, message_type, message_metadata, message_body, tenant_id, destination, occurred_on, attempts, dispatcher_id, dispatcher_timeout, scheduled_at)
			VALUES
				(@MessageId, @MessageType, @MessageMetadata, @MessageBody, @TenantId, @Destination, NOW(), 0, NULL, NULL, @ScheduledAt);
			""";

		var parameters = new DynamicParameters();
		parameters.Add("MessageId", messageId, direction: ParameterDirection.Input);
		parameters.Add("MessageType", messageType, direction: ParameterDirection.Input);
		parameters.Add("MessageMetadata", messageMetadata, direction: ParameterDirection.Input);
		parameters.Add("MessageBody", messageBody, direction: ParameterDirection.Input);
		parameters.Add("TenantId", tenantId, direction: ParameterDirection.Input);
		parameters.Add("Destination", destination, direction: ParameterDirection.Input);
		// Bind scheduled_at with an EXPLICIT timestamptz type. A CLR DateTime (even Kind=Utc) makes Dapper infer
		// DbType.DateTime → Npgsql timestamp WITHOUT time zone → a session-timezone shift by the host's local UTC
		// offset on reload. TimestampTzParameter binds the DateTimeOffset as a true timestamptz, preserving the instant.
		parameters.Add("ScheduledAt", new TimestampTzParameter(scheduledAt));

		Command = CreateCommand(sql, (DynamicParameters?)parameters, commandTimeout: sqlTimeOutSeconds, cancellationToken: cancellationToken);
		ResolveAsync = async conn => await conn.ExecuteAsync(Command).ConfigureAwait(false);
	}
}
