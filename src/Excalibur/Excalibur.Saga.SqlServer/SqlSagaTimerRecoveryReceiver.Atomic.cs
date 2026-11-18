// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Data;
using Excalibur.Dispatch.Messaging;
using Microsoft.Data.SqlClient;
using static Excalibur.Saga.SqlServer.SqlSagaTimerRecoveryRequests;

namespace Excalibur.Saga.SqlServer;

internal sealed partial class SqlSagaTimerRecoveryReceiver
{
    // Called only from the existing SQL atomic writer while it owns its durable-domain lock/transaction.
    // This never commits, rolls back, disposes or transfers the caller's transaction.
    internal static async Task RecordAtomicTimerMutationAsync(SqlConnection connection, SqlTransaction transaction,
        SagaTransitionIdentity identity, SagaPreparedTimerAction action, string? timeoutId, CancellationToken token)
    {
        const string table = "[dbo].[SagaTimeouts]";
        if (!(await connection.ResolveAsync(new RecoveryMetadataAvailableRequest(table, transaction, token)).ConfigureAwait(false)).Single())
        { return; }
        var scope = await LockRecoveryScopeForTargetAsync(connection, transaction,
            new(identity.TenantId, identity.SagaId), table, create: true, token).ConfigureAwait(false);
        _ = await connection.ResolveAsync(new RecordAtomicRecoveryTimerRequest(scope, identity, action, timeoutId,
            transaction, token)).ConfigureAwait(false);
    }
}
