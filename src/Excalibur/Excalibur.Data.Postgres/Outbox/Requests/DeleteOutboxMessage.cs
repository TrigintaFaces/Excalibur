// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Dapper;

using Excalibur.Data.Abstractions;

namespace Excalibur.Data.Postgres.Outbox;

/// <summary>
/// Represents a data request to delete an outbox message from the Postgres database.
/// </summary>
public sealed class DeleteOutboxMessage : DataRequest<int>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="DeleteOutboxMessage"/> class.
	/// </summary>
	/// <param name="messageId">The unique identifier of the message to delete.</param>
	/// <param name="outboxTableName">The name of the outbox table.</param>
	/// <param name="sqlTimeOutSeconds">The SQL command timeout in seconds.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public DeleteOutboxMessage(string messageId, string outboxTableName, int sqlTimeOutSeconds,
		CancellationToken cancellationToken)
	{
		var sql = $"""
		   DELETE FROM {outboxTableName}
		           WHERE message_id = @MessageId;
		   """;

		var parameters = new DynamicParameters();
		parameters.Add("MessageId", messageId, direction: ParameterDirection.Input);

		Command = CreateCommand(sql, (DynamicParameters?)parameters, commandTimeout: sqlTimeOutSeconds, cancellationToken: cancellationToken);

		ResolveAsync = async conn => await conn.ExecuteAsync(Command).ConfigureAwait(false);
	}
}
