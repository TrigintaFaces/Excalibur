// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Data;
using Excalibur.Dispatch.Messaging;
using Excalibur.Saga.Abstractions;
using Microsoft.Data.SqlClient;
using static Excalibur.Saga.SqlServer.SqlSagaTimerRecoveryRequests;

namespace Excalibur.Saga.SqlServer;

internal sealed partial class SqlSagaTimerRecoveryReceiver
{
    internal async Task LockFailureDomainAsync(SqlConnection connection, SqlTransaction transaction,
        string table, string tenant, string saga, CancellationToken token)
    {
        RequireRecoveryTenant(tenant);
        if (!Guid.TryParse(saga, out var id) || id == Guid.Empty) { return; }
        // Same existing order: optional atomic scope, then optional selected ordinary receiver domain.
        if ((await connection.ResolveAsync(new RecoveryMetadataAvailableRequest(table, transaction, token)).ConfigureAwait(false)).Single())
        {
            _ = await LockRecoveryScopeForTargetAsync(connection, transaction, new(tenant, id), table, create: true, token).ConfigureAwait(false);
        }
        else
        {
            await SqlServerSagaAtomicTransitionStore.LockFailureDomainAsync(connection, transaction, table, tenant, id, token).ConfigureAwait(false);
        }
    }

    internal async Task<bool> LockFailureAuthorityAsync(SqlConnection connection, SqlTransaction transaction,
        string table, SagaTimeout timeout, SagaTimeoutDeliveryIdentity? original, CancellationToken token)
    {
        await LockFailureDomainAsync(connection, transaction, table, timeout.TenantId!, timeout.SagaId, token).ConfigureAwait(false);
        if (original is null) { return true; }
        if (!Guid.TryParse(timeout.SagaId, out var saga) || saga != original.Scope.SagaId
            || timeout.TenantId != original.Scope.TenantId || timeout.TimeoutId != original.TimeoutId) { return false; }
        if ((await connection.ResolveAsync(new RecoveryMetadataAvailableRequest(table, transaction, token)).ConfigureAwait(false)).Single())
        {
            var scope = await LockRecoveryScopeForTargetAsync(connection, transaction,
                new(timeout.TenantId!, saga), table, create: false, token).ConfigureAwait(false);
            var timer = (await connection.ResolveAsync(new ReadRecoveryTimerRequest(scope.Id, timeout.TimeoutId, transaction, token)).ConfigureAwait(false)).SingleOrDefault();
            if (timer is { Tracked: true })
            {
                if (timer.Incarnation != original.IncarnationId || timer.Sequence != original.Generation
                    || timer.StateType != original.Scope.StateType)
                { throw new InvalidOperationException("Original tracked timeout metadata differs from immutable receiver authority."); }
                var through = Math.Max(scope.CancelAllThrough, timer.CancelThrough);
                return scope.Incarnation == original.IncarnationId && !timer.Cancelled && timer.CreatedRevision > through;
            }
        }
        // Atomic03/04 authority works without optional ordinary-recovery06. Neither path fabricates currentness.
        return await SqlServerSagaAtomicTransitionStore.IsFailureAuthorityCurrentAsync(connection, transaction,
            table, original, token).ConfigureAwait(false);
    }
}
