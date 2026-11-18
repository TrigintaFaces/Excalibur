// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;
using Excalibur.Data;
using Excalibur.Dispatch.Messaging;
using Microsoft.Data.SqlClient;

namespace Excalibur.Saga.SqlServer;

internal sealed partial class SqlServerSagaAtomicTransitionStore
{
    public async ValueTask<SagaDurableSnapshot?> LoadSnapshotAsync(SagaScopeIdentity identity,CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identity);
        ValidateTenant(identity.TenantId);
        return await ReadOwnedAsync<SagaDurableSnapshot?>(async(connection,transaction,token)=>
        {
        var scope=await LockScopeAsync(connection,transaction,identity.TenantId,identity.SagaId,token).ConfigureAwait(false);

        var rows=await connection.ResolveAsync(new LoadSnapshotAsyncRequest(command=>
        {
            Add(command,"TenantKey",TenantKey(identity.TenantId),DbType.Binary);
            Add(command,"TenantBytes",ExactBytes(identity.TenantId),DbType.Binary);
            Add(command,"TenantLength",checked(identity.TenantId.Length*sizeof(char)),DbType.Int32);
            Add(command,"SagaId",identity.SagaId,DbType.Guid);
            Add(command,"StateType",identity.StateType,DbType.String);
        }, transaction, token)).ConfigureAwait(false);
        var state=rows.SingleOrDefault();
        if (state?.PendingTimerBatch is not null)
        { throw new InvalidOperationException("An unresolved ordinary timer journal must be recovered before entering the atomic profile."); }
        if (scope is not null && state?.RecoveryIncarnation is { } recoveryIncarnation
            && recoveryIncarnation != scope.IncarnationId.ToString("N"))
        { throw new InvalidOperationException("Durable scope and state row have different lifetime authority."); }
        if(scope is null)
        {
            if(state is not null){throw new InvalidOperationException("An existing state-only row lacks durable ownership metadata; use the supported durable profile for new state; populated state-only rows are not adopted automatically.");}
            return null;
        }
        if(!string.Equals(scope.SagaType,identity.StateType,StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The saga GUID belongs to a different persisted state type; refusing a second namespace.");
        }
        if(state is not null&&(!string.Equals(state.StateType,identity.StateType,StringComparison.Ordinal)
            || state.Version!=scope.CommittedVersion || state.Completed!=scope.IsClosed))
        {
            throw new InvalidOperationException("Durable scope and persisted state do not share the same committed authority.");
        }
        if(state is null&&!scope.IsClosed){throw new InvalidOperationException("An active durable scope has lost its state payload.");}
        var result=new SagaDurableSnapshot(scope.IncarnationId,scope.CommittedVersion,scope.Generation,
            scope.NextEffectSequence,scope.IsClosed,state?.Json is null?null:StrictUtf8.GetBytes(state.Json),state?.CompletedAt);
        return result;
        },cancellationToken).ConfigureAwait(false);
    }

    private sealed record StateRow(string StateType,string? Json,long Version,bool Completed,DateTimeOffset? CompletedAt,string? RecoveryIncarnation,string? PendingTimerBatch);
}
