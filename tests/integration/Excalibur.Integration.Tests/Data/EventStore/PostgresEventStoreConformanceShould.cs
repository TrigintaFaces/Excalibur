// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json.Serialization.Metadata;

using Excalibur.EventSourcing.Postgres;
using Excalibur.Testing.Conformance;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Integration.Tests.Data.EventStore;

/// <summary>
/// Runs the <strong>complete</strong> <see cref="EventStoreConformanceTestKit"/> - every arm - against a
/// real Postgres, through the Postgres provider's OWN registration extension.
/// </summary>
/// <remarks>
/// <para>
/// This suite previously derived a conformance base that lives under <c>tests/</c>. That base is not
/// shipped, so the contract it enforced was one no consumer could obtain: a third party writing their own
/// event store had no way to run the same arms, and the two contracts were free to drift with nothing
/// detecting it. Deriving the published kit makes the contract this provider is held to identical to the
/// one a consumer is handed.
/// </para>
/// <para>
/// The repoint also strengthens what is verified. The kit resolves <c>IEventStore</c> from a container
/// built by <c>UsePostgres</c>, so what every arm asserts against is the object the shipped registration
/// actually produces. The previous suite constructed <c>PostgresEventStore</c> by hand, which proves the
/// store works when handed the right arguments - never that the registration hands them over. It also adds
/// four tenancy arms this provider had no conformance coverage for at all.
/// </para>
/// <para>
/// Against a real Postgres, not a fake: optimistic-concurrency detection, the authoritative version
/// counter, and the row-level tenant predicate are all server-side behaviour. The container is required,
/// never skip-gated - an arm that passes by not running is the gap that ships the bug.
/// </para>
/// </remarks>
[Collection(PostgresEventStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "Postgres")]
public sealed class PostgresEventStoreConformanceShould : EventStoreConformanceTestKit,
	IClassFixture<PostgresEventStoreContainerFixture>, IAsyncLifetime
{
	private readonly PostgresEventStoreContainerFixture _fixture;

	/// <summary>
	/// Initializes a new instance of the <see cref="PostgresEventStoreConformanceShould"/> class.
	/// </summary>
	/// <param name="fixture">The Postgres container fixture.</param>
	public PostgresEventStoreConformanceShould(PostgresEventStoreContainerFixture fixture) => _fixture = fixture;

	/// <summary>
	/// Brings the container and its schema up before any arm resolves the provider.
	/// </summary>
	/// <returns>A task that completes when Postgres is reachable and the event table exists.</returns>
	/// <remarks>
	/// The kit's provider seam is synchronous by design - the package targets no test framework and cannot
	/// own an async lifecycle. Awaiting the container here, in the runner's own lifecycle hook, is what lets
	/// a container-backed provider derive the kit at all.
	/// </remarks>
	public async ValueTask InitializeAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"The event-store conformance contract is verified against a real Postgres - its concurrency, "
			+ "versioning and tenant-predicate behaviour are server-side. This suite must never be skipped.");

		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
	}

	/// <inheritdoc />
	public ValueTask DisposeAsync() => ValueTask.CompletedTask;

	/// <inheritdoc />
	protected override void ConfigureProvider(
		IServiceCollection services,
		IJsonTypeInfoResolver? eventTypeInfoResolver)
		=> _ = services.AddExcalibur(x => x.AddEventSourcing(es => es.UsePostgres(pg =>
		{
			_ = pg
				.ConnectionString(_fixture.ConnectionString)
				.EventStoreSchema("public")
				.EventStoreTable(_fixture.TableName);

			if (eventTypeInfoResolver is not null)
			{
				_ = pg.EventTypeInfoResolver(eventTypeInfoResolver);
			}
		})));

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


	/// <inheritdoc />
	protected override async Task CleanupAsync() =>
		await _fixture.CleanupTableAsync().ConfigureAwait(false);

	#region Append arms

	/// <summary>Appending to a stream that does not yet exist succeeds and reports the new version.</summary>
	[Fact]
	public Task AppendAsync_ToNewStream_ShouldSucceed_Test() =>
		AppendAsync_ToNewStream_ShouldSucceed();

	/// <summary>An append at the stream's current version succeeds and advances it.</summary>
	[Fact]
	public Task AppendAsync_WithCorrectExpectedVersion_ShouldSucceed_Test() =>
		AppendAsync_WithCorrectExpectedVersion_ShouldSucceed();

	/// <summary>SAFETY: an append at a stale version is rejected as a conflict, not silently applied.</summary>
	[Fact]
	public Task AppendAsync_WithWrongExpectedVersion_ShouldReturnConcurrencyConflict_Test() =>
		AppendAsync_WithWrongExpectedVersion_ShouldReturnConcurrencyConflict();

	/// <summary>An append of no events is a no-op that leaves the version untouched.</summary>
	[Fact]
	public Task AppendAsync_EmptyEvents_ShouldNotChangeVersion_Test() =>
		AppendAsync_EmptyEvents_ShouldNotChangeVersion();

	/// <summary>SAFETY: under a race at one expected version, exactly one writer wins and the rest conflict.</summary>
	[Fact]
	public Task ConcurrentAppend_SameExpectedVersion_OnlyOneShouldSucceed_Test() =>
		ConcurrentAppend_SameExpectedVersion_OnlyOneShouldSucceed();

	/// <summary>LIVENESS: independent aggregates do not falsely conflict with one another.</summary>
	[Fact]
	public Task ConcurrentAppend_DifferentAggregates_AllShouldSucceed_Test() =>
		ConcurrentAppend_DifferentAggregates_AllShouldSucceed();

	/// <summary>SAFETY: a null, empty or whitespace aggregate identifier is rejected rather than written to a stream no reader can name.</summary>
	[Fact]
	public Task AppendAsync_UnaddressableAggregateId_ShouldThrow_Test() =>
		AppendAsync_UnaddressableAggregateId_ShouldThrow();

	/// <summary>SAFETY: an append past the stream tail is refused as a conflict rather than leaving a gap in the version sequence.</summary>
	[Fact]
	public Task AppendAsync_WithExpectedVersionBeyondTail_ShouldReturnConcurrencyConflict_Test() =>
		AppendAsync_WithExpectedVersionBeyondTail_ShouldReturnConcurrencyConflict();

	/// <summary>SAFETY: an append to a stream that does not exist is refused unless it claims the empty stream.</summary>
	[Fact]
	public Task AppendAsync_NonExistentStream_WithWrongExpectedVersion_ShouldReturnConcurrencyConflict_Test() =>
		AppendAsync_NonExistentStream_WithWrongExpectedVersion_ShouldReturnConcurrencyConflict();

	#endregion

	#region Load arms

	/// <summary>A stream that was never written reads back empty rather than faulting.</summary>
	[Fact]
	public Task LoadAsync_EmptyStream_ShouldReturnEmpty_Test() =>
		LoadAsync_EmptyStream_ShouldReturnEmpty();

	/// <summary>LIVENESS: a written stream reads back its complete history.</summary>
	[Fact]
	public Task LoadAsync_ExistingStream_ShouldReturnAllEvents_Test() =>
		LoadAsync_ExistingStream_ShouldReturnAllEvents();

	/// <summary>Events read back in strictly ascending version order - replay depends on it.</summary>
	[Fact]
	public Task LoadAsync_ShouldReturnEventsInVersionOrder_Test() =>
		LoadAsync_ShouldReturnEventsInVersionOrder();

	/// <summary>A from-version read returns exactly the events after that version.</summary>
	[Fact]
	public Task LoadAsync_FromVersion_ShouldReturnEventsAfterVersion_Test() =>
		LoadAsync_FromVersion_ShouldReturnEventsAfterVersion();

	/// <summary>A from-version read past the end of the stream is empty, not an error.</summary>
	[Fact]
	public Task LoadAsync_FromVersionBeyondStream_ShouldReturnEmpty_Test() =>
		LoadAsync_FromVersionBeyondStream_ShouldReturnEmpty();

	/// <summary>The from-version bound is exclusive: loading from version zero returns every event after the first, and not the first.</summary>
	[Fact]
	public Task LoadAsync_FromVersionZero_ShouldReturnAllExceptTheFirst_Test() =>
		LoadAsync_FromVersionZero_ShouldReturnAllExceptTheFirst();

	/// <summary>LIVENESS: many callers arriving at once on a cold store all succeed rather than racing its initialisation.</summary>
	[Fact]
	public Task ConcurrentFirstUse_ShouldNotFault_Test() =>
		ConcurrentFirstUse_ShouldNotFault();

	#endregion

	#region Isolation arms

	/// <summary>SAFETY: one aggregate identifier under two types addresses two separate streams.</summary>
	[Fact]
	public Task LoadAsync_ShouldIsolateByAggregateType_Test() =>
		LoadAsync_ShouldIsolateByAggregateType();

	/// <summary>SAFETY: two aggregates of one type do not bleed into each other's history.</summary>
	[Fact]
	public Task LoadAsync_ShouldIsolateByAggregateId_Test() =>
		LoadAsync_ShouldIsolateByAggregateId();

	#endregion

	#region Round-trip arms

	/// <summary>An event's identity and version survive the write/read round trip intact.</summary>
	[Fact]
	public Task AppendAndLoad_ShouldPreserveEventData_Test() =>
		AppendAndLoad_ShouldPreserveEventData();

	/// <summary>Event metadata survives the write/read round trip - it is not silently dropped.</summary>
	[Fact]
	public Task AppendAndLoad_ShouldPreserveMetadata_Test() =>
		AppendAndLoad_ShouldPreserveMetadata();

	#endregion

	#region Tenancy arms

	/// <summary>SAFETY: one tenant's events must not be readable by another.</summary>
	[Fact]
	public Task TenantScopedLoad_MustNotSeeAnotherTenantsEvents_Test() =>
		TenantScopedLoad_MustNotSeeAnotherTenantsEvents();

	/// <summary>LIVENESS: a tenant must read back its own complete history.</summary>
	[Fact]
	public Task TenantScopedLoad_MustSeeItsOwnEvents_Test() =>
		TenantScopedLoad_MustSeeItsOwnEvents();

	/// <summary>Two tenants sharing an aggregate identifier must version it independently.</summary>
	[Fact]
	public Task TenantPartitions_MustVersionTheSameAggregateIndependently_Test() =>
		TenantPartitions_MustVersionTheSameAggregateIndependently();

	/// <summary>LIVENESS: a host that established no tenant still round-trips its own events.</summary>
	[Fact]
	public Task UntenantedPartition_MustRoundTripItsOwnEvents_Test() =>
		UntenantedPartition_MustRoundTripItsOwnEvents();

	#endregion

	/// <summary>
	/// The harness guard: fails if this suite has left any kit arm unwired, so an arm added to the kit
	/// later cannot silently never run here.
	/// </summary>
	[Fact]
	public Task ConformanceSuite_ShouldWireEveryArm_Test() => ConformanceSuite_ShouldWireEveryArm();
}
