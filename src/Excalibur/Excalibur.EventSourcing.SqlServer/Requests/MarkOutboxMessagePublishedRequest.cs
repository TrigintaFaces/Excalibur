// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Dapper;

using Excalibur.Data.Abstractions;

namespace Excalibur.EventSourcing.SqlServer.Requests;

/// <summary>
/// Data request to mark an outbox message as published.
/// </summary>
public sealed class MarkOutboxMessagePublishedRequest : DataRequestBase<IDbConnection, int>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="MarkOutboxMessagePublishedRequest"/> class.
	/// </summary>
	/// <param name="messageId">The unique identifier of the message to mark as published.</param>
	/// <param name="transaction">Optional database transaction.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <param name="schema">The schema name for the outbox table. Default: "dbo".</param>
	/// <param name="table">The outbox table name. Default: "EventSourcedOutbox".</param>
	public MarkOutboxMessagePublishedRequest(
		Guid messageId,
		IDbTransaction? transaction,
		CancellationToken cancellationToken,
		string schema = "dbo",
		string table = "EventSourcedOutbox")
	{
		var qualifiedTable = SqlTableName.Format(schema, table);

#pragma warning disable CA2100 // Schema and table validated by SqlIdentifierValidator in SqlTableName.Format
		var sql = $"""
			UPDATE {qualifiedTable}
			SET PublishedAt = @PublishedAt
			WHERE Id = @Id AND PublishedAt IS NULL
			""";
#pragma warning restore CA2100

		var parameters = new DynamicParameters();
		parameters.Add("@Id", messageId);
		parameters.Add("@PublishedAt", DateTimeOffset.UtcNow);

		Command = CreateCommand(sql, parameters, transaction: transaction, cancellationToken: cancellationToken);

		ResolveAsync = async connection =>
			await connection.ExecuteAsync(Command).ConfigureAwait(false);
	}
}
