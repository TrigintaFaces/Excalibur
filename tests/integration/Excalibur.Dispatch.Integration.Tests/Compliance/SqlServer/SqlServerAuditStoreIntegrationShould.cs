// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Dapper;

using Excalibur.AuditLogging.SqlServer;
using Excalibur.Compliance;
using Excalibur.Dispatch;

using Microsoft.Data.SqlClient;

namespace Excalibur.Dispatch.Integration.Tests.Compliance.SqlServer;

[IntegrationTest]
[Collection(ContainerCollections.SqlServer)]
[Trait("Component", TestComponents.AuditLogging)]
[Trait("Infrastructure", TestInfrastructure.SqlServer)]
[Trait(TraitNames.Category, TestCategories.Integration)]
[Trait(TraitNames.Component, TestComponents.Compliance)]
public sealed class SqlServerAuditStoreIntegrationShould : IntegrationTestBase
{
	private readonly SqlServerFixture _fixture;

	public SqlServerAuditStoreIntegrationShould(SqlServerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task Store_and_get_by_id_round_trip()
	{
		await InitializeAuditTableAsync();
		using var store = CreateStore();
		var evt = CreateAuditEvent("evt-roundtrip", "tenant-1", DateTimeOffset.UtcNow.AddMinutes(-2));

		var stored = await store.StoreAsync(evt, TestCancellationToken);
		var loaded = await store.GetByIdAsync(evt.EventId, TestCancellationToken);

		stored.SequenceNumber.ShouldBeGreaterThan(0);
		loaded.ShouldNotBeNull();
		loaded!.EventId.ShouldBe(evt.EventId);
		loaded.Action.ShouldBe(evt.Action);
		loaded.ActorId.ShouldBe(evt.ActorId);
		loaded.Metadata.ShouldNotBeNull();
		loaded.Metadata!["scenario"].ShouldBe("integration");
	}

	[Fact]
	public async Task Query_and_count_with_filters()
	{
		await InitializeAuditTableAsync();
		using var store = CreateStore();

		await store.StoreAsync(CreateAuditEvent("evt-q-1", "tenant-1", DateTimeOffset.UtcNow.AddMinutes(-4), actorId: "actor-a", action: "read"), TestCancellationToken);
		await store.StoreAsync(CreateAuditEvent("evt-q-2", "tenant-1", DateTimeOffset.UtcNow.AddMinutes(-3), actorId: "actor-a", action: "write"), TestCancellationToken);
		await store.StoreAsync(CreateAuditEvent("evt-q-3", "tenant-2", DateTimeOffset.UtcNow.AddMinutes(-2), actorId: "actor-b", action: "read"), TestCancellationToken);

		var query = new AuditQuery
		{
			ActorId = "actor-a",
			TenantId = "tenant-1",
			MaxResults = 10,
			Skip = 0
		};

		var results = await store.QueryAsync(query, TestCancellationToken);
		var count = await store.CountAsync(query, TestCancellationToken);

		results.Count.ShouldBe(2);
		count.ShouldBe(2);
	}

	[Fact]
	public async Task Verify_chain_integrity_detects_tampering()
	{
		await InitializeAuditTableAsync();
		using var store = CreateStore();

		var first = CreateAuditEvent("evt-v-1", "tenant-1", DateTimeOffset.UtcNow.AddMinutes(-2));
		var second = CreateAuditEvent("evt-v-2", "tenant-1", DateTimeOffset.UtcNow.AddMinutes(-1));

		await store.StoreAsync(first, TestCancellationToken);
		await store.StoreAsync(second, TestCancellationToken);

		var start = DateTimeOffset.UtcNow.AddHours(-1);
		var end = DateTimeOffset.UtcNow.AddHours(1);

		var validResult = await store.VerifyChainIntegrityAsync(start, end, TestCancellationToken);
		validResult.IsValid.ShouldBeTrue();

		await using (var connection = new SqlConnection(_fixture.ConnectionString))
		{
			await connection.OpenAsync(TestCancellationToken);
			_ = await connection.ExecuteAsync(
				"UPDATE [audit].[AuditEvents] SET EventHash = @BadHash WHERE EventId = @EventId",
				new { BadHash = new string('F', 64), EventId = second.EventId });
		}

		var invalidResult = await store.VerifyChainIntegrityAsync(start, end, TestCancellationToken);

		invalidResult.IsValid.ShouldBeFalse();
		invalidResult.ViolationCount.ShouldBeGreaterThan(0);
		invalidResult.FirstViolationEventId.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public async Task Verify_chain_integrity_detects_chain_link_break()
	{
		await InitializeAuditTableAsync();
		using var store = CreateStore();

		var first = CreateAuditEvent("evt-chain-1", "tenant-1", DateTimeOffset.UtcNow.AddMinutes(-2));
		var second = CreateAuditEvent("evt-chain-2", "tenant-1", DateTimeOffset.UtcNow.AddMinutes(-1));

		await store.StoreAsync(first, TestCancellationToken);
		await store.StoreAsync(second, TestCancellationToken);

		var start = DateTimeOffset.UtcNow.AddHours(-1);
		var end = DateTimeOffset.UtcNow.AddHours(1);

		await using (var connection = new SqlConnection(_fixture.ConnectionString))
		{
			await connection.OpenAsync(TestCancellationToken);
			_ = await connection.ExecuteAsync(
				"UPDATE [audit].[AuditEvents] SET PreviousEventHash = @BadHash WHERE EventId = @EventId",
				new { BadHash = new string('F', 64), EventId = second.EventId });
		}

		var invalidResult = await store.VerifyChainIntegrityAsync(start, end, TestCancellationToken);

		invalidResult.IsValid.ShouldBeFalse();
		invalidResult.ViolationCount.ShouldBeGreaterThan(0);
		invalidResult.FirstViolationEventId.ShouldBe(second.EventId);
	}

	[Fact]
	public async Task Get_last_event_supports_tenant_filter()
	{
		await InitializeAuditTableAsync();
		using var store = CreateStore();

		await store.StoreAsync(CreateAuditEvent("evt-last-1", "tenant-1", DateTimeOffset.UtcNow.AddMinutes(-3)), TestCancellationToken);
		await store.StoreAsync(CreateAuditEvent("evt-last-2", "tenant-1", DateTimeOffset.UtcNow.AddMinutes(-2)), TestCancellationToken);
		await store.StoreAsync(CreateAuditEvent("evt-last-3", "tenant-2", DateTimeOffset.UtcNow.AddMinutes(-1)), TestCancellationToken);

		// The store is scoped to tenant-1 by construction, so this returns tenant-1's latest and NOT
		// evt-last-3 (tenant-2's), even though evt-last-3 is the newest row in the table. The tenant
		// argument is not what selects the tenant — the ambient context is.
		var tenantLast = await store.GetLastEventAsync("tenant-1", TestCancellationToken);

		tenantLast.ShouldNotBeNull();
		tenantLast!.EventId.ShouldBe("evt-last-2");

		// Liveness arm: the scoping is not vacuously excluding everything — a tenant-2 store sees
		// tenant-2's row, and neither store can reach the other's.
		using var tenant2Store = CreateStore(tenantId: "tenant-2");
		var tenant2Last = await tenant2Store.GetLastEventAsync(null, TestCancellationToken);

		tenant2Last.ShouldNotBeNull();
		tenant2Last!.EventId.ShouldBe("evt-last-3");
	}

	[Fact]
	public async Task Enforce_retention_deletes_old_rows_in_batches()
	{
		await InitializeAuditTableAsync();
		using var store = CreateStore(options =>
		{
			options.Retention.CleanupBatchSize = 1;
		});

		await store.StoreAsync(CreateAuditEvent("evt-old", "tenant-1", DateTimeOffset.UtcNow.AddDays(-40)), TestCancellationToken);
		await store.StoreAsync(CreateAuditEvent("evt-new", "tenant-1", DateTimeOffset.UtcNow.AddDays(-1)), TestCancellationToken);

		// Rebound from the removed EnforceRetentionAsync to the purge capability. The partition is now
		// explicit: retention is expressed per-tenant, so a destructive sweep can only span tenants by a
		// caller asking for each partition in turn rather than by omitting a term.
		var purge = (IAuditPurgeCapability)((IAuditStore)store).GetService(typeof(IAuditPurgeCapability))!;

		var deleted = await purge.PurgeTenantAsync(
			DateTimeOffset.UtcNow.AddDays(-30), KeyedTenantPartition.Scoped("tenant-1"), TestCancellationToken);
		var remaining = await store.CountAsync(new AuditQuery(), TestCancellationToken);

		deleted.ShouldBe(1);
		remaining.ShouldBe(1);
	}

	/// <summary>
	/// A legacy row written before this table had a tenant column — <c>TenantId</c> NULL — must be purgeable
	/// by asking for the un-tenanted partition.
	/// </summary>
	/// <remarks>
	/// In SQL, null equals nothing, not even null. A purge predicate written as <c>[TenantId] = @TenantId</c>
	/// therefore matches a NULL-tenant row for **no** partition value a caller can supply — the type has no
	/// "all" inhabitant — so the row is permanently unpurgeable: invisible to the sweep, retained past its
	/// policy, with no error and no operator remedy. In a compliance package that is a silent
	/// indefinite-retention-of-PII defect, not a nit, and it is invisible to every arm that only stores rows
	/// through the current write path (which always supplies a term).
	/// <para>
	/// The row is therefore inserted as raw SQL with an explicit NULL. Writing it through the store would
	/// prove nothing — the defect only exists for data that predates the column.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task Purge_removes_legacy_rows_whose_tenant_is_null()
	{
		await InitializeAuditTableAsync();
		using var store = CreateStore();

		await InsertLegacyNullTenantEventAsync("evt-legacy-null", DateTimeOffset.UtcNow.AddDays(-40));

		(await CountEventsAsync("evt-legacy-null")).ShouldBe(
			1, "the legacy row must exist before the purge, or this arm proves nothing.");

		var purge = ((IAuditStore)store).GetService(typeof(IAuditPurgeCapability)).ShouldBeAssignableTo<IAuditPurgeCapability>(
			"the SQL Server audit store must advertise the purge capability — a compliance package with no "
			+ "reachable deletion path cannot honour a retention policy at all.");

		var purged = await purge.PurgeTenantAsync(
			DateTimeOffset.UtcNow.AddDays(-30), KeyedTenantPartition.Untenanted, TestCancellationToken);

		purged.ShouldBe(
			1,
			"a NULL-tenant row belongs to the un-tenanted partition: folding NULL onto the reserved sentinel "
			+ "is what makes it reachable. Without that fold it matches no partition and is unpurgeable forever.");

		(await CountEventsAsync("evt-legacy-null")).ShouldBe(0, "the expired legacy row must actually be gone.");
	}

	/// <summary>
	/// SAFETY pair: purging the un-tenanted partition must not reach a real tenant's rows. Without this, a
	/// fold implemented as "match everything" would satisfy the arm above while destroying every tenant's audit
	/// history — the opposite failure, and a far worse one.
	/// </summary>
	[Fact]
	public async Task Purge_of_the_untenanted_partition_leaves_a_real_tenants_rows_alone()
	{
		await InitializeAuditTableAsync();
		using var store = CreateStore();

		await InsertLegacyNullTenantEventAsync("evt-legacy-mixed", DateTimeOffset.UtcNow.AddDays(-40));
		await store.StoreAsync(
			CreateAuditEvent("evt-tenant-old", "tenant-1", DateTimeOffset.UtcNow.AddDays(-40)), TestCancellationToken);

		var purge = (IAuditPurgeCapability)((IAuditStore)store).GetService(typeof(IAuditPurgeCapability))!;
		var purged = await purge.PurgeTenantAsync(
			DateTimeOffset.UtcNow.AddDays(-30), KeyedTenantPartition.Untenanted, TestCancellationToken);

		purged.ShouldBe(1, "only the un-tenanted row is in the requested partition.");
		(await CountEventsAsync("evt-tenant-old")).ShouldBe(
			1,
			"tenant-1's expired row must survive a purge of the UN-TENANTED partition — a destructive sweep may "
			+ "only span tenants when a caller asks for each partition in turn, deliberately.");
	}

	private async Task InsertLegacyNullTenantEventAsync(string eventId, DateTimeOffset timestamp)
	{
		await ExecuteSqlAsync(
			"""
			INSERT INTO [audit].[AuditEvents]
			    (EventId, EventType, [Action], Outcome, [Timestamp], ActorId, TenantId, EventHash)
			VALUES
			    (@EventId, 0, 'legacy-seed', 0, @Timestamp, 'actor-legacy', NULL, @EventHash)
			""",
			new { EventId = eventId, Timestamp = timestamp, EventHash = $"hash-{eventId}" });
	}

	/// <summary>
	/// The estate-wide member must reach EVERY partition — both tenants and the un-tenanted rows.
	/// </summary>
	/// <remarks>
	/// Retention is estate-wide by contract: it governs how long data may be kept, which is not a per-tenant
	/// question. A "purge everything expired" that silently skips a partition retains PII past its policy in
	/// exactly the tenant nobody is looking at.
	/// </remarks>
	[Fact]
	public async Task Purge_expired_reaches_every_partition()
	{
		await InitializeAuditTableAsync();
		using var store = CreateStore();

		await store.StoreAsync(CreateAuditEvent("evt-estate-a", "tenant-a", DateTimeOffset.UtcNow.AddDays(-40)), TestCancellationToken);
		await store.StoreAsync(CreateAuditEvent("evt-estate-b", "tenant-b", DateTimeOffset.UtcNow.AddDays(-40)), TestCancellationToken);
		await InsertLegacyNullTenantEventAsync("evt-estate-null", DateTimeOffset.UtcNow.AddDays(-40));

		var purge = (IAuditPurgeCapability)((IAuditStore)store).GetService(typeof(IAuditPurgeCapability))!;
		_ = await purge.PurgeExpiredAsync(DateTimeOffset.UtcNow.AddDays(-30), TestCancellationToken);

		(await CountEventsAsync("evt-estate-a")).ShouldBe(0, "tenant-a's expired row must be reached.");
		(await CountEventsAsync("evt-estate-b")).ShouldBe(0, "tenant-b's expired row must be reached.");
		(await CountEventsAsync("evt-estate-null")).ShouldBe(
			0, "the un-tenanted partition is part of the estate — skipping it retains data past its policy.");
	}

	/// <summary>
	/// The estate-wide member must respect the cutoff. Now that "it deleted everything expired" is the CORRECT
	/// outcome, the cutoff is the only thing separating retention from destroying the audit log.
	/// </summary>
	[Fact]
	public async Task Purge_expired_leaves_rows_newer_than_the_cutoff()
	{
		await InitializeAuditTableAsync();
		using var store = CreateStore();

		await store.StoreAsync(CreateAuditEvent("evt-fresh-a", "tenant-a", DateTimeOffset.UtcNow.AddDays(-1)), TestCancellationToken);
		await InsertLegacyNullTenantEventAsync("evt-fresh-null", DateTimeOffset.UtcNow.AddDays(-1));

		var purge = (IAuditPurgeCapability)((IAuditStore)store).GetService(typeof(IAuditPurgeCapability))!;
		_ = await purge.PurgeExpiredAsync(DateTimeOffset.UtcNow.AddDays(-30), TestCancellationToken);

		(await CountEventsAsync("evt-fresh-a")).ShouldBe(1, "a row newer than the cutoff must survive.");
		(await CountEventsAsync("evt-fresh-null")).ShouldBe(
			1, "the un-tenanted partition is subject to the cutoff too, not purged wholesale.");
	}

	/// <summary>
	/// THE SURVIVOR ARM. A tenant-scoped purge must leave every OTHER tenant's rows intact.
	/// </summary>
	/// <remarks>
	/// Without this, an over-broad <c>PurgeTenantAsync</c> — one that ignores its partition and sweeps the
	/// estate — passes every other arm here: the estate-wide arms want everything gone, and the tenant arms
	/// only check that the named tenant's rows went. The failure is silent and destructive: an operator
	/// purging tenant A on a deletion request destroys tenant B's audit history, and the only evidence is data
	/// that is already gone.
	/// <para>
	/// This is the arm that makes the two-member seam mean anything. Without it, the seam is indistinguishable
	/// from a single estate-wide member with a parameter nobody reads.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task Purge_of_one_tenant_leaves_every_other_tenants_rows_intact()
	{
		await InitializeAuditTableAsync();
		using var store = CreateStore();

		await store.StoreAsync(CreateAuditEvent("evt-scoped-a", "tenant-a", DateTimeOffset.UtcNow.AddDays(-40)), TestCancellationToken);
		await store.StoreAsync(CreateAuditEvent("evt-scoped-b", "tenant-b", DateTimeOffset.UtcNow.AddDays(-40)), TestCancellationToken);
		await InsertLegacyNullTenantEventAsync("evt-scoped-null", DateTimeOffset.UtcNow.AddDays(-40));

		var purge = (IAuditPurgeCapability)((IAuditStore)store).GetService(typeof(IAuditPurgeCapability))!;
		var purged = await purge.PurgeTenantAsync(
			DateTimeOffset.UtcNow.AddDays(-30), KeyedTenantPartition.Scoped("tenant-a"), TestCancellationToken);

		purged.ShouldBe(1, "exactly the requested partition's expired row.");
		(await CountEventsAsync("evt-scoped-a")).ShouldBe(0, "tenant-a asked to be purged.");
		(await CountEventsAsync("evt-scoped-b")).ShouldBe(
			1,
			"tenant-b DID NOT ask to be purged — a scoped purge that also deletes B destroys another customer's "
			+ "audit history, and nothing but this assertion can tell the two implementations apart.");
		(await CountEventsAsync("evt-scoped-null")).ShouldBe(
			1, "the un-tenanted partition is a partition like any other and was not the one requested.");
	}

	/// <summary>
	/// THE CASCADE ARM. Purging an event must delete the annotations that hang off it.
	/// </summary>
	/// <remarks>
	/// Every purge arm above this one asserts only that EVENTS go. All of them pass against a purge that
	/// deletes events and leaves their annotations behind — and that residue is not untidy, it is an
	/// integrity violation the store's own implementation calls out: an annotation has no tenant column,
	/// its tenant is derived by joining <c>EventId -> AuditEvents.TenantId</c>. Delete the event and the
	/// annotation's tenant is NO LONGER DERIVABLE, so it either vanishes from every tenant under an INNER
	/// JOIN (silent permanent loss) or folds onto the untenanted sentinel and becomes readable by an
	/// untenanted scope (a cross-tenant exposure of one customer's annotation text).
	/// <para>
	/// The fixture was already wired to make this detectable — <c>CreateStore</c> points the store at the
	/// REAL annotation table precisely so a purge that fails to reach annotations cannot pass. That
	/// capability existed with no arm exercising it, so nothing had ever executed the cascade.
	/// </para>
	/// <para>
	/// <b>Both directions, deliberately.</b> The cascade assertion alone is satisfied by a purge that
	/// deletes the WHOLE annotation table — which would destroy the annotations of every event that is
	/// still perfectly alive. So the surviving event's annotation is asserted in the same arm. A purge
	/// that under-reaches fails the first assertion; one that over-reaches fails the second; only a purge
	/// scoped exactly to the events it deleted passes both.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task Purge_expired_deletes_the_annotations_of_the_events_it_deletes_and_no_others()
	{
		await InitializeAuditTableAsync();
		await InitializeAnnotationTableAsync();
		using var store = CreateStore();

		// One expired event and one that is still inside the retention window, each carrying an annotation.
		await store.StoreAsync(
			CreateAuditEvent("evt-cascade-old", "tenant-a", DateTimeOffset.UtcNow.AddDays(-40)), TestCancellationToken);
		await store.StoreAsync(
			CreateAuditEvent("evt-cascade-new", "tenant-a", DateTimeOffset.UtcNow.AddDays(-1)), TestCancellationToken);
		await InsertAnnotationAsync("ann-cascade-old", "evt-cascade-old");
		await InsertAnnotationAsync("ann-cascade-new", "evt-cascade-new");

		// Guard the premise: if the seed did not land, every assertion below would pass vacuously.
		(await CountAnnotationsAsync("ann-cascade-old")).ShouldBe(
			1, "the annotation must exist BEFORE the purge, or this arm proves nothing about deleting it.");

		var purge = (IAuditPurgeCapability)((IAuditStore)store).GetService(typeof(IAuditPurgeCapability))!;
		_ = await purge.PurgeExpiredAsync(DateTimeOffset.UtcNow.AddDays(-30), TestCancellationToken);

		(await CountEventsAsync("evt-cascade-old")).ShouldBe(0, "the expired event is past its retention policy.");
		(await CountAnnotationsAsync("ann-cascade-old")).ShouldBe(
			0,
			"the annotation of a purged event must be purged with it — left behind, its tenant is no longer "
			+ "derivable, so it is either invisible to every tenant or exposed to the untenanted scope.");

		(await CountEventsAsync("evt-cascade-new")).ShouldBe(1, "an event inside the window must survive.");
		(await CountAnnotationsAsync("ann-cascade-new")).ShouldBe(
			1,
			"a surviving event KEEPS its annotation — without this, a purge that simply emptied the annotation "
			+ "table would satisfy the cascade assertion above while destroying live annotation history.");
	}

	private async Task ExecuteSqlAsync(string sql, object? parameters = null)
	{
#pragma warning disable CA2100 // Test code with controlled input
		await using var connection = new SqlConnection(_fixture.ConnectionString);
		await connection.OpenAsync(TestCancellationToken);
		_ = await connection.ExecuteAsync(sql, parameters);
#pragma warning restore CA2100
	}

	private async Task<int> CountEventsAsync(string eventId)
	{
		await using var connection = new SqlConnection(_fixture.ConnectionString);
		await connection.OpenAsync(TestCancellationToken);

		return await connection.ExecuteScalarAsync<int>(
			"SELECT COUNT(*) FROM [audit].[AuditEvents] WHERE EventId = @EventId", new { EventId = eventId });
	}

	/// <summary>
	/// Counts by the annotation's own Id, never by EventId: after a correct cascade the parent event is gone,
	/// so a count keyed on the event cannot distinguish "the annotation was deleted" from "the join lost it".
	/// </summary>
	private async Task<int> CountAnnotationsAsync(string annotationId)
	{
		await using var connection = new SqlConnection(_fixture.ConnectionString);
		await connection.OpenAsync(TestCancellationToken);

		return await connection.ExecuteScalarAsync<int>(
			"SELECT COUNT(*) FROM [audit].[AuditAnnotations] WHERE Id = @Id", new { Id = annotationId });
	}

	private async Task InsertAnnotationAsync(string annotationId, string eventId) =>
		await ExecuteSqlAsync(
			"""
			INSERT INTO [audit].[AuditAnnotations]
			    (Id, EventId, AnnotationType, Content, ActorId, CreatedAt, Visibility)
			VALUES (@Id, @EventId, 0, @Content, 'actor-cascade', SYSDATETIMEOFFSET(), 0);
			""",
			new { Id = annotationId, EventId = eventId, Content = $"annotation for {eventId}" });

	/// <summary>
	/// The annotations table is created separately from the events table because
	/// <see cref="InitializeAuditTableAsync"/> creates only the latter. Column shape is taken from the
	/// annotation store's own integration fixture rather than invented here, so the two cannot drift.
	/// </summary>
	private async Task InitializeAnnotationTableAsync() =>
		await ExecuteSqlAsync(
			"""
			IF NOT EXISTS (SELECT * FROM sys.objects
			               WHERE object_id = OBJECT_ID(N'[audit].[AuditAnnotations]') AND type in (N'U'))
			BEGIN
			    CREATE TABLE [audit].[AuditAnnotations] (
			        [Id] NVARCHAR(64) NOT NULL,
			        [EventId] NVARCHAR(64) NOT NULL,
			        [AnnotationType] INT NOT NULL,
			        [Content] NVARCHAR(MAX) NOT NULL,
			        [ActorId] NVARCHAR(256) NOT NULL,
			        [CreatedAt] DATETIMEOFFSET(7) NOT NULL,
			        [Visibility] INT NOT NULL,
			        CONSTRAINT [PK_AuditAnnotations] PRIMARY KEY CLUSTERED ([Id]),
			        INDEX [IX_AuditAnnotations_EventId] NONCLUSTERED ([EventId])
			    );
			END;
			DELETE FROM [audit].[AuditAnnotations];
			""");

	[Fact]
	public async Task Store_batch_persists_all_events()
	{
		await InitializeAuditTableAsync();
		using var store = CreateStore();
		var events = new List<AuditEvent>
		{
			CreateAuditEvent("evt-batch-1", "tenant-1", DateTimeOffset.UtcNow.AddMinutes(-2)),
			CreateAuditEvent("evt-batch-2", "tenant-1", DateTimeOffset.UtcNow.AddMinutes(-1)),
		};

		var ids = await store.StoreBatchAsync(events, TestCancellationToken);
		var count = await store.CountAsync(new AuditQuery(), TestCancellationToken);

		ids.Count.ShouldBe(2);
		ids.All(id => id.SequenceNumber > 0).ShouldBeTrue();
		count.ShouldBe(2);
	}

	/// <param name="tenantId">
	/// The ambient tenant the store reads under. Defaults to the tenant this suite's events are stamped
	/// with, because the store binds its WRITE term from <c>auditEvent.TenantId</c> and its READ term from
	/// the ambient context — so a store with no ambient tenant reads under the untenanted sentinel and can
	/// never see a tenant-stamped row it just wrote. The tenant reaches the store through CONSTRUCTION,
	/// never through a query argument: <c>AuditQuery.TenantId</c> and the <c>GetLastEventAsync</c> tenant
	/// parameter are deliberately not consulted, so that a caller naming someone else's tenant cannot read
	/// it. Per-tenant arms therefore build a store per tenant rather than passing a tenant argument.
	/// </param>
	private SqlServerAuditStore CreateStore(Action<SqlServerAuditOptions>? configure = null, string? tenantId = "tenant-1")
	{
		var options = new SqlServerAuditOptions
		{
			ConnectionString = _fixture.ConnectionString,
			SchemaName = "audit",
			TableName = "AuditEvents",
			CommandTimeoutSeconds = 30,
			Retention = { CleanupBatchSize = 100 }
		};

		configure?.Invoke(options);

		// Retention cascades into the annotation table, so the store needs its location. Pointing it at the
		// real annotation table (rather than a throwaway name) is deliberate: a retention arm that deleted
		// events while silently failing to reach annotations would otherwise still pass here.
		return new SqlServerAuditStore(
			Microsoft.Extensions.Options.Options.Create(options),
			Microsoft.Extensions.Options.Options.Create(new SqlServerAuditAnnotationStoreOptions
			{
				ConnectionString = _fixture.ConnectionString,
				SchemaName = "audit",
				TableName = "AuditAnnotations",
			}),
			AuditIntegrityTestStrategy.Create(),
			tenantContext: tenantId is null ? null : new FixedTenantContext(tenantId),
			EnabledTestLogger.Create<SqlServerAuditStore>());
	}

	private async Task InitializeAuditTableAsync()
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
		await connection.OpenAsync(TestCancellationToken);
		_ = await connection.ExecuteAsync(createSchemaAndTableSql);
	}

	private static AuditEvent CreateAuditEvent(
		string id,
		string tenantId,
		DateTimeOffset timestamp,
		string actorId = "actor-1",
		string action = "read")
	{
		return new AuditEvent
		{
			EventId = id,
			EventType = AuditEventType.DataAccess,
			Action = action,
			Outcome = AuditOutcome.Success,
			Timestamp = timestamp,
			ActorId = actorId,
			ActorType = "User",
			ResourceId = "resource-1",
			ResourceType = "Document",
			ResourceClassification = DataClassification.Confidential,
			TenantId = tenantId,
			CorrelationId = $"corr-{id}",
			SessionId = $"session-{id}",
			IpAddress = "127.0.0.1",
			UserAgent = "integration-test",
			Reason = "coverage",
			Metadata = new Dictionary<string, string>
			{
				["scenario"] = "integration"
			}
		};
	}

	/// <summary>
	/// A tenant fixed at construction — the shape the store's contract actually takes.
	/// </summary>
	private sealed class FixedTenantContext(string tenantId) : ITenantContext
	{
		public string? TenantId { get; } = tenantId;

		public bool HasTenant => !string.IsNullOrWhiteSpace(TenantId);
	}
}