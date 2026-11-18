// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;

using Excalibur.Data;
using Excalibur.Dispatch.Messaging;

namespace Excalibur.Saga.SqlServer;

internal sealed partial class SqlServerSagaAtomicTransitionStore
{
    public async ValueTask<bool> IsTimeoutDeliveryCurrentAsync(SagaTimeoutDeliveryIdentity delivery,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        ValidateTenant(delivery.Scope.TenantId);
        return await ReadOwnedAsync<bool>(async (connection, transaction, token) =>
        {
            var scope = await LockScopeAsync(connection, transaction, delivery.Scope.TenantId,
                delivery.Scope.SagaId, token).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Original timeout scope authority is missing.");
            if (!string.Equals(scope.TenantId, delivery.Scope.TenantId, StringComparison.Ordinal)
                || !string.Equals(scope.SagaType, delivery.Scope.StateType, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Original timeout scope does not match the selected authority.");
            }

            return await IsOriginalTimeoutCurrentAsync(connection, transaction, scope, delivery, token).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<bool> IsOriginalTimeoutCurrentAsync(Microsoft.Data.SqlClient.SqlConnection connection,
        Microsoft.Data.SqlClient.SqlTransaction transaction, ScopeRow scope, SagaTimeoutDeliveryIdentity delivery, CancellationToken token)
    {

        var rows = await connection.ResolveAsync(new IsOriginalTimeoutCurrentAsyncRequest(command =>
        {
            Add(command, "ScopeId", scope.ScopeId, DbType.Guid);
            Add(command, "TimeoutId", delivery.TimeoutId, DbType.String);
        }, transaction, token)).ConfigureAwait(false);
        var original = rows.SingleOrDefault()
            ?? throw new InvalidOperationException("Original timeout membership authority is missing.");
        if (original.IncarnationId != delivery.IncarnationId || original.Generation != delivery.Generation
            || !string.Equals(original.StateType, delivery.Scope.StateType, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Delivered timeout metadata differs from its immutable original membership.");
        }

        // Payload row presence is deliberately irrelevant. Queue acceptance can retire that
        // row before this handler runs; only retained authority can revoke its logical delivery.
        return !scope.IsClosed && !original.IsCancelled
            && scope.IncarnationId == original.IncarnationId && scope.Generation == original.Generation;
    }

    private sealed record OriginalTimerAuthority(Guid IncarnationId, long Generation, string StateType, bool IsCancelled);
}
