// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Dapper;

using Excalibur.AuditLogging.Postgres;
using Excalibur.Compliance;
using Excalibur.Dispatch;

using Npgsql;

namespace Excalibur.Dispatch.Integration.Tests.Compliance.Postgres;

/// <summary>
/// Real-Postgres safety+liveness lock for klx8fa: <see cref="PostgresAuditStore"/> now implements
/// <see cref="IAuditPurgeCapability"/>, ported from <c>SqlServerAuditStore.PurgeCoreAsync</c>.
/// </summary>
/// <remarks>
/// <para>
/// SUBJECT: binds <c>Excalibur.AuditLogging.Postgres.PostgresAuditStore</c> -- see the companion
/// conformance test's remarks on the unrelated same-named type in <c>Excalibur.Data.Postgres.Audit</c>.
/// </para>
/// <para>
/// This is a separate file from <see cref="PostgresAuditStoreConformanceTests"/> deliberately: purge is
/// not a member of <see cref="AuditStoreConformanceTestKit"/> (it is a discoverable capability, not part of
/// the core contract every store must offer), so it is exercised directly here rather than through the kit.
/// </para>
/// <para>
/// Arms mirror <c>SqlServerAuditStoreIntegrationShould</c>'s purge suite, minus the annotation-cascade arm:
/// this package ships no Postgres-specific audit-annotation store (the only durable
/// <c>IAuditAnnotationStore</c> implementation is <c>SqlServerAuditAnnotationStore</c>), so there is nothing
/// for a Postgres purge to cascade into today.
/// </para>
/// <para>
/// <b>THE SURVIVOR ARM</b> (<see cref="Purge_of_one_tenant_leaves_every_other_tenants_rows_intact"/>) is the
/// load-bearing safety half: an over-broad <c>PurgeTenantAsync</c> that ignores its partition and sweeps the
/// estate passes every OTHER arm here (the estate-wide arms want everything gone; the single-tenant arms
/// only check that the NAMED tenant's rows went). Without this arm the two-member seam (estate-wide vs
/// tenant-confined) is indistinguishable from one member with an ignored parameter -- ADR-345 territory,
/// since this is a range delete (not a primary-key-addressed statement), the tenant predicate is load-bearing
/// here rather than the anti-pattern ADR-345 warns against.
/// </para>
/// </remarks>
[IntegrationTest]
[Collection(ContainerCollections.Postgres)]
[Trait("Component", TestComponents.AuditLogging)]
[Trait("Infrastructure", TestInfrastructure.Postgres)]
[Trait(TraitNames.Category, TestCategories.Integration)]
[Trait(TraitNames.Component, TestComponents.Compliance)]
public sealed class PostgresAuditStorePurgeShould : IntegrationTestBase
{
	private readonly PostgresFixture _fixture;

	public PostgresAuditStorePurgeShould(PostgresFixture fixture)
	{
		_fixture = fixture;
	}

	/// <summary>
	/// A legacy row written before this table had a tenant column -- <c>tenant_id</c> NULL -- must be
	/// purgeable by asking for the un-tenanted partition. See the SqlServer sibling arm for why: a bare
	/// <c>tenant_id = @TenantId</c> matches a NULL-tenant row for no partition value a caller can supply,
	/// so without the COALESCE fold the row is permanently unpurgeable.
	/// </summary>
	[Fact]
	public async Task Purge_removes_legacy_rows_whose_tenant_is_null()
	{
		_fixture.DockerAvailable.ShouldBeTrue("this lock must never soft-skip -- see verify-against-real-infra-not-mock.md");

		await InitializeAuditTableAsync();
		using var store = CreateStore();

		await InsertLegacyNullTenantEventAsync("evt-legacy-null", DateTimeOffset.UtcNow.AddDays(-40));

		(await CountEventsAsync("evt-legacy-null")).ShouldBe(
			1, "the legacy row must exist before the purge, or this arm proves nothing.");

		var purge = ((IAuditStore)store).GetService(typeof(IAuditPurgeCapability)).ShouldBeAssignableTo<IAuditPurgeCapability>(
			"the Postgres audit store must advertise the purge capability -- a compliance package with no "
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
	/// SAFETY pair: purging the un-tenanted partition must not reach a real tenant's rows.
	/// </summary>
	[Fact]
	public async Task Purge_of_the_untenanted_partition_leaves_a_real_tenants_rows_alone()
	{
		_fixture.DockerAvailable.ShouldBeTrue("this lock must never soft-skip -- see verify-against-real-infra-not-mock.md");

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
			"tenant-1's expired row must survive a purge of the UN-TENANTED partition -- a destructive sweep may "
			+ "only span tenants when a caller asks for each partition in turn, deliberately.");
	}

	/// <summary>
	/// The estate-wide member must reach EVERY partition -- both tenants and the un-tenanted rows.
	/// </summary>
	[Fact]
	public async Task Purge_expired_reaches_every_partition()
	{
		_fixture.DockerAvailable.ShouldBeTrue("this lock must never soft-skip -- see verify-against-real-infra-not-mock.md");

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
			0, "the un-tenanted partition is part of the estate -- skipping it retains data past its policy.");
	}

	/// <summary>
	/// The estate-wide member must respect the cutoff -- the only thing separating retention from destroying
	/// the audit log, now that "it deleted everything expired" is the correct outcome.
	/// </summary>
	[Fact]
	public async Task Purge_expired_leaves_rows_newer_than_the_cutoff()
	{
		_fixture.DockerAvailable.ShouldBeTrue("this lock must never soft-skip -- see verify-against-real-infra-not-mock.md");

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
	/// THE SURVIVOR ARM. A tenant-scoped purge must leave every OTHER tenant's rows intact -- see the class
	/// remarks for why this is the arm that makes the two-member seam mean anything.
	/// </summary>
	[Fact]
	public async Task Purge_of_one_tenant_leaves_every_other_tenants_rows_intact()
	{
		_fixture.DockerAvailable.ShouldBeTrue("this lock must never soft-skip -- see verify-against-real-infra-not-mock.md");

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
			"tenant-b DID NOT ask to be purged -- a scoped purge that also deletes B destroys another customer's "
			+ "audit history, and nothing but this assertion can tell the two implementations apart.");
		(await CountEventsAsync("evt-scoped-null")).ShouldBe(
			1, "the un-tenanted partition is a partition like any other and was not the one requested.");
	}

	/// <summary>
	/// LIVENESS pairing arm for the batch loop: a purge whose match count exceeds one batch must not stop
	/// after the first batch -- <c>PurgeCoreAsync</c>'s <c>do/while (deleted == CleanupBatchSize)</c> loop is
	/// what this proves. A batch size of 1 against 3 matching rows would leave 2 behind if the loop were
	/// dropped or its termination condition inverted.
	/// </summary>
	[Fact]
	public async Task Purge_continues_across_batches_until_none_remain()
	{
		_fixture.DockerAvailable.ShouldBeTrue("this lock must never soft-skip -- see verify-against-real-infra-not-mock.md");

		await InitializeAuditTableAsync();
		using var store = CreateStore(options => options.Retention.CleanupBatchSize = 1);

		await store.StoreAsync(CreateAuditEvent("evt-batch-1", "tenant-1", DateTimeOffset.UtcNow.AddDays(-40)), TestCancellationToken);
		await store.StoreAsync(CreateAuditEvent("evt-batch-2", "tenant-1", DateTimeOffset.UtcNow.AddDays(-40)), TestCancellationToken);
		await store.StoreAsync(CreateAuditEvent("evt-batch-3", "tenant-1", DateTimeOffset.UtcNow.AddDays(-40)), TestCancellationToken);

		var purge = (IAuditPurgeCapability)((IAuditStore)store).GetService(typeof(IAuditPurgeCapability))!;
		var purged = await purge.PurgeTenantAsync(
			DateTimeOffset.UtcNow.AddDays(-30), KeyedTenantPartition.Scoped("tenant-1"), TestCancellationToken);

		purged.ShouldBe(3, "all three expired rows must be reached across multiple size-1 batches.");
		(await CountEventsAsync("evt-batch-1")).ShouldBe(0);
		(await CountEventsAsync("evt-batch-2")).ShouldBe(0);
		(await CountEventsAsync("evt-batch-3")).ShouldBe(0);
	}

	private async Task InsertLegacyNullTenantEventAsync(string eventId, DateTimeOffset timestamp) =>
		await ExecuteSqlAsync(
			"""
			INSERT INTO audit.audit_events
			    (event_id, event_type, action, outcome, timestamp, actor_id, tenant_id, event_hash)
			VALUES
			    (@EventId, 0, 'legacy-seed', 0, @Timestamp, 'actor-legacy', NULL, @EventHash)
			""",
			new { EventId = eventId, Timestamp = timestamp, EventHash = $"hash-{eventId}" });

	private async Task ExecuteSqlAsync(string sql, object? parameters = null)
	{
#pragma warning disable CA2100 // Test code with controlled input
		await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
		await connection.OpenAsync(TestCancellationToken).ConfigureAwait(false);
		_ = await connection.ExecuteAsync(sql, parameters).ConfigureAwait(false);
#pragma warning restore CA2100
	}

	private async Task<int> CountEventsAsync(string eventId)
	{
		await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
		await connection.OpenAsync(TestCancellationToken).ConfigureAwait(false);

		return await connection.ExecuteScalarAsync<int>(
			"SELECT COUNT(*) FROM audit.audit_events WHERE event_id = @EventId", new { EventId = eventId })
			.ConfigureAwait(false);
	}

	/// <summary>
	/// Column set mirrors <see cref="PostgresAuditStore"/>'s own INSERT list verbatim -- this package ships
	/// no DDL for a consumer to apply, so the write statement is the only authority on the schema it needs.
	/// </summary>
	private async Task InitializeAuditTableAsync()
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
			    ip_address               VARCHAR(45),
			    user_agent              VARCHAR(500),
			    reason                  VARCHAR(1000),
			    metadata                JSONB,
			    previous_event_hash     VARCHAR(512),
			    event_hash              VARCHAR(512) NOT NULL
			);

			TRUNCATE TABLE audit.audit_events RESTART IDENTITY;
			""";

		await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
		await connection.OpenAsync(TestCancellationToken).ConfigureAwait(false);
		_ = await connection.ExecuteAsync(createSchemaAndTableSql).ConfigureAwait(false);
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

	/// <param name="tenantId">
	/// The ambient tenant the store reads under -- reaches the store through CONSTRUCTION, never through a
	/// query argument, mirroring the SqlServer sibling's <c>CreateStore</c>.
	/// </param>
	private PostgresAuditStore CreateStore(Action<PostgresAuditOptions>? configure = null, string? tenantId = "tenant-1")
	{
		var options = new PostgresAuditOptions
		{
			ConnectionString = _fixture.ConnectionString,
			SchemaName = "audit",
			TableName = "audit_events",
			CommandTimeoutSeconds = 30,
			Retention = { CleanupBatchSize = 100 }
		};

		configure?.Invoke(options);

		return new PostgresAuditStore(
			Microsoft.Extensions.Options.Options.Create(options),
			AuditIntegrityTestStrategy.Create(),
			tenantContext: tenantId is null
				? new TestTenantContext(TenantScope.UntenantedSentinel)
				: (ITenantContext)new TestTenantContext(tenantId),
			EnabledTestLogger.Create<PostgresAuditStore>());
	}
}
