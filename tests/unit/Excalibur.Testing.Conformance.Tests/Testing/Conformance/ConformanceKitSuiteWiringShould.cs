// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;
using System.Text.Json.Serialization.Metadata;

using Excalibur.Compliance;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.ErrorHandling;
using Excalibur.Dispatch.LeaderElection;
using Excalibur.Testing;
using Excalibur.Testing.Conformance;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

namespace Excalibur.Tests.Testing.Conformance;

/// <summary>
/// Locks the suite-wiring guard on every kit that carries one.
/// </summary>
/// <remarks>
/// <para>
/// The guard fails in the direction that reads as success: an arm nobody wired is indistinguishable in
/// the results from an arm that passed, so a guard that never fires is worth nothing and looks identical
/// to one that works. Each kit therefore gets two probes - one wiring every arm, which must pass, and one
/// omitting exactly one arm, which must fail AND name the arm it lost. "Some arm is unwired" would not be
/// actionable, so the message content is asserted, not merely the throw.
/// </para>
/// <para>
/// The probes' store factories throw. The guard is pure reflection over declared member names and never
/// resolves a store, so a factory that cannot run is the honest expression of that: were the guard ever
/// changed to touch the store, these probes would fail loudly rather than pass quietly.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ConformanceKitSuiteWiringShould
{
	private const string NeverResolved =
		"the wiring guard is reflection over declared member names and never resolves a store";

	[Fact]
	public async Task PassEventStoreWhenEveryArmIsWired()
	{
		var probe = new FullyWiredEventStoreProbe();

		await probe.ConformanceSuite_ShouldWireEveryArm().ConfigureAwait(false);
	}

	[Fact]
	public async Task FailEventStoreAndNameTheArmWhenOneIsNotWired()
	{
		var probe = new PartiallyWiredEventStoreProbe();

		var thrown = await Should.ThrowAsync<TestFixtureAssertionException>(
			async () => await probe.ConformanceSuite_ShouldWireEveryArm().ConfigureAwait(false))
			.ConfigureAwait(false);

		thrown.Message.ShouldContain(
			"AppendAsync_ToNewStream_ShouldSucceed",
			Case.Sensitive,
			"the failure must name the missing arm - 'some arm is unwired' is not actionable");

		thrown.Message.ShouldContain(
			$"1 of the {ArmCount<FullyWiredEventStoreProbe>()} arms",
			Case.Sensitive,
			"the count must be exact, so a guard that silently stopped enumerating is not mistaken for one "
			+ "that found a single gap");
	}
	[Fact]
	public async Task PassSagaStoreWhenEveryArmIsWired()
	{
		var probe = new FullyWiredSagaStoreProbe();

		await probe.ConformanceSuite_ShouldWireEveryArm().ConfigureAwait(false);
	}

	[Fact]
	public async Task FailSagaStoreAndNameTheArmWhenOneIsNotWired()
	{
		var probe = new PartiallyWiredSagaStoreProbe();

		var thrown = await Should.ThrowAsync<TestFixtureAssertionException>(
			async () => await probe.ConformanceSuite_ShouldWireEveryArm().ConfigureAwait(false))
			.ConfigureAwait(false);

		thrown.Message.ShouldContain(
			"SaveAsync_NewSaga_ShouldSucceed",
			Case.Sensitive,
			"the failure must name the missing arm - 'some arm is unwired' is not actionable");

		thrown.Message.ShouldContain(
			$"1 of the {ArmCount<FullyWiredSagaStoreProbe>()} arms",
			Case.Sensitive,
			"the count must be exact, so a guard that silently stopped enumerating is not mistaken for one "
			+ "that found a single gap");
	}
	[Fact]
	public async Task PassInboxStoreWhenEveryArmIsWired()
	{
		var probe = new FullyWiredInboxStoreProbe();

		await probe.ConformanceSuite_ShouldWireEveryArm().ConfigureAwait(false);
	}

	[Fact]
	public async Task FailInboxStoreAndNameTheArmWhenOneIsNotWired()
	{
		var probe = new PartiallyWiredInboxStoreProbe();

		var thrown = await Should.ThrowAsync<TestFixtureAssertionException>(
			async () => await probe.ConformanceSuite_ShouldWireEveryArm().ConfigureAwait(false))
			.ConfigureAwait(false);

		thrown.Message.ShouldContain(
			"CreateEntryAsync_NewEntry_ShouldSucceed",
			Case.Sensitive,
			"the failure must name the missing arm - 'some arm is unwired' is not actionable");

		thrown.Message.ShouldContain(
			$"1 of the {ArmCount<FullyWiredInboxStoreProbe>()} arms",
			Case.Sensitive,
			"the count must be exact, so a guard that silently stopped enumerating is not mistaken for one "
			+ "that found a single gap");
	}
	[Fact]
	public async Task PassLeaderElectionWhenEveryArmIsWired()
	{
		var probe = new FullyWiredLeaderElectionProbe();

		await probe.ConformanceSuite_ShouldWireEveryArm().ConfigureAwait(false);
	}

	[Fact]
	public async Task FailLeaderElectionAndNameTheArmWhenOneIsNotWired()
	{
		var probe = new PartiallyWiredLeaderElectionProbe();

		var thrown = await Should.ThrowAsync<TestFixtureAssertionException>(
			async () => await probe.ConformanceSuite_ShouldWireEveryArm().ConfigureAwait(false))
			.ConfigureAwait(false);

		thrown.Message.ShouldContain(
			"StartAsync_ShouldInitiateParticipation",
			Case.Sensitive,
			"the failure must name the missing arm - 'some arm is unwired' is not actionable");

		thrown.Message.ShouldContain(
			$"1 of the {ArmCount<FullyWiredLeaderElectionProbe>()} arms",
			Case.Sensitive,
			"the count must be exact, so a guard that silently stopped enumerating is not mistaken for one "
			+ "that found a single gap");
	}
	[Fact]
	public async Task PassDeadLetterStoreWhenEveryArmIsWired()
	{
		var probe = new FullyWiredDeadLetterStoreProbe();

		await probe.ConformanceSuite_ShouldWireEveryArm().ConfigureAwait(false);
	}

	[Fact]
	public async Task FailDeadLetterStoreAndNameTheArmWhenOneIsNotWired()
	{
		var probe = new PartiallyWiredDeadLetterStoreProbe();

		var thrown = await Should.ThrowAsync<TestFixtureAssertionException>(
			async () => await probe.ConformanceSuite_ShouldWireEveryArm().ConfigureAwait(false))
			.ConfigureAwait(false);

		thrown.Message.ShouldContain(
			"StoreAsync_ShouldPersistMessage",
			Case.Sensitive,
			"the failure must name the missing arm - 'some arm is unwired' is not actionable");

		thrown.Message.ShouldContain(
			$"1 of the {ArmCount<FullyWiredDeadLetterStoreProbe>()} arms",
			Case.Sensitive,
			"the count must be exact, so a guard that silently stopped enumerating is not mistaken for one "
			+ "that found a single gap");
	}

	[Fact]
	public async Task PassDeduplicatorWhenEveryArmIsWired()
	{
		var probe = new FullyWiredDeduplicatorProbe();

		await probe.ConformanceSuite_ShouldWireEveryArm().ConfigureAwait(false);
	}

	[Fact]
	public async Task FailDeduplicatorAndNameTheArmWhenOneIsNotWired()
	{
		var probe = new PartiallyWiredDeduplicatorProbe();

		var thrown = await Should.ThrowAsync<TestFixtureAssertionException>(
			async () => await probe.ConformanceSuite_ShouldWireEveryArm().ConfigureAwait(false))
			.ConfigureAwait(false);

		thrown.Message.ShouldContain(
			"MarkProcessedAsync_ThenIsDuplicate_ShouldReturnTrue",
			Case.Sensitive,
			"the failure must name the missing arm - 'some arm is unwired' is not actionable");

		thrown.Message.ShouldContain(
			$"1 of the {ArmCount<FullyWiredDeduplicatorProbe>()} arms",
			Case.Sensitive,
			"the count must be exact, so a guard that silently stopped enumerating is not mistaken for one "
			+ "that found a single gap. Every arm this kit declares is protected, so a guard enumerating "
			+ "public members only would report zero arms here and pass every suite");
	}

	/// <summary>
	/// The number of arms a kit declares, DERIVED from its fully-wired probe rather than restated here
	/// as a literal.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A hardcoded total goes stale the moment someone legitimately ADDS an arm, and the guard then fails
	/// with a message that misdirects: the reader sees a wiring gap where the only thing wrong is this
	/// number. That is worse than a check which cannot fail, because it trains people to distrust a
	/// working guard and to "fix" the wrong thing -- it happened here, and the stale literal was read as
	/// a missing wire by someone competent.
	/// </para>
	/// <para>
	/// The fully-wired probe wires every arm -- its own Pass test fails otherwise -- so its wrapper count
	/// IS the kit's arm count, and adding an arm updates every assertion automatically. The number now
	/// lives in exactly one place: the probe.
	/// </para>
	/// <para>
	/// Zero is a REFUSE, not an answer. An oracle that enumerates nothing would agree with whatever the
	/// guard printed, which is the same vacuity the guard itself exists to detect -- so it throws rather
	/// than assert against a count it could not measure.
	/// </para>
	/// </remarks>
	private static int ArmCount<TFullyWiredProbe>()
	{
		var wired = typeof(TFullyWiredProbe)
			.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
			.Count(m => m.Name.EndsWith("_Test", StringComparison.Ordinal));

		return wired > 0
			? wired
			: throw new TestFixtureAssertionException(
				$"{typeof(TFullyWiredProbe).Name} exposes no '_Test' wrappers, so the expected arm count "
				+ "could not be derived. A zero here is not a count -- it is this oracle failing to see its "
				+ "subject, and an assertion built on it would agree with whatever the guard printed.");
	}

	private sealed class FullyWiredEventStoreProbe : EventStoreConformanceTestKit
	{
		protected override void ConfigureProvider(
			IServiceCollection services,
			IJsonTypeInfoResolver? eventTypeInfoResolver) =>
			throw new NotSupportedException(NeverResolved);

		public Task AppendAsync_ToNewStream_ShouldSucceed_Test() => AppendAsync_ToNewStream_ShouldSucceed();
		public Task AppendAsync_WithWrongExpectedVersion_ShouldReturnConcurrencyConflict_Test() => AppendAsync_WithWrongExpectedVersion_ShouldReturnConcurrencyConflict();
		public Task ConcurrentAppend_SameExpectedVersion_OnlyOneShouldSucceed_Test() => ConcurrentAppend_SameExpectedVersion_OnlyOneShouldSucceed();
		public Task ConcurrentAppend_DifferentAggregates_AllShouldSucceed_Test() => ConcurrentAppend_DifferentAggregates_AllShouldSucceed();
		public Task AppendAsync_WithCorrectExpectedVersion_ShouldSucceed_Test() => AppendAsync_WithCorrectExpectedVersion_ShouldSucceed();
		public Task AppendAsync_EmptyEvents_ShouldNotChangeVersion_Test() => AppendAsync_EmptyEvents_ShouldNotChangeVersion();
		public Task LoadAsync_EmptyStream_ShouldReturnEmpty_Test() => LoadAsync_EmptyStream_ShouldReturnEmpty();
		public Task LoadAsync_ExistingStream_ShouldReturnAllEvents_Test() => LoadAsync_ExistingStream_ShouldReturnAllEvents();
		public Task LoadAsync_ShouldReturnEventsInVersionOrder_Test() => LoadAsync_ShouldReturnEventsInVersionOrder();
		public Task LoadAsync_FromVersion_ShouldReturnEventsAfterVersion_Test() => LoadAsync_FromVersion_ShouldReturnEventsAfterVersion();
		public Task LoadAsync_FromVersionBeyondStream_ShouldReturnEmpty_Test() => LoadAsync_FromVersionBeyondStream_ShouldReturnEmpty();
		public Task LoadAsync_ShouldIsolateByAggregateType_Test() => LoadAsync_ShouldIsolateByAggregateType();
		public Task LoadAsync_ShouldIsolateByAggregateId_Test() => LoadAsync_ShouldIsolateByAggregateId();
		public Task AppendAndLoad_ShouldPreserveEventData_Test() => AppendAndLoad_ShouldPreserveEventData();
		public Task AppendAndLoad_ShouldPreserveMetadata_Test() => AppendAndLoad_ShouldPreserveMetadata();
		public Task TenantScopedLoad_MustNotSeeAnotherTenantsEvents_Test() => TenantScopedLoad_MustNotSeeAnotherTenantsEvents();
		public Task TenantScopedLoad_MustSeeItsOwnEvents_Test() => TenantScopedLoad_MustSeeItsOwnEvents();
		public Task TenantPartitions_MustVersionTheSameAggregateIndependently_Test() => TenantPartitions_MustVersionTheSameAggregateIndependently();
		public Task UntenantedPartition_MustRoundTripItsOwnEvents_Test() => UntenantedPartition_MustRoundTripItsOwnEvents();
		public Task AppendAsync_UnaddressableAggregateId_ShouldThrow_Test() => AppendAsync_UnaddressableAggregateId_ShouldThrow();
		public Task AppendAsync_WithExpectedVersionBeyondTail_ShouldReturnConcurrencyConflict_Test() => AppendAsync_WithExpectedVersionBeyondTail_ShouldReturnConcurrencyConflict();
		public Task AppendAsync_NonExistentStream_WithWrongExpectedVersion_ShouldReturnConcurrencyConflict_Test() => AppendAsync_NonExistentStream_WithWrongExpectedVersion_ShouldReturnConcurrencyConflict();
		public Task LoadAsync_FromVersionZero_ShouldReturnAllExceptTheFirst_Test() => LoadAsync_FromVersionZero_ShouldReturnAllExceptTheFirst();
		public Task ConcurrentFirstUse_ShouldNotFault_Test() => ConcurrentFirstUse_ShouldNotFault();
		public Task AppendAsync_EventTypeTheResolverDoesNotDeclare_ShouldThrowAndWriteNothing_Test() => AppendAsync_EventTypeTheResolverDoesNotDeclare_ShouldThrowAndWriteNothing();
		public Task AppendAsync_AboveTheAtomicLimit_ShouldRefuseWholeOrAppendAtomically_Test() => AppendAsync_AboveTheAtomicLimit_ShouldRefuseWholeOrAppendAtomically();
	}

	private sealed class PartiallyWiredEventStoreProbe : EventStoreConformanceTestKit
	{
		protected override void ConfigureProvider(
			IServiceCollection services,
			IJsonTypeInfoResolver? eventTypeInfoResolver) =>
			throw new NotSupportedException(NeverResolved);

		public Task AppendAsync_WithWrongExpectedVersion_ShouldReturnConcurrencyConflict_Test() => AppendAsync_WithWrongExpectedVersion_ShouldReturnConcurrencyConflict();
		public Task ConcurrentAppend_SameExpectedVersion_OnlyOneShouldSucceed_Test() => ConcurrentAppend_SameExpectedVersion_OnlyOneShouldSucceed();
		public Task ConcurrentAppend_DifferentAggregates_AllShouldSucceed_Test() => ConcurrentAppend_DifferentAggregates_AllShouldSucceed();
		public Task AppendAsync_WithCorrectExpectedVersion_ShouldSucceed_Test() => AppendAsync_WithCorrectExpectedVersion_ShouldSucceed();
		public Task AppendAsync_EmptyEvents_ShouldNotChangeVersion_Test() => AppendAsync_EmptyEvents_ShouldNotChangeVersion();
		public Task LoadAsync_EmptyStream_ShouldReturnEmpty_Test() => LoadAsync_EmptyStream_ShouldReturnEmpty();
		public Task LoadAsync_ExistingStream_ShouldReturnAllEvents_Test() => LoadAsync_ExistingStream_ShouldReturnAllEvents();
		public Task LoadAsync_ShouldReturnEventsInVersionOrder_Test() => LoadAsync_ShouldReturnEventsInVersionOrder();
		public Task LoadAsync_FromVersion_ShouldReturnEventsAfterVersion_Test() => LoadAsync_FromVersion_ShouldReturnEventsAfterVersion();
		public Task LoadAsync_FromVersionBeyondStream_ShouldReturnEmpty_Test() => LoadAsync_FromVersionBeyondStream_ShouldReturnEmpty();
		public Task LoadAsync_ShouldIsolateByAggregateType_Test() => LoadAsync_ShouldIsolateByAggregateType();
		public Task LoadAsync_ShouldIsolateByAggregateId_Test() => LoadAsync_ShouldIsolateByAggregateId();
		public Task AppendAndLoad_ShouldPreserveEventData_Test() => AppendAndLoad_ShouldPreserveEventData();
		public Task AppendAndLoad_ShouldPreserveMetadata_Test() => AppendAndLoad_ShouldPreserveMetadata();
		public Task TenantScopedLoad_MustNotSeeAnotherTenantsEvents_Test() => TenantScopedLoad_MustNotSeeAnotherTenantsEvents();
		public Task TenantScopedLoad_MustSeeItsOwnEvents_Test() => TenantScopedLoad_MustSeeItsOwnEvents();
		public Task TenantPartitions_MustVersionTheSameAggregateIndependently_Test() => TenantPartitions_MustVersionTheSameAggregateIndependently();
		public Task UntenantedPartition_MustRoundTripItsOwnEvents_Test() => UntenantedPartition_MustRoundTripItsOwnEvents();
		public Task AppendAsync_UnaddressableAggregateId_ShouldThrow_Test() => AppendAsync_UnaddressableAggregateId_ShouldThrow();
		public Task AppendAsync_WithExpectedVersionBeyondTail_ShouldReturnConcurrencyConflict_Test() => AppendAsync_WithExpectedVersionBeyondTail_ShouldReturnConcurrencyConflict();
		public Task AppendAsync_NonExistentStream_WithWrongExpectedVersion_ShouldReturnConcurrencyConflict_Test() => AppendAsync_NonExistentStream_WithWrongExpectedVersion_ShouldReturnConcurrencyConflict();
		public Task LoadAsync_FromVersionZero_ShouldReturnAllExceptTheFirst_Test() => LoadAsync_FromVersionZero_ShouldReturnAllExceptTheFirst();
		public Task ConcurrentFirstUse_ShouldNotFault_Test() => ConcurrentFirstUse_ShouldNotFault();
		public Task AppendAsync_EventTypeTheResolverDoesNotDeclare_ShouldThrowAndWriteNothing_Test() => AppendAsync_EventTypeTheResolverDoesNotDeclare_ShouldThrowAndWriteNothing();
		public Task AppendAsync_AboveTheAtomicLimit_ShouldRefuseWholeOrAppendAtomically_Test() => AppendAsync_AboveTheAtomicLimit_ShouldRefuseWholeOrAppendAtomically();
	}

	private sealed class FullyWiredSagaStoreProbe : SagaStoreConformanceTestKit
	{
		protected override void ConfigureProvider(IServiceCollection services) =>
			throw new NotSupportedException(NeverResolved);

		public Task SaveAsync_NewSaga_ShouldSucceed_Test() => SaveAsync_NewSaga_ShouldSucceed();
		public Task SaveAsync_ExistingSaga_ShouldUpdate_Test() => SaveAsync_ExistingSaga_ShouldUpdate();
		public Task SaveAsync_CompletedSaga_ShouldPersistCompletedFlag_Test() => SaveAsync_CompletedSaga_ShouldPersistCompletedFlag();
		public Task LoadAsync_NonExistent_ShouldReturnNull_Test() => LoadAsync_NonExistent_ShouldReturnNull();
		public Task LoadAsync_ExistingSaga_ShouldReturnState_Test() => LoadAsync_ExistingSaga_ShouldReturnState();
		public Task LoadAsync_AfterMultipleUpdates_ShouldReturnLatest_Test() => LoadAsync_AfterMultipleUpdates_ShouldReturnLatest();
		public Task SaveAndLoad_ShouldPreserveAllProperties_Test() => SaveAndLoad_ShouldPreserveAllProperties();
		public Task SaveAndLoad_ShouldPreserveDateTimeValues_Test() => SaveAndLoad_ShouldPreserveDateTimeValues();
		public Task Sagas_ShouldIsolateBySagaId_Test() => Sagas_ShouldIsolateBySagaId();
		public Task UpdateOneSaga_ShouldNotAffectOthers_Test() => UpdateOneSaga_ShouldNotAffectOthers();
		public Task SaveAsync_WithDefaultValues_ShouldSucceed_Test() => SaveAsync_WithDefaultValues_ShouldSucceed();
		public Task StaleSave_ThrowsConcurrencyException_NoLostUpdate_Test() => StaleSave_ThrowsConcurrencyException_NoLostUpdate();
		public Task StaleSave_OnMissingSaga_DoesNotResurrect_Test() => StaleSave_OnMissingSaga_DoesNotResurrect();
		public Task LoadAsync_ReturnsAuthoritativeVersion_AndReloadMutateSaveSucceeds_Test() => LoadAsync_ReturnsAuthoritativeVersion_AndReloadMutateSaveSucceeds();
		public Task TenantScopedLoad_MustNotSeeAnotherTenantsSaga_Test() => TenantScopedLoad_MustNotSeeAnotherTenantsSaga();
		public Task TenantScopedLoad_MustSeeItsOwnSaga_Test() => TenantScopedLoad_MustSeeItsOwnSaga();
		public Task TenantPartitions_MustNotOverwriteEachOthersSagaWithTheSameId_Test() => TenantPartitions_MustNotOverwriteEachOthersSagaWithTheSameId();
		public Task UntenantedPartition_MustRoundTripItsOwnSaga_Test() => UntenantedPartition_MustRoundTripItsOwnSaga();
	}

	private sealed class PartiallyWiredSagaStoreProbe : SagaStoreConformanceTestKit
	{
		protected override void ConfigureProvider(IServiceCollection services) =>
			throw new NotSupportedException(NeverResolved);

		public Task SaveAsync_ExistingSaga_ShouldUpdate_Test() => SaveAsync_ExistingSaga_ShouldUpdate();
		public Task SaveAsync_CompletedSaga_ShouldPersistCompletedFlag_Test() => SaveAsync_CompletedSaga_ShouldPersistCompletedFlag();
		public Task LoadAsync_NonExistent_ShouldReturnNull_Test() => LoadAsync_NonExistent_ShouldReturnNull();
		public Task LoadAsync_ExistingSaga_ShouldReturnState_Test() => LoadAsync_ExistingSaga_ShouldReturnState();
		public Task LoadAsync_AfterMultipleUpdates_ShouldReturnLatest_Test() => LoadAsync_AfterMultipleUpdates_ShouldReturnLatest();
		public Task SaveAndLoad_ShouldPreserveAllProperties_Test() => SaveAndLoad_ShouldPreserveAllProperties();
		public Task SaveAndLoad_ShouldPreserveDateTimeValues_Test() => SaveAndLoad_ShouldPreserveDateTimeValues();
		public Task Sagas_ShouldIsolateBySagaId_Test() => Sagas_ShouldIsolateBySagaId();
		public Task UpdateOneSaga_ShouldNotAffectOthers_Test() => UpdateOneSaga_ShouldNotAffectOthers();
		public Task SaveAsync_WithDefaultValues_ShouldSucceed_Test() => SaveAsync_WithDefaultValues_ShouldSucceed();
		public Task StaleSave_ThrowsConcurrencyException_NoLostUpdate_Test() => StaleSave_ThrowsConcurrencyException_NoLostUpdate();
		public Task StaleSave_OnMissingSaga_DoesNotResurrect_Test() => StaleSave_OnMissingSaga_DoesNotResurrect();
		public Task LoadAsync_ReturnsAuthoritativeVersion_AndReloadMutateSaveSucceeds_Test() => LoadAsync_ReturnsAuthoritativeVersion_AndReloadMutateSaveSucceeds();
		public Task TenantScopedLoad_MustNotSeeAnotherTenantsSaga_Test() => TenantScopedLoad_MustNotSeeAnotherTenantsSaga();
		public Task TenantScopedLoad_MustSeeItsOwnSaga_Test() => TenantScopedLoad_MustSeeItsOwnSaga();
		public Task TenantPartitions_MustNotOverwriteEachOthersSagaWithTheSameId_Test() => TenantPartitions_MustNotOverwriteEachOthersSagaWithTheSameId();
		public Task UntenantedPartition_MustRoundTripItsOwnSaga_Test() => UntenantedPartition_MustRoundTripItsOwnSaga();
	}

	private sealed class FullyWiredInboxStoreProbe : InboxStoreConformanceTestKit
	{
		protected override IInboxStore CreateStore() =>
			throw new NotSupportedException(NeverResolved);

		public Task CreateEntryAsync_NewEntry_ShouldSucceed_Test() => CreateEntryAsync_NewEntry_ShouldSucceed();
		public Task CreateEntryAsync_DuplicateEntry_ShouldThrow_Test() => CreateEntryAsync_DuplicateEntry_ShouldThrow();
		public Task CreateEntryAsync_WithAllMetadata_ShouldPreserve_Test() => CreateEntryAsync_WithAllMetadata_ShouldPreserve();
		public Task MarkProcessedAsync_ExistingEntry_ShouldSucceed_Test() => MarkProcessedAsync_ExistingEntry_ShouldSucceed();
		public Task TryMarkAsProcessedAsync_FirstTime_ShouldReturnTrue_Test() => TryMarkAsProcessedAsync_FirstTime_ShouldReturnTrue();
		public Task TryMarkAsProcessedAsync_AlreadyProcessed_ShouldReturnFalse_Test() => TryMarkAsProcessedAsync_AlreadyProcessed_ShouldReturnFalse();
		public Task IsProcessedAsync_ProcessedMessage_ShouldReturnTrue_Test() => IsProcessedAsync_ProcessedMessage_ShouldReturnTrue();
		public Task IsProcessedAsync_UnprocessedMessage_ShouldReturnFalse_Test() => IsProcessedAsync_UnprocessedMessage_ShouldReturnFalse();
		public Task MarkFailedAsync_ExistingEntry_ShouldSetStatusAndError_Test() => MarkFailedAsync_ExistingEntry_ShouldSetStatusAndError();
		public Task MarkFailedAsync_ShouldIncrementRetryCount_Test() => MarkFailedAsync_ShouldIncrementRetryCount();
		public Task GetAllTenantsFailedEntriesAsync_ShouldRespectMaxRetries_Test() => GetAllTenantsFailedEntriesAsync_ShouldRespectMaxRetries();
		public Task GetAllTenantsFailedEntriesAsync_MustReturnEveryTenantsFailedEntries_Test() => GetAllTenantsFailedEntriesAsync_MustReturnEveryTenantsFailedEntries();
		public Task GetEntryAsync_Existing_ShouldReturnEntry_Test() => GetEntryAsync_Existing_ShouldReturnEntry();
		public Task GetEntryAsync_NonExistent_ShouldReturnNull_Test() => GetEntryAsync_NonExistent_ShouldReturnNull();
		public Task GetAllTenantsStatisticsAsync_ShouldReturnCorrectCounts_Test() => GetAllTenantsStatisticsAsync_ShouldReturnCorrectCounts();
		public Task CleanupAllTenantsProcessedEntriesAsync_OldProcessed_ShouldRemove_Test() => CleanupAllTenantsProcessedEntriesAsync_OldProcessed_ShouldRemove();
		public Task CleanupAllTenantsProcessedEntriesAsync_ShouldPreserveRecent_Test() => CleanupAllTenantsProcessedEntriesAsync_ShouldPreserveRecent();
		public Task Entries_ShouldIsolateByMessageIdAndHandlerType_Test() => Entries_ShouldIsolateByMessageIdAndHandlerType();
		public Task SameMessageId_DifferentHandlers_ShouldBeIndependent_Test() => SameMessageId_DifferentHandlers_ShouldBeIndependent();
		public Task GetAllTenantsEntriesAsync_ShouldReturnAllEntries_Test() => GetAllTenantsEntriesAsync_ShouldReturnAllEntries();
		public Task TryMarkAsProcessed_SameMessageIdInAnotherTenant_MustNotBeSwallowedAsADuplicate_Test() => TryMarkAsProcessed_SameMessageIdInAnotherTenant_MustNotBeSwallowedAsADuplicate();
		public Task IsProcessed_MustNotReportAnotherTenantsMessageAsProcessed_Test() => IsProcessed_MustNotReportAnotherTenantsMessageAsProcessed();
		public Task CreateEntry_SameMessageIdInAnotherTenant_MustNotCollide_Test() => CreateEntry_SameMessageIdInAnotherTenant_MustNotCollide();
		public Task ConcurrentClaimAndMark_MustElectExactlyOneWinner_AndKeepTheProcessedMarker_Test() => ConcurrentClaimAndMark_MustElectExactlyOneWinner_AndKeepTheProcessedMarker();
		public Task ExpiredLease_MustBeReclaimableByAnotherProcessor_Test() => ExpiredLease_MustBeReclaimableByAnotherProcessor();
		public Task LiveLease_MustNotBeReclaimableByAnotherProcessor_Test() => LiveLease_MustNotBeReclaimableByAnotherProcessor();

		[Fact]
		public Task ExpiredLease_MustBeReadmittedByTheRetryDrainRead_Test() => ExpiredLease_MustBeReadmittedByTheRetryDrainRead();

		[Fact]
		public Task LiveLease_MustNotBeReadmittedByTheRetryDrainRead_Test() => LiveLease_MustNotBeReadmittedByTheRetryDrainRead();
		public Task LeaselessClaim_MustNotBeReclaimableByTheLeasePath_Test() => LeaselessClaim_MustNotBeReclaimableByTheLeasePath();
		public Task ReleasedClaim_MustBeReadmittedForRedelivery_Test() => ReleasedClaim_MustBeReadmittedForRedelivery();
		public Task Release_MustNoOpOnAnUnheldClaim_AndMustNotEraseAFinalizedRecord_Test() => Release_MustNoOpOnAnUnheldClaim_AndMustNotEraseAFinalizedRecord();
		public Task ProcessedEntry_MustNotBeReadmittedByTheClaimPath_Test() => ProcessedEntry_MustNotBeReadmittedByTheClaimPath();
		public Task ProcessedEntry_MustNotBeDemotedByTheProcessingMark_Test() => ProcessedEntry_MustNotBeDemotedByTheProcessingMark();
		public Task FailedEntry_MustBeReAdmittedByTheLeasePath_Test() => FailedEntry_MustBeReAdmittedByTheLeasePath();

		public Task FailedEntry_MustNotBeReadmittedByTheClaimPath_Test() => FailedEntry_MustNotBeReadmittedByTheClaimPath();
	}

	private sealed class PartiallyWiredInboxStoreProbe : InboxStoreConformanceTestKit
	{
		protected override IInboxStore CreateStore() =>
			throw new NotSupportedException(NeverResolved);

		public Task CreateEntryAsync_DuplicateEntry_ShouldThrow_Test() => CreateEntryAsync_DuplicateEntry_ShouldThrow();
		public Task CreateEntryAsync_WithAllMetadata_ShouldPreserve_Test() => CreateEntryAsync_WithAllMetadata_ShouldPreserve();
		public Task MarkProcessedAsync_ExistingEntry_ShouldSucceed_Test() => MarkProcessedAsync_ExistingEntry_ShouldSucceed();
		public Task TryMarkAsProcessedAsync_FirstTime_ShouldReturnTrue_Test() => TryMarkAsProcessedAsync_FirstTime_ShouldReturnTrue();
		public Task TryMarkAsProcessedAsync_AlreadyProcessed_ShouldReturnFalse_Test() => TryMarkAsProcessedAsync_AlreadyProcessed_ShouldReturnFalse();
		public Task IsProcessedAsync_ProcessedMessage_ShouldReturnTrue_Test() => IsProcessedAsync_ProcessedMessage_ShouldReturnTrue();
		public Task IsProcessedAsync_UnprocessedMessage_ShouldReturnFalse_Test() => IsProcessedAsync_UnprocessedMessage_ShouldReturnFalse();
		public Task MarkFailedAsync_ExistingEntry_ShouldSetStatusAndError_Test() => MarkFailedAsync_ExistingEntry_ShouldSetStatusAndError();
		public Task MarkFailedAsync_ShouldIncrementRetryCount_Test() => MarkFailedAsync_ShouldIncrementRetryCount();
		public Task GetAllTenantsFailedEntriesAsync_ShouldRespectMaxRetries_Test() => GetAllTenantsFailedEntriesAsync_ShouldRespectMaxRetries();
		public Task GetAllTenantsFailedEntriesAsync_MustReturnEveryTenantsFailedEntries_Test() => GetAllTenantsFailedEntriesAsync_MustReturnEveryTenantsFailedEntries();
		public Task GetEntryAsync_Existing_ShouldReturnEntry_Test() => GetEntryAsync_Existing_ShouldReturnEntry();
		public Task GetEntryAsync_NonExistent_ShouldReturnNull_Test() => GetEntryAsync_NonExistent_ShouldReturnNull();
		public Task GetAllTenantsStatisticsAsync_ShouldReturnCorrectCounts_Test() => GetAllTenantsStatisticsAsync_ShouldReturnCorrectCounts();
		public Task CleanupAllTenantsProcessedEntriesAsync_OldProcessed_ShouldRemove_Test() => CleanupAllTenantsProcessedEntriesAsync_OldProcessed_ShouldRemove();
		public Task CleanupAllTenantsProcessedEntriesAsync_ShouldPreserveRecent_Test() => CleanupAllTenantsProcessedEntriesAsync_ShouldPreserveRecent();
		public Task Entries_ShouldIsolateByMessageIdAndHandlerType_Test() => Entries_ShouldIsolateByMessageIdAndHandlerType();
		public Task SameMessageId_DifferentHandlers_ShouldBeIndependent_Test() => SameMessageId_DifferentHandlers_ShouldBeIndependent();
		public Task GetAllTenantsEntriesAsync_ShouldReturnAllEntries_Test() => GetAllTenantsEntriesAsync_ShouldReturnAllEntries();
		public Task TryMarkAsProcessed_SameMessageIdInAnotherTenant_MustNotBeSwallowedAsADuplicate_Test() => TryMarkAsProcessed_SameMessageIdInAnotherTenant_MustNotBeSwallowedAsADuplicate();
		public Task IsProcessed_MustNotReportAnotherTenantsMessageAsProcessed_Test() => IsProcessed_MustNotReportAnotherTenantsMessageAsProcessed();
		public Task CreateEntry_SameMessageIdInAnotherTenant_MustNotCollide_Test() => CreateEntry_SameMessageIdInAnotherTenant_MustNotCollide();
		public Task ConcurrentClaimAndMark_MustElectExactlyOneWinner_AndKeepTheProcessedMarker_Test() => ConcurrentClaimAndMark_MustElectExactlyOneWinner_AndKeepTheProcessedMarker();
		public Task ExpiredLease_MustBeReclaimableByAnotherProcessor_Test() => ExpiredLease_MustBeReclaimableByAnotherProcessor();
		public Task LiveLease_MustNotBeReclaimableByAnotherProcessor_Test() => LiveLease_MustNotBeReclaimableByAnotherProcessor();

		[Fact]
		public Task ExpiredLease_MustBeReadmittedByTheRetryDrainRead_Test() => ExpiredLease_MustBeReadmittedByTheRetryDrainRead();

		[Fact]
		public Task LiveLease_MustNotBeReadmittedByTheRetryDrainRead_Test() => LiveLease_MustNotBeReadmittedByTheRetryDrainRead();
		public Task LeaselessClaim_MustNotBeReclaimableByTheLeasePath_Test() => LeaselessClaim_MustNotBeReclaimableByTheLeasePath();
		public Task ReleasedClaim_MustBeReadmittedForRedelivery_Test() => ReleasedClaim_MustBeReadmittedForRedelivery();
		public Task Release_MustNoOpOnAnUnheldClaim_AndMustNotEraseAFinalizedRecord_Test() => Release_MustNoOpOnAnUnheldClaim_AndMustNotEraseAFinalizedRecord();
		public Task ProcessedEntry_MustNotBeReadmittedByTheClaimPath_Test() => ProcessedEntry_MustNotBeReadmittedByTheClaimPath();
		public Task ProcessedEntry_MustNotBeDemotedByTheProcessingMark_Test() => ProcessedEntry_MustNotBeDemotedByTheProcessingMark();
		public Task FailedEntry_MustBeReAdmittedByTheLeasePath_Test() => FailedEntry_MustBeReAdmittedByTheLeasePath();

		public Task FailedEntry_MustNotBeReadmittedByTheClaimPath_Test() => FailedEntry_MustNotBeReadmittedByTheClaimPath();
	}

	private sealed class FullyWiredLeaderElectionProbe : LeaderElectionConformanceTestKit
	{
		protected override ILeaderElection CreateElection(string resourceName, string? candidateId) =>
			throw new NotSupportedException(NeverResolved);

		public Task StartAsync_ShouldInitiateParticipation_Test() => StartAsync_ShouldInitiateParticipation();
		public Task StopAsync_ShouldRelinquishLeadership_Test() => StopAsync_ShouldRelinquishLeadership();
		public Task StartAsync_AfterStop_ShouldRestartElection_Test() => StartAsync_AfterStop_ShouldRestartElection();
		public Task StartAsync_SingleCandidate_ShouldBecomeLeader_Test() => StartAsync_SingleCandidate_ShouldBecomeLeader();
		public Task StartAsync_SingleCandidate_IsLeaderShouldBeTrue_Test() => StartAsync_SingleCandidate_IsLeaderShouldBeTrue();
		public Task StartAsync_SingleCandidate_CurrentLeaderIdShouldMatchCandidateId_Test() => StartAsync_SingleCandidate_CurrentLeaderIdShouldMatchCandidateId();
		public Task MultipleCandidate_OnlyOneBecomesLeader_Test() => MultipleCandidate_OnlyOneBecomesLeader();
		public Task MultipleCandidate_ReportedLeaderIdShouldNameTheLeader_Test() => MultipleCandidate_ReportedLeaderIdShouldNameTheLeader();
		public Task MultipleCandidate_IncumbentShouldExcludeLaterCandidate_Test() => MultipleCandidate_IncumbentShouldExcludeLaterCandidate();
		public Task ConcurrentContention_ExactlyOneLeader_Test() => ConcurrentContention_ExactlyOneLeader();
		public Task BecameLeader_ShouldFireWhenElected_Test() => BecameLeader_ShouldFireWhenElected();
		public Task LostLeadership_ShouldFireWhenStopped_Test() => LostLeadership_ShouldFireWhenStopped();
		public Task LeaderChanged_ShouldFireOnLeadershipChange_Test() => LeaderChanged_ShouldFireOnLeadershipChange();
		public Task CandidateId_ShouldBeUniquePerInstance_Test() => CandidateId_ShouldBeUniquePerInstance();
		public Task CurrentLeadership_AfterStop_ShouldBeNull_Test() => CurrentLeadership_AfterStop_ShouldBeNull();
	}

	private sealed class PartiallyWiredLeaderElectionProbe : LeaderElectionConformanceTestKit
	{
		protected override ILeaderElection CreateElection(string resourceName, string? candidateId) =>
			throw new NotSupportedException(NeverResolved);

		public Task StopAsync_ShouldRelinquishLeadership_Test() => StopAsync_ShouldRelinquishLeadership();
		public Task StartAsync_AfterStop_ShouldRestartElection_Test() => StartAsync_AfterStop_ShouldRestartElection();
		public Task StartAsync_SingleCandidate_ShouldBecomeLeader_Test() => StartAsync_SingleCandidate_ShouldBecomeLeader();
		public Task StartAsync_SingleCandidate_IsLeaderShouldBeTrue_Test() => StartAsync_SingleCandidate_IsLeaderShouldBeTrue();
		public Task StartAsync_SingleCandidate_CurrentLeaderIdShouldMatchCandidateId_Test() => StartAsync_SingleCandidate_CurrentLeaderIdShouldMatchCandidateId();
		public Task MultipleCandidate_OnlyOneBecomesLeader_Test() => MultipleCandidate_OnlyOneBecomesLeader();
		public Task MultipleCandidate_ReportedLeaderIdShouldNameTheLeader_Test() => MultipleCandidate_ReportedLeaderIdShouldNameTheLeader();
		public Task MultipleCandidate_IncumbentShouldExcludeLaterCandidate_Test() => MultipleCandidate_IncumbentShouldExcludeLaterCandidate();
		public Task ConcurrentContention_ExactlyOneLeader_Test() => ConcurrentContention_ExactlyOneLeader();
		public Task BecameLeader_ShouldFireWhenElected_Test() => BecameLeader_ShouldFireWhenElected();
		public Task LostLeadership_ShouldFireWhenStopped_Test() => LostLeadership_ShouldFireWhenStopped();
		public Task LeaderChanged_ShouldFireOnLeadershipChange_Test() => LeaderChanged_ShouldFireOnLeadershipChange();
		public Task CandidateId_ShouldBeUniquePerInstance_Test() => CandidateId_ShouldBeUniquePerInstance();
		public Task CurrentLeadership_AfterStop_ShouldBeNull_Test() => CurrentLeadership_AfterStop_ShouldBeNull();
	}

	private sealed class FullyWiredDeadLetterStoreProbe : DeadLetterStoreConformanceTestKit
	{
		protected override IDeadLetterStore CreateStore(ITenantContext ambientTenant) =>
			throw new NotSupportedException(NeverResolved);

		public Task StoreAsync_ShouldPersistMessage_Test() => StoreAsync_ShouldPersistMessage();
		public Task StoreAsync_WithNullMessage_ShouldThrow_Test() => StoreAsync_WithNullMessage_ShouldThrow();
		public Task StoreAsync_MultipleMessages_ShouldPersistAll_Test() => StoreAsync_MultipleMessages_ShouldPersistAll();
		public Task GetMessagesAsync_EmptyStore_ShouldReturnEmpty_Test() => GetMessagesAsync_EmptyStore_ShouldReturnEmpty();
		public Task GetByIdAsync_ShouldReturnMessageByMessageId_Test() => GetByIdAsync_ShouldReturnMessageByMessageId();
		public Task GetByIdAsync_NonExistent_ShouldReturnNull_Test() => GetByIdAsync_NonExistent_ShouldReturnNull();
		public Task GetMessagesAsync_FilterByMessageType_ShouldFilter_Test() => GetMessagesAsync_FilterByMessageType_ShouldFilter();
		public Task GetMessagesAsync_Pagination_ShouldRespectMaxResults_Test() => GetMessagesAsync_Pagination_ShouldRespectMaxResults();
		public Task MarkAsReplayedAsync_ShouldSetIsReplayedTrue_Test() => MarkAsReplayedAsync_ShouldSetIsReplayedTrue();
		public Task MarkAsReplayedAsync_NonExistent_ShouldBeIdempotent_Test() => MarkAsReplayedAsync_NonExistent_ShouldBeIdempotent();
		public Task MarkAsReplayedAsync_AlreadyReplayed_ShouldBeIdempotent_Test() => MarkAsReplayedAsync_AlreadyReplayed_ShouldBeIdempotent();
		public Task DeleteAsync_ShouldRemoveAndReturnTrue_Test() => DeleteAsync_ShouldRemoveAndReturnTrue();
		public Task DeleteAsync_NonExistent_ShouldReturnFalse_Test() => DeleteAsync_NonExistent_ShouldReturnFalse();
		public Task DeleteAsync_ShouldDecreaseCount_Test() => DeleteAsync_ShouldDecreaseCount();
		public Task GetCountAsync_EmptyStore_ShouldReturnZero_Test() => GetCountAsync_EmptyStore_ShouldReturnZero();
		public Task GetCountAsync_AfterStores_ShouldReturnCorrectCount_Test() => GetCountAsync_AfterStores_ShouldReturnCorrectCount();
		public Task CleanupOldMessagesAsync_ShouldRemoveOldMessages_Test() => CleanupOldMessagesAsync_ShouldRemoveOldMessages();
		public Task CleanupOldMessagesAsync_ShouldRespectRetention_Test() => CleanupOldMessagesAsync_ShouldRespectRetention();
		public Task TenantScopedRead_MustNotSeeAnotherTenantsEntry_Test() => TenantScopedRead_MustNotSeeAnotherTenantsEntry();
		public Task TenantScopedRead_MustSeeItsOwnEntry_Test() => TenantScopedRead_MustSeeItsOwnEntry();
		public Task UntenantedPartition_MustRoundTripItsOwnEntry_Test() => UntenantedPartition_MustRoundTripItsOwnEntry();
		public Task ConcurrentDeleteAndStore_MustElectExactlyOneDeleter_AndLoseNoStoredMessage_Test() => ConcurrentDeleteAndStore_MustElectExactlyOneDeleter_AndLoseNoStoredMessage();
		public Task StoreAsync_ShouldRoundTripPropertyBag_Test() => StoreAsync_ShouldRoundTripPropertyBag();
	}

	private sealed class PartiallyWiredDeadLetterStoreProbe : DeadLetterStoreConformanceTestKit
	{
		protected override IDeadLetterStore CreateStore(ITenantContext ambientTenant) =>
			throw new NotSupportedException(NeverResolved);

		public Task StoreAsync_WithNullMessage_ShouldThrow_Test() => StoreAsync_WithNullMessage_ShouldThrow();
		public Task StoreAsync_MultipleMessages_ShouldPersistAll_Test() => StoreAsync_MultipleMessages_ShouldPersistAll();
		public Task GetMessagesAsync_EmptyStore_ShouldReturnEmpty_Test() => GetMessagesAsync_EmptyStore_ShouldReturnEmpty();
		public Task GetByIdAsync_ShouldReturnMessageByMessageId_Test() => GetByIdAsync_ShouldReturnMessageByMessageId();
		public Task GetByIdAsync_NonExistent_ShouldReturnNull_Test() => GetByIdAsync_NonExistent_ShouldReturnNull();
		public Task GetMessagesAsync_FilterByMessageType_ShouldFilter_Test() => GetMessagesAsync_FilterByMessageType_ShouldFilter();
		public Task GetMessagesAsync_Pagination_ShouldRespectMaxResults_Test() => GetMessagesAsync_Pagination_ShouldRespectMaxResults();
		public Task MarkAsReplayedAsync_ShouldSetIsReplayedTrue_Test() => MarkAsReplayedAsync_ShouldSetIsReplayedTrue();
		public Task MarkAsReplayedAsync_NonExistent_ShouldBeIdempotent_Test() => MarkAsReplayedAsync_NonExistent_ShouldBeIdempotent();
		public Task MarkAsReplayedAsync_AlreadyReplayed_ShouldBeIdempotent_Test() => MarkAsReplayedAsync_AlreadyReplayed_ShouldBeIdempotent();
		public Task DeleteAsync_ShouldRemoveAndReturnTrue_Test() => DeleteAsync_ShouldRemoveAndReturnTrue();
		public Task DeleteAsync_NonExistent_ShouldReturnFalse_Test() => DeleteAsync_NonExistent_ShouldReturnFalse();
		public Task DeleteAsync_ShouldDecreaseCount_Test() => DeleteAsync_ShouldDecreaseCount();
		public Task GetCountAsync_EmptyStore_ShouldReturnZero_Test() => GetCountAsync_EmptyStore_ShouldReturnZero();
		public Task GetCountAsync_AfterStores_ShouldReturnCorrectCount_Test() => GetCountAsync_AfterStores_ShouldReturnCorrectCount();
		public Task CleanupOldMessagesAsync_ShouldRemoveOldMessages_Test() => CleanupOldMessagesAsync_ShouldRemoveOldMessages();
		public Task CleanupOldMessagesAsync_ShouldRespectRetention_Test() => CleanupOldMessagesAsync_ShouldRespectRetention();
		public Task TenantScopedRead_MustNotSeeAnotherTenantsEntry_Test() => TenantScopedRead_MustNotSeeAnotherTenantsEntry();
		public Task TenantScopedRead_MustSeeItsOwnEntry_Test() => TenantScopedRead_MustSeeItsOwnEntry();
		public Task UntenantedPartition_MustRoundTripItsOwnEntry_Test() => UntenantedPartition_MustRoundTripItsOwnEntry();
		public Task ConcurrentDeleteAndStore_MustElectExactlyOneDeleter_AndLoseNoStoredMessage_Test() => ConcurrentDeleteAndStore_MustElectExactlyOneDeleter_AndLoseNoStoredMessage();
		public Task StoreAsync_ShouldRoundTripPropertyBag_Test() => StoreAsync_ShouldRoundTripPropertyBag();
	}

	private sealed class FullyWiredDeduplicatorProbe : DeduplicatorConformanceTestKit
	{
		protected override IInMemoryDeduplicator CreateDeduplicator() =>
			throw new NotSupportedException(NeverResolved);

		public Task IsDuplicateAsync_NullMessageId_ShouldThrowArgumentException_Test() => IsDuplicateAsync_NullMessageId_ShouldThrowArgumentException();
		public Task IsDuplicateAsync_EmptyMessageId_ShouldThrowArgumentException_Test() => IsDuplicateAsync_EmptyMessageId_ShouldThrowArgumentException();
		public Task IsDuplicateAsync_WhitespaceMessageId_ShouldThrowArgumentException_Test() => IsDuplicateAsync_WhitespaceMessageId_ShouldThrowArgumentException();
		public Task IsDuplicateAsync_ZeroExpiry_ShouldThrowArgumentOutOfRangeException_Test() => IsDuplicateAsync_ZeroExpiry_ShouldThrowArgumentOutOfRangeException();
		public Task IsDuplicateAsync_NotProcessed_ShouldReturnFalse_Test() => IsDuplicateAsync_NotProcessed_ShouldReturnFalse();
		public Task MarkProcessedAsync_NullMessageId_ShouldThrowArgumentException_Test() => MarkProcessedAsync_NullMessageId_ShouldThrowArgumentException();
		public Task MarkProcessedAsync_ZeroExpiry_ShouldThrowArgumentOutOfRangeException_Test() => MarkProcessedAsync_ZeroExpiry_ShouldThrowArgumentOutOfRangeException();
		public Task MarkProcessedAsync_ThenIsDuplicate_ShouldReturnTrue_Test() => MarkProcessedAsync_ThenIsDuplicate_ShouldReturnTrue();
		public Task IsDuplicateAsync_ExpiredEntry_ShouldReturnFalse_Test() => IsDuplicateAsync_ExpiredEntry_ShouldReturnFalse();
		public Task CleanupExpiredEntriesAsync_WithExpiredEntries_ShouldReturnCount_Test() => CleanupExpiredEntriesAsync_WithExpiredEntries_ShouldReturnCount();
		public Task GetStatistics_ShouldReturnValidStatistics_Test() => GetStatistics_ShouldReturnValidStatistics();
		public Task GetStatistics_AfterChecks_ShouldIncrementTotalChecks_Test() => GetStatistics_AfterChecks_ShouldIncrementTotalChecks();
		public Task GetStatistics_AfterDuplicates_ShouldIncrementDuplicatesDetected_Test() => GetStatistics_AfterDuplicates_ShouldIncrementDuplicatesDetected();
		public Task ClearAsync_ShouldRemoveAllEntries_Test() => ClearAsync_ShouldRemoveAllEntries();
		public Task ClearAsync_ShouldResetTrackedMessageCount_Test() => ClearAsync_ShouldResetTrackedMessageCount();
		public Task DisposedDeduplicator_ShouldThrowObjectDisposedException_Test() => DisposedDeduplicator_ShouldThrowObjectDisposedException();
		public Task ConcurrentClaimAndSweep_MustElectExactlyOneWinner_AndKeepTheMarker_Test() => ConcurrentClaimAndSweep_MustElectExactlyOneWinner_AndKeepTheMarker();
	}

	private sealed class PartiallyWiredDeduplicatorProbe : DeduplicatorConformanceTestKit
	{
		protected override IInMemoryDeduplicator CreateDeduplicator() =>
			throw new NotSupportedException(NeverResolved);

		public Task IsDuplicateAsync_NullMessageId_ShouldThrowArgumentException_Test() => IsDuplicateAsync_NullMessageId_ShouldThrowArgumentException();
		public Task IsDuplicateAsync_EmptyMessageId_ShouldThrowArgumentException_Test() => IsDuplicateAsync_EmptyMessageId_ShouldThrowArgumentException();
		public Task IsDuplicateAsync_WhitespaceMessageId_ShouldThrowArgumentException_Test() => IsDuplicateAsync_WhitespaceMessageId_ShouldThrowArgumentException();
		public Task IsDuplicateAsync_ZeroExpiry_ShouldThrowArgumentOutOfRangeException_Test() => IsDuplicateAsync_ZeroExpiry_ShouldThrowArgumentOutOfRangeException();
		public Task IsDuplicateAsync_NotProcessed_ShouldReturnFalse_Test() => IsDuplicateAsync_NotProcessed_ShouldReturnFalse();
		public Task MarkProcessedAsync_NullMessageId_ShouldThrowArgumentException_Test() => MarkProcessedAsync_NullMessageId_ShouldThrowArgumentException();
		public Task MarkProcessedAsync_ZeroExpiry_ShouldThrowArgumentOutOfRangeException_Test() => MarkProcessedAsync_ZeroExpiry_ShouldThrowArgumentOutOfRangeException();
		public Task IsDuplicateAsync_ExpiredEntry_ShouldReturnFalse_Test() => IsDuplicateAsync_ExpiredEntry_ShouldReturnFalse();
		public Task CleanupExpiredEntriesAsync_WithExpiredEntries_ShouldReturnCount_Test() => CleanupExpiredEntriesAsync_WithExpiredEntries_ShouldReturnCount();
		public Task GetStatistics_ShouldReturnValidStatistics_Test() => GetStatistics_ShouldReturnValidStatistics();
		public Task GetStatistics_AfterChecks_ShouldIncrementTotalChecks_Test() => GetStatistics_AfterChecks_ShouldIncrementTotalChecks();
		public Task GetStatistics_AfterDuplicates_ShouldIncrementDuplicatesDetected_Test() => GetStatistics_AfterDuplicates_ShouldIncrementDuplicatesDetected();
		public Task ClearAsync_ShouldRemoveAllEntries_Test() => ClearAsync_ShouldRemoveAllEntries();
		public Task ClearAsync_ShouldResetTrackedMessageCount_Test() => ClearAsync_ShouldResetTrackedMessageCount();
		public Task DisposedDeduplicator_ShouldThrowObjectDisposedException_Test() => DisposedDeduplicator_ShouldThrowObjectDisposedException();
		public Task ConcurrentClaimAndSweep_MustElectExactlyOneWinner_AndKeepTheMarker_Test() => ConcurrentClaimAndSweep_MustElectExactlyOneWinner_AndKeepTheMarker();
	}

	[Fact]
	public async Task PassKeyEscrowServiceWhenEveryArmIsWired()
	{
		var probe = new FullyWiredKeyEscrowServiceProbe();

		await probe.ConformanceSuite_ShouldWireEveryArm().ConfigureAwait(false);
	}

	[Fact]
	public async Task FailKeyEscrowServiceAndNameTheArmWhenOneIsNotWired()
	{
		var probe = new PartiallyWiredKeyEscrowServiceProbe();

		var thrown = await Should.ThrowAsync<TestFixtureAssertionException>(
			async () => await probe.ConformanceSuite_ShouldWireEveryArm().ConfigureAwait(false))
			.ConfigureAwait(false);

		thrown.Message.ShouldContain(
			"RecoverKeyAsync_SingleShareBelowThreshold_ShouldFailClosed",
			Case.Sensitive,
			"the failure must name the missing arm - 'some arm is unwired' is not actionable, and this is "
			+ "the arm that hides the most in this kit: a caller who holds one share of a Shamir split must not be handed the key; an unwired arm here certifies a service that releases key material below its own threshold");

		thrown.Message.ShouldContain(
			$"1 of the {ArmCount<FullyWiredKeyEscrowServiceProbe>()} arms",
			Case.Sensitive,
			"the count must be exact, so a guard that silently stopped enumerating is not mistaken for one "
			+ "that found a single gap");
	}

	[Fact]
	public async Task PassMasterKeyBackupServiceWhenEveryArmIsWired()
	{
		var probe = new FullyWiredMasterKeyBackupServiceProbe();

		await probe.ConformanceSuite_ShouldWireEveryArm().ConfigureAwait(false);
	}

	[Fact]
	public async Task FailMasterKeyBackupServiceAndNameTheArmWhenOneIsNotWired()
	{
		var probe = new PartiallyWiredMasterKeyBackupServiceProbe();

		var thrown = await Should.ThrowAsync<TestFixtureAssertionException>(
			async () => await probe.ConformanceSuite_ShouldWireEveryArm().ConfigureAwait(false))
			.ConfigureAwait(false);

		thrown.Message.ShouldContain(
			"ImportMasterKeyAsync_ExpiredBackup_ShouldThrowMasterKeyBackupException",
			Case.Sensitive,
			"the failure must name the missing arm - 'some arm is unwired' is not actionable, and this is "
			+ "the arm that hides the most in this kit: an expired master-key backup must be refused on import; an unwired arm here certifies a service that silently restores from one");

		thrown.Message.ShouldContain(
			$"1 of the {ArmCount<FullyWiredMasterKeyBackupServiceProbe>()} arms",
			Case.Sensitive,
			"the count must be exact, so a guard that silently stopped enumerating is not mistaken for one "
			+ "that found a single gap");
	}

	[Fact]
	public async Task PassEncryptionProviderWhenEveryArmIsWired()
	{
		var probe = new FullyWiredEncryptionProviderProbe();

		await probe.ConformanceSuite_ShouldWireEveryArm().ConfigureAwait(false);
	}

	[Fact]
	public async Task FailEncryptionProviderAndNameTheArmWhenOneIsNotWired()
	{
		var probe = new PartiallyWiredEncryptionProviderProbe();

		var thrown = await Should.ThrowAsync<TestFixtureAssertionException>(
			async () => await probe.ConformanceSuite_ShouldWireEveryArm().ConfigureAwait(false))
			.ConfigureAwait(false);

		thrown.Message.ShouldContain(
			"DecryptAsync_SuspendedKey_ShouldThrowEncryptionException",
			Case.Sensitive,
			"the failure must name the missing arm - 'some arm is unwired' is not actionable, and this is "
			+ "the arm that hides the most in this kit: a suspended key must stop decrypting; an unwired arm here certifies a provider that keeps honouring a key an operator has withdrawn");

		thrown.Message.ShouldContain(
			$"1 of the {ArmCount<FullyWiredEncryptionProviderProbe>()} arms",
			Case.Sensitive,
			"the count must be exact, so a guard that silently stopped enumerating is not mistaken for one "
			+ "that found a single gap");
	}

	[Fact]
	public async Task PassEncryptionMigrationServiceWhenEveryArmIsWired()
	{
		var probe = new FullyWiredEncryptionMigrationServiceProbe();

		await probe.ConformanceSuite_ShouldWireEveryArm().ConfigureAwait(false);
	}

	[Fact]
	public async Task FailEncryptionMigrationServiceAndNameTheArmWhenOneIsNotWired()
	{
		var probe = new PartiallyWiredEncryptionMigrationServiceProbe();

		var thrown = await Should.ThrowAsync<TestFixtureAssertionException>(
			async () => await probe.ConformanceSuite_ShouldWireEveryArm().ConfigureAwait(false))
			.ConfigureAwait(false);

		thrown.Message.ShouldContain(
			"MigrateAsync_RoundTrip_ShouldPreserveOriginalPlaintext",
			Case.Sensitive,
			"the failure must name the missing arm - 'some arm is unwired' is not actionable, and this is "
			+ "the arm that hides the most in this kit: re-encryption must return the same plaintext; an unwired arm here certifies a migration that loses data and reports success");

		thrown.Message.ShouldContain(
			$"1 of the {ArmCount<FullyWiredEncryptionMigrationServiceProbe>()} arms",
			Case.Sensitive,
			"the count must be exact, so a guard that silently stopped enumerating is not mistaken for one "
			+ "that found a single gap");
	}

	[Fact]
	public async Task PassLegalHoldStoreWhenEveryArmIsWired()
	{
		var probe = new FullyWiredLegalHoldStoreProbe();

		await probe.ConformanceSuite_ShouldWireEveryArm().ConfigureAwait(false);
	}

	[Fact]
	public async Task FailLegalHoldStoreAndNameTheArmWhenOneIsNotWired()
	{
		var probe = new PartiallyWiredLegalHoldStoreProbe();

		var thrown = await Should.ThrowAsync<TestFixtureAssertionException>(
			async () => await probe.ConformanceSuite_ShouldWireEveryArm().ConfigureAwait(false))
			.ConfigureAwait(false);

		thrown.Message.ShouldContain(
			"GetExpiredHoldsAsync_ShouldExcludeReleasedHolds",
			Case.Sensitive,
			"the failure must name the missing arm - 'some arm is unwired' is not actionable, and this is "
			+ "the arm that hides the most in this kit: a released hold must not resurface as expired-and-active; an unwired arm here certifies a store that misreports the regulatory state of a hold");

		thrown.Message.ShouldContain(
			$"1 of the {ArmCount<FullyWiredLegalHoldStoreProbe>()} arms",
			Case.Sensitive,
			"the count must be exact, so a guard that silently stopped enumerating is not mistaken for one "
			+ "that found a single gap");
	}

	[Fact]
	public async Task PassDataInventoryStoreWhenEveryArmIsWired()
	{
		var probe = new FullyWiredDataInventoryStoreProbe();

		await probe.ConformanceSuite_ShouldWireEveryArm().ConfigureAwait(false);
	}

	[Fact]
	public async Task FailDataInventoryStoreAndNameTheArmWhenOneIsNotWired()
	{
		var probe = new PartiallyWiredDataInventoryStoreProbe();

		var thrown = await Should.ThrowAsync<TestFixtureAssertionException>(
			async () => await probe.ConformanceSuite_ShouldWireEveryArm().ConfigureAwait(false))
			.ConfigureAwait(false);

		thrown.Message.ShouldContain(
			"GetDiscoveredLocationsAsync_ShouldIsolateByDataSubject",
			Case.Sensitive,
			"the failure must name the missing arm - 'some arm is unwired' is not actionable, and this is "
			+ "the arm that hides the most in this kit: one data subject's discovered locations must not be returned for another; an unwired arm here certifies a store that leaks across subjects into the erasure path that reads it");

		thrown.Message.ShouldContain(
			$"1 of the {ArmCount<FullyWiredDataInventoryStoreProbe>()} arms",
			Case.Sensitive,
			"the count must be exact, so a guard that silently stopped enumerating is not mistaken for one "
			+ "that found a single gap");
	}

	[Fact]
	public async Task PassScheduleStoreWhenEveryArmIsWired()
	{
		var probe = new FullyWiredScheduleStoreProbe();

		await probe.ConformanceSuite_ShouldWireEveryArm().ConfigureAwait(false);
	}

	[Fact]
	public async Task FailScheduleStoreAndNameTheArmWhenOneIsNotWired()
	{
		var probe = new PartiallyWiredScheduleStoreProbe();

		var thrown = await Should.ThrowAsync<TestFixtureAssertionException>(
			async () => await probe.ConformanceSuite_ShouldWireEveryArm().ConfigureAwait(false))
			.ConfigureAwait(false);

		thrown.Message.ShouldContain(
			"CompleteAsync_NonExistent_ShouldBeIdempotent",
			Case.Sensitive,
			"the failure must name the missing arm - 'some arm is unwired' is not actionable, and this is "
			+ "the arm that hides the most in this kit: completing a schedule twice, or completing one that is gone, must not throw; an unwired arm here certifies a store that faults on a redelivered completion");

		thrown.Message.ShouldContain(
			$"1 of the {ArmCount<FullyWiredScheduleStoreProbe>()} arms",
			Case.Sensitive,
			"the count must be exact, so a guard that silently stopped enumerating is not mistaken for one "
			+ "that found a single gap");
	}

	[Fact]
	public async Task PassStreamingHandlerWhenEveryArmIsWired()
	{
		var probe = new FullyWiredStreamingHandlerProbe();

		await probe.ConformanceSuite_ShouldWireEveryArm().ConfigureAwait(false);
	}

	[Fact]
	public async Task FailStreamingHandlerAndNameTheArmWhenOneIsNotWired()
	{
		var probe = new PartiallyWiredStreamingHandlerProbe();

		var thrown = await Should.ThrowAsync<TestFixtureAssertionException>(
			async () => await probe.ConformanceSuite_ShouldWireEveryArm().ConfigureAwait(false))
			.ConfigureAwait(false);

		thrown.Message.ShouldContain(
			"StreamConsumer_ReceivesDocumentsInOrder",
			Case.Sensitive,
			"the failure must name the missing arm - 'some arm is unwired' is not actionable, and this is "
			+ "the arm that hides the most in this kit: a stream must arrive in the order it was produced; an unwired arm here certifies a handler that reorders documents");

		thrown.Message.ShouldContain(
			$"1 of the {ArmCount<FullyWiredStreamingHandlerProbe>()} arms",
			Case.Sensitive,
			"the count must be exact, so a guard that silently stopped enumerating is not mistaken for one "
			+ "that found a single gap");
	}

	private sealed class FullyWiredKeyEscrowServiceProbe : KeyEscrowServiceConformanceTestKit
	{
		protected override IKeyEscrowService CreateService() =>
			throw new NotSupportedException(NeverResolved);

		public Task BackupKeyAsync_NullKeyId_ShouldThrowArgumentException_Test() => BackupKeyAsync_NullKeyId_ShouldThrowArgumentException();
		public Task BackupKeyAsync_EmptyKeyMaterial_ShouldThrowArgumentException_Test() => BackupKeyAsync_EmptyKeyMaterial_ShouldThrowArgumentException();
		public Task BackupKeyAsync_ValidKeyMaterial_ShouldReturnReceipt_Test() => BackupKeyAsync_ValidKeyMaterial_ShouldReturnReceipt();
		public Task BackupKeyAsync_DuplicateKey_ShouldThrowKeyEscrowException_Test() => BackupKeyAsync_DuplicateKey_ShouldThrowKeyEscrowException();
		public Task RecoverKeyAsync_NullKeyId_ShouldThrowArgumentException_Test() => RecoverKeyAsync_NullKeyId_ShouldThrowArgumentException();
		public Task RecoverKeyAsync_NonExistentKey_ShouldThrowKeyEscrowException_Test() => RecoverKeyAsync_NonExistentKey_ShouldThrowKeyEscrowException();
		public Task RecoverKeyAsync_ValidToken_ShouldReturnKeyMaterial_Test() => RecoverKeyAsync_ValidToken_ShouldReturnKeyMaterial();
		public Task RecoverKeyAsync_SingleShareBelowThreshold_ShouldFailClosed_Test() => RecoverKeyAsync_SingleShareBelowThreshold_ShouldFailClosed();
		public Task RecoverKeyAsync_RevokedEscrow_ShouldThrowKeyEscrowException_Test() => RecoverKeyAsync_RevokedEscrow_ShouldThrowKeyEscrowException();
		public Task GenerateRecoveryTokensAsync_NullKeyId_ShouldThrowArgumentException_Test() => GenerateRecoveryTokensAsync_NullKeyId_ShouldThrowArgumentException();
		public Task GenerateRecoveryTokensAsync_ThresholdLessThan2_ShouldThrowArgumentOutOfRangeException_Test() => GenerateRecoveryTokensAsync_ThresholdLessThan2_ShouldThrowArgumentOutOfRangeException();
		public Task GenerateRecoveryTokensAsync_ValidParams_ShouldGenerateCorrectCount_Test() => GenerateRecoveryTokensAsync_ValidParams_ShouldGenerateCorrectCount();
		public Task RevokeEscrowAsync_NullKeyId_ShouldThrowArgumentException_Test() => RevokeEscrowAsync_NullKeyId_ShouldThrowArgumentException();
		public Task RevokeEscrowAsync_NonExistentKey_ShouldReturnFalse_Test() => RevokeEscrowAsync_NonExistentKey_ShouldReturnFalse();
		public Task RevokeEscrowAsync_ExistingEscrow_ShouldReturnTrue_Test() => RevokeEscrowAsync_ExistingEscrow_ShouldReturnTrue();
		public Task GetEscrowStatusAsync_NullKeyId_ShouldThrowArgumentException_Test() => GetEscrowStatusAsync_NullKeyId_ShouldThrowArgumentException();
		public Task GetEscrowStatusAsync_NonExistentKey_ShouldReturnNull_Test() => GetEscrowStatusAsync_NonExistentKey_ShouldReturnNull();
		public Task GetEscrowStatusAsync_ExistingEscrow_ShouldReturnStatus_Test() => GetEscrowStatusAsync_ExistingEscrow_ShouldReturnStatus();
	}

	private sealed class PartiallyWiredKeyEscrowServiceProbe : KeyEscrowServiceConformanceTestKit
	{
		protected override IKeyEscrowService CreateService() =>
			throw new NotSupportedException(NeverResolved);

		public Task BackupKeyAsync_NullKeyId_ShouldThrowArgumentException_Test() => BackupKeyAsync_NullKeyId_ShouldThrowArgumentException();
		public Task BackupKeyAsync_EmptyKeyMaterial_ShouldThrowArgumentException_Test() => BackupKeyAsync_EmptyKeyMaterial_ShouldThrowArgumentException();
		public Task BackupKeyAsync_ValidKeyMaterial_ShouldReturnReceipt_Test() => BackupKeyAsync_ValidKeyMaterial_ShouldReturnReceipt();
		public Task BackupKeyAsync_DuplicateKey_ShouldThrowKeyEscrowException_Test() => BackupKeyAsync_DuplicateKey_ShouldThrowKeyEscrowException();
		public Task RecoverKeyAsync_NullKeyId_ShouldThrowArgumentException_Test() => RecoverKeyAsync_NullKeyId_ShouldThrowArgumentException();
		public Task RecoverKeyAsync_NonExistentKey_ShouldThrowKeyEscrowException_Test() => RecoverKeyAsync_NonExistentKey_ShouldThrowKeyEscrowException();
		public Task RecoverKeyAsync_ValidToken_ShouldReturnKeyMaterial_Test() => RecoverKeyAsync_ValidToken_ShouldReturnKeyMaterial();
		public Task RecoverKeyAsync_RevokedEscrow_ShouldThrowKeyEscrowException_Test() => RecoverKeyAsync_RevokedEscrow_ShouldThrowKeyEscrowException();
		public Task GenerateRecoveryTokensAsync_NullKeyId_ShouldThrowArgumentException_Test() => GenerateRecoveryTokensAsync_NullKeyId_ShouldThrowArgumentException();
		public Task GenerateRecoveryTokensAsync_ThresholdLessThan2_ShouldThrowArgumentOutOfRangeException_Test() => GenerateRecoveryTokensAsync_ThresholdLessThan2_ShouldThrowArgumentOutOfRangeException();
		public Task GenerateRecoveryTokensAsync_ValidParams_ShouldGenerateCorrectCount_Test() => GenerateRecoveryTokensAsync_ValidParams_ShouldGenerateCorrectCount();
		public Task RevokeEscrowAsync_NullKeyId_ShouldThrowArgumentException_Test() => RevokeEscrowAsync_NullKeyId_ShouldThrowArgumentException();
		public Task RevokeEscrowAsync_NonExistentKey_ShouldReturnFalse_Test() => RevokeEscrowAsync_NonExistentKey_ShouldReturnFalse();
		public Task RevokeEscrowAsync_ExistingEscrow_ShouldReturnTrue_Test() => RevokeEscrowAsync_ExistingEscrow_ShouldReturnTrue();
		public Task GetEscrowStatusAsync_NullKeyId_ShouldThrowArgumentException_Test() => GetEscrowStatusAsync_NullKeyId_ShouldThrowArgumentException();
		public Task GetEscrowStatusAsync_NonExistentKey_ShouldReturnNull_Test() => GetEscrowStatusAsync_NonExistentKey_ShouldReturnNull();
		public Task GetEscrowStatusAsync_ExistingEscrow_ShouldReturnStatus_Test() => GetEscrowStatusAsync_ExistingEscrow_ShouldReturnStatus();
	}

	private sealed class FullyWiredMasterKeyBackupServiceProbe : MasterKeyBackupServiceConformanceTestKit
	{
		protected override IMasterKeyBackupService CreateService() =>
			throw new NotSupportedException(NeverResolved);

		protected override Task RegisterTestKeyAsync(
			IMasterKeyBackupService service,
			string keyId,
			byte[] keyMaterial,
			CancellationToken cancellationToken) =>
			throw new NotSupportedException(NeverResolved);

		public Task ExportMasterKeyAsync_NullKeyId_ShouldThrowArgumentException_Test() => ExportMasterKeyAsync_NullKeyId_ShouldThrowArgumentException();
		public Task ExportMasterKeyAsync_NonExistentKey_ShouldThrowMasterKeyBackupException_Test() => ExportMasterKeyAsync_NonExistentKey_ShouldThrowMasterKeyBackupException();
		public Task ExportMasterKeyAsync_ValidKey_ShouldReturnBackup_Test() => ExportMasterKeyAsync_ValidKey_ShouldReturnBackup();
		public Task ImportMasterKeyAsync_NullBackup_ShouldThrowArgumentNullException_Test() => ImportMasterKeyAsync_NullBackup_ShouldThrowArgumentNullException();
		public Task ImportMasterKeyAsync_ExpiredBackup_ShouldThrowMasterKeyBackupException_Test() => ImportMasterKeyAsync_ExpiredBackup_ShouldThrowMasterKeyBackupException();
		public Task ImportMasterKeyAsync_ValidBackup_ShouldSucceed_Test() => ImportMasterKeyAsync_ValidBackup_ShouldSucceed();
		public Task ImportMasterKeyAsync_KeyExists_ShouldThrowMasterKeyBackupException_Test() => ImportMasterKeyAsync_KeyExists_ShouldThrowMasterKeyBackupException();
		public Task GenerateRecoverySplitAsync_NullKeyId_ShouldThrowArgumentException_Test() => GenerateRecoverySplitAsync_NullKeyId_ShouldThrowArgumentException();
		public Task GenerateRecoverySplitAsync_ThresholdLessThan2_ShouldThrowArgumentOutOfRangeException_Test() => GenerateRecoverySplitAsync_ThresholdLessThan2_ShouldThrowArgumentOutOfRangeException();
		public Task GenerateRecoverySplitAsync_TotalSharesLessThan2_ShouldThrowArgumentOutOfRangeException_Test() => GenerateRecoverySplitAsync_TotalSharesLessThan2_ShouldThrowArgumentOutOfRangeException();
		public Task GenerateRecoverySplitAsync_ThresholdExceedsTotalShares_ShouldThrowArgumentOutOfRangeException_Test() => GenerateRecoverySplitAsync_ThresholdExceedsTotalShares_ShouldThrowArgumentOutOfRangeException();
		public Task GenerateRecoverySplitAsync_ValidParams_ShouldGenerateCorrectCount_Test() => GenerateRecoverySplitAsync_ValidParams_ShouldGenerateCorrectCount();
		public Task ReconstructFromSharesAsync_NullShares_ShouldThrowArgumentNullException_Test() => ReconstructFromSharesAsync_NullShares_ShouldThrowArgumentNullException();
		public Task ReconstructFromSharesAsync_EmptyShares_ShouldThrowArgumentException_Test() => ReconstructFromSharesAsync_EmptyShares_ShouldThrowArgumentException();
		public Task ReconstructFromSharesAsync_InsufficientShares_ShouldThrowMasterKeyBackupException_Test() => ReconstructFromSharesAsync_InsufficientShares_ShouldThrowMasterKeyBackupException();
		public Task ReconstructFromSharesAsync_ValidShares_ShouldReconstruct_Test() => ReconstructFromSharesAsync_ValidShares_ShouldReconstruct();
		public Task ReconstructFromSharesAsync_MismatchedShares_ShouldThrowMasterKeyBackupException_Test() => ReconstructFromSharesAsync_MismatchedShares_ShouldThrowMasterKeyBackupException();
		public Task VerifyBackupAsync_NullBackup_ShouldThrowArgumentNullException_Test() => VerifyBackupAsync_NullBackup_ShouldThrowArgumentNullException();
		public Task VerifyBackupAsync_ExpiredBackup_ShouldReturnInvalid_Test() => VerifyBackupAsync_ExpiredBackup_ShouldReturnInvalid();
		public Task VerifyBackupAsync_ValidBackup_ShouldReturnValid_Test() => VerifyBackupAsync_ValidBackup_ShouldReturnValid();
		public Task GetBackupStatusAsync_NullKeyId_ShouldThrowArgumentException_Test() => GetBackupStatusAsync_NullKeyId_ShouldThrowArgumentException();
		public Task GetBackupStatusAsync_NonExistentKey_ShouldReturnNull_Test() => GetBackupStatusAsync_NonExistentKey_ShouldReturnNull();
		public Task GetBackupStatusAsync_ExistingBackup_ShouldReturnStatus_Test() => GetBackupStatusAsync_ExistingBackup_ShouldReturnStatus();
	}

	private sealed class PartiallyWiredMasterKeyBackupServiceProbe : MasterKeyBackupServiceConformanceTestKit
	{
		protected override IMasterKeyBackupService CreateService() =>
			throw new NotSupportedException(NeverResolved);

		protected override Task RegisterTestKeyAsync(
			IMasterKeyBackupService service,
			string keyId,
			byte[] keyMaterial,
			CancellationToken cancellationToken) =>
			throw new NotSupportedException(NeverResolved);

		public Task ExportMasterKeyAsync_NullKeyId_ShouldThrowArgumentException_Test() => ExportMasterKeyAsync_NullKeyId_ShouldThrowArgumentException();
		public Task ExportMasterKeyAsync_NonExistentKey_ShouldThrowMasterKeyBackupException_Test() => ExportMasterKeyAsync_NonExistentKey_ShouldThrowMasterKeyBackupException();
		public Task ExportMasterKeyAsync_ValidKey_ShouldReturnBackup_Test() => ExportMasterKeyAsync_ValidKey_ShouldReturnBackup();
		public Task ImportMasterKeyAsync_NullBackup_ShouldThrowArgumentNullException_Test() => ImportMasterKeyAsync_NullBackup_ShouldThrowArgumentNullException();
		public Task ImportMasterKeyAsync_ValidBackup_ShouldSucceed_Test() => ImportMasterKeyAsync_ValidBackup_ShouldSucceed();
		public Task ImportMasterKeyAsync_KeyExists_ShouldThrowMasterKeyBackupException_Test() => ImportMasterKeyAsync_KeyExists_ShouldThrowMasterKeyBackupException();
		public Task GenerateRecoverySplitAsync_NullKeyId_ShouldThrowArgumentException_Test() => GenerateRecoverySplitAsync_NullKeyId_ShouldThrowArgumentException();
		public Task GenerateRecoverySplitAsync_ThresholdLessThan2_ShouldThrowArgumentOutOfRangeException_Test() => GenerateRecoverySplitAsync_ThresholdLessThan2_ShouldThrowArgumentOutOfRangeException();
		public Task GenerateRecoverySplitAsync_TotalSharesLessThan2_ShouldThrowArgumentOutOfRangeException_Test() => GenerateRecoverySplitAsync_TotalSharesLessThan2_ShouldThrowArgumentOutOfRangeException();
		public Task GenerateRecoverySplitAsync_ThresholdExceedsTotalShares_ShouldThrowArgumentOutOfRangeException_Test() => GenerateRecoverySplitAsync_ThresholdExceedsTotalShares_ShouldThrowArgumentOutOfRangeException();
		public Task GenerateRecoverySplitAsync_ValidParams_ShouldGenerateCorrectCount_Test() => GenerateRecoverySplitAsync_ValidParams_ShouldGenerateCorrectCount();
		public Task ReconstructFromSharesAsync_NullShares_ShouldThrowArgumentNullException_Test() => ReconstructFromSharesAsync_NullShares_ShouldThrowArgumentNullException();
		public Task ReconstructFromSharesAsync_EmptyShares_ShouldThrowArgumentException_Test() => ReconstructFromSharesAsync_EmptyShares_ShouldThrowArgumentException();
		public Task ReconstructFromSharesAsync_InsufficientShares_ShouldThrowMasterKeyBackupException_Test() => ReconstructFromSharesAsync_InsufficientShares_ShouldThrowMasterKeyBackupException();
		public Task ReconstructFromSharesAsync_ValidShares_ShouldReconstruct_Test() => ReconstructFromSharesAsync_ValidShares_ShouldReconstruct();
		public Task ReconstructFromSharesAsync_MismatchedShares_ShouldThrowMasterKeyBackupException_Test() => ReconstructFromSharesAsync_MismatchedShares_ShouldThrowMasterKeyBackupException();
		public Task VerifyBackupAsync_NullBackup_ShouldThrowArgumentNullException_Test() => VerifyBackupAsync_NullBackup_ShouldThrowArgumentNullException();
		public Task VerifyBackupAsync_ExpiredBackup_ShouldReturnInvalid_Test() => VerifyBackupAsync_ExpiredBackup_ShouldReturnInvalid();
		public Task VerifyBackupAsync_ValidBackup_ShouldReturnValid_Test() => VerifyBackupAsync_ValidBackup_ShouldReturnValid();
		public Task GetBackupStatusAsync_NullKeyId_ShouldThrowArgumentException_Test() => GetBackupStatusAsync_NullKeyId_ShouldThrowArgumentException();
		public Task GetBackupStatusAsync_NonExistentKey_ShouldReturnNull_Test() => GetBackupStatusAsync_NonExistentKey_ShouldReturnNull();
		public Task GetBackupStatusAsync_ExistingBackup_ShouldReturnStatus_Test() => GetBackupStatusAsync_ExistingBackup_ShouldReturnStatus();
	}

	private sealed class FullyWiredEncryptionProviderProbe : EncryptionProviderConformanceTestKit
	{
		protected override Task<(IEncryptionProvider Provider, IKeyManagementProvider KeyManagement)> CreateProviderAsync() =>
			throw new NotSupportedException(NeverResolved);

		public Task EncryptAsync_NullPlaintext_ShouldThrowArgumentNullException_Test() => EncryptAsync_NullPlaintext_ShouldThrowArgumentNullException();
		public Task EncryptAsync_ShouldPopulateEncryptedDataMetadata_Test() => EncryptAsync_ShouldPopulateEncryptedDataMetadata();
		public Task EncryptAsync_NoActiveKey_ShouldThrowEncryptionException_Test() => EncryptAsync_NoActiveKey_ShouldThrowEncryptionException();
		public Task EncryptAsync_DecryptOnlyKey_ShouldThrowEncryptionException_Test() => EncryptAsync_DecryptOnlyKey_ShouldThrowEncryptionException();
		public Task DecryptAsync_NullEncryptedData_ShouldThrowArgumentNullException_Test() => DecryptAsync_NullEncryptedData_ShouldThrowArgumentNullException();
		public Task DecryptAsync_UnsupportedAlgorithm_ShouldThrowEncryptionException_Test() => DecryptAsync_UnsupportedAlgorithm_ShouldThrowEncryptionException();
		public Task DecryptAsync_SuspendedKey_ShouldThrowEncryptionException_Test() => DecryptAsync_SuspendedKey_ShouldThrowEncryptionException();
		public Task DecryptAsync_NonExistentKey_ShouldThrowEncryptionException_Test() => DecryptAsync_NonExistentKey_ShouldThrowEncryptionException();
		public Task RoundTrip_EncryptDecrypt_ShouldReturnOriginalPlaintext_Test() => RoundTrip_EncryptDecrypt_ShouldReturnOriginalPlaintext();
		public Task RoundTrip_TextData_ShouldPreserveContent_Test() => RoundTrip_TextData_ShouldPreserveContent();
		public Task RoundTrip_AfterKeyRotation_ShouldStillDecrypt_Test() => RoundTrip_AfterKeyRotation_ShouldStillDecrypt();
		public Task ValidateFipsComplianceAsync_ShouldReturnBoolean_Test() => ValidateFipsComplianceAsync_ShouldReturnBoolean();
		public Task Disposed_Provider_ShouldThrowObjectDisposedException_Test() => Disposed_Provider_ShouldThrowObjectDisposedException();
	}

	private sealed class PartiallyWiredEncryptionProviderProbe : EncryptionProviderConformanceTestKit
	{
		protected override Task<(IEncryptionProvider Provider, IKeyManagementProvider KeyManagement)> CreateProviderAsync() =>
			throw new NotSupportedException(NeverResolved);

		public Task EncryptAsync_NullPlaintext_ShouldThrowArgumentNullException_Test() => EncryptAsync_NullPlaintext_ShouldThrowArgumentNullException();
		public Task EncryptAsync_ShouldPopulateEncryptedDataMetadata_Test() => EncryptAsync_ShouldPopulateEncryptedDataMetadata();
		public Task EncryptAsync_NoActiveKey_ShouldThrowEncryptionException_Test() => EncryptAsync_NoActiveKey_ShouldThrowEncryptionException();
		public Task EncryptAsync_DecryptOnlyKey_ShouldThrowEncryptionException_Test() => EncryptAsync_DecryptOnlyKey_ShouldThrowEncryptionException();
		public Task DecryptAsync_NullEncryptedData_ShouldThrowArgumentNullException_Test() => DecryptAsync_NullEncryptedData_ShouldThrowArgumentNullException();
		public Task DecryptAsync_UnsupportedAlgorithm_ShouldThrowEncryptionException_Test() => DecryptAsync_UnsupportedAlgorithm_ShouldThrowEncryptionException();
		public Task DecryptAsync_NonExistentKey_ShouldThrowEncryptionException_Test() => DecryptAsync_NonExistentKey_ShouldThrowEncryptionException();
		public Task RoundTrip_EncryptDecrypt_ShouldReturnOriginalPlaintext_Test() => RoundTrip_EncryptDecrypt_ShouldReturnOriginalPlaintext();
		public Task RoundTrip_TextData_ShouldPreserveContent_Test() => RoundTrip_TextData_ShouldPreserveContent();
		public Task RoundTrip_AfterKeyRotation_ShouldStillDecrypt_Test() => RoundTrip_AfterKeyRotation_ShouldStillDecrypt();
		public Task ValidateFipsComplianceAsync_ShouldReturnBoolean_Test() => ValidateFipsComplianceAsync_ShouldReturnBoolean();
		public Task Disposed_Provider_ShouldThrowObjectDisposedException_Test() => Disposed_Provider_ShouldThrowObjectDisposedException();
	}

	private sealed class FullyWiredEncryptionMigrationServiceProbe : EncryptionMigrationServiceConformanceTestKit
	{
		protected override Task<(IEncryptionMigrationService Service, IEncryptionProvider Encryption, IKeyManagementProvider KeyManagement)> CreateServiceAsync() =>
			throw new NotSupportedException(NeverResolved);

		public Task MigrateAsync_NullEncryptedData_ShouldThrowArgumentNullException_Test() => MigrateAsync_NullEncryptedData_ShouldThrowArgumentNullException();
		public Task MigrateAsync_NullSourceContext_ShouldThrowArgumentNullException_Test() => MigrateAsync_NullSourceContext_ShouldThrowArgumentNullException();
		public Task MigrateAsync_NullTargetContext_ShouldThrowArgumentNullException_Test() => MigrateAsync_NullTargetContext_ShouldThrowArgumentNullException();
		public Task MigrateAsync_ValidData_ShouldReturnSuccessfulResult_Test() => MigrateAsync_ValidData_ShouldReturnSuccessfulResult();
		public Task MigrateAsync_RoundTrip_ShouldPreserveOriginalPlaintext_Test() => MigrateAsync_RoundTrip_ShouldPreserveOriginalPlaintext();
		public Task MigrateBatchAsync_NullItems_ShouldThrowArgumentNullException_Test() => MigrateBatchAsync_NullItems_ShouldThrowArgumentNullException();
		public Task MigrateBatchAsync_NullTargetContext_ShouldThrowArgumentNullException_Test() => MigrateBatchAsync_NullTargetContext_ShouldThrowArgumentNullException();
		public Task MigrateBatchAsync_NullOptions_ShouldThrowArgumentNullException_Test() => MigrateBatchAsync_NullOptions_ShouldThrowArgumentNullException();
		public Task MigrateBatchAsync_ValidBatch_ShouldReturnSuccessfulResult_Test() => MigrateBatchAsync_ValidBatch_ShouldReturnSuccessfulResult();
		public Task MigrateBatchAsync_EmptyBatch_ShouldReturnSuccessfulResult_Test() => MigrateBatchAsync_EmptyBatch_ShouldReturnSuccessfulResult();
		public Task RequiresMigrationAsync_NullEncryptedData_ShouldThrowArgumentNullException_Test() => RequiresMigrationAsync_NullEncryptedData_ShouldThrowArgumentNullException();
		public Task RequiresMigrationAsync_NullPolicy_ShouldThrowArgumentNullException_Test() => RequiresMigrationAsync_NullPolicy_ShouldThrowArgumentNullException();
		public Task RequiresMigrationAsync_DeprecatedKeyId_ShouldReturnTrue_Test() => RequiresMigrationAsync_DeprecatedKeyId_ShouldReturnTrue();
		public Task RequiresMigrationAsync_NoMatchingPolicy_ShouldReturnFalse_Test() => RequiresMigrationAsync_NoMatchingPolicy_ShouldReturnFalse();
		public Task GetMigrationStatusAsync_NullMigrationId_ShouldThrowArgumentNullException_Test() => GetMigrationStatusAsync_NullMigrationId_ShouldThrowArgumentNullException();
		public Task GetMigrationStatusAsync_NonExistentMigration_ShouldReturnNull_Test() => GetMigrationStatusAsync_NonExistentMigration_ShouldReturnNull();
		public Task GetMigrationStatusAsync_AfterBatchMigration_ShouldReturnStatus_Test() => GetMigrationStatusAsync_AfterBatchMigration_ShouldReturnStatus();
		public Task EstimateMigrationAsync_NullPolicy_ShouldThrowArgumentNullException_Test() => EstimateMigrationAsync_NullPolicy_ShouldThrowArgumentNullException();
		public Task EstimateMigrationAsync_ValidPolicy_ShouldReturnEstimate_Test() => EstimateMigrationAsync_ValidPolicy_ShouldReturnEstimate();
	}

	private sealed class PartiallyWiredEncryptionMigrationServiceProbe : EncryptionMigrationServiceConformanceTestKit
	{
		protected override Task<(IEncryptionMigrationService Service, IEncryptionProvider Encryption, IKeyManagementProvider KeyManagement)> CreateServiceAsync() =>
			throw new NotSupportedException(NeverResolved);

		public Task MigrateAsync_NullEncryptedData_ShouldThrowArgumentNullException_Test() => MigrateAsync_NullEncryptedData_ShouldThrowArgumentNullException();
		public Task MigrateAsync_NullSourceContext_ShouldThrowArgumentNullException_Test() => MigrateAsync_NullSourceContext_ShouldThrowArgumentNullException();
		public Task MigrateAsync_NullTargetContext_ShouldThrowArgumentNullException_Test() => MigrateAsync_NullTargetContext_ShouldThrowArgumentNullException();
		public Task MigrateAsync_ValidData_ShouldReturnSuccessfulResult_Test() => MigrateAsync_ValidData_ShouldReturnSuccessfulResult();
		public Task MigrateBatchAsync_NullItems_ShouldThrowArgumentNullException_Test() => MigrateBatchAsync_NullItems_ShouldThrowArgumentNullException();
		public Task MigrateBatchAsync_NullTargetContext_ShouldThrowArgumentNullException_Test() => MigrateBatchAsync_NullTargetContext_ShouldThrowArgumentNullException();
		public Task MigrateBatchAsync_NullOptions_ShouldThrowArgumentNullException_Test() => MigrateBatchAsync_NullOptions_ShouldThrowArgumentNullException();
		public Task MigrateBatchAsync_ValidBatch_ShouldReturnSuccessfulResult_Test() => MigrateBatchAsync_ValidBatch_ShouldReturnSuccessfulResult();
		public Task MigrateBatchAsync_EmptyBatch_ShouldReturnSuccessfulResult_Test() => MigrateBatchAsync_EmptyBatch_ShouldReturnSuccessfulResult();
		public Task RequiresMigrationAsync_NullEncryptedData_ShouldThrowArgumentNullException_Test() => RequiresMigrationAsync_NullEncryptedData_ShouldThrowArgumentNullException();
		public Task RequiresMigrationAsync_NullPolicy_ShouldThrowArgumentNullException_Test() => RequiresMigrationAsync_NullPolicy_ShouldThrowArgumentNullException();
		public Task RequiresMigrationAsync_DeprecatedKeyId_ShouldReturnTrue_Test() => RequiresMigrationAsync_DeprecatedKeyId_ShouldReturnTrue();
		public Task RequiresMigrationAsync_NoMatchingPolicy_ShouldReturnFalse_Test() => RequiresMigrationAsync_NoMatchingPolicy_ShouldReturnFalse();
		public Task GetMigrationStatusAsync_NullMigrationId_ShouldThrowArgumentNullException_Test() => GetMigrationStatusAsync_NullMigrationId_ShouldThrowArgumentNullException();
		public Task GetMigrationStatusAsync_NonExistentMigration_ShouldReturnNull_Test() => GetMigrationStatusAsync_NonExistentMigration_ShouldReturnNull();
		public Task GetMigrationStatusAsync_AfterBatchMigration_ShouldReturnStatus_Test() => GetMigrationStatusAsync_AfterBatchMigration_ShouldReturnStatus();
		public Task EstimateMigrationAsync_NullPolicy_ShouldThrowArgumentNullException_Test() => EstimateMigrationAsync_NullPolicy_ShouldThrowArgumentNullException();
		public Task EstimateMigrationAsync_ValidPolicy_ShouldReturnEstimate_Test() => EstimateMigrationAsync_ValidPolicy_ShouldReturnEstimate();
	}

	private sealed class FullyWiredLegalHoldStoreProbe : LegalHoldStoreConformanceTestKit
	{
		protected override ILegalHoldStore CreateStore() =>
			throw new NotSupportedException(NeverResolved);

		public Task SaveHoldAsync_ShouldPersistHold_Test() => SaveHoldAsync_ShouldPersistHold();
		public Task SaveHoldAsync_DuplicateHoldId_ShouldThrowInvalidOperationException_Test() => SaveHoldAsync_DuplicateHoldId_ShouldThrowInvalidOperationException();
		public Task SaveHoldAsync_NullHold_ShouldThrowArgumentNullException_Test() => SaveHoldAsync_NullHold_ShouldThrowArgumentNullException();
		public Task GetHoldAsync_ExistingHold_ShouldReturnHold_Test() => GetHoldAsync_ExistingHold_ShouldReturnHold();
		public Task GetHoldAsync_NonExistent_ShouldReturnNull_Test() => GetHoldAsync_NonExistent_ShouldReturnNull();
		public Task UpdateHoldAsync_ExistingHold_ShouldUpdateAndReturnTrue_Test() => UpdateHoldAsync_ExistingHold_ShouldUpdateAndReturnTrue();
		public Task UpdateHoldAsync_NonExistent_ShouldReturnFalse_Test() => UpdateHoldAsync_NonExistent_ShouldReturnFalse();
		public Task UpdateHoldAsync_NullHold_ShouldThrowArgumentNullException_Test() => UpdateHoldAsync_NullHold_ShouldThrowArgumentNullException();
		public Task GetActiveHoldsForDataSubjectAsync_ActiveHolds_ShouldReturnMatching_Test() => GetActiveHoldsForDataSubjectAsync_ActiveHolds_ShouldReturnMatching();
		public Task GetActiveHoldsForDataSubjectAsync_WithTenantFilter_ShouldFilterCorrectly_Test() => GetActiveHoldsForDataSubjectAsync_WithTenantFilter_ShouldFilterCorrectly();
		public Task GetActiveHoldsForDataSubjectAsync_GlobalHold_ShouldBeReachableUnscoped_Test() => GetActiveHoldsForDataSubjectAsync_GlobalHold_ShouldBeReachableUnscoped();
		public Task GetActiveHoldsForDataSubjectAsync_GlobalHold_ShouldBeVisibleToScopedCaller_Test() => GetActiveHoldsForDataSubjectAsync_GlobalHold_ShouldBeVisibleToScopedCaller();
		public Task GetActiveHoldsForDataSubjectAsync_NullDataSubjectIdHash_ShouldThrowArgumentException_Test() => GetActiveHoldsForDataSubjectAsync_NullDataSubjectIdHash_ShouldThrowArgumentException();
		public Task GetActiveHoldsForTenantAsync_ActiveTenantHolds_ShouldReturnMatching_Test() => GetActiveHoldsForTenantAsync_ActiveTenantHolds_ShouldReturnMatching();
		public Task GetActiveHoldsForTenantAsync_GlobalHold_ShouldBeVisibleToScopedCaller_Test() => GetActiveHoldsForTenantAsync_GlobalHold_ShouldBeVisibleToScopedCaller();
		public Task GetActiveHoldsForTenantAsync_NullTenantId_ShouldThrowArgumentException_Test() => GetActiveHoldsForTenantAsync_NullTenantId_ShouldThrowArgumentException();
		public Task ListActiveHoldsAsync_AllActive_ShouldReturnNonExpiredOrderedByCreatedAtDesc_Test() => ListActiveHoldsAsync_AllActive_ShouldReturnNonExpiredOrderedByCreatedAtDesc();
		public Task ListActiveHoldsAsync_WithTenantFilter_ShouldFilterCorrectly_Test() => ListActiveHoldsAsync_WithTenantFilter_ShouldFilterCorrectly();
		public Task ListAllHoldsAsync_IncludesReleasedHolds_ShouldReturnAll_Test() => ListAllHoldsAsync_IncludesReleasedHolds_ShouldReturnAll();
		public Task ListAllHoldsAsync_DateRangeFilters_ShouldFilterCorrectly_Test() => ListAllHoldsAsync_DateRangeFilters_ShouldFilterCorrectly();
		public Task GetExpiredHoldsAsync_ShouldReturnActiveHoldsWithPassedExpiration_Test() => GetExpiredHoldsAsync_ShouldReturnActiveHoldsWithPassedExpiration();
		public Task GetExpiredHoldsAsync_ShouldExcludeReleasedHolds_Test() => GetExpiredHoldsAsync_ShouldExcludeReleasedHolds();
	}

	private sealed class PartiallyWiredLegalHoldStoreProbe : LegalHoldStoreConformanceTestKit
	{
		protected override ILegalHoldStore CreateStore() =>
			throw new NotSupportedException(NeverResolved);

		public Task SaveHoldAsync_ShouldPersistHold_Test() => SaveHoldAsync_ShouldPersistHold();
		public Task SaveHoldAsync_DuplicateHoldId_ShouldThrowInvalidOperationException_Test() => SaveHoldAsync_DuplicateHoldId_ShouldThrowInvalidOperationException();
		public Task SaveHoldAsync_NullHold_ShouldThrowArgumentNullException_Test() => SaveHoldAsync_NullHold_ShouldThrowArgumentNullException();
		public Task GetHoldAsync_ExistingHold_ShouldReturnHold_Test() => GetHoldAsync_ExistingHold_ShouldReturnHold();
		public Task GetHoldAsync_NonExistent_ShouldReturnNull_Test() => GetHoldAsync_NonExistent_ShouldReturnNull();
		public Task UpdateHoldAsync_ExistingHold_ShouldUpdateAndReturnTrue_Test() => UpdateHoldAsync_ExistingHold_ShouldUpdateAndReturnTrue();
		public Task UpdateHoldAsync_NonExistent_ShouldReturnFalse_Test() => UpdateHoldAsync_NonExistent_ShouldReturnFalse();
		public Task UpdateHoldAsync_NullHold_ShouldThrowArgumentNullException_Test() => UpdateHoldAsync_NullHold_ShouldThrowArgumentNullException();
		public Task GetActiveHoldsForDataSubjectAsync_ActiveHolds_ShouldReturnMatching_Test() => GetActiveHoldsForDataSubjectAsync_ActiveHolds_ShouldReturnMatching();
		public Task GetActiveHoldsForDataSubjectAsync_WithTenantFilter_ShouldFilterCorrectly_Test() => GetActiveHoldsForDataSubjectAsync_WithTenantFilter_ShouldFilterCorrectly();
		public Task GetActiveHoldsForDataSubjectAsync_GlobalHold_ShouldBeReachableUnscoped_Test() => GetActiveHoldsForDataSubjectAsync_GlobalHold_ShouldBeReachableUnscoped();
		public Task GetActiveHoldsForDataSubjectAsync_GlobalHold_ShouldBeVisibleToScopedCaller_Test() => GetActiveHoldsForDataSubjectAsync_GlobalHold_ShouldBeVisibleToScopedCaller();
		public Task GetActiveHoldsForDataSubjectAsync_NullDataSubjectIdHash_ShouldThrowArgumentException_Test() => GetActiveHoldsForDataSubjectAsync_NullDataSubjectIdHash_ShouldThrowArgumentException();
		public Task GetActiveHoldsForTenantAsync_ActiveTenantHolds_ShouldReturnMatching_Test() => GetActiveHoldsForTenantAsync_ActiveTenantHolds_ShouldReturnMatching();
		public Task GetActiveHoldsForTenantAsync_GlobalHold_ShouldBeVisibleToScopedCaller_Test() => GetActiveHoldsForTenantAsync_GlobalHold_ShouldBeVisibleToScopedCaller();
		public Task GetActiveHoldsForTenantAsync_NullTenantId_ShouldThrowArgumentException_Test() => GetActiveHoldsForTenantAsync_NullTenantId_ShouldThrowArgumentException();
		public Task ListActiveHoldsAsync_AllActive_ShouldReturnNonExpiredOrderedByCreatedAtDesc_Test() => ListActiveHoldsAsync_AllActive_ShouldReturnNonExpiredOrderedByCreatedAtDesc();
		public Task ListActiveHoldsAsync_WithTenantFilter_ShouldFilterCorrectly_Test() => ListActiveHoldsAsync_WithTenantFilter_ShouldFilterCorrectly();
		public Task ListAllHoldsAsync_IncludesReleasedHolds_ShouldReturnAll_Test() => ListAllHoldsAsync_IncludesReleasedHolds_ShouldReturnAll();
		public Task ListAllHoldsAsync_DateRangeFilters_ShouldFilterCorrectly_Test() => ListAllHoldsAsync_DateRangeFilters_ShouldFilterCorrectly();
		public Task GetExpiredHoldsAsync_ShouldReturnActiveHoldsWithPassedExpiration_Test() => GetExpiredHoldsAsync_ShouldReturnActiveHoldsWithPassedExpiration();
	}

	private sealed class FullyWiredDataInventoryStoreProbe : DataInventoryStoreConformanceTestKit
	{
		protected override IDataInventoryStore CreateStore() =>
			throw new NotSupportedException(NeverResolved);
		protected override IDisposable? EnterTenant(string tenantId) =>
			throw new NotSupportedException(NeverResolved);

		public Task SaveRegistrationAsync_ShouldPersistRegistration_Test() => SaveRegistrationAsync_ShouldPersistRegistration();
		public Task SaveRegistrationAsync_DuplicateKey_ShouldUpsert_Test() => SaveRegistrationAsync_DuplicateKey_ShouldUpsert();
		public Task SaveRegistrationAsync_NullRegistration_ShouldThrowArgumentNullException_Test() => SaveRegistrationAsync_NullRegistration_ShouldThrowArgumentNullException();
		public Task RemoveRegistrationAsync_ExistingRegistration_ShouldReturnTrue_Test() => RemoveRegistrationAsync_ExistingRegistration_ShouldReturnTrue();
		public Task RemoveRegistrationAsync_NonExistent_ShouldReturnFalse_Test() => RemoveRegistrationAsync_NonExistent_ShouldReturnFalse();
		public Task GetAllRegistrationsAsync_ShouldReturnAllRegistrations_Test() => GetAllRegistrationsAsync_ShouldReturnAllRegistrations();
		public Task FindRegistrationsForDataSubjectAsync_ShouldFilterByIdType_Test() => FindRegistrationsForDataSubjectAsync_ShouldFilterByIdType();
		public Task RecordDiscoveredLocationAsync_ShouldPersistLocation_Test() => RecordDiscoveredLocationAsync_ShouldPersistLocation();
		public Task RecordDiscoveredLocationAsync_NullLocation_ShouldThrowArgumentNullException_Test() => RecordDiscoveredLocationAsync_NullLocation_ShouldThrowArgumentNullException();
		public Task RecordDiscoveredLocationAsync_NullDataSubjectId_ShouldThrowArgumentException_Test() => RecordDiscoveredLocationAsync_NullDataSubjectId_ShouldThrowArgumentException();
		public Task RecordDiscoveredLocationAsync_DuplicateLocation_ShouldDeduplicate_Test() => RecordDiscoveredLocationAsync_DuplicateLocation_ShouldDeduplicate();
		public Task GetDiscoveredLocationsAsync_ExistingSubject_ShouldReturnLocations_Test() => GetDiscoveredLocationsAsync_ExistingSubject_ShouldReturnLocations();
		public Task GetDiscoveredLocationsAsync_NonExistentSubject_ShouldReturnEmptyList_Test() => GetDiscoveredLocationsAsync_NonExistentSubject_ShouldReturnEmptyList();
		public Task GetDataMapEntriesAsync_ShouldMergeRegistrationsAndDiscovered_Test() => GetDataMapEntriesAsync_ShouldMergeRegistrationsAndDiscovered();
		public Task GetDataMapEntriesAsync_RegistrationsShouldSetIsAutoDiscoveredFalse_Test() => GetDataMapEntriesAsync_RegistrationsShouldSetIsAutoDiscoveredFalse();
		public Task GetDataMapEntriesAsync_ShouldCalculateRecordCount_Test() => GetDataMapEntriesAsync_ShouldCalculateRecordCount();
		public Task FindRegistrationsForDataSubjectAsync_WithTenantFilter_ShouldFilterCorrectly_Test() => FindRegistrationsForDataSubjectAsync_WithTenantFilter_ShouldFilterCorrectly();
		public Task GetDataMapEntriesAsync_NullTenant_ShouldReturnAllEntries_Test() => GetDataMapEntriesAsync_NullTenant_ShouldReturnAllEntries();
		public Task GetDiscoveredLocationsAsync_ShouldIsolateByDataSubject_Test() => GetDiscoveredLocationsAsync_ShouldIsolateByDataSubject();
	}

	private sealed class PartiallyWiredDataInventoryStoreProbe : DataInventoryStoreConformanceTestKit
	{
		protected override IDataInventoryStore CreateStore() =>
			throw new NotSupportedException(NeverResolved);
		protected override IDisposable? EnterTenant(string tenantId) =>
			throw new NotSupportedException(NeverResolved);

		public Task SaveRegistrationAsync_ShouldPersistRegistration_Test() => SaveRegistrationAsync_ShouldPersistRegistration();
		public Task SaveRegistrationAsync_DuplicateKey_ShouldUpsert_Test() => SaveRegistrationAsync_DuplicateKey_ShouldUpsert();
		public Task SaveRegistrationAsync_NullRegistration_ShouldThrowArgumentNullException_Test() => SaveRegistrationAsync_NullRegistration_ShouldThrowArgumentNullException();
		public Task RemoveRegistrationAsync_ExistingRegistration_ShouldReturnTrue_Test() => RemoveRegistrationAsync_ExistingRegistration_ShouldReturnTrue();
		public Task RemoveRegistrationAsync_NonExistent_ShouldReturnFalse_Test() => RemoveRegistrationAsync_NonExistent_ShouldReturnFalse();
		public Task GetAllRegistrationsAsync_ShouldReturnAllRegistrations_Test() => GetAllRegistrationsAsync_ShouldReturnAllRegistrations();
		public Task FindRegistrationsForDataSubjectAsync_ShouldFilterByIdType_Test() => FindRegistrationsForDataSubjectAsync_ShouldFilterByIdType();
		public Task RecordDiscoveredLocationAsync_ShouldPersistLocation_Test() => RecordDiscoveredLocationAsync_ShouldPersistLocation();
		public Task RecordDiscoveredLocationAsync_NullLocation_ShouldThrowArgumentNullException_Test() => RecordDiscoveredLocationAsync_NullLocation_ShouldThrowArgumentNullException();
		public Task RecordDiscoveredLocationAsync_NullDataSubjectId_ShouldThrowArgumentException_Test() => RecordDiscoveredLocationAsync_NullDataSubjectId_ShouldThrowArgumentException();
		public Task RecordDiscoveredLocationAsync_DuplicateLocation_ShouldDeduplicate_Test() => RecordDiscoveredLocationAsync_DuplicateLocation_ShouldDeduplicate();
		public Task GetDiscoveredLocationsAsync_ExistingSubject_ShouldReturnLocations_Test() => GetDiscoveredLocationsAsync_ExistingSubject_ShouldReturnLocations();
		public Task GetDiscoveredLocationsAsync_NonExistentSubject_ShouldReturnEmptyList_Test() => GetDiscoveredLocationsAsync_NonExistentSubject_ShouldReturnEmptyList();
		public Task GetDataMapEntriesAsync_ShouldMergeRegistrationsAndDiscovered_Test() => GetDataMapEntriesAsync_ShouldMergeRegistrationsAndDiscovered();
		public Task GetDataMapEntriesAsync_RegistrationsShouldSetIsAutoDiscoveredFalse_Test() => GetDataMapEntriesAsync_RegistrationsShouldSetIsAutoDiscoveredFalse();
		public Task GetDataMapEntriesAsync_ShouldCalculateRecordCount_Test() => GetDataMapEntriesAsync_ShouldCalculateRecordCount();
		public Task FindRegistrationsForDataSubjectAsync_WithTenantFilter_ShouldFilterCorrectly_Test() => FindRegistrationsForDataSubjectAsync_WithTenantFilter_ShouldFilterCorrectly();
		public Task GetDataMapEntriesAsync_NullTenant_ShouldReturnAllEntries_Test() => GetDataMapEntriesAsync_NullTenant_ShouldReturnAllEntries();
	}

	private sealed class FullyWiredScheduleStoreProbe : ScheduleStoreConformanceTestKit
	{
		protected override IScheduleStore CreateStore() =>
			throw new NotSupportedException(NeverResolved);

		public Task StoreAsync_ShouldPersistMessage_Test() => StoreAsync_ShouldPersistMessage();
		public Task StoreAsync_WithNullMessage_ShouldThrow_Test() => StoreAsync_WithNullMessage_ShouldThrow();
		public Task StoreAsync_SameId_ShouldUpsert_Test() => StoreAsync_SameId_ShouldUpsert();
		public Task StoreAsync_MultipleMessages_ShouldPersistAll_Test() => StoreAsync_MultipleMessages_ShouldPersistAll();
		public Task GetAllAsync_EmptyStore_ShouldReturnEmpty_Test() => GetAllAsync_EmptyStore_ShouldReturnEmpty();
		public Task GetAllAsync_AfterStore_ShouldReturnMessage_Test() => GetAllAsync_AfterStore_ShouldReturnMessage();
		public Task GetAllAsync_ShouldReturnAllMessages_Test() => GetAllAsync_ShouldReturnAllMessages();
		public Task CompleteAsync_ShouldSetEnabledFalse_Test() => CompleteAsync_ShouldSetEnabledFalse();
		public Task CompleteAsync_NonExistent_ShouldBeIdempotent_Test() => CompleteAsync_NonExistent_ShouldBeIdempotent();
		public Task CompleteAsync_AlreadyCompleted_ShouldBeIdempotent_Test() => CompleteAsync_AlreadyCompleted_ShouldBeIdempotent();
		public Task StoreAsync_ThenComplete_MessageRemainsPersisted_Test() => StoreAsync_ThenComplete_MessageRemainsPersisted();
		public Task MultipleMessages_CompleteOne_OthersUnaffected_Test() => MultipleMessages_CompleteOne_OthersUnaffected();
	}

	private sealed class PartiallyWiredScheduleStoreProbe : ScheduleStoreConformanceTestKit
	{
		protected override IScheduleStore CreateStore() =>
			throw new NotSupportedException(NeverResolved);

		public Task StoreAsync_ShouldPersistMessage_Test() => StoreAsync_ShouldPersistMessage();
		public Task StoreAsync_WithNullMessage_ShouldThrow_Test() => StoreAsync_WithNullMessage_ShouldThrow();
		public Task StoreAsync_SameId_ShouldUpsert_Test() => StoreAsync_SameId_ShouldUpsert();
		public Task StoreAsync_MultipleMessages_ShouldPersistAll_Test() => StoreAsync_MultipleMessages_ShouldPersistAll();
		public Task GetAllAsync_EmptyStore_ShouldReturnEmpty_Test() => GetAllAsync_EmptyStore_ShouldReturnEmpty();
		public Task GetAllAsync_AfterStore_ShouldReturnMessage_Test() => GetAllAsync_AfterStore_ShouldReturnMessage();
		public Task GetAllAsync_ShouldReturnAllMessages_Test() => GetAllAsync_ShouldReturnAllMessages();
		public Task CompleteAsync_ShouldSetEnabledFalse_Test() => CompleteAsync_ShouldSetEnabledFalse();
		public Task CompleteAsync_AlreadyCompleted_ShouldBeIdempotent_Test() => CompleteAsync_AlreadyCompleted_ShouldBeIdempotent();
		public Task StoreAsync_ThenComplete_MessageRemainsPersisted_Test() => StoreAsync_ThenComplete_MessageRemainsPersisted();
		public Task MultipleMessages_CompleteOne_OthersUnaffected_Test() => MultipleMessages_CompleteOne_OthersUnaffected();
	}

	private sealed class FullyWiredStreamingHandlerProbe : StreamingHandlerConformanceTestKit
	{
		protected override (IStreamConsumerHandler<TestStreamDocument> Handler, Func<IReadOnlyList<TestStreamDocument>> GetProcessed) CreateConsumerHandler() =>
			throw new NotSupportedException(NeverResolved);

		public Task StreamConsumer_ProcessesAllDocuments_Test() => StreamConsumer_ProcessesAllDocuments();
		public Task StreamConsumer_ReceivesDocumentsInOrder_Test() => StreamConsumer_ReceivesDocumentsInOrder();
		public Task StreamConsumer_EmptyStream_CompletesSuccessfully_Test() => StreamConsumer_EmptyStream_CompletesSuccessfully();
		public Task StreamConsumer_SingleDocument_ProcessedCorrectly_Test() => StreamConsumer_SingleDocument_ProcessedCorrectly();
		public Task StreamConsumer_RespectsCancellation_Test() => StreamConsumer_RespectsCancellation();
		public Task ChunkedStream_FirstChunkIsMarkedFirst_Test() => ChunkedStream_FirstChunkIsMarkedFirst();
		public Task ChunkedStream_LastChunkIsMarkedLast_Test() => ChunkedStream_LastChunkIsMarkedLast();
		public Task ChunkedStream_MiddleChunksAreMiddle_Test() => ChunkedStream_MiddleChunksAreMiddle();
		public Task ChunkedStream_SingleChunk_IsBothFirstAndLast_Test() => ChunkedStream_SingleChunk_IsBothFirstAndLast();
		public Task ChunkedStream_IndicesAreSequential_Test() => ChunkedStream_IndicesAreSequential();
		public Task StreamConsumer_LargeStream_ProcessesAll_Test() => StreamConsumer_LargeStream_ProcessesAll();
	}

	private sealed class PartiallyWiredStreamingHandlerProbe : StreamingHandlerConformanceTestKit
	{
		protected override (IStreamConsumerHandler<TestStreamDocument> Handler, Func<IReadOnlyList<TestStreamDocument>> GetProcessed) CreateConsumerHandler() =>
			throw new NotSupportedException(NeverResolved);

		public Task StreamConsumer_ProcessesAllDocuments_Test() => StreamConsumer_ProcessesAllDocuments();
		public Task StreamConsumer_EmptyStream_CompletesSuccessfully_Test() => StreamConsumer_EmptyStream_CompletesSuccessfully();
		public Task StreamConsumer_SingleDocument_ProcessedCorrectly_Test() => StreamConsumer_SingleDocument_ProcessedCorrectly();
		public Task StreamConsumer_RespectsCancellation_Test() => StreamConsumer_RespectsCancellation();
		public Task ChunkedStream_FirstChunkIsMarkedFirst_Test() => ChunkedStream_FirstChunkIsMarkedFirst();
		public Task ChunkedStream_LastChunkIsMarkedLast_Test() => ChunkedStream_LastChunkIsMarkedLast();
		public Task ChunkedStream_MiddleChunksAreMiddle_Test() => ChunkedStream_MiddleChunksAreMiddle();
		public Task ChunkedStream_SingleChunk_IsBothFirstAndLast_Test() => ChunkedStream_SingleChunk_IsBothFirstAndLast();
		public Task ChunkedStream_IndicesAreSequential_Test() => ChunkedStream_IndicesAreSequential();
		public Task StreamConsumer_LargeStream_ProcessesAll_Test() => StreamConsumer_LargeStream_ProcessesAll();
	}

	#region The shared implementation itself

	/// <summary>
	/// LIVENESS: a suite wiring every arm of its kit must PASS.
	/// </summary>
	/// <remarks>
	/// Without this cell a guard that threw unconditionally would satisfy both detection cells below while
	/// making every suite in the repository red.
	/// </remarks>
	[Fact]
	public async Task PassASuiteThatWiresEveryArmOfItsKit() =>
		await new FullyWiredSuite().ConformanceSuite_ShouldWireEveryArm().ConfigureAwait(false);

	/// <summary>
	/// DETECTION: a suite that omits one arm must FAIL and NAME the arm it lost.
	/// </summary>
	/// <remarks>
	/// Naming is the load-bearing part. "Some arm is unwired" leaves the reader to find it, and a guard
	/// nobody can act on is one people learn to skip.
	/// </remarks>
	[Fact]
	public async Task DetectASuiteThatOmitsOneArm()
	{
		var thrown = await Should.ThrowAsync<TestFixtureAssertionException>(
			async () => await new PartiallyWiredSuite().ConformanceSuite_ShouldWireEveryArm().ConfigureAwait(false))
			.ConfigureAwait(false);

		thrown.Message.ShouldContain("SecondArm", Case.Sensitive);
	}

	/// <summary>
	/// DETECTION: enumerating NO arms must FAIL, never pass.
	/// </summary>
	/// <remarks>
	/// The cell that makes every other suite's green worth reading. A wiring check that finds nothing
	/// certifies everything it is pointed at -- the precise defect it exists to detect -- so the empty
	/// enumeration has to be a failure. This suite derives the base directly, with no kit layer between,
	/// which is the shape that produces it.
	/// </remarks>
	[Fact]
	public async Task RefuseWhenNoArmsAreFound()
	{
		var thrown = await Should.ThrowAsync<TestFixtureAssertionException>(
			async () => await new NoKitLayerSuite().ConformanceSuite_ShouldWireEveryArm().ConfigureAwait(false))
			.ConfigureAwait(false);

		thrown.Message.ShouldContain("no arms were found", Case.Sensitive);
	}

	/// <summary>
	/// DETECTION: one arm's wrapper must not satisfy a SHORTER arm whose name it contains.
	/// </summary>
	/// <remarks>
	/// A substring match passes this suite, and the shorter arm then never runs — the exact defect the
	/// check exists to detect, produced by the check itself. Real pairs of this shape exist in the
	/// control-validation kits, so this is a live case rather than a constructed one.
	/// </remarks>
	[Fact]
	public async Task DetectAWrapperThatOnlySatisfiesALongerArmByPrefix()
	{
		var thrown = await Should.ThrowAsync<TestFixtureAssertionException>(
			async () => await new PrefixOnlySuite().ConformanceSuite_ShouldWireEveryArm().ConfigureAwait(false))
			.ConfigureAwait(false);

		// The unwired list is the tail of the message, joined with ", ". With exactly one unwired arm there
		// is no separator, so asserting "ReturnResult," could never match what the guard emits — the arm was
		// unsatisfiable rather than merely weak. Assert the property that was wanted instead: the SHORTER
		// arm is the one reported, and the longer one it is a prefix of is not.
		thrown.Message.ShouldEndWith("Unwired: ReturnResult", Case.Sensitive);
		thrown.Message.ShouldNotContain("ReturnResultWithRequiredProperties", Case.Sensitive);
	}

	/// <summary>
	/// ANTI-OVERREACH: a lifecycle member an intermediate layer implements is not an arm.
	/// </summary>
	/// <remarks>
	/// An implicit interface implementation is virtual but sealed, so a virtual-only filter counts
	/// <c>InitializeAsync</c> as an arm and demands the suite wire it — a requirement imposed on a
	/// consumer for implementing an unrelated interface.
	/// </remarks>
	[Fact]
	public async Task NotCountALifecycleMemberAsAnArm() =>
		await new LifecycleSuite().ConformanceSuite_ShouldWireEveryArm().ConfigureAwait(false);

	/// <summary>Two arms, one name a strict prefix of the other.</summary>
	private abstract class PrefixArmKit : ConformanceTestKit
	{
		public virtual Task ReturnResult() => Task.CompletedTask;

		public virtual Task ReturnResultWithRequiredProperties() => Task.CompletedTask;
	}

	/// <summary>Wires only the longer arm. A substring match would call this fully wired.</summary>
	private sealed class PrefixOnlySuite : PrefixArmKit
	{
		public Task ReturnResultWithRequiredProperties_Test() => ReturnResultWithRequiredProperties();
	}

	/// <summary>An intermediate layer implementing a lifecycle interface, as a consumer's fixture would.</summary>
	private abstract class LifecycleKit : ConformanceTestKit, IAsyncLifetime
	{
		public virtual Task OnlyArm() => Task.CompletedTask;

		public ValueTask InitializeAsync() => ValueTask.CompletedTask;

		public ValueTask DisposeAsync() => ValueTask.CompletedTask;
	}

	private sealed class LifecycleSuite : LifecycleKit
	{
		public Task OnlyArm_Test() => OnlyArm();
	}

	/// <summary>A two-arm kit standing in for any real one; the guard only sees shape, not subject.</summary>
	private abstract class TwoArmKit : ConformanceTestKit
	{
		public virtual Task FirstArm() => Task.CompletedTask;

		public virtual Task SecondArm() => Task.CompletedTask;
	}

	private sealed class FullyWiredSuite : TwoArmKit
	{
		public Task FirstArm_Test() => FirstArm();

		public Task SecondArm_Test() => SecondArm();
	}

	/// <summary>Wires one of the two. The omission is the experiment.</summary>
	private sealed class PartiallyWiredSuite : TwoArmKit
	{
		public Task FirstArm_Test() => FirstArm();
	}

	/// <summary>Derives the base with no kit layer, so there are no arms to enumerate.</summary>
	private sealed class NoKitLayerSuite : ConformanceTestKit;

	#endregion The shared implementation itself
}
