// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Dapper;

using Excalibur.Data.Abstractions;

namespace Excalibur.EventSourcing.Postgres.Requests;

/// <summary>
/// Data request to delete published outbox messages older than a specified retention period from the Postgres outbox.
/// </summary>
public sealed class DeletePublishedOutboxMessagesRequest : DataRequestBase<IDbConnection, int>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="DeletePublishedOutboxMessagesRequest"/> class.
	/// </summary>
	/// <param name="retentionPeriod">The retention period for published messages.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <param name="schema">The schema name for the outbox table. Default: "public".</param>
	/// <param name="table">The outbox table name. Default: "event_sourced_outbox".</param>
	public DeletePublishedOutboxMessagesRequest(
		TimeSpan retentionPeriod,
		CancellationToken cancellationToken,
		string schema = "public",
		string table = "event_sourced_outbox")
	{
		var qualifiedTable = PgTableName.Format(schema, table);

#pragma warning disable CA2100 // Schema and table validated by SqlIdentifierValidator in PgTableName.Format
		var sql = $"""
			DELETE FROM {qualifiedTable}
			WHERE published_at IS NOT NULL
			  AND published_at < @CutoffDate
			""";
#pragma warning restore CA2100

		var parameters = new DynamicParameters();
		parameters.Add("@CutoffDate", DateTimeOffset.UtcNow - retentionPeriod);

		Command = CreateCommand(sql, parameters, cancellationToken: cancellationToken);

		ResolveAsync = async connection =>
			await connection.ExecuteAsync(Command).ConfigureAwait(false);
	}
}
