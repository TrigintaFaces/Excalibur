// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;
using System.Diagnostics.CodeAnalysis;

using Dapper;

using Excalibur.Data;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Serialization;

namespace Excalibur.Saga.SqlServer.Requests;

/// <summary>
/// Represents a data request to save a saga state to the SQL Server saga store.
/// </summary>
/// <typeparam name="TSagaState">The type of the saga state.</typeparam>
public sealed class SaveSagaRequest<TSagaState> : DataRequestBase<IDbConnection, int>
	where TSagaState : SagaState
{
	/// <summary>
	/// Initializes a new instance of the <see cref="SaveSagaRequest{TSagaState}"/> class.
	/// </summary>
	/// <param name="sagaState">The saga state to save.</param>
	/// <param name="serializer">The JSON serializer for serializing saga state.</param>
	/// <param name="qualifiedTableName">The fully qualified saga table name.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	[RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
	[RequiresDynamicCode("JSON serialization and deserialization might require runtime code generation.")]
	public SaveSagaRequest(
			TSagaState sagaState,
			DispatchJsonSerializer serializer,
			string qualifiedTableName,
			CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(qualifiedTableName);
		SagaSqlValidator.ThrowIfInvalidQualifiedName(qualifiedTableName);

		ArgumentNullException.ThrowIfNull(sagaState);

		// Optimistic-concurrency compare-and-swap (bd-eszc06), store-owns-increment (EF-style; SA seam ruling).
		// SagaState.Version is the version the caller LOADED (the concurrency token; a brand-new saga is 0) -- the
		// caller performs NO version arithmetic. The store expects the persisted Version to still equal that loaded
		// value and writes the bumped (loadedVersion + 1). A concurrent write that already advanced the row makes
		// both MERGE branches no-op -> 0 rows affected, which the store surfaces as a ConcurrencyException. The
		// INSERT branch stays guarded to @ExpectedVersion = 0 so a natural new saga (Version 0) inserts, while a
		// MISSING row with a non-zero expected (a deleted/stale saga) is NOT resurrected at a high version (it
		// matches neither branch -> 0 rows -> ConcurrencyException). This makes the previous unchecked
		// last-writer-wins UPDATE inexpressible: there is no save path that ignores Version.
		var expectedVersion = sagaState.Version;
		var newVersion = sagaState.Version + 1;

		var sql = $"""
                        MERGE {qualifiedTableName} AS target
                        USING (SELECT @SagaId AS SagaId) AS source
                        ON (target.SagaId = source.SagaId)
                        WHEN MATCHED AND target.Version = @ExpectedVersion THEN UPDATE SET
                        StateJson = @StateJson,
                        IsCompleted = @IsCompleted,
                        Version = @NewVersion,
                        UpdatedUtc = SYSUTCDATETIME()
                        WHEN NOT MATCHED AND @ExpectedVersion = 0 THEN INSERT
                        (SagaId, SagaType, StateJson, IsCompleted, Version)
                        VALUES (@SagaId, @SagaType, @StateJson, @IsCompleted, @NewVersion);
                        """;

		var stateJson = serializer.Serialize(sagaState);
		Parameters.Add("SagaId", sagaState.SagaId);
		Parameters.Add("SagaType", typeof(TSagaState).Name);
		Parameters.Add("StateJson", stateJson);
		Parameters.Add("IsCompleted", sagaState.Completed);
		Parameters.Add("ExpectedVersion", expectedVersion);
		Parameters.Add("NewVersion", newVersion);

		Command = CreateCommand(sql, cancellationToken: cancellationToken);
		ResolveAsync = conn => conn.ExecuteAsync(Command);
	}
}
