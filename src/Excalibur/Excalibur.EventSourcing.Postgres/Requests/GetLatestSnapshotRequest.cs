// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Dapper;

using Excalibur.Data.Abstractions;
using Excalibur.Domain.Model;

namespace Excalibur.EventSourcing.Postgres.Requests;

/// <summary>
/// Data request to get the latest snapshot for an aggregate from the Postgres snapshot store.
/// </summary>
public sealed class GetLatestSnapshotRequest : DataRequestBase<IDbConnection, ISnapshot?>
{
	private const string Sql = """
		SELECT snapshot_id AS SnapshotId, aggregate_id AS AggregateId, aggregate_type AS AggregateType,
		       version AS Version, data AS Data, created_at AS CreatedAt
		FROM event_store_snapshots
		WHERE aggregate_id = @AggregateId AND aggregate_type = @AggregateType
		""";

	/// <summary>
	/// Initializes a new instance of the <see cref="GetLatestSnapshotRequest"/> class.
	/// </summary>
	/// <param name="aggregateId">The aggregate identifier.</param>
	/// <param name="aggregateType">The aggregate type name.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public GetLatestSnapshotRequest(
		string aggregateId,
		string aggregateType,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateType);

		var parameters = new DynamicParameters();
		parameters.Add("@AggregateId", aggregateId);
		parameters.Add("@AggregateType", aggregateType);

		Command = CreateCommand(Sql, parameters, cancellationToken: cancellationToken);

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
