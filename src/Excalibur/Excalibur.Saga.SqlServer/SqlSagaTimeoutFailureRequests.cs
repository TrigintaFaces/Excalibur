// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;
using System.Data.Common;
using Excalibur.Data;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;
using Excalibur.Saga.Abstractions;
using Microsoft.Data.SqlClient;
using static Excalibur.Saga.SqlServer.SqlSagaTimeoutFailureState;

namespace Excalibur.Saga.SqlServer;

internal static class SqlSagaTimeoutFailureRequests
{
    private static readonly string[] FailureProjectionColumns =
    [
        "FailureSourceEpoch","FailureDeliveryEpoch","FailureHeld","FailureVersion","FailureToken",
        "FailureOperation","FailureOriginalClaim","FailureCurrentClaim","FailureObservedDeliveryEpoch","FailureKind","FailureReason","FailureResumeCommand",
    ];

    private static void Add(DbCommand command, string name, DbType type, object? value)
    {
        var parameter = new SqlParameter { ParameterName = name, DbType = type, Value = value ?? DBNull.Value };
        if (value is string text) { parameter.Size = text.Length <= 4000 ? 4000 : 0; }
        _ = command.Parameters.Add(parameter);
    }

    private static string Projection(bool owned) => "TimeoutId,SagaId,SagaType,TimeoutType,TimeoutData,DueAt,ScheduledAt,TenantId,ClaimedBy,"
        + (owned ? "OwnedIncarnationId,OwnedGeneration,OwnedStateType," : "CAST(NULL AS UNIQUEIDENTIFIER),CAST(NULL AS BIGINT),CAST(NULL AS NVARCHAR(500)),")
        + "FailureSourceEpoch,FailureDeliveryEpoch,FailureHeld,FailureVersion,FailureToken,FailureOperation,"
        + "FailureOriginalClaim,FailureCurrentClaim,FailureObservedDeliveryEpoch,FailureKind,FailureReason,FailureResumeCommand";

    private static Row Map(DbDataReader reader)
    {
        var timeout = new SagaTimeout(reader.GetString(0),reader.GetString(1),reader.GetString(2),reader.GetString(3),
            reader.GetValue(4) is DBNull ? null : (byte[])reader.GetValue(4),reader.GetFieldValue<DateTimeOffset>(5),reader.GetFieldValue<DateTimeOffset>(6))
            { TenantId = KeyedTenantPartition.FromStoredValue(reader.GetValue(7) is DBNull ? null : reader.GetString(7)).TenantId };
        var claim = reader.GetValue(8) is DBNull ? null : reader.GetString(8);
        SagaTimeoutDeliveryIdentity? owned = null;
        if (reader.GetValue(9) is not DBNull || reader.GetValue(10) is not DBNull || reader.GetValue(11) is not DBNull)
        {
            if (reader.GetValue(9) is DBNull || reader.GetValue(10) is DBNull || reader.GetValue(11) is DBNull
                || !Guid.TryParse(timeout.SagaId,out var saga) || saga == Guid.Empty)
            { throw new InvalidOperationException("Claimed timeout has partial or invalid original authority."); }
            owned = new(new(timeout.TenantId!,reader.GetString(11),saga),timeout.TimeoutId,reader.GetGuid(9),reader.GetInt64(10));
        }
        var source = reader.GetGuid(12); var delivery = reader.GetGuid(13);
        if (source == Guid.Empty || delivery == Guid.Empty) { throw new InvalidOperationException("Persistent timeout epochs must be nonempty."); }
        return new(timeout,claim,owned,source,delivery,reader.GetBoolean(14),reader.GetInt64(15),
            reader.GetValue(16) is DBNull ? null : reader.GetGuid(16),reader.GetValue(17) is DBNull ? null : reader.GetGuid(17),
            reader.GetValue(18) is DBNull ? null : reader.GetString(18),reader.GetValue(19) is DBNull ? null : reader.GetString(19),
            reader.GetValue(20) is DBNull ? null : reader.GetGuid(20),reader.GetValue(21) is DBNull ? null : (MessageFailureKind)reader.GetInt32(21),
            reader.GetValue(22) is DBNull ? null : reader.GetString(22),reader.GetValue(23) is DBNull ? null : reader.GetGuid(23));
    }

    internal sealed class FailureMetadataRequest : DataRequestBase<IDbConnection, IEnumerable<Metadata>>
    {
        internal FailureMetadataRequest(string table, SqlTransaction? transaction, CancellationToken token)
        {
            Parameters.Add("Target", table, DbType.String);
            const string sql = """
                DECLARE @Object INT=OBJECT_ID(@Target,N'U');
                IF @Object IS NULL THROW 51004,'Timeout failure storage requires the selected direct physical table, not a view or synonym.',1;
                DECLARE @Count INT=(SELECT COUNT(*) FROM sys.columns WHERE object_id=@Object AND name IN
                 (N'FailureSourceEpoch',N'FailureDeliveryEpoch',N'FailureHeld',N'FailureVersion',N'FailureToken',N'FailureOperation',
                 N'FailureOriginalClaim',N'FailureCurrentClaim',N'FailureObservedDeliveryEpoch',N'FailureKind',N'FailureReason',N'FailureResumeCommand'));
                IF @Count<>0 AND @Count<>12 THROW 51004,'Complete the timeout failure-hold schema on the selected table.',1;
                IF @Count=12 AND EXISTS(
                 SELECT 1 FROM (VALUES
                 (N'FailureSourceEpoch',36,16,0),(N'FailureDeliveryEpoch',36,16,0),(N'FailureHeld',104,1,0),(N'FailureVersion',127,8,0),
                 (N'FailureToken',36,16,1),(N'FailureOperation',36,16,1),(N'FailureOriginalClaim',231,400,1),(N'FailureCurrentClaim',231,400,1),
                 (N'FailureObservedDeliveryEpoch',36,16,1),(N'FailureKind',56,4,1),(N'FailureReason',231,2048,1),(N'FailureResumeCommand',36,16,1))
                 expected(Name,TypeId,Length,Nullable)
                 LEFT JOIN sys.columns c ON c.object_id=@Object AND c.name=expected.Name
                 WHERE c.system_type_id<>expected.TypeId OR c.max_length<>expected.Length OR c.is_nullable<>expected.Nullable)
                 THROW 51004,'Timeout failure-hold columns have incompatible types.',1;
                IF @Count=12 AND (SELECT COUNT(*) FROM sys.columns c
                 JOIN sys.default_constraints d ON d.object_id=c.default_object_id
                 WHERE c.object_id=@Object AND
                 ((c.name IN(N'FailureSourceEpoch',N'FailureDeliveryEpoch') AND UPPER(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(d.definition,N'(',N''),N')',N''),N' ',N''),NCHAR(9),N''),NCHAR(10),N''),NCHAR(13),N''))=N'NEWID')
                  OR (c.name IN(N'FailureHeld',N'FailureVersion') AND UPPER(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(d.definition,N'(',N''),N')',N''),N' ',N''),NCHAR(9),N''),NCHAR(10),N''),NCHAR(13),N''))=N'0')))<>4
                 THROW 51004,'Timeout failure-hold default definitions must use NEWID epochs and zero state.',1;
                IF @Count=12 AND EXISTS(
                 SELECT 1 FROM (VALUES (N'TimeoutId',231,900,0),(N'SagaId',231,900,0),(N'SagaType',231,1024,0),
                 (N'TimeoutType',231,1024,0),(N'TimeoutData',165,-1,1),(N'DueAt',43,10,0),(N'ScheduledAt',43,10,0),
                 (N'ClaimedAt',43,10,1),(N'ClaimedBy',231,400,1),(N'TenantId',231,128,0)) expected(Name,TypeId,Length,Nullable)
                 LEFT JOIN sys.columns c ON c.object_id=@Object AND c.name=expected.Name
                 WHERE c.column_id IS NULL OR c.system_type_id<>expected.TypeId OR c.max_length<>expected.Length OR c.is_nullable<>expected.Nullable)
                 THROW 51004,'Timeout failure holds require the shipped payload and exact-identity column shape.',1;
                DECLARE @Owned INT=(SELECT COUNT(*) FROM sys.columns WHERE object_id=@Object AND name IN(N'OwnedIncarnationId',N'OwnedGeneration',N'OwnedStateType'));
                IF @Owned<>0 AND @Owned<>3 THROW 51004,'Timeout owned authority columns are incomplete.',1;
                IF @Count=12 AND @Owned=3 AND EXISTS(
                 SELECT 1 FROM (VALUES (N'OwnedIncarnationId',36,16),(N'OwnedGeneration',127,8),(N'OwnedStateType',231,1000)) expected(Name,TypeId,Length)
                 JOIN sys.columns c ON c.object_id=@Object AND c.name=expected.Name
                 WHERE c.system_type_id<>expected.TypeId OR c.max_length<>expected.Length OR c.is_nullable<>1)
                 THROW 51004,'Timeout original authority columns have incompatible types.',1;
                DECLARE @Epoch UNIQUEIDENTIFIER=(SELECT TRY_CONVERT(UNIQUEIDENTIFIER,value) FROM sys.extended_properties
                 WHERE class=1 AND major_id=@Object AND minor_id=0 AND name=N'Excalibur.TimeoutFailureTarget');
                IF @Count=12 AND (@Epoch IS NULL OR @Epoch='00000000-0000-0000-0000-000000000000')
                 THROW 51004,'Register the physical timeout table incarnation using its shipped failure-hold migration.',1;
                SELECT @Object,CAST(CASE WHEN @Count=12 THEN 1 ELSE 0 END AS BIT),CAST(CASE WHEN @Owned=3 THEN 1 ELSE 0 END AS BIT),ISNULL(@Epoch,CAST('00000000-0000-0000-0000-000000000000' AS UNIQUEIDENTIFIER));
                """;
            Command = CreateCommand(sql, transaction: transaction, cancellationToken: token);
            ResolveAsync = connection => ReadRowsAsync(connection, command =>
                {
                    Add(command, "Target", DbType.String, Parameters.Get<string>("Target"));
                }, reader => new Metadata(reader.GetInt32(0),reader.GetBoolean(1),reader.GetBoolean(2),reader.GetGuid(3)));
        }
    }

    internal sealed class ReadFailureRowRequest : DataRequestBase<IDbConnection, IEnumerable<Row>>
    {
        internal ReadFailureRowRequest(string table, bool owned, string tenant, string saga, string id, SqlTransaction transaction, CancellationToken token)
        {
            Parameters.Add("Tenant", tenant, DbType.String);
            Parameters.Add("Saga", saga, DbType.String);
            Parameters.Add("Id", id, DbType.String);
            var sql = $"""
                SELECT {Projection(owned)} FROM {table} WITH(UPDLOCK,HOLDLOCK) WHERE TenantId=@Tenant AND DATALENGTH(TenantId)=DATALENGTH(@Tenant) AND CONVERT(VARBINARY(128),TenantId)=CONVERT(VARBINARY(128),@Tenant)
                 AND SagaId=@Saga AND DATALENGTH(SagaId)=DATALENGTH(@Saga) AND CONVERT(VARBINARY(900),SagaId)=CONVERT(VARBINARY(900),@Saga)
                 AND TimeoutId=@Id AND DATALENGTH(TimeoutId)=DATALENGTH(@Id) AND CONVERT(VARBINARY(900),TimeoutId)=CONVERT(VARBINARY(900),@Id);
                """;
            Command = CreateCommand(sql, transaction: transaction, cancellationToken: token);
            ResolveAsync = connection => ReadRowsAsync(connection, command =>
                {
                    Add(command, "Tenant", DbType.String, Parameters.Get<string>("Tenant"));
                    Add(command, "Saga", DbType.String, Parameters.Get<string>("Saga"));
                    Add(command, "Id", DbType.String, Parameters.Get<string>("Id"));
                }, Map);
        }
    }

    internal sealed class WriteFailureHoldRequest : DataRequestBase<IDbConnection, int>
    {
        internal WriteFailureHoldRequest(string table, Row row, SqlTransaction transaction, CancellationToken token)
        {
            Parameters.Add("Tenant", row.Timeout.TenantId!, DbType.String);
            Parameters.Add("Saga", row.Timeout.SagaId, DbType.String);
            Parameters.Add("Id", row.Timeout.TimeoutId, DbType.String);
            Parameters.Add("Version", row.Version, DbType.Int64);
            Parameters.Add("Token", row.Token!.Value, DbType.Guid);
            Parameters.Add("Operation", row.Operation!.Value, DbType.Guid);
            Parameters.Add("OriginalClaim", row.OriginalClaim!, DbType.String);
            Parameters.Add("CurrentClaim", row.HoldClaim!, DbType.String);
            Parameters.Add("Cycle", row.ObservedDelivery!.Value, DbType.Guid);
            Parameters.Add("Kind", (int)row.Kind!.Value, DbType.Int32);
            Parameters.Add("Reason", row.Reason!, DbType.String);
            var sql = $"""
                UPDATE {table} SET FailureHeld=1,FailureVersion=@Version,FailureToken=@Token,FailureOperation=@Operation,
                 FailureOriginalClaim=@OriginalClaim,FailureCurrentClaim=@CurrentClaim,FailureObservedDeliveryEpoch=@Cycle,
                 FailureKind=@Kind,FailureReason=@Reason,FailureResumeCommand=NULL,ClaimedAt=NULL,ClaimedBy=NULL
                WHERE TenantId=@Tenant AND DATALENGTH(TenantId)=DATALENGTH(@Tenant) AND CONVERT(VARBINARY(128),TenantId)=CONVERT(VARBINARY(128),@Tenant)
                 AND SagaId=@Saga AND DATALENGTH(SagaId)=DATALENGTH(@Saga) AND CONVERT(VARBINARY(900),SagaId)=CONVERT(VARBINARY(900),@Saga)
                 AND TimeoutId=@Id AND DATALENGTH(TimeoutId)=DATALENGTH(@Id) AND CONVERT(VARBINARY(900),TimeoutId)=CONVERT(VARBINARY(900),@Id);
                IF @@ROWCOUNT<>1 THROW 51004,'The locked timeout source disappeared before its hold transition.',1;
                """;
            Command = CreateCommand(sql, transaction: transaction, cancellationToken: token);
            ResolveAsync = connection => ExecuteNonQueryAsync(connection, command =>
                {
                    Add(command, "Tenant", DbType.String, Parameters.Get<string>("Tenant"));
                    Add(command, "Saga", DbType.String, Parameters.Get<string>("Saga"));
                    Add(command, "Id", DbType.String, Parameters.Get<string>("Id"));
                    Add(command, "Version", DbType.Int64, Parameters.Get<long>("Version"));
                    Add(command, "Token", DbType.Guid, Parameters.Get<Guid>("Token"));
                    Add(command, "Operation", DbType.Guid, Parameters.Get<Guid>("Operation"));
                    Add(command, "OriginalClaim", DbType.String, Parameters.Get<string>("OriginalClaim"));
                    Add(command, "CurrentClaim", DbType.String, Parameters.Get<string>("CurrentClaim"));
                    Add(command, "Cycle", DbType.Guid, Parameters.Get<Guid>("Cycle"));
                    Add(command, "Kind", DbType.Int32, Parameters.Get<int>("Kind"));
                    Add(command, "Reason", DbType.String, Parameters.Get<string>("Reason"));
                });
        }
    }

    internal sealed class ResumeFailureRowRequest : DataRequestBase<IDbConnection, int>
    {
        internal ResumeFailureRowRequest(string table, Row row, SqlTransaction transaction, CancellationToken token)
        {
            Parameters.Add("Tenant", row.Timeout.TenantId!, DbType.String);
            Parameters.Add("Saga", row.Timeout.SagaId, DbType.String);
            Parameters.Add("Id", row.Timeout.TimeoutId, DbType.String);
            Parameters.Add("Delivery", row.Delivery, DbType.Guid);
            Parameters.Add("Command", row.ResumeCommand!.Value, DbType.Guid);
            var sql = $"""
                UPDATE {table} SET FailureHeld=0,FailureDeliveryEpoch=@Delivery,FailureResumeCommand=@Command,ClaimedAt=NULL,ClaimedBy=NULL
                WHERE TenantId=@Tenant AND DATALENGTH(TenantId)=DATALENGTH(@Tenant) AND CONVERT(VARBINARY(128),TenantId)=CONVERT(VARBINARY(128),@Tenant)
                 AND SagaId=@Saga AND DATALENGTH(SagaId)=DATALENGTH(@Saga) AND CONVERT(VARBINARY(900),SagaId)=CONVERT(VARBINARY(900),@Saga)
                 AND TimeoutId=@Id AND DATALENGTH(TimeoutId)=DATALENGTH(@Id) AND CONVERT(VARBINARY(900),TimeoutId)=CONVERT(VARBINARY(900),@Id);
                IF @@ROWCOUNT<>1 THROW 51004,'The locked held timeout disappeared before resume.',1;
                """;
            Command = CreateCommand(sql, transaction: transaction, cancellationToken: token);
            ResolveAsync = connection => ExecuteNonQueryAsync(connection, command =>
                {
                    Add(command, "Tenant", DbType.String, Parameters.Get<string>("Tenant"));
                    Add(command, "Saga", DbType.String, Parameters.Get<string>("Saga"));
                    Add(command, "Id", DbType.String, Parameters.Get<string>("Id"));
                    Add(command, "Delivery", DbType.Guid, Parameters.Get<Guid>("Delivery"));
                    Add(command, "Command", DbType.Guid, Parameters.Get<Guid>("Command"));
                });
        }
    }

    internal sealed class QueryHeldFailureRowsRequest : DataRequestBase<IDbConnection, IEnumerable<Row>>
    {
        internal QueryHeldFailureRowsRequest(string table, bool owned, string tenant, string? saga, string? after, int count, SqlTransaction transaction, CancellationToken token)
        {
            Parameters.Add("Count", count, DbType.Int32);
            Parameters.Add("Tenant", tenant, DbType.String);
            Parameters.Add("Saga", saga, DbType.String);
            Parameters.Add("After", after, DbType.String);
            var sql = $"""
                SELECT TOP(@Count) {Projection(owned)} FROM {table}
                WHERE FailureHeld=1 AND TenantId=@Tenant AND DATALENGTH(TenantId)=DATALENGTH(@Tenant)
                 AND CONVERT(VARBINARY(128),TenantId)=CONVERT(VARBINARY(128),@Tenant)
                 AND (@Saga IS NULL OR (SagaId=@Saga AND DATALENGTH(SagaId)=DATALENGTH(@Saga)
                 AND CONVERT(VARBINARY(900),SagaId)=CONVERT(VARBINARY(900),@Saga)))
                 AND (@After IS NULL OR CONVERT(VARBINARY(900),TimeoutId)>CONVERT(VARBINARY(900),@After))
                ORDER BY CONVERT(VARBINARY(900),TimeoutId);
                """;
            Command = CreateCommand(sql, transaction: transaction, cancellationToken: token);
            ResolveAsync = connection => ReadRowsAsync(connection, command =>
                {
                    Add(command, "Count", DbType.Int32, Parameters.Get<int>("Count"));
                    Add(command, "Tenant", DbType.String, Parameters.Get<string>("Tenant"));
                    Add(command, "Saga", DbType.String, Parameters.Get<string?>("Saga"));
                    Add(command, "After", DbType.String, Parameters.Get<string?>("After"));
                }, Map);
        }
    }

    internal sealed class ClaimFailureTimeoutRowsRequest : DataRequestBase<IDbConnection, IEnumerable<Row>>
    {
        internal ClaimFailureTimeoutRowsRequest(string table, bool owned, DateTimeOffset asOf, int count, int leaseSeconds, string claim, CancellationToken token)
        {
            Parameters.Add("Count", count, DbType.Int32);
            Parameters.Add("AsOf", asOf, DbType.DateTimeOffset);
            Parameters.Add("Lease", leaseSeconds, DbType.Int32);
            Parameters.Add("Claim", claim, DbType.String);
            var sql = $"""
                WITH Claimable AS (
                 SELECT TOP(@Count) * FROM {table} WITH(READPAST,UPDLOCK,ROWLOCK)
                 WHERE FailureHeld=0 AND DueAt<=@AsOf AND (ClaimedAt IS NULL OR ClaimedAt<DATEADD(SECOND,-@Lease,SYSDATETIMEOFFSET()))
                 ORDER BY DueAt ASC)
                UPDATE Claimable SET ClaimedAt=SYSDATETIMEOFFSET(),ClaimedBy=@Claim
                OUTPUT {InsertedProjection(owned)};
                """;
            Command = CreateCommand(sql, cancellationToken: token);
            ResolveAsync = connection => ReadRowsAsync(connection, command =>
                {
                    Add(command, "Count", DbType.Int32, Parameters.Get<int>("Count"));
                    Add(command, "AsOf", DbType.DateTimeOffset, Parameters.Get<DateTimeOffset>("AsOf"));
                    Add(command, "Lease", DbType.Int32, Parameters.Get<int>("Lease"));
                    Add(command, "Claim", DbType.String, Parameters.Get<string>("Claim"));
                }, Map);
        }
    }

    internal sealed class RetireFailureTimeoutRequest : DataRequestBase<IDbConnection, int>
    {
        internal RetireFailureTimeoutRequest(string table, string tenant, ClaimedSagaTimeout claim, Guid source, CancellationToken token)
        {
            Parameters.Add("Tenant", tenant, DbType.String);
            Parameters.Add("Id", claim.Timeout.TimeoutId, DbType.String);
            Parameters.Add("Claim", claim.ClaimToken, DbType.String);
            Parameters.Add("Source", source, DbType.Guid);
            Parameters.Add("Delivery", ParseEpoch(claim.DeliveryEpoch), DbType.Guid);
            var sql = $"""
                DELETE FROM {table} WHERE TenantId=@Tenant AND DATALENGTH(TenantId)=DATALENGTH(@Tenant)
                 AND CONVERT(VARBINARY(128),TenantId)=CONVERT(VARBINARY(128),@Tenant)
                 AND TimeoutId=@Id AND DATALENGTH(TimeoutId)=DATALENGTH(@Id) AND CONVERT(VARBINARY(900),TimeoutId)=CONVERT(VARBINARY(900),@Id)
                 AND ClaimedBy=@Claim AND FailureHeld=0
                 AND (@Source IS NULL OR FailureSourceEpoch=@Source) AND (@Delivery IS NULL OR FailureDeliveryEpoch=@Delivery);
                """;
            Command = CreateCommand(sql, cancellationToken: token);
            ResolveAsync = connection => ExecuteNonQueryAsync(connection, command =>
                {
                    Add(command, "Tenant", DbType.String, Parameters.Get<string>("Tenant"));
                    Add(command, "Id", DbType.String, Parameters.Get<string>("Id"));
                    Add(command, "Claim", DbType.String, Parameters.Get<string>("Claim"));
                    Add(command, "Source", DbType.Guid, Parameters.Get<Guid>("Source"));
                    Add(command, "Delivery", DbType.Guid, Parameters.Get<Guid?>("Delivery"));
                });
        }
    }

    private static Guid? ParseEpoch(string? value) => value is null ? null : Guid.ParseExact(value,"N");
    private static string InsertedProjection(bool owned)
    {
        // Explicit prefixes avoid replacing CAST's SQL type names or expression aliases.
        var columns = new[] { "TimeoutId","SagaId","SagaType","TimeoutType","TimeoutData","DueAt","ScheduledAt","TenantId","ClaimedBy" };
        var projection = string.Join(',',columns.Select(name => "INSERTED." + name)) + ",";
        projection += owned ? "INSERTED.OwnedIncarnationId,INSERTED.OwnedGeneration,INSERTED.OwnedStateType,"
            : "CAST(NULL AS UNIQUEIDENTIFIER),CAST(NULL AS BIGINT),CAST(NULL AS NVARCHAR(500)),";
        return projection + string.Join(',',FailureProjectionColumns.Select(name => "INSERTED." + name));
    }
}
