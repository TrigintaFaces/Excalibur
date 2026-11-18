// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;
using System.Data.Common;
using System.Runtime.ExceptionServices;
using Dapper;
using Excalibur.Data;

namespace Excalibur.Saga.SqlServer;

public sealed partial class SqlServerSagaTimeoutStore
{
    private sealed class ClaimTimeoutRowsRequest : DataRequestBase<IDbConnection, List<TimeoutRecord>>
    {
        internal ClaimTimeoutRowsRequest(string qualifiedTarget, bool hasOwnedColumns, DateTimeOffset asOf,
            int batchSize, int leaseSeconds, string claimToken, CancellationToken token)
        {
		var ownedProjection = hasOwnedColumns
			? "INSERTED.OwnedIncarnationId, INSERTED.OwnedGeneration, INSERTED.OwnedStateType"
			: "CAST(NULL AS UNIQUEIDENTIFIER) AS OwnedIncarnationId, CAST(NULL AS BIGINT) AS OwnedGeneration, CAST(NULL AS NVARCHAR(500)) AS OwnedStateType";

		var sql = $"""
			WITH Claimable AS (
				SELECT TOP (@BatchSize) *
				FROM {qualifiedTarget} WITH (READPAST, UPDLOCK, ROWLOCK)
				WHERE DueAt <= @AsOf
					AND (ClaimedAt IS NULL OR ClaimedAt < DATEADD(SECOND, -@LeaseTimeoutSeconds, SYSDATETIMEOFFSET()))
				ORDER BY DueAt ASC
			)
			UPDATE Claimable
			SET ClaimedAt = SYSDATETIMEOFFSET(), ClaimedBy = @ClaimToken
			OUTPUT
				INSERTED.TimeoutId, INSERTED.SagaId, INSERTED.SagaType, INSERTED.TimeoutType,
				INSERTED.TimeoutData, INSERTED.DueAt, INSERTED.ScheduledAt, INSERTED.TenantId,
				{ownedProjection}
			""";

            Parameters.Add("BatchSize", batchSize, DbType.Int32);
            Parameters.Add("AsOf", asOf, DbType.DateTimeOffset);
            Parameters.Add("LeaseTimeoutSeconds", leaseSeconds, DbType.Int32);
            Parameters.Add("ClaimToken", claimToken, DbType.String);
            Command = CreateCommand(sql, cancellationToken: token);
            ResolveAsync = async connection =>
            {
                List<TimeoutRecord> rows = [];
                IDataReader? owned = null;
                Exception? primary = null;
                try
                {
                    owned = await connection.ExecuteReaderAsync(Command).ConfigureAwait(false);
                    if (owned is not DbDataReader reader) { throw new NotSupportedException("SQL timeout claims require an asynchronous reader."); }
                    while (await reader.ReadAsync(token).ConfigureAwait(false))
                    {
                        rows.Add(new TimeoutRecord
                        {
                            TimeoutId = reader.GetString(0), SagaId = reader.GetString(1), SagaType = reader.GetString(2),
                            TimeoutType = reader.GetString(3), TimeoutData = (await reader.IsDBNullAsync(4, token).ConfigureAwait(false)) ? null : (byte[])reader.GetValue(4),
                            DueAt = await reader.GetFieldValueAsync<DateTimeOffset>(5, token).ConfigureAwait(false), ScheduledAt = await reader.GetFieldValueAsync<DateTimeOffset>(6, token).ConfigureAwait(false),
                            TenantId = (await reader.IsDBNullAsync(7, token).ConfigureAwait(false)) ? null : reader.GetString(7),
                            OwnedIncarnationId = (await reader.IsDBNullAsync(8, token).ConfigureAwait(false)) ? null : reader.GetGuid(8),
                            OwnedGeneration = (await reader.IsDBNullAsync(9, token).ConfigureAwait(false)) ? null : reader.GetInt64(9),
                            OwnedStateType = (await reader.IsDBNullAsync(10, token).ConfigureAwait(false)) ? null : reader.GetString(10),
                        });
                    }
                }
                catch (Exception error) { primary = error; }
                finally { DisposeRequestReader(owned, ref primary); }
                if (primary is not null) { ExceptionDispatchInfo.Capture(primary).Throw(); }
                return rows;
            };
        }
    }

    private sealed class OwnedColumnsRequest : DataRequestBase<IDbConnection, List<OwnedColumn>>
    {
        internal OwnedColumnsRequest(string target, CancellationToken token)
        {
            const string sql = """
                SELECT c.name,c.system_type_id,c.max_length,c.is_nullable FROM sys.columns c
                WHERE c.object_id=OBJECT_ID(@TableName,N'U')
                  AND c.name IN(N'OwnedIncarnationId',N'OwnedGeneration',N'OwnedStateType');
                """;
            Parameters.Add("TableName", target, DbType.String);
            Command = CreateCommand(sql, cancellationToken: token);
            ResolveAsync = async connection =>
            {
                List<OwnedColumn> rows = [];
                IDataReader? owned = null;
                Exception? primary = null;
                try
                {
                    owned = await connection.ExecuteReaderAsync(Command).ConfigureAwait(false);
                    if (owned is not DbDataReader reader) { throw new NotSupportedException("SQL metadata requires an asynchronous reader."); }
                    while (await reader.ReadAsync(token).ConfigureAwait(false))
                    {
                        rows.Add(new OwnedColumn(reader.GetString(0), reader.GetByte(1), reader.GetInt16(2), reader.GetBoolean(3)));
                    }
                }
                catch (Exception error) { primary = error; }
                finally { DisposeRequestReader(owned, ref primary); }
                if (primary is not null) { ExceptionDispatchInfo.Capture(primary).Throw(); }
                return rows;
            };
        }
    }

    private static void DisposeRequestReader(IDisposable? reader, ref Exception? primary)
    {
        try { reader?.Dispose(); }
        catch (Exception cleanup)
        {
            if (primary is null) { primary = cleanup; }
            else { primary.Data["SecondaryTimeoutRequestReaderDispose"] = cleanup; }
        }
    }
}
