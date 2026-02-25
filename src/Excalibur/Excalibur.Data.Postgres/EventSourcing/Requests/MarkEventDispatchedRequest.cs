// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Dapper;

using Excalibur.Data.Abstractions;

namespace Excalibur.Data.Postgres.EventSourcing;

/// <summary>
/// Data request to mark an event as dispatched in the Postgres event store.
/// </summary>
public sealed class MarkEventDispatchedRequest : DataRequestBase<IDbConnection, int>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="MarkEventDispatchedRequest"/> class.
	/// </summary>
	/// <param name="eventId">The unique event identifier.</param>
	/// <param name="schemaName">The database schema name.</param>
	/// <param name="tableName">The events table name.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public MarkEventDispatchedRequest(
		string eventId,
		string schemaName,
		string tableName,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
		ArgumentException.ThrowIfNullOrWhiteSpace(schemaName);
		ArgumentException.ThrowIfNullOrWhiteSpace(tableName);

		var sql = $"UPDATE {schemaName}.{tableName} SET is_dispatched = true WHERE event_id = @EventId";

		var parameters = new DynamicParameters();
		parameters.Add("@EventId", eventId);

		Command = CreateCommand(sql, parameters, cancellationToken: cancellationToken);

		ResolveAsync = async connection =>
			await connection.ExecuteAsync(Command).ConfigureAwait(false);
	}
}
