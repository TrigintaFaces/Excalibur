// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.Saga.Abstractions;
using Excalibur.Saga.Orchestration;
using Oracle.ManagedDataAccess.Client;

using static Excalibur.Saga.Oracle.OracleSagaTimerRecoveryRequests;

namespace Excalibur.Saga.Oracle;

internal sealed partial class OracleSagaTimerRecovery
{
    private sealed record RecoveryPhysicalRow(string RowId, string TimeoutId, string SagaId, string? TenantId);

    private static async Task RefuseTrackedRecoveryIdAsync(OracleConnection connection, OracleTransaction transaction,
        RecoveryTargetBinding target, Guid selected, string timeoutId, CancellationToken cancellationToken)
    {
        var rows = await ReadRecoveryAsync(connection, transaction,
            $"SELECT IdBytes FROM {target.Timers} WHERE TargetId=:Target AND IdKey=:IdKey AND Tracked=1",
            [Raw("Target", selected), Binary("IdKey", RecoveryKey(timeoutId))], reader => (byte[])reader.GetValue(0), cancellationToken).ConfigureAwait(false);
        foreach (var identity in rows)
        {
            if (!identity.AsSpan().SequenceEqual(RecoveryBytes(timeoutId)))
            { throw new InvalidOperationException("Timer identity differs from its retained exact key."); }
            throw new InvalidOperationException("A timer identity already belongs to retained recovery authority and cannot be allocated again.");
        }
    }
    private static async Task WriteRecoveryTimerAsync(OracleConnection connection, OracleTransaction transaction,
        RecoveryTargetBinding target, RecoveryScopeRow scope, string timeoutId, RecoveryTimerRow fact, CancellationToken cancellationToken)
    {
        // Same scope row is locked for every mutation. The conditional unique tracked-ID index additionally
        // prevents concurrent different domains from allocating the same retired/no-payload identity.
        var previous = await ReadRecoveryTimerAsync(connection, transaction, target, scope.Id, timeoutId, cancellationToken).ConfigureAwait(false);
        var parameters = new List<RecoveryParameter>
        {
            Raw("Scope", scope.Id), Raw("Target", scope.Target), Binary("IdKey", RecoveryKey(timeoutId)), Binary("IdBytes", RecoveryBytes(timeoutId)),
            Number("Tracked", fact.Tracked ? 1 : 0), new("Incarnation", OracleDbType.Raw, fact.Incarnation?.ToByteArray(), 16),
            new("Sequence", OracleDbType.Int64, fact.Sequence), Text("StateType", fact.StateType, 500), Number("Created", fact.CreatedRevision),
            Number("Cancelled", fact.Cancelled ? 1 : 0), Number("Through", fact.CancelThrough), Number("CancelRevision", fact.CancelRevision),
        };
        var sql = previous is null
            ? $"INSERT INTO {target.Timers}(ScopeId,TargetId,IdKey,IdBytes,Tracked,IncarnationId,SequenceNumber,StateType,CreatedRevision,IsCancelled,CancelThrough,CancelRevision) VALUES(:Scope,:Target,:IdKey,:IdBytes,:Tracked,:Incarnation,:Sequence,:StateType,:Created,:Cancelled,:Through,:CancelRevision)"
            : $"UPDATE {target.Timers} SET Tracked=:Tracked,IncarnationId=:Incarnation,SequenceNumber=:Sequence,StateType=:StateType,CreatedRevision=:Created,IsCancelled=:Cancelled,CancelThrough=:Through,CancelRevision=:CancelRevision WHERE ScopeId=:Scope AND IdKey=:IdKey";
        if (previous is not null)
        {
            parameters.RemoveAll(value => value.Name is "Target" or "IdBytes"); // Bind only placeholders actually present.
        }
        var written = await ExecuteRecoveryAsync(connection, transaction, sql, parameters.ToArray(), cancellationToken).ConfigureAwait(false);
        if (written != 1) { throw new InvalidOperationException("The retained timer fact was not conditionally written once."); }
    }
    private static async Task<IReadOnlyList<RecoveryPhysicalRow>> ReadRecoveryPhysicalRowsAsync(OracleConnection connection,
        OracleTransaction transaction, RecoveryTargetBinding target, RecoveryScopeRow scope, CancellationToken cancellationToken)
    {
        var rows = await ReadRecoveryAsync(connection, transaction,
            $"SELECT ROWIDTOCHAR(ROWID),TimeoutId,SagaId,TenantId FROM {target.Payload} WHERE TenantId=:Tenant AND (RecoverySagaGuid=:Saga OR RecoverySagaGuid IS NULL) FOR UPDATE",
            [Text("Tenant", scope.Tenant, 64), Raw("Saga", scope.SagaId)],
            reader => new RecoveryPhysicalRow(reader.GetString(0), reader.GetString(1), reader.GetString(2), NullableRecoveryText(reader, 3)), cancellationToken).ConfigureAwait(false);
        // RecoverySagaGuid indexes all newly mediated rows. Only legacy rows without that metadata need
        // CLR Guid parsing; never normalize away the original public SagaId envelope value.
        return rows.Where(row => string.Equals(KeyedTenantPartition.FromStoredValue(row.TenantId).TenantId, scope.Tenant, StringComparison.Ordinal)
            && Guid.TryParse(row.SagaId, out var id) && id == scope.SagaId).ToArray();
    }
    private static async Task<bool> RecoveryPhysicalIdExistsAsync(OracleConnection connection, OracleTransaction transaction,
        RecoveryTargetBinding target, string timeoutId, CancellationToken cancellationToken)
    {
        var ids = await ReadRecoveryAsync(connection, transaction, $"SELECT TimeoutId FROM {target.Payload} WHERE TimeoutId=:Id",
            [Text("Id", timeoutId, 450)], reader => reader.GetString(0), cancellationToken).ConfigureAwait(false);
        return ids.Any(id => string.Equals(id, timeoutId, StringComparison.Ordinal));
    }
    private static Task<int> DeleteRecoveryPhysicalRowAsync(OracleConnection connection, OracleTransaction transaction,
        RecoveryTargetBinding target, RecoveryPhysicalRow row, CancellationToken cancellationToken) =>
        ExecuteRecoveryAsync(connection, transaction, $"DELETE FROM {target.Payload} WHERE ROWID=CHARTOROWID(:PhysicalRow)",
            [new("PhysicalRow", OracleDbType.Varchar2, row.RowId, 4000)], cancellationToken);

    private static async Task InsertRecoveryPhysicalAsync(OracleConnection connection, OracleTransaction transaction,
        RecoveryTargetBinding target, SagaTimeout timeout, string tenant, Guid? sagaGuid,
        Guid? incarnation, long? sequence, string? stateType, CancellationToken cancellationToken)
    {
        _ = await ExecuteRecoveryAsync(connection, transaction,
            $"INSERT INTO {target.Payload}(TimeoutId,SagaId,SagaType,TimeoutType,TimeoutData,DueAt,ScheduledAt,TenantId,RecoverySagaGuid,OwnedIncarnationId,OwnedGeneration,OwnedStateType) VALUES(:Id,:Saga,:SagaType,:TimeoutType,:Data,:Due,:Scheduled,:Tenant,:SagaGuid,:Incarnation,:Generation,:StateType)",
            [Text("Id", timeout.TimeoutId, 450), Text("Saga", timeout.SagaId, 450), Text("SagaType", timeout.SagaType), Text("TimeoutType", timeout.TimeoutType),
                new("Data", OracleDbType.Blob, timeout.TimeoutData), new("Due", OracleDbType.TimeStampTZ, timeout.DueAt),
                new("Scheduled", OracleDbType.TimeStampTZ, timeout.ScheduledAt), Text("Tenant", tenant, 64),
                new("SagaGuid", OracleDbType.Raw, sagaGuid?.ToByteArray(), 16), new("Incarnation", OracleDbType.Raw, incarnation?.ToByteArray(), 16),
                new("Generation", OracleDbType.Int64, sequence), Text("StateType", stateType, 500)], cancellationToken).ConfigureAwait(false);
    }
    private static async Task<RecoveryIntentDisposition> ApplyRecoveryIntentAsync(OracleConnection connection, OracleTransaction transaction,
        RecoveryTargetBinding target, RecoveryScopeRow scope, SagaTimerRecoveryJournal journal, SagaTimerIntent intent,
        long revision, RecoveryCutoff external, CancellationToken cancellationToken)
    {
        if (intent.Action == SagaTimerAction.Schedule)
        {
            await RefuseTrackedRecoveryIdAsync(connection, transaction, target, scope.Target, intent.TimeoutId, cancellationToken).ConfigureAwait(false);
            if (await RecoveryPhysicalIdExistsAsync(connection, transaction, target, intent.TimeoutId, cancellationToken).ConfigureAwait(false))
            { throw new InvalidOperationException("A fresh timer intent cannot overwrite an existing physical timer identity."); }
            var old = await ReadRecoveryTimerAsync(connection, transaction, target, scope.Id, intent.TimeoutId, cancellationToken).ConfigureAwait(false);
            var blocked = journal.ObservedRevision <= external.Through;
            var created = new RecoveryTimerRow(true, journal.IncarnationId, journal.Sequence, journal.Scope.StateType,
                revision, blocked, old?.CancelThrough ?? -1, old?.CancelRevision ?? 0);
            await WriteRecoveryTimerAsync(connection, transaction, target, scope, intent.TimeoutId, created, cancellationToken).ConfigureAwait(false);
            if (!blocked)
            {
                await InsertRecoveryPhysicalAsync(connection, transaction, target, intent.Timeout!, scope.Tenant,
                    scope.SagaId, journal.IncarnationId, journal.Sequence, journal.Scope.StateType, cancellationToken).ConfigureAwait(false);
            }
            return new(blocked ? SagaTimerRecoveryIntentOutcome.Cancelled : SagaTimerRecoveryIntentOutcome.Applied,
                blocked ? external.Revision : revision);
        }
        if (intent.Action == SagaTimerAction.Cancel)
        {
            var old = await ReadRecoveryTimerAsync(connection, transaction, target, scope.Id, intent.TimeoutId, cancellationToken).ConfigureAwait(false)
                ?? new RecoveryTimerRow(false, null, null, null, -1, false, -1, 0);
            var own = old.Tracked && old.Incarnation == journal.IncarnationId && old.Sequence == journal.Sequence && old.CreatedRevision == revision;
            var protectedNewer = !own && old.CreatedRevision > journal.ObservedRevision;
            foreach (var row in await ReadRecoveryPhysicalRowsAsync(connection, transaction, target, scope, cancellationToken).ConfigureAwait(false))
            {
                if (string.Equals(row.TimeoutId, intent.TimeoutId, StringComparison.Ordinal) && !protectedNewer)
                { _ = await DeleteRecoveryPhysicalRowAsync(connection, transaction, target, row, cancellationToken).ConfigureAwait(false); }
            }
            var cutoff = old.CancelThrough > journal.ObservedRevision ? old.CancelThrough : journal.ObservedRevision;
            await WriteRecoveryTimerAsync(connection, transaction, target, scope, intent.TimeoutId,
                old with { Cancelled = old.Cancelled || !protectedNewer, CancelThrough = cutoff,
                    CancelRevision = old.CancelThrough > journal.ObservedRevision ? old.CancelRevision : revision }, cancellationToken).ConfigureAwait(false);
            return new(protectedNewer ? SagaTimerRecoveryIntentOutcome.ProtectedNewerTimer : SagaTimerRecoveryIntentOutcome.Applied, revision);
        }
        if (intent.Action != SagaTimerAction.CancelAll) { throw new InvalidDataException("Unknown timer recovery action."); }
        var protectedAny = false;
        foreach (var row in await ReadRecoveryPhysicalRowsAsync(connection, transaction, target, scope, cancellationToken).ConfigureAwait(false))
        {
            var old = await ReadRecoveryTimerAsync(connection, transaction, target, scope.Id, row.TimeoutId, cancellationToken).ConfigureAwait(false);
            var own = old is not null && old.Tracked && old.Incarnation == journal.IncarnationId && old.Sequence == journal.Sequence && old.CreatedRevision == revision;
            if (!own && (old?.CreatedRevision ?? 0) > journal.ObservedRevision) { protectedAny = true; continue; }
            _ = await DeleteRecoveryPhysicalRowAsync(connection, transaction, target, row, cancellationToken).ConfigureAwait(false);
            if (own)
            { await WriteRecoveryTimerAsync(connection, transaction, target, scope, row.TimeoutId, old! with { Cancelled = true }, cancellationToken).ConfigureAwait(false); }
        }
        _ = await ExecuteRecoveryAsync(connection, transaction,
            $"UPDATE {target.Scopes} SET CancelAllRevision=CASE WHEN CancelAllThrough>:Through THEN CancelAllRevision ELSE :Revision END,CancelAllThrough=GREATEST(CancelAllThrough,:Through) WHERE ScopeId=:Scope",
            [Number("Through", journal.ObservedRevision), Number("Revision", revision), Raw("Scope", scope.Id)], cancellationToken).ConfigureAwait(false);
        return new(protectedAny ? SagaTimerRecoveryIntentOutcome.ProtectedNewerTimer : SagaTimerRecoveryIntentOutcome.Applied, revision);
    }
}
