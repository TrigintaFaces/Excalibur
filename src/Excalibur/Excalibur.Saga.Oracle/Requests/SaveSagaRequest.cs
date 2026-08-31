// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;
using System.Diagnostics.CodeAnalysis;

using Dapper;

using Excalibur.Data;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Serialization;

namespace Excalibur.Saga.Oracle.Requests;

/// <summary>
/// Represents a data request to save a saga state to the Oracle saga store.
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
	/// the scope is untenanted it stamps the reserved sentinel, not an absent tenant, so the term is present
	/// either way and the match predicate is unconditional. A tenant-scoped scope cannot
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

		// Optimistic-concurrency compare-and-swap, store-owns-increment. SagaState.Version is the version the
		// caller LOADED (the concurrency token; a brand-new saga is 0). The store expects the persisted Version to
		// still equal that loaded value and writes the bumped (loadedVersion + 1). A concurrent write that already
		// advanced the row makes both MERGE branches no-op -> 0 rows affected, surfaced as a ConcurrencyException.
		// The INSERT branch stays guarded to :ExpectedVersion = 0 so a natural new saga (Version 0) inserts, while a
		// MISSING row with a non-zero expected (a deleted/stale saga) is NOT resurrected at a high version.
		// Oracle MERGE requires a FROM DUAL driver row and places conditional predicates in a trailing WHERE clause.
		var expectedVersion = sagaState.Version;
		var newVersion = sagaState.Version + 1;

		// The tenant term is UNCONDITIONAL in the match. It used to vanish on the unscoped path, which let an
		// unscoped save match a SCOPED tenant's row by SagaId alone and overwrite both its state and its tenant
		// stamp. That conditional existed because the term could be bound to NULL, and `target.TenantId =
		// :TenantId` never matches a NULL — so on Oracle it had to be dropped or every untenanted save would
		// break. Resolving through KeyedTenantPartition makes the bound value a non-empty reserved sentinel
		// instead, so equality holds on both paths and no branch can omit the term.
		// TenantId is in the ON clause and therefore MUST NOT appear in the UPDATE SET below. Oracle
		// rejects that with "ORA-38104: Columns referenced in the ON Clause cannot be updated", so every
		// version-gated save against an EXISTING saga failed on this provider -- 18 conformance failures,
		// all of them the update path. The SQL Server sibling assigns it and is accepted; this restriction
		// is Oracle-specific, which is why the shared shape hid the defect.
		//
		// Dropping the assignment loses nothing: the row matched only BECAUSE target.TenantId already
		// equals :TenantId, so re-assigning the same value was always a no-op. The tenant term stays
		// unconditional in the match, so the isolation property the term exists for is untouched -- an
		// unscoped save still cannot match a scoped tenant's row.
		var partition = KeyedTenantPartition.FromScope(scope);
		const string onTenant = " AND target.TenantId = :TenantId";
		var sql = $"""
			MERGE INTO {qualifiedTableName} target
			USING (SELECT :SagaId AS SagaId FROM DUAL) source
			ON (target.SagaId = source.SagaId{onTenant})
			WHEN MATCHED THEN UPDATE SET
				StateJson = :StateJson,
				IsCompleted = :IsCompleted,
				CompletedAt = :CompletedAt,
				Version = :NewVersion,
				UpdatedUtc = SYS_EXTRACT_UTC(SYSTIMESTAMP)
			WHERE target.Version = :ExpectedVersion
			WHEN NOT MATCHED THEN INSERT
				(SagaId, SagaType, StateJson, IsCompleted, CompletedAt, TenantId, Version)
				VALUES (:SagaId, :SagaType, :StateJson, :IsCompleted, :CompletedAt, :TenantId, :NewVersion)
			WHERE :ExpectedVersion = 0
			""";

		var stateJson = serializer.Serialize(sagaState);

		var dp = new DynamicParameters();
		// ODP.NET has no native Guid bind type; a raw Guid throws. Bind the 16 canonical bytes as RAW(16).
		// Guid.ToByteArray() is deterministic, so LoadSagaRequest's symmetric ToByteArray() matches this
		// stored value byte-for-byte in its WHERE clause. Scoped per-bind so no process-wide Dapper type
		// handler mutates Guid mapping for other providers in the same process.
		dp.Add("SagaId", sagaState.SagaId.ToByteArray());
		dp.Add("SagaType", typeof(TSagaState).Name);
		// Store JSON as CLOB to accommodate arbitrarily large saga state.
		dp.Add("StateJson", stateJson, DbType.String);
		// Oracle (pre-23c) has no native BOOLEAN column type; persist completion as NUMBER(1) 0/1.
		dp.Add("IsCompleted", sagaState.Completed ? 1 : 0);
		// Persist the explicit completion instant into an indexed column so retention purge keys on the same
		// CompletedAt field across every provider, not a proxy column.
		dp.Add("CompletedAt", sagaState.CompletedAt);
		// Ambient tenant is the isolation authority, resolved from the SCOPE ALONE and never from
		// sagaState.TenantId: LoadSagaRequest is given a SagaId, not a state, so the read side has only the scope
		// to resolve from. Deriving the row's discriminator from the saga's own tenant would let the two sides
		// resolve different terms for the same saga and write it where no read looks.
		//
		// No empty-string normalization is needed any more, and its absence is the point. Oracle folds an empty
		// string to NULL, which is why the previous code had to special-case a zero-length tenant — and why a
		// NULL-encoded untenanted partition is unworkable on this provider specifically: neither `= :TenantId`
		// with a null bind nor `= ''` can ever match a row. partition.TenantId is a NON-EMPTY reserved sentinel,
		// so Oracle stores and compares it as an ordinary string and the fold never applies.
		dp.Add("TenantId", partition.TenantId);
		dp.Add("ExpectedVersion", expectedVersion);
		dp.Add("NewVersion", newVersion);

		Command = new CommandDefinition(sql, new OracleDynamicParameters(dp), cancellationToken: cancellationToken);
		ResolveAsync = conn => conn.ExecuteAsync(Command);
	}
}
