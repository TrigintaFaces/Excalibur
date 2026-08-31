// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Inbox.Oracle;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Oracle.ManagedDataAccess.Client;

using Shouldly;

using Xunit;

#pragma warning disable CA2100 // SQL uses compile-time-const columns + fixed table names in a test fixture.

namespace Excalibur.Inbox.Oracle.Tests;

/// <summary>
/// Real-Oracle lock for the Design-A column-agnostic-per-deployment tenant contract on
/// <see cref="OracleInboxStore"/> (guide §RULING 2A): the single-tenant NON-MT read/claim path on the pair
/// schema plus the deployment-mode ↔ physical-schema fail-closed handshake. The multi-tenant isolation
/// dimension is owned by <see cref="OracleInboxStoreTenantIsolationShould"/> (not repeated here — <c>karpathy</c>).
/// </summary>
/// <remarks>
/// The non-MT read arm is the RED-detection lock for the SELECT-list emission gap: a read path that projects
/// the tenant column unconditionally throws <c>ORA-00904 (invalid identifier)</c> on the no-column default
/// schema. Real infrastructure is a hard requirement — never skipped (<c>Infra=Required</c>).
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Inbox")]
[Trait("Database", "Oracle")]
[Trait("Infra", "Required")]
[Collection(OracleInboxTestCollection.CollectionName)]
public sealed class OracleInboxStoreDeploymentModeShould
{
    private const string HandlerType = "TestHandler";
    private const string TenantA = "tenant-A";
    private const string PairTable = "INBOX_DM_PAIR";
    private const string TripleTable = "INBOX_DM_TRIPLE";

    // Mirrors the shipped INBOX_MESSAGES column set (the triple schema adds TenantId NOT NULL). F-5 backstop.
    private const string SharedColumns =
        "MessageId VARCHAR2(255) NOT NULL, HandlerType VARCHAR2(500) NOT NULL, MessageType VARCHAR2(500), " +
        "Payload BLOB, Metadata CLOB, ReceivedAt TIMESTAMP(7) WITH TIME ZONE NOT NULL, " +
        "ProcessedAt TIMESTAMP(7) WITH TIME ZONE, Status NUMBER(10) DEFAULT 0 NOT NULL, LastError VARCHAR2(4000), " +
        "RetryCount NUMBER(10) DEFAULT 0 NOT NULL, LastAttemptAt TIMESTAMP(7) WITH TIME ZONE, " +
        "NextAttemptAt TIMESTAMP(7) WITH TIME ZONE, LeaseExpiresAtUtc TIMESTAMP(7) WITH TIME ZONE, " +
        "CorrelationId VARCHAR2(255), Source VARCHAR2(255)";

    private readonly OracleInboxStoreContainerFixture _fixture;

    public OracleInboxStoreDeploymentModeShould(OracleInboxStoreContainerFixture fixture) => _fixture = fixture;

    // ─────────────── NON-MT (pair schema) ───────────────

    /// <summary>
    /// LIVENESS for the non-MT READ path — a single-tenant store reads back its own entry on the pair schema.
    /// RED-detects the SELECT-list emission gap (a projection naming the tenant column on the no-column schema).
    /// </summary>
    [Fact]
    public async Task Read_Back_A_Non_Multi_Tenant_Entry_On_The_Pair_Schema()
    {
        await EnsureTableAsync(PairTable, multiTenant: false).ConfigureAwait(false);
        var store = CreateStore(PairTable, requireTenant: false);
        const string messageId = "msg-nonmt-read";

        _ = await store.CreateEntryAsync(
            messageId, HandlerType, "TestMessage", [1, 2, 3],
            new Dictionary<string, object>(), CancellationToken.None).ConfigureAwait(false);

        var entry = await store.GetEntryAsync(messageId, HandlerType, CancellationToken.None).ConfigureAwait(false);
        entry.ShouldNotBeNull(
            "a single-tenant store must read back its own entry on the pair schema — the read path must not " +
            "project a tenant column the non-MT schema does not have.");
    }

    /// <summary>LIVENESS — a single-tenant store claims and deduplicates on the pair schema.</summary>
    [Fact]
    public async Task Claim_And_Dedup_A_Non_Multi_Tenant_Message_On_The_Pair_Schema()
    {
        await EnsureTableAsync(PairTable, multiTenant: false).ConfigureAwait(false);
        var store = CreateStore(PairTable, requireTenant: false);
        const string messageId = "msg-nonmt-claim";

        var first = await store.TryMarkAsProcessedAsync(messageId, HandlerType, CancellationToken.None).ConfigureAwait(false);
        var second = await store.TryMarkAsProcessedAsync(messageId, HandlerType, CancellationToken.None).ConfigureAwait(false);

        first.ShouldBeTrue("a single-tenant store must win the first claim on the pair schema.");
        second.ShouldBeFalse("the second claim must deduplicate on the pair key.");
    }

    // ─────────────── Startup handshake (fail-closed) ───────────────

    /// <summary>SAFETY — a multi-tenant store on the pair schema fails closed on first use.</summary>
    [Fact]
    public async Task Fail_Closed_When_A_Multi_Tenant_Store_Runs_The_Pair_Schema()
    {
        await EnsureTableAsync(PairTable, multiTenant: false).ConfigureAwait(false);
        var store = CreateStore(PairTable, requireTenant: true, TenantA);

        _ = await Should.ThrowAsync<InvalidOperationException>(
            async () => await store.TryMarkAsProcessedAsync("msg-mismatch-mt", HandlerType, CancellationToken.None)
                .ConfigureAwait(false)).ConfigureAwait(false);
    }

    /// <summary>SAFETY — a non-MT store on the triple schema fails closed on first use.</summary>
    [Fact]
    public async Task Fail_Closed_When_A_Non_Multi_Tenant_Store_Runs_The_Triple_Schema()
    {
        await EnsureTableAsync(TripleTable, multiTenant: true).ConfigureAwait(false);
        var store = CreateStore(TripleTable, requireTenant: false);

        _ = await Should.ThrowAsync<InvalidOperationException>(
            async () => await store.TryMarkAsProcessedAsync("msg-mismatch-nonmt", HandlerType, CancellationToken.None)
                .ConfigureAwait(false)).ConfigureAwait(false);
    }

    // ─────────────── helpers ───────────────

    private OracleInboxStore CreateStore(string tableName, bool requireTenant, string? tenantId = null)
    {
        _fixture.DockerAvailable.ShouldBeTrue(
            "Oracle container must be available — this deployment-mode boundary lock runs against real " +
            "infrastructure and is never skipped (Infra=Required).");

        var options = Options.Create(new OracleInboxOptions
        {
            ConnectionString = _fixture.ConnectionString,
            SchemaName = _fixture.SchemaName,
            TableName = tableName,
        });

        return new OracleInboxStore(
            options,
            NullLogger<OracleInboxStore>.Instance,
            // A non-multi-tenant deployment: the reserved untenanted term, which is exactly what this store
            // resolved when no context was registered at all. Byte-identical to the state these arms model.
            tenantId is null
                ? new FixedTenantContext(TenantScope.UntenantedSentinel)
                : new FixedTenantContext(tenantId),
            Options.Create(new TenantContextOptions { RequireTenant = requireTenant }));
    }

    private async Task EnsureTableAsync(string table, bool multiTenant)
    {
        await using var connection = _fixture.CreateConnection();
        await connection.OpenAsync().ConfigureAwait(false);

        // Oracle has no DROP TABLE IF EXISTS — swallow ORA-00942 (table or view does not exist).
        try
        {
            await using var drop = connection.CreateCommand();
            drop.CommandText = $"DROP TABLE {table} CASCADE CONSTRAINTS";
            _ = await drop.ExecuteNonQueryAsync().ConfigureAwait(false);
        }
        catch (OracleException ex) when (ex.Number == 942)
        {
            // nothing to drop.
        }

        var tenantColumn = multiTenant ? ", TenantId VARCHAR2(255) NOT NULL" : string.Empty;
        var key = multiTenant ? "(MessageId, HandlerType, TenantId)" : "(MessageId, HandlerType)";

        await using var create = connection.CreateCommand();
        create.CommandText =
            $"CREATE TABLE {table} ({SharedColumns}{tenantColumn}, CONSTRAINT PK_{table} PRIMARY KEY {key})";
        _ = await create.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    private sealed class FixedTenantContext(string tenantId) : ITenantContext
    {
        public string? TenantId => tenantId;

        public bool HasTenant => !string.IsNullOrWhiteSpace(tenantId);
    }
}
