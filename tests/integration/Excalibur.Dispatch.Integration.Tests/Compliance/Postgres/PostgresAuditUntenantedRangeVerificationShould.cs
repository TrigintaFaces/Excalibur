// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Dapper;

using Excalibur.AuditLogging.Postgres;
using Excalibur.Compliance;

using Npgsql;

namespace Excalibur.Dispatch.Integration.Tests.Compliance.Postgres;

/// <summary>
/// Verification over a window whose edges are not the trail's edges — it neither begins at the first record
/// nor ends at the last — against REAL Postgres.
/// </summary>
/// <remarks>
/// <para>
/// Companion to the SqlServer class of the same shape. The two providers carried the defect
/// independently: verifying a date range matches two reads of the same table — the records inside the
/// window, and the tag of the record immediately preceding it — and the keys those two are matched on
/// were derived separately. The record path mapped the stored tenant back to the value that was signed,
/// so an untenanted record keyed on null; the anchor path read the column raw, so its anchor keyed on the
/// reserved sentinel. Every untenanted lookup missed, the anchor was read as absent, and the verifier was
/// told to assert that the first record in the window was the partition's genesis record.
/// </para>
/// <para>
/// It was not, so verification reported removal, insertion, or reordering against a trail nobody had
/// touched — a false accusation of tampering, emitted by a compliance artifact, that an auditor cannot
/// tell apart from the real thing.
/// </para>
/// <para>
/// The arms are paired on purpose: reporting an untouched trail clean is only correct while the check can
/// still report a touched one, so a tampered arm that must fail sits beside the arms that must pass.
/// </para>
/// <para>
/// The later arms cover the other edge. A window's right edge is pinned by the record that follows it, whose
/// keyed MAC was computed over the tag of the record it was written to follow — so removing records from the
/// end of the window breaks it. Without that pin the survivors of a suffix deletion chain perfectly to one
/// another and to the anchor, and the store reports a truncated trail intact. The same arms cover the stored
/// prior-tag column, which is not a MAC input and so is checked separately.
/// </para>
/// </remarks>
[IntegrationTest]
[Collection(ContainerCollections.Postgres)]
[Trait("Component", TestComponents.AuditLogging)]
[Trait("Infrastructure", TestInfrastructure.Postgres)]
[Trait(TraitNames.Category, TestCategories.Integration)]
[Trait(TraitNames.Component, TestComponents.Compliance)]
public sealed class PostgresAuditUntenantedRangeVerificationShould : IAsyncLifetime
{
    /// <summary>Fixed rather than relative to now: the window boundaries are the subject of the test.</summary>
    private static readonly DateTimeOffset TrailStart = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private readonly PostgresFixture _fixture;

    public PostgresAuditUntenantedRangeVerificationShould(PostgresFixture fixture) => _fixture = fixture;

    public async ValueTask InitializeAsync()
    {
        // Never skip-gated. A range-verification arm that quietly does not run is how the false accusation
        // reached a shipped compliance surface in the first place.
        _fixture.DockerAvailable.ShouldBeTrue(
            "Postgres container must be available — audit range verification is never skipped.");

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
    /// anchor query grouped on the raw column, so it split that one chain into two groups and produced two
    /// candidate anchors for it. Folding the key alone does not fix that: it makes both candidates collide
    /// on one key, and which one survives is then decided by the order the rows came back in. The grouping
    /// has to be done on the canonical term, which leaves exactly one anchor per partition.
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

    /// <summary>
    /// The liveness half of the right-edge pin: an untouched window that does not end at the trail's last
    /// record must still verify.
    /// </summary>
    /// <remarks>
    /// Stated before the safety arms below, and for the same reason this class states its other liveness
    /// arms first: a verifier that reported tampering whenever a record existed after the window would
    /// satisfy every deletion arm here while being worthless, and a compliance check that accuses healthy
    /// data gets switched off and takes the real detections with it.
    /// </remarks>
    [Fact]
    public async Task ReportAnUntouchedTrailVerified_WhenTheWindowDoesNotEndAtTheLastRecord()
    {
        var store = CreateStore();
        _ = await StoreTrailAsync(store, tenantId: null, count: 5).ConfigureAwait(false);

        var result = await store
            .VerifyChainIntegrityAsync(At(1), At(3), CancellationToken.None)
            .ConfigureAwait(false);

        result.Outcome.ShouldBe(
            AuditIntegrityOutcome.Verified,
            "a window with records after it is untouched, so pinning its right edge must not accuse it");
        result.EventsVerified.ShouldBe(3);
    }

    /// <summary>
    /// A record removed from the <em>end</em> of the verified window is reported, because the record that
    /// follows the window still carries the tag of what was there.
    /// </summary>
    /// <remarks>
    /// This is the case the left-edge anchor could not reach. The survivors of a suffix deletion chain
    /// perfectly to one another and to the anchor, so the walk holds and nothing among the records presented
    /// mentions the removed one; without a right-edge pin the store reports the trail intact.
    /// </remarks>
    [Fact]
    public async Task ReportViolations_WhenARecordIsRemovedFromTheEndOfTheWindow()
    {
        var store = CreateStore();
        var ids = await StoreTrailAsync(store, tenantId: null, count: 5).ConfigureAwait(false);

        // The last record inside the window, leaving records 1 and 2 behind and record 4 after it.
        await DeleteRecordAsync(ids[2]).ConfigureAwait(false);

        var result = await store
            .VerifyChainIntegrityAsync(At(1), At(3), CancellationToken.None)
            .ConfigureAwait(false);

        result.Outcome.ShouldBe(
            AuditIntegrityOutcome.ViolationsDetected,
            "the record after the window was written to follow the removed one, so its tag no longer verifies");
        result.ViolationDescription.ShouldContain("end of the range");
    }

    /// <summary>
    /// Rewriting only the stored prior-tag column is reported, and an untouched trail is not.
    /// </summary>
    /// <remarks>
    /// The column is not an input to the keyed MAC — the tag is computed over the prior tag supplied at write
    /// time, not over the copy kept in the row — so rewriting it alone leaves every MAC intact and every link
    /// walking correctly. It is compared against the predecessor actually present for that reason: exports
    /// handed to an auditor read this value, and a value nothing verifies can be rewritten for free.
    /// </remarks>
    [Fact]
    public async Task ReportViolations_WhenOnlyTheStoredPriorTagIsRewritten()
    {
        var store = CreateStore();
        var ids = await StoreTrailAsync(store, tenantId: null, count: 4).ConfigureAwait(false);

        var untouched = await store
            .VerifyChainIntegrityAsync(At(1), At(4), CancellationToken.None)
            .ConfigureAwait(false);

        untouched.Outcome.ShouldBe(
            AuditIntegrityOutcome.Verified,
            "the trail is untouched, so the stored-linkage comparison must not accuse it");

        await RewritePreviousTagAsync(ids[2]).ConfigureAwait(false);

        var result = await store
            .VerifyChainIntegrityAsync(At(1), At(4), CancellationToken.None)
            .ConfigureAwait(false);

        result.Outcome.ShouldBe(
            AuditIntegrityOutcome.ViolationsDetected,
            "the record's own claim about its predecessor no longer names the record that precedes it");
        result.FirstViolationEventId.ShouldBe(ids[2]);
        result.ViolationDescription.ShouldContain("stored prior tag");
    }

    /// <summary>
    /// The liveness half of Excalibur_Dispatch-8sbvv4: with hash chaining disabled, StoreAsync signs every
    /// record independently against a null prior tag. An untouched trail must verify clean — not report the
    /// tampering the enabled-chaining walk would see if it (wrongly) carried tags forward across records
    /// that were never chained to each other in the first place.
    /// </summary>
    [Fact]
    public async Task VerifiesAnUntouchedTrail_WhenHashChainingIsDisabled()
    {
        var store = CreateUnchainedStore();
        _ = await StoreTrailAsync(store, tenantId: null, count: 3).ConfigureAwait(false);

        var result = await store
            .VerifyChainIntegrityAsync(At(1), At(3), CancellationToken.None)
            .ConfigureAwait(false);

        result.Outcome.ShouldBe(
            AuditIntegrityOutcome.Verified,
            "an unchained trail nobody touched must not be reported as tampered. First violation: "
            + (result.FirstViolationEventId ?? "<none>")
            + "; description: " + (result.ViolationDescription ?? "<none>"));
        result.EventsVerified.ShouldBe(3);
    }

    /// <summary>
    /// The safety half, paired with the liveness arm above. Chaining disabled trades away D2 (linkage), not
    /// D1 (content integrity) — each record still verifies its own MAC against the null prior tag it was
    /// actually signed with, so a rewritten record must still be caught even though no chain links it to its
    /// neighbours.
    /// </summary>
    [Fact]
    public async Task StillDetectsARewrittenRecord_WhenHashChainingIsDisabled()
    {
        var store = CreateUnchainedStore();
        var ids = await StoreTrailAsync(store, tenantId: null, count: 3).ConfigureAwait(false);

        await RewriteEventHashAsync(ids[1]).ConfigureAwait(false);

        var result = await store
            .VerifyChainIntegrityAsync(At(1), At(3), CancellationToken.None)
            .ConfigureAwait(false);

        result.Outcome.ShouldBe(AuditIntegrityOutcome.ViolationsDetected);
        result.FirstViolationEventId.ShouldBe(ids[1]);
    }

    private static DateTimeOffset At(int minute) => TrailStart.AddMinutes(minute);

    /// <summary>Rewrites a record's stored prior tag, moving no input to its keyed MAC.</summary>
    private async Task RewritePreviousTagAsync(string eventId)
    {
        await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync(CancellationToken.None).ConfigureAwait(false);

        var affected = await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE audit.audit_events SET previous_event_hash = @Rewritten WHERE event_id = @EventId",
                new { Rewritten = new string('F', 64), EventId = eventId })).ConfigureAwait(false);

        // A rewrite that touched nothing would let the safety arm pass against a store that detects nothing.
        affected.ShouldBe(1, "expected to rewrite exactly one row for '" + eventId + "'");
    }

    private async Task RewriteEventHashAsync(string eventId)
    {
        await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync(CancellationToken.None).ConfigureAwait(false);

        var affected = await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE audit.audit_events SET event_hash = @Rewritten WHERE event_id = @EventId",
                new { Rewritten = new string('F', 64), EventId = eventId })).ConfigureAwait(false);

        // A rewrite that touched nothing would let the safety arm pass against a store that detects nothing.
        affected.ShouldBe(1, "expected to rewrite exactly one row for '" + eventId + "'");
    }

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
        await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync(CancellationToken.None).ConfigureAwait(false);

        var affected = await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE audit.audit_events SET tenant_id = NULL WHERE event_id = @EventId",
                new { EventId = eventId })).ConfigureAwait(false);

        // A rewrite that touched nothing would let the arm pass without ever creating the mixed trail.
        affected.ShouldBe(1, "expected to rewrite exactly one row for '" + eventId + "'");
    }

    private async Task DeleteRecordAsync(string eventId)
    {
        await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync(CancellationToken.None).ConfigureAwait(false);

        var affected = await connection.ExecuteAsync(
            new CommandDefinition(
                "DELETE FROM audit.audit_events WHERE event_id = @EventId",
                new { EventId = eventId })).ConfigureAwait(false);

        // A delete that removed nothing would let the safety arm pass against a store that detects nothing.
        affected.ShouldBe(1, "expected to delete exactly one row for '" + eventId + "'");
    }

    private IAuditStore CreateStore() => BuildStore(tenantContext: new TestTenantContext(TenantScope.UntenantedSentinel));

    private IAuditStore CreateTenantAwareStore() => BuildStore(new AmbientHolderTenantContext());

    /// <summary>A store configured the way Excalibur_Dispatch-8sbvv4's arms need it: chaining off.</summary>
    private IAuditStore CreateUnchainedStore() =>
        BuildStore(tenantContext: new TestTenantContext(TenantScope.UntenantedSentinel), enableHashChain: false);

    private PostgresAuditStore BuildStore(ITenantContext? tenantContext, bool enableHashChain = true) =>
        new(
            Microsoft.Extensions.Options.Options.Create(new PostgresAuditOptions
            {
                ConnectionString = _fixture.ConnectionString,
                SchemaName = "audit",
                TableName = "audit_events",
                CommandTimeoutSeconds = 30,
                EnableHashChain = enableHashChain
            }),
            AuditIntegrityTestStrategy.Create(),
            tenantContext,
            EnabledTestLogger.Create<PostgresAuditStore>());

    /// <summary>Resolves the tenant established with <c>TenantContextHolder.BeginScope</c>.</summary>
    private sealed class AmbientHolderTenantContext : ITenantContext
    {
        public string? TenantId => TenantContextHolder.Current;

        public bool HasTenant => !string.IsNullOrEmpty(TenantContextHolder.Current);
    }

    /// <summary>
    /// Creates the audit schema and clears it. tenant_id is NULLABLE, matching what the store writes: a
    /// fixture that made it NOT NULL would delete the mixed-spelling case one of these arms exists for.
    /// </summary>
    private async Task EnsureSchemaAsync()
    {
        const string CreateSchemaAndTableSql = """
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
        _ = await connection.ExecuteAsync(CreateSchemaAndTableSql).ConfigureAwait(false);
    }
}
