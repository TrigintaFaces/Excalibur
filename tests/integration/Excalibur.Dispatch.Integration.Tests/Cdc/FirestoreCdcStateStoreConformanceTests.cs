// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc.Firestore;
using Excalibur.Dispatch.Integration.Tests.DispatchCore.Providers.Firestore;
using Excalibur.Testing.Conformance;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Integration.Tests.Cdc;

/// <summary>
/// Runs the shared CDC state-store conformance kit against the REAL <see cref="FirestoreCdcStateStore"/> on
/// the Firestore emulator.
/// </summary>
/// <remarks>
/// <para>
/// Excalibur_Dispatch-5oy7c9: before this deriver, no arm of <see cref="CdcProviderConformanceTestKit"/> had
/// ever run against <see cref="FirestoreCdcStateStore"/>. That mattered beyond coverage because Firestore is
/// the one CDC store that deliberately REFUSES a stale position: <c>SavePositionAsync</c> wraps its write in
/// a native Firestore transaction and throws <see cref="FirestoreStalePositionException"/> rather than
/// regress a watermark a concurrent writer already advanced past (see
/// <see cref="FirestoreCdcStateStoreOptimisticConcurrencyIntegrationShould"/> for the dedicated lock on that
/// guard). The kit's own <c>ConcurrentSavePosition_SameConsumer_LastWriteWins</c> arm used to run every
/// concurrent write through <c>Task.WhenAll</c> and required that nothing throw, which would have made this
/// store's correct behavior look like conformance failure.
/// </para>
/// <para>
/// ARCHITECT RULING (recorded on 5oy7c9): the kit tolerates a documented monotonic refusal rather than the
/// store being weakened to satisfy an under-specified arm. The arm never asserted which write won, only that
/// the concurrent saves complete and the final position is valid — <see cref="ConcurrentSaveMayRefuseAsStale"/>
/// is the kit's extension point for exactly this, and this deriver's override recognizes
/// <see cref="FirestoreStalePositionException"/> as the documented refusal.
/// </para>
/// <para>
/// Positions are constructed with strictly increasing watermarks by index (<see cref="CreateTestPosition"/>),
/// so every SEQUENTIAL kit arm (round-trip, overwrite, resume) never trips the guard — only the concurrent
/// same-consumer arm can race two watermarks against each other, which is exactly the case the guard exists
/// for.
/// </para>
/// </remarks>
[IntegrationTest]
[Collection(FirestoreCdcStateStoreTestCollection.CollectionName)]
[Trait("Component", TestComponents.Core)]
[Trait("Infrastructure", TestInfrastructure.Firestore)]
[Trait(TraitNames.Category, TestCategories.Integration)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class FirestoreCdcStateStoreConformanceTests : CdcProviderConformanceTestKit
{
	private static readonly DateTimeOffset BaseWatermark = DateTimeOffset.UtcNow;

	private readonly FirestoreCdcStateStoreContainerFixture _fixture;

	/// <summary>
	/// This arm's own Firestore collection, distinct from every other test's.
	/// </summary>
	/// <remarks>
	/// The container fixture is an ICollectionFixture, so ONE instance -- and therefore one
	/// fixture.CollectionName -- is shared by every class in the collection. Sharing the container is
	/// deliberate (a per-class container overwrote the single FIRESTORE_EMULATOR_HOST variable), but
	/// sharing the DATA namespace is not: a sibling class's saved positions are still there when this
	/// class runs, and an arm asserting an EMPTY store then reads theirs and fails. It passes in
	/// isolation and fails in the suite, which is the signature.
	///
	/// Scoped to the INSTANCE, not to the call: xUnit builds a new test-class instance per test, so each
	/// arm gets a clean collection, while a single arm that creates two stores -- the concurrent-save
	/// arms do -- still has both aimed at the same data, which is the whole point of those arms.
	/// </remarks>
	private readonly string _collectionName = $"conformance_{Guid.NewGuid():N}";

	public FirestoreCdcStateStoreConformanceTests(FirestoreCdcStateStoreContainerFixture fixture) => _fixture = fixture;

	/// <inheritdoc />
	protected override Task<ICdcStateStore> CreateStateStoreAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"a Firestore emulator must be available - real-infra CDC conformance is never skipped, because "
			+ "an arm that passes by being skipped is indistinguishable from one that passed by working.");

		ICdcStateStore store = new FirestoreCdcStateStore(
			_fixture.Db,
			_collectionName,
			NullLogger<FirestoreCdcStateStore>.Instance);

		return Task.FromResult(store);
	}

	/// <inheritdoc />
	/// <remarks>
	/// Strictly increasing watermarks by index, per <c>CreateTestPosition</c>'s own contract ("a higher index
	/// means a later position"). This is also what keeps every SEQUENTIAL arm clear of the stale-position
	/// guard: two positions never share a watermark unless the caller deliberately reuses an index, which no
	/// arm in this kit does.
	/// </remarks>
	protected override ChangePosition CreateTestPosition(int index) =>
		FirestoreCdcPosition.FromUpdateTime(
			collectionPath: "conformance/documents",
			updateTime: BaseWatermark.AddSeconds(index),
			lastDocumentId: $"doc-{index:D6}");

	/// <inheritdoc />
	/// <remarks>
	/// The documented monotonic refusal this store's <c>SavePositionAsync</c> throws when a concurrent write
	/// already advanced the stored watermark past the one being saved. See the class remarks for why this is
	/// the correct behavior rather than a conformance gap.
	/// </remarks>
	protected override bool ConcurrentSaveMayRefuseAsStale(Exception exception) =>
		exception is FirestoreStalePositionException;

	[Fact] public Task SaveAndGetPosition_RoundTrips_Test() => SaveAndGetPosition_RoundTrips();
	[Fact] public Task GetPosition_NoCheckpoint_ReturnsNull_Test() => GetPosition_NoCheckpoint_ReturnsNull();
	[Fact] public Task SavePosition_MultipleConsumers_Independent_Test() => SavePosition_MultipleConsumers_Independent();
	[Fact] public Task SavePosition_Overwrites_PreviousCheckpoint_Test() => SavePosition_Overwrites_PreviousCheckpoint();
	[Fact] public Task SavePosition_PreservesPositionValidity_Test() => SavePosition_PreservesPositionValidity();
	[Fact] public Task Resume_FromSavedCheckpoint_ReturnsCorrectPosition_Test() => Resume_FromSavedCheckpoint_ReturnsCorrectPosition();
	[Fact] public Task Resume_AfterDelete_ReturnsNull_Test() => Resume_AfterDelete_ReturnsNull();
	[Fact] public Task DeletePosition_ExistingCheckpoint_ReturnsTrue_Test() => DeletePosition_ExistingCheckpoint_ReturnsTrue();
	[Fact] public Task DeletePosition_NonExistentCheckpoint_ReturnsFalse_Test() => DeletePosition_NonExistentCheckpoint_ReturnsFalse();
	[Fact] public Task DeletePosition_DoesNotAffectOtherConsumers_Test() => DeletePosition_DoesNotAffectOtherConsumers();
	[Fact] public Task GetAllPositions_ReturnsAllConsumerCheckpoints_Test() => GetAllPositions_ReturnsAllConsumerCheckpoints();
	[Fact] public Task GetAllPositions_EmptyStore_ReturnsEmpty_Test() => GetAllPositions_EmptyStore_ReturnsEmpty();
	[Fact] public Task ConcurrentSavePosition_AllSucceed_Test() => ConcurrentSavePosition_AllSucceed();

	/// <summary>
	/// THE ARM 5oy7c9 EXISTS FOR. Concurrent same-consumer writes on Firestore may legitimately throw
	/// <see cref="FirestoreStalePositionException"/> for the writes that lose the race — that is this store's
	/// documented, correct behavior, recognized via <see cref="ConcurrentSaveMayRefuseAsStale"/> rather than
	/// papered over by weakening the store.
	/// </summary>
	[Fact] public Task ConcurrentSavePosition_SameConsumer_LastWriteWins_Test() => ConcurrentSavePosition_SameConsumer_LastWriteWins();

	[Fact] public Task ConformanceSuite_ShouldWireEveryArm_Test() => ConformanceSuite_ShouldWireEveryArm();
}
