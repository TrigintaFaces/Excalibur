// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Domain.Model;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.Queries;
using Excalibur.EventSourcing.Postgres.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Excalibur.EventSourcing.Postgres;

using Microsoft.Extensions.Logging.Abstractions;

using Npgsql;

using Shouldly;
using Tests.Shared.Helpers;

using Xunit;

namespace Excalibur.Integration.Tests.Data.EventStore;

/// <summary>
/// Author≠impl real-infra lock for the row-discriminator tenancy keystone — the Postgres
/// <see cref="PostgresEventStore"/> derives a tenant scope from the ambient <see cref="ITenantContext"/> and
/// applies a row-level <c>tenant_id</c> discriminator (<c>AND tenant_id = @TenantId</c>) in the same atomic
/// statement on every read/write, so one tenant can NEVER observe another tenant's event streams — and the
/// non-multi-tenant path (no tenant context) round-trips unchanged with no tenant column referenced at all.
/// </summary>
/// <remarks>
/// <b>verify-against-real-infra-not-mock:</b> runs against a real Postgres (TestContainers) so the
/// <c>WHERE tenant_id = @TenantId</c> predicate is evaluated by the real engine — a mock cannot reproduce
/// row-level scoping or the non-MT (predicate-free) round-trip. NON-SKIPPED
/// (<c>DockerAvailable.ShouldBeTrue</c>). Shares the Postgres container via the collection fixture.
/// <para>
/// <b>Both arms (testing-patterns §3):</b> SAFETY — tenant B's scoped read must not see tenant A's rows;
/// LIVENESS — tenant A still reads its own stream, and the non-MT store round-trips its events.
/// </para>
/// <para>
/// <b>RED-on-mutant:</b> drop the <c>tenant_id</c> predicate from the Load/IsErased requests ⇒ tenant B's
/// <c>LoadAsync</c> returns tenant A's events ⇒ the isolation facts go RED.
/// </para>
/// </remarks>
[Collection(PostgresEventStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "Postgres")]
public sealed class PostgresEventStoreTenantIsolationShould
{
	private const string AggregateType = "Order";

	private readonly PostgresEventStoreContainerFixture _fixture;

	public PostgresEventStoreTenantIsolationShould(PostgresEventStoreContainerFixture fixture) => _fixture = fixture;

	private sealed class FixedTenant(string? tenantId) : ITenantContext
	{
		public string? TenantId { get; } = tenantId;

		public bool HasTenant => TenantId is not null;
	}

	private PostgresEventStore StoreFor(string? tenantId, string? table = null) =>
		new(
			NpgsqlDataSource.Create(_fixture.ConnectionString),
			NullLogger<PostgresEventStore>.Instance,
			schema: "public",
			table: table ?? _fixture.TableName,
			tenantContext: tenantId is null ? UntenantedTestTenantContext.Instance : (ITenantContext)new FixedTenant(tenantId));

[MessageName("Test.PostgresEventStoreTenantIsolation.OrderPlaced")]
private sealed record OrderPlaced(string AggregateId, long Version) : IDomainEvent
	{
		public string EventId { get; init; } = Guid.NewGuid().ToString();
		public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
		public IDictionary<string, object>? Metadata { get; init; }
	}

	[Fact]
	[Trait("Pattern", "Integration")]
	public async Task RecheckCommittedErasureOutsideAnAmbientStaleSnapshot()
	{
		_fixture.DockerAvailable.ShouldBeTrue();
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		var aggregateId = "fresh-" + Guid.NewGuid().ToString("N");
		var tenant = KeyedTenantPartition.FromStoredValue("fresh-tenant");
		await using var source = NpgsqlDataSource.Create(_fixture.ConnectionString);
		var store = new PostgresEventStore(source, NullLogger<PostgresEventStore>.Instance, new FixedTenant(tenant.TenantId));
		var original = new OrderPlaced(aggregateId, 0);
		(await store.AppendAsync(aggregateId, AggregateType, [original], -1, CancellationToken.None)
			.ConfigureAwait(false)).Success.ShouldBeTrue();
		var reader = store.GetService(typeof(IEventStoreAuthoritativeReader)).ShouldBeAssignableTo<IEventStoreAuthoritativeReader>();
		using var ambient = new System.Transactions.TransactionScope(System.Transactions.TransactionScopeOption.RequiresNew,
			new System.Transactions.TransactionOptions { IsolationLevel = System.Transactions.IsolationLevel.RepeatableRead },
			System.Transactions.TransactionScopeAsyncFlowOption.Enabled);
		var ambientTransaction = System.Transactions.Transaction.Current;
		await using var stale = await source.OpenConnectionAsync().ConfigureAwait(false);
		// Fixed shipped fixture table; the event identity is parameterized.
		await using var staleQuery = new NpgsqlCommand("SELECT event_type FROM public.events WHERE event_id = @id", stale);
		staleQuery.Parameters.AddWithValue("id", original.EventId);
		var oldType = (string)(await staleQuery.ExecuteScalarAsync().ConfigureAwait(false))!;
		using (var outside = new System.Transactions.TransactionScope(System.Transactions.TransactionScopeOption.Suppress,
			System.Transactions.TransactionScopeAsyncFlowOption.Enabled))
		{
			(await store.EraseEventsAsync(aggregateId, AggregateType, Guid.NewGuid(), CancellationToken.None)
				.ConfigureAwait(false)).ShouldBe(1);
		}
		(await staleQuery.ExecuteScalarAsync().ConfigureAwait(false)).ShouldBe(oldType,
			"the ambient transaction must demonstrably retain the pre-erasure snapshot");
		var current = await reader.ReadCurrentAsync(tenant, aggregateId, AggregateType, original.EventId, 0,
			CancellationToken.None).ConfigureAwait(false);
		current.ShouldNotBeNull();
		current.EventType.ShouldBe(ErasedEventMarker.EventType);
		current.EventId.ShouldBe(original.EventId);
		current.Tenant.TenantId.ShouldBe(tenant.TenantId);
		System.Transactions.Transaction.Current.ShouldBeSameAs(ambientTransaction);
		stale.State.ShouldBe(System.Data.ConnectionState.Open);
	}

	[Fact]
	[Trait("Pattern", "Integration")]
	public async Task RecheckScopeOnEveryCallAndKeepAbsenceDistinctFromCancellation()
	{
		_fixture.DockerAvailable.ShouldBeTrue();
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await using var source = NpgsqlDataSource.Create(_fixture.ConnectionString);
		var tenant = KeyedTenantPartition.FromStoredValue("reader-a");
		var context = new ChangingTenant { TenantId = tenant.TenantId };
		var store = new PostgresEventStore(source, NullLogger<PostgresEventStore>.Instance, context);
		var reader = store.GetService(typeof(IEventStoreAuthoritativeReader)).ShouldBeAssignableTo<IEventStoreAuthoritativeReader>();
		store.GetService(typeof(IEventStoreErasure)).ShouldBeSameAs(store);
		store.GetService(typeof(string)).ShouldBeNull();
		new DenyingDecorator(store).GetService(typeof(IEventStoreAuthoritativeReader)).ShouldBeNull();
		Should.Throw<ArgumentNullException>(() => store.GetService(null!));
		var missingId = Guid.NewGuid().ToString("N");
		(await reader.ReadCurrentAsync(tenant, missingId, AggregateType, missingId, 0, CancellationToken.None)
			.ConfigureAwait(false)).ShouldBeNull();
		context.TenantId = "reader-b";
		await Should.ThrowAsync<InvalidOperationException>(async () =>
			await reader.ReadCurrentAsync(tenant, missingId, AggregateType, missingId, 0, CancellationToken.None));
		using var cancelled = new CancellationTokenSource();
		cancelled.Cancel();
		await Should.ThrowAsync<OperationCanceledException>(async () =>
			await reader.ReadCurrentAsync(KeyedTenantPartition.FromContext(context), missingId, AggregateType, missingId, 0, cancelled.Token));
		// The reader owns connection handles only; the supplied data source remains usable.
		await using var stillUsable = await source.OpenConnectionAsync().ConfigureAwait(false);
		stillUsable.State.ShouldBe(System.Data.ConnectionState.Open);
	}

	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	[Trait("Pattern", "Integration")]
	public async Task UseResolvedLocationForBothAppendsAndGlobalReads(bool useAdvancedSource)
	{
		_fixture.DockerAvailable.ShouldBeTrue();
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);
		await using (var connection = _fixture.CreateConnection())
		{
			await connection.OpenAsync().ConfigureAwait(false);
			foreach (var script in ShippedSchemaScript.ReadAll(
				"src/Excalibur/Excalibur.EventSourcing.Postgres/Scripts/001_CreateSnapshotSchema.sql"))
			{
				// Fixed shipped schema file above; no caller-supplied SQL or identifiers.
#pragma warning disable CA2100
				await using var command = new NpgsqlCommand(script, connection);
#pragma warning restore CA2100
				_ = await command.ExecuteNonQueryAsync().ConfigureAwait(false);
			}
		}
		var services = new ServiceCollection();
		services.AddLogging();
		if (useAdvancedSource)
		{
			// DI's selected source must win over the unused builder connection string.
			services.AddSingleton(_ => NpgsqlDataSource.Create(_fixture.ConnectionString));
		}
		new ExcaliburEventSourcingBuilder(services).UsePostgres(builder =>
		{
			builder.ConnectionString(useAdvancedSource
				? "Host=127.0.0.1;Port=1;Database=unused;Username=unused;Timeout=1"
				: _fixture.ConnectionString);
			builder
			.EventStoreSchema("unused_builder_schema")
			.EventStoreTable("unused_builder_table")
			.SnapshotStoreSchema("unused_snapshot_schema")
			.SnapshotStoreTable("unused_snapshot_table");
		});
		services.PostConfigure<PostgresEventSourcingOptions>(options =>
		{
			options.EventStoreSchema = "public";
			options.EventStoreTable = _fixture.TableName;
			options.SnapshotStoreSchema = "public";
			options.SnapshotStoreTable = "event_store_snapshots";
		});
		await using var provider = services.BuildServiceProvider();
		var store = provider.GetRequiredKeyedService<IEventStore>("default");
		var eventId = Guid.NewGuid().ToString("N");
		var aggregateId = "configured-" + eventId;
		(await store.AppendAsync(aggregateId, AggregateType,
			[new OrderPlaced(aggregateId, 0) { EventId = eventId }], -1, CancellationToken.None)
			.ConfigureAwait(false)).Success.ShouldBeTrue();
		var loaded = await store.LoadAsync(aggregateId, AggregateType, CancellationToken.None).ConfigureAwait(false);
		loaded.Single().EventId.ShouldBe(eventId);
		var capability = store.GetService(typeof(IEventStoreAuthoritativeReader)).ShouldBeAssignableTo<IEventStoreAuthoritativeReader>();
		var state = await capability.ReadCurrentAsync(KeyedTenantPartition.FromStoredValue(loaded.Single().TenantId!),
			aggregateId, AggregateType, eventId, 0, CancellationToken.None).ConfigureAwait(false);
		state.ShouldNotBeNull();
		state.EventId.ShouldBe(eventId);
		var query = provider.GetRequiredService<IGlobalStreamQuery>();
		var global = await query.ReadAllAsync(GlobalStreamPosition.Start, 100, CancellationToken.None).ConfigureAwait(false);
		global.ShouldContain(e => e.EventId == eventId && e.AggregateId == aggregateId);
		var snapshots = provider.GetRequiredKeyedService<ISnapshotStore>("default");
		var snapshot = new ConfiguredSnapshot(Guid.NewGuid().ToString("N"), aggregateId, AggregateType,
			0, DateTimeOffset.UtcNow, new byte[] { 1, 2, 3 }, null, null);
		await snapshots.SaveSnapshotAsync(snapshot, CancellationToken.None).ConfigureAwait(false);
		var restored = await snapshots.GetLatestSnapshotAsync(aggregateId, AggregateType, CancellationToken.None).ConfigureAwait(false);
		restored.ShouldNotBeNull();
		restored.SnapshotId.ShouldBe(snapshot.SnapshotId);
		restored.Data.ToArray().ShouldBe(snapshot.Data.ToArray());
	}

	[Fact]
	public async Task PreserveTenantProvenanceAcrossAggregateLoadOverloads()
	{
		_fixture.DockerAvailable.ShouldBeTrue("tenant provenance requires a real provider test");
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);
		var aggregateId = "provenance-" + Guid.NewGuid().ToString("N");
		var originals = new Dictionary<string, OrderPlaced[]>(StringComparer.Ordinal);
		foreach (var tenantId in new string?[] { "Acme", "acme", null })
		{
			var tenant = tenantId ?? KeyedTenantPartition.Untenanted.TenantId;
			var events = new[] { new OrderPlaced(aggregateId, 0), new OrderPlaced(aggregateId, 1) };
			originals.Add(tenant, events);
			var store = StoreFor(tenantId);
			(await store.AppendAsync(aggregateId, AggregateType, events, -1, CancellationToken.None))
				.Success.ShouldBeTrue();
			(await store.TombstoneArchivedEventsUpToVersionAsync(
				KeyedTenantPartition.FromStoredValue(tenant), aggregateId, AggregateType, 0, CancellationToken.None))
				.ShouldBe(1);
		}

		foreach (var tenantId in new string?[] { "Acme", "acme", null })
		{
			var tenant = tenantId ?? KeyedTenantPartition.Untenanted.TenantId;
			var store = StoreFor(tenantId);
			var all = await store.LoadAsync(aggregateId, AggregateType, CancellationToken.None);
			all.Select(e => e.TenantId).ShouldBe(new[] { tenant, tenant });
			all.Select(e => e.EventId).ShouldBe(originals[tenant].Select(e => e.EventId));
			all.Select(e => e.Version).ShouldBe(new long[] { 0, 1 });
			all[0].EventData.ShouldBeNull();
			all[0].ArchivedAt.ShouldNotBeNull();
			all[0].GlobalPosition.ShouldBeGreaterThan(0);

			var suffix = await store.LoadAsync(aggregateId, AggregateType, 0, CancellationToken.None);
			suffix.Count.ShouldBe(1);
			suffix[0].TenantId.ShouldBe(tenant);
			suffix[0].EventId.ShouldBe(originals[tenant][1].EventId);
			suffix[0].Version.ShouldBe(1);
		}

		// Exercise legacy nullable tenant storage without weakening the shipped table.
		await using var connection = _fixture.CreateConnection();
		await connection.OpenAsync();
		await using var copy = new Npgsql.NpgsqlCommand("""
			CREATE TABLE public.legacy_aggregate_tenant_events AS
			SELECT position, event_id, aggregate_id, aggregate_type, event_type, event_data,
			       metadata, version, timestamp, archived_at,
			       NULLIF(tenant_id, '__untenanted__') AS tenant_id FROM public.events
			""", connection);
		_ = await copy.ExecuteNonQueryAsync();
		try
		{
			var legacyStore = StoreFor(null, "legacy_aggregate_tenant_events");
			var all = await legacyStore.LoadAsync(aggregateId, AggregateType, CancellationToken.None);
			all.Select(e => e.TenantId).ShouldBe(new[]
			{
				KeyedTenantPartition.Untenanted.TenantId, KeyedTenantPartition.Untenanted.TenantId,
			});
			all.Select(e => e.EventId).ShouldBe(originals[KeyedTenantPartition.Untenanted.TenantId].Select(e => e.EventId));
			all[0].EventData.ShouldBeNull();
			all[0].ArchivedAt.ShouldNotBeNull();
			var suffix = await legacyStore.LoadAsync(aggregateId, AggregateType, 0, CancellationToken.None);
			suffix.Count.ShouldBe(1);
			suffix[0].TenantId.ShouldBe(KeyedTenantPartition.Untenanted.TenantId);
			suffix[0].EventId.ShouldBe(originals[KeyedTenantPartition.Untenanted.TenantId][1].EventId);
			suffix[0].Version.ShouldBe(1);
		}
		finally
		{
			await using var cleanup = new Npgsql.NpgsqlCommand("DROP TABLE public.legacy_aggregate_tenant_events", connection);
			_ = await cleanup.ExecuteNonQueryAsync();
		}
	}

	[Fact]
	public async Task ScopeEveryStreamToItsTenant_OneTenantNeverSeesAnother()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"cross-tenant isolation is a security boundary — this real-Postgres lock must never be skipped");
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);

		var aggId = "agg-" + Guid.NewGuid().ToString("N");
		var tenantA = StoreFor("tenant-A");
		var tenantB = StoreFor("tenant-B");

		// Tenant A writes a 2-event stream.
		_ = await tenantA.AppendAsync(
			aggId, AggregateType,
			new IDomainEvent[] { new OrderPlaced(aggId, 0), new OrderPlaced(aggId, 1) },
			-1, CancellationToken.None).ConfigureAwait(false);

		// SAFETY — tenant B cannot LOAD tenant A's events.
		(await tenantB.LoadAsync(aggId, AggregateType, -1, CancellationToken.None).ConfigureAwait(false))
			.ShouldBeEmpty("tenant B must not load tenant A's event stream (row-level tenant_id scoping)");

		// SAFETY — tenant B sees no ERASURE state for tenant A's aggregate.
		(await tenantB.IsErasedAsync(aggId, AggregateType, CancellationToken.None).ConfigureAwait(false))
			.ShouldBeFalse("tenant B must not observe tenant A's aggregate erasure state");

		// LIVENESS — tenant A still sees exactly its own 2 events (reads scoped to the writing tenant).
		(await tenantA.LoadAsync(aggId, AggregateType, -1, CancellationToken.None).ConfigureAwait(false))
			.Count.ShouldBe(2, "tenant A sees exactly its own 2 events (its own tenant-scoped stream)");
	}

	[Fact]
	public async Task RoundTripUnscoped_WhenNoTenantContext_WithNoTenantPredicate()
	{
		// LIVENESS (non-MT / AC-K1.1) — a store with NO tenant context is the genuine non-multi-tenant path:
		// it must round-trip append→load and not throw. Under the keyed migration the None scope no longer emits an
		// empty predicate: it binds the reserved __untenanted__ sentinel, so this store reaches exactly the
		// the column on INSERT nor the predicate on SELECT).
		_fixture.DockerAvailable.ShouldBeTrue(
			"the non-multi-tenant round-trip is the keystone's fail-open path — this real lock must never be skipped");
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);

		var aggId = "agg-" + Guid.NewGuid().ToString("N");
		var nonTenant = StoreFor(null);

		_ = await nonTenant.AppendAsync(
			aggId, AggregateType,
			new IDomainEvent[] { new OrderPlaced(aggId, 0), new OrderPlaced(aggId, 1) },
			-1, CancellationToken.None).ConfigureAwait(false);

		(await nonTenant.LoadAsync(aggId, AggregateType, -1, CancellationToken.None).ConfigureAwait(false))
			.Count.ShouldBe(2, "the non-multi-tenant store round-trips its own events via the untenanted partition");
	}

	[Fact]
	public async Task NotDiscloseATenantsEvents_ToAnUnscopedReader()
	{
		// SAFETY (18c3el read-leak). The 6 event-read fixes (LoadEvents/GetCurrentVersion unscoped → tenant_id
		// IS NULL) shipped without a non-vacuous SAFETY lock: the existing arms are scoped-vs-scoped isolation
		// and unscoped-LIVENESS only, neither of which proves an UNSCOPED read does not DISCLOSE a tenant's
		// events. This is that missing disclosure lock. RED against the pre-fix empty predicate (an unscoped
		// read returned every tenant's events); GREEN once the unscoped branch is bounded to the untenanted
		// partition. Property-based: asserts the disclosure, not the SQL.
		_fixture.DockerAvailable.ShouldBeTrue(
			"cross-tenant read disclosure is a security boundary — this real-Postgres lock must never be skipped");
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);

		var aggId = "agg-" + Guid.NewGuid().ToString("N");

		// Tenant B owns a 2-event stream.
		_ = await StoreFor("tenant-B").AppendAsync(
			aggId, AggregateType,
			new IDomainEvent[] { new OrderPlaced(aggId, 0), new OrderPlaced(aggId, 1) },
			-1, CancellationToken.None).ConfigureAwait(false);

		// An unscoped reader's partition is the untenanted one; it must NOT receive tenant B's events.
		(await StoreFor(null).LoadAsync(aggId, AggregateType, -1, CancellationToken.None).ConfigureAwait(false))
			.ShouldBeEmpty(
				"an unscoped reader must not receive a tenant's events — the untenanted partition "
				+ "(the __untenanted__ sentinel, onto which COALESCE folds legacy NULL rows) excludes tenant-scoped rows; the empty-branch predicate disclosed every "
				+ "tenant's events to an unscoped host");
	}

	/// <summary>
	/// LIVENESS for the tenant-scoped stream key. Two tenants must be able to use the SAME natural
	/// aggregate id independently — the case a tenant-less uniqueness key makes impossible.
	/// </summary>
	/// <remarks>
	/// RED before the key carried the tenant term: the version probe is tenant-scoped, so tenant B probes
	/// its own (empty) partition and gets -1, then its INSERT collides with tenant A's row on
	/// <c>UNIQUE (aggregate_id, aggregate_type, version)</c>. The store classifies a unique violation as a
	/// concurrency conflict, which is RETRYABLE — and the retry re-probes, gets -1 again, and collides
	/// again. So the pre-fix failure is not merely a rejected append but one that cannot converge, which is
	/// why this arm asserts success rather than merely a different error classification.
	/// <para>
	/// This is the arm the safety-only assertions could not supply: an isolation lock is satisfied by a
	/// store that refuses every cross-tenant write, and that is exactly what the tenant-less key did.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task AdmitTheSameAggregateId_ForTwoTenantsIndependently()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"the tenant-scoped stream key is the fix under test — this real-Postgres lock must never be skipped");
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);

		// A natural key, which is the whole point: an order number or customer reference is chosen by the
		// consumer, so two tenants colliding on one is routine rather than exotic.
		const string sharedAggId = "order-40771";

		var tenantA = StoreFor("tenant-A");
		var tenantB = StoreFor("tenant-B");

		var appendA = await tenantA.AppendAsync(
			sharedAggId, AggregateType,
			new IDomainEvent[] { new OrderPlaced(sharedAggId, 0), new OrderPlaced(sharedAggId, 1) },
			-1, CancellationToken.None).ConfigureAwait(false);
		appendA.Success.ShouldBeTrue("tenant A opens the stream first; nothing contends with it");

		// The lock. Tenant B has never used this aggregate id, its probe returns -1, and its append must
		// therefore succeed. Under the tenant-less key this returned a concurrency conflict forever.
		var appendB = await tenantB.AppendAsync(
			sharedAggId, AggregateType,
			new IDomainEvent[] { new OrderPlaced(sharedAggId, 0) },
			-1, CancellationToken.None).ConfigureAwait(false);

		appendB.Success.ShouldBeTrue(
			"tenant B must be able to open its own stream at an aggregate id tenant A already used — the "
			+ "tenant participates in stream identity, so the two streams are distinct rows. Under a "
			+ "tenant-less key B's tenant-scoped probe reports -1 while its INSERT collides with A's row, "
			+ "and the resulting conflict is retryable but can never converge");

		// Each tenant reads back exactly its own stream, so the two did not merge into one.
		(await tenantA.LoadAsync(sharedAggId, AggregateType, -1, CancellationToken.None).ConfigureAwait(false))
			.Count.ShouldBe(2, "tenant A still holds its own two events");
		(await tenantB.LoadAsync(sharedAggId, AggregateType, -1, CancellationToken.None).ConfigureAwait(false))
			.Count.ShouldBe(1, "tenant B holds its own single event, not tenant A's stream");
	}

	/// <summary>
	/// SAFETY for the tenant-scoped stream key. Widening the key must not weaken optimistic concurrency
	/// WITHIN a tenant: a second append of the same version by the same tenant must still be rejected.
	/// </summary>
	/// <remarks>
	/// This is the arm that fails if the key is widened by DROPPING the constraint rather than replacing
	/// it — a change under which every isolation and liveness assertion in this file continues to pass.
	/// </remarks>
	[Fact]
	public async Task StillRejectADuplicateVersion_WithinOneTenant()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"optimistic concurrency is the constraint's reason to exist — this real-Postgres lock must never be skipped");
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);

		var aggId = "agg-" + Guid.NewGuid().ToString("N");
		var tenantA = StoreFor("tenant-A");

		(await tenantA.AppendAsync(
			aggId, AggregateType,
			new IDomainEvent[] { new OrderPlaced(aggId, 0) },
			-1, CancellationToken.None).ConfigureAwait(false))
			.Success.ShouldBeTrue("the first append opens the stream at version 0");

		// Same tenant, same aggregate, same version. The tenant term is equal on both rows, so the key
		// must still reject this.
		var duplicate = await tenantA.AppendAsync(
			aggId, AggregateType,
			new IDomainEvent[] { new OrderPlaced(aggId, 0) },
			-1, CancellationToken.None).ConfigureAwait(false);

		duplicate.Success.ShouldBeFalse(
			"a second append of version 0 by the SAME tenant must be rejected — adding the tenant to the "
			+ "key makes concurrency per-tenant, it does not remove it");
		duplicate.IsConcurrencyConflict.ShouldBeTrue(
			"and it must be classified as a concurrency conflict so the caller's retry path engages");

		// The rejected append left nothing behind.
		(await tenantA.LoadAsync(aggId, AggregateType, -1, CancellationToken.None).ConfigureAwait(false))
			.Count.ShouldBe(1, "the rejected duplicate must not have been written");
	}

	/// <summary>
	/// SAFETY for the untenanted partition specifically — the rows a pre-tenancy database is made of.
	/// </summary>
	/// <remarks>
	/// Separate from the arm above because it exercises the sentinel rather than a real tenant, and the
	/// sentinel is the value the migration backfills legacy NULL rows onto. Were the column left nullable,
	/// those rows would carry NULL; Postgres treats each NULL in a unique key as distinct from every other,
	/// so this duplicate would be ACCEPTED — optimistic concurrency silently gone for exactly the rows that
	/// predate tenancy, with no other assertion in this file able to observe it.
	/// </remarks>
	[Fact]
	public async Task StillRejectADuplicateVersion_InTheUntenantedPartition()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"the untenanted partition is where a nullable tenant column silently disables the key — never skip");
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);

		var aggId = "agg-" + Guid.NewGuid().ToString("N");
		var unscoped = StoreFor(null);

		(await unscoped.AppendAsync(
			aggId, AggregateType,
			new IDomainEvent[] { new OrderPlaced(aggId, 0) },
			-1, CancellationToken.None).ConfigureAwait(false))
			.Success.ShouldBeTrue("the unscoped store opens its stream in the untenanted partition");

		var duplicate = await unscoped.AppendAsync(
			aggId, AggregateType,
			new IDomainEvent[] { new OrderPlaced(aggId, 0) },
			-1, CancellationToken.None).ConfigureAwait(false);

		duplicate.Success.ShouldBeFalse(
			"a duplicate version in the untenanted partition must be rejected — the sentinel is a concrete "
			+ "value precisely so it can participate in a unique key, which a NULL discriminator cannot");
		duplicate.IsConcurrencyConflict.ShouldBeTrue(
			"and it must be classified as a concurrency conflict");
	}
	private sealed record ConfiguredSnapshot(string SnapshotId, string AggregateId, string AggregateType,
		long Version, DateTimeOffset CreatedAt, ReadOnlyMemory<byte> Data,
		IDictionary<string, object>? Metadata, string? TenantId) : ISnapshot;

	private sealed class ChangingTenant : ITenantContext
	{
		public string? TenantId { get; set; }
		public bool HasTenant => TenantId is not null;
	}

	private sealed class DenyingDecorator(IEventStore inner)
		: Excalibur.EventSourcing.Decorators.IsolatingEventStoreDecorator(inner);

}
