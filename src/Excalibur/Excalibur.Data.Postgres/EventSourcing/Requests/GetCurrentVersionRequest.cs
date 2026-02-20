// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Dapper;

using Excalibur.Data.Abstractions;

namespace Excalibur.Data.Postgres.EventSourcing;

/// <summary>
/// Data request to get the current version of an aggregate stream in Postgres.
/// </summary>
public sealed class GetCurrentVersionRequest : DataRequestBase<IDbConnection, long>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="GetCurrentVersionRequest"/> class.
	/// </summary>
	/// <param name="aggregateId">The aggregate identifier.</param>
	/// <param name="aggregateType">The aggregate type name.</param>
	/// <param name="schemaName">The database schema name.</param>
	/// <param name="tableName">The events table name.</param>
	/// <param name="transaction">Optional transaction to participate in.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public GetCurrentVersionRequest(
		string aggregateId,
		string aggregateType,
		string schemaName,
		string tableName,
		IDbTransaction? transaction,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateId);
		ArgumentException.ThrowIfNullOrWhiteSpace(aggregateType);
		ArgumentException.ThrowIfNullOrWhiteSpace(schemaName);
		ArgumentException.ThrowIfNullOrWhiteSpace(tableName);

		var sql = $"""
			SELECT COALESCE(MAX(version), -1)
			FROM {schemaName}.{tableName}
			WHERE aggregate_id = @AggregateId AND aggregate_type = @AggregateType
			""";

		var parameters = new DynamicParameters();
		parameters.Add("@AggregateId", aggregateId);
		parameters.Add("@AggregateType", aggregateType);

		Command = CreateCommand(sql, parameters, transaction, cancellationToken: cancellationToken);

		ResolveAsync = async connection =>
			await connection.ExecuteScalarAsync<long>(Command).ConfigureAwait(false);
	}
}
