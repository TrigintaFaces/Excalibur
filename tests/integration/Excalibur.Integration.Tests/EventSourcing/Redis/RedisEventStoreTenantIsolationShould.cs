// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.EventSourcing;
using Excalibur.EventSourcing.Redis;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using StackExchange.Redis;

using System.Text;

namespace Excalibur.Integration.Tests.EventSourcing.Redis;

/// <summary>
/// Regression lock for <c>arwnbi</c>: <see cref="RedisEventStore"/> previously carried NO tenant term
/// in its stream key (<c>{prefix}:{aggregateType}:{aggregateId}</c>), unlike every sibling store
/// (<see cref="RedisSnapshotStore"/> in the same package, and every other <c>IEventStore</c>
/// implementation) — so two tenants appending events for the same
/// (<paramref name="aggregateType"/... see facts below), aggregateId shared ONE stream and ONE version
/// counter: a cross-tenant collision/corruption, not merely a read leak.
/// </summary>
/// <remarks>
/// <para>
/// SAFETY: two different tenants appending to the SAME aggregate id do not observe or corrupt each
/// other's stream — each keeps its own independent version counter and its own events, against a real
/// Redis (the atomicity and the key-shape are both server behaviour a mock cannot reproduce).
/// </para>
/// <para>
/// LIVENESS: a single tenant's append/load round-trip still works exactly as before — the fix must not
/// have merely made cross-tenant collisions "correctly fail"; the store must still function normally
/// for the common single- and multi-tenant cases.
/// </para>
/// <para>
/// NOT skip-gated. A Docker-unavailable run FAILS rather than passing vacuously — a concurrency/
/// isolation lock that never touched a real server would certify nothing.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "EventSourcing")]
[Trait("Database", "Redis")]
public sealed class RedisEventStoreTenantIsolationShould : IClassFixture<RedisContainerFixture>
{
	private const string AggregateType = "TenantIsolationTestAggregate";

	private readonly RedisContainerFixture _fixture;

	public RedisEventStoreTenantIsolationShould(RedisContainerFixture fixture) => _fixture = fixture;

	/// <summary>
	/// SAFETY: two tenants appending events for the SAME aggregate id each get their own stream and
	/// their own version counter — the collision this bead exists to close.
	/// </summary>
	[Fact]
	public async Task KeepTwoTenantsAppendingTheSameAggregateIdCompletelyIsolated()
	{
		RequireDocker();

		await using var connection = await ConnectionMultiplexer.ConnectAsync(_fixture.ConnectionString);
		var prefix = $"es-tenant-{Guid.NewGuid():N}";
		var acme = CreateStore(connection, prefix, new NamedTenantContext("acme"));
		var globex = CreateStore(connection, prefix, new NamedTenantContext("globex"));

		// Deliberately the SAME aggregate id for both tenants — this is exactly the shape that used to
		// share one Redis stream and one version counter.
		var aggregateId = Guid.NewGuid().ToString();

		var acmeCreate = await acme.AppendAsync(
			aggregateId, AggregateType, [new TestDomainEvent(aggregateId, "acme-1")], expectedVersion: -1,
			CancellationToken.None);
		acmeCreate.Success.ShouldBeTrue("acme's create must succeed on its own, isolated stream");
		acmeCreate.NextExpectedVersion.ShouldBe(0);

		// If the two tenants shared a stream/counter, globex's own -1 create would now collide with
		// acme's just-created stream and report a concurrency conflict instead of succeeding.
		var globexCreate = await globex.AppendAsync(
			aggregateId, AggregateType, [new TestDomainEvent(aggregateId, "globex-1")], expectedVersion: -1,
			CancellationToken.None);
		globexCreate.Success.ShouldBeTrue(
			"globex must be able to create the SAME aggregate id independently of acme — shared identity across " +
			"tenants is not a leak, it is a corruption of both tenants' streams");
		globexCreate.NextExpectedVersion.ShouldBe(0, "globex's own stream starts fresh at version 0, unaffected by acme's append");

		var acmeEvents = await acme.LoadAsync(aggregateId, AggregateType, CancellationToken.None);
		var globexEvents = await globex.LoadAsync(aggregateId, AggregateType, CancellationToken.None);

		acmeEvents.Count.ShouldBe(1, "acme must see only its own event");
		Encoding.UTF8.GetString(acmeEvents[0].EventData!).ShouldContain("acme-1");

		globexEvents.Count.ShouldBe(1, "globex must see only its own event, never acme's");
		Encoding.UTF8.GetString(globexEvents[0].EventData!).ShouldContain("globex-1");

		// Append a second event to each independently — proves the version counters are genuinely
		// separate, not merely the first write.
		var acmeSecond = await acme.AppendAsync(
			aggregateId, AggregateType, [new TestDomainEvent(aggregateId, "acme-2")], expectedVersion: 0,
			CancellationToken.None);
		acmeSecond.Success.ShouldBeTrue("acme's own version counter must have advanced to 0 independently of globex");

		var globexReloaded = await globex.LoadAsync(aggregateId, AggregateType, CancellationToken.None);
		globexReloaded.Count.ShouldBe(1, "acme's second append must never appear on globex's stream");
	}

	/// <summary>
	/// LIVENESS: a single tenant's ordinary append/load round-trip is unaffected by the fix.
	/// </summary>
	[Fact]
	public async Task StillRoundTripNormallyForASingleTenant()
	{
		RequireDocker();

		await using var connection = await ConnectionMultiplexer.ConnectAsync(_fixture.ConnectionString);
		var prefix = $"es-tenant-{Guid.NewGuid():N}";
		var store = CreateStore(connection, prefix, new NamedTenantContext("acme"));

		var aggregateId = Guid.NewGuid().ToString();

		var created = await store.AppendAsync(
			aggregateId, AggregateType, [new TestDomainEvent(aggregateId, "e1"), new TestDomainEvent(aggregateId, "e2")],
			expectedVersion: -1, CancellationToken.None);

		created.Success.ShouldBeTrue();
		created.NextExpectedVersion.ShouldBe(1);

		var loaded = await store.LoadAsync(aggregateId, AggregateType, CancellationToken.None);
		loaded.Count.ShouldBe(2, "an ordinary single-tenant round-trip must be unaffected by the tenant-key fix");
	}

	private void RequireDocker() =>
		_fixture.DockerAvailable.ShouldBeTrue(
			"this lock asserts a cross-tenant data-isolation property against real Redis atomicity and is " +
			"deliberately never skipped — a green run that never reached a server would certify nothing.");

	private static RedisEventStore CreateStore(ConnectionMultiplexer connection, string prefix, ITenantContext tenantContext)
	{
		var options = Options.Create(new RedisEventStoreOptions
		{
			ConnectionString = "unused-in-this-fixture",
			StreamKeyPrefix = prefix,
			DatabaseIndex = -1,
		});

		return new RedisEventStore(connection, options, NullLogger<RedisEventStore>.Instance, tenantContext);
	}

	[MessageName("Test.RedisEventStoreTenantIsolation.TestDomainEvent")]
	private sealed record TestDomainEvent : IDomainEvent
	{
		public TestDomainEvent(string aggregateId, string marker)
		{
			EventId = Guid.NewGuid().ToString();
			AggregateId = aggregateId;
			Version = 0;
			OccurredAt = DateTimeOffset.UtcNow;
			Marker = marker;
		}

		/// <summary>
		/// Distinguishes one tenant's event from the other's. This used to ride on the event's own
		/// EventType property; that property is gone, and the stored event-type field now holds the
		/// type's declared name -- identical for both tenants, so it can no longer tell them apart.
		/// The marker is payload, which is where a per-instance value belongs.
		/// </summary>
		public string Marker { get; init; }

		public string EventId { get; init; }
		public string AggregateId { get; init; }
		public long Version { get; init; }
		public DateTimeOffset OccurredAt { get; init; }
		public IDictionary<string, object>? Metadata => null;
	}

	/// <summary>A context resolving a real, named tenant.</summary>
	private sealed class NamedTenantContext(string tenantId) : ITenantContext
	{
		public string? TenantId { get; } = tenantId;

		public bool HasTenant => true;
	}
}
