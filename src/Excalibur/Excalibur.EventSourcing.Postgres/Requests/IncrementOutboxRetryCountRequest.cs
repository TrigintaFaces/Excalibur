// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Dapper;

using Excalibur.Data.Abstractions;

namespace Excalibur.EventSourcing.Postgres.Requests;

/// <summary>
/// Data request to increment the retry count for an outbox message in the Postgres outbox.
/// </summary>
public sealed class IncrementOutboxRetryCountRequest : DataRequestBase<IDbConnection, int>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="IncrementOutboxRetryCountRequest"/> class.
	/// </summary>
	/// <param name="messageId">The unique identifier of the message.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <param name="schema">The schema name for the outbox table. Default: "public".</param>
	/// <param name="table">The outbox table name. Default: "event_sourced_outbox".</param>
	public IncrementOutboxRetryCountRequest(
		Guid messageId,
		CancellationToken cancellationToken,
		string schema = "public",
		string table = "event_sourced_outbox")
	{
		var qualifiedTable = PgTableName.Format(schema, table);

#pragma warning disable CA2100 // Schema and table validated by SqlIdentifierValidator in PgTableName.Format
		var sql = $"""
			UPDATE {qualifiedTable}
			SET retry_count = retry_count + 1
			WHERE id = @Id
			""";
#pragma warning restore CA2100

		var parameters = new DynamicParameters();
		parameters.Add("@Id", messageId);

		Command = CreateCommand(sql, parameters, cancellationToken: cancellationToken);

		ResolveAsync = async connection =>
			await connection.ExecuteAsync(Command).ConfigureAwait(false);
	}
}
