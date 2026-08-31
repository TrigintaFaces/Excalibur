// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc.MongoDB;
using Excalibur.Testing.Conformance;

using MongoDB.Bson;
using MongoDB.Driver;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Integration.Tests.Cdc;

/// <summary>
/// Runs the shared CDC state-store conformance kit against the REAL <see cref="MongoDbCdcStateStore"/> on
/// a MongoDB (replica-set) container.
/// </summary>
/// <remarks>
/// <para>
/// Mirrors <c>PostgresCdcStateStoreConformanceTests</c>: <see cref="CdcProviderConformanceTestKit"/> had no
/// MongoDB deriver, so every arm of the kit had never exercised the resume-token round-trip, the
/// generic-checkpoint filter (<c>Namespace == null</c>), or the unique-index-backed upsert against a real
/// server.
/// </para>
/// <para>
/// Each arm gets a freshly-named collection, because the kit's empty-store arm requires a store with no
/// documents and the fixture's container is shared across the collection.
/// </para>
/// </remarks>
[IntegrationTest]
[Collection(ContainerCollections.MongoDB)]
[Trait("Infrastructure", TestInfrastructure.MongoDB)]
[Trait(TraitNames.Category, TestCategories.Integration)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class MongoDbCdcStateStoreConformanceTests : CdcProviderConformanceTestKit
{
	private readonly MongoDbContainerFixture _fixture;

	public MongoDbCdcStateStoreConformanceTests(MongoDbContainerFixture fixture) => _fixture = fixture;

	/// <inheritdoc />
	protected override Task<ICdcStateStore> CreateStateStoreAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"a MongoDB container must be available - real-infra CDC conformance is never skipped, because "
			+ "an arm that passes by being skipped is indistinguishable from one that passed by working.");

		var client = new MongoClient(_fixture.ConnectionString);
		ICdcStateStore store = new MongoDbCdcStateStore(
			client,
			MsOptions.Create(new MongoDbCdcStateStoreOptions
			{
				DatabaseName = "excalibur_cdc_conformance",
				CollectionName = $"cdc_state_{Guid.NewGuid():N}",
			}));

		return Task.FromResult(store);
	}

	/// <inheritdoc />
	/// <remarks>
	/// Built through <see cref="MongoDbCdcPosition"/> rather than as a free-form token, so the expected
	/// value is exactly what the store will hand back: a resume token stored as the change-stream document
	/// under <c>_data</c>, round-tripped through <c>ToJson()</c>/<c>BsonDocument.Parse</c> on the way in and
	/// out.
	/// </remarks>
	protected override ChangePosition CreateTestPosition(int index) =>
		new MongoDbCdcPosition(new BsonDocument("_data", $"resume-{index:D6}")).ToChangePosition();

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
