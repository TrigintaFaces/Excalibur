// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Dapper;

using Excalibur.Data.Abstractions;
using Excalibur.Domain.Model;

namespace Excalibur.Data.Postgres.Snapshots;

/// <summary>
/// Data request to save (upsert) a snapshot for an aggregate in Postgres.
/// Uses INSERT ON CONFLICT for atomic insert-or-update semantics.
/// </summary>
/// <remarks>
/// <para>
/// Postgres's INSERT ON CONFLICT is used instead of SQL Server's MERGE statement.
/// The WHERE clause ensures older snapshots don't overwrite newer ones.
/// </para>
/// </remarks>
public sealed class SaveSnapshotRequest : DataRequestBase<IDbConnection, int>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="SaveSnapshotRequest"/> class.
	/// </summary>
	/// <param name="snapshot">The snapshot to save.</param>
	/// <param name="schemaName">The database schema name.</param>
	/// <param name="tableName">The snapshots table name.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public SaveSnapshotRequest(
		ISnapshot snapshot,
		string schemaName,
		string tableName,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(snapshot);
		ArgumentException.ThrowIfNullOrWhiteSpace(schemaName);
		ArgumentException.ThrowIfNullOrWhiteSpace(tableName);

		// Postgres upsert with INSERT ON CONFLICT
		// The WHERE clause ensures older snapshots don't overwrite newer ones
		var sql = $"""
			INSERT INTO {schemaName}.{tableName} (
			    snapshot_id, aggregate_id, aggregate_type, version,
			    snapshot_type, data, metadata, created_at
			)
			VALUES (
			    @SnapshotId, @AggregateId, @AggregateType, @Version,
			    @SnapshotType, @Data, @Metadata, @CreatedAt
			)
			ON CONFLICT (aggregate_id, aggregate_type)
			DO UPDATE SET
			    snapshot_id = EXCLUDED.snapshot_id,
			    version = EXCLUDED.version,
			    snapshot_type = EXCLUDED.snapshot_type,
			    data = EXCLUDED.data,
			    metadata = EXCLUDED.metadata,
			    created_at = EXCLUDED.created_at
			WHERE {tableName}.version < EXCLUDED.version
			""";

		var parameters = new DynamicParameters();
		parameters.Add("@SnapshotId", Guid.TryParse(snapshot.SnapshotId, out var snapshotGuid) ? snapshotGuid : Guid.NewGuid());
		parameters.Add("@AggregateId", snapshot.AggregateId);
		parameters.Add("@AggregateType", snapshot.AggregateType);
		parameters.Add("@Version", snapshot.Version);
		parameters.Add("@SnapshotType", snapshot.GetType().AssemblyQualifiedName ?? snapshot.GetType().FullName ?? "Unknown");
		parameters.Add("@Data", snapshot.Data, DbType.Binary);
		parameters.Add("@Metadata", null, DbType.Binary);
		parameters.Add("@CreatedAt", snapshot.CreatedAt);

		Command = CreateCommand(sql, parameters, cancellationToken: cancellationToken);

		ResolveAsync = async connection =>
			await connection.ExecuteAsync(Command).ConfigureAwait(false);
	}
}
