// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.InMemory.Snapshots;
using Excalibur.EventSourcing;
using Excalibur.Testing;
using Excalibur.Testing.Conformance;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

using Xunit;

namespace Excalibur.Tests.Testing.Conformance;

/// <summary>
/// Locks the two suite-integrity mechanisms on <see cref="SnapshotStoreConformanceTestKit"/>.
/// </summary>
/// <remarks>
/// Both mechanisms fail in the direction that reads as success, which is why each is asserted on both
/// arms rather than only the one that looks like the point. A cleanup nothing invokes is
/// indistinguishable from one that works; an arm nobody wired is indistinguishable from one that passed.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class SnapshotKitSuiteIntegrityShould
{
	[Fact]
	public async Task InvokeCleanupOncePerArm()
	{
		var probe = new CountingProbe();

		await probe.SaveAndGetLatestSnapshot_ShouldRoundTrip().ConfigureAwait(false);

		probe.ResetCalls.ShouldBe(
			1,
			"an arm must clear residual data before it runs; a kit that declares CleanupAsync and never "
			+ "calls it leaves every deriver's override dead while looking wired");
	}

	[Fact]
	public async Task InvokeCleanupForEveryArmNotJustOne()
	{
		var probe = new CountingProbe();

		await probe.SaveAndGetLatestSnapshot_ShouldRoundTrip().ConfigureAwait(false);
		await probe.DeleteSnapshots_ShouldRemoveAll().ConfigureAwait(false);
		await probe.Snapshots_ShouldIsolateByAggregateId().ConfigureAwait(false);

		probe.ResetCalls.ShouldBe(
			3,
			"every arm obtains its store through the reset wrapper, not some arms only - a partial wiring "
			+ "leaves the unrouted arms running against residue");
	}

	[Fact]
	public async Task SurfaceAFailingCleanupRatherThanSwallowIt()
	{
		var probe = new ThrowingCleanupProbe();

		var thrown = await Should.ThrowAsync<InvalidOperationException>(
			async () => await probe.SaveAndGetLatestSnapshot_ShouldRoundTrip().ConfigureAwait(false))
			.ConfigureAwait(false);

		thrown.Message.ShouldBe(
			"cleanup ran",
			"the arm must propagate a cleanup failure; if the kit had not invoked cleanup at all this arm "
			+ "would pass, which is exactly the false green this lock exists to catch");
	}

	[Fact]
	public async Task PassWhenEveryArmIsWired()
	{
		var probe = new FullyWiredProbe();

		await probe.ConformanceSuite_ShouldWireEveryArm().ConfigureAwait(false);
	}

	[Fact]
	public async Task FailAndNameTheArmWhenOneIsNotWired()
	{
		var probe = new PartiallyWiredProbe();

		var thrown = await Should.ThrowAsync<TestFixtureAssertionException>(
			async () => await probe.ConformanceSuite_ShouldWireEveryArm().ConfigureAwait(false))
			.ConfigureAwait(false);

		thrown.Message.ShouldContain(
			"SaveAndLoad_ShouldPreserveData",
			Case.Sensitive,
			"the failure must name the missing arm - 'some arm is unwired' is not actionable");
	}

	private static ISnapshotStore NewStore() =>
		new InMemorySnapshotStore(
			Options.Create(new InMemorySnapshotOptions()),
			NullLogger<InMemorySnapshotStore>.Instance,
			new ConformanceAmbientTenantContext());

	private sealed class CountingProbe : SnapshotStoreConformanceTestKit
	{
		public int ResetCalls { get; private set; }

		protected override Task<ISnapshotStore> CreateStoreAsync() => Task.FromResult(NewStore());

		protected override Task CleanupAsync()
		{
			ResetCalls++;
			return Task.CompletedTask;
		}
	}

	private sealed class ThrowingCleanupProbe : SnapshotStoreConformanceTestKit
	{
		protected override Task<ISnapshotStore> CreateStoreAsync() => Task.FromResult(NewStore());

		protected override Task CleanupAsync() => throw new InvalidOperationException("cleanup ran");
	}

	private sealed class FullyWiredProbe : SnapshotStoreConformanceTestKit
	{
		protected override Task<ISnapshotStore> CreateStoreAsync() => Task.FromResult(NewStore());

		public Task GetLatestSnapshotAsync_NoSnapshot_ShouldReturnNull_Test() => GetLatestSnapshotAsync_NoSnapshot_ShouldReturnNull();
		public Task SaveAndGetLatestSnapshot_ShouldRoundTrip_Test() => SaveAndGetLatestSnapshot_ShouldRoundTrip();
		public Task GetLatestSnapshot_MultipleVersions_ShouldReturnLatest_Test() => GetLatestSnapshot_MultipleVersions_ShouldReturnLatest();
		public Task SaveSnapshot_ShouldUpdateLatest_Test() => SaveSnapshot_ShouldUpdateLatest();
		public Task SaveSnapshot_StaleOrEqualVersion_ShouldBeASuccessfulNoOp_Test() => SaveSnapshot_StaleOrEqualVersion_ShouldBeASuccessfulNoOp();
		public Task DeleteSnapshots_ShouldRemoveAll_Test() => DeleteSnapshots_ShouldRemoveAll();
		public Task DeleteSnapshotsOlderThan_ShouldPreserveNewer_Test() => DeleteSnapshotsOlderThan_ShouldPreserveNewer();
		public Task DeleteSnapshots_NonExistent_ShouldNotThrow_Test() => DeleteSnapshots_NonExistent_ShouldNotThrow();
		public Task Snapshots_ShouldIsolateByAggregateType_Test() => Snapshots_ShouldIsolateByAggregateType();
		public Task Snapshots_ShouldIsolateByAggregateId_Test() => Snapshots_ShouldIsolateByAggregateId();
		public Task DeleteSnapshots_ShouldNotAffectOtherAggregates_Test() => DeleteSnapshots_ShouldNotAffectOtherAggregates();
		public Task SaveAndLoad_ShouldPreserveData_Test() => SaveAndLoad_ShouldPreserveData();
		public Task Snapshots_HigherVersionFromAnotherTenant_MustNotOverwrite_Test() => Snapshots_HigherVersionFromAnotherTenant_MustNotOverwrite();
		public Task Snapshots_SaveBehindAnotherTenantsVersion_MustNotBeDiscarded_Test() => Snapshots_SaveBehindAnotherTenantsVersion_MustNotBeDiscarded();
		public Task Snapshots_EachTenant_MustReadItsOwnForASharedAggregateId_Test() => Snapshots_EachTenant_MustReadItsOwnForASharedAggregateId();
		public Task SaveSnapshot_ConcurrentRisingVersions_ShouldLeaveTheHighestReadable_Test() => SaveSnapshot_ConcurrentRisingVersions_ShouldLeaveTheHighestReadable();
		public Task GetLatestSnapshot_ConcurrentReaders_ShouldAllObserveIt_Test() => GetLatestSnapshot_ConcurrentReaders_ShouldAllObserveIt();
		public Task Store_ShouldNotFaultWhenManyCallersArriveAtOnce_Test() => Store_ShouldNotFaultWhenManyCallersArriveAtOnce();
		public Task SaveAndLoad_EmptyPayload_ShouldRoundTripAsEmpty_Test() => SaveAndLoad_EmptyPayload_ShouldRoundTripAsEmpty();
		public Task SaveAndLoad_LargePayload_ShouldRoundTripByteForByte_Test() => SaveAndLoad_LargePayload_ShouldRoundTripByteForByte();
		public Task SaveAndLoad_AggregateIdWithSeparatorCharacters_ShouldRoundTrip_Test() => SaveAndLoad_AggregateIdWithSeparatorCharacters_ShouldRoundTrip();
	}

	private sealed class PartiallyWiredProbe : SnapshotStoreConformanceTestKit
	{
		protected override Task<ISnapshotStore> CreateStoreAsync() => Task.FromResult(NewStore());

		public Task GetLatestSnapshotAsync_NoSnapshot_ShouldReturnNull_Test() => GetLatestSnapshotAsync_NoSnapshot_ShouldReturnNull();
		public Task SaveAndGetLatestSnapshot_ShouldRoundTrip_Test() => SaveAndGetLatestSnapshot_ShouldRoundTrip();
		public Task GetLatestSnapshot_MultipleVersions_ShouldReturnLatest_Test() => GetLatestSnapshot_MultipleVersions_ShouldReturnLatest();
		public Task SaveSnapshot_ShouldUpdateLatest_Test() => SaveSnapshot_ShouldUpdateLatest();
		public Task SaveSnapshot_StaleOrEqualVersion_ShouldBeASuccessfulNoOp_Test() => SaveSnapshot_StaleOrEqualVersion_ShouldBeASuccessfulNoOp();
		public Task DeleteSnapshots_ShouldRemoveAll_Test() => DeleteSnapshots_ShouldRemoveAll();
		public Task DeleteSnapshotsOlderThan_ShouldPreserveNewer_Test() => DeleteSnapshotsOlderThan_ShouldPreserveNewer();
		public Task DeleteSnapshots_NonExistent_ShouldNotThrow_Test() => DeleteSnapshots_NonExistent_ShouldNotThrow();
		public Task Snapshots_ShouldIsolateByAggregateType_Test() => Snapshots_ShouldIsolateByAggregateType();
		public Task Snapshots_ShouldIsolateByAggregateId_Test() => Snapshots_ShouldIsolateByAggregateId();
		public Task DeleteSnapshots_ShouldNotAffectOtherAggregates_Test() => DeleteSnapshots_ShouldNotAffectOtherAggregates();
		public Task Snapshots_HigherVersionFromAnotherTenant_MustNotOverwrite_Test() => Snapshots_HigherVersionFromAnotherTenant_MustNotOverwrite();
		public Task Snapshots_SaveBehindAnotherTenantsVersion_MustNotBeDiscarded_Test() => Snapshots_SaveBehindAnotherTenantsVersion_MustNotBeDiscarded();
		public Task Snapshots_EachTenant_MustReadItsOwnForASharedAggregateId_Test() => Snapshots_EachTenant_MustReadItsOwnForASharedAggregateId();
		public Task SaveSnapshot_ConcurrentRisingVersions_ShouldLeaveTheHighestReadable_Test() => SaveSnapshot_ConcurrentRisingVersions_ShouldLeaveTheHighestReadable();
		public Task GetLatestSnapshot_ConcurrentReaders_ShouldAllObserveIt_Test() => GetLatestSnapshot_ConcurrentReaders_ShouldAllObserveIt();
		public Task Store_ShouldNotFaultWhenManyCallersArriveAtOnce_Test() => Store_ShouldNotFaultWhenManyCallersArriveAtOnce();
		public Task SaveAndLoad_EmptyPayload_ShouldRoundTripAsEmpty_Test() => SaveAndLoad_EmptyPayload_ShouldRoundTripAsEmpty();
		public Task SaveAndLoad_LargePayload_ShouldRoundTripByteForByte_Test() => SaveAndLoad_LargePayload_ShouldRoundTripByteForByte();
		public Task SaveAndLoad_AggregateIdWithSeparatorCharacters_ShouldRoundTrip_Test() => SaveAndLoad_AggregateIdWithSeparatorCharacters_ShouldRoundTrip();
	}
}
