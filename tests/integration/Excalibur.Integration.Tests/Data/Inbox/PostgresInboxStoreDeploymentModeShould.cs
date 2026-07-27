// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Inbox;
using Excalibur.Inbox.Postgres;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Npgsql;

using Shouldly;

using Xunit;

#pragma warning disable CA2100 // SQL strings use compile-time-const columns + Guid-generated table names in a test fixture.

namespace Excalibur.Integration.Tests.Data.Inbox;

/// <summary>
/// Real-Postgres lock for the Design-A <b>column-agnostic-per-deployment</b> tenant contract on
/// <see cref="PostgresInboxStore"/> (guide §RULING 2A): the MULTI-TENANT isolation dimension plus the
/// deployment-mode ↔ physical-schema fail-closed handshake.
/// </summary>
/// <remarks>
/// <para>
/// The NON-MT (single-tenant) pair-key dimension — a non-MT claim degrades <c>ON CONFLICT</c> to the pair
/// target and deduplicates — is already owned by <see cref="PostgresInboxStoreNonMultiTenantConflictTargetShould"/>
/// (the S887 lock, still valid under Design A), so this lock does not repeat it (<c>karpathy</c> — every arm
/// traces to a distinct property). It adds the two dimensions that lock has no coverage of: cross-tenant
/// isolation under the triple key, and the mode↔schema handshake.
/// </para>
/// <para>
/// Isolation is asserted through the store's <em>public</em> claim API rather than a mechanism: tenant B's
/// first claim on the <em>same</em> <c>(message_id, handler_type)</c> must SUCCEED, because under the triple
/// key it lands in B's own partition — it would return <see langword="false"/> (deduplicated against A's row)
/// only if the tenant term were dropped from the <c>ON CONFLICT</c> target. That success is the safety signal,
/// and B's second claim deduplicating is its liveness partner (<c>testing-patterns §3</c>). Fixtures mirror the
/// shipped <c>001_CreateInboxSchema[.MultiTenant].sql</c> columns on per-test isolated tables (<c>f5</c>). Real
/// infrastructure is a hard requirement — never skipped (<c>Infra=Required</c>).
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Inbox")]
[Trait("Database", "Postgres")]
[Trait("Infra", "Required")]
public sealed class PostgresInboxStoreDeploymentModeShould : IClassFixture<PostgresInboxStoreContainerFixture>
{
    private const string SchemaName = "public";
    private const string HandlerType = "TestHandler";
    private const string TenantA = "tenant-A";
    private const string TenantB = "tenant-B";

    // Shipped column set shared by both schemas (mirrors 001_CreateInboxSchema[.MultiTenant].sql; the triple
    // schema adds tenant_id NOT NULL). Kept in lockstep with the shipped DDL — see the F-5 sweep.
    private const string SharedColumns =
        "message_id TEXT NOT NULL, handler_type TEXT NOT NULL, message_type TEXT NOT NULL, " +
        "payload BYTEA NOT NULL, metadata JSONB NOT NULL DEFAULT '{}'::jsonb, " +
        "received_at TIMESTAMPTZ NOT NULL, processed_at TIMESTAMPTZ NULL, status INT NOT NULL DEFAULT 0, " +
        "retry_count INT NOT NULL DEFAULT 0, last_error TEXT NULL, last_attempt_at TIMESTAMPTZ NULL, " +
        "lease_expires_at TIMESTAMPTZ NULL, correlation_id TEXT NULL, source TEXT NULL";

    private readonly PostgresInboxStoreContainerFixture _fixture;

    public PostgresInboxStoreDeploymentModeShould(PostgresInboxStoreContainerFixture fixture) =>
        _fixture = fixture;

    // ─────────────────────────── MT isolation (triple schema) ───────────────────────────

    /// <summary>
    /// SAFETY + LIVENESS — under multi-tenancy, tenant B's claim on the same message id lands in B's own
    /// partition (SUCCEEDS) rather than deduplicating against tenant A's row, and B's own duplicate still dedups.
    /// A dropped tenant term in the <c>ON CONFLICT</c> target would make B's first claim return
    /// <see langword="false"/> — this arm reddens exactly there.
    /// </summary>
    [Fact]
    public async Task Isolate_Claims_By_Tenant_On_The_Triple_Schema()
    {
        var table = await NewTripleTableAsync().ConfigureAwait(false);
        var storeA = CreateStore(table, requireTenant: true, new FixedTenantContext(TenantA));
        var storeB = CreateStore(table, requireTenant: true, new FixedTenantContext(TenantB));
        const string messageId = "msg-shared-id";

        var aFirst = await storeA.TryMarkAsProcessedAsync(messageId, HandlerType, CancellationToken.None).ConfigureAwait(false);
        aFirst.ShouldBeTrue("tenant A wins its first claim (LIVENESS).");

        var bFirst = await storeB.TryMarkAsProcessedAsync(messageId, HandlerType, CancellationToken.None).ConfigureAwait(false);
        bFirst.ShouldBeTrue(
            "tenant B's first claim on the SAME (message_id, handler_type) must SUCCEED — under the triple key it " +
            "is a distinct row in B's partition. A false here means the ON CONFLICT target dropped tenant_id and B " +
            "deduplicated against tenant A's row (cross-tenant loss) — fix the store's conflict target, not this line.");

        var bSecond = await storeB.TryMarkAsProcessedAsync(messageId, HandlerType, CancellationToken.None).ConfigureAwait(false);
        bSecond.ShouldBeFalse("tenant B's genuine duplicate must deduplicate within its own partition (isolation must not disable dedup).");
    }

    // ─────────────────────── Startup handshake (fail-closed) ───────────────────────

    /// <summary>
    /// SAFETY — a multi-tenant store constructed against the <em>pair</em> schema (no <c>tenant_id</c> column)
    /// fails closed on first use, rather than running tenant-blind. The check fires lazily on first connection
    /// open, so the assertion is on the observable throw.
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
    /// SAFETY — a non-MT store constructed against the <em>triple</em> schema (a <c>tenant_id NOT NULL</c> column
    /// it cannot populate) fails closed on first use.
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
    // triple), read at first op. tenantContext supplies the tenant VALUE for the MT isolation arm.
    private PostgresInboxStore CreateStore(string tableName, bool requireTenant, ITenantContext? tenantContext = null)
    {
        _fixture.DockerAvailable.ShouldBeTrue(
            "Postgres container must be available — this deployment-mode boundary lock runs against real " +
            "infrastructure and is never skipped (Infra=Required).");

        var options = new PostgresInboxOptions
        {
            ConnectionString = _fixture.ConnectionString,
            SchemaName = SchemaName,
            TableName = tableName,
        };

        return new PostgresInboxStore(
            () => new NpgsqlConnection(_fixture.ConnectionString),
            options,
            NullLogger<PostgresInboxStore>.Instance,
            tenantContext,
            Options.Create(new TenantContextOptions { RequireTenant = requireTenant }));
    }

    // Non-MT (single-tenant) shipped shape: pair PK, NO tenant_id column. Mirrors 001_CreateInboxSchema.sql.
    private async Task<string> NewPairTableAsync()
    {
        var table = "inbox_pair_" + Guid.NewGuid().ToString("N");
        await ExecuteAsync(
            $"CREATE TABLE {SchemaName}.{table} ({SharedColumns}, " +
            $"CONSTRAINT pk_{table} PRIMARY KEY (message_id, handler_type));").ConfigureAwait(false);
        return table;
    }

    // MT shipped shape: triple PK, tenant_id NOT NULL. Mirrors 001_CreateInboxSchema.MultiTenant.sql.
    private async Task<string> NewTripleTableAsync()
    {
        var table = "inbox_triple_" + Guid.NewGuid().ToString("N");
        await ExecuteAsync(
            $"CREATE TABLE {SchemaName}.{table} ({SharedColumns}, " +
            $"tenant_id TEXT NOT NULL, " +
            $"CONSTRAINT pk_{table} PRIMARY KEY (message_id, handler_type, tenant_id));").ConfigureAwait(false);
        return table;
    }

    private async Task ExecuteAsync(string sql)
    {
        await using var connection = _fixture.CreateConnection();
        await connection.OpenAsync().ConfigureAwait(false);
        await using var command = new NpgsqlCommand(sql, connection);
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
