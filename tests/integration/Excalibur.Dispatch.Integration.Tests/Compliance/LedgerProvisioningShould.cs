// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data.Common;
using System.Reflection;

using Dapper;

using Microsoft.Data.SqlClient;

using Npgsql;

using Shouldly;

using Tests.Shared.Fixtures;

namespace Excalibur.Dispatch.Integration.Tests.Compliance;

[Collection(ContainerCollections.SqlServer)]
[Trait("Category", "Integration")]
[Trait("Component", "Compliance")]
[Trait("Pattern", "Regression")]
public sealed class SqlServerLedgerProvisioningShould(SqlServerFixture fixture)
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public Task PreserveEvidenceAndRejectIncompatibleSchemas(int scenario) =>
        LedgerProvisioningChecks.RunAsync(() => new SqlConnection(fixture.ConnectionString), false, scenario);
}

[Collection(ContainerCollections.Postgres)]
[Trait("Category", "Integration")]
[Trait("Component", "Compliance")]
[Trait("Pattern", "Regression")]
public sealed class PostgresLedgerProvisioningShould(PostgresFixture fixture)
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public Task PreserveEvidenceAndRejectIncompatibleSchemas(int scenario) =>
        LedgerProvisioningChecks.RunAsync(() => new NpgsqlConnection(fixture.ConnectionString), true, scenario);
}

internal static class LedgerProvisioningChecks
{
    internal static async Task RunAsync(Func<DbConnection> connect, bool postgres, int scenario)
    {
        var schema = "ledger_" + Guid.NewGuid().ToString("N");
        var script = LoadScript(postgres).Replace("compliance", schema, StringComparison.Ordinal);
        var ledger = schema + (postgres ? ".erasure_destroyed_keys" : ".ErasureDestroyedKeys");
        var intents = schema + (postgres ? ".erasure_destruction_intents" : ".ErasureDestructionIntents");
        await using var connection = connect();
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await ExecuteAsync(connection, "CREATE SCHEMA " + schema);
        try
        {
            if (scenario == 0)
            {
                // Two independent sessions race to provision the same absent tables.
                await Task.WhenAll(ApplyAsync(connect, script), ApplyAsync(connect, script));
                await AssertCountAsync(connection, ledger, 0);
                await AssertCountAsync(connection, intents, 0);
                return;
            }

            if (scenario == 4)
            {
                await ExecuteAsync(connection, $"CREATE TABLE {intents} (sentinel INT NOT NULL PRIMARY KEY); INSERT INTO {intents} VALUES (7);");
            }
            else if (scenario is 2 or 3)
            {
                var request = postgres ? "request_id" : "RequestId";
                var handle = postgres ? "key_handle" : "KeyHandle";
                var generation = postgres ? "key_generation" : "KeyGeneration";
                var key = scenario == 2 ? request + ", " + handle : generation;
                await ExecuteAsync(connection, $"CREATE TABLE {ledger} ({request} INT NOT NULL, {handle} VARCHAR(20) NOT NULL, {generation} VARCHAR(20) NOT NULL, PRIMARY KEY ({key})); INSERT INTO {ledger} VALUES (7, 'handle', 'generation');");
            }
            else
            {
                await ApplyAsync(connect, script);
                var values = postgres
                    ? "(key_generation, key_handle, destroyed_at, recorded_by) VALUES ('generation', 'handle', CURRENT_TIMESTAMP, 'caller-assertion')"
                    : "(KeyGeneration, KeyHandle, DestroyedAt, RecordedBy) VALUES ('generation', 'handle', SYSDATETIMEOFFSET(), 'caller-assertion')";
                await ExecuteAsync(connection, $"INSERT INTO {ledger} {values};");
                if (scenario == 5)
                {
                    var column = postgres ? "recorded_by" : "RecordedBy";
                    await ExecuteAsync(connection, postgres
                        ? $"ALTER TABLE {ledger} ALTER COLUMN {column} DROP NOT NULL;"
                        : $"ALTER TABLE {ledger} ALTER COLUMN {column} NVARCHAR(32) NULL;");
                }
                else if (scenario == 6)
                {
                    await ExecuteAsync(connection, $"CREATE COLLATION {schema}.\"C\" FROM pg_catalog.\"POSIX\"; ALTER TABLE {ledger} ALTER COLUMN key_handle TYPE TEXT COLLATE {schema}.\"C\";");
                }
            }

            if (scenario == 1)
            {
                await ApplyAsync(connect, script);
                await ApplyAsync(connect, script);
            }
            else
            {
                var exception = await Should.ThrowAsync<DbException>(() => ApplyAsync(connect, script));
                exception.Message.ShouldContain("Incompatible");
            }

            await AssertCountAsync(connection, scenario == 4 ? intents : ledger, 1);
            if (scenario != 4)
            {
                var handle = postgres ? "key_handle" : "KeyHandle";
                var generation = postgres ? "key_generation" : "KeyGeneration";
                var evidence = $"SELECT {handle} FROM {ledger} WHERE {generation}='generation'";
                (await connection.ExecuteScalarAsync<string>(new CommandDefinition(evidence,
                    cancellationToken: TestContext.Current.CancellationToken))).ShouldBe("handle");
            }
            if (scenario == 4)
            {
                var exists = postgres
                    ? $"SELECT COUNT(*) FROM information_schema.tables WHERE table_schema='{schema}' AND table_name='erasure_destroyed_keys'"
                    : $"SELECT COUNT(*) FROM sys.tables WHERE schema_id=SCHEMA_ID('{schema}') AND name='ErasureDestroyedKeys'";
                (await connection.ExecuteScalarAsync<int>(new CommandDefinition(exists, cancellationToken: TestContext.Current.CancellationToken))).ShouldBe(0);
            }
        }
        finally
        {
            await ExecuteAsync(connection, postgres
                ? $"DROP SCHEMA {schema} CASCADE;"
                : $"DROP TABLE IF EXISTS {ledger}; DROP TABLE IF EXISTS {intents}; DROP SCHEMA {schema};");
        }
    }

    private static async Task ApplyAsync(Func<DbConnection> connect, string script)
    {
        await using var connection = connect();
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await ExecuteAsync(connection, script);
    }

    private static async Task AssertCountAsync(DbConnection connection, string table, int count) =>
        (await connection.ExecuteScalarAsync<int>(new CommandDefinition("SELECT COUNT(*) FROM " + table,
            cancellationToken: TestContext.Current.CancellationToken))).ShouldBe(count);

    private static async Task ExecuteAsync(DbConnection connection, string sql) =>
        _ = await connection.ExecuteAsync(new CommandDefinition(sql, commandTimeout: 90,
            cancellationToken: TestContext.Current.CancellationToken));

    private static string LoadScript(bool postgres)
    {
        var suffix = postgres ? "Postgres.004_AddKeyDestructionLedger.sql" : "SqlServer.005_AddKeyDestructionLedger.sql";
        var assembly = Assembly.GetExecutingAssembly();
        var name = assembly.GetManifestResourceNames().Single(name => name.EndsWith(suffix, StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(name) ?? throw new InvalidOperationException("Missing ledger SQL resource.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
