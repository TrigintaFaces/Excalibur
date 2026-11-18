// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;
using Dapper;
using Excalibur.Data;

namespace Excalibur.Outbox.SqlServer.Requests;

/// <summary>Advances the scope first and retains its authority lock in the supplied transaction.</summary>
[NoTenantTerm(TenantConfinement.EstateWide, "The durable fence scope governs the entire outbox table, independently of tenant provenance.")]
internal sealed class SqlServerTransactionalFenceRequest : DataRequestBase<IDbConnection, long>
{
    internal SqlServerTransactionalFenceRequest(string fenceTable, string scope, long token, int timeout,
        IDbTransaction transaction, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(token);
        var sql = $"""
            DECLARE @Advanced TABLE (HighWaterToken bigint);
            MERGE {fenceTable} WITH (UPDLOCK, HOLDLOCK) AS f
            USING (SELECT @Scope AS OutboxTable) AS s ON (f.OutboxTable = s.OutboxTable)
            WHEN MATCHED THEN UPDATE SET HighWaterToken =
                CASE WHEN @Token > f.HighWaterToken THEN @Token ELSE f.HighWaterToken END
            WHEN NOT MATCHED THEN INSERT (OutboxTable,HighWaterToken) VALUES (@Scope,@Token)
            OUTPUT INSERTED.HighWaterToken INTO @Advanced;
            SELECT @HighWater = HighWaterToken FROM @Advanced;
            """;
        var parameters = new DynamicParameters();
        parameters.Add("Scope", scope);
        parameters.Add("Token", token);
        parameters.Add("HighWater", dbType: DbType.Int64, direction: ParameterDirection.Output);
        Command = CreateCommand(sql, parameters, transaction, timeout, cancellationToken: cancellationToken);
        ResolveAsync = connection => StaticDbCommand.ExecuteOutputsAsync(connection, Command, command =>
        {
            StaticDbCommand.AddParameter(command, "Scope", StaticDbCommand.RequiredInput<string>(Parameters, "Scope"), DbType.String);
            StaticDbCommand.AddParameter(command, "Token", StaticDbCommand.Input<long>(Parameters, "Token"), DbType.Int64);
            StaticDbCommand.AddOutputParameter(command, "HighWater", DbType.Int64);
        }, command => StaticDbCommand.Output<long>(command, Parameters, "HighWater", DbType.Int64));
    }
}
