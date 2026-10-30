// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text.RegularExpressions;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.SqlServer;
using Microsoft.Data.SqlClient;
using Excalibur.EventSourcing.Postgres;
using Excalibur.EventSourcing.Sqlite;
using Excalibur.EventSourcing.Oracle;
using Microsoft.Data.Sqlite;
using Npgsql;
using Oracle.ManagedDataAccess.Client;

namespace Excalibur.Examples.TransactionalInline;

internal static partial class SampleSchema
{
    internal static async Task<string> CreateAsync(string connectionString, string provider)
    {
        if (provider == "sqlite")
        {
            await using var sqlite = new SqliteConnection(connectionString);
            await sqlite.OpenAsync();
            await using var model = sqlite.CreateCommand();
#pragma warning disable CA2100 // Provider validates the fixed sample table.
            model.CommandText = SqliteProjectionSchema.CreateTransactionalTableScript(ModelTable);
#pragma warning restore CA2100
            await model.ExecuteNonQueryAsync();
            return string.Empty;
        }
        if (provider == "postgres")
        {
            await using var postgres = new NpgsqlConnection(connectionString);
            await postgres.OpenAsync();
            await using var command = postgres.CreateCommand();
#pragma warning disable CA2100 // Shipped provider DDL and validated sample identifiers.
            command.CommandText = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "event-store-postgres.sql"));
            await command.ExecuteNonQueryAsync();
            command.CommandText = PostgresProjectionSchema.CreateTransactionalTableScript(ModelTable);
#pragma warning restore CA2100
            await command.ExecuteNonQueryAsync();
            return "public";
        }
        if (provider == "oracle") { return await CreateOracleAsync(connectionString); }
        if (provider != "sqlserver") { throw new ArgumentException("Provider must be sqlserver, postgres, sqlite or oracle.", nameof(provider)); }
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var probe = new SqlCommand("SELECT OBJECT_ID(N'dbo.EventStoreEvents', N'U')", connection);
        if (await probe.ExecuteScalarAsync() is DBNull)
        {
            var ddl = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "event-store.sql"));
            foreach (var batch in BatchSeparator().Split(ddl).Where(b => !string.IsNullOrWhiteSpace(b)))
            {
#pragma warning disable CA2100 // Linked, shipped provider DDL; no user input is SQL text.
                await using var command = new SqlCommand(batch, connection);
#pragma warning restore CA2100
                await command.ExecuteNonQueryAsync();
            }
        }

#pragma warning disable CA2100 // Provider validates and quotes the fixed table name.
        await using var projection = new SqlCommand(SqlServerProjectionSchema.CreateTransactionalTableScript(
            ProjectionVersionConvention.Qualify("TransactionalOrderSummary", typeof(OrderSummary), '_')), connection);
#pragma warning restore CA2100
        await projection.ExecuteNonQueryAsync();
        return "dbo";
    }

    private static string ModelTable => ProjectionVersionConvention.Qualify("TransactionalOrderSummary", typeof(OrderSummary), '_');

    private static async Task<string> CreateOracleAsync(string connectionString)
    {
        await using var connection = new OracleConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT SYS_CONTEXT('USERENV', 'CURRENT_SCHEMA') FROM DUAL";
        var schema = (string)(await command.ExecuteScalarAsync())!;
        command.CommandText = "SELECT COUNT(*) FROM USER_TABLES WHERE TABLE_NAME = 'EVENTSTOREEVENTS'";
        if (Convert.ToInt32(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture) == 0)
        {
            var ddl = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "event-store-oracle.sql"));
            var sql = string.Join('\n', ddl.Split('\n').Where(line => !line.TrimStart().StartsWith("--", StringComparison.Ordinal)
                && !line.TrimStart().StartsWith("WHENEVER ", StringComparison.OrdinalIgnoreCase)));
            foreach (var statement in sql.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
#pragma warning disable CA2100 // Statements come from the shipped Oracle schema.
                command.CommandText = statement;
#pragma warning restore CA2100
                await command.ExecuteNonQueryAsync();
            }
        }
        command.CommandText = "SELECT COUNT(*) FROM USER_TABLES WHERE TABLE_NAME = :TableName";
        command.Parameters.Add("TableName", ModelTable.ToUpperInvariant());
        if (Convert.ToInt32(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture) == 0)
        {
            command.Parameters.Clear();
#pragma warning disable CA2100 // Provider validates the schema and model table.
            command.CommandText = OracleProjectionSchema.CreateTransactionalTableScript(ModelTable, schema);
#pragma warning restore CA2100
            await command.ExecuteNonQueryAsync();
        }
        return schema;
    }

    [GeneratedRegex(@"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BatchSeparator();
}
