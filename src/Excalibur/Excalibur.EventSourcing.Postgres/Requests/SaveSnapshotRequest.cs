// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Dapper;

using Excalibur.Data.Abstractions;
using Excalibur.Domain.Model;

namespace Excalibur.EventSourcing.Postgres.Requests;

/// <summary>
/// Data request to save (upsert) a snapshot for an aggregate.
/// Uses Postgres INSERT ... ON CONFLICT for atomic insert-or-update semantics.
/// </summary>
public sealed class SaveSnapshotRequest : DataRequestBase<IDbConnection, int>
{
	private const string Sql = """
		INSERT INTO event_store_snapshots (snapshot_id, aggregate_id, aggregate_type, version, data, created_at)
		VALUES (@SnapshotId, @AggregateId, @AggregateType, @Version, @Data, @CreatedAt)
		ON CONFLICT (aggregate_id, aggregate_type)
		DO UPDATE SET
		    snapshot_id = EXCLUDED.snapshot_id,
		    version = EXCLUDED.version,
		    data = EXCLUDED.data,
		    created_at = EXCLUDED.created_at
		""";

	/// <summary>
	/// Initializes a new instance of the <see cref="SaveSnapshotRequest"/> class.
	/// </summary>
	/// <param name="snapshot">The snapshot to save.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public SaveSnapshotRequest(
		ISnapshot snapshot,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(snapshot);

		var parameters = new DynamicParameters();
		parameters.Add("@SnapshotId", snapshot.SnapshotId);
		parameters.Add("@AggregateId", snapshot.AggregateId);
		parameters.Add("@AggregateType", snapshot.AggregateType);
		parameters.Add("@Version", snapshot.Version);
		parameters.Add("@Data", snapshot.Data, DbType.Binary);
		parameters.Add("@CreatedAt", snapshot.CreatedAt);

		Command = CreateCommand(Sql, parameters, cancellationToken: cancellationToken);

		ResolveAsync = async connection =>
			await connection.ExecuteAsync(Command).ConfigureAwait(false);
	}
}
