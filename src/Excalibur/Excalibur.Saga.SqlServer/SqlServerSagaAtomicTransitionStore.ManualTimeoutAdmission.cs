// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;
using System.Security.Cryptography;

using Excalibur.Data;
using Excalibur.Dispatch.Messaging;

using Microsoft.Data.SqlClient;

namespace Excalibur.Saga.SqlServer;

internal sealed partial class SqlServerSagaAtomicTransitionStore
{
    public async ValueTask<bool> HasOwnedTimeoutIdentityAsync(SagaScopeIdentity identity, string deliveryId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentException.ThrowIfNullOrEmpty(deliveryId);
        ValidateTenant(identity.TenantId);
        return await ReadOwnedAsync<bool>(async (connection, transaction, token) =>
        {
            await EnsureTimeoutAuthoritySchemaAsync(connection, transaction, token).ConfigureAwait(false);
            var scope = await LockScopeAsync(connection, transaction, identity.TenantId, identity.SagaId, token).ConfigureAwait(false);
            if (scope is null)
            {
                return false; // synchronized absence; an IO/schema failure is never this outcome
            }

            if (!string.Equals(scope.TenantId, identity.TenantId, StringComparison.Ordinal)
                || !string.Equals(scope.SagaType, identity.StateType, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Manual timeout lookup conflicts with the authoritative scope.");
            }

            return await HasOwnedTimerIdentityAsync(connection, transaction, scope.ScopeId, deliveryId, token).ConfigureAwait(false);
        }, cancellationToken).ConfigureAwait(false);
    }

    private static async Task EnsureTimeoutAuthoritySchemaAsync(SqlConnection connection, SqlTransaction transaction,
        CancellationToken cancellationToken)
    {

        var result = await connection.ResolveAsync(new EnsureTimeoutAuthoritySchemaAsyncRequest(static _ => { }, transaction, cancellationToken))
            .ConfigureAwait(false);
        if (result is not 1)
        {
            throw new InvalidOperationException("Complete the owned-timeout authority schema before admission.");
        }
    }

    private static async Task ValidateManualTimeoutAdmissionAsync(SqlConnection connection, SqlTransaction transaction,
        Guid scopeId, SagaPreparedTransition transition, CancellationToken cancellationToken)
    {
        if (!transition.IsManualTimeout)
        {
            return;
        }

        var identity = transition.Identity.DeliveryId
            ?? throw new InvalidOperationException("Manual timeout admission has no original delivery identity.");
        if (await HasOwnedTimerIdentityAsync(connection, transaction, scopeId, identity, cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("An owned timeout identity cannot be accepted as a manual timeout.");
        }
    }

    private static async Task<bool> HasOwnedTimerIdentityAsync(SqlConnection connection, SqlTransaction transaction,
        Guid scopeId, string deliveryId, CancellationToken cancellationToken)
    {
        // No generation/cancelled/row-exists filter: historical owned IDs cannot become manual.

        var result = await connection.ResolveAsync(new HasOwnedTimerIdentityAsyncRequest(command =>
        {
            Add(command, "ScopeId", scopeId, DbType.Guid);
            Add(command, "DeliveryId", deliveryId, DbType.String);
        }, transaction, cancellationToken)).ConfigureAwait(false);
        return result switch
        {
            0 => false,
            1 => true,
            _ => throw new InvalidOperationException("Timer ownership lookup returned no authoritative scalar."),
        };
    }

    private static async Task RefuseTimerDeliveryCollisionAsync(SqlConnection connection, SqlTransaction transaction,
        Guid scopeId, string timeoutId, CancellationToken cancellationToken)
    {
        // Any matching hash refuses: either an actual ID collision or an ambiguous hash collision.
        // Hash equality alone is never accepted as proof that two opaque strings are equal.

        var result = await connection.ResolveAsync(new RefuseTimerDeliveryCollisionAsyncRequest(command =>
        {
            Add(command, "ScopeId", scopeId, DbType.Guid);
            Add(command, "DeliveryHash", SHA256.HashData(ExactBytes(timeoutId)), DbType.Binary);
        }, transaction, cancellationToken)).ConfigureAwait(false);
        if (result is not 0)
        {
            throw new InvalidOperationException("A scheduled timeout identity conflicts with retained delivery authority.");
        }
    }
}
