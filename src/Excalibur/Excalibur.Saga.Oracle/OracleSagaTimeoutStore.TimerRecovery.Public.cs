// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.Saga.Abstractions;
using Oracle.ManagedDataAccess.Client;

using static Excalibur.Saga.Oracle.OracleSagaTimerRecoveryRequests;

namespace Excalibur.Saga.Oracle;

internal sealed partial class OracleSagaTimerRecovery
{
    internal Task<bool> ScheduleWithRecoveryOrLegacyAsync(SagaTimeout timeout, string tenant,
        RecoveryTargetBinding target, CancellationToken cancellationToken) =>
        RunRecoveryTransactionAsync(async (connection, transaction) =>
        {
            if (!await RecoveryMetadataAvailableAsync(connection, transaction, target, cancellationToken).ConfigureAwait(false))
            {
                _ = await ExecuteRecoveryAsync(connection, transaction,
                    $"INSERT INTO {target.Payload}(TimeoutId,SagaId,SagaType,TimeoutType,TimeoutData,DueAt,ScheduledAt,TenantId) VALUES(:Id,:Saga,:SagaType,:TimeoutType,:Data,:Due,:Scheduled,:Tenant)",
                    [Text("Id", timeout.TimeoutId, 450), Text("Saga", timeout.SagaId, 450), Text("SagaType", timeout.SagaType), Text("TimeoutType", timeout.TimeoutType),
                        new("Data", OracleDbType.Blob, timeout.TimeoutData), new("Due", OracleDbType.TimeStampTZ, timeout.DueAt),
                        new("Scheduled", OracleDbType.TimeStampTZ, timeout.ScheduledAt), Text("Tenant", tenant, 64)], cancellationToken).ConfigureAwait(false);
                return true;
            }
            var selected = await RequireRecoveryTargetAsync(connection, transaction, target, cancellationToken).ConfigureAwait(false);
            RecoveryScopeRow? scope = null;
            if (Guid.TryParse(timeout.SagaId, out var sagaId) && sagaId != Guid.Empty)
            { scope = await LockRecoveryScopeAsync(connection, transaction, new(tenant, sagaId), target, create: true, cancellationToken).ConfigureAwait(false); }
            await RefuseTrackedRecoveryIdAsync(connection, transaction, target, selected, timeout.TimeoutId, cancellationToken).ConfigureAwait(false);
            if (scope is not null)
            {
                var previous = await ReadRecoveryTimerAsync(connection, transaction, target, scope.Id, timeout.TimeoutId, cancellationToken).ConfigureAwait(false);
                if (previous?.CancelThrough >= 0)
                { throw new InvalidOperationException("Public Schedule cannot overwrite a retained cancelled timer identity."); }
                var revision = checked(scope.Revision + 1);
                await WriteRecoveryTimerAsync(connection, transaction, target, scope, timeout.TimeoutId,
                    new(false, null, null, null, revision, false, -1, 0), cancellationToken).ConfigureAwait(false);
                _ = await ExecuteRecoveryAsync(connection, transaction, $"UPDATE {target.Scopes} SET MutationRevision=:Revision WHERE ScopeId=:Scope",
                    [Number("Revision", revision), Raw("Scope", scope.Id)], cancellationToken).ConfigureAwait(false);
            }
            await InsertRecoveryPhysicalAsync(connection, transaction, target, timeout, tenant, scope?.SagaId,
                null, null, null, cancellationToken).ConfigureAwait(false);
            return true;
        }, cancellationToken);

    internal Task<int> CancelWithRecoveryOrLegacyAsync(string sagaId, string? timeoutId, string tenant,
        RecoveryTargetBinding target, CancellationToken cancellationToken) =>
        RunRecoveryTransactionAsync(async (connection, transaction) =>
        {
            var available = await RecoveryMetadataAvailableAsync(connection, transaction, target, cancellationToken).ConfigureAwait(false);
            RecoveryScopeRow? scope = null;
            if (available && Guid.TryParse(sagaId, out var id) && id != Guid.Empty)
            {
                scope = await LockRecoveryScopeAsync(connection, transaction, new(tenant, id), target, create: true, cancellationToken).ConfigureAwait(false);
                var revision = checked(scope.Revision + 1);
                if (timeoutId is null)
                {
                    _ = await ExecuteRecoveryAsync(connection, transaction,
                        $"UPDATE {target.Scopes} SET CancelAllThrough=:Through,CancelAllRevision=:Revision,MutationRevision=:Revision WHERE ScopeId=:Scope",
                        [Number("Through", scope.Revision), Number("Revision", revision), Raw("Scope", scope.Id)], cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    var fact = await ReadRecoveryTimerAsync(connection, transaction, target, scope.Id, timeoutId, cancellationToken).ConfigureAwait(false)
                        ?? new RecoveryTimerRow(false, null, null, null, -1, false, -1, 0);
                    await WriteRecoveryTimerAsync(connection, transaction, target, scope, timeoutId,
                        fact with { Cancelled = true, CancelThrough = scope.Revision, CancelRevision = revision }, cancellationToken).ConfigureAwait(false);
                    _ = await ExecuteRecoveryAsync(connection, transaction, $"UPDATE {target.Scopes} SET MutationRevision=:Revision WHERE ScopeId=:Scope",
                        [Number("Revision", revision), Raw("Scope", scope.Id)], cancellationToken).ConfigureAwait(false);
                }
            }
            if (scope is not null)
            {
                var removed = 0;
                foreach (var row in await ReadRecoveryPhysicalRowsAsync(connection, transaction, target, scope, cancellationToken).ConfigureAwait(false))
                {
                    if (timeoutId is null || string.Equals(row.TimeoutId, timeoutId, StringComparison.Ordinal))
                    { removed += await DeleteRecoveryPhysicalRowAsync(connection, transaction, target, row, cancellationToken).ConfigureAwait(false); }
                }
                return removed;
            }
            // Non-Guid public saga identifiers preserve their original ordinal string domain. No receiver
            // journal can name them. With no03 the original public predicates/lease behavior remain.
            var filter = timeoutId is null ? string.Empty : " AND TimeoutId=:Id";
            var parameters = new List<RecoveryParameter> { Text("Tenant", tenant, 64), Text("Saga", sagaId, 450) };
            if (timeoutId is not null) { parameters.Add(Text("Id", timeoutId, 450)); }
            return await ExecuteRecoveryAsync(connection, transaction,
                $"DELETE FROM {target.Payload} WHERE TenantId=:Tenant AND SagaId=:Saga" + filter,
                parameters.ToArray(), cancellationToken).ConfigureAwait(false);
        }, cancellationToken);

}
