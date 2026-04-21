// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Dapper;

using Excalibur.Data.Abstractions;

namespace Excalibur.EventSourcing.Postgres.Requests;

/// <summary>
/// Data request to delete all snapshots for an aggregate from the Postgres snapshot store.
/// </summary>
public sealed class DeleteSnapshotsRequest : DataRequestBase<IDbConnection, int>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="DeleteSnapshotsRequest"/> class.
	/// </summary>
	/// <param name="aggregateId">The aggregate identifier.</param>
	/// <param name="aggregateType">The aggregate type name.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <param name="schema">The schema name for the snapshot store table. Default: "public".</param>
	/// <param name="table">The snapshot store table name. Default: "event_store_snapshots".</param>
	public DeleteSnapshotsRequest(
		string aggregateId,
		string aggregateType,
		CancellationToken cancellationToken,
		string schema = "public",
		string table = "event_store_snapshots")
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateType);

		var qualifiedTable = PgTableName.Format(schema, table);

#pragma warning disable CA2100 // Schema and table validated by SqlIdentifierValidator in PgTableName.Format
		var sql = $"""
			DELETE FROM {qualifiedTable}
			WHERE aggregate_id = @AggregateId AND aggregate_type = @AggregateType
			""";
#pragma warning restore CA2100

		var parameters = new DynamicParameters();
		parameters.Add("@AggregateId", aggregateId);
		parameters.Add("@AggregateType", aggregateType);

		Command = CreateCommand(sql, parameters, cancellationToken: cancellationToken);

		ResolveAsync = async connection =>
			await connection.ExecuteAsync(Command).ConfigureAwait(false);
	}
}
