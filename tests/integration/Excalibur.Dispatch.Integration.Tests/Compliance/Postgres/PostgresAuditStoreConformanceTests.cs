// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Dapper;

using Excalibur.AuditLogging.Postgres;
using Excalibur.Compliance;
using Excalibur.Dispatch;
using Excalibur.Testing.Conformance;

using Npgsql;

namespace Excalibur.Dispatch.Integration.Tests.Compliance.Postgres;

/// <summary>
/// Runs the shared audit-store conformance kit against the REAL Postgres audit store.
/// </summary>
/// <remarks>
/// Companion to the SqlServer conformance class. Until both existed, the only type deriving
/// <see cref="AuditStoreConformanceTestKit"/> was the in-memory store, so every arm — including the
/// tenant-scoping arm on the query path — ran against the one implementation with no SQL in it.
///
/// SUBJECT: this binds <c>Excalibur.AuditLogging.Postgres.PostgresAuditStore</c>. There is a second,
/// unrelated <c>PostgresAuditStore</c> in <c>Excalibur.Data.Postgres.Audit</c>, and an existing
/// integration test binds THAT one. They share a simple name and nothing else; the opt-in tenant
/// predicates live here, so this is the class the conformance arms must gate.
/// </remarks>
[IntegrationTest]
[Collection(ContainerCollections.Postgres)]
[Trait("Component", TestComponents.AuditLogging)]
[Trait("Infrastructure", TestInfrastructure.Postgres)]
[Trait(TraitNames.Category, TestCategories.Integration)]
[Trait(TraitNames.Component, TestComponents.Compliance)]
public sealed class PostgresAuditStoreConformanceTests : AuditStoreConformanceTestKit, IAsyncLifetime
{
    private readonly PostgresFixture _fixture;

    public PostgresAuditStoreConformanceTests(PostgresFixture fixture) => _fixture = fixture;

    public async ValueTask InitializeAsync() => await EnsureSchemaAsync().ConfigureAwait(false);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    /// <inheritdoc />
    /// <summary>
    /// Ambient-resolving store for the tenant-scoped arms. CreateStore is deliberately ambient-less
    /// (see its comment), and a store with no ITenantContext resolves the untenanted sentinel for every
    /// read -- so a tenant-scoped assertion cannot hold against it. This override supplies a context
    /// that reads TenantContextHolder, which is what the tenant arms establish.
    /// </summary>
    /// <returns>An audit store that resolves the ambient tenant.</returns>
    protected override IAuditStore CreateTenantAwareStore()
    {
        var options = new PostgresAuditOptions
        {
            ConnectionString = _fixture.ConnectionString,
            SchemaName = "audit",
            TableName = "audit_events",
            CommandTimeoutSeconds = 30
        };

        return new PostgresAuditStore(
            Microsoft.Extensions.Options.Options.Create(options),
            AuditIntegrityTestStrategy.Create(),
            new AmbientAuditTenantContext(),
            EnabledTestLogger.Create<PostgresAuditStore>());
    }

    private sealed class AmbientAuditTenantContext : ITenantContext
    {
        public string? TenantId => TenantContextHolder.Current;

        public bool HasTenant => !string.IsNullOrEmpty(TenantContextHolder.Current);
    }

    /// <inheritdoc />
    /// <remarks>
    /// Raw SQL, deliberately bypassing the store, because that is what a party with database access has.
    /// Refuses silently to do nothing: a delete that removed no row would let the deletion arm pass against
    /// a store that detects nothing at all.
    /// </remarks>
    protected override async Task DeleteRecordOutOfBandAsync(
        IAuditStore store,
        string eventId,
        CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        var affected = await connection.ExecuteAsync(
            new CommandDefinition(
                "DELETE FROM audit.audit_events WHERE event_id = @EventId",
                new { EventId = eventId },
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        if (affected != 1)
        {
            throw new InvalidOperationException(
                $"Expected to delete exactly one audit row for '{eventId}', deleted {affected}.");
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// Touches only the action column. Both hash columns are left exactly as written, so the trail stays
    /// self-consistent on linkage and the arm establishes that verification recomputes from live content.
    /// </remarks>
    protected override async Task RewriteRecordActionOutOfBandAsync(
        IAuditStore store,
        string eventId,
        string newAction,
        CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        var affected = await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE audit.audit_events SET action = @NewAction WHERE event_id = @EventId",
                new { EventId = eventId, NewAction = newAction },
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        if (affected != 1)
        {
            throw new InvalidOperationException(
                $"Expected to rewrite exactly one audit row for '{eventId}', rewrote {affected}.");
        }
    }

    /// <summary>A record removed from the middle of the trail is reported, on real Postgres.</summary>
    [Fact]
    public Task VerifyChainIntegrityAsync_RecordDeletedFromMiddle_ShouldReportViolations_Test() =>
        VerifyChainIntegrityAsync_RecordDeletedFromMiddle_ShouldReportViolations();

    /// <summary>A rewritten record with intact hash columns is reported, on real Postgres.</summary>
    [Fact]
    public Task VerifyChainIntegrityAsync_RecordContentRewritten_ShouldReportViolations_Test() =>
        VerifyChainIntegrityAsync_RecordContentRewritten_ShouldReportViolations();

    /// <summary>An intact trail interleaving two tenants verifies clean, on real Postgres.</summary>
    [Fact]
    public Task VerifyChainIntegrityAsync_IntactTrailInterleavingTwoTenants_ShouldReportVerified_Test() =>
        VerifyChainIntegrityAsync_IntactTrailInterleavingTwoTenants_ShouldReportVerified();

    protected override IAuditStore CreateStore()
    {
        var options = new PostgresAuditOptions
        {
            ConnectionString = _fixture.ConnectionString,
            SchemaName = "audit",
            TableName = "audit_events",
            CommandTimeoutSeconds = 30
        };

        return new PostgresAuditStore(
            Microsoft.Extensions.Options.Options.Create(options),
            AuditIntegrityTestStrategy.Create(),
            // No ambient context — see the SqlServer companion. The arms assert the partition an
            // ambient-less caller resolves to; supplying a tenant here would test the fixture.
            tenantContext: new TestTenantContext(TenantScope.UntenantedSentinel),
            EnabledTestLogger.Create<PostgresAuditStore>());
    }

    /// <summary>
    /// Tenant scoping on the query path, against the real Postgres predicates.
    /// </summary>
    [Fact]
    public Task QueryAsync_WithoutAnExplicitTenant_ShouldNotReturnAnotherTenantsEvents_Test() =>
        QueryAsync_WithoutAnExplicitTenant_ShouldNotReturnAnotherTenantsEvents();

    [Fact]
    public Task QueryAsync_ScopedToATenant_ShouldStillReturnThatTenantsOwnEvents_Test() =>
        QueryAsync_ScopedToATenant_ShouldStillReturnThatTenantsOwnEvents();

    [Fact]
    public Task GetByIdAsync_ForAnotherTenantsEvent_ShouldNotReturnIt_Test() =>
        GetByIdAsync_ForAnotherTenantsEvent_ShouldNotReturnIt();

    [Fact]
    public Task GetLastEventAsync_WithTenant_ShouldReturnLastForTenant_Test() =>
        GetLastEventAsync_WithTenant_ShouldReturnLastForTenant();

    [Fact]
    public Task QueryAsync_ByDateRange_ShouldReturnMatching_Test() =>
        QueryAsync_ByDateRange_ShouldReturnMatching();

    [Fact]
    public Task QueryAsync_ByEventType_ShouldFilter_Test() =>
        QueryAsync_ByEventType_ShouldFilter();

    /// <summary>
    /// Creates the audit schema this store writes to, and clears it.
    /// </summary>
    /// <remarks>
    /// The column set mirrors the store's own INSERT list verbatim, because the package ships no DDL
    /// for a consumer to apply — the write statement is the only authority on the schema it needs.
    /// tenant_id is NULLABLE, matching what the store writes: a fixture that made it NOT NULL would
    /// make the untenanted row unstorable and silently delete the case the tenancy arms exercise.
    /// </remarks>
    private async Task EnsureSchemaAsync()
    {
        const string createSchemaAndTableSql = """
            CREATE SCHEMA IF NOT EXISTS audit;

            CREATE TABLE IF NOT EXISTS audit.audit_events (
                sequence_number         BIGSERIAL PRIMARY KEY,
                event_id                VARCHAR(64)  NOT NULL UNIQUE,
                event_type              INT          NOT NULL,
                action                  VARCHAR(100) NOT NULL,
                outcome                 INT          NOT NULL,
                timestamp               TIMESTAMPTZ  NOT NULL,
                actor_id                VARCHAR(256) NOT NULL,
                actor_type              VARCHAR(50),
                resource_id             VARCHAR(256),
                resource_type           VARCHAR(100),
                resource_classification INT,
                tenant_id               VARCHAR(64),
                application_name        VARCHAR(256),
                correlation_id          VARCHAR(64),
                session_id              VARCHAR(64),
                ip_address              VARCHAR(45),
                user_agent              VARCHAR(500),
                reason                  VARCHAR(1000),
                metadata                JSONB,
                previous_event_hash     VARCHAR(512),
                event_hash              VARCHAR(512) NOT NULL
            );

            TRUNCATE TABLE audit.audit_events RESTART IDENTITY;
            """;

        await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync(CancellationToken.None).ConfigureAwait(false);
        _ = await connection.ExecuteAsync(createSchemaAndTableSql).ConfigureAwait(false);
    }

    #region Arms wired for real-infrastructure coverage

    // These arms are declared by the kit and were surfaced by no wrapper here, so they never ran
    // against real Postgres. The class doc above already promised the opposite; a promise in prose does
    // not survive an edit, so ConformanceSuite_ShouldWireEveryArm now enforces it mechanically.

    /// <summary>Kit arm <c>StoreAsync_ShouldPersistEvent</c>, exercised against real Postgres.</summary>
    [Fact]
    public Task StoreAsync_ShouldPersistEvent_Test() =>
        StoreAsync_ShouldPersistEvent();

    /// <summary>Kit arm <c>StoreAsync_WithNullEvent_ShouldThrow</c>, exercised against real Postgres.</summary>
    [Fact]
    public Task StoreAsync_WithNullEvent_ShouldThrow_Test() =>
        StoreAsync_WithNullEvent_ShouldThrow();

    /// <summary>Kit arm <c>StoreAsync_DuplicateId_ShouldThrowInvalidOperationException</c>, exercised against real Postgres.</summary>
    [Fact]
    public Task StoreAsync_DuplicateId_ShouldThrowInvalidOperationException_Test() =>
        StoreAsync_DuplicateId_ShouldThrowInvalidOperationException();

    /// <summary>Kit arm <c>GetByIdAsync_ExistingEvent_ShouldReturnEvent</c>, exercised against real Postgres.</summary>
    [Fact]
    public Task GetByIdAsync_ExistingEvent_ShouldReturnEvent_Test() =>
        GetByIdAsync_ExistingEvent_ShouldReturnEvent();

    /// <summary>Kit arm <c>GetByIdAsync_NonExistent_ShouldReturnNull</c>, exercised against real Postgres.</summary>
    [Fact]
    public Task GetByIdAsync_NonExistent_ShouldReturnNull_Test() =>
        GetByIdAsync_NonExistent_ShouldReturnNull();

    /// <summary>Kit arm <c>GetByIdAsync_NullOrEmpty_ShouldThrow</c>, exercised against real Postgres.</summary>
    [Fact]
    public Task GetByIdAsync_NullOrEmpty_ShouldThrow_Test() =>
        GetByIdAsync_NullOrEmpty_ShouldThrow();

    /// <summary>Kit arm <c>QueryAsync_ByActorId_ShouldFilter</c>, exercised against real Postgres.</summary>
    [Fact]
    public Task QueryAsync_ByActorId_ShouldFilter_Test() =>
        QueryAsync_ByActorId_ShouldFilter();

    /// <summary>Kit arm <c>QueryAsync_Pagination_ShouldRespectSkipAndMaxResults</c>, exercised against real Postgres.</summary>
    [Fact]
    public Task QueryAsync_Pagination_ShouldRespectSkipAndMaxResults_Test() =>
        QueryAsync_Pagination_ShouldRespectSkipAndMaxResults();

    /// <summary>Kit arm <c>CountAsync_WithFilters_ShouldReturnCount</c>, exercised against real Postgres.</summary>
    [Fact]
    public Task CountAsync_WithFilters_ShouldReturnCount_Test() =>
        CountAsync_WithFilters_ShouldReturnCount();

    /// <summary>Kit arm <c>CountAsync_EmptyResult_ShouldReturnZero</c>, exercised against real Postgres.</summary>
    [Fact]
    public Task CountAsync_EmptyResult_ShouldReturnZero_Test() =>
        CountAsync_EmptyResult_ShouldReturnZero();

    /// <summary>Kit arm <c>VerifyChainIntegrityAsync_ValidChain_ShouldReportVerified</c>, exercised against real Postgres.</summary>
    [Fact]
    public Task VerifyChainIntegrityAsync_ValidChain_ShouldReportVerified_Test() =>
        VerifyChainIntegrityAsync_ValidChain_ShouldReportVerified();

    /// <summary>Kit arm <c>VerifyChainIntegrityAsync_EmptyRange_ShouldReportNoEventsInScope</c>, exercised against real Postgres.</summary>
    [Fact]
    public Task VerifyChainIntegrityAsync_EmptyRange_ShouldReportNoEventsInScope_Test() =>
        VerifyChainIntegrityAsync_EmptyRange_ShouldReportNoEventsInScope();

    /// <summary>Kit arm <c>GetLastEventAsync_DefaultTenant_ShouldReturnLast</c>, exercised against real Postgres.</summary>
    [Fact]
    public Task GetLastEventAsync_DefaultTenant_ShouldReturnLast_Test() =>
        GetLastEventAsync_DefaultTenant_ShouldReturnLast();

    /// <summary>Kit arm <c>StoreAsync_ShouldSetPreviousEventHash</c>, exercised against real Postgres.</summary>
    [Fact]
    public Task StoreAsync_ShouldSetPreviousEventHash_Test() =>
        StoreAsync_ShouldSetPreviousEventHash();

    /// <summary>Kit arm <c>StoreAsync_ShouldComputeEventHash</c>, exercised against real Postgres.</summary>
    [Fact]
    public Task StoreAsync_ShouldComputeEventHash_Test() =>
        StoreAsync_ShouldComputeEventHash();

    /// <summary>Kit arm <c>StoreAsync_WithApplicationName_ShouldPersistApplicationName</c>, exercised against real Postgres.</summary>
    [Fact]
    public Task StoreAsync_WithApplicationName_ShouldPersistApplicationName_Test() =>
        StoreAsync_WithApplicationName_ShouldPersistApplicationName();

    /// <summary>Kit arm <c>StoreAsync_WithNullApplicationName_ShouldPersistNull</c>, exercised against real Postgres.</summary>
    [Fact]
    public Task StoreAsync_WithNullApplicationName_ShouldPersistNull_Test() =>
        StoreAsync_WithNullApplicationName_ShouldPersistNull();

    /// <summary>Kit arm <c>QueryAsync_ByApplicationName_ShouldFilter</c>, exercised against real Postgres.</summary>
    [Fact]
    public Task QueryAsync_ByApplicationName_ShouldFilter_Test() =>
        QueryAsync_ByApplicationName_ShouldFilter();

    /// <summary>Kit arm <c>CountAsync_ByApplicationName_ShouldCount</c>, exercised against real Postgres.</summary>
    [Fact]
    public Task CountAsync_ByApplicationName_ShouldCount_Test() =>
        CountAsync_ByApplicationName_ShouldCount();

    /// <summary>Kit arm <c>StoreAsync_DifferentApplicationName_ShouldProduceDifferentHash</c>, exercised against real Postgres.</summary>
    [Fact]
    public Task StoreAsync_DifferentApplicationName_ShouldProduceDifferentHash_Test() =>
        StoreAsync_DifferentApplicationName_ShouldProduceDifferentHash();

    /// <summary>Every arm this kit declares is surfaced above; an omission fails by name.</summary>
    [Fact]
    public Task ConformanceSuite_ShouldWireEveryArm_Test() =>
        ConformanceSuite_ShouldWireEveryArm();

    #endregion
}
