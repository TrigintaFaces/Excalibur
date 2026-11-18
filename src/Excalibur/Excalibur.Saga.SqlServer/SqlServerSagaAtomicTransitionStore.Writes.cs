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
    private static async Task<int> WriteStateAsync(SqlConnection connection,SqlTransaction transaction,
        SagaPreparedTransition transition,long version,CancellationToken token)
    {

        return await connection.ResolveAsync(new WriteStateAsyncRequest(command=>
        {
            BindIdentity(command,transition.Identity);
            Add(command,"SagaType",transition.Identity.SagaType,DbType.String);
            Add(command,"StateJson",StrictUtf8.GetString(transition.GetEncodedState()),DbType.String);
            Add(command,"Completed",transition.Completed,DbType.Boolean);
            Add(command,"CompletedAt",transition.CompletedAt,DbType.DateTimeOffset);
            Add(command,"ExpectedVersion",transition.ExpectedVersion,DbType.Int64);
            Add(command,"Version",version,DbType.Int64);
            Add(command,"RecoveryIncarnation",transition.Identity.IncarnationId.ToString("N"),DbType.String);
        }, transaction, token)).ConfigureAwait(false);
    }

    private static async Task InsertReceiptAsync(SqlConnection connection,SqlTransaction transaction,Guid scopeId,
        SagaPreparedTransition transition,long version,CancellationToken token)
    {

        await connection.ResolveAsync(new InsertReceiptAsyncRequest(command=>
        {
            Add(command,"ScopeId",scopeId,DbType.Guid);
            Add(command,"IncarnationId",transition.Identity.IncarnationId,DbType.Guid);
            Add(command,"TransitionId",transition.Identity.TransitionId,DbType.Guid);
            Add(command,"ExpectedVersion",transition.ExpectedVersion,DbType.Int64);
            Add(command,"Version",version,DbType.Int64);
            Add(command,"Digest",transition.GetDigest(),DbType.Binary);
            Add(command,"DeliveryHash",transition.Identity.DeliveryId is null?null:SHA256.HashData(ExactBytes(transition.Identity.DeliveryId)),DbType.Binary);
            Add(command,"DeliveryId",transition.Identity.DeliveryId,DbType.String);
            Add(command,"Manifest",EncodeManifest(transition),DbType.Binary);
            Add(command,"TimerCount",transition.Timers.Count,DbType.Int32);
            Add(command,"MessageCount",transition.Writes.Count,DbType.Int32);
        }, transaction, token)).ConfigureAwait(false);
    }

    private static async Task InsertMembershipAsync(SqlConnection connection,SqlTransaction transaction,Guid scopeId,
        SagaPreparedTransition transition,string messageId,int ordinal,long sequence,CancellationToken token)
    {

        var changed=await connection.ResolveAsync(new InsertMembershipAsyncRequest(command=>
        {
            Add(command,"MessageId",messageId,DbType.String);
            Add(command,"ScopeId",scopeId,DbType.Guid);
            Add(command,"IncarnationId",transition.Identity.IncarnationId,DbType.Guid);
            Add(command,"TransitionId",transition.Identity.TransitionId,DbType.Guid);
            Add(command,"Ordinal",ordinal,DbType.Int32);
            Add(command,"Sequence",sequence,DbType.Int64);
            Add(command,"WireHash",SHA256.HashData(transition.Writes[ordinal].GetCanonicalWireBytes()),DbType.Binary);
        }, transaction, token)).ConfigureAwait(false);
        if(changed!=1){throw new InvalidOperationException("Durable message membership insertion must affect one row.");}
    }

    private static async Task UpdateScopeAsync(SqlConnection connection,SqlTransaction transaction,Guid scopeId,
        SagaPreparedTransition transition,long version,long generation,CancellationToken token)
    {

        var changed=await connection.ResolveAsync(new UpdateScopeAsyncRequest(command=>
        {
            Add(command,"Version",version,DbType.Int64);
            Add(command,"Generation",generation,DbType.Int64);
            Add(command,"Completed",transition.Completed,DbType.Boolean);
            Add(command,"HasTimerParticipant",transition.HasTimerParticipant,DbType.Boolean);
            Add(command,"NextSequence",checked(transition.FirstSequence+transition.Writes.Count),DbType.Int64);
            Add(command,"ScopeId",scopeId,DbType.Guid);
            Add(command,"IncarnationId",transition.Identity.IncarnationId,DbType.Guid);
            Add(command,"ExpectedVersion",transition.ExpectedVersion,DbType.Int64);
        }, transaction, token)).ConfigureAwait(false);
        if(changed!=1){throw new InvalidOperationException("Durable scope compare-and-swap lost its locked authority.");}
    }

    private static byte[] EncodeManifest(SagaPreparedTransition transition)
    {
        // Retain identity and digests, not a second payload archive that changes production retention.
        using var buffer=new MemoryStream();
        using var writer=new BinaryWriter(buffer,System.Text.Encoding.UTF8,leaveOpen:true);
        writer.Write(1);
        writer.Write(transition.Writes.Count);
        foreach(var write in transition.Writes)
        {
            var id=ExactBytes(write.MessageId);
            writer.Write(id.Length);writer.Write(id);
            var digest=SHA256.HashData(write.GetCanonicalWireBytes());writer.Write(digest.Length);writer.Write(digest);
        }
        writer.Write(transition.Timers.Count);
        foreach(var timer in transition.Timers)
        {
            var id=ExactBytes(timer.TimeoutId);writer.Write(id.Length);writer.Write(id);
            writer.Write((int)timer.Action);writer.Write(timer.Generation);
        }
        writer.Flush();return buffer.ToArray();
    }
}
