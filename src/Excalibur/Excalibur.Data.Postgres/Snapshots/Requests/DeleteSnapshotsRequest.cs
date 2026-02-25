// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Dapper;

using Excalibur.Data.Abstractions;

namespace Excalibur.Data.Postgres.Snapshots;

/// <summary>
/// Data request to delete all snapshots for an aggregate from Postgres.
/// </summary>
public sealed class DeleteSnapshotsRequest : DataRequestBase<IDbConnection, int>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="DeleteSnapshotsRequest"/> class.
	/// </summary>
	/// <param name="aggregateId">The aggregate identifier.</param>
	/// <param name="aggregateType">The aggregate type name.</param>
	/// <param name="schemaName">The database schema name.</param>
	/// <param name="tableName">The snapshots table name.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public DeleteSnapshotsRequest(
		string aggregateId,
		string aggregateType,
		string schemaName,
		string tableName,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateType);
		ArgumentException.ThrowIfNullOrWhiteSpace(schemaName);
		ArgumentException.ThrowIfNullOrWhiteSpace(tableName);

		var sql = $"""
			DELETE FROM {schemaName}.{tableName}
			WHERE aggregate_id = @AggregateId AND aggregate_type = @AggregateType
			""";

		var parameters = new DynamicParameters();
		parameters.Add("@AggregateId", aggregateId);
		parameters.Add("@AggregateType", aggregateType);

		Command = CreateCommand(sql, parameters, cancellationToken: cancellationToken);

		ResolveAsync = async connection =>
			await connection.ExecuteAsync(Command).ConfigureAwait(false);
	}
}
