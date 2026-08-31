// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Outbox.MongoDB;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

using Excalibur.Testing.Conformance;

#pragma warning disable CA1812 // Internal class is never instantiated

namespace Excalibur.Integration.Tests.Data.Outbox;

/// <summary>
/// Real-infrastructure conformance tests for <see cref="MongoDbOutboxStore"/> using the Outbox
/// Conformance Test Kit against a live MongoDB container.
/// </summary>
/// <remarks>
/// These tests verify that the MongoDB implementation correctly implements the
/// <see cref="IOutboxStore"/> contract — including atomic status transitions and concurrent
/// MarkSent — using TestContainers. They are never skipped: when Docker is unavailable the fixture
/// fails fast, so a missing container surfaces as a failure rather than a silent pass. The store is
/// constructed via its options-only constructor, which builds the provider's DEFAULT
/// <c>MongoClient</c> (and therefore the default serializer) from the connection string — the surface
/// a normal consumer uses. The store self-initializes its collection and indexes on first use.
/// </remarks>
[Collection(MongoDbOutboxStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "MongoDb")]
public sealed class MongoDbOutboxStoreConformanceShould : OutboxStoreConformanceTestKit, IAsyncLifetime, IClassFixture<MongoDbOutboxStoreContainerFixture>
{
	private readonly MongoDbOutboxStoreContainerFixture _fixture;

	/// <summary>
	/// Initializes a new instance of the <see cref="MongoDbOutboxStoreConformanceShould"/> class.
	/// </summary>
	/// <param name="fixture">The MongoDB container fixture.</param>
	public MongoDbOutboxStoreConformanceShould(MongoDbOutboxStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	/// <inheritdoc/>
	protected override Task<IOutboxStore> CreateStoreAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"MongoDB container must be available - real-infra conformance is never skipped.");

		var options = Options.Create(new MongoDbOutboxOptions
		{
			ConnectionString = _fixture.ConnectionString,
			DatabaseName = _fixture.DatabaseName,
		});

		// Options-only constructor: the store builds the provider's DEFAULT MongoClient (default
		// serializer) from the connection string — the surface most consumers use. The store
		// self-initializes its collection and indexes on first use.
		return Task.FromResult<IOutboxStore>(
			new MongoDbOutboxStore(options, NullLogger<MongoDbOutboxStore>.Instance));
	}

	/// <inheritdoc/>
	/// <remarks>
	/// wseau9 (SA seam ruling): opt real MongoDB into the universal re-claim-floor property arms (R1 floor,
	/// R3 monotonic, and the owned-path liveness twin). Uses the DEFAULT <c>MongoClient</c> and the REAL system
	/// clock (default <see cref="System.TimeProvider"/>) so the base arms' real-time floor poll (F=1s) exercises
	/// the store's actual <c>NextAttemptAt</c> gate — never a fake clock (which the base's wall-clock poll could
	/// not advance). RED against pre-fix Mongo, whose claim filtered <c>Status==Staged</c> only and set no
	/// <c>NextAttemptAt</c> → a failed message stranded (§1.5).
	/// </remarks>
	protected override Task<IOutboxStore?> CreateStoreWithReclaimFloorAsync(int floorSeconds)
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"MongoDB container must be available - real-infra re-claim-floor conformance is never skipped.");

		var options = Options.Create(new MongoDbOutboxOptions
		{
			ConnectionString = _fixture.ConnectionString,
			DatabaseName = _fixture.DatabaseName,
			FailureBackoffFloorSeconds = floorSeconds,
		});

		return Task.FromResult<IOutboxStore?>(
			new MongoDbOutboxStore(options, NullLogger<MongoDbOutboxStore>.Instance));
	}

	/// <inheritdoc/>
	/// <remarks>
	/// Reserve the message under a FOREIGN <c>ProcessorId</c> — a second store instance over the same
	/// collection whose owner token differs from the store that calls <c>MarkFailedAsync</c> — the only way to
	/// exercise the R2 ownership guard (<c>LeasedBy == null || LeasedBy == ProcessorId</c>,
	/// <c>MongoDbOutboxStore</c> claim/mark). The claiming store stamps <c>LeasedBy = ProcessorId</c>, so a
	/// distinct <c>ProcessorId</c> makes the subsequent non-owner mark a no-op (R2 safety), while the real owner
	/// can still mark its own claim (R2 liveness).
	/// </remarks>
	protected override async Task<bool> TryReserveMessageUnderForeignDispatcherAsync(IOutboxStore store, string messageId)
	{
		var foreignOptions = Options.Create(new MongoDbOutboxOptions
		{
			ConnectionString = _fixture.ConnectionString,
			DatabaseName = _fixture.DatabaseName,
			ProcessorId = "conformance-foreign-leader",
		});

		var foreignStore = new MongoDbOutboxStore(foreignOptions, NullLogger<MongoDbOutboxStore>.Instance);
		var reserved = await foreignStore
			.GetUnsentMessagesAsync(50, CancellationToken.None).ConfigureAwait(false);
		return reserved.Any(m => m.Id == messageId);
	}

	/// <inheritdoc/>
	protected override async Task CleanupAsync()
	{
		await _fixture.CleanupAsync().ConfigureAwait(false);
	}

	/// <summary>
	/// xnyhjd (REVIEW_CODE P1, cross-provider closure of bbazps/cys98n) — Mongo <see cref="MongoDbOutboxStore"/>
	/// <c>EnqueueAsync(IDispatchMessage, IMessageContext, …)</c> must derive the routing <c>Destination</c>
	/// from the message context (not silently pass the type name), falling back to the message TYPE name when
	/// the context carries none. SQL/Postgres were fixed this sprint (bbazps, Postgres-only); REVIEW_CODE caught
	/// Redis + Mongo still dropped it. Real-infra round-trip against a live MongoDB container.
	/// </summary>
	[Fact]
	public async Task EnqueueAsync_DerivesDestinationFromContext_ElseFallsBackToTypeName()
	{
		await CleanupAsync().ConfigureAwait(false);
		var store = await CreateStoreAsync().ConfigureAwait(false);
		const string ConfiguredDestination = "orders.commands.v1";

		// Case A: context carries a destination. Case B: none → fall back to the message type name.
		await store.EnqueueAsync(new DestinationDerivationTestMessage(), CreateContext("ctx-derived", ConfiguredDestination), CancellationToken.None).ConfigureAwait(false);
		await store.EnqueueAsync(new DestinationDerivationTestMessage(), CreateContext("ctx-fallback", destination: null), CancellationToken.None).ConfigureAwait(false);

		var messages = (await store.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false)).ToList();

		// pfgcj6: Mongo now falls back to the SIMPLE type name (message.GetType().Name), matching Postgres.
		messages.ShouldContain(
			m => m.Destination == ConfiguredDestination,
			"xnyhjd: Mongo EnqueueAsync must persist the destination derived from the context metadata.");
		messages.ShouldContain(
			m => m.Destination == nameof(DestinationDerivationTestMessage),
			"xnyhjd/pfgcj6: with no context destination, Mongo EnqueueAsync must fall back to the message TYPE name (simple, Postgres-parity), not drop it.");
	}

	private static IMessageContext CreateContext(string messageId, string? destination)
	{
		var items = new Dictionary<string, object>(StringComparer.Ordinal);
		if (destination is not null)
		{
			items[MetadataPropertyKeys.Destination] = destination;
		}

		// A bare fake returns "" for unconfigured strings, tripping ExtractMetadata's non-empty guards;
		// configure the direct-read properties: CorrelationId non-empty, CausationId null.
		var context = A.Fake<IMessageContext>();
		_ = A.CallTo(() => context.MessageId).Returns(messageId);
		_ = A.CallTo(() => context.CorrelationId).Returns(messageId);
		_ = A.CallTo(() => context.CausationId).Returns((string?)null);
		_ = A.CallTo(() => context.Items).Returns(items);
		return context;
	}

	private sealed record DestinationDerivationTestMessage : IDispatchMessage;

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
