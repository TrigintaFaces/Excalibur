// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc.MongoDB;
using Excalibur.Dispatch;

using MongoDB.Bson;
using MongoDB.Driver;

using Shouldly;

using Tests.Shared.Fixtures;

using MsOptions = Microsoft.Extensions.Options.Options;

#pragma warning disable CA1812 // Instantiated by the xUnit test runner.

using Excalibur.Testing.Conformance;

namespace Excalibur.Dispatch.Integration.Tests.DispatchCore.Providers.MongoDB;

/// <summary>
/// Per-provider real-store durability conformance for the MongoDB <see cref="ICdcStateStore"/>
/// implementation, driven by the published <see cref="CdcProviderConformanceTestKit"/> against a real
/// MongoDB container. Verifies the checkpoint round-trip actually survives the database (real-infra bar).
/// </summary>
/// <remarks>
/// Each test instance uses a unique collection so the run is isolated and
/// <c>GetAllPositions_EmptyStore_ReturnsEmpty</c> holds; Docker is a hard requirement (never skipped).
/// </remarks>
[Collection(ContainerCollections.MongoDB)]
[Trait("Category", "Integration")]
[Trait("Component", "Cdc")]
[Trait("Database", "MongoDB")]
public sealed class MongoDbCdcStateStoreConformanceShould : CdcProviderConformanceTestKit
{
	private readonly MongoDbContainerFixture _fixture;
	private readonly string _collectionName = $"cdc_state_{Guid.NewGuid():N}";

	/// <summary>
	/// Initializes a new instance of the <see cref="MongoDbCdcStateStoreConformanceShould"/> class.
	/// </summary>
	/// <param name="fixture">The shared MongoDB container fixture.</param>
	public MongoDbCdcStateStoreConformanceShould(MongoDbContainerFixture fixture)
	{
		_fixture = fixture;
	}

	/// <inheritdoc/>
	protected override Task<ICdcStateStore> CreateStateStoreAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"Docker/MongoDB must be available - the CDC state-store durability conformance is a real-infra lock and must never be skipped.");

		var client = new MongoClient(_fixture.ConnectionString);
		ICdcStateStore store = new MongoDbCdcStateStore(
			client,
			MsOptions.Create(new MongoDbCdcStateStoreOptions
			{
				DatabaseName = "excalibur_cdc_conformance",
				CollectionName = _collectionName,
			}));
		return Task.FromResult(store);
	}

	/// <inheritdoc/>
	protected override ChangePosition CreateTestPosition(int index) =>
		// A distinct, non-null Mongo change-stream resume token per index (index 0 is still valid: a
		// non-null ResumeToken is valid). Round-trip through MongoDbCdcPosition so the token format matches
		// what the store persists.
		new MongoDbCdcPosition(new BsonDocument("_data", $"resume-{index:D6}")).ToChangePosition();

	/// <inheritdoc/>
	protected override Task CleanupAsync() =>
		// Each instance uses a unique collection, so state is naturally isolated; the container teardown
		// reclaims the collections. No per-test cleanup required.
		Task.CompletedTask;

	// ---------------------------------------------------------------------------------------------
	// Conformance arm wiring.
	//
	// The published kit ships without test-framework attributes so a consumer is not forced onto our
	// runner. Discovery is this suite's job: one attributed member per arm. An arm nobody wires never
	// executes, and an arm that never executes cannot fail -- in the results it is indistinguishable
	// from one that passed.
	// ---------------------------------------------------------------------------------------------

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
	[Fact] public Task ConcurrentSavePosition_SameConsumer_LastWriteWins_Test() => ConcurrentSavePosition_SameConsumer_LastWriteWins();
	[Fact] public Task ConformanceSuite_ShouldWireEveryArm_Test() => ConformanceSuite_ShouldWireEveryArm();

}
