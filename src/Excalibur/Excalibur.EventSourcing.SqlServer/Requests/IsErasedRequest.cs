// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using Dapper;

using Excalibur.Data.Abstractions;

namespace Excalibur.EventSourcing.SqlServer.Requests;

/// <summary>
/// Data request to check whether events for an aggregate have been erased (tombstoned).
/// </summary>
internal sealed class IsErasedRequest : DataRequestBase<IDbConnection, bool>
{
	private const string Sql = """
		SELECT CASE WHEN EXISTS (
		    SELECT 1 FROM EventStoreEvents
		    WHERE AggregateId = @AggregateId
		      AND AggregateType = @AggregateType
		      AND EventType = '$erased'
		) THEN 1 ELSE 0 END
		""";

	public IsErasedRequest(
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
			await connection.ExecuteScalarAsync<bool>(Command).ConfigureAwait(false);
	}
}
