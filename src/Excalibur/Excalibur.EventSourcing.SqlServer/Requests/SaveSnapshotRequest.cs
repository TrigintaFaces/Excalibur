// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Dapper;

using Excalibur.Data.Abstractions;
using Excalibur.Domain.Model;

namespace Excalibur.EventSourcing.SqlServer.Requests;

/// <summary>
/// Data request to save (upsert) a snapshot for an aggregate.
/// Uses MERGE for atomic insert-or-update semantics.
/// </summary>
public sealed class SaveSnapshotRequest : DataRequestBase<IDbConnection, int>
{
	private const string Sql = """
		MERGE INTO EventStoreSnapshots WITH (HOLDLOCK) AS target
		USING (SELECT @SnapshotId, @AggregateId, @AggregateType, @Version, @Data, @CreatedAt)
		    AS source (SnapshotId, AggregateId, AggregateType, Version, Data, CreatedAt)
		ON target.AggregateId = source.AggregateId
		   AND target.AggregateType = source.AggregateType
		WHEN MATCHED THEN
		    UPDATE SET
		        SnapshotId = source.SnapshotId,
		        Version = source.Version,
		        Data = source.Data,
		        CreatedAt = source.CreatedAt
		WHEN NOT MATCHED THEN
		    INSERT (SnapshotId, AggregateId, AggregateType, Version, Data, CreatedAt)
		    VALUES (source.SnapshotId, source.AggregateId, source.AggregateType,
		            source.Version, source.Data, source.CreatedAt);
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
		parameters.Add("@Data", snapshot.Data);
		parameters.Add("@CreatedAt", snapshot.CreatedAt);

		Command = CreateCommand(Sql, parameters, cancellationToken: cancellationToken);

		ResolveAsync = async connection =>
			await connection.ExecuteAsync(Command).ConfigureAwait(false);
	}
}
