// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;

using Excalibur.Outbox.InMemory;
using Excalibur.Outbox.MultiTransport;

using Excalibur.Testing.Conformance;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Tests.Testing.Conformance;

/// <summary>
/// Holds the published outbox conformance kit to <see cref="MultiTransportOutboxStore"/>, the router a
/// consumer actually receives when multi-transport routing is enabled, rather than to the raw store it
/// wraps.
/// </summary>
/// <remarks>
/// <see cref="MultiTransportOutboxStore"/> derives from <see cref="OutboxStoreDecorator"/> and forwards
/// every capability it does not itself provide through <see cref="IServiceProvider.GetService(Type)"/> to
/// the store it wraps, so a full run of the published kit against it exercises the router the same way a
/// consumer's own code does: routing decisions on top, every base arm underneath.
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Test method naming convention")]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Pattern", "STORE")]
public sealed class MultiTransportOutboxStoreConformanceShould : OutboxStoreConformanceTestKit
{
	/// <inheritdoc />
	protected override Task<IOutboxStore> CreateStoreAsync() =>
		Task.FromResult<IOutboxStore>(NewRouter(NewInnerStore()));

	/// <inheritdoc />
	protected override Task<IOutboxStore?> CreateStoreWithReclaimFloorAsync(int floorSeconds) =>
		Task.FromResult<IOutboxStore?>(NewRouter(NewInnerStore(floorSeconds)));

	/// <inheritdoc />
	protected override Task ResetDataAsync() => Task.CompletedTask;

	private static MultiTransportOutboxStore NewRouter(IOutboxStore inner) =>
		new MultiTransportOutboxStore(inner, Options.Create(new MultiTransportOutboxOptions()), NullLogger<MultiTransportOutboxStore>.Instance);

	private static InMemoryOutboxStore NewInnerStore(int? floorSeconds = null)
	{
		var options = new InMemoryOutboxOptions();
		if (floorSeconds is { } floor)
		{
			options.FailureBackoffFloorSeconds = floor;
		}

		return new InMemoryOutboxStore(Options.Create(options), NullLogger<InMemoryOutboxStore>.Instance);
	}

	// ---------------------------------------------------------------------------------------------------
	// Conformance arm wiring. One member per kit arm, attributed for xUnit -- see
	// InMemoryOutboxStoreConformanceShould for why this is written out rather than reflected.
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
