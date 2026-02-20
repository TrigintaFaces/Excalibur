// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Dapper;

using Excalibur.Data.Abstractions;

namespace Excalibur.Data.Postgres.Outbox;

/// <summary>
/// Represents a data request to reset outbox message reservations for a specific dispatcher in the Postgres database.
/// </summary>
public sealed class ResetOutboxMessageReservation : DataRequest<int>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="ResetOutboxMessageReservation"/> class.
	/// </summary>
	/// <param name="dispatcherId">The unique identifier of the dispatcher whose reservations should be reset.</param>
	/// <param name="outboxTableName">The name of the outbox table.</param>
	/// <param name="sqlTimeOutSeconds">The SQL command timeout in seconds.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public ResetOutboxMessageReservation(string dispatcherId, string outboxTableName, int sqlTimeOutSeconds,
		CancellationToken cancellationToken)
	{
		var sql = $"""
		   UPDATE {outboxTableName}
		           SET dispatcher_id = NULL,
		           dispatcher_timeout = NULL
		           WHERE dispatcher_id = @DispatcherId;
		   """;

		var parameters = new DynamicParameters();
		parameters.Add("DispatcherId", dispatcherId, direction: ParameterDirection.Input);

		Command = CreateCommand(sql, (DynamicParameters?)parameters, commandTimeout: sqlTimeOutSeconds, cancellationToken: cancellationToken);
		ResolveAsync = async conn => await conn.ExecuteAsync(Command).ConfigureAwait(false);
	}
}
