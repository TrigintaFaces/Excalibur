// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Dapper;

using Excalibur.AuditLogging.SqlServer;
using Excalibur.Compliance;
using Excalibur.Testing.Conformance;

using Microsoft.Data.SqlClient;

namespace Excalibur.Dispatch.Integration.Tests.Compliance.SqlServer;

/// <summary>
/// Runs the shared audit-store conformance kit against the REAL SqlServer store.
/// </summary>
/// <remarks>
/// WHY THIS EXISTS. Until this class, exactly one type derived <see cref="AuditStoreConformanceTestKit"/>
/// — the in-memory store. Every arm in that kit, including the tenant-scoping arm on the query path,
/// therefore ran against the one implementation with no SQL in it, while the production SqlServer and
/// Postgres stores — the ones that actually build predicates and actually leaked — sat outside every
/// conformance gate.
///
/// A kit with arms and no provider consumers proves the CONTRACT and gates NOTHING. This class makes
/// the arms load-bearing for SqlServer: a regression in its query predicates now fails here rather
/// than being invisible until someone reads the SQL.
///
/// The kit's arms are inherited wholesale and each is surfaced as a [Fact] below, so adding an arm to
/// the kit does not silently skip this provider — an un-wrapped arm is a visible omission in this file
/// rather than an absence nobody can see.
/// </remarks>
[IntegrationTest]
[Collection(ContainerCollections.SqlServer)]
[Trait("Component", TestComponents.AuditLogging)]
[Trait("Infrastructure", TestInfrastructure.SqlServer)]
[Trait(TraitNames.Category, TestCategories.Integration)]
[Trait(TraitNames.Component, TestComponents.Compliance)]
public sealed class SqlServerAuditStoreConformanceTests : AuditStoreConformanceTestKit, IAsyncLifetime
{
    private readonly SqlServerFixture _fixture;

    public SqlServerAuditStoreConformanceTests(SqlServerFixture fixture) => _fixture = fixture;

    public async ValueTask InitializeAsync() => await EnsureSchemaAsync().ConfigureAwait(false);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    /// <inheritdoc />
    protected override IAuditStore CreateStore()
    {
        var options = new SqlServerAuditOptions
        {
            ConnectionString = _fixture.ConnectionString,
            SchemaName = "audit",
            TableName = "AuditEvents",
            CommandTimeoutSeconds = 30,
            Retention = { CleanupBatchSize = 100 }
        };

        return new SqlServerAuditStore(
            Microsoft.Extensions.Options.Options.Create(options),
            Microsoft.Extensions.Options.Options.Create(new SqlServerAuditAnnotationStoreOptions
            {
                ConnectionString = _fixture.ConnectionString,
                SchemaName = "audit",
                TableName = "AuditAnnotations",
            }),
            AuditIntegrityTestStrategy.Create(),
            // No ambient context: the kit's arms assert the partition an ambient-less caller resolves
            // to, which is the untenanted one. Passing null here is the honest representation of a host
            // that has not established a tenant, and it is what makes the tenancy arms meaningful —
            // handing the store a tenant would test the fixture rather than the store.
            tenantContext: null,
            EnabledTestLogger.Create<SqlServerAuditStore>());
    }

    /// <summary>
    /// An AMBIENT-RESOLVING instance of the same store, for the kit's tenant-scoped arms.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="CreateStore"/> is deliberately ambient-less, which is correct for every arm that
    /// asserts the untenanted partition — but it makes the tenant-scoped arms UNSATISFIABLE rather than
    /// failing: with no tenant context every read resolves the untenanted sentinel, so no behaviour of
    /// the store could make them pass. The kit anticipates exactly this and provides this hook; not
    /// overriding it is what left <c>GetLastEventAsync_WithTenant_ShouldReturnLastForTenant</c> red
    /// against a store whose scoping is in fact correct.
    /// </para>
    /// <para>
    /// The ambient context is a test-local double reading <see cref="TenantContextHolder"/> — the same
    /// ambient the kit's arms set with <c>BeginScope</c>. The production <c>AmbientTenantContext</c> is
    /// <see langword="internal"/> and stays that way: widening a production type's visibility to satisfy
    /// a test is the wrong direction.
    /// </para>
    /// </remarks>
    /// <returns>A store that resolves the ambient tenant.</returns>
    protected override IAuditStore CreateTenantAwareStore()
    {
        var options = new SqlServerAuditOptions
        {
            ConnectionString = _fixture.ConnectionString,
            SchemaName = "audit",
            TableName = "AuditEvents",
            CommandTimeoutSeconds = 30,
            Retention = { CleanupBatchSize = 100 }
        };

        return new SqlServerAuditStore(
            Microsoft.Extensions.Options.Options.Create(options),
            Microsoft.Extensions.Options.Options.Create(new SqlServerAuditAnnotationStoreOptions
            {
                ConnectionString = _fixture.ConnectionString,
                SchemaName = "audit",
                TableName = "AuditAnnotations",
            }),
            AuditIntegrityTestStrategy.Create(),
            tenantContext: new AmbientHolderTenantContext(),
            EnabledTestLogger.Create<SqlServerAuditStore>());
    }

    /// <summary>
    /// Resolves the tenant the kit's arms establish via <c>TenantContextHolder.BeginScope</c>.
    /// </summary>
    private sealed class AmbientHolderTenantContext : ITenantContext
    {
        public string? TenantId => TenantContextHolder.Current;

        public bool HasTenant => !string.IsNullOrEmpty(TenantContextHolder.Current);
    }

    // ---- THE ARM THIS CLASS EXISTS FOR ---------------------------------------------------------

    /// <summary>
    /// Tenant scoping on the query path, against the real SqlServer predicates.
    /// </summary>
    /// <remarks>
    /// This is the arm whose absence let the opt-in-per-call scoping ship. Against the in-memory store
    /// it demonstrates the contract is wrong; against THIS store it gates the actual predicates that
    /// leaked.
    /// </remarks>
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

    // ---- fixture ------------------------------------------------------------------------------

    /// <summary>
    /// Creates the audit schema and clears it, so each run starts from a known state.
    /// </summary>
    /// <remarks>
    /// The DDL mirrors the production audit schema. TenantId is deliberately NULLABLE here, matching
    /// the shipped schema: a fixture that made it NOT NULL would make the untenanted row unstorable
    /// and quietly remove the case the tenancy arms exist to exercise.
    /// </remarks>
    private async Task EnsureSchemaAsync()
    {
        const string createSchemaAndTableSql = """
            IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'audit')
            BEGIN
                EXEC('CREATE SCHEMA [audit]');
            END;

            IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[audit].[AuditEvents]') AND type in (N'U'))
            BEGIN
                CREATE TABLE [audit].[AuditEvents] (
                    [SequenceNumber] BIGINT IDENTITY(1,1) NOT NULL,
                    [EventId] NVARCHAR(64) NOT NULL,
                    [EventType] INT NOT NULL,
                    [Action] NVARCHAR(100) NOT NULL,
                    [Outcome] INT NOT NULL,
                    [Timestamp] DATETIMEOFFSET(7) NOT NULL,
                    [ActorId] NVARCHAR(256) NOT NULL,
                    [ActorType] NVARCHAR(50) NULL,
                    [ResourceId] NVARCHAR(256) NULL,
                    [ResourceType] NVARCHAR(100) NULL,
                    [ResourceClassification] INT NULL,
                    [TenantId] NVARCHAR(64) NULL,
                    [ApplicationName] NVARCHAR(256) NULL,
                    [CorrelationId] NVARCHAR(64) NULL,
                    [SessionId] NVARCHAR(64) NULL,
                    [IpAddress] NVARCHAR(45) NULL,
                    [UserAgent] NVARCHAR(500) NULL,
                    [Reason] NVARCHAR(1000) NULL,
                    [Metadata] NVARCHAR(MAX) NULL,
                    [PreviousEventHash] NVARCHAR(512) NULL,
                    [EventHash] NVARCHAR(512) NOT NULL,
                    CONSTRAINT [PK_AuditEvents] PRIMARY KEY CLUSTERED ([SequenceNumber] ASC),
                    CONSTRAINT [UQ_AuditEvents_EventId] UNIQUE NONCLUSTERED ([EventId])
                );
            END;

            DELETE FROM [audit].[AuditEvents];
            """;

        await using var connection = new SqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync(CancellationToken.None).ConfigureAwait(false);
        _ = await connection.ExecuteAsync(createSchemaAndTableSql).ConfigureAwait(false);
    }
}
