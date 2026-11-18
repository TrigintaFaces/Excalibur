// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;
using Excalibur.Data;
using Excalibur.Dispatch.Messaging;
using Microsoft.Data.SqlClient;

namespace Excalibur.Saga.SqlServer;

internal sealed partial class SqlServerSagaAtomicTransitionStore
{
    private static async Task<long> ApplyTimersAsync(SqlConnection connection,SqlTransaction transaction,Guid scopeId,
        SagaPreparedTransition transition,CancellationToken token)
    {
        var generation=transition.Generation;
        for(var ordinal=0;ordinal<transition.Timers.Count;ordinal++)
        {
            var mutation=transition.Timers[ordinal];
            if(mutation.Generation!=generation)
            {
                throw new InvalidOperationException("Prepared timer generation does not follow the ordered cancellation boundary.");
            }
            switch(mutation.Action)
            {
                case SagaPreparedTimerAction.Schedule:
                    await ScheduleAsync(connection,transaction,scopeId,transition,mutation,ordinal,token).ConfigureAwait(false);
                    break;
                case SagaPreparedTimerAction.Cancel:
                    await CancelTimerAsync(connection,transaction,scopeId,transition,mutation.TimeoutId,token).ConfigureAwait(false);
                    break;
                case SagaPreparedTimerAction.CancelAll:
                    await CancelGenerationAsync(connection,transaction,scopeId,transition,token).ConfigureAwait(false);
                    generation=checked(generation+1);
                    break;
                default:
                    throw new InvalidOperationException("Unknown prepared timer operation.");
            }
        }
        if(transition.Completed && transition.HasTimerParticipant)
        {
            // Completion seals the whole incarnation, including schedules earlier in this same transition.
            await CancelGenerationAsync(connection,transaction,scopeId,transition,token).ConfigureAwait(false);
        }
        return generation;
    }

    private static async Task ScheduleAsync(SqlConnection connection,SqlTransaction transaction,Guid scopeId,
        SagaPreparedTransition transition,SagaPreparedTimerMutation mutation,int ordinal,CancellationToken token)
    {
        await SqlServerSagaTimeoutStore.RecordAtomicTimerMutationAsync(connection, transaction,
            transition.Identity, SagaPreparedTimerAction.Schedule, mutation.TimeoutId, token).ConfigureAwait(false);
        // Receipt insertion precedes timer mutations, so this also catches same-batch
        // TimeoutId == original delivery ID. Scope lock serializes the reverse ordering.
        await RefuseTimerDeliveryCollisionAsync(connection, transaction, scopeId, mutation.TimeoutId, token).ConfigureAwait(false);

        var changed=await connection.ResolveAsync(new ScheduleAsyncRequest(command=>
        {
            BindIdentity(command,transition.Identity);
            Add(command,"TimeoutId",mutation.TimeoutId,DbType.String);
            Add(command,"SagaIdText",transition.Identity.SagaId.ToString(),DbType.String);
            Add(command,"SagaRoutingType",mutation.SagaType,DbType.String);
            Add(command,"TimeoutType",mutation.TimeoutType,DbType.String);
            Add(command,"Data",mutation.GetData(),DbType.Binary);
            Add(command,"DueAt",mutation.DueAt,DbType.DateTimeOffset);
            Add(command,"ScheduledAt",mutation.ScheduledAt,DbType.DateTimeOffset);
            Add(command,"ScopeId",scopeId,DbType.Guid);
            Add(command,"IncarnationId",transition.Identity.IncarnationId,DbType.Guid);
            Add(command,"Generation",mutation.Generation,DbType.Int64);
            Add(command,"TransitionId",transition.Identity.TransitionId,DbType.Guid);
            Add(command,"StateType",transition.Identity.SagaType,DbType.String);
            Add(command,"Ordinal",ordinal,DbType.Int32);
        }, transaction, token)).ConfigureAwait(false);
        if(changed!=2){throw new InvalidOperationException("Timer schedule and ownership membership must both be inserted.");}
    }

    private static async Task CancelTimerAsync(SqlConnection connection,SqlTransaction transaction,Guid scopeId,
        SagaPreparedTransition transition,string timeoutId,CancellationToken token)
    {

        await SqlServerSagaTimeoutStore.RecordAtomicTimerMutationAsync(connection, transaction,
            transition.Identity, SagaPreparedTimerAction.Cancel, timeoutId, token).ConfigureAwait(false);
        await connection.ResolveAsync(new CancelTimerAsyncRequest(command=>
        {
            BindIdentity(command,transition.Identity);
            Add(command,"ScopeId",scopeId,DbType.Guid);
            Add(command,"IncarnationId",transition.Identity.IncarnationId,DbType.Guid);
            Add(command,"TimeoutId",timeoutId,DbType.String);
        }, transaction, token)).ConfigureAwait(false);
    }

    private static async Task CancelGenerationAsync(SqlConnection connection,SqlTransaction transaction,Guid scopeId,
        SagaPreparedTransition transition,CancellationToken token)
    {

        await SqlServerSagaTimeoutStore.RecordAtomicTimerMutationAsync(connection, transaction,
            transition.Identity, SagaPreparedTimerAction.CancelAll, null, token).ConfigureAwait(false);
        await connection.ResolveAsync(new CancelGenerationAsyncRequest(command=>
        {
            BindIdentity(command,transition.Identity);
            Add(command,"ScopeId",scopeId,DbType.Guid);
            Add(command,"IncarnationId",transition.Identity.IncarnationId,DbType.Guid);
        }, transaction, token)).ConfigureAwait(false);
    }
}
