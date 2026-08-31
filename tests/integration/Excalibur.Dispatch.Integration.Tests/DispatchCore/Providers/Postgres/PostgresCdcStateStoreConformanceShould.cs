// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Cdc.Postgres;
using Excalibur.Dispatch;

using Shouldly;

using Tests.Shared.Fixtures;

using MsOptions = Microsoft.Extensions.Options.Options;

#pragma warning disable CA1812 // Instantiated by the xUnit test runner.

using Excalibur.Testing.Conformance;

namespace Excalibur.Dispatch.Integration.Tests.DispatchCore.Providers.Postgres;

/// <summary>
/// Per-provider real-store durability conformance for the Postgres <see cref="ICdcStateStore"/>
/// implementation, driven by the published <see cref="CdcProviderConformanceTestKit"/> against a
/// real PostgreSQL container.
/// </summary>
/// <remarks>
/// <para>
/// Before this deriver the CDC state-store contract (save/get/overwrite/delete/resume/GetAll/concurrent)
/// was exercised only in-memory; no per-provider <em>real-store</em> durability test existed. Running the
/// kit against the real store verifies the checkpoint round-trip actually survives the database — not a
/// mock — per the real-infra verification bar.
/// </para>
/// <para>
/// Each test instance gets a unique state table so the run is isolated and
/// <c>GetAllPositions_EmptyStore_ReturnsEmpty</c> holds; Docker is a hard requirement (never skipped).
/// </para>
/// </remarks>
[Collection(ContainerCollections.Postgres)]
[Trait("Category", "Integration")]
[Trait("Component", "Cdc")]
[Trait("Database", "Postgres")]
public sealed class PostgresCdcStateStoreConformanceShould : CdcProviderConformanceTestKit
{
	private readonly PostgresFixture _fixture;
	private readonly string _tableName = $"cdc_state_{Guid.NewGuid():N}";

	/// <summary>
	/// Initializes a new instance of the <see cref="PostgresCdcStateStoreConformanceShould"/> class.
	/// </summary>
	/// <param name="fixture">The shared Postgres container fixture.</param>
	public PostgresCdcStateStoreConformanceShould(PostgresFixture fixture)
	{
		_fixture = fixture;
	}

	/// <inheritdoc/>
	protected override Task<ICdcStateStore> CreateStateStoreAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"Docker/Postgres must be available - the CDC state-store durability conformance is a real-infra lock and must never be skipped.");

		ICdcStateStore store = new PostgresCdcStateStore(
			_fixture.ConnectionString,
			MsOptions.Create(new PostgresCdcStateStoreOptions { SchemaName = "public", TableName = _tableName }));
		return Task.FromResult(store);
	}

	/// <inheritdoc/>
	protected override ChangePosition CreateTestPosition(int index) =>
		// A valid, strictly-increasing LSN (index 0 must still be valid: LSN 0 == Invalid, so offset by 1).
		// Round-trip through PostgresCdcPosition so the token format matches what the store persists.
		new PostgresCdcPosition((ulong)((index + 1) * 1000)).ToChangePosition();

	/// <inheritdoc/>
	protected override Task CleanupAsync() =>
		// Each instance uses a unique table name, so state is naturally isolated; the container teardown
		// reclaims the tables. No per-test cleanup required.
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
