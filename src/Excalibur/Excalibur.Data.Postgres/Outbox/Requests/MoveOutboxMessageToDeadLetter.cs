// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Dapper;

using Excalibur.Data.Abstractions;

namespace Excalibur.Data.Postgres.Outbox;

/// <summary>
/// Represents a data request to move an outbox message to the dead letter table in the Postgres database.
/// </summary>
public sealed class MoveOutboxMessageToDeadLetter : DataRequest<int>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="MoveOutboxMessageToDeadLetter"/> class.
	/// </summary>
	/// <param name="messageId">The unique identifier of the message to move.</param>
	/// <param name="outboxTableName">The name of the outbox table.</param>
	/// <param name="deadLetterTableName">The name of the dead letter table.</param>
	/// <param name="sqlTimeOutSeconds">The SQL command timeout in seconds.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public MoveOutboxMessageToDeadLetter(
		string messageId,
		string outboxTableName,
		string deadLetterTableName,
		int sqlTimeOutSeconds,
		CancellationToken cancellationToken)
	{
		var sql = $"""
		   INSERT INTO {deadLetterTableName} (message_id, message_type, message_metadata, message_body, occurred_on, attempts, error_message)
		           SELECT message_id, message_type, message_metadata, message_body, occurred_on, attempts + 1, @ErrorMessage
		           FROM {outboxTableName}
		           WHERE message_id = @MessageId;

		           DELETE FROM {outboxTableName}
		           WHERE message_id = @MessageId;
		   """;

		var parameters = new DynamicParameters();
		parameters.Add("MessageId", messageId, direction: ParameterDirection.Input);
		parameters.Add("ErrorMessage", string.Empty, direction: ParameterDirection.Input);

		Command = CreateCommand(sql, (DynamicParameters?)parameters, commandTimeout: sqlTimeOutSeconds, cancellationToken: cancellationToken);
		ResolveAsync = async conn => await conn.ExecuteAsync(Command).ConfigureAwait(false);
	}
}
