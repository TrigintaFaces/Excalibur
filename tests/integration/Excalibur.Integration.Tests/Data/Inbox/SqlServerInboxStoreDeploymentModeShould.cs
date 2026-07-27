// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Inbox;
using Excalibur.Inbox.SqlServer;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

using Xunit;

#pragma warning disable CA2100 // SQL strings use compile-time-const columns + Guid-generated table names in a test fixture.

namespace Excalibur.Integration.Tests.Data.Inbox;

/// <summary>
/// Real-SQL-Server lock for the Design-A <b>column-agnostic-per-deployment</b> tenant contract on
/// <see cref="SqlServerInboxStore"/> (guide §RULING 2A). The store carries the tenant term <b>iff</b> it is
/// constructed multi-tenant (an <see cref="ITenantContext"/> is supplied); a single-tenant store carries no
/// tenant column, and a mode↔schema mismatch fails closed on first use.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why deployment-mode, not request-scope.</b> The earlier "unscoped request shares a schema with a tenant
/// and claims across" premise is <em>structurally unreachable</em> under Design A: a non-MT deployment runs the
/// pair schema (no <c>TenantId</c> column — a single tenant has no other tenant's rows to collide with), and an
/// MT deployment runs the triple schema plus a startup handshake that makes "MT host silently on the pair
/// schema" fail fast. The properties this lock binds are therefore per-mode, and asserted through the store's
/// <em>public</em> API (not a mechanism), so they survive the exact-column/sentinel details
/// (<c>testing-patterns §3</c> mechanism-vs-property; <c>pin-interface-seam-before-tests</c>).
/// </para>
/// <para>
/// <b>Fixtures mirror the shipped DDL</b> (<c>001_CreateInboxSchema.sql</c> = pair,
/// <c>001_CreateInboxSchema.MultiTenant.sql</c> = triple) on per-test isolated table names, so an arm measures
/// the store against the real deployment schema rather than an invented one (<c>f5</c> — the shared column set,
/// including <c>LeaseExpiresAtUtc</c>, is kept in lockstep with the shipped scripts; the F-5 sweep backstops
/// drift). Real infrastructure is a hard requirement — never skipped (<c>Infra=Required</c>).
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Inbox")]
[Trait("Database", "SqlServer")]
[Trait("Infra", "Required")]
public sealed class SqlServerInboxStoreDeploymentModeShould : IClassFixture<SqlServerInboxStoreContainerFixture>
{
    private const string SchemaName = "dbo";
    private const string HandlerType = "TestHandler";
    private const string TenantA = "tenant-A";
    private const string TenantB = "tenant-B";

    // The shipped column set, shared by both schemas (mirrors 001_CreateInboxSchema[.MultiTenant].sql verbatim;
    // the triple schema adds TenantId NOT NULL). Kept in lockstep with the shipped DDL — see the F-5 sweep.
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

    public SqlServerInboxStoreDeploymentModeShould(SqlServerInboxStoreContainerFixture fixture) =>
        _fixture = fixture;

    // ─────────────────────────── NON-MT (pair schema) ───────────────────────────

    /// <summary>
    /// LIVENESS — a single-tenant store on the pair schema claims and deduplicates. Isolation is trivial (one
    /// tenant), so the load-bearing property is that the non-MT path is not <em>inert</em>: the claim works and
    /// a genuine duplicate still dedups on the pair key.
    /// </summary>
    [Fact]
    public async Task Claim_And_Dedup_A_Non_Multi_Tenant_Message_On_The_Pair_Schema()
    {
        var table = await NewPairTableAsync().ConfigureAwait(false);
        var store = CreateStore(table, requireTenant: false);
        const string messageId = "msg-nonmt-claim";

        var first = await store.TryMarkAsProcessedAsync(messageId, HandlerType, CancellationToken.None).ConfigureAwait(false);
        var second = await store.TryMarkAsProcessedAsync(messageId, HandlerType, CancellationToken.None).ConfigureAwait(false);

        first.ShouldBeTrue("a single-tenant store must win the first claim on the pair schema (the non-MT path is live, not inert).");
        second.ShouldBeFalse("the second claim must deduplicate on the pair key — dropping the tenant column must not disable dedup.");
    }

    /// <summary>
    /// LIVENESS for the non-MT READ path — a single-tenant store reads back its own entry on the pair schema.
    /// This is the RED-detection lock for the SELECT-list emission gap: a read path that projects the tenant
    /// column unconditionally throws "Invalid column name" on the no-column default deployment.
    /// </summary>
    [Fact]
    public async Task Read_Back_A_Non_Multi_Tenant_Entry_On_The_Pair_Schema()
    {
        var table = await NewPairTableAsync().ConfigureAwait(false);
        var store = CreateStore(table, requireTenant: false);
        const string messageId = "msg-nonmt-read";

        _ = await store.CreateEntryAsync(
            messageId, HandlerType, "TestMessage", [1, 2, 3],
            new Dictionary<string, object>(), CancellationToken.None).ConfigureAwait(false);

        var entry = await store.GetEntryAsync(messageId, HandlerType, CancellationToken.None).ConfigureAwait(false);
        entry.ShouldNotBeNull(
            "a single-tenant store must read back its own entry on the pair schema — the read path must not " +
            "project a tenant column the non-MT schema does not have (RED-detects the SELECT-list emission gap).");
    }

    // ─────────────────────────── MT (triple schema) ───────────────────────────

    /// <summary>
    /// SAFETY + LIVENESS — under multi-tenancy, tenant B cannot see or dedup against tenant A's row, and tenant
    /// A still sees its own. The same <c>(MessageId, HandlerType)</c> is a distinct row per tenant (the triple
    /// key), so a B-resolved claim lands in B's partition and never promotes A's row.
    /// </summary>
    [Fact]
    public async Task Isolate_Rows_By_Tenant_On_The_Triple_Schema()
    {
        var table = await NewTripleTableAsync().ConfigureAwait(false);
        var storeA = CreateStore(table, requireTenant: true, new FixedTenantContext(TenantA));
        var storeB = CreateStore(table, requireTenant: true, new FixedTenantContext(TenantB));
        const string messageId = "msg-shared-id";

        _ = await storeA.CreateEntryAsync(
            messageId, HandlerType, "TestMessage", [1, 2, 3],
            new Dictionary<string, object>(), CancellationToken.None).ConfigureAwait(false);

        // LIVENESS: A sees its own row.
        var aEntry = await storeA.GetEntryAsync(messageId, HandlerType, CancellationToken.None).ConfigureAwait(false);
        aEntry.ShouldNotBeNull("tenant A must see its own row (LIVENESS — a store that returns nothing to anyone would pass a safety-only assertion).");

        // SAFETY: B cannot see A's row.
        var bEntry = await storeB.GetEntryAsync(messageId, HandlerType, CancellationToken.None).ConfigureAwait(false);
        bEntry.ShouldBeNull("tenant B must NOT see tenant A's row — the triple key isolates by tenant (SAFETY).");

        // SAFETY: B's claim on the same id lands in B's own partition and does not promote A's row.
        var bClaim = await storeB.TryMarkAsProcessedAsync(messageId, HandlerType, CancellationToken.None).ConfigureAwait(false);
        bClaim.ShouldBeTrue("tenant B claims in its OWN partition — the same (MessageId, HandlerType) is a distinct row per tenant.");

        var aAfter = await storeA.GetEntryAsync(messageId, HandlerType, CancellationToken.None).ConfigureAwait(false);
        aAfter.ShouldNotBeNull("tenant A's row must survive tenant B's activity.");
        aAfter!.Status.ShouldNotBe(
            InboxStatus.Processed,
            "tenant B's claim must not promote tenant A's row to Processed — cross-tenant write isolation (SAFETY).");
    }

    /// <summary>
    /// SAFETY-liveness partner — a genuine duplicate <em>within</em> a tenant still deduplicates. Isolation must
    /// not disable dedup: without this, an isolation impl that simply never conflicts would pass the safety arm.
    /// </summary>
    [Fact]
    public async Task Dedup_A_Genuine_Duplicate_Within_A_Single_Tenant_On_The_Triple_Schema()
    {
        var table = await NewTripleTableAsync().ConfigureAwait(false);
        var storeA = CreateStore(table, requireTenant: true, new FixedTenantContext(TenantA));
        const string messageId = "msg-dup-within-tenant";

        var first = await storeA.TryMarkAsProcessedAsync(messageId, HandlerType, CancellationToken.None).ConfigureAwait(false);
        var second = await storeA.TryMarkAsProcessedAsync(messageId, HandlerType, CancellationToken.None).ConfigureAwait(false);

        first.ShouldBeTrue("the first claim within tenant A must win (LIVENESS).");
        second.ShouldBeFalse("a genuine duplicate within the same tenant must deduplicate on the triple key (SAFETY).");
    }

    // ─────────────────────── Startup handshake (fail-closed) ───────────────────────

    /// <summary>
    /// SAFETY — an MT store constructed against the <em>pair</em> schema (no <c>TenantId</c> column) fails closed
    /// on first use rather than silently running tenant-blind. The leak state (an MT host on the single-tenant
    /// schema) is unreachable at runtime, not a comment. The handshake fires lazily on the first operation, so
    /// the assertion is on the observable throw regardless of whether the trigger is later moved to startup.
    /// </summary>
    [Fact]
    public async Task Fail_Closed_When_A_Multi_Tenant_Store_Runs_The_Pair_Schema()
    {
        var pairTable = await NewPairTableAsync().ConfigureAwait(false);
        var mtStore = CreateStore(pairTable, requireTenant: true, new FixedTenantContext(TenantA));

        _ = await Should.ThrowAsync<InvalidOperationException>(
            async () => await mtStore.TryMarkAsProcessedAsync("msg-mode-mismatch-mt", HandlerType, CancellationToken.None)
                .ConfigureAwait(false)).ConfigureAwait(false);
    }

    /// <summary>
    /// SAFETY — a non-MT store constructed against the <em>triple</em> schema (a <c>TenantId NOT NULL</c> column
    /// it cannot populate) fails closed on first use, rather than inserting NULLs or running against a key it
    /// does not match.
    /// </summary>
    [Fact]
    public async Task Fail_Closed_When_A_Non_Multi_Tenant_Store_Runs_The_Triple_Schema()
    {
        var tripleTable = await NewTripleTableAsync().ConfigureAwait(false);
        var nonMtStore = CreateStore(tripleTable, requireTenant: false);

        _ = await Should.ThrowAsync<InvalidOperationException>(
            async () => await nonMtStore.TryMarkAsProcessedAsync("msg-mode-mismatch-nonmt", HandlerType, CancellationToken.None)
                .ConfigureAwait(false)).ConfigureAwait(false);
    }

    // ─────────────────────────── helpers ───────────────────────────

    // Mode is RequireTenant (the only mode input); emission is driven by the fixture's actual schema (pair vs
    // triple), read at first op. tenantContext supplies the tenant VALUE for the MT isolation arms.
    private SqlServerInboxStore CreateStore(string tableName, bool requireTenant, ITenantContext? tenantContext = null)
    {
        _fixture.DockerAvailable.ShouldBeTrue(
            "SQL Server container must be available — this deployment-mode boundary lock runs against real " +
            "infrastructure and is never skipped (Infra=Required).");

        var options = new SqlServerInboxOptions
        {
            ConnectionString = _fixture.ConnectionString,
            SchemaName = SchemaName,
            TableName = tableName,
        };

        return new SqlServerInboxStore(
            () => new SqlConnection(_fixture.ConnectionString),
            options,
            NullLogger<SqlServerInboxStore>.Instance,
            tenantContext,
            Options.Create(new TenantContextOptions { RequireTenant = requireTenant }));
    }

    // Non-MT (single-tenant) shipped shape: pair PK, NO TenantId column. Mirrors 001_CreateInboxSchema.sql.
    private async Task<string> NewPairTableAsync()
    {
        var table = "inbox_pair_" + Guid.NewGuid().ToString("N");
        await ExecuteAsync(
            $"CREATE TABLE [{SchemaName}].[{table}] ({SharedColumns}, " +
            $"CONSTRAINT [PK_{table}] PRIMARY KEY (MessageId, HandlerType));").ConfigureAwait(false);
        return table;
    }

    // MT shipped shape: triple PK, TenantId NOT NULL. Mirrors 001_CreateInboxSchema.MultiTenant.sql.
    private async Task<string> NewTripleTableAsync()
    {
        var table = "inbox_triple_" + Guid.NewGuid().ToString("N");
        await ExecuteAsync(
            $"CREATE TABLE [{SchemaName}].[{table}] ({SharedColumns}, " +
            $"TenantId NVARCHAR(255) NOT NULL, " +
            $"CONSTRAINT [PK_{table}] PRIMARY KEY (MessageId, HandlerType, TenantId));").ConfigureAwait(false);
        return table;
    }

    private async Task ExecuteAsync(string sql)
    {
        await using var connection = _fixture.CreateConnection();
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = new SqlCommand(sql, connection);
        _ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// An <see cref="ITenantContext"/> resolving one fixed tenant, implemented directly against the interface so
    /// the arm binds the contract rather than an inherited convenience (<c>testing-patterns §3</c> fixture-shape).
    /// </summary>
    private sealed class FixedTenantContext(string tenantId) : ITenantContext
    {
        public string? TenantId => tenantId;

        public bool HasTenant => !string.IsNullOrWhiteSpace(tenantId);
    }
}
