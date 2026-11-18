// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Data;
using Excalibur.Dispatch.Messaging;
using Microsoft.Data.SqlClient;
using static Excalibur.Saga.SqlServer.SqlSagaTimerRecoveryRequests;

namespace Excalibur.Saga.SqlServer;

internal sealed partial class SqlServerSagaAtomicTransitionStore
{
    internal static async Task LockFailureDomainAsync(SqlConnection connection, SqlTransaction transaction,
        string table, string tenant, Guid saga, CancellationToken token)
    {
        if ((await connection.ResolveAsync(new RecoveryAtomicMetadataRequest(table, transaction, token)).ConfigureAwait(false)).Single())
        { _ = await LockScopeAsync(connection, transaction, tenant, saga, token).ConfigureAwait(false); }
    }

    internal static async Task<bool> IsFailureAuthorityCurrentAsync(SqlConnection connection, SqlTransaction transaction,
        string table, SagaTimeoutDeliveryIdentity original, CancellationToken token)
    {
        if (!(await connection.ResolveAsync(new RecoveryAtomicMetadataRequest(table, transaction, token)).ConfigureAwait(false)).Single())
        { throw new InvalidOperationException("Original owned timeout authority is missing from the selected backend."); }
        var scope = await LockScopeAsync(connection, transaction, original.Scope.TenantId, original.Scope.SagaId, token).ConfigureAwait(false);
        if (scope is null || scope.TenantId != original.Scope.TenantId
            || (scope.IncarnationId == original.IncarnationId && scope.SagaType != original.Scope.StateType))
        { throw new InvalidOperationException("Original owned timeout scope is missing or inconsistent."); }
        return await IsOriginalTimeoutCurrentAsync(connection, transaction, scope, original, token).ConfigureAwait(false);
    }
}
