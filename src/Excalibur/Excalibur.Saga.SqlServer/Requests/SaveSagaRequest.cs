// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


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
	/// the scope is untenanted it stamps the reserved sentinel, not an absent tenant, so the term is present
	/// either way and the match predicate is unconditional. A tenant-scoped scope cannot
	/// be constructed without a tenant, so a predicate-less save while tenancy is active is unrepresentable.
	/// </param>
	/// <exception cref="ArgumentOutOfRangeException">The saga state's version is negative.</exception>
	/// <exception cref="OverflowException">The saga state's version is <see cref="long.MaxValue"/> and cannot be incremented.</exception>
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

		// Optimistic-concurrency compare-and-swap, store-owns-increment (EF-style; SA seam ruling).
		// SagaState.Version is the version the caller LOADED (the concurrency token; a brand-new saga is 0) -- the
		// caller performs NO version arithmetic. The store expects the persisted Version to still equal that loaded
		// value and writes the bumped (loadedVersion + 1). A concurrent write that already advanced the row makes
		// both MERGE branches no-op -> 0 rows affected, which the store surfaces as a ConcurrencyException. The
		// INSERT branch stays guarded to @ExpectedVersion = 0 so a natural new saga (Version 0) inserts, while a
		// MISSING row with a non-zero expected (a deleted/stale saga) is NOT resurrected at a high version (it
		// matches neither branch -> 0 rows -> ConcurrencyException). This makes the previous unchecked
		// last-writer-wins UPDATE inexpressible: there is no save path that ignores Version.
		if (sagaState.TimerRecoveryIncarnationId == Guid.Empty)
        {
            if (sagaState.Version < 1) { throw new InvalidOperationException("A new saga must have a nonempty timer recovery incarnation."); }
            // The conditional write below, not this observation, decides whether legacy initialization is still current.
            sagaState.TimerRecoveryIncarnationId = Guid.NewGuid();
        }
        var recoveryTracked = sagaState.PendingTimerRecovery is not null || sagaState.LastTimerRecoveryBatchId != Guid.Empty;
        var recoveryIncarnation = sagaState.TimerRecoveryIncarnationId.ToString("N");
        var recoveryBatch = sagaState.PendingTimerRecovery?.BatchId.ToString("N");
        var recoveryDigest = sagaState.PendingTimerRecovery is { } pending ? Convert.ToHexString(pending.GetAuthorityDigest()) : null;
        var expectedVersion = sagaState.Version;
		ArgumentOutOfRangeException.ThrowIfNegative(expectedVersion, nameof(sagaState));
		var newVersion = checked(expectedVersion + 1);

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
		const string onTenant = " AND target.TenantId = @TenantId"
			+ " AND DATALENGTH(target.TenantId) = DATALENGTH(@TenantId)"
			+ " AND CONVERT(VARBINARY(128), target.TenantId) = CONVERT(VARBINARY(128), @TenantId)";
		// WITH (UPDLOCK, HOLDLOCK) -- both hints, and neither is optional.
		//
		// HOLDLOCK takes a serializable key-range lock on the MERGE target so two concurrent saves for the
		// same (SagaId[, TenantId]) key cannot both evaluate WHEN NOT MATCHED and both INSERT -> primary-key
		// violation. This is Microsoft's documented guard against the MERGE upsert race; without it the atomic
		// find-or-create guarantee is only probabilistic under concurrency.
		//
		// HOLDLOCK ALONE is the conversion-deadlock shape. It takes a SHARED range lock for the match, then
		// asks to convert that lock to exclusive for the write. Two sessions upserting the same key each hold
		// S and each wait for the other to drop it before either can take X -- a cycle the engine can only
		// break by killing one as a deadlock victim (error 1205). UPDLOCK takes an UPDATE lock on the read
		// instead, and two UPDATE locks are not mutually compatible, so the second session BLOCKS briefly and
		// then proceeds. It appears only under contention -- which for a process manager is the ordinary case,
		// not the exceptional one, and a 1205 here is a lost or retried saga state transition.
		//
		// UPDLOCK alone is not the fix either: it does not hold the range, so it reopens the phantom-insert
		// race HOLDLOCK exists to close. Both, or the guarantee is incomplete in one direction or the other.
		// The snapshot store's MERGE already carries the pair.
		var sql = $"""
                        MERGE {qualifiedTableName} WITH (UPDLOCK, HOLDLOCK) AS target
                        USING (SELECT @SagaId AS SagaId) AS source
                        ON (target.SagaId = source.SagaId{onTenant})
                        WHEN MATCHED AND target.Version = @ExpectedVersion
                        AND (target.TimerRecoveryIncarnation IS NULL OR target.TimerRecoveryIncarnation = @RecoveryIncarnation)
                        AND (target.TimerRecoveryPendingBatch IS NULL OR (target.TimerRecoveryPendingBatch = @RecoveryBatch AND target.TimerRecoveryPendingDigest = @RecoveryDigest)
                          OR (@RecoveryBatch IS NULL AND target.TimerRecoveryPendingBatch = @RecoveryLastAck))
                        AND target.SagaType = @SagaType
                        AND DATALENGTH(target.SagaType) = DATALENGTH(@SagaType)
                        AND CONVERT(VARBINARY(1000), target.SagaType) = CONVERT(VARBINARY(1000), @SagaType) THEN UPDATE SET
                        TimerRecoveryIncarnation = COALESCE(target.TimerRecoveryIncarnation, @RecoveryIncarnation),
                        TimerRecoveryTracked = CASE WHEN target.TimerRecoveryTracked = 1 OR @RecoveryTracked = 1 THEN 1 ELSE 0 END,
                        TimerRecoveryPendingBatch = @RecoveryBatch, TimerRecoveryPendingDigest = @RecoveryDigest,
                        StateJson = @StateJson,
                        IsCompleted = @IsCompleted,
                        CompletedAt = @CompletedAt,
                        TenantId = @TenantId,
                        Version = @NewVersion,
                        UpdatedUtc = SYSUTCDATETIME()
                        WHEN NOT MATCHED AND @ExpectedVersion = 0 THEN INSERT
                        (SagaId, SagaType, StateJson, IsCompleted, CompletedAt, TenantId, Version, TimerRecoveryIncarnation, TimerRecoveryTracked, TimerRecoveryPendingBatch, TimerRecoveryPendingDigest)
                        VALUES (@SagaId, @SagaType, @StateJson, @IsCompleted, @CompletedAt, @TenantId, @NewVersion, @RecoveryIncarnation, @RecoveryTracked, @RecoveryBatch, @RecoveryDigest);
                        """;

		var stateJson = serializer.Serialize(sagaState);
        if (recoveryTracked)
        {
            var restored = serializer.Deserialize<TSagaState>(stateJson);
            if (restored is null || restored.TimerRecoveryIncarnationId != sagaState.TimerRecoveryIncarnationId
                || restored.TimerRecoverySequence != sagaState.TimerRecoverySequence
                || restored.LastTimerRecoveryBatchId != sagaState.LastTimerRecoveryBatchId
                || (restored.PendingTimerRecovery is { } restoredJournal ? Convert.ToHexString(restoredJournal.GetAuthorityDigest()) : null) != recoveryDigest)
            { throw new InvalidOperationException("The selected saga serializer does not preserve ordinary timer recovery authority."); }
        }

		Parameters.Add("SagaId", sagaState.SagaId);
		Parameters.Add("SagaType", typeof(TSagaState).Name);
		Parameters.Add("StateJson", stateJson);
		Parameters.Add("IsCompleted", sagaState.Completed);
		// Persist the explicit completion instant out of StateJson into an indexed column so retention purge
		// keys on the same CompletedAt field across every provider (SA ruling), not a proxy column.
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
        if (sagaState.PendingTimerRecovery is { } owned
            && (owned.Scope.SagaId != sagaState.SagaId || owned.Scope.StateType != typeof(TSagaState).Name
                || !string.Equals(owned.Scope.TenantId, partition.TenantId, StringComparison.Ordinal)
                || owned.IncarnationId != sagaState.TimerRecoveryIncarnationId || owned.Sequence != sagaState.TimerRecoverySequence))
        { throw new InvalidOperationException("Timer journal differs from the state store's exact authority."); }

		Parameters.Add("TenantId", partition.TenantId);
		Parameters.Add("ExpectedVersion", expectedVersion);
		Parameters.Add("NewVersion", newVersion);

		Parameters.Add("RecoveryIncarnation", recoveryIncarnation);
        Parameters.Add("RecoveryTracked", recoveryTracked ? 1 : 0);
        Parameters.Add("RecoveryBatch", recoveryBatch);
        Parameters.Add("RecoveryDigest", recoveryDigest);
        Parameters.Add("RecoveryLastAck", sagaState.LastTimerRecoveryBatchId.ToString("N"));

		Command = CreateCommand(sql, cancellationToken: cancellationToken);
		ResolveAsync = conn => conn.ExecuteAsync(Command);
	}
}
