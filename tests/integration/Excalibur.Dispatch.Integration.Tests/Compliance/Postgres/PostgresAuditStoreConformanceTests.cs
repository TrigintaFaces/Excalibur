// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Dapper;

using Excalibur.AuditLogging.Postgres;
using Excalibur.Compliance;
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
            tenantContext: null,
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
    public Task QueryAsync_NamingAnotherTenant_ShouldNotReturnThatTenantsEvents_Test() =>
        QueryAsync_NamingAnotherTenant_ShouldNotReturnThatTenantsEvents();

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
}
