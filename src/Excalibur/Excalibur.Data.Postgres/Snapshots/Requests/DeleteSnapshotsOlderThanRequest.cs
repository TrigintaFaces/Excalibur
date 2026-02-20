// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Dapper;

using Excalibur.Data.Abstractions;

namespace Excalibur.Data.Postgres.Snapshots;

/// <summary>
/// Data request to delete snapshots older than a specific version from Postgres.
/// </summary>
public sealed class DeleteSnapshotsOlderThanRequest : DataRequestBase<IDbConnection, int>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="DeleteSnapshotsOlderThanRequest"/> class.
	/// </summary>
	/// <param name="aggregateId">The aggregate identifier.</param>
	/// <param name="aggregateType">The aggregate type name.</param>
	/// <param name="olderThanVersion">Delete snapshots with version less than this value.</param>
	/// <param name="schemaName">The database schema name.</param>
	/// <param name="tableName">The snapshots table name.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public DeleteSnapshotsOlderThanRequest(
		string aggregateId,
		string aggregateType,
		long olderThanVersion,
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
			WHERE aggregate_id = @AggregateId
			  AND aggregate_type = @AggregateType
			  AND version < @Version
			""";

		var parameters = new DynamicParameters();
		parameters.Add("@AggregateId", aggregateId);
		parameters.Add("@AggregateType", aggregateType);
		parameters.Add("@Version", olderThanVersion);

		Command = CreateCommand(sql, parameters, cancellationToken: cancellationToken);

		ResolveAsync = async connection =>
			await connection.ExecuteAsync(Command).ConfigureAwait(false);
	}
}
