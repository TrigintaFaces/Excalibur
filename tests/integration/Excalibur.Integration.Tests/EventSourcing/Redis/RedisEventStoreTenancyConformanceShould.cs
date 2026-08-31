// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json.Serialization.Metadata;

using Excalibur.EventSourcing.Redis;
using Excalibur.Testing.Conformance;

using Microsoft.Extensions.DependencyInjection;

using Tests.Shared.Fixtures;

using Xunit;

namespace Excalibur.Integration.Tests.EventSourcing.Redis;

/// <summary>
/// Runs the shipped <see cref="EventStoreConformanceTestKit"/> tenancy arms against a real Redis, through
/// the Redis provider's OWN registration extension.
/// </summary>
/// <remarks>
/// <para>
/// The kit resolves <c>IEventStore</c> from the container these registrations build, so what is under test
/// is the object <c>UseRedis</c> actually produces — not an instance this file assembled. A test that
/// constructed <c>RedisEventStore</c> by hand would prove the store works when handed the right arguments,
/// never that the shipped registration path hands them over.
/// </para>
/// <para>
/// Against a real Redis, not a fake: the stream key shape and the atomicity of the append script are
/// server-side behaviour, and the defect these arms detect lives in the key. NOT skip-gated — a
/// Docker-unavailable run fails rather than passing vacuously.
/// </para>
/// <para>
/// Each test gets its own key prefix, so the arms never collide with each other or with anything else in
/// the shared Redis container.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "EventSourcing")]
[Trait("Database", "Redis")]
// A conformance kit arm carries no runner attribute, so an arm this suite never wraps does not
// run, cannot fail, and reads in the results exactly like one that passed. This suite runs the tenancy arms only, because the defect they detect lives in the Redis key.
// The rest of the event-store contract is exercised against Redis by the sibling below.
// Declared, so the omission is a recorded decision rather than silence:
// conformance-partial-suite: full coverage in RedisEventStoreConformanceShould
public sealed class RedisEventStoreTenancyConformanceShould : EventStoreConformanceTestKit,
	IClassFixture<RedisContainerFixture>
{
	private readonly RedisContainerFixture _fixture;
	private readonly string _keyPrefix = $"conformance-{Guid.NewGuid():N}";

	/// <summary>
	/// Initializes a new instance of the <see cref="RedisEventStoreTenancyConformanceShould"/> class.
	/// </summary>
	/// <param name="fixture">The shared Redis container fixture.</param>
	public RedisEventStoreTenancyConformanceShould(RedisContainerFixture fixture) => _fixture = fixture;

	/// <inheritdoc />
	protected override void ConfigureProvider(
		IServiceCollection services,
		IJsonTypeInfoResolver? eventTypeInfoResolver)
	{
		_ = services.AddExcalibur(x => x.AddEventSourcing(es => es.UseRedis(redis =>
			_ = redis
				.ConnectionString(_fixture.ConnectionString)
				.KeyPrefix(_keyPrefix))));

		_ = services.Configure<RedisEventStoreOptions>(
			options => options.EventTypeInfoResolver = eventTypeInfoResolver);
	}

	/// <summary>
	/// SAFETY: one tenant's events must not be readable by another.
	/// </summary>
	[Fact]
	public Task TenantScopedLoad_MustNotSeeAnotherTenantsEvents_Test() =>
		TenantScopedLoad_MustNotSeeAnotherTenantsEvents();

	/// <summary>
	/// LIVENESS: a tenant must read back its own complete history.
	/// </summary>
	[Fact]
	public Task TenantScopedLoad_MustSeeItsOwnEvents_Test() =>
		TenantScopedLoad_MustSeeItsOwnEvents();

	/// <summary>
	/// The arm that catches a version counter shared across tenants — the shape in which two tenants using
	/// the same aggregate identifier corrupt rather than merely leak.
	/// </summary>
	[Fact]
	public Task TenantPartitions_MustVersionTheSameAggregateIndependently_Test() =>
		TenantPartitions_MustVersionTheSameAggregateIndependently();

	/// <summary>
	/// LIVENESS: a host that established no tenant still round-trips its own events.
	/// </summary>
	[Fact]
	public Task UntenantedPartition_MustRoundTripItsOwnEvents_Test() =>
		UntenantedPartition_MustRoundTripItsOwnEvents();
}
