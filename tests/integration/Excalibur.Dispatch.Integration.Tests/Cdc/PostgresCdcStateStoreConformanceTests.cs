// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc.Postgres;
using Excalibur.Testing.Conformance;

namespace Excalibur.Dispatch.Integration.Tests.Cdc;

/// <summary>
/// Runs the shared CDC state-store conformance kit against the REAL <see cref="PostgresCdcStateStore"/>
/// on a Postgres container.
/// </summary>
/// <remarks>
/// <para>
/// Until this class existed, the only type deriving <see cref="CdcProviderConformanceTestKit"/> was an
/// in-memory dictionary reference implementation, so every arm ran against the one implementation with no
/// SQL in it. A dictionary round-trips whatever object it was handed; this store does not — it projects the
/// position through <see cref="PostgresCdcPosition"/> to an LSN string, writes that string, and reparses it
/// on read. Only a real server exercises that conversion, the ON CONFLICT upsert, and the generic-vs-typed
/// slot predicates that decide which row a read resolves to.
/// </para>
/// <para>
/// Each arm gets a freshly-named state table, because the kit's empty-store arm requires a store with no
/// rows and the fixture's container is shared across the collection. The store creates its own schema on
/// first use, so no external DDL is involved.
/// </para>
/// </remarks>
[IntegrationTest]
[Collection(ContainerCollections.Postgres)]
[Trait("Infrastructure", TestInfrastructure.Postgres)]
[Trait(TraitNames.Category, TestCategories.Integration)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class PostgresCdcStateStoreConformanceTests : CdcProviderConformanceTestKit
{
	private readonly PostgresFixture _fixture;

	public PostgresCdcStateStoreConformanceTests(PostgresFixture fixture) => _fixture = fixture;

	/// <inheritdoc />
	protected override Task<ICdcStateStore> CreateStateStoreAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"a Postgres container must be available - real-infra CDC conformance is never skipped, because "
			+ "an arm that passes by being skipped is indistinguishable from one that passed by working.");

		var options = Microsoft.Extensions.Options.Options.Create(new PostgresCdcStateStoreOptions
		{
			SchemaName = "cdc_conformance",
			TableName = $"cdc_state_{Guid.NewGuid():N}"
		});

		return Task.FromResult<ICdcStateStore>(
			new PostgresCdcStateStore(_fixture.ConnectionString, options));
	}

	/// <inheritdoc />
	/// <remarks>
	/// Built through <see cref="PostgresCdcPosition"/> rather than as a free-form token, so the expected
	/// value is exactly what the store will hand back. A token this provider cannot parse as an LSN reads
	/// back as no-checkpoint, which would fail the round-trip arm for a reason about the fixture rather
	/// than about the store.
	/// </remarks>
	protected override ChangePosition CreateTestPosition(int index) =>
		new PostgresCdcPosition(0x0100_0000UL + (ulong)index).ToChangePosition();

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
