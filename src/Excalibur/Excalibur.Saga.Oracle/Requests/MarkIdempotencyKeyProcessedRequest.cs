// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using Dapper;

using Excalibur.Data;

using Oracle.ManagedDataAccess.Client;

namespace Excalibur.Saga.Oracle.Requests;

/// <summary>
/// Marks a saga idempotency key as processed. First-writer-wins via a plain INSERT whose unique
/// constraint (ORA-00001) makes a duplicate a no-op, returning 0 rows.
/// </summary>
internal sealed class MarkIdempotencyKeyProcessedRequest : DataRequestBase<IDbConnection, int>
{
	private const int OracleUniqueConstraintViolation = 1;

	/// <summary>
	/// Initializes a new instance of the <see cref="MarkIdempotencyKeyProcessedRequest"/> class.
	/// </summary>
	/// <param name="sagaId">The saga identifier.</param>
	/// <param name="idempotencyKey">The idempotency key to mark as processed.</param>
	/// <param name="qualifiedTableName">The fully qualified idempotency table name.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public MarkIdempotencyKeyProcessedRequest(
		string sagaId,
		string idempotencyKey,
		string qualifiedTableName,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(sagaId);
		ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);
		ArgumentException.ThrowIfNullOrWhiteSpace(qualifiedTableName);
		SagaSqlValidator.ThrowIfInvalidQualifiedName(qualifiedTableName);

		var sql = $"""
			INSERT INTO {qualifiedTableName} (SagaId, IdempotencyKey, ProcessedAt)
			VALUES (:SagaId, :IdempotencyKey, SYS_EXTRACT_UTC(SYSTIMESTAMP))
			""";

		var dp = new DynamicParameters();
		dp.Add("SagaId", sagaId);
		dp.Add("IdempotencyKey", idempotencyKey);

		Command = new CommandDefinition(sql, new OracleDynamicParameters(dp), cancellationToken: cancellationToken);
		ResolveAsync = async conn =>
		{
			try
			{
				return await conn.ExecuteAsync(Command).ConfigureAwait(false);
			}
			catch (OracleException ex) when (ex.Number == OracleUniqueConstraintViolation)
			{
				// Already marked by a concurrent processor (unique (SagaId, IdempotencyKey)) -> idempotent no-op.
				return 0;
			}
		};
	}
}
