// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Dapper;

using Excalibur.Data;

namespace Excalibur.Outbox.Oracle;

/// <summary>
/// Represents a data request to move an outbox message to the dead letter table in the Oracle database.
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
		// Oracle: two DML statements must run in a single PL/SQL anonymous block (no client-side ';' batching).
		var sql = $"""
		   BEGIN
		           INSERT INTO {deadLetterTableName} (message_id, message_type, message_metadata, message_body, occurred_on, attempts, error_message)
		           SELECT message_id, message_type, message_metadata, message_body, occurred_on, attempts + 1, :ErrorMessage
		           FROM {outboxTableName}
		           WHERE message_id = :MessageIdInsert;

		           DELETE FROM {outboxTableName}
		           WHERE message_id = :MessageIdDelete;
		   END;
		   """;

		// ODP.NET binds positionally (this store does not set BindByName), so a placeholder appearing more
		// than once consumes one parameter PER occurrence and parameters must be added in textual order.
		// The reused message_id is given a distinct name per occurrence, both bound to the same id, so the
		// block binds correctly regardless of BindByName.
		var parameters = new DynamicParameters();
		parameters.Add("ErrorMessage", string.Empty, direction: ParameterDirection.Input);
		parameters.Add("MessageIdInsert", messageId, direction: ParameterDirection.Input);
		parameters.Add("MessageIdDelete", messageId, direction: ParameterDirection.Input);

		Command = CreateCommand(sql, (DynamicParameters?)parameters, commandTimeout: sqlTimeOutSeconds, cancellationToken: cancellationToken);
		ResolveAsync = async conn => await conn.ExecuteAsync(Command).ConfigureAwait(false);
	}
}
