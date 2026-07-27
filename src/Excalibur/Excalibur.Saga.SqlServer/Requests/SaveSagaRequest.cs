// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;
using System.Diagnostics.CodeAnalysis;

using Dapper;

using Excalibur.Data;
using Excalibur.Dispatch;
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
	/// <param name="scope">
	/// The tenant scope. When tenant-scoped it is the isolation authority: the saga row is stamped with this
	/// tenant and the version-gated MERGE match additionally requires the persisted tenant to equal it, so a
	/// save under one tenant can never match (and overwrite) another tenant's saga. When
	/// <see cref="TenantScope.None"/> (the non-multi-tenant path) the saga's own <c>TenantId</c> is persisted
	/// and no tenant is added to the match (byte-identical un-scoped behavior). A tenant-scoped scope cannot
	/// be constructed without a tenant, so a predicate-less save while tenancy is active is unrepresentable.
	/// </param>
	[RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
	[RequiresDynamicCode("JSON serialization and deserialization might require runtime code generation.")]
	public SaveSagaRequest(
			TSagaState sagaState,
			DispatchJsonSerializer serializer,
			string qualifiedTableName,
			TenantScope scope,
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

		// UNCONDITIONAL, and a const so it cannot become conditional again.
		//
		// This was `scope.IsScoped ? " AND target.TenantId = @TenantId" : string.Empty`. On the unscoped path
		// the fragment was empty, so the MERGE matched on SagaId ALONE — and a MERGE match is a WRITE, so an
		// unscoped save could UPDATE a row owned by another tenant and re-stamp its TenantId. That is a
		// cross-tenant write, not a read leak.
		//
		// Postgres and Oracle already bind it unconditionally; SQL Server was the only provider still
		// carrying the ternary. It is declared `const` rather than merely un-branched because that is the
		// form Oracle uses and it makes the regression inexpressible: a const cannot acquire a
		// `scope.IsScoped` branch without someone deliberately changing its type — which matters here
		// precisely because the conditional was removed from the other providers and survived in this one.
		const string onTenant = " AND target.TenantId = @TenantId";
		// WITH (HOLDLOCK) takes a serializable key-range lock on the MERGE target so two concurrent saves for
		// the same (SagaId[, TenantId]) key cannot both evaluate WHEN NOT MATCHED and both INSERT -> primary-key
		// violation. This is Microsoft's documented guard against the MERGE upsert race; without it the atomic
		// find-or-create guarantee is only probabilistic under concurrency.
		var sql = $"""
                        MERGE {qualifiedTableName} WITH (HOLDLOCK) AS target
                        USING (SELECT @SagaId AS SagaId) AS source
                        ON (target.SagaId = source.SagaId{onTenant})
                        WHEN MATCHED AND target.Version = @ExpectedVersion THEN UPDATE SET
                        StateJson = @StateJson,
                        IsCompleted = @IsCompleted,
                        CompletedAt = @CompletedAt,
                        TenantId = @TenantId,
                        Version = @NewVersion,
                        UpdatedUtc = SYSUTCDATETIME()
                        WHEN NOT MATCHED AND @ExpectedVersion = 0 THEN INSERT
                        (SagaId, SagaType, StateJson, IsCompleted, CompletedAt, TenantId, Version)
                        VALUES (@SagaId, @SagaType, @StateJson, @IsCompleted, @CompletedAt, @TenantId, @NewVersion);
                        """;

		var stateJson = serializer.Serialize(sagaState);
		Parameters.Add("SagaId", sagaState.SagaId);
		Parameters.Add("SagaType", typeof(TSagaState).Name);
		Parameters.Add("StateJson", stateJson);
		Parameters.Add("IsCompleted", sagaState.Completed);
		// Persist the explicit completion instant out of StateJson into an indexed column so retention purge
		// keys on the same CompletedAt field across every provider (SA w8aqq3 ruling), not a proxy column.
		Parameters.Add("CompletedAt", sagaState.CompletedAt);
		// Ambient tenant is the isolation authority: the scope resolves the partition, and that ONE value both
		// gates the MERGE match and is persisted.
		//
		// Resolved through KeyedTenantPartition so the untenanted case is a non-empty reserved sentinel rather
		// than null. The previous `scope.IsScoped ? scope.TenantId : sagaState.TenantId` fallback let the
		// match term and the persisted term come from different sources, and a null bind makes
		// `target.TenantId = @TenantId` match nothing — so the row would be written where no scoped read
		// looks. Postgres and Oracle both resolve it this way; this brings SQL Server onto the same contract
		// rather than inventing a third.
		var partition = KeyedTenantPartition.FromScope(scope);
		Parameters.Add("TenantId", partition.TenantId);
		Parameters.Add("ExpectedVersion", expectedVersion);
		Parameters.Add("NewVersion", newVersion);

		Command = CreateCommand(sql, cancellationToken: cancellationToken);
		ResolveAsync = conn => conn.ExecuteAsync(Command);
	}
}
