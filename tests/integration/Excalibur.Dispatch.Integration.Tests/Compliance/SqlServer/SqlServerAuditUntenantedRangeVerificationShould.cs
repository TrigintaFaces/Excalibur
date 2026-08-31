// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Dapper;

using Excalibur.AuditLogging.SqlServer;
using Excalibur.Compliance;

using Microsoft.Data.SqlClient;

namespace Excalibur.Dispatch.Integration.Tests.Compliance.SqlServer;

/// <summary>
/// Verification over a window that does not begin at the trail's first record, against REAL SQL Server.
/// </summary>
/// <remarks>
/// <para>
/// WHY THIS EXISTS. Verifying a date range means matching two reads of the same table: the records inside
/// the window, and the tag of the record immediately preceding it. They are matched per chain partition,
/// by key — and the two keys were derived separately. The record path mapped the stored tenant back to the
/// value that was signed, so an untenanted record keyed on null; the anchor path read the column raw, so
/// its anchor keyed on the reserved sentinel. Every untenanted lookup therefore missed, the anchor was
/// read as absent, and the verifier was told to assert that the first record in the window was the
/// partition's genesis record.
/// </para>
/// <para>
/// It was not, so verification reported the trail as broken. Not a stale count or a missing row — a
/// compliance artifact reporting removal, insertion, or reordering against a trail nobody had touched,
/// which an auditor cannot tell apart from the real thing.
/// </para>
/// <para>
/// These arms are deliberately paired. Reporting an untouched trail clean is only correct if the check is
/// still capable of reporting a touched one; a fix that quieted the false accusation by weakening the
/// check would trade a false alarm for a silent hole, which in a tamper-evidence control is strictly
/// worse. So the untenanted arms sit next to a tampered arm that must still fail, and a tenanted arm that
/// must keep passing.
/// </para>
/// </remarks>
[IntegrationTest]
[Collection(ContainerCollections.SqlServer)]
[Trait("Component", TestComponents.AuditLogging)]
[Trait("Infrastructure", TestInfrastructure.SqlServer)]
[Trait(TraitNames.Category, TestCategories.Integration)]
[Trait(TraitNames.Component, TestComponents.Compliance)]
public sealed class SqlServerAuditUntenantedRangeVerificationShould : IAsyncLifetime
{
    /// <summary>Fixed rather than relative to now: the window boundaries are the subject of the test.</summary>
    private static readonly DateTimeOffset TrailStart = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly SqlServerFixture _fixture;

    public SqlServerAuditUntenantedRangeVerificationShould(SqlServerFixture fixture) => _fixture = fixture;

    public async ValueTask InitializeAsync()
    {
        // Never skip-gated. A range-verification arm that quietly does not run is how the false accusation
        // reached a shipped compliance surface in the first place.
        _fixture.DockerAvailable.ShouldBeTrue(
            "SQL Server container must be available — audit range verification is never skipped.");

        await EnsureSchemaAsync().ConfigureAwait(false);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    /// <summary>
    /// THE ARM THIS CLASS EXISTS FOR. An untouched untenanted trail, verified over a window that starts
    /// after its first record, is clean.
    /// </summary>
    [Fact]
    public async Task ReportAnUntouchedUntenantedTrailVerified_WhenTheWindowDoesNotBeginAtGenesis()
    {
        var store = CreateStore();
        _ = await StoreTrailAsync(store, tenantId: null, count: 5).ConfigureAwait(false);

        var result = await store
            .VerifyChainIntegrityAsync(At(3), At(5), CancellationToken.None)
            .ConfigureAwait(false);

        result.Outcome.ShouldBe(
            AuditIntegrityOutcome.Verified,
            "an untenanted trail nobody touched must not be reported as tampered. First violation: "
            + (result.FirstViolationEventId ?? "<none>")
            + "; description: " + (result.ViolationDescription ?? "<none>"));
    }

    /// <summary>
    /// The same, where the untenanted rows are stored in BOTH spellings the column admits.
    /// </summary>
    /// <remarks>
    /// The table legitimately holds untenanted rows written as the reserved sentinel and rows whose column
    /// is NULL — the write path's COALESCE exists precisely to reconcile them, and chains them as one. The
    /// anchor query grouped on the raw column, so it split that one chain into two groups, produced two
    /// candidate anchors, and let whichever the database happened to return last decide. Folding the key
    /// alone does not fix that: it makes both candidates collide on one key. The grouping has to be done
    /// on the canonical term, which is what leaves exactly one anchor per partition and makes the answer
    /// independent of result order.
    /// </remarks>
    [Fact]
    public async Task ReportAnUntouchedUntenantedTrailVerified_WhenItsRowsUseBothSpellingsOfNoTenant()
    {
        var store = CreateStore();
        var ids = await StoreTrailAsync(store, tenantId: null, count: 5).ConfigureAwait(false);

        // The OLDER of the two records preceding the window. Leaving the newer one as the sentinel is what
        // puts two candidate anchors in play and makes "which one wins" observable.
        await RewriteTenantColumnToNullAsync(ids[0]).ConfigureAwait(false);

        var result = await store
            .VerifyChainIntegrityAsync(At(3), At(5), CancellationToken.None)
            .ConfigureAwait(false);

        result.Outcome.ShouldBe(
            AuditIntegrityOutcome.Verified,
            "a NULL-tenant row and a sentinel row are one chain on write and must be one on read. "
            + "First violation: " + (result.FirstViolationEventId ?? "<none>")
            + "; description: " + (result.ViolationDescription ?? "<none>"));
    }

    /// <summary>
    /// LIVENESS. The fold must not break the case that already worked: a tenanted trail verified over a
    /// window that starts after its first record is still clean.
    /// </summary>
    [Fact]
    public async Task ReportAnUntouchedTenantedTrailVerified_WhenTheWindowDoesNotBeginAtGenesis()
    {
        const string Tenant = "tenant-a";
        var store = CreateTenantAwareStore();

        using (TenantContextHolder.BeginScope(Tenant))
        {
            _ = await StoreTrailAsync(store, Tenant, count: 5).ConfigureAwait(false);
        }

        var result = await store
            .VerifyChainIntegrityAsync(At(3), At(5), CancellationToken.None)
            .ConfigureAwait(false);

        result.Outcome.ShouldBe(
            AuditIntegrityOutcome.Verified,
            "First violation: " + (result.FirstViolationEventId ?? "<none>")
            + "; description: " + (result.ViolationDescription ?? "<none>"));
    }

    /// <summary>
    /// SAFETY. An untenanted trail with a record genuinely removed from inside the window is still
    /// reported — the false accusation is not being silenced by making the check toothless.
    /// </summary>
    [Fact]
    public async Task StillReportViolations_WhenARecordIsRemovedFromAnUntenantedWindow()
    {
        var store = CreateStore();
        var ids = await StoreTrailAsync(store, tenantId: null, count: 5).ConfigureAwait(false);

        await DeleteRecordAsync(ids[3]).ConfigureAwait(false);

        var result = await store
            .VerifyChainIntegrityAsync(At(3), At(5), CancellationToken.None)
            .ConfigureAwait(false);

        result.Outcome.ShouldBe(
            AuditIntegrityOutcome.ViolationsDetected,
            "a record removed from inside the verified window must still be reported");
    }

    private static DateTimeOffset At(int minute) => TrailStart.AddMinutes(minute);

    /// <summary>Appends <paramref name="count"/> chained records, one per minute from the trail start.</summary>
    private static async Task<IReadOnlyList<string>> StoreTrailAsync(IAuditStore store, string? tenantId, int count)
    {
        var ids = new List<string>(count);
        for (var i = 1; i <= count; i++)
        {
            var eventId = "range-verify-" + i.ToString(System.Globalization.CultureInfo.InvariantCulture);
            _ = await store.StoreAsync(
                new AuditEvent
                {
                    EventId = eventId,
                    EventType = AuditEventType.DataAccess,
                    Action = "action-" + i.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    Outcome = AuditOutcome.Success,
                    Timestamp = At(i),
                    ActorId = "actor-1",
                    TenantId = tenantId
                },
                CancellationToken.None).ConfigureAwait(false);

            ids.Add(eventId);
        }

        return ids;
    }

    private async Task RewriteTenantColumnToNullAsync(string eventId)
    {
        await using var connection = new SqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync(CancellationToken.None).ConfigureAwait(false);

        var affected = await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE audit.AuditEvents SET TenantId = NULL WHERE EventId = @EventId",
                new { EventId = eventId })).ConfigureAwait(false);

        // A rewrite that touched nothing would let the arm pass without ever creating the mixed trail.
        affected.ShouldBe(1, "expected to rewrite exactly one row for '" + eventId + "'");
    }

    private async Task DeleteRecordAsync(string eventId)
    {
        await using var connection = new SqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync(CancellationToken.None).ConfigureAwait(false);

        var affected = await connection.ExecuteAsync(
            new CommandDefinition(
                "DELETE FROM audit.AuditEvents WHERE EventId = @EventId",
                new { EventId = eventId })).ConfigureAwait(false);

        // A delete that removed nothing would let the safety arm pass against a store that detects nothing.
        affected.ShouldBe(1, "expected to delete exactly one row for '" + eventId + "'");
    }

    private IAuditStore CreateStore() => BuildStore(tenantContext: new TestTenantContext(TenantScope.UntenantedSentinel));

    private IAuditStore CreateTenantAwareStore() => BuildStore(new AmbientHolderTenantContext());

    private SqlServerAuditStore BuildStore(ITenantContext? tenantContext) =>
        new(
            Microsoft.Extensions.Options.Options.Create(new SqlServerAuditOptions
            {
                ConnectionString = _fixture.ConnectionString,
                SchemaName = "audit",
                TableName = "AuditEvents",
                CommandTimeoutSeconds = 30
            }),
            Microsoft.Extensions.Options.Options.Create(new SqlServerAuditAnnotationStoreOptions
            {
                ConnectionString = _fixture.ConnectionString,
                SchemaName = "audit",
                TableName = "AuditAnnotations"
            }),
            AuditIntegrityTestStrategy.Create(),
            tenantContext,
            EnabledTestLogger.Create<SqlServerAuditStore>());

    /// <summary>Resolves the tenant established with <c>TenantContextHolder.BeginScope</c>.</summary>
    private sealed class AmbientHolderTenantContext : ITenantContext
    {
        public string? TenantId => TenantContextHolder.Current;

        public bool HasTenant => !string.IsNullOrEmpty(TenantContextHolder.Current);
    }

    /// <summary>
    /// Creates the audit schema and clears it. TenantId is NULLABLE, matching the shipped schema: a
    /// fixture that made it NOT NULL would delete the mixed-spelling case one of these arms exists for.
    /// </summary>
    private async Task EnsureSchemaAsync()
    {
        const string CreateSchemaAndTableSql = """
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
        _ = await connection.ExecuteAsync(CreateSchemaAndTableSql).ConfigureAwait(false);
    }
}
