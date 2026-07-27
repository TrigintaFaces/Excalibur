// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Data.SqlClient;

using Shouldly;

using Xunit;

#pragma warning disable CA2100 // SQL strings use compile-time-const columns + Guid-generated table names in a test fixture.

namespace Excalibur.Integration.Tests.Data.Inbox;

/// <summary>
/// Real-SQL-Server lock for the single-tenant → multi-tenant inbox <b>migration</b> (guide §RULING 2A,
/// <c>f4urdj</c>): the expand-contract migration anchors every pre-existing (untenanted) row to the reserved
/// <c>__untenanted__</c> sentinel and rebuilds the pair key into the triple key, so a consumer can grow a
/// shipped single-tenant inbox into multi-tenant without losing or mis-partitioning legacy messages.
/// </summary>
/// <remarks>
/// <para>
/// The migration steps mirror the shipped <c>002_MigrateToMultiTenant.sql</c> (add <c>TenantId NOT NULL
/// DEFAULT '__untenanted__'</c> → drop the pair PK → add the triple PK → drop the sentinel default), run on a
/// per-test isolated table so the arm measures the shipped migration shape rather than an invented one
/// (<c>f5</c> — the F-5 sweep backstops drift against the script). This lock is store-<em>independent</em> (pure
/// DDL/DML), so it does not depend on the store's deployment-mode construction surface.
/// </para>
/// <para>
/// <b>Safety + liveness.</b> LIVENESS — legacy rows survive the migration and carry the sentinel; a real-tenant
/// row inserts afterward. SAFETY — a real tenant carrying the same <c>(MessageId, HandlerType)</c> as a migrated
/// untenanted row coexists as a distinct row under the triple key, so the sentinel partition can never collide
/// with a real tenant. Real infrastructure is a hard requirement — never skipped (<c>Infra=Required</c>).
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Inbox")]
[Trait("Database", "SqlServer")]
[Trait("Infra", "Required")]
public sealed class SqlServerInboxStoreMigrationShould : IClassFixture<SqlServerInboxStoreContainerFixture>
{
    private const string SchemaName = "dbo";
    private const string Sentinel = "__untenanted__";

    private static readonly string[] ExpectedTripleKey = ["MessageId", "HandlerType", "TenantId"];

    // Mirrors the shipped 001_CreateInboxSchema.sql (pair, single-tenant) column set — kept in lockstep via F-5.
    private const string SharedColumns =
        "MessageId NVARCHAR(255) NOT NULL, HandlerType NVARCHAR(500) NOT NULL, " +
        "MessageType NVARCHAR(500) NOT NULL, Payload VARBINARY(MAX) NOT NULL, " +
        "Metadata NVARCHAR(MAX) NULL, ReceivedAt DATETIMEOFFSET NOT NULL, " +
        "ProcessedAt DATETIMEOFFSET NULL, Status INT NOT NULL DEFAULT 0, " +
        "RetryCount INT NOT NULL DEFAULT 0, LastError NVARCHAR(MAX) NULL, " +
        "LastAttemptAt DATETIMEOFFSET NULL, NextAttemptAt DATETIMEOFFSET NULL, " +
        "LeaseExpiresAtUtc DATETIMEOFFSET NULL, CorrelationId NVARCHAR(255) NULL, " +
        "Source NVARCHAR(255) NULL";

    private readonly SqlServerInboxStoreContainerFixture _fixture;

    public SqlServerInboxStoreMigrationShould(SqlServerInboxStoreContainerFixture fixture) =>
        _fixture = fixture;

    /// <summary>
    /// LIVENESS + SAFETY — after the expand-contract migration, pre-existing rows are anchored to the
    /// <c>__untenanted__</c> sentinel, the key is the triple, and a real tenant can coexist on the same message
    /// id without colliding with the migrated untenanted partition.
    /// </summary>
    [Fact]
    public async Task Anchor_Pre_Existing_Rows_To_The_Sentinel_And_Rebuild_The_Triple_Key()
    {
        _fixture.DockerAvailable.ShouldBeTrue(
            "SQL Server container must be available — this migration lock runs against real infrastructure and is never skipped (Infra=Required).");

        var table = "inbox_mig_" + Guid.NewGuid().ToString("N");

        await using var connection = _fixture.CreateConnection();
        await connection.OpenAsync().ConfigureAwait(false);

        // 1) Single-tenant (pair) table, per shipped 001 — no TenantId column.
        await ExecAsync(connection,
            $"CREATE TABLE [{SchemaName}].[{table}] ({SharedColumns}, " +
            $"CONSTRAINT [PK_{table}] PRIMARY KEY (MessageId, HandlerType));").ConfigureAwait(false);

        // Seed two legacy (pre-multi-tenant) rows.
        await ExecAsync(connection,
            $"INSERT INTO [{SchemaName}].[{table}] (MessageId, HandlerType, MessageType, Payload, ReceivedAt) VALUES " +
            "('legacy-1', 'H', 'T', 0x01, SYSDATETIMEOFFSET()), " +
            "('legacy-2', 'H', 'T', 0x02, SYSDATETIMEOFFSET());").ConfigureAwait(false);

        // 2) Apply the shipped 002 migration steps (mirrored): anchor to the sentinel, rebuild the key.
        await ExecAsync(connection,
            $"ALTER TABLE [{SchemaName}].[{table}] ADD TenantId NVARCHAR(255) NOT NULL " +
            $"CONSTRAINT [DF_{table}_TenantId] DEFAULT N'{Sentinel}';").ConfigureAwait(false);
        await ExecAsync(connection,
            $"ALTER TABLE [{SchemaName}].[{table}] DROP CONSTRAINT [PK_{table}];").ConfigureAwait(false);
        await ExecAsync(connection,
            $"ALTER TABLE [{SchemaName}].[{table}] ADD CONSTRAINT [PK_{table}] PRIMARY KEY (MessageId, HandlerType, TenantId);").ConfigureAwait(false);
        await ExecAsync(connection,
            $"ALTER TABLE [{SchemaName}].[{table}] DROP CONSTRAINT [DF_{table}_TenantId];").ConfigureAwait(false);

        // LIVENESS: legacy rows survived and are anchored to the sentinel.
        var anchored = await ScalarAsync(connection,
            $"SELECT COUNT(*) FROM [{SchemaName}].[{table}] WHERE TenantId = N'{Sentinel}';").ConfigureAwait(false);
        anchored.ShouldBe(2, "both pre-existing rows must survive the migration anchored to the __untenanted__ sentinel (no message lost).");

        // LIVENESS: the key is now the triple (TenantId participates in the PK).
        var pkColumns = await PkColumnsAsync(connection, table).ConfigureAwait(false);
        pkColumns.ShouldBe(ExpectedTripleKey, "the migration must rebuild the pair PK into the triple key.");

        // SAFETY: a real tenant carrying the SAME message id as a migrated untenanted row coexists as a distinct
        // row — the sentinel partition never collides with a real tenant.
        await ExecAsync(connection,
            $"INSERT INTO [{SchemaName}].[{table}] (MessageId, HandlerType, MessageType, Payload, ReceivedAt, TenantId) VALUES " +
            "('legacy-1', 'H', 'T', 0x03, SYSDATETIMEOFFSET(), N'tenant-real');").ConfigureAwait(false);

        var sameIdRows = await ScalarAsync(connection,
            $"SELECT COUNT(*) FROM [{SchemaName}].[{table}] WHERE MessageId = 'legacy-1' AND HandlerType = 'H';").ConfigureAwait(false);
        sameIdRows.ShouldBe(2, "a real tenant must coexist with the migrated __untenanted__ row on the same message id (triple-key isolation).");
    }

    /// <summary>
    /// SAFETY + LIVENESS — an install that has ALREADY adopted multi-tenancy (TenantId present, NOT NULL,
    /// key already rebuilt) still gets its tenant column re-collated to a binary collation.
    /// </summary>
    /// <remarks>
    /// This is the population the provisioning guards cannot see. An existence guard
    /// (<c>IF NOT EXISTS ... name = 'TenantId'</c>) and a nullability guard (<c>AND is_nullable = 1</c>) are
    /// both correct idempotency keys for provisioning — already-migrated means nothing to do. Collation breaks
    /// that reasoning: a column can exist, be NOT NULL, be fully migrated, and still carry the server's default
    /// collation, which is typically case-INSENSITIVE. Those installs are the only ones holding more than one
    /// tenant's rows, so they are the only ones that can leak.
    /// <para>
    /// RED by construction against the pre-fix scripts: this test provisions an OLD-SHAPE table, so a migration
    /// keyed on existence or nullability performs no work and the collation assertion fails. An arm that
    /// provisions a fresh table passes even against the unfixed script and proves nothing.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Re_Collate_A_Table_That_Already_Adopted_Multi_Tenancy()
    {
        _fixture.DockerAvailable.ShouldBeTrue(
            "SQL Server container must be available — this collation lock runs against real infrastructure and is never skipped (Infra=Required).");

        var table = "inbox_collate_" + Guid.NewGuid().ToString("N");

        await using var connection = _fixture.CreateConnection();
        await connection.OpenAsync().ConfigureAwait(false);

        // An install that already ran the earlier migration: TenantId present, NOT NULL, triple key in place —
        // and pinned to a case-INSENSITIVE collation, which is what the server default gives you.
        await ExecAsync(connection,
            $"CREATE TABLE [{SchemaName}].[{table}] ({SharedColumns}, " +
            "TenantId NVARCHAR(255) COLLATE SQL_Latin1_General_CP1_CI_AS NOT NULL, " +
            $"CONSTRAINT [PK_{table}] PRIMARY KEY (MessageId, HandlerType, TenantId));").ConfigureAwait(false);

        await ExecAsync(connection,
            $"INSERT INTO [{SchemaName}].[{table}] (MessageId, HandlerType, MessageType, Payload, ReceivedAt, TenantId) VALUES " +
            "('m-1', 'H', 'T', 0x01, SYSDATETIMEOFFSET(), N'Acme');").ConfigureAwait(false);

        // PREMISE: under the case-insensitive collation the tenant predicate fails OPEN — a second tenant
        // differing only in case reads the first tenant's row. This is the defect being closed.
        var leaked = await ScalarAsync(connection,
            $"SELECT COUNT(*) FROM [{SchemaName}].[{table}] WHERE TenantId = N'acme';").ConfigureAwait(false);
        leaked.ShouldBe(1, "premise check: a case-insensitive tenant column must leak before the fix, otherwise this test proves nothing.");

        // Apply the shipped re-collate step (mirrored): keyed on the COLLATION, not on existence or nullability.
        // TenantId participates in the primary key, so the key is dropped and rebuilt around the alter.
        await ExecAsync(connection,
            $"""
             IF EXISTS (SELECT * FROM sys.columns
                        WHERE object_id = OBJECT_ID(N'[{SchemaName}].[{table}]')
                          AND name = N'TenantId'
                          AND collation_name <> N'Latin1_General_BIN2')
             BEGIN
                 ALTER TABLE [{SchemaName}].[{table}] DROP CONSTRAINT [PK_{table}];
                 ALTER TABLE [{SchemaName}].[{table}]
                     ALTER COLUMN TenantId NVARCHAR(255) COLLATE Latin1_General_BIN2 NOT NULL;
                 ALTER TABLE [{SchemaName}].[{table}]
                     ADD CONSTRAINT [PK_{table}] PRIMARY KEY (MessageId, HandlerType, TenantId);
             END
             """).ConfigureAwait(false);

        var collation = await CollationAsync(connection, table).ConfigureAwait(false);
        collation.ShouldBe("Latin1_General_BIN2", "the already-adopted install must end up binary-collated; an existence- or nullability-keyed guard leaves it on the server default.");

        // SAFETY: the other tenant's predicate no longer matches this row.
        var crossTenant = await ScalarAsync(connection,
            $"SELECT COUNT(*) FROM [{SchemaName}].[{table}] WHERE TenantId = N'acme';").ConfigureAwait(false);
        crossTenant.ShouldBe(0, "'acme' must not read 'Acme' rows once the column is binary-collated.");

        // LIVENESS: the owning tenant still reads its own row. A column that matched nothing at all would
        // satisfy the safety arm above while breaking every tenant.
        var ownRow = await ScalarAsync(connection,
            $"SELECT COUNT(*) FROM [{SchemaName}].[{table}] WHERE TenantId = N'Acme';").ConfigureAwait(false);
        ownRow.ShouldBe(1, "'Acme' must still read its own row — a store that returns nothing to anybody also passes the safety arm.");

        // LIVENESS: the two tenants can now coexist. Under the case-insensitive collation the second insert
        // violated the primary key, so this was a denial of service as well as a disclosure.
        await ExecAsync(connection,
            $"INSERT INTO [{SchemaName}].[{table}] (MessageId, HandlerType, MessageType, Payload, ReceivedAt, TenantId) VALUES " +
            "('m-1', 'H', 'T', 0x02, SYSDATETIMEOFFSET(), N'acme');").ConfigureAwait(false);

        var bothTenants = await ScalarAsync(connection,
            $"SELECT COUNT(*) FROM [{SchemaName}].[{table}] WHERE MessageId = 'm-1';").ConfigureAwait(false);
        bothTenants.ShouldBe(2, "'Acme' and 'acme' must occupy distinct rows — under the case-insensitive key the second tenant could not be inserted at all.");
    }

    private static async Task ExecAsync(SqlConnection connection, string sql)
    {
        await using var command = new SqlCommand(sql, connection);
        _ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    private static async Task<int> ScalarAsync(SqlConnection connection, string sql)
    {
        await using var command = new SqlCommand(sql, connection);
        return (int)(await command.ExecuteScalarAsync().ConfigureAwait(false))!;
    }

    private static async Task<string> CollationAsync(SqlConnection connection, string table)
    {
        await using var command = new SqlCommand(
            "SELECT collation_name FROM sys.columns WHERE object_id = OBJECT_ID(@t) AND name = N'TenantId';",
            connection);
        _ = command.Parameters.AddWithValue("@t", $"[{SchemaName}].[{table}]");

        return (string)(await command.ExecuteScalarAsync().ConfigureAwait(false))!;
    }

    private static async Task<string[]> PkColumnsAsync(SqlConnection connection, string table)
    {
        await using var command = new SqlCommand(
            """
            SELECT c.name
            FROM sys.indexes i
            JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
            JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
            WHERE i.object_id = OBJECT_ID(@t) AND i.is_primary_key = 1
            ORDER BY ic.key_ordinal
            """,
            connection);
        _ = command.Parameters.AddWithValue("@t", $"[{SchemaName}].[{table}]");

        var columns = new List<string>();
        await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
        while (await reader.ReadAsync().ConfigureAwait(false))
        {
            columns.Add(reader.GetString(0));
        }

        return [.. columns];
    }
}
