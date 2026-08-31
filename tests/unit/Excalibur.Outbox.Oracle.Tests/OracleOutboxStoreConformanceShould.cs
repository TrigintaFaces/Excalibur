// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data;
using Excalibur.Dispatch;

using FakeItEasy;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

using Excalibur.Testing.Conformance;

using Xunit;

#pragma warning disable CA1812 // Internal class is never instantiated

namespace Excalibur.Outbox.Oracle.Tests;

/// <summary>
/// Real-infrastructure conformance tests for <see cref="OracleOutboxStore"/> using the Outbox Conformance
/// Test Kit against a live Oracle (<c>gvenzl/oracle-free</c>) container.
/// </summary>
/// <remarks>
/// Verifies the Oracle implementation satisfies the <see cref="IOutboxStore"/> (and admin/dead-letter/
/// backoff/transactional) contract against real infrastructure. Never skipped: when Docker is unavailable
/// the fixture fails fast, so a missing container surfaces as a failure rather than a silent pass. The
/// store is constructed via its consumer-default surface — an <see cref="IDb"/> over a fresh Oracle
/// connection per access plus an <c>IOptions&lt;OracleOutboxStoreOptions&gt;</c> bound to the fixture's
/// connection string. Exercises the emitted behavior — the two-step reserve/claim, dead-letter move,
/// duplicate-insert dedup (ORA-00001), and the empty-field round-trip (A0 ruling #5) — not merely that a
/// value was written. The fixture owns the schema because the store does not self-create its tables.
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "Oracle")]
public sealed class OracleOutboxStoreConformanceShould : OutboxStoreConformanceTestKit, IAsyncLifetime, IClassFixture<OracleOutboxStoreContainerFixture>
{
	private readonly OracleOutboxStoreContainerFixture _fixture;

	/// <summary>
	/// Initializes a new instance of the <see cref="OracleOutboxStoreConformanceShould"/> class.
	/// </summary>
	/// <param name="fixture">The Oracle container fixture.</param>
	public OracleOutboxStoreConformanceShould(OracleOutboxStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	/// <inheritdoc/>
	protected override async Task<IOutboxStore> CreateStoreAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"Oracle container must be available - real-infra conformance is never skipped.");

		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);

		// Consumer-default surface: an IDb whose Connection yields a fresh Oracle connection per access
		// (a fresh connection is required for the concurrent-staging conformance cases, which drive
		// multiple operations through IDb.Connection in parallel).
		var db = A.Fake<IDb>();
		_ = A.CallTo(() => db.Connection).ReturnsLazily(() => _fixture.CreateConnection());

		var options = Options.Create(new OracleOutboxStoreOptions
		{
			SchemaName = _fixture.SchemaName,
			OutboxTableName = _fixture.OutboxTableName,
			DeadLetterTableName = _fixture.DeadLetterTableName,
			ReservationTimeout = 300,
			MaxAttempts = 3,
		});

		return new OracleOutboxStore(db, options, NullLogger<OracleOutboxStore>.Instance);
	}

	/// <inheritdoc/>
	protected override async Task<IOutboxStore?> CreateStoreWithReclaimFloorAsync(int floorSeconds)
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"Oracle container must be available - real-infra re-claim-floor conformance is never skipped.");
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);

		var db = A.Fake<IDb>();
		_ = A.CallTo(() => db.Connection).ReturnsLazily(() => _fixture.CreateConnection());

		var options = Options.Create(new OracleOutboxStoreOptions
		{
			SchemaName = _fixture.SchemaName,
			OutboxTableName = _fixture.OutboxTableName,
			DeadLetterTableName = _fixture.DeadLetterTableName,
			ReservationTimeout = 300,
			MaxAttempts = 3,
			FailureBackoffFloorSeconds = floorSeconds,
		});

		return new OracleOutboxStore(db, options, NullLogger<OracleOutboxStore>.Instance);
	}

	/// <inheritdoc/>
	protected override async Task<bool> TryReserveMessageUnderForeignDispatcherAsync(IOutboxStore store, string messageId)
	{
		// Reserve under a dispatcher id distinct from OracleOutboxStore's static per-process id, so the row's owner
		// differs from the caller of IOutboxStore.MarkFailedAsync — the only way to exercise the R2 guard.
		var reserved = await ((OracleOutboxStore)store)
			.ReserveOutboxMessagesAsync("conformance-foreign-leader", 50, CancellationToken.None).ConfigureAwait(false);
		return reserved.Any(m => m.MessageId == messageId);
	}

	/// <inheritdoc/>
	protected override async Task CleanupAsync()
	{
		await _fixture.CleanupTableAsync().ConfigureAwait(false);
	}

	/// <summary>
	/// Proves the Oracle reserve honors TRUE <c>FOR UPDATE SKIP LOCKED</c> (SA ruling on bead 9wka92):
	/// two dispatchers reserving concurrently claim DISJOINT batches and neither BLOCKS to timeout. A
	/// non-skip-locked (lock-wait) reserve would serialize/deadlock the two sessions and time out.
    /// </summary>
	[Fact]
	[Trait("Category", "Integration")]
	public async Task Reserve_TwoConcurrentDispatchers_ClaimDisjointBatchesWithoutBlocking()
	{
		// Arrange — 10 unsent messages; two dispatchers each try to reserve 5, concurrently, on their
		// own Oracle sessions (a second store => a second connection; skip-locked is only observable
		// across distinct sessions).
		var storeA = (OracleOutboxStore)await CreateStoreForArmAsync().ConfigureAwait(false);
		var storeB = (OracleOutboxStore)await CreateStoreAsync().ConfigureAwait(false);

		var stagedIds = new List<string>();
		for (var i = 0; i < 10; i++)
		{
			var message = CreateTestMessage();
			stagedIds.Add(message.Id);
			await storeA.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);
		}

		// Act — both reserves launched together; a lock-wait impl would block one until the other's txn
		// releases, blowing the timeout. Skip-locked lets both proceed immediately over disjoint rows.
		var reserveA = storeA.ReserveOutboxMessagesAsync("dispatcher-A", 5, CancellationToken.None);
		var reserveB = storeB.ReserveOutboxMessagesAsync("dispatcher-B", 5, CancellationToken.None);

		var results = await Task.WhenAll(reserveA, reserveB)
			.WaitAsync(TimeSpan.FromSeconds(30))
			.ConfigureAwait(false);

		// Assert — disjoint, each <= batchSize, union has no duplicate, all belong to the staged set.
		var idsA = results[0].Select(m => m.MessageId).ToList();
		var idsB = results[1].Select(m => m.MessageId).ToList();

		// NON-VACUITY (SA REVIEW_ARCH B2): a positive LOWER bound. The previous assertions (<=5, unique,
		// disjoint, subset) are ALL satisfied by EMPTY result sets, so a reserve that returns ZERO rows
		// (the B1 select-back positional-binding stall) passed GREEN. With 10 staged messages and two
		// dispatchers each claiming up to 5 over FOR UPDATE SKIP LOCKED, the two together MUST claim the
		// entire claimable set (10), and each MUST claim at least one. This is RED on the zero-row impl.
		idsA.Count.ShouldBeGreaterThan(0, "dispatcher-A must actually claim rows, not an empty select-back");
		idsB.Count.ShouldBeGreaterThan(0, "dispatcher-B must actually claim rows, not an empty select-back");
		idsA.Count.ShouldBeLessThanOrEqualTo(5);
		idsB.Count.ShouldBeLessThanOrEqualTo(5);
		idsA.ShouldBeUnique();
		idsB.ShouldBeUnique();
		idsA.Intersect(idsB).ShouldBeEmpty("concurrent dispatchers must claim disjoint rows (skip-locked)");

		var union = idsA.Concat(idsB).ToList();
		union.Count.ShouldBe(union.Distinct().Count());
		union.ShouldAllBe(id => stagedIds.Contains(id));
		// The claimable set is fully drained across the two dispatchers — the load-bearing lower bound.
		union.Count.ShouldBe(10, "both dispatchers together must claim every staged message, not zero rows");
	}

	/// <summary>
	/// u4x8sb scoped lock: the Oracle-local fix adds <c>correlation_id</c>/<c>causation_id</c> columns and
	/// positional binds so a staged message's CorrelationId and CausationId survive a reload from real Oracle.
	/// </summary>
	/// <remarks>
	/// Deliberately scoped to the two Oracle-local fields. Priority and the four multi-transport fields
	/// (PartitionKey/GroupKey/TargetTransports/IsMultiTransport) are a cross-provider shared-seam carry tracked
	/// separately (y5tn3e) and are intentionally NOT asserted here — the broad shared-kit
	/// <c>StageMessageAsync_ShouldRoundTripEveryCallerSuppliedField</c> covers those and stays RED until y5tn3e.
	/// </remarks>
	[Fact]
	public async Task StageMessage_RoundTripsCorrelationAndCausationId_OnRealOracleReload()
	{
		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
		await CleanupAsync().ConfigureAwait(false);

		var message = new OutboundMessage("Test.OrderPlaced", "order-data"u8.ToArray(), "orders-queue")
		{
			CorrelationId = "corr-oracle-1",
			CausationId = "cause-oracle-2",
		};

		var store = await CreateStoreForArmAsync().ConfigureAwait(false);
		await store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);

		var unsent = await store.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false);
		var retrieved = unsent.FirstOrDefault(m => m.Id == message.Id);

		_ = retrieved.ShouldNotBeNull("the staged message must reload from real Oracle");
		retrieved.CorrelationId.ShouldBe("corr-oracle-1", "correlation_id must round-trip (Oracle-local fix)");
		retrieved.CausationId.ShouldBe("cause-oracle-2", "causation_id must round-trip (Oracle-local fix)");
	}

	/// <inheritdoc/>
	public ValueTask InitializeAsync() => ValueTask.CompletedTask;

	/// <inheritdoc/>
	/// <remarks>
	/// The kit resets data before every arm and carries no lifecycle of its own, so that it does not depend
	/// on a test framework. Teardown is this suite's job, and this is where it belongs: it may dispose
	/// clients and connections, because nothing runs against the store afterwards.
	/// </remarks>
	public async ValueTask DisposeAsync()
	{
		await CleanupAsync().ConfigureAwait(false);
		GC.SuppressFinalize(this);
	}


	// ---------------------------------------------------------------------------------------------------
	// Conformance arm wiring.
	//
	// The kit ships without test-framework attributes so that a consumer is not forced onto our runner,
	// which means discovery is this suite's job: one member per arm, attributed for xUnit. The kit's
	// ConformanceSuite_ShouldWireEveryArm arm fails if any arm here is missing, so an arm cannot be
	// silently dropped -- an arm that never runs cannot fail, and reads in the results exactly like one
	// that passed. A tracked gap stays declared and visibly skipped rather than being deleted.
	// ---------------------------------------------------------------------------------------------------

	[Fact]
	public Task CleanupAllTenantsSentMessagesAsync_ShouldPreserveFailedMessages_Test() => CleanupAllTenantsSentMessagesAsync_ShouldPreserveFailedMessages();

	[Fact]
	public Task CleanupAllTenantsSentMessagesAsync_ShouldPreservePendingMessages_Test() => CleanupAllTenantsSentMessagesAsync_ShouldPreservePendingMessages();

	[Fact]
	public Task CleanupAllTenantsSentMessagesAsync_ShouldPreserveRecentlySentMessages_Test() => CleanupAllTenantsSentMessagesAsync_ShouldPreserveRecentlySentMessages();

	[Fact]
	public Task CleanupAllTenantsSentMessagesAsync_ShouldRemoveOldMessages_Test() => CleanupAllTenantsSentMessagesAsync_ShouldRemoveOldMessages();

	[Fact]
	public Task CleanupAllTenantsSentMessagesAsync_ShouldRespectBatchSize_Test() => CleanupAllTenantsSentMessagesAsync_ShouldRespectBatchSize();

	[Fact]
	public Task ConcurrentMixedOperations_ShouldLeaveStatisticsConsistent_Test() => ConcurrentMixedOperations_ShouldLeaveStatisticsConsistent();

	[Fact]
	public Task ConformanceSuite_ShouldWireEveryArm_Test() => ConformanceSuite_ShouldWireEveryArm();

	[Fact]
	public Task DeadLettered_ShouldBeTerminalOnBothRetrievalPaths_Test() => DeadLettered_ShouldBeTerminalOnBothRetrievalPaths();

	[Fact]
	public Task Drain_MustReturnMessagesFromEveryTenant_Test() => Drain_MustReturnMessagesFromEveryTenant();

	[Fact]
	public Task Fencing_CurrentLeaderToken_ShouldClaimAndComplete_Test() => Fencing_CurrentLeaderToken_ShouldClaimAndComplete();

	[Fact]
	public Task Fencing_HighWaterMark_ShouldSurviveCleanup_Test() => Fencing_HighWaterMark_ShouldSurviveCleanup();

	[Fact]
	public Task Fencing_Refusal_ShouldReportTheHighWaterMark_Test() => Fencing_Refusal_ShouldReportTheHighWaterMark();

	[Fact]
	public Task Fencing_StaleToken_ShouldBeRefusedWithoutApplyingTheMutation_Test() => Fencing_StaleToken_ShouldBeRefusedWithoutApplyingTheMutation();

	[Fact]
	public Task Fencing_SupersededLeader_ShouldNeitherMutateNorLoseTheMessage_Test() => Fencing_SupersededLeader_ShouldNeitherMutateNorLoseTheMessage();

	[Fact]
	public Task GetAllTenantsFailedMessagesAsync_ShouldRespectBatchSize_Test() => GetAllTenantsFailedMessagesAsync_ShouldRespectBatchSize();

	[Fact]
	public Task GetAllTenantsFailedMessagesAsync_ShouldRespectMaxRetries_Test() => GetAllTenantsFailedMessagesAsync_ShouldRespectMaxRetries();

	[Fact]
	public Task GetAllTenantsFailedMessagesAsync_ShouldRespectOlderThan_Test() => GetAllTenantsFailedMessagesAsync_ShouldRespectOlderThan();

	[Fact]
	public Task GetAllTenantsFailedMessagesAsync_ShouldReturnOnlyFailedMessages_Test() => GetAllTenantsFailedMessagesAsync_ShouldReturnOnlyFailedMessages();

	[Fact]
	public Task GetAllTenantsScheduledMessagesAsync_ShouldNotReturnImmediateMessages_Test() => GetAllTenantsScheduledMessagesAsync_ShouldNotReturnImmediateMessages();

	[Fact]
	public Task GetAllTenantsScheduledMessagesAsync_ShouldRespectBatchSize_Test() => GetAllTenantsScheduledMessagesAsync_ShouldRespectBatchSize();

	[Fact]
	public Task GetAllTenantsScheduledMessagesAsync_ShouldReturnScheduledBeforeThreshold_Test() => GetAllTenantsScheduledMessagesAsync_ShouldReturnScheduledBeforeThreshold();

	[Fact]
	public Task GetAllTenantsStatisticsAsync_AfterOperations_ShouldUpdateAccurately_Test() => GetAllTenantsStatisticsAsync_AfterOperations_ShouldUpdateAccurately();

	[Fact]
	public Task GetAllTenantsStatisticsAsync_EmptyStore_ShouldReportZeroCounts_Test() => GetAllTenantsStatisticsAsync_EmptyStore_ShouldReportZeroCounts();

	[Fact]
	public Task GetAllTenantsStatisticsAsync_ShouldReflectMessageCounts_Test() => GetAllTenantsStatisticsAsync_ShouldReflectMessageCounts();

	[Fact]
	public Task GetAllTenantsStatisticsAsync_ShouldTrackAllStatesTogether_Test() => GetAllTenantsStatisticsAsync_ShouldTrackAllStatesTogether();

	[Fact]
	public Task GetAllTenantsStatisticsAsync_ShouldTrackFailedMessages_Test() => GetAllTenantsStatisticsAsync_ShouldTrackFailedMessages();

	[Fact]
	public Task GetAllTenantsStatisticsAsync_ShouldTrackSentMessages_Test() => GetAllTenantsStatisticsAsync_ShouldTrackSentMessages();

	[Fact]
	public Task GetAllTenantsStatisticsAsync_ShouldTrackStagedMessages_Test() => GetAllTenantsStatisticsAsync_ShouldTrackStagedMessages();

	[Fact]
	public Task GetStatistics_MustCountTenantedMessages_Test() => GetStatistics_MustCountTenantedMessages();

	[Fact]
	public Task GetUnsentMessagesAsync_ConcurrentClaimers_ShouldReceiveDisjointSets_Test() => GetUnsentMessagesAsync_ConcurrentClaimers_ShouldReceiveDisjointSets();

	[Fact]
	public Task GetUnsentMessagesAsync_CreatedAt_ShouldRiseWithStagingOrder_Test() => GetUnsentMessagesAsync_CreatedAt_ShouldRiseWithStagingOrder();

	[Fact]
	public Task GetUnsentMessagesAsync_EmptyStore_ShouldReturnNothing_Test() => GetUnsentMessagesAsync_EmptyStore_ShouldReturnNothing();

	[Fact]
	public Task GetUnsentMessagesAsync_NonPositiveBatchSize_ShouldThrowArgumentOutOfRangeException_Test() => GetUnsentMessagesAsync_NonPositiveBatchSize_ShouldThrowArgumentOutOfRangeException();

	[Fact]
	public Task GetUnsentMessagesAsync_ShouldRespectBatchSize_Test() => GetUnsentMessagesAsync_ShouldRespectBatchSize();

	[Fact]
	public Task GetUnsentMessagesAsync_ShouldReturnStagedMessages_Test() => GetUnsentMessagesAsync_ShouldReturnStagedMessages();

	[Fact]
	public Task MarkFailedAsync_NullErrorMessage_ShouldThrowArgumentNullException_Test() => MarkFailedAsync_NullErrorMessage_ShouldThrowArgumentNullException();

	[Fact]
	public Task MarkFailedAsync_NullMessageId_ShouldThrowArgumentException_Test() => MarkFailedAsync_NullMessageId_ShouldThrowArgumentException();

	[Fact]
	public Task MarkFailedAsync_ShouldSetErrorMessage_Test() => MarkFailedAsync_ShouldSetErrorMessage();

	[Fact]
	public Task MarkFailedAsync_ShouldSetRetryCount_Test() => MarkFailedAsync_ShouldSetRetryCount();

	[Fact]
	public Task MarkFailed_AfterTheFloorElapses_ShouldBecomeReclaimable_Test() => MarkFailed_AfterTheFloorElapses_ShouldBecomeReclaimable();

	[Fact]
	public Task MarkFailed_ByANonOwner_ShouldNotReleaseTheClaim_Test() => MarkFailed_ByANonOwner_ShouldNotReleaseTheClaim();

	[Fact]
	public Task MarkFailed_ByTheClaimOwner_ShouldRecordAndRelease_Test() => MarkFailed_ByTheClaimOwner_ShouldRecordAndRelease();

	[Fact]
	public Task MarkFailed_StaleLateReport_ShouldNotLowerTheAttemptCount_Test() => MarkFailed_StaleLateReport_ShouldNotLowerTheAttemptCount();

	[Fact]
	public Task MarkFailed_WithinTheFloor_ShouldNotBeReclaimable_ReservedPath_Test() => MarkFailed_WithinTheFloor_ShouldNotBeReclaimable_ReservedPath();

	[Fact]
	public Task MarkFailed_WithinTheFloor_ShouldNotBeReclaimable_UnclaimedPath_Test() => MarkFailed_WithinTheFloor_ShouldNotBeReclaimable_UnclaimedPath();

	[Fact]
	public Task MarkSentAsync_AlreadySent_ShouldThrowInvalidOperationException_Test() => MarkSentAsync_AlreadySent_ShouldThrowInvalidOperationException();

	[Fact]
	public Task MarkSentAsync_ConcurrentAttempts_ShouldSucceedExactlyOnce_Test() => MarkSentAsync_ConcurrentAttempts_ShouldSucceedExactlyOnce();

	[Fact]
	public Task MarkSentAsync_EmptyMessageId_ShouldThrowArgumentException_Test() => MarkSentAsync_EmptyMessageId_ShouldThrowArgumentException();

	[Fact]
	public Task MarkSentAsync_ExistingMessage_ShouldSetSentAt_Test() => MarkSentAsync_ExistingMessage_ShouldSetSentAt();

	[Fact]
	public Task MarkSentAsync_NonExistent_ShouldThrowInvalidOperationException_Test() => MarkSentAsync_NonExistent_ShouldThrowInvalidOperationException();

	[Fact]
	public Task MarkSentAsync_NullMessageId_ShouldThrowArgumentException_Test() => MarkSentAsync_NullMessageId_ShouldThrowArgumentException();

	[Fact]
	public Task MarkSentAsync_ShouldExcludeFromUnsent_Test() => MarkSentAsync_ShouldExcludeFromUnsent();

	[Fact]
	public Task ReclaimFloorSuite_ShouldExerciseThisStoreOrNotDeclareIt_Test() => ReclaimFloorSuite_ShouldExerciseThisStoreOrNotDeclareIt();

	[Fact]
	public Task OwnershipSuite_ShouldExerciseThisStoreOrNotDeclareIt_Test() => OwnershipSuite_ShouldExerciseThisStoreOrNotDeclareIt();

	[Fact]
	public Task StageMessageAsync_ConcurrentDistinctMessages_ShouldAllSucceed_Test() => StageMessageAsync_ConcurrentDistinctMessages_ShouldAllSucceed();

	[Fact]
	public Task StageMessageAsync_DuplicateId_ShouldThrowInvalidOperationException_Test() => StageMessageAsync_DuplicateId_ShouldThrowInvalidOperationException();

	[Fact]
	public Task StageMessageAsync_NewMessage_ShouldSucceed_Test() => StageMessageAsync_NewMessage_ShouldSucceed();

	[Fact]
	public Task StageMessageAsync_NullMessage_ShouldThrowArgumentNullException_Test() => StageMessageAsync_NullMessage_ShouldThrowArgumentNullException();

	[Fact]
	public Task StageMessageAsync_ShouldRoundTripEveryCallerSuppliedField_Test() => StageMessageAsync_ShouldRoundTripEveryCallerSuppliedField();

	[Fact]
	public Task StageMessageAsync_WithScheduledAt_ShouldStoreCorrectly_Test() => StageMessageAsync_WithScheduledAt_ShouldStoreCorrectly();

	[Fact]
	public Task StageMessage_TenantAttribution_SurvivesTheDrain_Test() => StageMessage_TenantAttribution_SurvivesTheDrain();

	[Fact]
	public Task Store_ShouldNotFaultWhenManyCallersArriveAtOnce_Test() => Store_ShouldNotFaultWhenManyCallersArriveAtOnce();

	[Fact]
	public Task UntenantedPartition_MustRoundTripItsOwnMessage_Test() => UntenantedPartition_MustRoundTripItsOwnMessage();
}
