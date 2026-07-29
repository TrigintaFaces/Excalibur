// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;
using System.Diagnostics.CodeAnalysis;

using Dapper;

using Excalibur.Data;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Serialization;

namespace Excalibur.Saga.Postgres;

/// <summary>
/// Represents a data request to save a saga state to the Postgres saga store.
/// </summary>
/// <typeparam name="TSagaState">The type of the saga state.</typeparam>
/// <remarks>
/// <para>
/// This request uses Postgres's <c>INSERT ON CONFLICT</c> (upsert) pattern for an atomic,
/// single-round-trip, version-gated save. It is the optimistic-concurrency analogue of
/// <c>SqlServerSagaStore</c>'s version-gated MERGE (store-owns-increment).
/// </para>
/// <para>
/// The saga state is serialized to JSONB for efficient storage and querying capabilities.
/// </para>
/// </remarks>
public sealed class SaveSagaRequest<TSagaState>: DataRequestBase<IDbConnection, int>
	where TSagaState: SagaState
{
	/// <summary>
	/// Initializes a new instance of the <see cref="SaveSagaRequest{TSagaState}"/> class.
	/// </summary>
	/// <param name="sagaState">The saga state to save.</param>
	/// <param name="options">The Postgres saga store options.</param>
	/// <param name="serializer">The JSON serializer for serializing saga state.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <param name="scope">
	/// The tenant scope, and the sole authority for the row's tenant. The saga row is stamped from this
	/// scope and the version-gated UPDATE additionally requires the persisted tenant to equal it, so a save
	/// under one tenant can never overwrite another tenant's saga. <see cref="TenantScope.None"/> stamps
	/// the reserved untenanted partition, not an absent tenant, so the term is present either way and the
	/// match predicate is unconditional. A tenant-scoped scope cannot be constructed without a tenant, so a
	/// predicate-less save while tenancy is active is unrepresentable.
	/// <para>
	/// <c>sagaState.TenantId</c> does NOT influence where the row is stored, under any scope. It travels in
	/// the serialized payload only. The read side is given a saga id and a scope — never a state — so
	/// deriving the discriminator from the saga's own tenant would let the two sides resolve different
	/// terms for the same saga and write it where no read looks.
	/// </para>
	/// </param>
	[RequiresUnreferencedCode("JSON serialization and deserialization might require types that cannot be statically analyzed.")]
	[RequiresDynamicCode("JSON serialization and deserialization might require runtime code generation.")]
	public SaveSagaRequest(
 TSagaState sagaState,
 PostgresSagaOptions options,
 DispatchJsonSerializer serializer,
 TenantScope scope,
 CancellationToken cancellationToken)
	{
 ArgumentNullException.ThrowIfNull(sagaState);
 ArgumentNullException.ThrowIfNull(options);
 ArgumentNullException.ThrowIfNull(serializer);

 // Defense-in-depth: validate the config-sourced qualified table name before interpolating
 // it into SQL — parity with SqlServer's saga request types. SagaSqlValidator enforces the safe
 // "schema"."table" identifier shape.
 SagaSqlValidator.ThrowIfInvalidQualifiedName(options.QualifiedTableName);

 // Optimistic-concurrency compare-and-swap, store-owns-increment (EF-style; mirrors
 // SqlServerSagaStore's TWO guarded MERGE branches). SagaState.Version is the version the caller LOADED
 // (the concurrency token; a brand-new saga is 0) -- the caller performs NO version arithmetic. The store
 // expects the persisted version column to still equal that loaded value and writes the bumped
 // (loadedVersion + 1).
 //
 // SA ruling: branch on the expected version so a deleted/completed saga cannot be RESURRECTED
 // at a high version (a "zombie" saga). This mirrors SqlServer's MERGE, whose INSERT branch is guarded to
 // @ExpectedVersion = 0 and whose UPDATE branch is version-gated -- a missing row with a non-zero expected
 // matches neither branch. Both branches below funnel a non-match to 0 rows affected, which the store
 // surfaces as a ConcurrencyException (no silent lost update, no resurrection):
 // - expected == 0 (new saga) -> INSERT... ON CONFLICT DO NOTHING. A pre-existing row (a concurrent
 // create, or an already-advanced saga) yields 0 rows -- a fresh-insert collision IS a conflict.
 // - expected > 0 (update) -> UPDATE... WHERE version = @ExpectedVersion, NO insert. A stale version
 // OR a missing row (deleted/zombie saga) matches no row -> 0 rows -> conflict. No INSERT path means a
 // deleted saga is never re-created.
 var expectedVersion = sagaState.Version;
 var newVersion = sagaState.Version + 1;

 var stateJson = serializer.Serialize(sagaState);

 string sql;
 if (expectedVersion == 0)
 {
 sql = $"""
 INSERT INTO {options.QualifiedTableName}
 (saga_id, saga_type, state_json, is_completed, completed_at, tenant_id, version, created_utc, updated_utc)
 VALUES
 (@SagaId, @SagaType, @StateJson::jsonb, @IsCompleted, @CompletedAt, @TenantId, @NewVersion, NOW(), NOW())
 ON CONFLICT (tenant_id, saga_id) DO NOTHING;
 """;
 }
 else
 {
 sql = $"""
 UPDATE {options.QualifiedTableName} SET
 saga_type = @SagaType,
 state_json = @StateJson::jsonb,
 is_completed = @IsCompleted,
 completed_at = @CompletedAt,
 tenant_id = @TenantId,
 version = @NewVersion,
 updated_utc = NOW()
 WHERE saga_id = @SagaId AND version = @ExpectedVersion AND tenant_id = @TenantId;
 """;
 }

 Parameters.Add("SagaId", sagaState.SagaId);
 Parameters.Add("SagaType", typeof(TSagaState).Name);
 Parameters.Add("StateJson", stateJson);
 Parameters.Add("IsCompleted", sagaState.Completed);
 // Persist the explicit completion instant (UTC) into an indexed column so retention purge keys on
 // completed_at across every provider (SA w8aqq3 ruling), not a proxy column.
 // Normalised to UTC. Npgsql writes a DateTimeOffset to timestamptz ONLY when its offset is zero;
		// any other offset is rejected outright rather than converted, so a saga completed at
		// DateTimeOffset.Now on a host east or west of UTC could not be saved at all -- the write threw
		// "Cannot write DateTimeOffset with Offset=..., only offset 0 (UTC) is supported". Converting
		// preserves the exact instant and lets a caller supply one in any offset.
		Parameters.Add("CompletedAt", sagaState.CompletedAt?.ToUniversalTime());
 // Ambient tenant is the isolation authority (SA 24815), resolved from the SCOPE ALONE and never from
 // sagaState.TenantId: LoadSagaRequest is given a saga_id, not a state, so the read side has only the
 // scope to resolve from. Deriving the row's discriminator from the saga's own tenant would let the two
 // sides resolve different terms for the same saga and write it where no read looks. partition.TenantId
 // is never null (untenanted yields the reserved sentinel), which is what lets the tenant term above be
 // unconditional in both the ON CONFLICT target and the UPDATE predicate. sagaState.TenantId remains
 // inside state_json as defense-in-depth, not as the row key.
 var partition = KeyedTenantPartition.FromScope(scope);
 Parameters.Add("TenantId", partition.TenantId);
 Parameters.Add("NewVersion", newVersion);

 // @ExpectedVersion is referenced only by the UPDATE (expected > 0) branch; bind it only there so no
 // unreferenced parameter is sent on the INSERT path.
 if (expectedVersion != 0)
 {
 Parameters.Add("ExpectedVersion", expectedVersion);
 }

 Command = CreateCommand(sql, commandTimeout: options.CommandTimeoutSeconds, cancellationToken: cancellationToken);
 ResolveAsync = conn => conn.ExecuteAsync(Command);
	}
}
