// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
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
        await using var connection = new SqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        var affected = await connection.ExecuteAsync(
            new CommandDefinition(
                "DELETE FROM audit.AuditEvents WHERE EventId = @EventId",
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
        await using var connection = new SqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        var affected = await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE audit.AuditEvents SET [Action] = @NewAction WHERE EventId = @EventId",
                new { EventId = eventId, NewAction = newAction },
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        if (affected != 1)
        {
            throw new InvalidOperationException(
                $"Expected to rewrite exactly one audit row for '{eventId}', rewrote {affected}.");
        }
    }

    /// <summary>A record removed from the middle of the trail is reported, on real SQL Server.</summary>
    [Fact]
    public Task VerifyChainIntegrityAsync_RecordDeletedFromMiddle_ShouldReportViolations_Test() =>
        VerifyChainIntegrityAsync_RecordDeletedFromMiddle_ShouldReportViolations();

    /// <summary>A rewritten record with intact hash columns is reported, on real SQL Server.</summary>
    [Fact]
    public Task VerifyChainIntegrityAsync_RecordContentRewritten_ShouldReportViolations_Test() =>
        VerifyChainIntegrityAsync_RecordContentRewritten_ShouldReportViolations();

    /// <summary>An intact trail interleaving two tenants verifies clean, on real SQL Server.</summary>
    [Fact]
    public Task VerifyChainIntegrityAsync_IntactTrailInterleavingTwoTenants_ShouldReportVerified_Test() =>
        VerifyChainIntegrityAsync_IntactTrailInterleavingTwoTenants_ShouldReportVerified();

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
            tenantContext: new TestTenantContext(TenantScope.UntenantedSentinel),
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

    #region Arms wired for real-infrastructure coverage

    // These arms are declared by the kit and were surfaced by no wrapper here, so they never ran
    // against real SQL Server. The class doc above already promised the opposite; a promise in prose does
    // not survive an edit, so ConformanceSuite_ShouldWireEveryArm now enforces it mechanically.

    /// <summary>Kit arm <c>StoreAsync_ShouldPersistEvent</c>, exercised against real SQL Server.</summary>
    [Fact]
    public Task StoreAsync_ShouldPersistEvent_Test() =>
        StoreAsync_ShouldPersistEvent();

    /// <summary>Kit arm <c>StoreAsync_WithNullEvent_ShouldThrow</c>, exercised against real SQL Server.</summary>
    [Fact]
    public Task StoreAsync_WithNullEvent_ShouldThrow_Test() =>
        StoreAsync_WithNullEvent_ShouldThrow();

    /// <summary>Kit arm <c>StoreAsync_DuplicateId_ShouldThrowInvalidOperationException</c>, exercised against real SQL Server.</summary>
    [Fact]
    public Task StoreAsync_DuplicateId_ShouldThrowInvalidOperationException_Test() =>
        StoreAsync_DuplicateId_ShouldThrowInvalidOperationException();

    /// <summary>Kit arm <c>GetByIdAsync_ExistingEvent_ShouldReturnEvent</c>, exercised against real SQL Server.</summary>
    [Fact]
    public Task GetByIdAsync_ExistingEvent_ShouldReturnEvent_Test() =>
        GetByIdAsync_ExistingEvent_ShouldReturnEvent();

    /// <summary>Kit arm <c>GetByIdAsync_NonExistent_ShouldReturnNull</c>, exercised against real SQL Server.</summary>
    [Fact]
    public Task GetByIdAsync_NonExistent_ShouldReturnNull_Test() =>
        GetByIdAsync_NonExistent_ShouldReturnNull();

    /// <summary>Kit arm <c>GetByIdAsync_NullOrEmpty_ShouldThrow</c>, exercised against real SQL Server.</summary>
    [Fact]
    public Task GetByIdAsync_NullOrEmpty_ShouldThrow_Test() =>
        GetByIdAsync_NullOrEmpty_ShouldThrow();

    /// <summary>Kit arm <c>QueryAsync_ByActorId_ShouldFilter</c>, exercised against real SQL Server.</summary>
    [Fact]
    public Task QueryAsync_ByActorId_ShouldFilter_Test() =>
        QueryAsync_ByActorId_ShouldFilter();

    /// <summary>Kit arm <c>QueryAsync_Pagination_ShouldRespectSkipAndMaxResults</c>, exercised against real SQL Server.</summary>
    [Fact]
    public Task QueryAsync_Pagination_ShouldRespectSkipAndMaxResults_Test() =>
        QueryAsync_Pagination_ShouldRespectSkipAndMaxResults();

    /// <summary>Kit arm <c>CountAsync_WithFilters_ShouldReturnCount</c>, exercised against real SQL Server.</summary>
    [Fact]
    public Task CountAsync_WithFilters_ShouldReturnCount_Test() =>
        CountAsync_WithFilters_ShouldReturnCount();

    /// <summary>Kit arm <c>CountAsync_EmptyResult_ShouldReturnZero</c>, exercised against real SQL Server.</summary>
    [Fact]
    public Task CountAsync_EmptyResult_ShouldReturnZero_Test() =>
        CountAsync_EmptyResult_ShouldReturnZero();

    /// <summary>Kit arm <c>VerifyChainIntegrityAsync_ValidChain_ShouldReportVerified</c>, exercised against real SQL Server.</summary>
    [Fact]
    public Task VerifyChainIntegrityAsync_ValidChain_ShouldReportVerified_Test() =>
        VerifyChainIntegrityAsync_ValidChain_ShouldReportVerified();

    /// <summary>Kit arm <c>VerifyChainIntegrityAsync_EmptyRange_ShouldReportNoEventsInScope</c>, exercised against real SQL Server.</summary>
    [Fact]
    public Task VerifyChainIntegrityAsync_EmptyRange_ShouldReportNoEventsInScope_Test() =>
        VerifyChainIntegrityAsync_EmptyRange_ShouldReportNoEventsInScope();

    /// <summary>Kit arm <c>GetLastEventAsync_DefaultTenant_ShouldReturnLast</c>, exercised against real SQL Server.</summary>
    [Fact]
    public Task GetLastEventAsync_DefaultTenant_ShouldReturnLast_Test() =>
        GetLastEventAsync_DefaultTenant_ShouldReturnLast();

    /// <summary>Kit arm <c>StoreAsync_ShouldSetPreviousEventHash</c>, exercised against real SQL Server.</summary>
    [Fact]
    public Task StoreAsync_ShouldSetPreviousEventHash_Test() =>
        StoreAsync_ShouldSetPreviousEventHash();

    /// <summary>Kit arm <c>StoreAsync_ShouldComputeEventHash</c>, exercised against real SQL Server.</summary>
    [Fact]
    public Task StoreAsync_ShouldComputeEventHash_Test() =>
        StoreAsync_ShouldComputeEventHash();

    /// <summary>Kit arm <c>StoreAsync_WithApplicationName_ShouldPersistApplicationName</c>, exercised against real SQL Server.</summary>
    [Fact]
    public Task StoreAsync_WithApplicationName_ShouldPersistApplicationName_Test() =>
        StoreAsync_WithApplicationName_ShouldPersistApplicationName();

    /// <summary>Kit arm <c>StoreAsync_WithNullApplicationName_ShouldPersistNull</c>, exercised against real SQL Server.</summary>
    [Fact]
    public Task StoreAsync_WithNullApplicationName_ShouldPersistNull_Test() =>
        StoreAsync_WithNullApplicationName_ShouldPersistNull();

    /// <summary>Kit arm <c>QueryAsync_ByApplicationName_ShouldFilter</c>, exercised against real SQL Server.</summary>
    [Fact]
    public Task QueryAsync_ByApplicationName_ShouldFilter_Test() =>
        QueryAsync_ByApplicationName_ShouldFilter();

    /// <summary>Kit arm <c>CountAsync_ByApplicationName_ShouldCount</c>, exercised against real SQL Server.</summary>
    [Fact]
    public Task CountAsync_ByApplicationName_ShouldCount_Test() =>
        CountAsync_ByApplicationName_ShouldCount();

    /// <summary>Kit arm <c>StoreAsync_DifferentApplicationName_ShouldProduceDifferentHash</c>, exercised against real SQL Server.</summary>
    [Fact]
    public Task StoreAsync_DifferentApplicationName_ShouldProduceDifferentHash_Test() =>
        StoreAsync_DifferentApplicationName_ShouldProduceDifferentHash();

    /// <summary>Every arm this kit declares is surfaced above; an omission fails by name.</summary>
    [Fact]
    public Task ConformanceSuite_ShouldWireEveryArm_Test() =>
        ConformanceSuite_ShouldWireEveryArm();

    #endregion
}
