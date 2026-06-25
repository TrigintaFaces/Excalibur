// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;
using System.Text.Json;

using Dapper;

using Excalibur.Data;
using Excalibur.Domain.Model;

namespace Excalibur.EventSourcing.SqlServer.Requests;

/// <summary>
/// Data request to save (upsert) a snapshot for an aggregate.
/// Uses MERGE for atomic insert-or-update semantics.
/// </summary>
public sealed class SaveSnapshotRequest : DataRequestBase<IDbConnection, int>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="SaveSnapshotRequest"/> class.
	/// </summary>
	/// <param name="snapshot">The snapshot to save.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <param name="schema">The schema name for the snapshot store table. Default: "dbo".</param>
	/// <param name="table">The snapshot store table name. Default: "EventStoreSnapshots".</param>
	public SaveSnapshotRequest(
		ISnapshot snapshot,
		CancellationToken cancellationToken,
		string schema = "dbo",
		string table = "EventStoreSnapshots")
	{
		ArgumentNullException.ThrowIfNull(snapshot);

		var qualifiedTable = SqlTableName.Format(schema, table);

#pragma warning disable CA2100 // Schema and table validated by SqlIdentifierValidator in SqlTableName.Format
		var sql = $"""
			MERGE INTO {qualifiedTable} WITH (HOLDLOCK, ROWLOCK, UPDLOCK) AS target
			USING (SELECT @SnapshotId, @AggregateId, @AggregateType, @Version, @Data, @CreatedAt, @Metadata)
			    AS source (SnapshotId, AggregateId, AggregateType, Version, Data, CreatedAt, Metadata)
			ON target.AggregateId = source.AggregateId
			   AND target.AggregateType = source.AggregateType
			WHEN MATCHED AND source.Version > target.Version THEN
			    UPDATE SET
			        SnapshotId = source.SnapshotId,
			        Version = source.Version,
			        Data = source.Data,
			        CreatedAt = source.CreatedAt,
			        Metadata = source.Metadata
			WHEN NOT MATCHED THEN
			    INSERT (SnapshotId, AggregateId, AggregateType, Version, Data, CreatedAt, Metadata)
			    VALUES (source.SnapshotId, source.AggregateId, source.AggregateType,
			            source.Version, source.Data, source.CreatedAt, source.Metadata);
			""";
#pragma warning restore CA2100

		var parameters = new DynamicParameters();
		parameters.Add("@SnapshotId", snapshot.SnapshotId);
		parameters.Add("@AggregateId", snapshot.AggregateId);
		parameters.Add("@AggregateType", snapshot.AggregateType);
		parameters.Add("@Version", snapshot.Version);
		parameters.Add("@Data", snapshot.Data.ToArray());
		parameters.Add("@CreatedAt", snapshot.CreatedAt);
		parameters.Add("@Metadata", SerializeMetadata(snapshot.Metadata), DbType.Binary);

		Command = CreateCommand(sql, parameters, cancellationToken: cancellationToken);

		ResolveAsync = async connection =>
			await connection.ExecuteAsync(Command).ConfigureAwait(false);
	}

	/// <summary>
	/// Serializes the snapshot metadata dictionary to a binary payload for storage so that the
	/// schema-version entry consumed by snapshot upgrading round-trips. Null metadata is stored as
	/// SQL NULL; an empty dictionary is preserved as empty.
	/// </summary>
	private static byte[]? SerializeMetadata(IDictionary<string, object>? metadata)
	{
		if (metadata is null)
		{
			return null;
		}

#pragma warning disable IL2026, IL3050 // Metadata serialization inherently uses reflection (matches SqlServerEventStore precedent)
		return JsonSerializer.SerializeToUtf8Bytes(metadata);
#pragma warning restore IL2026, IL3050
	}
}
