// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;
using Dapper;
using Excalibur.Data;
using Microsoft.Data.SqlClient;

namespace Excalibur.Saga.SqlServer;

internal sealed partial class SqlServerSagaAtomicTransitionStore
{
    private sealed class ValidateDurabilityAsyncRequest : DataRequestBase<IDbConnection, object?>
    {
        internal ValidateDurabilityAsyncRequest(Action<DynamicParameters> bind, SqlTransaction? transaction, CancellationToken token)
        {
            const string sql = "SELECT delayed_durability_desc FROM sys.databases WHERE database_id=DB_ID();";
            bind(Parameters);
            Command = CreateCommand(sql, transaction: transaction, cancellationToken: token);
            ResolveAsync = async connection =>
            {
                var value = await ExecuteScalarAsync(connection, command =>
                {

                }).ConfigureAwait(false);
                return value is DBNull ? null : value;
            };
        }
    }

    private sealed class EnsureTimeoutAuthoritySchemaAsyncRequest : DataRequestBase<IDbConnection, object?>
    {
        internal EnsureTimeoutAuthoritySchemaAsyncRequest(Action<DynamicParameters> bind, SqlTransaction? transaction, CancellationToken token)
        {
            const string sql = """
            SELECT CASE WHEN COL_LENGTH(N'dispatch.SagaTimeoutMembership',N'StateType') IS NOT NULL
                AND COL_LENGTH(N'dispatch.SagaTimeoutMembership',N'IsCancelled') IS NOT NULL
              THEN CAST(1 AS INT) ELSE CAST(0 AS INT) END;
            """;
            bind(Parameters);
            Command = CreateCommand(sql, transaction: transaction, cancellationToken: token);
            ResolveAsync = async connection =>
            {
                var value = await ExecuteScalarAsync(connection, command =>
                {

                }).ConfigureAwait(false);
                return value is DBNull ? null : value;
            };
        }
    }

    private sealed class HasOwnedTimerIdentityAsyncRequest : DataRequestBase<IDbConnection, object?>
    {
        internal HasOwnedTimerIdentityAsyncRequest(Action<DynamicParameters> bind, SqlTransaction? transaction, CancellationToken token)
        {
            const string sql = """
            IF COL_LENGTH(N'dispatch.SagaTimeoutMembership',N'StateType') IS NULL
               OR COL_LENGTH(N'dispatch.SagaTimeoutMembership',N'IsCancelled') IS NULL
                THROW 51004, 'Complete the owned-timeout metadata schema before admission.', 1;
            SELECT CASE WHEN EXISTS (
                SELECT 1 FROM [dispatch].[SagaTimeoutMembership]
                WHERE ScopeId=@ScopeId AND TimeoutId=@DeliveryId
                  AND DATALENGTH(TimeoutId)=DATALENGTH(@DeliveryId)
                  AND CONVERT(VARBINARY(MAX),TimeoutId)=CONVERT(VARBINARY(MAX),@DeliveryId)
            ) THEN CAST(1 AS INT) ELSE CAST(0 AS INT) END;
            """;
            bind(Parameters);
            Command = CreateCommand(sql, transaction: transaction, cancellationToken: token);
            ResolveAsync = async connection =>
            {
                var value = await ExecuteScalarAsync(connection, command =>
                {
                    var scopeIdValue = Parameters.Get<Guid>("ScopeId");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "ScopeId",
                        DbType = DbType.Guid,
                        Value = scopeIdValue,
                    });
                    var deliveryIdValue = Parameters.Get<string?>("DeliveryId");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "DeliveryId",
                        DbType = DbType.String,
                        Value = (object?)deliveryIdValue ?? DBNull.Value,
                        Size = deliveryIdValue is { Length: <= 4000 } ? 4000 : 0,
                    });
                }).ConfigureAwait(false);
                return value is DBNull ? null : value;
            };
        }
    }

    private sealed class RefuseTimerDeliveryCollisionAsyncRequest : DataRequestBase<IDbConnection, object?>
    {
        internal RefuseTimerDeliveryCollisionAsyncRequest(Action<DynamicParameters> bind, SqlTransaction? transaction, CancellationToken token)
        {
            const string sql = """
            SELECT CASE WHEN EXISTS (
                SELECT 1 FROM [dispatch].[SagaTransitionReceipts]
                WHERE ScopeId=@ScopeId AND DeliveryIdHash=@DeliveryHash
            ) THEN CAST(1 AS INT) ELSE CAST(0 AS INT) END;
            """;
            bind(Parameters);
            Command = CreateCommand(sql, transaction: transaction, cancellationToken: token);
            ResolveAsync = async connection =>
            {
                var value = await ExecuteScalarAsync(connection, command =>
                {
                    var scopeIdValue = Parameters.Get<Guid>("ScopeId");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "ScopeId",
                        DbType = DbType.Guid,
                        Value = scopeIdValue,
                    });
                    var deliveryHashValue = Parameters.Get<byte[]?>("DeliveryHash");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "DeliveryHash",
                        DbType = DbType.Binary,
                        Value = (object?)deliveryHashValue ?? DBNull.Value,
                    });
                }).ConfigureAwait(false);
                return value is DBNull ? null : value;
            };
        }
    }

    private sealed class LockScopeAsyncRequest : DataRequestBase<IDbConnection, IEnumerable<ScopeRow>>
    {
        internal LockScopeAsyncRequest(Action<DynamicParameters> bind, SqlTransaction? transaction, CancellationToken token)
        {
            const string sql = """
            SELECT ScopeId,TenantId,SagaType,IncarnationId,Generation,IsClosed,CommittedVersion,NextEffectSequence,HasTimerParticipant
            FROM [dispatch].[SagaDurableScopes] WITH (UPDLOCK,HOLDLOCK)
            WHERE TenantKey=@TenantKey AND SagaId=@SagaId AND DATALENGTH(TenantId)=@TenantLength
                AND CONVERT(VARBINARY(128),TenantId)=@TenantBytes;
            """;
            bind(Parameters);
            Command = CreateCommand(sql, transaction: transaction, cancellationToken: token);
            ResolveAsync = async connection =>
                await ReadRowsAsync(connection, command =>
                {
                    var tenantKeyValue = Parameters.Get<byte[]?>("TenantKey");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "TenantKey",
                        DbType = DbType.Binary,
                        Value = (object?)tenantKeyValue ?? DBNull.Value,
                    });
                    var sagaIdValue = Parameters.Get<Guid>("SagaId");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "SagaId",
                        DbType = DbType.Guid,
                        Value = sagaIdValue,
                    });
                    var tenantBytesValue = Parameters.Get<byte[]?>("TenantBytes");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "TenantBytes",
                        DbType = DbType.Binary,
                        Value = (object?)tenantBytesValue ?? DBNull.Value,
                    });
                    var tenantLengthValue = Parameters.Get<int>("TenantLength");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "TenantLength",
                        DbType = DbType.Int32,
                        Value = tenantLengthValue,
                    });
                }, static reader => new ScopeRow(reader.GetGuid(0), reader.GetString(1),
                reader.GetString(2), reader.GetGuid(3), reader.GetInt64(4), reader.GetBoolean(5), reader.GetInt64(6),
                reader.GetInt64(7), reader.GetBoolean(8))).ConfigureAwait(false);
        }
    }

    private sealed class LockScopeAsyncRequest2 : DataRequestBase<IDbConnection, object?>
    {
        internal LockScopeAsyncRequest2(Action<DynamicParameters> bind, SqlTransaction? transaction, CancellationToken token)
        {
            const string sql = "SELECT COUNT_BIG(*) FROM [dispatch].[SagaDurableScopes] WITH(UPDLOCK,HOLDLOCK) WHERE TenantKey=@TenantKey AND SagaId=@SagaId;";
            bind(Parameters);
            Command = CreateCommand(sql, transaction: transaction, cancellationToken: token);
            ResolveAsync = async connection =>
            {
                var value = await ExecuteScalarAsync(connection, command =>
                {
                    var tenantKeyValue = Parameters.Get<byte[]?>("TenantKey");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "TenantKey",
                        DbType = DbType.Binary,
                        Value = (object?)tenantKeyValue ?? DBNull.Value,
                    });
                    var sagaIdValue = Parameters.Get<Guid>("SagaId");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "SagaId",
                        DbType = DbType.Guid,
                        Value = sagaIdValue,
                    });
                }).ConfigureAwait(false);
                return value is DBNull ? null : value;
            };
        }
    }

    private sealed class CreateScopeAsyncRequest : DataRequestBase<IDbConnection, int>
    {
        internal CreateScopeAsyncRequest(Action<DynamicParameters> bind, SqlTransaction? transaction, CancellationToken token)
        {
            const string sql = """
            INSERT INTO [dispatch].[SagaDurableScopes]
              (ScopeId,TenantId,TenantKey,SagaId,SagaType,IncarnationId,Generation,IsClosed,CommittedVersion,NextEffectSequence)
            VALUES(@ScopeId,@TenantId,@TenantKey,@SagaId,@SagaType,@IncarnationId,0,0,0,0);
            """;
            bind(Parameters);
            Command = CreateCommand(sql, transaction: transaction, cancellationToken: token);
            ResolveAsync = async connection =>
                await ExecuteNonQueryAsync(connection, command =>
                {
                    var tenantIdValue = Parameters.Get<string?>("TenantId");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "TenantId",
                        DbType = DbType.String,
                        Value = (object?)tenantIdValue ?? DBNull.Value,
                        Size = tenantIdValue is { Length: <= 4000 } ? 4000 : 0,
                    });
                    var tenantKeyValue = Parameters.Get<byte[]?>("TenantKey");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "TenantKey",
                        DbType = DbType.Binary,
                        Value = (object?)tenantKeyValue ?? DBNull.Value,
                    });
                    var tenantBytesValue = Parameters.Get<byte[]?>("TenantBytes");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "TenantBytes",
                        DbType = DbType.Binary,
                        Value = (object?)tenantBytesValue ?? DBNull.Value,
                    });
                    var tenantLengthValue = Parameters.Get<int>("TenantLength");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "TenantLength",
                        DbType = DbType.Int32,
                        Value = tenantLengthValue,
                    });
                    var sagaIdValue = Parameters.Get<Guid>("SagaId");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "SagaId",
                        DbType = DbType.Guid,
                        Value = sagaIdValue,
                    });
                    var scopeIdValue = Parameters.Get<Guid>("ScopeId");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "ScopeId",
                        DbType = DbType.Guid,
                        Value = scopeIdValue,
                    });
                    var sagaTypeValue = Parameters.Get<string?>("SagaType");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "SagaType",
                        DbType = DbType.String,
                        Value = (object?)sagaTypeValue ?? DBNull.Value,
                        Size = sagaTypeValue is { Length: <= 4000 } ? 4000 : 0,
                    });
                    var incarnationIdValue = Parameters.Get<Guid>("IncarnationId");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "IncarnationId",
                        DbType = DbType.Guid,
                        Value = incarnationIdValue,
                    });
                }).ConfigureAwait(false);
        }
    }

    private sealed class FindReceiptAsyncRequest : DataRequestBase<IDbConnection, IEnumerable<ReceiptRow>>
    {
        internal FindReceiptAsyncRequest(Action<DynamicParameters> bind, SqlTransaction? transaction, CancellationToken token)
        {
            const string sql = """
            SELECT Digest,CommittedVersion,DeliveryId FROM [dispatch].[SagaTransitionReceipts] WITH(UPDLOCK,HOLDLOCK)
            WHERE ScopeId=@ScopeId AND IncarnationId=@IncarnationId AND TransitionId=@TransitionId;
            """;
            bind(Parameters);
            Command = CreateCommand(sql, transaction: transaction, cancellationToken: token);
            ResolveAsync = async connection =>
                await ReadRowsAsync(connection, command =>
                {
                    var scopeIdValue = Parameters.Get<Guid>("ScopeId");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "ScopeId",
                        DbType = DbType.Guid,
                        Value = scopeIdValue,
                    });
                    var incarnationIdValue = Parameters.Get<Guid>("IncarnationId");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "IncarnationId",
                        DbType = DbType.Guid,
                        Value = incarnationIdValue,
                    });
                    var transitionIdValue = Parameters.Get<Guid>("TransitionId");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "TransitionId",
                        DbType = DbType.Guid,
                        Value = transitionIdValue,
                    });
                }, static reader => new ReceiptRow((byte[])reader.GetValue(0),reader.GetInt64(1),reader.GetValue(2) is DBNull?null:reader.GetString(2))).ConfigureAwait(false);
        }
    }

    private sealed class FindDeliveryAsyncRequest : DataRequestBase<IDbConnection, IEnumerable<ReceiptRow>>
    {
        internal FindDeliveryAsyncRequest(Action<DynamicParameters> bind, SqlTransaction? transaction, CancellationToken token)
        {
            const string sql = """
            SELECT Digest,CommittedVersion,DeliveryId FROM [dispatch].[SagaTransitionReceipts] WITH(UPDLOCK,HOLDLOCK)
            WHERE ScopeId=@ScopeId AND IncarnationId=@IncarnationId AND DeliveryIdHash=@DeliveryHash;
            """;
            bind(Parameters);
            Command = CreateCommand(sql, transaction: transaction, cancellationToken: token);
            ResolveAsync = async connection =>
                await ReadRowsAsync(connection, command =>
                {
                    var scopeIdValue = Parameters.Get<Guid>("ScopeId");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "ScopeId",
                        DbType = DbType.Guid,
                        Value = scopeIdValue,
                    });
                    var incarnationIdValue = Parameters.Get<Guid>("IncarnationId");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "IncarnationId",
                        DbType = DbType.Guid,
                        Value = incarnationIdValue,
                    });
                    var deliveryHashValue = Parameters.Get<byte[]?>("DeliveryHash");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "DeliveryHash",
                        DbType = DbType.Binary,
                        Value = (object?)deliveryHashValue ?? DBNull.Value,
                    });
                }, static reader => new ReceiptRow((byte[])reader.GetValue(0),reader.GetInt64(1),reader.GetValue(2) is DBNull?null:reader.GetString(2))).ConfigureAwait(false);
        }
    }

    private sealed class LoadSnapshotAsyncRequest : DataRequestBase<IDbConnection, IEnumerable<StateRow>>
    {
        internal LoadSnapshotAsyncRequest(Action<DynamicParameters> bind, SqlTransaction? transaction, CancellationToken token)
        {
            const string sql = """
            SELECT SagaType,
                CASE WHEN DATALENGTH(SagaType)=DATALENGTH(@StateType)
                    AND CONVERT(VARBINARY(1000),SagaType)=CONVERT(VARBINARY(1000),@StateType) THEN StateJson ELSE NULL END,
                Version,IsCompleted,CompletedAt,TimerRecoveryIncarnation,TimerRecoveryPendingBatch
            FROM [dispatch].[sagas] WITH(UPDLOCK,HOLDLOCK)
            WHERE TenantKey=@TenantKey AND SagaId=@SagaId AND DATALENGTH(TenantId)=@TenantLength
                AND CONVERT(VARBINARY(128),TenantId)=@TenantBytes;
            """;
            bind(Parameters);
            Command = CreateCommand(sql, transaction: transaction, cancellationToken: token);
            ResolveAsync = async connection =>
                await ReadRowsAsync(connection, command =>
                {
                    var tenantKeyValue = Parameters.Get<byte[]?>("TenantKey");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "TenantKey",
                        DbType = DbType.Binary,
                        Value = (object?)tenantKeyValue ?? DBNull.Value,
                    });
                    var tenantBytesValue = Parameters.Get<byte[]?>("TenantBytes");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "TenantBytes",
                        DbType = DbType.Binary,
                        Value = (object?)tenantBytesValue ?? DBNull.Value,
                    });
                    var tenantLengthValue = Parameters.Get<int>("TenantLength");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "TenantLength",
                        DbType = DbType.Int32,
                        Value = tenantLengthValue,
                    });
                    var sagaIdValue = Parameters.Get<Guid>("SagaId");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "SagaId",
                        DbType = DbType.Guid,
                        Value = sagaIdValue,
                    });
                    var stateTypeValue = Parameters.Get<string?>("StateType");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "StateType",
                        DbType = DbType.String,
                        Value = (object?)stateTypeValue ?? DBNull.Value,
                        Size = stateTypeValue is { Length: <= 4000 } ? 4000 : 0,
                    });
                }, static reader => new StateRow(reader.GetString(0),reader.GetValue(1) is DBNull?null:reader.GetString(1),reader.GetInt64(2),
            reader.GetBoolean(3),reader.GetValue(4) is DBNull?null:(DateTimeOffset)reader.GetValue(4),
            reader.IsDBNull(5)?null:reader.GetString(5),reader.IsDBNull(6)?null:reader.GetString(6))).ConfigureAwait(false);
        }
    }

    private sealed class IsOriginalTimeoutCurrentAsyncRequest : DataRequestBase<IDbConnection, IEnumerable<OriginalTimerAuthority>>
    {
        internal IsOriginalTimeoutCurrentAsyncRequest(Action<DynamicParameters> bind, SqlTransaction? transaction, CancellationToken token)
        {
            const string sql = """
            SELECT IncarnationId,Generation,StateType,IsCancelled
            FROM [dispatch].[SagaTimeoutMembership]
            WHERE ScopeId=@ScopeId AND TimeoutId=@TimeoutId
              AND DATALENGTH(TimeoutId)=DATALENGTH(@TimeoutId)
              AND CONVERT(VARBINARY(900),TimeoutId)=CONVERT(VARBINARY(900),@TimeoutId);
            """;
            bind(Parameters);
            Command = CreateCommand(sql, transaction: transaction, cancellationToken: token);
            ResolveAsync = async connection =>
                await ReadRowsAsync(connection, command =>
                {
                    var scopeIdValue = Parameters.Get<Guid>("ScopeId");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "ScopeId",
                        DbType = DbType.Guid,
                        Value = scopeIdValue,
                    });
                    var timeoutIdValue = Parameters.Get<string?>("TimeoutId");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "TimeoutId",
                        DbType = DbType.String,
                        Value = (object?)timeoutIdValue ?? DBNull.Value,
                        Size = timeoutIdValue is { Length: <= 4000 } ? 4000 : 0,
                    });
                }, static reader => new OriginalTimerAuthority(reader.GetGuid(0), reader.GetInt64(1),
            reader.GetString(2), reader.GetBoolean(3))).ConfigureAwait(false);
        }
    }

    private sealed class ScheduleAsyncRequest : DataRequestBase<IDbConnection, int>
    {
        internal ScheduleAsyncRequest(Action<DynamicParameters> bind, SqlTransaction? transaction, CancellationToken token)
        {
            const string sql = """
            INSERT INTO [dbo].[SagaTimeouts]
              (TimeoutId,SagaId,SagaType,TimeoutType,TimeoutData,DueAt,ScheduledAt,TenantId,OwnedIncarnationId,OwnedGeneration,OwnedStateType)
            VALUES(@TimeoutId,@SagaIdText,@SagaRoutingType,@TimeoutType,@Data,@DueAt,@ScheduledAt,@TenantId,@IncarnationId,@Generation,@StateType);
            INSERT INTO [dispatch].[SagaTimeoutMembership]
              (TimeoutId,ScopeId,IncarnationId,Generation,TransitionId,EffectOrdinal,StateType,IsCancelled)
            VALUES(@TimeoutId,@ScopeId,@IncarnationId,@Generation,@TransitionId,@Ordinal,@StateType,0);
            """;
            bind(Parameters);
            Command = CreateCommand(sql, transaction: transaction, cancellationToken: token);
            ResolveAsync = async connection =>
                await ExecuteNonQueryAsync(connection, command =>
                {
                    var tenantIdValue = Parameters.Get<string?>("TenantId");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "TenantId",
                        DbType = DbType.String,
                        Value = (object?)tenantIdValue ?? DBNull.Value,
                        Size = tenantIdValue is { Length: <= 4000 } ? 4000 : 0,
                    });
                    var tenantKeyValue = Parameters.Get<byte[]?>("TenantKey");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "TenantKey",
                        DbType = DbType.Binary,
                        Value = (object?)tenantKeyValue ?? DBNull.Value,
                    });
                    var tenantBytesValue = Parameters.Get<byte[]?>("TenantBytes");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "TenantBytes",
                        DbType = DbType.Binary,
                        Value = (object?)tenantBytesValue ?? DBNull.Value,
                    });
                    var tenantLengthValue = Parameters.Get<int>("TenantLength");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "TenantLength",
                        DbType = DbType.Int32,
                        Value = tenantLengthValue,
                    });
                    var sagaIdValue = Parameters.Get<Guid>("SagaId");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "SagaId",
                        DbType = DbType.Guid,
                        Value = sagaIdValue,
                    });
                    var timeoutIdValue = Parameters.Get<string?>("TimeoutId");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "TimeoutId",
                        DbType = DbType.String,
                        Value = (object?)timeoutIdValue ?? DBNull.Value,
                        Size = timeoutIdValue is { Length: <= 4000 } ? 4000 : 0,
                    });
                    var sagaIdTextValue = Parameters.Get<string?>("SagaIdText");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "SagaIdText",
                        DbType = DbType.String,
                        Value = (object?)sagaIdTextValue ?? DBNull.Value,
                        Size = sagaIdTextValue is { Length: <= 4000 } ? 4000 : 0,
                    });
                    var sagaRoutingTypeValue = Parameters.Get<string?>("SagaRoutingType");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "SagaRoutingType",
                        DbType = DbType.String,
                        Value = (object?)sagaRoutingTypeValue ?? DBNull.Value,
                        Size = sagaRoutingTypeValue is { Length: <= 4000 } ? 4000 : 0,
                    });
                    var timeoutTypeValue = Parameters.Get<string?>("TimeoutType");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "TimeoutType",
                        DbType = DbType.String,
                        Value = (object?)timeoutTypeValue ?? DBNull.Value,
                        Size = timeoutTypeValue is { Length: <= 4000 } ? 4000 : 0,
                    });
                    var dataValue = Parameters.Get<byte[]?>("Data");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "Data",
                        DbType = DbType.Binary,
                        Value = (object?)dataValue ?? DBNull.Value,
                    });
                    var dueAtValue = Parameters.Get<DateTimeOffset?>("DueAt");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "DueAt",
                        DbType = DbType.DateTimeOffset,
                        Value = (object?)dueAtValue ?? DBNull.Value,
                    });
                    var scheduledAtValue = Parameters.Get<DateTimeOffset?>("ScheduledAt");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "ScheduledAt",
                        DbType = DbType.DateTimeOffset,
                        Value = (object?)scheduledAtValue ?? DBNull.Value,
                    });
                    var scopeIdValue = Parameters.Get<Guid>("ScopeId");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "ScopeId",
                        DbType = DbType.Guid,
                        Value = scopeIdValue,
                    });
                    var incarnationIdValue = Parameters.Get<Guid>("IncarnationId");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "IncarnationId",
                        DbType = DbType.Guid,
                        Value = incarnationIdValue,
                    });
                    var generationValue = Parameters.Get<long>("Generation");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "Generation",
                        DbType = DbType.Int64,
                        Value = generationValue,
                    });
                    var transitionIdValue = Parameters.Get<Guid>("TransitionId");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "TransitionId",
                        DbType = DbType.Guid,
                        Value = transitionIdValue,
                    });
                    var stateTypeValue = Parameters.Get<string?>("StateType");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "StateType",
                        DbType = DbType.String,
                        Value = (object?)stateTypeValue ?? DBNull.Value,
                        Size = stateTypeValue is { Length: <= 4000 } ? 4000 : 0,
                    });
                    var ordinalValue = Parameters.Get<int>("Ordinal");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "Ordinal",
                        DbType = DbType.Int32,
                        Value = ordinalValue,
                    });
                }).ConfigureAwait(false);
        }
    }

    private sealed class CancelTimerAsyncRequest : DataRequestBase<IDbConnection, int>
    {
        internal CancelTimerAsyncRequest(Action<DynamicParameters> bind, SqlTransaction? transaction, CancellationToken token)
        {
            const string sql = """
            DELETE t FROM [dbo].[SagaTimeouts] t
            JOIN [dispatch].[SagaTimeoutMembership] m ON m.TimeoutId=t.TimeoutId COLLATE Latin1_General_BIN2
                AND DATALENGTH(m.TimeoutId)=DATALENGTH(t.TimeoutId)
                AND CONVERT(VARBINARY(900),m.TimeoutId)=CONVERT(VARBINARY(900),t.TimeoutId)
            WHERE m.ScopeId=@ScopeId AND m.IncarnationId=@IncarnationId
                AND t.TenantKey=@TenantKey AND DATALENGTH(t.TenantId)=@TenantLength
                AND CONVERT(VARBINARY(128),t.TenantId)=@TenantBytes
                AND t.TimeoutId=@TimeoutId AND DATALENGTH(t.TimeoutId)=DATALENGTH(@TimeoutId)
                AND CONVERT(VARBINARY(900),t.TimeoutId)=CONVERT(VARBINARY(900),@TimeoutId);
            UPDATE [dispatch].[SagaTimeoutMembership] SET IsCancelled=1
            WHERE ScopeId=@ScopeId AND IncarnationId=@IncarnationId
                AND TimeoutId=@TimeoutId AND DATALENGTH(TimeoutId)=DATALENGTH(@TimeoutId)
                AND CONVERT(VARBINARY(900),TimeoutId)=CONVERT(VARBINARY(900),@TimeoutId);
            """;
            bind(Parameters);
            Command = CreateCommand(sql, transaction: transaction, cancellationToken: token);
            ResolveAsync = async connection =>
                await ExecuteNonQueryAsync(connection, command =>
                {
                    var tenantIdValue = Parameters.Get<string?>("TenantId");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "TenantId",
                        DbType = DbType.String,
                        Value = (object?)tenantIdValue ?? DBNull.Value,
                        Size = tenantIdValue is { Length: <= 4000 } ? 4000 : 0,
                    });
                    var tenantKeyValue = Parameters.Get<byte[]?>("TenantKey");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "TenantKey",
                        DbType = DbType.Binary,
                        Value = (object?)tenantKeyValue ?? DBNull.Value,
                    });
                    var tenantBytesValue = Parameters.Get<byte[]?>("TenantBytes");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "TenantBytes",
                        DbType = DbType.Binary,
                        Value = (object?)tenantBytesValue ?? DBNull.Value,
                    });
                    var tenantLengthValue = Parameters.Get<int>("TenantLength");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "TenantLength",
                        DbType = DbType.Int32,
                        Value = tenantLengthValue,
                    });
                    var sagaIdValue = Parameters.Get<Guid>("SagaId");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "SagaId",
                        DbType = DbType.Guid,
                        Value = sagaIdValue,
                    });
                    var scopeIdValue = Parameters.Get<Guid>("ScopeId");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "ScopeId",
                        DbType = DbType.Guid,
                        Value = scopeIdValue,
                    });
                    var incarnationIdValue = Parameters.Get<Guid>("IncarnationId");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "IncarnationId",
                        DbType = DbType.Guid,
                        Value = incarnationIdValue,
                    });
                    var timeoutIdValue = Parameters.Get<string?>("TimeoutId");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "TimeoutId",
                        DbType = DbType.String,
                        Value = (object?)timeoutIdValue ?? DBNull.Value,
                        Size = timeoutIdValue is { Length: <= 4000 } ? 4000 : 0,
                    });
                }).ConfigureAwait(false);
        }
    }

    private sealed class CancelGenerationAsyncRequest : DataRequestBase<IDbConnection, int>
    {
        internal CancelGenerationAsyncRequest(Action<DynamicParameters> bind, SqlTransaction? transaction, CancellationToken token)
        {
            const string sql = """
            DELETE t FROM [dbo].[SagaTimeouts] t
            JOIN [dispatch].[SagaTimeoutMembership] m ON m.TimeoutId=t.TimeoutId COLLATE Latin1_General_BIN2
                AND DATALENGTH(m.TimeoutId)=DATALENGTH(t.TimeoutId)
                AND CONVERT(VARBINARY(900),m.TimeoutId)=CONVERT(VARBINARY(900),t.TimeoutId)
            WHERE m.ScopeId=@ScopeId AND m.IncarnationId=@IncarnationId
                AND t.TenantKey=@TenantKey AND DATALENGTH(t.TenantId)=@TenantLength
                AND CONVERT(VARBINARY(128),t.TenantId)=@TenantBytes;
            UPDATE [dispatch].[SagaTimeoutMembership] SET IsCancelled=1
            WHERE ScopeId=@ScopeId AND IncarnationId=@IncarnationId;
            """;
            bind(Parameters);
            Command = CreateCommand(sql, transaction: transaction, cancellationToken: token);
            ResolveAsync = async connection =>
                await ExecuteNonQueryAsync(connection, command =>
                {
                    var tenantIdValue = Parameters.Get<string?>("TenantId");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "TenantId",
                        DbType = DbType.String,
                        Value = (object?)tenantIdValue ?? DBNull.Value,
                        Size = tenantIdValue is { Length: <= 4000 } ? 4000 : 0,
                    });
                    var tenantKeyValue = Parameters.Get<byte[]?>("TenantKey");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "TenantKey",
                        DbType = DbType.Binary,
                        Value = (object?)tenantKeyValue ?? DBNull.Value,
                    });
                    var tenantBytesValue = Parameters.Get<byte[]?>("TenantBytes");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "TenantBytes",
                        DbType = DbType.Binary,
                        Value = (object?)tenantBytesValue ?? DBNull.Value,
                    });
                    var tenantLengthValue = Parameters.Get<int>("TenantLength");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "TenantLength",
                        DbType = DbType.Int32,
                        Value = tenantLengthValue,
                    });
                    var sagaIdValue = Parameters.Get<Guid>("SagaId");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "SagaId",
                        DbType = DbType.Guid,
                        Value = sagaIdValue,
                    });
                    var scopeIdValue = Parameters.Get<Guid>("ScopeId");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "ScopeId",
                        DbType = DbType.Guid,
                        Value = scopeIdValue,
                    });
                    var incarnationIdValue = Parameters.Get<Guid>("IncarnationId");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "IncarnationId",
                        DbType = DbType.Guid,
                        Value = incarnationIdValue,
                    });
                }).ConfigureAwait(false);
        }
    }

    private sealed class WriteStateAsyncRequest : DataRequestBase<IDbConnection, int>
    {
        internal WriteStateAsyncRequest(Action<DynamicParameters> bind, SqlTransaction? transaction, CancellationToken token)
        {
            const string sql = """
            MERGE [dispatch].[sagas] WITH(UPDLOCK,HOLDLOCK) AS target
            USING(SELECT @SagaId AS SagaId) AS source ON target.TenantKey=@TenantKey AND target.SagaId=source.SagaId
            WHEN MATCHED AND target.Version=@ExpectedVersion AND target.SagaType=@SagaType
            AND (target.TimerRecoveryIncarnation IS NULL OR target.TimerRecoveryIncarnation=@RecoveryIncarnation)
            AND target.TimerRecoveryPendingBatch IS NULL
                AND DATALENGTH(target.SagaType)=DATALENGTH(@SagaType)
                AND CONVERT(VARBINARY(1000),target.SagaType)=CONVERT(VARBINARY(1000),@SagaType)
                AND DATALENGTH(target.TenantId)=@TenantLength AND CONVERT(VARBINARY(128),target.TenantId)=@TenantBytes
            THEN UPDATE SET TimerRecoveryIncarnation=@RecoveryIncarnation,StateJson=@StateJson,IsCompleted=@Completed,CompletedAt=@CompletedAt,Version=@Version,UpdatedUtc=SYSUTCDATETIME()
            WHEN NOT MATCHED AND @ExpectedVersion=0 THEN INSERT
                (SagaId,SagaType,StateJson,IsCompleted,CompletedAt,TenantId,Version,TimerRecoveryIncarnation)
                VALUES(@SagaId,@SagaType,@StateJson,@Completed,@CompletedAt,@TenantId,@Version,@RecoveryIncarnation);
            """;
            bind(Parameters);
            Command = CreateCommand(sql, transaction: transaction, cancellationToken: token);
            ResolveAsync = async connection =>
                await ExecuteNonQueryAsync(connection, command =>
                {
                    var tenantIdValue = Parameters.Get<string?>("TenantId");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "TenantId",
                        DbType = DbType.String,
                        Value = (object?)tenantIdValue ?? DBNull.Value,
                        Size = tenantIdValue is { Length: <= 4000 } ? 4000 : 0,
                    });
                    var tenantKeyValue = Parameters.Get<byte[]?>("TenantKey");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "TenantKey",
                        DbType = DbType.Binary,
                        Value = (object?)tenantKeyValue ?? DBNull.Value,
                    });
                    var tenantBytesValue = Parameters.Get<byte[]?>("TenantBytes");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "TenantBytes",
                        DbType = DbType.Binary,
                        Value = (object?)tenantBytesValue ?? DBNull.Value,
                    });
                    var tenantLengthValue = Parameters.Get<int>("TenantLength");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "TenantLength",
                        DbType = DbType.Int32,
                        Value = tenantLengthValue,
                    });
                    var sagaIdValue = Parameters.Get<Guid>("SagaId");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "SagaId",
                        DbType = DbType.Guid,
                        Value = sagaIdValue,
                    });
                    var sagaTypeValue = Parameters.Get<string?>("SagaType");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "SagaType",
                        DbType = DbType.String,
                        Value = (object?)sagaTypeValue ?? DBNull.Value,
                        Size = sagaTypeValue is { Length: <= 4000 } ? 4000 : 0,
                    });
                    var stateJsonValue = Parameters.Get<string?>("StateJson");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "StateJson",
                        DbType = DbType.String,
                        Value = (object?)stateJsonValue ?? DBNull.Value,
                        Size = stateJsonValue is { Length: <= 4000 } ? 4000 : 0,
                    });
                    var completedValue = Parameters.Get<bool>("Completed");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "Completed",
                        DbType = DbType.Boolean,
                        Value = completedValue,
                    });
                    var completedAtValue = Parameters.Get<DateTimeOffset?>("CompletedAt");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "CompletedAt",
                        DbType = DbType.DateTimeOffset,
                        Value = (object?)completedAtValue ?? DBNull.Value,
                    });
                    var expectedVersionValue = Parameters.Get<long>("ExpectedVersion");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "ExpectedVersion",
                        DbType = DbType.Int64,
                        Value = expectedVersionValue,
                    });
                    var versionValue = Parameters.Get<long>("Version");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "Version",
                        DbType = DbType.Int64,
                        Value = versionValue,
                    });
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "RecoveryIncarnation",
                        DbType = DbType.AnsiStringFixedLength,
                        Size = 32,
                        Value = Parameters.Get<string>("RecoveryIncarnation"),
                    });
                }).ConfigureAwait(false);
        }
    }

    private sealed class InsertReceiptAsyncRequest : DataRequestBase<IDbConnection, int>
    {
        internal InsertReceiptAsyncRequest(Action<DynamicParameters> bind, SqlTransaction? transaction, CancellationToken token)
        {
            const string sql = """
            INSERT INTO [dispatch].[SagaTransitionReceipts]
             (ScopeId,IncarnationId,TransitionId,ExpectedVersion,CommittedVersion,Digest,DeliveryIdHash,DeliveryId,EncodedEffectManifest,TimerCount,MessageCount)
            VALUES(@ScopeId,@IncarnationId,@TransitionId,@ExpectedVersion,@Version,@Digest,@DeliveryHash,@DeliveryId,@Manifest,@TimerCount,@MessageCount);
            """;
            bind(Parameters);
            Command = CreateCommand(sql, transaction: transaction, cancellationToken: token);
            ResolveAsync = async connection =>
                await ExecuteNonQueryAsync(connection, command =>
                {
                    var scopeIdValue = Parameters.Get<Guid>("ScopeId");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "ScopeId",
                        DbType = DbType.Guid,
                        Value = scopeIdValue,
                    });
                    var incarnationIdValue = Parameters.Get<Guid>("IncarnationId");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "IncarnationId",
                        DbType = DbType.Guid,
                        Value = incarnationIdValue,
                    });
                    var transitionIdValue = Parameters.Get<Guid>("TransitionId");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "TransitionId",
                        DbType = DbType.Guid,
                        Value = transitionIdValue,
                    });
                    var expectedVersionValue = Parameters.Get<long>("ExpectedVersion");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "ExpectedVersion",
                        DbType = DbType.Int64,
                        Value = expectedVersionValue,
                    });
                    var versionValue = Parameters.Get<long>("Version");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "Version",
                        DbType = DbType.Int64,
                        Value = versionValue,
                    });
                    var digestValue = Parameters.Get<byte[]?>("Digest");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "Digest",
                        DbType = DbType.Binary,
                        Value = (object?)digestValue ?? DBNull.Value,
                    });
                    var deliveryHashValue = Parameters.Get<byte[]?>("DeliveryHash");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "DeliveryHash",
                        DbType = DbType.Binary,
                        Value = (object?)deliveryHashValue ?? DBNull.Value,
                    });
                    var deliveryIdValue = Parameters.Get<string?>("DeliveryId");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "DeliveryId",
                        DbType = DbType.String,
                        Value = (object?)deliveryIdValue ?? DBNull.Value,
                        Size = deliveryIdValue is { Length: <= 4000 } ? 4000 : 0,
                    });
                    var manifestValue = Parameters.Get<byte[]?>("Manifest");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "Manifest",
                        DbType = DbType.Binary,
                        Value = (object?)manifestValue ?? DBNull.Value,
                    });
                    var timerCountValue = Parameters.Get<int>("TimerCount");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "TimerCount",
                        DbType = DbType.Int32,
                        Value = timerCountValue,
                    });
                    var messageCountValue = Parameters.Get<int>("MessageCount");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "MessageCount",
                        DbType = DbType.Int32,
                        Value = messageCountValue,
                    });
                }).ConfigureAwait(false);
        }
    }

    private sealed class InsertMembershipAsyncRequest : DataRequestBase<IDbConnection, int>
    {
        internal InsertMembershipAsyncRequest(Action<DynamicParameters> bind, SqlTransaction? transaction, CancellationToken token)
        {
            const string sql = """
            INSERT INTO [dispatch].[SagaOutboxMembership]
             (MessageId,ScopeId,IncarnationId,TransitionId,EffectOrdinal,EffectSequence,CanonicalWireHash)
            VALUES(@MessageId,@ScopeId,@IncarnationId,@TransitionId,@Ordinal,@Sequence,@WireHash);
            """;
            bind(Parameters);
            Command = CreateCommand(sql, transaction: transaction, cancellationToken: token);
            ResolveAsync = async connection =>
                await ExecuteNonQueryAsync(connection, command =>
                {
                    var messageIdValue = Parameters.Get<string?>("MessageId");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "MessageId",
                        DbType = DbType.String,
                        Value = (object?)messageIdValue ?? DBNull.Value,
                        Size = messageIdValue is { Length: <= 4000 } ? 4000 : 0,
                    });
                    var scopeIdValue = Parameters.Get<Guid>("ScopeId");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "ScopeId",
                        DbType = DbType.Guid,
                        Value = scopeIdValue,
                    });
                    var incarnationIdValue = Parameters.Get<Guid>("IncarnationId");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "IncarnationId",
                        DbType = DbType.Guid,
                        Value = incarnationIdValue,
                    });
                    var transitionIdValue = Parameters.Get<Guid>("TransitionId");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "TransitionId",
                        DbType = DbType.Guid,
                        Value = transitionIdValue,
                    });
                    var ordinalValue = Parameters.Get<int>("Ordinal");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "Ordinal",
                        DbType = DbType.Int32,
                        Value = ordinalValue,
                    });
                    var sequenceValue = Parameters.Get<long>("Sequence");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "Sequence",
                        DbType = DbType.Int64,
                        Value = sequenceValue,
                    });
                    var wireHashValue = Parameters.Get<byte[]?>("WireHash");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "WireHash",
                        DbType = DbType.Binary,
                        Value = (object?)wireHashValue ?? DBNull.Value,
                    });
                }).ConfigureAwait(false);
        }
    }

    private sealed class UpdateScopeAsyncRequest : DataRequestBase<IDbConnection, int>
    {
        internal UpdateScopeAsyncRequest(Action<DynamicParameters> bind, SqlTransaction? transaction, CancellationToken token)
        {
            const string sql = """
            UPDATE [dispatch].[SagaDurableScopes] SET CommittedVersion=@Version,Generation=@Generation,
                IsClosed=@Completed,NextEffectSequence=@NextSequence,HasTimerParticipant=CASE WHEN @HasTimerParticipant=1 THEN 1 ELSE HasTimerParticipant END
            WHERE ScopeId=@ScopeId AND IncarnationId=@IncarnationId AND CommittedVersion=@ExpectedVersion;
            """;
            bind(Parameters);
            Command = CreateCommand(sql, transaction: transaction, cancellationToken: token);
            ResolveAsync = async connection =>
                await ExecuteNonQueryAsync(connection, command =>
                {
                    var versionValue = Parameters.Get<long>("Version");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "Version",
                        DbType = DbType.Int64,
                        Value = versionValue,
                    });
                    var generationValue = Parameters.Get<long>("Generation");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "Generation",
                        DbType = DbType.Int64,
                        Value = generationValue,
                    });
                    var completedValue = Parameters.Get<bool>("Completed");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "Completed",
                        DbType = DbType.Boolean,
                        Value = completedValue,
                    });
                    var hasTimerParticipantValue = Parameters.Get<bool>("HasTimerParticipant");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "HasTimerParticipant",
                        DbType = DbType.Boolean,
                        Value = hasTimerParticipantValue,
                    });
                    var nextSequenceValue = Parameters.Get<long>("NextSequence");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "NextSequence",
                        DbType = DbType.Int64,
                        Value = nextSequenceValue,
                    });
                    var scopeIdValue = Parameters.Get<Guid>("ScopeId");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "ScopeId",
                        DbType = DbType.Guid,
                        Value = scopeIdValue,
                    });
                    var incarnationIdValue = Parameters.Get<Guid>("IncarnationId");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "IncarnationId",
                        DbType = DbType.Guid,
                        Value = incarnationIdValue,
                    });
                    var expectedVersionValue = Parameters.Get<long>("ExpectedVersion");
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "ExpectedVersion",
                        DbType = DbType.Int64,
                        Value = expectedVersionValue,
                    });
                }).ConfigureAwait(false);
        }
    }

}
