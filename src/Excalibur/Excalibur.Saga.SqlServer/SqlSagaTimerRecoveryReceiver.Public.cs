// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Data;
using Excalibur.Saga.Abstractions;
using Microsoft.Data.SqlClient;
using static Excalibur.Saga.SqlServer.SqlSagaTimerRecoveryRequests;
using static Excalibur.Saga.SqlServer.SqlSagaTimerRecoveryState;

namespace Excalibur.Saga.SqlServer;

internal sealed partial class SqlSagaTimerRecoveryReceiver
{
    internal async Task<bool> TryScheduleWithRecoveryAuthorityAsync(SqlConnection connection, string table, SagaTimeout timeout, CancellationToken token)
    {
        var tenant = _getTenant();
        var available = (await connection.ResolveAsync(new RecoveryMetadataAvailableRequest(table, null, token)).ConfigureAwait(false)).Single();
        if (!available) { return false; }
        return await RunRecoveryTransactionAsync(async (ownedConnection, transaction) =>
        {
            connection = ownedConnection;
            RecoveryScopeRow? scope = null;
            if (Guid.TryParse(timeout.SagaId, out var sagaId) && sagaId != Guid.Empty)
            {
                scope = await LockRecoveryScopeForTargetAsync(connection, transaction, new(tenant, sagaId), table, create: true, token).ConfigureAwait(false);
            }
            _ = await connection.ResolveAsync(new PublicRecoveryScheduleRequest(table, scope, timeout, tenant,
                transaction, token)).ConfigureAwait(false);
            return true;
        }, token, connection).ConfigureAwait(false);
    }

    internal async Task<(bool Handled, int Count)> TryCancelWithRecoveryAuthorityAsync(SqlConnection connection, string table, string sagaId, string? timeoutId, CancellationToken token)
    {
        var tenant = _getTenant();
        var available = (await connection.ResolveAsync(new RecoveryMetadataAvailableRequest(table, null, token)).ConfigureAwait(false)).Single();
        if (!available) { return (false, 0); }
        return await RunRecoveryTransactionAsync(async (ownedConnection, transaction) =>
        {
            connection = ownedConnection;
            RecoveryScopeRow? scope = null;
            if (Guid.TryParse(sagaId, out var id) && id != Guid.Empty)
            {
                scope = await LockRecoveryScopeForTargetAsync(connection, transaction, new(tenant, id), table, create: true, token).ConfigureAwait(false);
                _ = await connection.ResolveAsync(new RecordPublicRecoveryCancellationRequest(scope, timeoutId, transaction, token)).ConfigureAwait(false);
            }
            // The existing atomic03/04 membership tombstone and payload DELETE remain in this SAME
            // transaction. Its durable-domain lock is reentrant and was acquired before the receiver.
            var owned = (await connection.ResolveAsync(new RecoveryAtomicMetadataRequest(table, transaction, token)).ConfigureAwait(false)).Single();
            var count = await SqlServerSagaTimeoutStore.ExecuteRetainedTimerCancellationAsync(connection, transaction,
                table, owned, tenant, sagaId, timeoutId, scope is not null, token).ConfigureAwait(false);
            return (true, count);
        }, token, connection).ConfigureAwait(false);
    }
}
