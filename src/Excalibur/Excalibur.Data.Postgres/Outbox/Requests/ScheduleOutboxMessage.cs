// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using Dapper;

using Excalibur.Data.Abstractions;

namespace Excalibur.Data.Postgres.Outbox;

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
	/// <param name="messageBody"> The body content of the message. </param>
	/// <param name="scheduledAt"> The scheduled delivery time. </param>
	/// <param name="outboxTableName"> The name of the outbox table. </param>
	/// <param name="sqlTimeOutSeconds"> The SQL command timeout in seconds. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	public ScheduleOutboxMessage(
		string messageId,
		string messageType,
		string messageMetadata,
		string messageBody,
		DateTimeOffset scheduledAt,
		string outboxTableName,
		int sqlTimeOutSeconds,
		CancellationToken cancellationToken)
	{
		var sql = $"""
			INSERT INTO {outboxTableName}
				(message_id, message_type, message_metadata, message_body, occurred_on, attempts, dispatcher_id, dispatcher_timeout, scheduled_at)
			VALUES
				(@MessageId, @MessageType, @MessageMetadata, @MessageBody, NOW(), 0, NULL, NULL, @ScheduledAt);
			""";

		var parameters = new DynamicParameters();
		parameters.Add("MessageId", messageId, direction: ParameterDirection.Input);
		parameters.Add("MessageType", messageType, direction: ParameterDirection.Input);
		parameters.Add("MessageMetadata", messageMetadata, direction: ParameterDirection.Input);
		parameters.Add("MessageBody", messageBody, direction: ParameterDirection.Input);
		parameters.Add("ScheduledAt", scheduledAt, direction: ParameterDirection.Input);

		Command = CreateCommand(sql, (DynamicParameters?)parameters, commandTimeout: sqlTimeOutSeconds, cancellationToken: cancellationToken);
		ResolveAsync = async conn => await conn.ExecuteAsync(Command).ConfigureAwait(false);
	}
}
