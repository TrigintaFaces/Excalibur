// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Domain.Model;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.DependencyInjection;
using Excalibur.EventSourcing.Queries;
using Excalibur.EventSourcing.SqlServer.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Excalibur.EventSourcing.SqlServer;

using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

using Xunit;

namespace Excalibur.Integration.Tests.Data.EventStore;

/// <summary>
/// Author≠impl real-infra lock for the row-discriminator tenancy keystone — the SQL Server
/// <see cref="SqlServerEventStore"/> derives a tenant scope from the ambient <see cref="ITenantContext"/>
/// and applies a row-level <c>TenantId</c> discriminator (<c>AND TenantId = @TenantId</c>) in the same atomic
/// statement on every read/write, so one tenant can NEVER observe another tenant's event streams — and the
/// non-multi-tenant path (no tenant context) round-trips unchanged with no tenant column referenced at all.
/// </summary>
/// <remarks>
/// <b>verify-against-real-infra-not-mock:</b> runs against a real SQL Server (TestContainers) so the
/// <c>WHERE TenantId = @TenantId</c> predicate is evaluated by the real engine — a mock cannot reproduce
/// row-level scoping or the non-MT (predicate-free) round-trip. NON-SKIPPED
/// (<c>DockerAvailable.ShouldBeTrue</c>). Shares the SQL Server container via the collection fixture.
/// <para>
/// <b>Both arms (testing-patterns §3):</b> SAFETY — tenant B's scoped read must not see tenant A's rows;
/// LIVENESS — tenant A still reads its own stream, and the non-MT store round-trips its events.
/// </para>
/// <para>
/// <b>RED-on-mutant:</b> drop the <c>TenantId</c> predicate from the Load/IsErased requests ⇒ tenant B's
/// <c>LoadAsync</c> returns tenant A's events ⇒ the isolation facts go RED. Emit the predicate on the
/// unscoped path ⇒ the non-MT round-trip throws on the (real) missing column binding.
/// </para>
/// </remarks>
[Collection(SqlServerEventStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "SqlServer")]
public sealed class SqlServerEventStoreTenantIsolationShould
{
	private const string AggregateType = "Order";

	private readonly SqlServerEventStoreContainerFixture _fixture;

	public SqlServerEventStoreTenantIsolationShould(SqlServerEventStoreContainerFixture fixture) => _fixture = fixture;

	private sealed class FixedTenant(string? tenantId) : ITenantContext
	{
		public string? TenantId { get; } = tenantId;

		public bool HasTenant => TenantId is not null;
	}

	private SqlServerEventStore StoreFor(string? tenantId, string? table = null) =>
		new(
			() => _fixture.CreateConnection(),
			NullLogger<SqlServerEventStore>.Instance,
			schema: _fixture.SchemaName,
			table: table ?? _fixture.TableName,
			tenantContext: tenantId is null ? UntenantedTestTenantContext.Instance : (ITenantContext)new FixedTenant(tenantId));

[MessageName("Test.SqlServerEventStoreTenantIsolation.OrderPlaced")]
private sealed record OrderPlaced(string AggregateId, long Version) : IDomainEvent
	{
		public string EventId { get; init; } = Guid.NewGuid().ToString();
		public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
		public IDictionary<string, object>? Metadata { get; init; }
	}

	[Theory]
	[InlineData(false, false)]
	[InlineData(true, false)]
	[InlineData(true, true)]
	[Trait("Pattern", "Integration")]
	public async Task UseResolvedLocationForBothAppendsAndGlobalReads(bool useAdvancedSource, bool ownedPrimary)
	{
		_fixture.DockerAvailable.ShouldBeTrue();
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);
		var services = new ServiceCollection();
		services.AddLogging();
		var outerFactoryCalls = 0;
		new ExcaliburEventSourcingBuilder(services).UseSqlServer(builder =>
		{
			if (useAdvancedSource)
			{
				Func<Microsoft.Data.SqlClient.SqlConnection> CreateFactory(IServiceProvider _)
				{
					Interlocked.Increment(ref outerFactoryCalls).ShouldBe(1,
						"the outer source factory must be resolved only once for writes, snapshots and global reads");
					return () => _fixture.CreateConnection();
				}
				if (ownedPrimary)
				{
					builder.OwnedPrimaryConnectionFactory(CreateFactory);
				}
				else
				{
					builder.ConnectionFactory(CreateFactory);
				}
			}
			else
			{
				builder.ConnectionString(_fixture.ConnectionString);
			}
			builder
			.EventStoreSchema("unused_builder_schema")
			.EventStoreTable("unused_builder_table")
			.SnapshotStoreSchema("unused_snapshot_schema")
			.SnapshotStoreTable("unused_snapshot_table");
		});
		services.PostConfigure<SqlServerEventSourcingOptions>(options =>
		{
			options.EventStoreSchema = _fixture.SchemaName;
			options.EventStoreTable = _fixture.TableName;
			options.SnapshotStoreSchema = _fixture.SchemaName;
			options.SnapshotStoreTable = "EventStoreSnapshots";
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
		var capability = store.GetService(typeof(IEventStoreAuthoritativeReader));
		if (!useAdvancedSource || ownedPrimary)
		{
			var reader = capability.ShouldBeAssignableTo<IEventStoreAuthoritativeReader>();
			var state = await reader.ReadCurrentAsync(KeyedTenantPartition.FromStoredValue(loaded.Single().TenantId!),
				aggregateId, AggregateType, eventId, 0, CancellationToken.None).ConfigureAwait(false);
			state.ShouldNotBeNull();
			state.EventId.ShouldBe(eventId);
		}
		else
		{
			capability.ShouldBeNull("an ordinary custom factory has not transferred independent connection ownership");
		}
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
		outerFactoryCalls.ShouldBe(useAdvancedSource ? 1 : 0);
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
		await using var copy = new Microsoft.Data.SqlClient.SqlCommand("""
			SELECT EventId, AggregateId, AggregateType, EventType, EventData, Metadata,
			       Version, Timestamp, Position, ArchivedAt,
			       NULLIF(TenantId, '__untenanted__') AS TenantId
			INTO dbo.LegacyAggregateTenantEvents FROM dbo.EventStoreEvents
			""", connection);
		_ = await copy.ExecuteNonQueryAsync();
		try
		{
			var legacyStore = StoreFor(null, "LegacyAggregateTenantEvents");
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
			await using var cleanup = new Microsoft.Data.SqlClient.SqlCommand("DROP TABLE dbo.LegacyAggregateTenantEvents", connection);
			_ = await cleanup.ExecuteNonQueryAsync();
		}
	}

	[Fact]
	public async Task ScopeEveryStreamToItsTenant_OneTenantNeverSeesAnother()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"cross-tenant isolation is a security boundary — this real-SQL Server lock must never be skipped");
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
			.ShouldBeEmpty("tenant B must not load tenant A's event stream (row-level TenantId scoping)");

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
		// SAFETY (18c3el read-leak). The event-read fixes shipped without a non-vacuous SAFETY lock — the
		// existing arms are scoped-vs-scoped + unscoped-LIVENESS only, neither proving an unscoped read does not
		// DISCLOSE a tenant's events. RED against the pre-fix empty predicate; GREEN once the unscoped branch is
		// bounded to the untenanted partition (the __untenanted__ sentinel). Property-based: asserts the disclosure.
		_fixture.DockerAvailable.ShouldBeTrue(
			"cross-tenant read disclosure is a security boundary — this real-SQL-Server lock must never be skipped");
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await _fixture.CleanupTableAsync().ConfigureAwait(false);

		var aggId = "agg-" + Guid.NewGuid().ToString("N");

		_ = await StoreFor("tenant-B").AppendAsync(
			aggId, AggregateType,
			new IDomainEvent[] { new OrderPlaced(aggId, 0), new OrderPlaced(aggId, 1) },
			-1, CancellationToken.None).ConfigureAwait(false);

		(await StoreFor(null).LoadAsync(aggId, AggregateType, -1, CancellationToken.None).ConfigureAwait(false))
			.ShouldBeEmpty(
				"an unscoped reader must not receive a tenant's events — the untenanted partition "
				+ "(the __untenanted__ sentinel, onto which COALESCE folds legacy NULL rows) excludes tenant-scoped rows; the empty-branch predicate disclosed every "
				+ "tenant's events to an unscoped host");
	}
	private sealed record ConfiguredSnapshot(string SnapshotId, string AggregateId, string AggregateType,
		long Version, DateTimeOffset CreatedAt, ReadOnlyMemory<byte> Data,
		IDictionary<string, object>? Metadata, string? TenantId) : ISnapshot;

}
