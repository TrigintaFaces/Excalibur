// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Inbox.MongoDB;
using Excalibur.Testing.Conformance;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

namespace Excalibur.Integration.Tests.Data.Inbox;

/// <summary>
/// Binds <see cref="MongoDbInboxStore"/> to the SHIPPED <see cref="InboxStoreConformanceTestKit"/>
/// against a live MongoDB container.
/// </summary>
/// <remarks>
/// The store is built through its options-only constructor, which creates the provider's DEFAULT
/// <c>MongoClient</c> — and therefore the default serializer — from the connection string. That is the
/// surface a normal consumer uses; a hand-configured client would exercise a configuration the consumer
/// does not have.
/// </remarks>
[Collection(MongoDbInboxStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "MongoDb")]
[Trait("Pattern", "STORE")]
public sealed class MongoDbInboxStoreKitConformanceShould : InboxStoreConformanceTestKit, IAsyncLifetime
{
	private readonly MongoDbInboxStoreContainerFixture _fixture;
	private IInboxStore? _store;

	/// <summary>
	/// Initializes a new instance of the <see cref="MongoDbInboxStoreKitConformanceShould"/> class.
	/// </summary>
	/// <param name="fixture">The MongoDB container fixture, shared by the collection.</param>
	public MongoDbInboxStoreKitConformanceShould(MongoDbInboxStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	/// <inheritdoc/>
	protected override IInboxStore CreateStore()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"MongoDB container must be available - real-infra conformance is never skipped. "
			+ _fixture.InitializationError);

		if (_store is not null)
		{
			return _store;
		}

		var options = Options.Create(new MongoDbInboxOptions
		{
			ConnectionString = _fixture.ConnectionString,
			DatabaseName = _fixture.DatabaseName,
		});

		// The AMBIENT context, not the fixed single-tenant one the private-base suite uses. The store
		// resolves its partition per call through TenantScope.FromContext (MongoDbInboxStore.cs:55-56),
		// so varying the ambient tenant around one store is what the kit's isolation arms require; a fixed
		// context would address one partition for every arm and pass those arms without exercising them.
		_store = new MongoDbInboxStore(
			options,
			NullLogger<MongoDbInboxStore>.Instance,
			new ConformanceAmbientTenantContext());

		return _store;
	}

	/// <inheritdoc/>
	/// <remarks>
	/// Drops the test database, which is data-only with respect to the store: the kit runs this after
	/// construction and before the arm, and the store creates its collection and indexes lazily on first
	/// use, so the arm gets a clean collection with its unique index intact.
	/// </remarks>
	protected override Task CleanupAsync() => _fixture.CleanupAsync();

	/// <inheritdoc/>
	public ValueTask InitializeAsync() => ValueTask.CompletedTask;

	/// <inheritdoc/>
	public async ValueTask DisposeAsync()
	{
		if (_store is IAsyncDisposable asyncDisposable)
		{
			await asyncDisposable.DisposeAsync().ConfigureAwait(false);
		}
		else if (_store is IDisposable disposable)
		{
			disposable.Dispose();
		}

		_store = null;
	}

	#region Create arms

	[Fact]
	public Task CreateEntryAsync_NewEntry_ShouldSucceed_Test() =>
		CreateEntryAsync_NewEntry_ShouldSucceed();

	[Fact]
	public Task CreateEntryAsync_DuplicateEntry_ShouldThrow_Test() =>
		CreateEntryAsync_DuplicateEntry_ShouldThrow();

	[Fact]
	public Task CreateEntryAsync_WithAllMetadata_ShouldPreserve_Test() =>
		CreateEntryAsync_WithAllMetadata_ShouldPreserve();

	#endregion Create arms

	#region Process arms

	[Fact]
	public Task MarkProcessedAsync_ExistingEntry_ShouldSucceed_Test() =>
		MarkProcessedAsync_ExistingEntry_ShouldSucceed();

	[Fact]
	public Task TryMarkAsProcessedAsync_FirstTime_ShouldReturnTrue_Test() =>
		TryMarkAsProcessedAsync_FirstTime_ShouldReturnTrue();

	[Fact]
	public Task TryMarkAsProcessedAsync_AlreadyProcessed_ShouldReturnFalse_Test() =>
		TryMarkAsProcessedAsync_AlreadyProcessed_ShouldReturnFalse();

	[Fact]
	public Task IsProcessedAsync_ProcessedMessage_ShouldReturnTrue_Test() =>
		IsProcessedAsync_ProcessedMessage_ShouldReturnTrue();

	[Fact]
	public Task IsProcessedAsync_UnprocessedMessage_ShouldReturnFalse_Test() =>
		IsProcessedAsync_UnprocessedMessage_ShouldReturnFalse();

	#endregion Process arms

	#region Fail arms

	[Fact]
	public Task MarkFailedAsync_ExistingEntry_ShouldSetStatusAndError_Test() =>
		MarkFailedAsync_ExistingEntry_ShouldSetStatusAndError();

	[Fact]
	public Task MarkFailedAsync_ShouldIncrementRetryCount_Test() =>
		MarkFailedAsync_ShouldIncrementRetryCount();

	[Fact]
	public Task GetAllTenantsFailedEntriesAsync_ShouldRespectMaxRetries_Test() =>
		GetAllTenantsFailedEntriesAsync_ShouldRespectMaxRetries();

	[Fact]
	public Task GetAllTenantsFailedEntriesAsync_MustReturnEveryTenantsFailedEntries_Test() =>
		GetAllTenantsFailedEntriesAsync_MustReturnEveryTenantsFailedEntries();


	#endregion Fail arms

	#region Query arms

	[Fact]
	public Task GetEntryAsync_Existing_ShouldReturnEntry_Test() =>
		GetEntryAsync_Existing_ShouldReturnEntry();

	[Fact]
	public Task GetEntryAsync_NonExistent_ShouldReturnNull_Test() =>
		GetEntryAsync_NonExistent_ShouldReturnNull();

	[Fact]
	public Task GetAllTenantsStatisticsAsync_ShouldReturnCorrectCounts_Test() =>
		GetAllTenantsStatisticsAsync_ShouldReturnCorrectCounts();

	#endregion Query arms

	#region Cleanup arms

	[Fact]
	public Task CleanupAllTenantsProcessedEntriesAsync_OldProcessed_ShouldRemove_Test() =>
		CleanupAllTenantsProcessedEntriesAsync_OldProcessed_ShouldRemove();

	[Fact]
	public Task CleanupAllTenantsProcessedEntriesAsync_ShouldPreserveRecent_Test() =>
		CleanupAllTenantsProcessedEntriesAsync_ShouldPreserveRecent();

	#endregion Cleanup arms

	#region Key-isolation arms

	[Fact]
	public Task Entries_ShouldIsolateByMessageIdAndHandlerType_Test() =>
		Entries_ShouldIsolateByMessageIdAndHandlerType();

	[Fact]
	public Task SameMessageId_DifferentHandlers_ShouldBeIndependent_Test() =>
		SameMessageId_DifferentHandlers_ShouldBeIndependent();

	#endregion Key-isolation arms

	#region Edge cases

	[Fact]
	public Task GetAllTenantsEntriesAsync_ShouldReturnAllEntries_Test() =>
		GetAllTenantsEntriesAsync_ShouldReturnAllEntries();

	#endregion Edge cases

	#region Tenant-isolation arms

	[Fact]
	public Task TryMarkAsProcessed_SameMessageIdInAnotherTenant_MustNotBeSwallowedAsADuplicate_Test() =>
		TryMarkAsProcessed_SameMessageIdInAnotherTenant_MustNotBeSwallowedAsADuplicate();

	[Fact]
	public Task IsProcessed_MustNotReportAnotherTenantsMessageAsProcessed_Test() =>
		IsProcessed_MustNotReportAnotherTenantsMessageAsProcessed();

	[Fact]
	public Task CreateEntry_SameMessageIdInAnotherTenant_MustNotCollide_Test() =>
		CreateEntry_SameMessageIdInAnotherTenant_MustNotCollide();

	#endregion Tenant-isolation arms

	#region Concurrency arm

	[Fact]
	public Task ConcurrentClaimAndMark_MustElectExactlyOneWinner_AndKeepTheProcessedMarker_Test() =>
		ConcurrentClaimAndMark_MustElectExactlyOneWinner_AndKeepTheProcessedMarker();

	[Fact]
	public Task ExpiredLease_MustBeReclaimableByAnotherProcessor_Test() =>
		ExpiredLease_MustBeReclaimableByAnotherProcessor();

	[Fact]
	public Task LiveLease_MustNotBeReclaimableByAnotherProcessor_Test() =>
		LiveLease_MustNotBeReclaimableByAnotherProcessor();

	[Fact]
	public Task ExpiredLease_MustBeReadmittedByTheRetryDrainRead_Test() =>
		ExpiredLease_MustBeReadmittedByTheRetryDrainRead();

	[Fact]
	public Task LiveLease_MustNotBeReadmittedByTheRetryDrainRead_Test() =>
		LiveLease_MustNotBeReadmittedByTheRetryDrainRead();

	[Fact]
	public Task LeaselessClaim_MustNotBeReclaimableByTheLeasePath_Test() =>
		LeaselessClaim_MustNotBeReclaimableByTheLeasePath();

	[Fact]
	public Task ReleasedClaim_MustBeReadmittedForRedelivery_Test() =>
		ReleasedClaim_MustBeReadmittedForRedelivery();

	[Fact]
	public Task Release_MustNoOpOnAnUnheldClaim_AndMustNotEraseAFinalizedRecord_Test() =>
		Release_MustNoOpOnAnUnheldClaim_AndMustNotEraseAFinalizedRecord();

	#endregion Concurrency arm

	#region Suite wiring

	[Fact]
	public Task ProcessedEntry_MustNotBeReadmittedByTheClaimPath_Test() =>
		ProcessedEntry_MustNotBeReadmittedByTheClaimPath();

	[Fact]
	public Task ProcessedEntry_MustNotBeDemotedByTheProcessingMark_Test() =>
		ProcessedEntry_MustNotBeDemotedByTheProcessingMark();

	[Fact]
	public Task FailedEntry_MustBeReAdmittedByTheLeasePath_Test() =>
		FailedEntry_MustBeReAdmittedByTheLeasePath();

	[Fact]
	public Task FailedEntry_MustNotBeReadmittedByTheClaimPath_Test() =>
		FailedEntry_MustNotBeReadmittedByTheClaimPath();

	/// <summary>
	/// Fails if this suite stops exposing an arm the shipped kit declares.
	/// </summary>
	/// <remarks>
	/// An arm nobody wires never executes, and an arm that never executes cannot fail -- in the results it
	/// is indistinguishable from one that passed. Wiring is therefore checked rather than asserted: an arm
	/// added to the shipped kit turns this red here instead of going silently unrun.
	/// </remarks>
	[Fact]
	public Task ConformanceSuite_ShouldWireEveryArm_Test() =>
		ConformanceSuite_ShouldWireEveryArm();

	#endregion Suite wiring
}
