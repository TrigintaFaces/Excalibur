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
    private static async Task<ScopeRow?> LockScopeAsync(SqlConnection connection, SqlTransaction transaction,
        string tenantId, Guid sagaId, CancellationToken token)
    {

        var rows = await connection.ResolveAsync(new LockScopeAsyncRequest(command =>
            {
                Add(command,"TenantKey",TenantKey(tenantId),DbType.Binary);
                Add(command,"SagaId",sagaId,DbType.Guid);
                Add(command,"TenantBytes",ExactBytes(tenantId),DbType.Binary);
                Add(command,"TenantLength",checked(tenantId.Length*sizeof(char)),DbType.Int32);
            }, transaction, token)).ConfigureAwait(false);
        var row=rows.SingleOrDefault();
        if(row is null)
        {

            var collision=await connection.ResolveAsync(new LockScopeAsyncRequest2(command=>
            {
                Add(command,"TenantKey",TenantKey(tenantId),DbType.Binary);Add(command,"SagaId",sagaId,DbType.Guid);
            }, transaction, token)).ConfigureAwait(false);
            if(collision is long count&&count!=0){throw new InvalidOperationException("Tenant hash collision or inconsistent scope key; refusing absent-scope admission.");}
        }
        return row;
    }

    private static async Task<ScopeRow> CreateScopeAsync(SqlConnection connection, SqlTransaction transaction,
        SagaTransitionIdentity identity, CancellationToken token)
    {
        var scopeId = Guid.NewGuid();

        var changed = await connection.ResolveAsync(new CreateScopeAsyncRequest(command =>
        {
            BindIdentity(command, identity);
            Add(command, "ScopeId", scopeId, DbType.Guid);
            Add(command, "SagaType", identity.SagaType, DbType.String);
            Add(command, "IncarnationId", identity.IncarnationId, DbType.Guid);
        }, transaction, token)).ConfigureAwait(false);
        if (changed != 1) { throw new InvalidOperationException("Durable scope insertion did not affect exactly one row."); }
        return new(scopeId, identity.TenantId, identity.SagaType, identity.IncarnationId, 0, false, 0, 0, false);
    }

    private static async Task<ReceiptRow?> FindReceiptAsync(SqlConnection connection, SqlTransaction transaction,
        Guid scopeId, Guid incarnationId, Guid transitionId, CancellationToken token)
    {

        var rows = await connection.ResolveAsync(new FindReceiptAsyncRequest(command =>
        {
            Add(command,"ScopeId",scopeId,DbType.Guid);
            Add(command,"IncarnationId",incarnationId,DbType.Guid);
            Add(command,"TransitionId",transitionId,DbType.Guid);
        }, transaction, token)).ConfigureAwait(false);
        return rows.SingleOrDefault();
    }

    private static async Task<ReceiptRow?> FindDeliveryAsync(SqlConnection connection, SqlTransaction transaction,
        Guid scopeId, Guid incarnationId, string deliveryId, CancellationToken token)
    {

        var rows = await connection.ResolveAsync(new FindDeliveryAsyncRequest(command =>
        {
            Add(command,"ScopeId",scopeId,DbType.Guid);
            Add(command,"IncarnationId",incarnationId,DbType.Guid);
            Add(command,"DeliveryHash",SHA256.HashData(ExactBytes(deliveryId)),DbType.Binary);
        }, transaction, token)).ConfigureAwait(false);
        var row=rows.SingleOrDefault();
        if(row is not null && !string.Equals(row.DeliveryId,deliveryId,StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Delivery hash collision or corrupted receipt identity; refusing replay admission.");
        }
        return row;
    }
}
