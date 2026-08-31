// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Outbox.Marten;

using global::Marten;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

using Excalibur.Testing.Conformance;

#pragma warning disable CA1812 // Internal class is never instantiated

namespace Excalibur.Integration.Tests.Data.Outbox;

/// <summary>
/// Real-infrastructure conformance tests for <see cref="MartenOutboxStore"/> using the Outbox
/// Conformance Test Kit against a live PostgreSQL container.
/// </summary>
/// <remarks>
/// Marten runs on PostgreSQL, so this deriver reuses the same shared Postgres container fixture as
/// <see cref="PostgresOutboxStoreConformanceShould"/>, but self-creates its own document schema (a
/// dedicated schema, isolated from the Dapper-managed <c>outbox</c>/<c>outbox_dead_letters</c> tables
/// the sibling deriver owns) rather than requiring pre-existing tables. These tests are never skipped:
/// when Docker is unavailable the fixture fails fast, so a missing container surfaces as a failure
/// rather than a silent pass. The store is constructed via its consumer-default surface — a real
/// <see cref="IDocumentStore"/> built with <c>DocumentStore.For</c> against the fixture's connection
/// string plus an <c>IOptions&lt;MartenOutboxStoreOptions&gt;</c> — proving the store against Marten's
/// real unit-of-work session and conditional-insert semantics rather than a mock.
/// </remarks>
[Collection(PostgresOutboxStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "Postgres")]
public sealed class MartenOutboxStoreConformanceShould : OutboxStoreConformanceTestKit, IAsyncLifetime, IClassFixture<PostgresOutboxStoreContainerFixture>
{
	private readonly PostgresOutboxStoreContainerFixture _fixture;
	private static IDocumentStore? SharedDocumentStore;
	private IDocumentStore? _documentStore;

	/// <summary>
	/// Initializes a new instance of the <see cref="MartenOutboxStoreConformanceShould"/> class.
	/// </summary>
	/// <param name="fixture">The shared Postgres container fixture.</param>
	public MartenOutboxStoreConformanceShould(PostgresOutboxStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	/// <inheritdoc/>
	protected override async Task<IOutboxStore> CreateStoreAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"Postgres container must be available - real-infra conformance is never skipped.");

		// Marten owns its own schema and self-creates the document storage; it does not depend on the
		// Dapper-managed outbox tables the sibling Postgres deriver creates via EnsureInitializedAsync.
		//
		// IDocumentStore is an expensive, thread-safe SINGLETON and is built once for the whole run.
		// DocumentStore.For builds an NpgsqlDataSource, and Npgsql pools that data source BY CONNECTION
		// STRING -- so disposing the store after one test disposes the pool every LATER test in this
		// collection still resolves, and they all fail with
		//   ObjectDisposedException: Cannot access a disposed object (Npgsql.PoolingDataSource).
		// Per-test isolation comes from DeleteAllDocumentsAsync in CleanupAsync, not from rebuilding
		// the store. xUnit constructs a fresh test-class instance per test, so a per-instance store
		// would rebuild-and-dispose on every single arm.
		_documentStore = SharedDocumentStore ??= DocumentStore.For(opts =>
		{
			opts.Connection(_fixture.ConnectionString);
			opts.AutoCreateSchemaObjects = global::JasperFx.AutoCreate.All;
			opts.DatabaseSchemaName = "marten_outbox";
		});

		var options = Options.Create(new MartenOutboxStoreOptions());

		return await Task.FromResult(
			new MartenOutboxStore(_documentStore, options, NullLogger<MartenOutboxStore>.Instance)).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	/// <remarks>
	/// Overriding this is what makes the re-claim floor region assert anything at all. Every arm in that
	/// region returns early when the seam yields no store, so a suite that declares the arms without
	/// overriding the seam reports a region of passing results while exercising the store zero times.
	/// The shared document store is reused for the reason given on <see cref="CreateStoreAsync"/>.
	/// </remarks>
	protected override async Task<IOutboxStore?> CreateStoreWithReclaimFloorAsync(int floorSeconds)
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"Postgres container must be available - real-infra re-claim-floor conformance is never skipped.");

		// Ensures the shared document store exists, so this seam does not depend on arm ordering.
		_ = await CreateStoreAsync().ConfigureAwait(false);

		var options = Options.Create(new MartenOutboxStoreOptions { FailureBackoffFloorSeconds = floorSeconds });

		return new MartenOutboxStore(_documentStore!, options, NullLogger<MartenOutboxStore>.Instance);
	}

	/// <inheritdoc/>
	/// <remarks>
	/// A Marten store generates a fresh dispatcher identity per instance, so a second store over the same
	/// document store is already foreign to the first. Claiming through it writes that identity into the
	/// claim table, which is the state the ownership guard is written against.
	/// </remarks>
	protected override async Task<bool> TryReserveMessageUnderForeignDispatcherAsync(
		IOutboxStore store,
		string messageId)
	{
		var foreignStore = new MartenOutboxStore(
			_documentStore!,
			Options.Create(new MartenOutboxStoreOptions()),
			NullLogger<MartenOutboxStore>.Instance);

		var reserved = await foreignStore.GetUnsentMessagesAsync(50, CancellationToken.None)
			.ConfigureAwait(false);

		return reserved.Any(m => string.Equals(m.Id, messageId, StringComparison.Ordinal));
	}

	/// <inheritdoc/>
	protected override async Task CleanupAsync()
	{
		if (_documentStore is not null)
		{
			// Documents only. The store itself is the shared singleton above and is deliberately NOT
			// disposed here -- disposing it releases the connection-string-pooled NpgsqlDataSource that
			// every later arm in this collection still needs.
			await _documentStore.Advanced.Clean.DeleteAllDocumentsAsync().ConfigureAwait(false);
		}
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
