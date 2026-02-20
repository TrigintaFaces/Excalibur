// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Dapper;

using Excalibur.Data.Abstractions;

namespace Excalibur.Data.Postgres.Outbox;

/// <summary>
/// Represents a data request to increment the attempt count of an outbox message in the Postgres database.
/// </summary>
public sealed class IncrementOutboxMessageAttempts : DataRequest<int>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="IncrementOutboxMessageAttempts"/> class.
	/// </summary>
	/// <param name="messageId">The unique identifier of the message to increment attempts for.</param>
	/// <param name="outboxTableName">The name of the outbox table.</param>
	/// <param name="sqlTimeOutSeconds">The SQL command timeout in seconds.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public IncrementOutboxMessageAttempts(string messageId, string outboxTableName, int sqlTimeOutSeconds,
		CancellationToken cancellationToken)
	{
		var sql = $"""
		   UPDATE {outboxTableName}
		           SET attempts = attempts + 1
		           WHERE message_id = @MessageId;
		   """;

		var parameters = new DynamicParameters();
		parameters.Add("MessageId", messageId, direction: ParameterDirection.Input);

		Command = CreateCommand(sql, (DynamicParameters?)parameters, commandTimeout: sqlTimeOutSeconds, cancellationToken: cancellationToken);
		ResolveAsync = async conn => await conn.ExecuteAsync(Command).ConfigureAwait(false);
	}
}
