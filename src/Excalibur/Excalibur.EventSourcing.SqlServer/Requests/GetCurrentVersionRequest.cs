// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Dapper;

using Excalibur.Data.Abstractions;

namespace Excalibur.EventSourcing.SqlServer.Requests;

/// <summary>
/// Data request to get the current version of an aggregate stream.
/// </summary>
public sealed class GetCurrentVersionRequest : DataRequestBase<IDbConnection, long>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="GetCurrentVersionRequest"/> class.
	/// </summary>
	/// <param name="aggregateId">The aggregate identifier.</param>
	/// <param name="aggregateType">The aggregate type name.</param>
	/// <param name="transaction">Optional transaction to participate in.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <param name="schema">The schema name for the event store table. Default: "dbo".</param>
	/// <param name="table">The event store table name. Default: "EventStoreEvents".</param>
	public GetCurrentVersionRequest(
		string aggregateId,
		string aggregateType,
		IDbTransaction? transaction,
		CancellationToken cancellationToken,
		string schema = "dbo",
		string table = "EventStoreEvents")
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateType);

		var qualifiedTable = SqlTableName.Format(schema, table);

#pragma warning disable CA2100 // Schema and table validated by SqlIdentifierValidator in SqlTableName.Format
		var sql = $"""
			SELECT ISNULL(MAX(Version), -1)
			FROM {qualifiedTable}
			WHERE AggregateId = @AggregateId AND AggregateType = @AggregateType
			""";
#pragma warning restore CA2100

		var parameters = new DynamicParameters();
		parameters.Add("@AggregateId", aggregateId);
		parameters.Add("@AggregateType", aggregateType);

		Command = CreateCommand(sql, parameters, transaction, cancellationToken: cancellationToken);

		ResolveAsync = async connection =>
			await connection.ExecuteScalarAsync<long>(Command).ConfigureAwait(false);
	}
}
