// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using Dapper;
using Excalibur.Data;
using Microsoft.Data.SqlClient;

namespace Excalibur.Saga.SqlServer;

public sealed partial class SqlServerSagaTimeoutStore
{
	private async Task<int> CancelWithRetainedAuthorityAsync(string sagaId, string? timeoutId, CancellationToken token)
	{
		_options.Validate();
		var table = CaptureQualifiedTarget();
		var tenant = CurrentPartition().TenantId;
		SqlConnection? connection = null;
		SqlTransaction? transaction = null;
		Exception? primary = null;
		var committed = false;
		var count = 0;
		try
		{
			connection = _connectionFactory();
			await connection.OpenAsync(token).ConfigureAwait(false);
            var retained = await TryCancelWithRecoveryAuthorityAsync(connection, table, sagaId, timeoutId, token).ConfigureAwait(false);
            if (retained.Handled)
            {
                count = retained.Count;
                committed = true;
            }
            else
            {
				var owned = await connection.ResolveAsync(new CancellationMetadataRequest(table, token)).ConfigureAwait(false) == 1;
				transaction = (SqlTransaction)await connection.BeginTransactionAsync(token).ConfigureAwait(false);
				count = await connection.ResolveAsync(new CancelRetainedTimeoutRequest(table,
					owned, tenant, sagaId, timeoutId, transaction, token)).ConfigureAwait(false);
				await transaction.CommitAsync(token).ConfigureAwait(false);
				committed = true;
            }
		}
		catch (Exception error) { primary = error; }
		finally
		{
			if (transaction is not null)
			{
				if (!committed)
				{
					try
					{
						using var cleanupBudget = new CancellationTokenSource(TimeSpan.FromSeconds(10));
						await transaction.RollbackAsync(cleanupBudget.Token).ConfigureAwait(false);
					}
					catch (Exception cleanup) { PreserveCancellationFailure(ref primary, cleanup, "Rollback"); }
				}
				try { await transaction.DisposeAsync().ConfigureAwait(false); }
				catch (Exception cleanup) { PreserveCancellationFailure(ref primary, cleanup, "TransactionDispose"); }
			}
			if (connection is not null)
			{
				try { await connection.DisposeAsync().ConfigureAwait(false); }
				catch (Exception cleanup) { PreserveCancellationFailure(ref primary, cleanup, "ConnectionDispose"); }
			}
		}
		if (primary is not null) { ExceptionDispatchInfo.Capture(primary).Throw(); }
		return count;
	}

	private static void PreserveCancellationFailure(ref Exception? primary, Exception cleanup, string phase)
	{
		if (primary is null) { primary = cleanup; }
		else { primary.Data["SecondaryCancellation" + phase] = cleanup; }
	}

	private sealed class CancellationMetadataRequest : DataRequestBase<IDbConnection, int>
	{
		internal CancellationMetadataRequest(string target, CancellationToken token)
		{
			const string sql = """
				IF OBJECT_ID(@Target) IS NOT NULL AND OBJECT_ID(@Target,N'U') IS NULL
				 THROW 51004,'Public timeout cancellation requires a direct supported table, not a view or synonym.',1;
				IF ISNULL(OBJECT_ID(@Target,N'U'),-1)<>ISNULL(OBJECT_ID(N'dbo.SagaTimeouts',N'U'),-2) SELECT 0;
				ELSE IF OBJECT_ID(N'dispatch.SagaDurableScopes',N'U') IS NULL
				   AND OBJECT_ID(N'dispatch.SagaTimeoutMembership',N'U') IS NULL SELECT 0;
				ELSE BEGIN
				 IF OBJECT_ID(N'dispatch.SagaDurableScopes',N'U') IS NULL
				    OR COL_LENGTH(N'dispatch.SagaTimeoutMembership',N'IsCancelled') IS NULL
				    THROW 51004,'Complete durable timeout cancellation metadata before cancellation.',1;
				 SELECT 1;
				END;
				""";
			Parameters.Add("Target",target,DbType.String);
			Command = CreateCommand(sql, cancellationToken: token);
			ResolveAsync = connection => connection.ExecuteScalarAsync<int>(Command);
		}
	}

	private sealed class CancelRetainedTimeoutRequest : DataRequestBase<IDbConnection, int>
	{
		internal CancelRetainedTimeoutRequest(string table, bool owned, string tenant, string sagaId,
			string? timeoutId, SqlTransaction transaction, CancellationToken token, bool normalizeSagaId = false)
		{
			// Scope-first ordering matches transition commit and receiver admission. Retained membership,
			// rather than payload presence, revokes work already accepted by the delivery queue.
			var authority = owned ? """
				DECLARE @ScopeId UNIQUEIDENTIFIER;
				SELECT @ScopeId=ScopeId FROM dispatch.SagaDurableScopes WITH(UPDLOCK,HOLDLOCK)
				 WHERE TenantKey=@TenantKey AND SagaId=@SagaGuid;
				IF @ScopeId IS NOT NULL AND NOT EXISTS(SELECT 1 FROM dispatch.SagaDurableScopes
				 WHERE ScopeId=@ScopeId AND DATALENGTH(TenantId)=DATALENGTH(@TenantId)
				 AND CONVERT(VARBINARY(128),TenantId)=CONVERT(VARBINARY(128),@TenantId))
				 THROW 51004,'Exact tenant authority differs during timeout cancellation.',1;
				UPDATE dispatch.SagaTimeoutMembership SET IsCancelled=1
				 WHERE ScopeId=@ScopeId AND (@TimeoutId IS NULL OR
				 (TimeoutId=@TimeoutId AND DATALENGTH(TimeoutId)=DATALENGTH(@TimeoutId)
				 AND CONVERT(VARBINARY(900),TimeoutId)=CONVERT(VARBINARY(900),@TimeoutId)));
				""" : string.Empty;
            var sagaPredicate = normalizeSagaId ? "TRY_CONVERT(UNIQUEIDENTIFIER,SagaId)=@SagaGuid" : "SagaId=@SagaId";
			var sql = authority + $"""
				DELETE FROM {table} WHERE TenantId=@TenantId
				 AND DATALENGTH(TenantId)=DATALENGTH(@TenantId)
				 AND CONVERT(VARBINARY(128),TenantId)=CONVERT(VARBINARY(128),@TenantId)
				 AND {sagaPredicate} AND (@TimeoutId IS NULL OR (TimeoutId=@TimeoutId
				 AND DATALENGTH(TimeoutId)=DATALENGTH(@TimeoutId)
				 AND CONVERT(VARBINARY(900),TimeoutId)=CONVERT(VARBINARY(900),@TimeoutId)));
				SELECT CAST(@@ROWCOUNT AS INT);
				""";
			Parameters.Add("TenantId",tenant,DbType.String);
			Parameters.Add("TenantKey",SHA256.HashData(ExactCancellationTenantBytes(tenant)),DbType.Binary);
			Parameters.Add("SagaGuid",Guid.TryParse(sagaId,out var id) ? id : (Guid?)null,DbType.Guid);
			Parameters.Add("SagaId",sagaId,DbType.String);
			Parameters.Add("TimeoutId",timeoutId,DbType.String);
			Command=CreateCommand(sql,transaction:transaction,cancellationToken:token);
			ResolveAsync=connection=>connection.ExecuteScalarAsync<int>(Command);
		}
	}

	private static byte[] ExactCancellationTenantBytes(string value)
	{
		var bytes=new byte[checked(value.Length*sizeof(char))];
		for(var index=0;index<value.Length;index++)
		{
			bytes[index * 2]=(byte)value[index];
			bytes[(index * 2) + 1]=(byte)(value[index] >> 8);
		}
		return bytes;
	}
}
