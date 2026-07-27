// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using Dapper;

using Excalibur.Data;

namespace Excalibur.Saga.Oracle.Requests;

/// <summary>
/// Checks whether a saga idempotency key has already been processed.
/// </summary>
internal sealed class IsIdempotencyKeyProcessedRequest : DataRequestBase<IDbConnection, bool>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="IsIdempotencyKeyProcessedRequest"/> class.
	/// </summary>
	/// <param name="sagaId">The saga identifier.</param>
	/// <param name="idempotencyKey">The idempotency key to check.</param>
	/// <param name="qualifiedTableName">The fully qualified idempotency table name.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public IsIdempotencyKeyProcessedRequest(
		string sagaId,
		string idempotencyKey,
		string qualifiedTableName,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(sagaId);
		ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
		ArgumentException.ThrowIfNullOrWhiteSpace(qualifiedTableName);
		SagaSqlValidator.ThrowIfInvalidQualifiedName(qualifiedTableName);

		// Oracle requires a FROM DUAL for a scalar SELECT with an EXISTS predicate.
		var sql = $"""
			SELECT CASE WHEN EXISTS (
				SELECT 1 FROM {qualifiedTableName}
				WHERE SagaId = :SagaId AND IdempotencyKey = :IdempotencyKey
			) THEN 1 ELSE 0 END
			FROM DUAL
			""";

		var dp = new DynamicParameters();
		dp.Add("SagaId", sagaId);
		dp.Add("IdempotencyKey", idempotencyKey);

		Command = new CommandDefinition(sql, new OracleDynamicParameters(dp), cancellationToken: cancellationToken);
		ResolveAsync = async conn =>
		{
			var result = await conn.ExecuteScalarAsync<int>(Command).ConfigureAwait(false);
			return result == 1;
		};
	}
}
