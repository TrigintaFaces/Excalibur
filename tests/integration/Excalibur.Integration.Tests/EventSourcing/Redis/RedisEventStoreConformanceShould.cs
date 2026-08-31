// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json.Serialization.Metadata;

using Excalibur.EventSourcing.Redis;
using Excalibur.Testing.Conformance;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Tests.Shared.Fixtures;

using Xunit;

namespace Excalibur.Integration.Tests.EventSourcing.Redis;

/// <summary>
/// Runs the <strong>complete</strong> <see cref="EventStoreConformanceTestKit"/> — every arm — against a
/// real Redis, through the Redis provider's OWN registration extension.
/// </summary>
/// <remarks>
/// <para>
/// Its sibling tenancy suite wires only the four tenancy arms, which is its stated and correct scope. The
/// consequence was that the other fifteen arms — the append, load, isolation, and round-trip contract —
/// had no deriving suite anywhere in the repository, so they had never executed against any event store.
/// An arm that never executes cannot fail, and is indistinguishable in a results summary from one that
/// passed. This suite closes that gap by wiring the whole kit, and it wires
/// <see cref="EventStoreConformanceTestKit.ConformanceSuite_ShouldWireEveryArm"/> so an arm added to the
/// kit later and forgotten here fails loudly rather than silently not running.
/// </para>
/// <para>
/// The kit resolves <c>IEventStore</c> from the container these registrations build, so what is under test
/// is the object <c>UseRedis</c> actually produces — not an instance this file assembled. A test that
/// constructed the store by hand would prove it works when handed the right arguments, never that the
/// shipped registration path hands them over.
/// </para>
/// <para>
/// Against a real Redis, not a fake: the append script's optimistic-concurrency check, the authoritative
/// version counter, the stream key shape, and the JSON round trip are all server-side behaviour.
/// <c>DockerAvailable.ShouldBeTrue</c> makes every arm NON-SKIPPED — a Docker-unavailable run fails rather
/// than passing vacuously.
/// </para>
/// <para>
/// xUnit constructs a fresh instance per test method, so the per-instance key prefix below gives every arm
/// its own Redis keyspace, and aggregate identifiers are freshly generated on top of that. No arm can
/// contaminate another, nor anything else sharing the container.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "EventSourcing")]
[Trait("Database", "Redis")]
public sealed class RedisEventStoreConformanceShould : EventStoreConformanceTestKit,
	IClassFixture<RedisContainerFixture>
{
	private readonly RedisContainerFixture _fixture;
	private readonly string _keyPrefix = $"conformance-full-{Guid.NewGuid():N}";

	/// <summary>
	/// Initializes a new instance of the <see cref="RedisEventStoreConformanceShould"/> class.
	/// </summary>
	/// <param name="fixture">The shared Redis container fixture.</param>
	public RedisEventStoreConformanceShould(RedisContainerFixture fixture) => _fixture = fixture;

	/// <inheritdoc />
	protected override void ConfigureProvider(
		IServiceCollection services,
		IJsonTypeInfoResolver? eventTypeInfoResolver)
	{
		_ = services.AddExcalibur(x => x.AddEventSourcing(es => es.UseRedis(redis =>
			_ = redis
				.ConnectionString(_fixture.ConnectionString)
				.KeyPrefix(_keyPrefix))));

		// The Redis builder exposes no resolver method, so the consumer surface is the options type the
		// registration reads.
		_ = services.Configure<RedisEventStoreOptions>(
			options => options.EventTypeInfoResolver = eventTypeInfoResolver);
	}

	/// <summary>
	/// An event type the configured resolver does not declare is refused by throwing, and nothing is
	/// written.
	/// </summary>
	[Fact]
	public Task AppendAsync_EventTypeTheResolverDoesNotDeclare_ShouldThrowAndWriteNothing_Test() =>
		AppendAsync_EventTypeTheResolverDoesNotDeclare_ShouldThrowAndWriteNothing();

	/// <summary>SAFETY: an append above this provider's atomic limit is refused whole rather than committed in pieces.</summary>
	[Fact]
	public Task AppendAsync_AboveTheAtomicLimit_ShouldRefuseWholeOrAppendAtomically_Test() =>
		AppendAsync_AboveTheAtomicLimit_ShouldRefuseWholeOrAppendAtomically();


	// NEVER skip-gated. A conformance arm that passes by not running is the gap that ships the bug.
	private void RequireRealRedis() =>
		_fixture.DockerAvailable.ShouldBeTrue(
			"The event-store conformance contract is verified against a real Redis — its concurrency, "
			+ "versioning and round-trip behaviour are server-side. This suite must never be skipped.");

	#region Append arms

	/// <summary>Appending to a stream that does not yet exist succeeds and reports the new version.</summary>
	[Fact]
	public Task AppendAsync_ToNewStream_ShouldSucceed_Test()
	{
		RequireRealRedis();
		return AppendAsync_ToNewStream_ShouldSucceed();
	}

	/// <summary>An append at the stream's current version succeeds and advances it.</summary>
	[Fact]
	public Task AppendAsync_WithCorrectExpectedVersion_ShouldSucceed_Test()
	{
		RequireRealRedis();
		return AppendAsync_WithCorrectExpectedVersion_ShouldSucceed();
	}

	/// <summary>SAFETY: an append at a stale version is rejected as a conflict, not silently applied.</summary>
	[Fact]
	public Task AppendAsync_WithWrongExpectedVersion_ShouldReturnConcurrencyConflict_Test()
	{
		RequireRealRedis();
		return AppendAsync_WithWrongExpectedVersion_ShouldReturnConcurrencyConflict();
	}

	/// <summary>An append of no events is a no-op that leaves the version untouched.</summary>
	[Fact]
	public Task AppendAsync_EmptyEvents_ShouldNotChangeVersion_Test()
	{
		RequireRealRedis();
		return AppendAsync_EmptyEvents_ShouldNotChangeVersion();
	}

	/// <summary>SAFETY: under a race at one expected version, exactly one writer wins and the rest conflict.</summary>
	[Fact]
	public Task ConcurrentAppend_SameExpectedVersion_OnlyOneShouldSucceed_Test()
	{
		RequireRealRedis();
		return ConcurrentAppend_SameExpectedVersion_OnlyOneShouldSucceed();
	}

	/// <summary>LIVENESS: independent aggregates do not falsely conflict with one another.</summary>
	[Fact]
	public Task ConcurrentAppend_DifferentAggregates_AllShouldSucceed_Test()
	{
		RequireRealRedis();
		return ConcurrentAppend_DifferentAggregates_AllShouldSucceed();
	}

	/// <summary>SAFETY: a null, empty or whitespace aggregate identifier is rejected rather than written to a stream no reader can name.</summary>
	[Fact]
	public Task AppendAsync_UnaddressableAggregateId_ShouldThrow_Test()
	{
		RequireRealRedis();
		return AppendAsync_UnaddressableAggregateId_ShouldThrow();
	}

	/// <summary>SAFETY: an append past the stream tail is refused as a conflict rather than leaving a gap in the version sequence.</summary>
	[Fact]
	public Task AppendAsync_WithExpectedVersionBeyondTail_ShouldReturnConcurrencyConflict_Test()
	{
		RequireRealRedis();
		return AppendAsync_WithExpectedVersionBeyondTail_ShouldReturnConcurrencyConflict();
	}

	/// <summary>SAFETY: an append to a stream that does not exist is refused unless it claims the empty stream.</summary>
	[Fact]
	public Task AppendAsync_NonExistentStream_WithWrongExpectedVersion_ShouldReturnConcurrencyConflict_Test()
	{
		RequireRealRedis();
		return AppendAsync_NonExistentStream_WithWrongExpectedVersion_ShouldReturnConcurrencyConflict();
	}

	#endregion

	#region Load arms

	/// <summary>A stream that was never written reads back empty rather than faulting.</summary>
	[Fact]
	public Task LoadAsync_EmptyStream_ShouldReturnEmpty_Test()
	{
		RequireRealRedis();
		return LoadAsync_EmptyStream_ShouldReturnEmpty();
	}

	/// <summary>LIVENESS: a written stream reads back its complete history.</summary>
	[Fact]
	public Task LoadAsync_ExistingStream_ShouldReturnAllEvents_Test()
	{
		RequireRealRedis();
		return LoadAsync_ExistingStream_ShouldReturnAllEvents();
	}

	/// <summary>Events read back in strictly ascending version order — replay depends on it.</summary>
	[Fact]
	public Task LoadAsync_ShouldReturnEventsInVersionOrder_Test()
	{
		RequireRealRedis();
		return LoadAsync_ShouldReturnEventsInVersionOrder();
	}

	/// <summary>A from-version read returns exactly the events after that version.</summary>
	[Fact]
	public Task LoadAsync_FromVersion_ShouldReturnEventsAfterVersion_Test()
	{
		RequireRealRedis();
		return LoadAsync_FromVersion_ShouldReturnEventsAfterVersion();
	}

	/// <summary>A from-version read past the end of the stream is empty, not an error.</summary>
	[Fact]
	public Task LoadAsync_FromVersionBeyondStream_ShouldReturnEmpty_Test()
	{
		RequireRealRedis();
		return LoadAsync_FromVersionBeyondStream_ShouldReturnEmpty();
	}

	/// <summary>The from-version bound is exclusive: loading from version zero returns every event after the first, and not the first.</summary>
	[Fact]
	public Task LoadAsync_FromVersionZero_ShouldReturnAllExceptTheFirst_Test()
	{
		RequireRealRedis();
		return LoadAsync_FromVersionZero_ShouldReturnAllExceptTheFirst();
	}

	/// <summary>LIVENESS: many callers arriving at once on a cold store all succeed rather than racing its initialisation.</summary>
	[Fact]
	public Task ConcurrentFirstUse_ShouldNotFault_Test()
	{
		RequireRealRedis();
		return ConcurrentFirstUse_ShouldNotFault();
	}

	#endregion

	#region Isolation arms

	/// <summary>SAFETY: one aggregate identifier under two types addresses two separate streams.</summary>
	[Fact]
	public Task LoadAsync_ShouldIsolateByAggregateType_Test()
	{
		RequireRealRedis();
		return LoadAsync_ShouldIsolateByAggregateType();
	}

	/// <summary>SAFETY: two aggregates of one type do not bleed into each other's history.</summary>
	[Fact]
	public Task LoadAsync_ShouldIsolateByAggregateId_Test()
	{
		RequireRealRedis();
		return LoadAsync_ShouldIsolateByAggregateId();
	}

	#endregion

	#region Round-trip arms

	/// <summary>An event's identity and version survive the write/read round trip intact.</summary>
	[Fact]
	public Task AppendAndLoad_ShouldPreserveEventData_Test()
	{
		RequireRealRedis();
		return AppendAndLoad_ShouldPreserveEventData();
	}

	/// <summary>Event metadata survives the write/read round trip — it is not silently dropped.</summary>
	[Fact]
	public Task AppendAndLoad_ShouldPreserveMetadata_Test()
	{
		RequireRealRedis();
		return AppendAndLoad_ShouldPreserveMetadata();
	}

	#endregion

	#region Tenancy arms

	/// <summary>SAFETY: one tenant's events must not be readable by another.</summary>
	[Fact]
	public Task TenantScopedLoad_MustNotSeeAnotherTenantsEvents_Test()
	{
		RequireRealRedis();
		return TenantScopedLoad_MustNotSeeAnotherTenantsEvents();
	}

	/// <summary>LIVENESS: a tenant must read back its own complete history.</summary>
	[Fact]
	public Task TenantScopedLoad_MustSeeItsOwnEvents_Test()
	{
		RequireRealRedis();
		return TenantScopedLoad_MustSeeItsOwnEvents();
	}

	/// <summary>Two tenants sharing an aggregate identifier must version it independently.</summary>
	[Fact]
	public Task TenantPartitions_MustVersionTheSameAggregateIndependently_Test()
	{
		RequireRealRedis();
		return TenantPartitions_MustVersionTheSameAggregateIndependently();
	}

	/// <summary>LIVENESS: a host that established no tenant still round-trips its own events.</summary>
	[Fact]
	public Task UntenantedPartition_MustRoundTripItsOwnEvents_Test()
	{
		RequireRealRedis();
		return UntenantedPartition_MustRoundTripItsOwnEvents();
	}

	#endregion

	/// <summary>
	/// The harness guard: fails if this suite has left any kit arm unwired, so an arm added to the kit
	/// later cannot silently never run here.
	/// </summary>
	[Fact]
	public Task ConformanceSuite_ShouldWireEveryArm_Test() => ConformanceSuite_ShouldWireEveryArm();
}
