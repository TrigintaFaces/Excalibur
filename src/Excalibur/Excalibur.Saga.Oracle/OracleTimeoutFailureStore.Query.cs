// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Saga.Abstractions;
using Oracle.ManagedDataAccess.Client;
using static Excalibur.Saga.Oracle.OracleSagaTimerRecoveryRequests;

namespace Excalibur.Saga.Oracle;

internal sealed partial class OracleTimeoutFailureStore
{
    internal Task<SagaTimeoutHoldPage> QueryAsync(string tenantId, string? sagaId, int pageSize, string? cursor,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tenantId); ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pageSize, 256);
        var target = recovery.CaptureRecoveryTarget(); var tenant = getTenant();
        if (tenantId != tenant) { throw new InvalidOperationException("Held timeout query differs from the current exact tenant authority."); }
        var after = cursor is null ? null : OracleTimeoutFailureFrames.DecodeCursor(cursor);
        if (after is not null && (after.Owner != target.Owner || after.Table != target.Table || after.Tenant != tenantId || after.SagaId != sagaId))
        { throw new ArgumentException("Held timeout cursor belongs to another target/partition/filter.", nameof(cursor)); }
        return recovery.RunFailureTransactionAsync(async (connection, transaction) =>
        {
            var profile = await RequireProfileAsync(connection, transaction, target, cancellationToken).ConfigureAwait(false);
            if (after is not null && (after.ObjectId != profile.ObjectId || after.Backend != profile.Backend))
            { throw new ArgumentException("Held timeout cursor physical target was replaced.", nameof(cursor)); }
            var owned = await RecoveryMetadataAvailableAsync(connection, transaction, target, cancellationToken).ConfigureAwait(false);
            var filter = "FailureHeld=1 AND NLSSORT(TenantId,'NLS_SORT=BINARY')=NLSSORT(:Tenant,'NLS_SORT=BINARY') AND LENGTH2(TenantId)=LENGTH2(:Tenant)";
            var parameters = new List<RecoveryParameter> { Text("Tenant", tenantId, 64), Number("Limit", pageSize + 1) };
            if (sagaId is not null)
            {
                filter += " AND NLSSORT(SagaId,'NLS_SORT=BINARY')=NLSSORT(:Saga,'NLS_SORT=BINARY') AND LENGTH2(SagaId)=LENGTH2(:Saga)";
                parameters.Add(Text("Saga", sagaId, 450));
            }
            if (after is not null)
            {
                filter += " AND NLSSORT(TimeoutId,'NLS_SORT=BINARY')>NLSSORT(:AfterId,'NLS_SORT=BINARY')";
                parameters.Add(Text("AfterId", after.LastId, 450));
            }
            var rows = (await ReadRecoveryAsync(connection, transaction,
                $"SELECT * FROM (SELECT {RowProjection(owned)} FROM {target.Payload} WHERE {filter} ORDER BY NLSSORT(TimeoutId,'NLS_SORT=BINARY')) WHERE ROWNUM<=:Limit",
                parameters.ToArray(), ProjectRow, cancellationToken).ConfigureAwait(false)).Select(row => BindSource(profile, row)).ToArray();
            if (rows.Any(row => row.Timeout.TenantId != tenantId || (sagaId is not null && row.Timeout.SagaId != sagaId)))
            { throw new InvalidDataException("Oracle held query lost exact partition equality."); }
            var page = rows.Take(pageSize).ToArray();
            var receipts = page.Select(row => Receipt(profile, row,
                row.Receipt ?? throw new InvalidDataException("Held source has no receipt."), SagaTimeoutHoldState.Held));
            var next = rows.Length <= pageSize ? null : OracleTimeoutFailureFrames.EncodeCursor(new(target.Owner, target.Table,
                profile.ObjectId, profile.Backend, tenantId, sagaId, page[^1].Timeout.TimeoutId));
            return new SagaTimeoutHoldPage(receipts, next);
        }, cancellationToken);
    }

    internal Task<IReadOnlyList<ClaimedSagaTimeout>?> TryClaimAsync(DateTimeOffset asOf, int batchSize,
        string processorId, int leaseSeconds, CancellationToken cancellationToken)
    {
        var target = recovery.CaptureRecoveryTarget();
        return recovery.RunFailureTransactionAsync<IReadOnlyList<ClaimedSagaTimeout>?>(async (connection, transaction) =>
        {
            var profile = await ReadProfileAsync(connection, transaction, target, cancellationToken).ConfigureAwait(false);
            if (profile is null) { return null; }
            var token = FormattableString.Invariant($"{processorId}:{Guid.NewGuid():N}");
            var sql = $"""
                DECLARE
                    CURSOR eligible IS
                        SELECT TimeoutId FROM {target.Payload}
                        WHERE FailureHeld=0 AND DueAt<=:AsOf
                          AND (ClaimedAt IS NULL OR ClaimedAt<SYSTIMESTAMP-NUMTODSINTERVAL(:Lease,'SECOND'))
                        ORDER BY DueAt FOR UPDATE SKIP LOCKED;
                    TYPE id_table IS TABLE OF {target.Payload}.TimeoutId%TYPE;
                    claimed_ids id_table;
                BEGIN
                    OPEN eligible;
                    FETCH eligible BULK COLLECT INTO claimed_ids LIMIT :BatchSize;
                    CLOSE eligible;
                    FORALL i IN 1 .. claimed_ids.COUNT
                        UPDATE {target.Payload} SET ClaimedAt=SYSTIMESTAMP,ClaimedBy=:Token
                        WHERE TimeoutId=claimed_ids(i);
                END;
                """;
            _ = await ExecuteRecoveryAsync(connection, transaction, sql,
                [new("AsOf", OracleDbType.TimeStampTZ, asOf), Number("Lease", leaseSeconds), Number("BatchSize", batchSize), Text("Token", token, 200)], cancellationToken).ConfigureAwait(false);
            var owned = await RecoveryMetadataAvailableAsync(connection, transaction, target, cancellationToken).ConfigureAwait(false);
            // SAME transaction as the claim UPDATE. Cancellation/resume cannot change epochs between
            // acquisition and projection; only rows stamped with this fresh token are materialized.
            var rows = await ReadRecoveryAsync(connection, transaction,
                $"SELECT {RowProjection(owned)} FROM {target.Payload} WHERE ClaimedBy=:Token AND FailureHeld=0 ORDER BY DueAt FETCH FIRST :BatchSize ROWS ONLY",
                [Text("Token", token, 200), Number("BatchSize", batchSize)], ProjectRow, cancellationToken).ConfigureAwait(false);
            return rows.Select(row => BindSource(profile, row).Claim(token)).ToArray();
        }, cancellationToken);
    }

    internal Task<SagaTimeoutRetirementOutcome?> TryRetireAsync(ClaimedSagaTimeout claim, CancellationToken cancellationToken)
    {
        var target = recovery.CaptureRecoveryTarget(); var tenant = getTenant();
        return recovery.RunFailureTransactionAsync<SagaTimeoutRetirementOutcome?>(async (connection, transaction) =>
        {
            var profile = await ReadProfileAsync(connection, transaction, target, cancellationToken).ConfigureAwait(false);
            if (profile is null) { return null; }
            if (claim.Timeout.TenantId != tenant || !HasSourceBinding(profile, claim.SourceEpoch ?? string.Empty, claim.Timeout.TenantId, claim.Timeout.SagaId, claim.Timeout.TimeoutId) || string.IsNullOrEmpty(claim.SourceEpoch) || string.IsNullOrEmpty(claim.DeliveryEpoch))
            { return SagaTimeoutRetirementOutcome.Superseded; }
            // No domain lock follows this payload DELETE: it cannot form a receiver/payload inversion.
            var rows = await ExecuteRecoveryAsync(connection, transaction,
                $"DELETE FROM {target.Payload} WHERE TimeoutId=:Id AND SagaId=:Saga AND TenantId=:Tenant AND LENGTH2(TimeoutId)=LENGTH2(:Id) AND LENGTH2(SagaId)=LENGTH2(:Saga) AND LENGTH2(TenantId)=LENGTH2(:Tenant) AND NLSSORT(TimeoutId,'NLS_SORT=BINARY')=NLSSORT(:Id,'NLS_SORT=BINARY') AND NLSSORT(SagaId,'NLS_SORT=BINARY')=NLSSORT(:Saga,'NLS_SORT=BINARY') AND NLSSORT(TenantId,'NLS_SORT=BINARY')=NLSSORT(:Tenant,'NLS_SORT=BINARY') AND ClaimedBy=:Claim AND FailureHeld=0 AND FailureSourceEpoch=:Source AND FailureDeliveryEpoch=:Cycle",
                [Text("Id", claim.Timeout.TimeoutId, 450), Text("Saga", claim.Timeout.SagaId, 450), Text("Tenant", tenant, 64), Text("Claim", claim.ClaimToken, 200),
                    Binary("Source", RawSource(claim.SourceEpoch)), Binary("Cycle", Convert.FromHexString(claim.DeliveryEpoch))], cancellationToken).ConfigureAwait(false);
            return rows == 0 ? SagaTimeoutRetirementOutcome.Superseded : SagaTimeoutRetirementOutcome.Retired;
        }, cancellationToken);
    }
}
