// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;
using Excalibur.Data;
using Microsoft.Data.SqlClient;

namespace Excalibur.Saga.SqlServer;

public sealed partial class SqlServerSagaTimeoutStore
{
    private sealed class GetDueTimeoutRowsRequest : DataRequestBase<IDbConnection, IEnumerable<TimeoutRecord>>
    {
        internal GetDueTimeoutRowsRequest(string qualifiedTableName, DateTimeOffset asOf, CancellationToken token)
        {
            SagaSqlValidator.ThrowIfInvalidQualifiedName(qualifiedTableName);
            // Plain diagnostic read: retain exact8column/order predicate; no claim, lease or batch limit.
            var sql = $"""
                SELECT TimeoutId, SagaId, SagaType, TimeoutType, TimeoutData, DueAt, ScheduledAt, TenantId
                FROM {qualifiedTableName}
                WHERE DueAt <= @AsOf
                ORDER BY DueAt ASC
                """;
            Parameters.Add("AsOf", asOf, DbType.DateTimeOffset);
            Command = CreateCommand(sql, cancellationToken: token);
            ResolveAsync = async connection =>
                await ReadRowsAsync(connection, command =>
                {
                    _ = command.Parameters.Add(new SqlParameter
                    {
                        ParameterName = "AsOf", DbType = DbType.DateTimeOffset,
                        Value = Parameters.Get<DateTimeOffset>("AsOf"),
                    });
                }, static reader => new TimeoutRecord
                {
                    TimeoutId = reader.GetString(0), SagaId = reader.GetString(1), SagaType = reader.GetString(2),
                    TimeoutType = reader.GetString(3), TimeoutData = reader.GetValue(4) is DBNull ? null : (byte[])reader.GetValue(4),
                    DueAt = (DateTimeOffset)reader.GetValue(5), ScheduledAt = (DateTimeOffset)reader.GetValue(6),
                    TenantId = reader.GetValue(7) is DBNull ? null : reader.GetString(7),
                }).ConfigureAwait(false);
        }
    }
}
