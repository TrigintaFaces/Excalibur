// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Dapper;

using Excalibur.Data.Abstractions;
using Excalibur.Domain.Model;

namespace Excalibur.EventSourcing.SqlServer.Requests;

/// <summary>
/// Data request to get the latest snapshot for an aggregate.
/// </summary>
public sealed class GetLatestSnapshotRequest : DataRequestBase<IDbConnection, ISnapshot?>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="GetLatestSnapshotRequest"/> class.
	/// </summary>
	/// <param name="aggregateId">The aggregate identifier.</param>
	/// <param name="aggregateType">The aggregate type name.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <param name="schema">The schema name for the snapshot store table. Default: "dbo".</param>
	/// <param name="table">The snapshot store table name. Default: "EventStoreSnapshots".</param>
	public GetLatestSnapshotRequest(
		string aggregateId,
		string aggregateType,
		CancellationToken cancellationToken,
		string schema = "dbo",
		string table = "EventStoreSnapshots")
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateType);

		var qualifiedTable = SqlTableName.Format(schema, table);

#pragma warning disable CA2100 // Schema and table validated by SqlIdentifierValidator in SqlTableName.Format
		var sql = $"""
			SELECT SnapshotId, AggregateId, AggregateType, Version, Data, CreatedAt
			FROM {qualifiedTable}
			WHERE AggregateId = @AggregateId AND AggregateType = @AggregateType
			""";
#pragma warning restore CA2100

		var parameters = new DynamicParameters();
		parameters.Add("@AggregateId", aggregateId);
		parameters.Add("@AggregateType", aggregateType);

		Command = CreateCommand(sql, parameters, cancellationToken: cancellationToken);

		ResolveAsync = async connection =>
		{
			var result = await connection.QuerySingleOrDefaultAsync<SnapshotData>(Command).ConfigureAwait(false);
			if (result == null)
			{
				return null;
			}

			return new Snapshot
			{
				SnapshotId = result.SnapshotId ?? Guid.NewGuid().ToString(),
				AggregateId = result.AggregateId,
				AggregateType = result.AggregateType,
				Version = result.Version,
				Data = result.Data,
				CreatedAt = new DateTimeOffset(DateTime.SpecifyKind(result.CreatedAt, DateTimeKind.Utc), TimeSpan.Zero),
			};
		};
	}

	private sealed record SnapshotData(
		string? SnapshotId,
		string AggregateId,
		string AggregateType,
		long Version,
		byte[] Data,
		DateTime CreatedAt);
}
