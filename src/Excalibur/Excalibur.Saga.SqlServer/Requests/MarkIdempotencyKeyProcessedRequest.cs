// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;

using Dapper;

using Excalibur.Data.Abstractions;

namespace Excalibur.Saga.SqlServer.Requests;

/// <summary>
/// Marks a saga idempotency key as processed using an idempotent MERGE upsert.
/// </summary>
internal sealed class MarkIdempotencyKeyProcessedRequest : DataRequestBase<IDbConnection, int>
{
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
			MERGE {qualifiedTableName} AS target
			USING (SELECT @SagaId AS SagaId, @IdempotencyKey AS IdempotencyKey) AS source
			ON target.SagaId = source.SagaId AND target.IdempotencyKey = source.IdempotencyKey
			WHEN NOT MATCHED THEN
				INSERT (SagaId, IdempotencyKey, ProcessedAt)
				VALUES (source.SagaId, source.IdempotencyKey, SYSUTCDATETIME());
			""";

		Parameters.Add("SagaId", sagaId);
		Parameters.Add("IdempotencyKey", idempotencyKey);
		Command = CreateCommand(sql, cancellationToken: cancellationToken);
		ResolveAsync = conn => conn.ExecuteAsync(Command);
	}
}
