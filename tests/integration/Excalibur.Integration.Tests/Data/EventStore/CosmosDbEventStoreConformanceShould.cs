// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.Json.Serialization.Metadata;

using Excalibur.EventSourcing.CosmosDb;
using Excalibur.Testing.Conformance;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Integration.Tests.Data.EventStore;

/// <summary>
/// Runs the <strong>complete</strong> <see cref="EventStoreConformanceTestKit"/> - every arm - against the
/// real Cosmos DB emulator, through the Cosmos provider's OWN registration extension.
/// </summary>
/// <remarks>
/// <para>
/// This suite previously derived a conformance base that lives under <c>tests/</c>. That base is not
/// shipped, so the contract it enforced was one no consumer could obtain: a third party writing their own
/// event store had no way to run the same arms, and the two contracts were free to drift with nothing
/// detecting it. Deriving the published kit makes the contract this provider is held to identical to the
/// one a consumer is handed, and adds four tenancy arms this provider had no coverage for at all.
/// </para>
/// <para>
/// The kit resolves <c>IEventStore</c> from a container built by <c>UseCosmosDb</c>, so what every arm
/// asserts against is the object the shipped registration actually produces. The previous suite handed the
/// store a <c>CosmosClient</c> it had constructed, which proves the store works when given the right
/// client - never that the registration supplies one.
/// </para>
/// <para>
/// Isolation is by a per-instance Cosmos container rather than by deleting documents between arms. xUnit
/// builds a fresh instance per test method, so each arm already starts against an empty container, and
/// <c>ResetDataAsync</c> is therefore a no-op. It must be: the kit clears data BEFORE an arm runs, and
/// dropping the container at that point would remove the one the arm is about to write to. The container
/// is deleted in <c>DisposeAsync</c> instead, the only point at which nothing still needs it.
/// </para>
/// <para>
/// Against the real emulator, not a fake: concurrency-conflict detection and the default-serializer round
/// trip are both server-side. The emulator is required, never skip-gated.
/// </para>
/// </remarks>
[Collection(CosmosDbEventStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Infrastructure", "CosmosEmulator")]
[Trait("Database", "CosmosDb")]
public sealed class CosmosDbEventStoreConformanceShould : EventStoreConformanceTestKit,
	IClassFixture<CosmosDbEventStoreContainerFixture>, IAsyncLifetime
{
	private readonly CosmosDbEventStoreContainerFixture _fixture;
	private readonly string _containerName = $"events_{Guid.NewGuid():N}";

	/// <summary>
	/// Initializes a new instance of the <see cref="CosmosDbEventStoreConformanceShould"/> class.
	/// </summary>
	/// <param name="fixture">The Cosmos DB emulator fixture.</param>
	public CosmosDbEventStoreConformanceShould(CosmosDbEventStoreContainerFixture fixture) => _fixture = fixture;

	/// <inheritdoc />
	public ValueTask InitializeAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"Cosmos DB event-store conformance exercises real concurrency-conflict detection and "
			+ "default-serializer round-trip behaviour - this real-emulator suite must never be skipped.");

		return ValueTask.CompletedTask;
	}

	/// <summary>Drops this instance's container once every arm that needed it has finished.</summary>
	/// <returns>A task that completes when the container is gone.</returns>
	public async ValueTask DisposeAsync() =>
		await _fixture.DeleteContainerAsync(_containerName).ConfigureAwait(false);

	/// <inheritdoc />
	/// <remarks>
	/// <c>DatabaseName</c> is load-bearing: the builder writes it onto the options and the store opens that
	/// database, so this suite runs against the database the fixture names rather than a hardcoded one.
	/// </remarks>
	protected override void ConfigureProvider(
		IServiceCollection services,
		IJsonTypeInfoResolver? eventTypeInfoResolver)
	{
		_ = services.AddExcalibur(x => x.AddEventSourcing(es => es.UseCosmosDb(cosmos =>
			_ = cosmos
				.Client(_fixture.Client)
				.DatabaseName(_fixture.DatabaseName)
				.ContainerName(_containerName))));

		// The Cosmos builder exposes no resolver method, so the consumer surface is the options type the
		// registration reads. Configure runs after the registration above and never touches this property
		// itself, so setting it here is order-independent.
		_ = services.Configure<CosmosDbEventStoreOptions>(
			options => options.EventTypeInfoResolver = eventTypeInfoResolver);
	}

	/// <summary>
	/// An event type the configured resolver does not declare is refused by throwing, and nothing is
	/// written.
	/// </summary>
	[Fact]
	public Task AppendAsync_EventTypeTheResolverDoesNotDeclare_ShouldThrowAndWriteNothing_Test() =>
		AppendAsync_EventTypeTheResolverDoesNotDeclare_ShouldThrowAndWriteNothing();

	/// <inheritdoc />
	/// <remarks>Cosmos DB caps a transactional batch at 100 operations and offers no larger atomic primitive.</remarks>
	protected override int? AtomicAppendLimit => 100;

	/// <summary>SAFETY: an append above this provider's atomic limit is refused whole rather than committed in pieces.</summary>
	[Fact]
	public Task AppendAsync_AboveTheAtomicLimit_ShouldRefuseWholeOrAppendAtomically_Test() =>
		AppendAsync_AboveTheAtomicLimit_ShouldRefuseWholeOrAppendAtomically();


	/// <summary>
	/// A no-op: each instance owns a private container, so an arm never starts against another arm's data.
	/// </summary>
	/// <returns>A completed task.</returns>
	protected override Task ResetDataAsync() => Task.CompletedTask;

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
