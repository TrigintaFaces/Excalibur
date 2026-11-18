// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;
using System.Data.Common;
using System.Security.Cryptography;
using Excalibur.Data;
using Excalibur.Dispatch.Messaging;
using Excalibur.Saga.Abstractions;
using Excalibur.Saga.Orchestration;
using Microsoft.Data.SqlClient;
using static Excalibur.Saga.SqlServer.SqlSagaTimerRecoveryState;

namespace Excalibur.Saga.SqlServer;

internal static class SqlSagaTimerRecoveryRequests
{
    private static byte[] RecoveryKey(string value)
    {
        var bytes = new byte[checked(value.Length * sizeof(char))];
        for (var index = 0; index < value.Length; index++)
        { bytes[index * 2] = (byte)value[index]; bytes[(index * 2) + 1] = (byte)(value[index] >> 8); }
        return SHA256.HashData(bytes);
    }

    private static void AddRecoveryParameter(DbCommand command, string name, DbType type, object? value)
    {
        var parameter = new SqlParameter { ParameterName = name, DbType = type, Value = value ?? DBNull.Value };
        if (value is string text) { parameter.Size = text.Length <= 4000 ? 4000 : 0; }
        _ = command.Parameters.Add(parameter);
    }
    internal sealed class RecoveryTargetRequest : DataRequestBase<IDbConnection, IEnumerable<int>>
    {
        internal RecoveryTargetRequest(string target, SqlTransaction transaction, CancellationToken token)
        {
            Parameters.Add("Target", target, DbType.String);
            const string sql = """
                IF OBJECT_ID(@Target,N'U') IS NULL
                    THROW 51004,'Ordinary timer recovery requires the selected direct payload table, not a view or synonym.',1;
                IF OBJECT_ID(N'dispatch.SagaTimerRecoveryTargets',N'U') IS NULL
                    THROW 51004,'Apply ordinary timer recovery metadata for the selected table.',1;
                IF NOT EXISTS(SELECT 1 FROM dispatch.SagaTimerRecoveryTargets WHERE TargetObjectId=OBJECT_ID(@Target,N'U'))
                    THROW 51004,'Register the selected direct payload table in its timer recovery migration.',1;
                IF OBJECT_ID(N'dispatch.SagaTimerRecoveryScopes',N'U') IS NULL
                  OR OBJECT_ID(N'dispatch.SagaTimerRecoveryTimers',N'U') IS NULL
                  OR OBJECT_ID(N'dispatch.SagaTimerRecoveryTokens',N'U') IS NULL
                  OR OBJECT_ID(N'dispatch.SagaTimerRecoveryResults',N'U') IS NULL
                  OR COL_LENGTH(@Target,N'OwnedIncarnationId') IS NULL
                  OR COL_LENGTH(@Target,N'OwnedGeneration') IS NULL
                  OR COL_LENGTH(@Target,N'OwnedStateType') IS NULL
                    THROW 51004,'Apply the complete ordinary timer recovery schema for the selected table.',1;
                SELECT OBJECT_ID(@Target,N'U');
                """;
            Command = CreateCommand(sql, transaction: transaction, cancellationToken: token);
            ResolveAsync = connection => ReadRowsAsync(connection, command =>
                {
                    AddRecoveryParameter(command, "Target", DbType.String, Parameters.Get<string>("Target"));
                }, reader => reader.GetInt32(0));
        }
    }

    internal sealed class LockAtomicRecoveryDomainRequest : DataRequestBase<IDbConnection, int>
    {
        internal LockAtomicRecoveryDomainRequest(int target, SagaTimerRecoveryDomain domain, SqlTransaction transaction, CancellationToken token)
        {
            Parameters.Add("Target", target, DbType.Int32);
            Parameters.Add("Tenant", domain.TenantId, DbType.String);
            Parameters.Add("TenantKey", RecoveryKey(domain.TenantId), DbType.Binary);
            Parameters.Add("SagaId", domain.SagaId, DbType.Guid);
            const string sql = """
                IF @Target=OBJECT_ID(N'dbo.SagaTimeouts',N'U') AND OBJECT_ID(N'dispatch.SagaDurableScopes',N'U') IS NOT NULL
                BEGIN
                  DECLARE @KnownTenant NVARCHAR(64);
                  SELECT @KnownTenant=TenantId FROM dispatch.SagaDurableScopes WITH(UPDLOCK,HOLDLOCK)
                    WHERE TenantKey=@TenantKey AND SagaId=@SagaId;
                  IF @KnownTenant IS NOT NULL AND (DATALENGTH(@KnownTenant)<>DATALENGTH(@Tenant)
                    OR CONVERT(VARBINARY(128),@KnownTenant)<>CONVERT(VARBINARY(128),@Tenant))
                    THROW 51004,'Exact atomic tenant differs from the ordinary timer domain.',1;
                END;
                """;
            Command = CreateCommand(sql, transaction: transaction, cancellationToken: token);
            ResolveAsync = connection => ExecuteNonQueryAsync(connection, command =>
                {
                    AddRecoveryParameter(command, "Target", DbType.Int32, Parameters.Get<int>("Target"));
                    AddRecoveryParameter(command, "Tenant", DbType.String, Parameters.Get<string>("Tenant"));
                    AddRecoveryParameter(command, "TenantKey", DbType.Binary, Parameters.Get<byte[]>("TenantKey"));
                    AddRecoveryParameter(command, "SagaId", DbType.Guid, Parameters.Get<Guid>("SagaId"));
                });
        }
    }

    internal sealed class ReadRecoveryScopeRequest : DataRequestBase<IDbConnection, IEnumerable<RecoveryScopeRow>>
    {
        internal ReadRecoveryScopeRequest(int target, SagaTimerRecoveryDomain domain, SqlTransaction transaction, CancellationToken token)
        {
            Parameters.Add("Target", target, DbType.Int32);
            Parameters.Add("Tenant", domain.TenantId, DbType.String);
            Parameters.Add("TenantKey", RecoveryKey(domain.TenantId), DbType.Binary);
            Parameters.Add("SagaId", domain.SagaId, DbType.Guid);
            const string sql = """
                SELECT ScopeId,TargetObjectId,TenantId,SagaId,FenceToken,IncarnationId,BatchSequence,
                  MutationRevision,StateType,BatchId,ExpectedFence,ObservedRevision,IntentDigest,CancelAllThrough,CancelAllRevision
                FROM dispatch.SagaTimerRecoveryScopes WITH(UPDLOCK,HOLDLOCK)
                WHERE TargetObjectId=@Target AND TenantKey=@TenantKey AND SagaId=@SagaId;
                """;
            Command = CreateCommand(sql, transaction: transaction, cancellationToken: token);
            ResolveAsync = connection => ReadRowsAsync(connection, command =>
                {
                    AddRecoveryParameter(command, "Target", DbType.Int32, Parameters.Get<int>("Target"));
                    AddRecoveryParameter(command, "Tenant", DbType.String, Parameters.Get<string>("Tenant"));
                    AddRecoveryParameter(command, "TenantKey", DbType.Binary, Parameters.Get<byte[]>("TenantKey"));
                    AddRecoveryParameter(command, "SagaId", DbType.Guid, Parameters.Get<Guid>("SagaId"));
                }, reader => new RecoveryScopeRow(reader.GetGuid(0), reader.GetInt32(1), reader.GetString(2), reader.GetGuid(3),
                    reader.GetGuid(4), reader.GetGuid(5), reader.GetInt64(6), reader.GetInt64(7),
                    reader.GetValue(8) is DBNull ? null : reader.GetString(8),
                    reader.GetValue(9) is DBNull ? null : reader.GetGuid(9), reader.GetValue(10) is DBNull ? null : reader.GetGuid(10),
                    reader.GetValue(11) is DBNull ? null : reader.GetInt64(11), reader.GetValue(12) is DBNull ? [] : (byte[])reader.GetValue(12),
                    reader.GetInt64(13), reader.GetInt64(14)));
        }
    }

    internal sealed class CreateRecoveryScopeRequest : DataRequestBase<IDbConnection, int>
    {
        internal CreateRecoveryScopeRequest(RecoveryScopeRow scope, SqlTransaction transaction, CancellationToken token)
        {
            Parameters.Add("Id", scope.Id, DbType.Guid);
            Parameters.Add("Target", scope.Target, DbType.Int32);
            Parameters.Add("Tenant", scope.Tenant, DbType.String);
            Parameters.Add("TenantKey", RecoveryKey(scope.Tenant), DbType.Binary);
            Parameters.Add("SagaId", scope.SagaId, DbType.Guid);
            Parameters.Add("Token", scope.Token, DbType.Guid);
            const string sql = """
                INSERT dispatch.SagaTimerRecoveryScopes
                  (ScopeId,TargetObjectId,TenantKey,TenantId,SagaId,FenceToken,IncarnationId,BatchSequence,MutationRevision,CancelAllThrough,CancelAllRevision)
                VALUES(@Id,@Target,@TenantKey,@Tenant,@SagaId,@Token,'00000000-0000-0000-0000-000000000000',0,0,-1,0);
                """;
            Command = CreateCommand(sql, transaction: transaction, cancellationToken: token);
            ResolveAsync = connection => ExecuteNonQueryAsync(connection, command =>
                {
                    AddRecoveryParameter(command, "Id", DbType.Guid, Parameters.Get<Guid>("Id"));
                    AddRecoveryParameter(command, "Target", DbType.Int32, Parameters.Get<int>("Target"));
                    AddRecoveryParameter(command, "Tenant", DbType.String, Parameters.Get<string>("Tenant"));
                    AddRecoveryParameter(command, "TenantKey", DbType.Binary, Parameters.Get<byte[]>("TenantKey"));
                    AddRecoveryParameter(command, "SagaId", DbType.Guid, Parameters.Get<Guid>("SagaId"));
                    AddRecoveryParameter(command, "Token", DbType.Guid, Parameters.Get<Guid>("Token"));
                });
        }
    }

    internal sealed class ReadRetiredRecoveryTokenRequest : DataRequestBase<IDbConnection, IEnumerable<int>>
    {
        internal ReadRetiredRecoveryTokenRequest(Guid id, Guid fence, SqlTransaction transaction, CancellationToken token)
        {
            Parameters.Add("Id", id, DbType.Guid);
            Parameters.Add("Token", fence, DbType.Guid);
            const string sql = """
                SELECT CAST(1 AS INT) FROM dispatch.SagaTimerRecoveryTokens WHERE ScopeId=@Id AND FenceToken=@Token;
                """;
            Command = CreateCommand(sql, transaction: transaction, cancellationToken: token);
            ResolveAsync = connection => ReadRowsAsync(connection, command =>
                {
                    AddRecoveryParameter(command, "Id", DbType.Guid, Parameters.Get<Guid>("Id"));
                    AddRecoveryParameter(command, "Token", DbType.Guid, Parameters.Get<Guid>("Token"));
                }, reader => reader.GetInt32(0));
        }
    }

    internal sealed class RetireRecoveryTokenRequest : DataRequestBase<IDbConnection, int>
    {
        internal RetireRecoveryTokenRequest(Guid id, Guid fence, SqlTransaction transaction, CancellationToken token)
        {
            Parameters.Add("Id", id, DbType.Guid);
            Parameters.Add("Token", fence, DbType.Guid);
            const string sql = """
                INSERT dispatch.SagaTimerRecoveryTokens(ScopeId,FenceToken) VALUES(@Id,@Token);
                """;
            Command = CreateCommand(sql, transaction: transaction, cancellationToken: token);
            ResolveAsync = connection => ExecuteNonQueryAsync(connection, command =>
                {
                    AddRecoveryParameter(command, "Id", DbType.Guid, Parameters.Get<Guid>("Id"));
                    AddRecoveryParameter(command, "Token", DbType.Guid, Parameters.Get<Guid>("Token"));
                });
        }
    }

    internal sealed class ReadRecoveryTimerRequest : DataRequestBase<IDbConnection, IEnumerable<RecoveryTimerRow>>
    {
        internal ReadRecoveryTimerRequest(Guid id, string timeoutId, SqlTransaction transaction, CancellationToken token)
        {
            Parameters.Add("Id", id, DbType.Guid);
            Parameters.Add("TimeoutKey", RecoveryKey(timeoutId), DbType.Binary);
            Parameters.Add("TimeoutId", timeoutId, DbType.String);
            const string sql = """
                IF EXISTS(SELECT 1 FROM dispatch.SagaTimerRecoveryTimers WITH(UPDLOCK,HOLDLOCK)
                  WHERE ScopeId=@Id AND TimeoutKey=@TimeoutKey
                    AND (DATALENGTH(TimeoutId)<>DATALENGTH(@TimeoutId)
                      OR CONVERT(VARBINARY(900),TimeoutId)<>CONVERT(VARBINARY(900),@TimeoutId)))
                  THROW 51004,'Exact timeout identity differs from its retained key.',1;
                SELECT IsTracked,IncarnationId,BatchSequence,StateType,CreatedRevision,IsCancelled,CancelThrough,CancelRevision
                FROM dispatch.SagaTimerRecoveryTimers WITH(UPDLOCK,HOLDLOCK) WHERE ScopeId=@Id AND TimeoutKey=@TimeoutKey;
                """;
            Command = CreateCommand(sql, transaction: transaction, cancellationToken: token);
            ResolveAsync = connection => ReadRowsAsync(connection, command =>
                {
                    AddRecoveryParameter(command, "Id", DbType.Guid, Parameters.Get<Guid>("Id"));
                    AddRecoveryParameter(command, "TimeoutKey", DbType.Binary, Parameters.Get<byte[]>("TimeoutKey"));
                    AddRecoveryParameter(command, "TimeoutId", DbType.String, Parameters.Get<string>("TimeoutId"));
                }, reader => new RecoveryTimerRow(reader.GetBoolean(0),reader.GetValue(1) is DBNull ? null : reader.GetGuid(1),
                    reader.GetValue(2) is DBNull ? null : reader.GetInt64(2),reader.GetValue(3) is DBNull ? null : reader.GetString(3),
                    reader.GetInt64(4),reader.GetBoolean(5),reader.GetInt64(6),reader.GetInt64(7)));
        }
    }

    internal sealed class ReadRecoveryResultsRequest : DataRequestBase<IDbConnection, IEnumerable<SagaTimerRecoveryIntentResult>>
    {
        internal ReadRecoveryResultsRequest(Guid id, SqlTransaction transaction, CancellationToken token)
        {
            Parameters.Add("Id", id, DbType.Guid);
            const string sql = """
                SELECT Ordinal,Action,TimeoutId,Outcome,AuthorityRevision FROM dispatch.SagaTimerRecoveryResults
                WHERE ScopeId=@Id ORDER BY Ordinal;
                """;
            Command = CreateCommand(sql, transaction: transaction, cancellationToken: token);
            ResolveAsync = connection => ReadRowsAsync(connection, command =>
                {
                    AddRecoveryParameter(command, "Id", DbType.Guid, Parameters.Get<Guid>("Id"));
                }, reader => new SagaTimerRecoveryIntentResult(reader.GetInt32(0),(SagaPreparedTimerAction)reader.GetInt32(1),
                    reader.GetString(2),(SagaTimerRecoveryIntentOutcome)reader.GetInt32(3),reader.GetInt64(4)));
        }
    }

    internal sealed class WriteRecoveryReceiptRequest : DataRequestBase<IDbConnection, int>
    {
        internal WriteRecoveryReceiptRequest(Guid id, Guid fence, SagaTimerRecoveryJournal journal, byte[] digest, long revision, SqlTransaction transaction, CancellationToken token)
        {
            Parameters.Add("Id", id, DbType.Guid);
            Parameters.Add("Token", fence, DbType.Guid);
            Parameters.Add("Incarnation", journal.IncarnationId, DbType.Guid);
            Parameters.Add("Sequence", journal.Sequence, DbType.Int64);
            Parameters.Add("StateType", journal.Scope.StateType, DbType.String);
            Parameters.Add("BatchId", journal.BatchId, DbType.Guid);
            Parameters.Add("ExpectedFence", journal.ExpectedFence, DbType.Guid);
            Parameters.Add("Observed", journal.ObservedRevision, DbType.Int64);
            Parameters.Add("Digest", digest, DbType.Binary);
            Parameters.Add("Revision", revision, DbType.Int64);
            const string sql = """
                UPDATE dispatch.SagaTimerRecoveryScopes SET FenceToken=@Token,IncarnationId=@Incarnation,
                  BatchSequence=@Sequence,StateType=@StateType,BatchId=@BatchId,ExpectedFence=@ExpectedFence,
                  ObservedRevision=@Observed,IntentDigest=@Digest,MutationRevision=@Revision WHERE ScopeId=@Id;
                IF @@ROWCOUNT<>1 THROW 51004,'The locked receiver domain disappeared.',1;
                DELETE dispatch.SagaTimerRecoveryResults WHERE ScopeId=@Id;
                """;
            Command = CreateCommand(sql, transaction: transaction, cancellationToken: token);
            ResolveAsync = connection => ExecuteNonQueryAsync(connection, command =>
                {
                    AddRecoveryParameter(command, "Id", DbType.Guid, Parameters.Get<Guid>("Id"));
                    AddRecoveryParameter(command, "Token", DbType.Guid, Parameters.Get<Guid>("Token"));
                    AddRecoveryParameter(command, "Incarnation", DbType.Guid, Parameters.Get<Guid>("Incarnation"));
                    AddRecoveryParameter(command, "Sequence", DbType.Int64, Parameters.Get<long>("Sequence"));
                    AddRecoveryParameter(command, "StateType", DbType.String, Parameters.Get<string>("StateType"));
                    AddRecoveryParameter(command, "BatchId", DbType.Guid, Parameters.Get<Guid>("BatchId"));
                    AddRecoveryParameter(command, "ExpectedFence", DbType.Guid, Parameters.Get<Guid>("ExpectedFence"));
                    AddRecoveryParameter(command, "Observed", DbType.Int64, Parameters.Get<long>("Observed"));
                    AddRecoveryParameter(command, "Digest", DbType.Binary, Parameters.Get<byte[]>("Digest"));
                    AddRecoveryParameter(command, "Revision", DbType.Int64, Parameters.Get<long>("Revision"));
                });
        }
    }

    internal sealed class WriteRecoveryResultRequest : DataRequestBase<IDbConnection, int>
    {
        internal WriteRecoveryResultRequest(Guid id, SagaTimerRecoveryIntentResult result, SqlTransaction transaction, CancellationToken token)
        {
            Parameters.Add("Id", id, DbType.Guid);
            Parameters.Add("Ordinal", result.Ordinal, DbType.Int32);
            Parameters.Add("Action", (int)result.Action, DbType.Int32);
            Parameters.Add("TimeoutId", result.TimeoutId, DbType.String);
            Parameters.Add("Outcome", (int)result.Outcome, DbType.Int32);
            Parameters.Add("Revision", result.AuthorityRevision, DbType.Int64);
            const string sql = """
                INSERT dispatch.SagaTimerRecoveryResults(ScopeId,Ordinal,Action,TimeoutId,Outcome,AuthorityRevision)
                VALUES(@Id,@Ordinal,@Action,@TimeoutId,@Outcome,@Revision);
                """;
            Command = CreateCommand(sql, transaction: transaction, cancellationToken: token);
            ResolveAsync = connection => ExecuteNonQueryAsync(connection, command =>
                {
                    AddRecoveryParameter(command, "Id", DbType.Guid, Parameters.Get<Guid>("Id"));
                    AddRecoveryParameter(command, "Ordinal", DbType.Int32, Parameters.Get<int>("Ordinal"));
                    AddRecoveryParameter(command, "Action", DbType.Int32, Parameters.Get<int>("Action"));
                    AddRecoveryParameter(command, "TimeoutId", DbType.String, Parameters.Get<string>("TimeoutId"));
                    AddRecoveryParameter(command, "Outcome", DbType.Int32, Parameters.Get<int>("Outcome"));
                    AddRecoveryParameter(command, "Revision", DbType.Int64, Parameters.Get<long>("Revision"));
                });
        }
    }

    internal sealed class ApplyRecoveryIntentRequest : DataRequestBase<IDbConnection, IEnumerable<RecoveryIntentDisposition>>
    {
        internal ApplyRecoveryIntentRequest(string table, RecoveryScopeRow scope, SagaTimerRecoveryJournal journal, SagaTimerIntent intent, long revision, RecoveryCutoff cutoff, SqlTransaction transaction, CancellationToken token)
        {
            Parameters.Add("ScopeId", scope.Id, DbType.Guid);
            Parameters.Add("Target", scope.Target, DbType.Int32);
            Parameters.Add("Tenant", scope.Tenant, DbType.String);
            Parameters.Add("SagaId", scope.SagaId, DbType.Guid);
            Parameters.Add("SagaText", scope.SagaId.ToString(), DbType.String);
            Parameters.Add("TimeoutId", intent.TimeoutId, DbType.String);
            Parameters.Add("TimeoutKey", RecoveryKey(intent.TimeoutId), DbType.Binary);
            Parameters.Add("Action", (int)intent.Action, DbType.Int32);
            Parameters.Add("ScheduleAction", (int)SagaTimerAction.Schedule, DbType.Int32);
            Parameters.Add("CancelAction", (int)SagaTimerAction.Cancel, DbType.Int32);
            Parameters.Add("Applied", (int)SagaTimerRecoveryIntentOutcome.Applied, DbType.Int32);
            Parameters.Add("Cancelled", (int)SagaTimerRecoveryIntentOutcome.Cancelled, DbType.Int32);
            Parameters.Add("Protected", (int)SagaTimerRecoveryIntentOutcome.ProtectedNewerTimer, DbType.Int32);
            Parameters.Add("Incarnation", journal.IncarnationId, DbType.Guid);
            Parameters.Add("Sequence", journal.Sequence, DbType.Int64);
            Parameters.Add("StateType", journal.Scope.StateType, DbType.String);
            Parameters.Add("Observed", journal.ObservedRevision, DbType.Int64);
            Parameters.Add("Revision", revision, DbType.Int64);
            Parameters.Add("ExternalThrough", cutoff.Through, DbType.Int64);
            Parameters.Add("ExternalRevision", cutoff.Revision, DbType.Int64);
            Parameters.Add("SagaType", intent.Timeout?.SagaType, DbType.String);
            Parameters.Add("TimeoutType", intent.Timeout?.TimeoutType, DbType.String);
            Parameters.Add("Data", intent.Timeout?.TimeoutData, DbType.Binary);
            Parameters.Add("DueAt", intent.Timeout?.DueAt, DbType.DateTimeOffset);
            Parameters.Add("ScheduledAt", intent.Timeout?.ScheduledAt, DbType.DateTimeOffset);
            var sql = $"""
                IF OBJECT_ID(N'{table}',N'U')<>@Target THROW 51004,'The selected timer payload table changed during recovery.',1;
                DECLARE @Outcome INT=@Applied,@AuthorityRevision BIGINT=@Revision;
                IF EXISTS(SELECT 1 FROM dispatch.SagaTimerRecoveryTimers WITH(UPDLOCK,HOLDLOCK)
                  WHERE ScopeId=@ScopeId AND TimeoutKey=@TimeoutKey
                  AND (DATALENGTH(TimeoutId)<>DATALENGTH(@TimeoutId)
                    OR CONVERT(VARBINARY(900),TimeoutId)<>CONVERT(VARBINARY(900),@TimeoutId)))
                  THROW 51004,'Exact timeout identity differs from its retained key.',1;
                IF @Action=@ScheduleAction
                BEGIN
                  IF EXISTS(SELECT 1 FROM dispatch.SagaTimerRecoveryTimers WITH(UPDLOCK,HOLDLOCK)
                    WHERE TargetObjectId=@Target AND TimeoutKey=@TimeoutKey AND IsTracked=1)
                    OR EXISTS(SELECT 1 FROM {table} WITH(UPDLOCK,HOLDLOCK) WHERE TimeoutId=@TimeoutId)
                    THROW 51004,'A fresh timer intent cannot overwrite an existing or retired timer identity.',1;
                  IF @Target=OBJECT_ID(N'dbo.SagaTimeouts',N'U') AND OBJECT_ID(N'dispatch.SagaTimeoutMembership',N'U') IS NOT NULL
                  BEGIN
                    IF EXISTS(SELECT 1 FROM dispatch.SagaTimeoutMembership WITH(UPDLOCK,HOLDLOCK)
                      WHERE TimeoutId=@TimeoutId COLLATE Latin1_General_BIN2)
                      THROW 51004,'An original atomic timer identity cannot be reused by ordinary recovery.',1;
                  END;
                  IF @Observed<=@ExternalThrough BEGIN SET @Outcome=@Cancelled; SET @AuthorityRevision=@ExternalRevision; END;
                  UPDATE dispatch.SagaTimerRecoveryTimers SET IsTracked=1,IncarnationId=@Incarnation,
                    BatchSequence=@Sequence,StateType=@StateType,CreatedRevision=@Revision,IsCancelled=CASE WHEN @Outcome=@Cancelled THEN 1 ELSE 0 END
                    WHERE ScopeId=@ScopeId AND TimeoutKey=@TimeoutKey;
                  IF @@ROWCOUNT=0
                    INSERT dispatch.SagaTimerRecoveryTimers(ScopeId,TimeoutKey,TimeoutId,TargetObjectId,IsTracked,
                      IncarnationId,BatchSequence,StateType,CreatedRevision,IsCancelled,CancelThrough,CancelRevision)
                    VALUES(@ScopeId,@TimeoutKey,@TimeoutId,@Target,1,@Incarnation,@Sequence,@StateType,@Revision,
                      CASE WHEN @Outcome=@Cancelled THEN 1 ELSE 0 END,-1,0);
                  IF @Outcome=@Applied
                    INSERT {table}(TimeoutId,SagaId,SagaType,TimeoutType,TimeoutData,DueAt,ScheduledAt,TenantId,
                      OwnedIncarnationId,OwnedGeneration,OwnedStateType)
                    VALUES(@TimeoutId,@SagaText,@SagaType,@TimeoutType,@Data,@DueAt,@ScheduledAt,@Tenant,
                      @Incarnation,@Sequence,@StateType);
                END
                ELSE IF @Action=@CancelAction
                BEGIN
                  IF EXISTS(SELECT 1 FROM dispatch.SagaTimerRecoveryTimers WHERE ScopeId=@ScopeId AND TimeoutKey=@TimeoutKey
                    AND CreatedRevision>@Observed AND NOT(IsTracked=1 AND IncarnationId=@Incarnation AND BatchSequence=@Sequence))
                    SET @Outcome=@Protected;
                  IF @Outcome=@Applied
                  BEGIN
                    UPDATE dispatch.SagaTimerRecoveryTimers SET IsCancelled=1 WHERE ScopeId=@ScopeId AND TimeoutKey=@TimeoutKey;
                    DELETE FROM {table} WHERE TenantId=@Tenant
                      AND DATALENGTH(TenantId)=DATALENGTH(@Tenant)
                      AND CONVERT(VARBINARY(128),TenantId)=CONVERT(VARBINARY(128),@Tenant)
                      AND TRY_CONVERT(UNIQUEIDENTIFIER,SagaId)=@SagaId AND TimeoutId=@TimeoutId
                      AND DATALENGTH(TimeoutId)=DATALENGTH(@TimeoutId)
                      AND CONVERT(VARBINARY(900),TimeoutId)=CONVERT(VARBINARY(900),@TimeoutId);
                  END;
                  UPDATE dispatch.SagaTimerRecoveryTimers SET
                    CancelRevision=CASE WHEN CancelThrough>@Observed THEN CancelRevision ELSE @Revision END,
                    CancelThrough=CASE WHEN CancelThrough>@Observed THEN CancelThrough ELSE @Observed END
                    WHERE ScopeId=@ScopeId AND TimeoutKey=@TimeoutKey;
                  IF @@ROWCOUNT=0
                    INSERT dispatch.SagaTimerRecoveryTimers(ScopeId,TimeoutKey,TimeoutId,TargetObjectId,IsTracked,
                      CreatedRevision,IsCancelled,CancelThrough,CancelRevision)
                    VALUES(@ScopeId,@TimeoutKey,@TimeoutId,@Target,0,0,1,@Observed,@Revision);
                END
                ELSE
                BEGIN
                  IF EXISTS(SELECT 1 FROM dispatch.SagaTimerRecoveryTimers WHERE ScopeId=@ScopeId AND IsCancelled=0
                    AND CreatedRevision>@Observed AND NOT(IsTracked=1 AND IncarnationId=@Incarnation AND BatchSequence=@Sequence))
                    SET @Outcome=@Protected;
                  DELETE payload FROM {table} payload
                    LEFT JOIN dispatch.SagaTimerRecoveryTimers fact ON fact.ScopeId=@ScopeId
                      AND fact.TimeoutKey=HASHBYTES('SHA2_256',CONVERT(VARBINARY(900),payload.TimeoutId))
                      AND DATALENGTH(fact.TimeoutId)=DATALENGTH(payload.TimeoutId)
                      AND CONVERT(VARBINARY(900),fact.TimeoutId)=CONVERT(VARBINARY(900),payload.TimeoutId)
                    WHERE payload.TenantId=@Tenant AND DATALENGTH(payload.TenantId)=DATALENGTH(@Tenant)
                      AND CONVERT(VARBINARY(128),payload.TenantId)=CONVERT(VARBINARY(128),@Tenant)
                      AND TRY_CONVERT(UNIQUEIDENTIFIER,payload.SagaId)=@SagaId
                      AND (fact.ScopeId IS NULL OR fact.CreatedRevision<=@Observed
                        OR (fact.IsTracked=1 AND fact.IncarnationId=@Incarnation AND fact.BatchSequence=@Sequence));
                  UPDATE dispatch.SagaTimerRecoveryTimers SET IsCancelled=1 WHERE ScopeId=@ScopeId
                    AND (CreatedRevision<=@Observed OR (IsTracked=1 AND IncarnationId=@Incarnation AND BatchSequence=@Sequence));
                  UPDATE dispatch.SagaTimerRecoveryScopes SET
                    CancelAllRevision=CASE WHEN CancelAllThrough>@Observed THEN CancelAllRevision ELSE @Revision END,
                    CancelAllThrough=CASE WHEN CancelAllThrough>@Observed THEN CancelAllThrough ELSE @Observed END
                    WHERE ScopeId=@ScopeId;
                END;
                SELECT @Outcome,@AuthorityRevision;
                """;
            Command = CreateCommand(sql, transaction: transaction, cancellationToken: token);
            ResolveAsync = connection => ReadRowsAsync(connection, command =>
                {
                    AddRecoveryParameter(command, "ScopeId", DbType.Guid, Parameters.Get<Guid>("ScopeId"));
                    AddRecoveryParameter(command, "Target", DbType.Int32, Parameters.Get<int>("Target"));
                    AddRecoveryParameter(command, "Tenant", DbType.String, Parameters.Get<string>("Tenant"));
                    AddRecoveryParameter(command, "SagaId", DbType.Guid, Parameters.Get<Guid>("SagaId"));
                    AddRecoveryParameter(command, "SagaText", DbType.String, Parameters.Get<string>("SagaText"));
                    AddRecoveryParameter(command, "TimeoutId", DbType.String, Parameters.Get<string>("TimeoutId"));
                    AddRecoveryParameter(command, "TimeoutKey", DbType.Binary, Parameters.Get<byte[]>("TimeoutKey"));
                    AddRecoveryParameter(command, "Action", DbType.Int32, Parameters.Get<int>("Action"));
                    AddRecoveryParameter(command, "ScheduleAction", DbType.Int32, Parameters.Get<int>("ScheduleAction"));
                    AddRecoveryParameter(command, "CancelAction", DbType.Int32, Parameters.Get<int>("CancelAction"));
                    AddRecoveryParameter(command, "Applied", DbType.Int32, Parameters.Get<int>("Applied"));
                    AddRecoveryParameter(command, "Cancelled", DbType.Int32, Parameters.Get<int>("Cancelled"));
                    AddRecoveryParameter(command, "Protected", DbType.Int32, Parameters.Get<int>("Protected"));
                    AddRecoveryParameter(command, "Incarnation", DbType.Guid, Parameters.Get<Guid>("Incarnation"));
                    AddRecoveryParameter(command, "Sequence", DbType.Int64, Parameters.Get<long>("Sequence"));
                    AddRecoveryParameter(command, "StateType", DbType.String, Parameters.Get<string>("StateType"));
                    AddRecoveryParameter(command, "Observed", DbType.Int64, Parameters.Get<long>("Observed"));
                    AddRecoveryParameter(command, "Revision", DbType.Int64, Parameters.Get<long>("Revision"));
                    AddRecoveryParameter(command, "ExternalThrough", DbType.Int64, Parameters.Get<long>("ExternalThrough"));
                    AddRecoveryParameter(command, "ExternalRevision", DbType.Int64, Parameters.Get<long>("ExternalRevision"));
                    AddRecoveryParameter(command, "SagaType", DbType.String, Parameters.Get<string>("SagaType"));
                    AddRecoveryParameter(command, "TimeoutType", DbType.String, Parameters.Get<string>("TimeoutType"));
                    AddRecoveryParameter(command, "Data", DbType.Binary, Parameters.Get<byte[]>("Data"));
                    AddRecoveryParameter(command, "DueAt", DbType.DateTimeOffset, Parameters.Get<DateTimeOffset?>("DueAt"));
                    AddRecoveryParameter(command, "ScheduledAt", DbType.DateTimeOffset, Parameters.Get<DateTimeOffset?>("ScheduledAt"));
                }, reader => new RecoveryIntentDisposition((SagaTimerRecoveryIntentOutcome)reader.GetInt32(0),reader.GetInt64(1)));
        }
    }

    internal sealed class RecoveryMetadataAvailableRequest : DataRequestBase<IDbConnection, IEnumerable<bool>>
    {
        internal RecoveryMetadataAvailableRequest(string target, SqlTransaction? transaction, CancellationToken token)
        {
            Parameters.Add("Target", target, DbType.String);
            const string sql = """
                IF OBJECT_ID(@Target,N'U') IS NULL
                  THROW 51004,'Timeout mutation requires the selected direct payload table.',1;
                IF OBJECT_ID(N'dispatch.SagaTimerRecoveryTargets',N'U') IS NULL BEGIN SELECT CAST(0 AS BIT); RETURN; END;
                IF NOT EXISTS(SELECT 1 FROM dispatch.SagaTimerRecoveryTargets WHERE TargetObjectId=OBJECT_ID(@Target,N'U'))
                  BEGIN SELECT CAST(0 AS BIT); RETURN; END;
                DECLARE @Scopes INT=OBJECT_ID(N'dispatch.SagaTimerRecoveryScopes',N'U');
                DECLARE @Timers INT=OBJECT_ID(N'dispatch.SagaTimerRecoveryTimers',N'U');
                DECLARE @Tokens INT=OBJECT_ID(N'dispatch.SagaTimerRecoveryTokens',N'U');
                DECLARE @Results INT=OBJECT_ID(N'dispatch.SagaTimerRecoveryResults',N'U');
                IF @Scopes IS NULL AND @Timers IS NULL AND @Tokens IS NULL AND @Results IS NULL SELECT CAST(0 AS BIT);
                ELSE BEGIN
                  IF @Scopes IS NULL OR @Timers IS NULL OR @Tokens IS NULL OR @Results IS NULL
                    OR COL_LENGTH(@Target,N'OwnedIncarnationId') IS NULL OR COL_LENGTH(@Target,N'OwnedGeneration') IS NULL
                    OR COL_LENGTH(@Target,N'OwnedStateType') IS NULL
                    THROW 51004,'Complete the selected timer recovery schema before mutation.',1;
                  SELECT CAST(1 AS BIT);
                END;
                """;
            Command = CreateCommand(sql, transaction: transaction, cancellationToken: token);
            ResolveAsync = connection => ReadRowsAsync(connection, command =>
                {
                    AddRecoveryParameter(command, "Target", DbType.String, Parameters.Get<string>("Target"));
                }, reader => reader.GetBoolean(0));
        }
    }

    internal sealed class RecoveryAtomicMetadataRequest : DataRequestBase<IDbConnection, IEnumerable<bool>>
    {
        internal RecoveryAtomicMetadataRequest(string target, SqlTransaction transaction, CancellationToken token)
        {
            Parameters.Add("Target", target, DbType.String);
            const string sql = """
                IF OBJECT_ID(@Target,N'U')<>ISNULL(OBJECT_ID(N'dbo.SagaTimeouts',N'U'),-1) SELECT CAST(0 AS BIT);
                ELSE IF OBJECT_ID(N'dispatch.SagaDurableScopes',N'U') IS NULL
                  AND OBJECT_ID(N'dispatch.SagaTimeoutMembership',N'U') IS NULL SELECT CAST(0 AS BIT);
                ELSE BEGIN
                  IF OBJECT_ID(N'dispatch.SagaDurableScopes',N'U') IS NULL
                    OR COL_LENGTH(N'dispatch.SagaTimeoutMembership',N'IsCancelled') IS NULL
                    THROW 51004,'Complete durable timeout cancellation metadata before mutation.',1;
                  SELECT CAST(1 AS BIT);
                END;
                """;
            Command = CreateCommand(sql, transaction: transaction, cancellationToken: token);
            ResolveAsync = connection => ReadRowsAsync(connection, command =>
                {
                    AddRecoveryParameter(command, "Target", DbType.String, Parameters.Get<string>("Target"));
                }, reader => reader.GetBoolean(0));
        }
    }

    internal sealed class PublicRecoveryScheduleRequest : DataRequestBase<IDbConnection, int>
    {
        internal PublicRecoveryScheduleRequest(string table, RecoveryScopeRow? scope, SagaTimeout timeout, string tenant, SqlTransaction transaction, CancellationToken token)
        {
            Parameters.Add("ScopeId", scope?.Id, DbType.Guid);
            Parameters.Add("Target", scope?.Target, DbType.Int32);
            Parameters.Add("Revision", scope is null ? 0L : checked(scope.Revision + 1), DbType.Int64);
            Parameters.Add("Tenant", tenant, DbType.String);
            Parameters.Add("TimeoutId", timeout.TimeoutId, DbType.String);
            Parameters.Add("TimeoutKey", RecoveryKey(timeout.TimeoutId), DbType.Binary);
            Parameters.Add("SagaId", timeout.SagaId, DbType.String);
            Parameters.Add("SagaType", timeout.SagaType, DbType.String);
            Parameters.Add("TimeoutType", timeout.TimeoutType, DbType.String);
            Parameters.Add("Data", timeout.TimeoutData, DbType.Binary);
            Parameters.Add("DueAt", timeout.DueAt, DbType.DateTimeOffset);
            Parameters.Add("ScheduledAt", timeout.ScheduledAt, DbType.DateTimeOffset);
            var sql = $"""
                DECLARE @ActualTarget INT=OBJECT_ID(N'{table}',N'U');
                IF @Target IS NOT NULL AND @ActualTarget<>@Target THROW 51004,'The selected timer payload table changed during scheduling.',1;
                IF EXISTS(SELECT 1 FROM dispatch.SagaTimerRecoveryTimers WITH(UPDLOCK,HOLDLOCK)
                  WHERE ScopeId=@ScopeId AND TimeoutKey=@TimeoutKey
                    AND (DATALENGTH(TimeoutId)<>DATALENGTH(@TimeoutId)
                      OR CONVERT(VARBINARY(900),TimeoutId)<>CONVERT(VARBINARY(900),@TimeoutId)))
                    THROW 51004,'Exact timeout identity differs from its retained key.',1;
                IF EXISTS(SELECT 1 FROM dispatch.SagaTimerRecoveryTimers WITH(UPDLOCK,HOLDLOCK)
                  WHERE TargetObjectId=@ActualTarget AND TimeoutKey=@TimeoutKey AND (IsTracked=1
                    OR (ScopeId=@ScopeId AND CancelThrough>=0)))
                    THROW 51004,'Public Schedule cannot overwrite a recovery-tracked or cancelled timer ID.',1;
                INSERT {table}(TimeoutId,SagaId,SagaType,TimeoutType,TimeoutData,DueAt,ScheduledAt,TenantId,
                  OwnedIncarnationId,OwnedGeneration,OwnedStateType)
                  VALUES(@TimeoutId,@SagaId,@SagaType,@TimeoutType,@Data,@DueAt,@ScheduledAt,@Tenant,NULL,NULL,NULL);
                IF @ScopeId IS NOT NULL
                BEGIN
                  UPDATE dispatch.SagaTimerRecoveryTimers SET CreatedRevision=@Revision,IsCancelled=0
                    WHERE ScopeId=@ScopeId AND TimeoutKey=@TimeoutKey;
                  IF @@ROWCOUNT=0
                    INSERT dispatch.SagaTimerRecoveryTimers(ScopeId,TimeoutKey,TimeoutId,TargetObjectId,IsTracked,
                      CreatedRevision,IsCancelled,CancelThrough,CancelRevision)
                    VALUES(@ScopeId,@TimeoutKey,@TimeoutId,@Target,0,@Revision,0,-1,0);
                  UPDATE dispatch.SagaTimerRecoveryScopes SET MutationRevision=@Revision WHERE ScopeId=@ScopeId;
                END;
                """;
            Command = CreateCommand(sql, transaction: transaction, cancellationToken: token);
            ResolveAsync = connection => ExecuteNonQueryAsync(connection, command =>
                {
                    AddRecoveryParameter(command, "ScopeId", DbType.Guid, Parameters.Get<Guid?>("ScopeId"));
                    AddRecoveryParameter(command, "Target", DbType.Int32, Parameters.Get<int?>("Target"));
                    AddRecoveryParameter(command, "Revision", DbType.Int64, Parameters.Get<long>("Revision"));
                    AddRecoveryParameter(command, "Tenant", DbType.String, Parameters.Get<string>("Tenant"));
                    AddRecoveryParameter(command, "TimeoutId", DbType.String, Parameters.Get<string>("TimeoutId"));
                    AddRecoveryParameter(command, "TimeoutKey", DbType.Binary, Parameters.Get<byte[]>("TimeoutKey"));
                    AddRecoveryParameter(command, "SagaId", DbType.String, Parameters.Get<string>("SagaId"));
                    AddRecoveryParameter(command, "SagaType", DbType.String, Parameters.Get<string>("SagaType"));
                    AddRecoveryParameter(command, "TimeoutType", DbType.String, Parameters.Get<string>("TimeoutType"));
                    AddRecoveryParameter(command, "Data", DbType.Binary, Parameters.Get<byte[]>("Data"));
                    AddRecoveryParameter(command, "DueAt", DbType.DateTimeOffset, Parameters.Get<DateTimeOffset>("DueAt"));
                    AddRecoveryParameter(command, "ScheduledAt", DbType.DateTimeOffset, Parameters.Get<DateTimeOffset>("ScheduledAt"));
                });
        }
    }

    internal sealed class RecordPublicRecoveryCancellationRequest : DataRequestBase<IDbConnection, int>
    {
        internal RecordPublicRecoveryCancellationRequest(RecoveryScopeRow scope, string? timeoutId, SqlTransaction transaction, CancellationToken token)
        {
            Parameters.Add("ScopeId", scope.Id, DbType.Guid);
            Parameters.Add("Target", scope.Target, DbType.Int32);
            Parameters.Add("TimeoutId", timeoutId, DbType.String);
            Parameters.Add("TimeoutKey", timeoutId is null ? null : RecoveryKey(timeoutId), DbType.Binary);
            Parameters.Add("Observed", scope.Revision, DbType.Int64);
            Parameters.Add("Revision", checked(scope.Revision + 1), DbType.Int64);
            const string sql = """
                IF @TimeoutId IS NULL
                BEGIN
                  UPDATE dispatch.SagaTimerRecoveryTimers SET IsCancelled=1 WHERE ScopeId=@ScopeId;
                  UPDATE dispatch.SagaTimerRecoveryScopes SET MutationRevision=@Revision,
                    CancelAllThrough=@Observed,CancelAllRevision=@Revision WHERE ScopeId=@ScopeId;
                END
                ELSE
                BEGIN
                  IF EXISTS(SELECT 1 FROM dispatch.SagaTimerRecoveryTimers WHERE ScopeId=@ScopeId AND TimeoutKey=@TimeoutKey
                    AND (DATALENGTH(TimeoutId)<>DATALENGTH(@TimeoutId)
                      OR CONVERT(VARBINARY(900),TimeoutId)<>CONVERT(VARBINARY(900),@TimeoutId)))
                    THROW 51004,'Exact timeout identity differs from its retained key.',1;
                  UPDATE dispatch.SagaTimerRecoveryTimers SET IsCancelled=1,CancelThrough=@Observed,CancelRevision=@Revision
                    WHERE ScopeId=@ScopeId AND TimeoutKey=@TimeoutKey;
                  IF @@ROWCOUNT=0
                    INSERT dispatch.SagaTimerRecoveryTimers(ScopeId,TimeoutKey,TimeoutId,TargetObjectId,IsTracked,
                      CreatedRevision,IsCancelled,CancelThrough,CancelRevision)
                    VALUES(@ScopeId,@TimeoutKey,@TimeoutId,@Target,0,0,1,@Observed,@Revision);
                  UPDATE dispatch.SagaTimerRecoveryScopes SET MutationRevision=@Revision WHERE ScopeId=@ScopeId;
                END;
                """;
            Command = CreateCommand(sql, transaction: transaction, cancellationToken: token);
            ResolveAsync = connection => ExecuteNonQueryAsync(connection, command =>
                {
                    AddRecoveryParameter(command, "ScopeId", DbType.Guid, Parameters.Get<Guid>("ScopeId"));
                    AddRecoveryParameter(command, "Target", DbType.Int32, Parameters.Get<int>("Target"));
                    AddRecoveryParameter(command, "TimeoutId", DbType.String, Parameters.Get<string?>("TimeoutId"));
                    AddRecoveryParameter(command, "TimeoutKey", DbType.Binary, Parameters.Get<byte[]?>("TimeoutKey"));
                    AddRecoveryParameter(command, "Observed", DbType.Int64, Parameters.Get<long>("Observed"));
                    AddRecoveryParameter(command, "Revision", DbType.Int64, Parameters.Get<long>("Revision"));
                });
        }
    }

    internal sealed class RecordAtomicRecoveryTimerRequest : DataRequestBase<IDbConnection, int>
    {
        internal RecordAtomicRecoveryTimerRequest(RecoveryScopeRow scope, SagaTransitionIdentity identity, SagaPreparedTimerAction action, string? timeoutId, SqlTransaction transaction, CancellationToken token)
        {
            Parameters.Add("ScopeId", scope.Id, DbType.Guid);
            Parameters.Add("Target", scope.Target, DbType.Int32);
            Parameters.Add("Revision", checked(scope.Revision + 1), DbType.Int64);
            Parameters.Add("TenantKey", RecoveryKey(identity.TenantId), DbType.Binary);
            Parameters.Add("SagaId", identity.SagaId, DbType.Guid);
            Parameters.Add("Incarnation", identity.IncarnationId, DbType.Guid);
            Parameters.Add("Action", (int)action, DbType.Int32);
            Parameters.Add("ScheduleAction", (int)SagaPreparedTimerAction.Schedule, DbType.Int32);
            Parameters.Add("CancelAllAction", (int)SagaPreparedTimerAction.CancelAll, DbType.Int32);
            Parameters.Add("TimeoutId", timeoutId, DbType.String);
            Parameters.Add("TimeoutKey", timeoutId is null ? null : RecoveryKey(timeoutId), DbType.Binary);
            const string sql = """
                IF @Action=@ScheduleAction
                BEGIN
                  IF EXISTS(SELECT 1 FROM dispatch.SagaTimerRecoveryTimers WITH(UPDLOCK,HOLDLOCK)
                    WHERE TargetObjectId=@Target AND TimeoutKey=@TimeoutKey AND IsTracked=1)
                    THROW 51004,'An atomic timer cannot reuse a retained ordinary timer identity.',1;
                  IF EXISTS(SELECT 1 FROM dispatch.SagaTimerRecoveryTimers WHERE ScopeId=@ScopeId AND TimeoutKey=@TimeoutKey
                    AND (DATALENGTH(TimeoutId)<>DATALENGTH(@TimeoutId)
                      OR CONVERT(VARBINARY(900),TimeoutId)<>CONVERT(VARBINARY(900),@TimeoutId)))
                    THROW 51004,'Exact timer identity differs from its retained key.',1;
                  UPDATE dispatch.SagaTimerRecoveryTimers SET CreatedRevision=@Revision,IsCancelled=0
                    WHERE ScopeId=@ScopeId AND TimeoutKey=@TimeoutKey;
                  IF @@ROWCOUNT=0
                    INSERT dispatch.SagaTimerRecoveryTimers(ScopeId,TimeoutKey,TimeoutId,TargetObjectId,IsTracked,
                      CreatedRevision,IsCancelled,CancelThrough,CancelRevision)
                    VALUES(@ScopeId,@TimeoutKey,@TimeoutId,@Target,0,@Revision,0,-1,0);
                END
                ELSE
                BEGIN
                  UPDATE fact SET IsCancelled=1
                    FROM dispatch.SagaTimerRecoveryTimers fact
                    JOIN dispatch.SagaTimeoutMembership membership
                      ON DATALENGTH(fact.TimeoutId)=DATALENGTH(membership.TimeoutId)
                      AND CONVERT(VARBINARY(900),fact.TimeoutId)=CONVERT(VARBINARY(900),membership.TimeoutId)
                    JOIN dispatch.SagaDurableScopes durable ON durable.ScopeId=membership.ScopeId
                    WHERE fact.ScopeId=@ScopeId AND durable.TenantKey=@TenantKey AND durable.SagaId=@SagaId
                      AND membership.IncarnationId=@Incarnation AND (@Action=@CancelAllAction OR
                        (membership.TimeoutId=@TimeoutId COLLATE Latin1_General_BIN2
                         AND DATALENGTH(membership.TimeoutId)=DATALENGTH(@TimeoutId)
                         AND CONVERT(VARBINARY(900),membership.TimeoutId)=CONVERT(VARBINARY(900),@TimeoutId)));
                END;
                UPDATE dispatch.SagaTimerRecoveryScopes SET MutationRevision=@Revision WHERE ScopeId=@ScopeId;
                """;
            Command = CreateCommand(sql, transaction: transaction, cancellationToken: token);
            ResolveAsync = connection => ExecuteNonQueryAsync(connection, command =>
                {
                    AddRecoveryParameter(command, "ScopeId", DbType.Guid, Parameters.Get<Guid>("ScopeId"));
                    AddRecoveryParameter(command, "Target", DbType.Int32, Parameters.Get<int>("Target"));
                    AddRecoveryParameter(command, "Revision", DbType.Int64, Parameters.Get<long>("Revision"));
                    AddRecoveryParameter(command, "TenantKey", DbType.Binary, Parameters.Get<byte[]?>("TenantKey"));
                    AddRecoveryParameter(command, "SagaId", DbType.Guid, Parameters.Get<Guid>("SagaId"));
                    AddRecoveryParameter(command, "Incarnation", DbType.Guid, Parameters.Get<Guid>("Incarnation"));
                    AddRecoveryParameter(command, "Action", DbType.Int32, Parameters.Get<int>("Action"));
                    AddRecoveryParameter(command, "ScheduleAction", DbType.Int32, Parameters.Get<int>("ScheduleAction"));
                    AddRecoveryParameter(command, "CancelAllAction", DbType.Int32, Parameters.Get<int>("CancelAllAction"));
                    AddRecoveryParameter(command, "TimeoutId", DbType.String, Parameters.Get<string?>("TimeoutId"));
                    AddRecoveryParameter(command, "TimeoutKey", DbType.Binary, Parameters.Get<byte[]?>("TimeoutKey"));
                });
        }
    }

}
