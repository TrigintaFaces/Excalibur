// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Amazon.DynamoDBv2;

using Excalibur.Data.DynamoDb;
using Excalibur.Dispatch;
using Excalibur.Inbox.DynamoDb;
using Excalibur.Testing.Conformance;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

namespace Excalibur.Integration.Tests.Data.Inbox;

/// <summary>
/// Binds <see cref="DynamoDbInboxStore"/> to the SHIPPED <see cref="InboxStoreConformanceTestKit"/>
/// against a live LocalStack DynamoDB container.
/// </summary>
/// <remarks>
/// The store creates its own table through <c>CreateTableIfNotExists</c>, which is the real
/// consumer-supplied-client path; the fixture does not pre-create it. TTL auto-reap is disabled so it can
/// never race the explicit-cleanup arms.
/// </remarks>
[Collection(DynamoDbInboxStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "DynamoDb")]
[Trait("Pattern", "STORE")]
public sealed class DynamoDbInboxStoreKitConformanceShould : InboxStoreConformanceTestKit, IAsyncLifetime
{
	private readonly DynamoDbInboxStoreContainerFixture _fixture;
	private IInboxStore? _store;
	private string? _tableName;

	/// <summary>
	/// Initializes a new instance of the <see cref="DynamoDbInboxStoreKitConformanceShould"/> class.
	/// </summary>
	/// <param name="fixture">The LocalStack DynamoDB container fixture, shared by the collection.</param>
	public DynamoDbInboxStoreKitConformanceShould(DynamoDbInboxStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	/// <inheritdoc/>
	/// <remarks>
	/// Not implementable synchronously: the table is provisioned by an awaited
	/// <c>InitializeAsync</c> on the store. <see cref="CreateStoreAsync"/> is the override this suite uses.
	/// </remarks>
	protected override IInboxStore CreateStore() =>
		throw new NotSupportedException(
			$"{nameof(DynamoDbInboxStoreKitConformanceShould)} overrides {nameof(CreateStoreAsync)}; "
			+ "the store provisions its table asynchronously and cannot be built synchronously.");

	/// <inheritdoc/>
	protected override async Task<IInboxStore> CreateStoreAsync()
	{
		if (_store is not null)
		{
			return _store;
		}

		_fixture.DockerAvailable.ShouldBeTrue(
			"LocalStack DynamoDB container must be available - real-infra conformance is never skipped. "
			+ _fixture.InitializationError);

		// One table per suite instance, and xUnit builds a fresh instance per arm, so each arm starts from
		// an empty table without needing a between-arm wipe.
		_tableName = $"{_fixture.TableName}_kit_{Guid.NewGuid():N}";

		var options = Options.Create(new DynamoDbInboxOptions
		{
			TableName = _tableName,
			CreateTableIfNotExists = true,

			// Keep TTL auto-reap off so it never races the explicit-cleanup arms.
			DefaultTtlSeconds = 0,
			Connection = new DynamoDbConnectionOptions { ServiceUrl = _fixture.ServiceUrl },
		});

		var store = new DynamoDbInboxStore(
			_fixture.Client,
			options,
			NullLogger<DynamoDbInboxStore>.Instance,
			new ConformanceAmbientTenantContext());

		await store.InitializeAsync(CancellationToken.None).ConfigureAwait(false);

		_store = store;
		return _store;
	}

	/// <inheritdoc/>
	/// <remarks>
	/// A DELIBERATE no-op, overriding the default that forwards to <see cref="CleanupAsync"/>.
	/// <see cref="CleanupAsync"/> drops the table, and the kit calls the reset AFTER building the store —
	/// so the default would delete the table the arm is about to use and every arm would fail on a missing
	/// table rather than on the contract. Isolation is supplied instead by the per-instance table above;
	/// the drop runs at <see cref="DisposeAsync"/>.
	/// </remarks>
	protected override Task ResetDataAsync() => Task.CompletedTask;

	/// <inheritdoc/>
	protected override async Task CleanupAsync()
	{
		if (_tableName is not null)
		{
			await _fixture.DeleteTableAsync(_tableName, CancellationToken.None).ConfigureAwait(false);
			_tableName = null;
		}
	}

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

		// The kit never calls CleanupAsync for this suite (ResetDataAsync is a no-op above), so the table
		// is dropped here or not at all.
		await CleanupAsync().ConfigureAwait(false);
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
