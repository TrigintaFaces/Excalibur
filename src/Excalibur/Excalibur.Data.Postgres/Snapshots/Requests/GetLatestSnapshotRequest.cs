// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Dapper;

using Excalibur.Data.Abstractions;
using Excalibur.Domain.Model;

namespace Excalibur.Data.Postgres.Snapshots;

/// <summary>
/// Data request to get the latest snapshot for an aggregate from Postgres.
/// </summary>
public sealed class GetLatestSnapshotRequest : DataRequestBase<IDbConnection, ISnapshot?>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="GetLatestSnapshotRequest"/> class.
	/// </summary>
	/// <param name="aggregateId">The aggregate identifier.</param>
	/// <param name="aggregateType">The aggregate type name.</param>
	/// <param name="schemaName">The database schema name.</param>
	/// <param name="tableName">The snapshots table name.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public GetLatestSnapshotRequest(
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
			SELECT snapshot_id, aggregate_id, aggregate_type, version, snapshot_type, data, metadata, created_at
			FROM {schemaName}.{tableName}
			WHERE aggregate_id = @AggregateId AND aggregate_type = @AggregateType
			""";

		var parameters = new DynamicParameters();
		parameters.Add("@AggregateId", aggregateId);
		parameters.Add("@AggregateType", aggregateType);

		Command = CreateCommand(sql, parameters, cancellationToken: cancellationToken);

		ResolveAsync = async connection =>
		{
			var result = await connection.QuerySingleOrDefaultAsync<SnapshotDto>(Command).ConfigureAwait(false);
			if (result == null)
			{
				return null;
			}

			return new Snapshot
			{
				SnapshotId = result.SnapshotId.ToString(),
				AggregateId = result.AggregateId,
				AggregateType = result.AggregateType,
				Version = result.Version,
				Data = result.Data,
				CreatedAt = new DateTimeOffset(DateTime.SpecifyKind(result.CreatedAt, DateTimeKind.Utc), TimeSpan.Zero),
			};
		};
	}

	/// <summary>
	/// DTO for Dapper materialization from Postgres.
	/// Uses class with settable properties instead of record because Dapper
	/// requires either a parameterless constructor or exact parameter name matching.
	/// Postgres returns snake_case column names which don't match PascalCase parameters.
	/// </summary>
	private sealed class SnapshotDto
	{
		public Guid SnapshotId { get; set; }

		public string AggregateId { get; set; } = string.Empty;

		public string AggregateType { get; set; } = string.Empty;

		public long Version { get; set; }

		public string SnapshotType { get; set; } = string.Empty;

		public byte[] Data { get; set; } = [];

		public byte[]? Metadata { get; set; }

		public DateTime CreatedAt { get; set; }
	}
}
