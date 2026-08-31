// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Inbox.Redis;
using Excalibur.Testing.Conformance;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

using StackExchange.Redis;

namespace Excalibur.Integration.Tests.Redis.Inbox;

/// <summary>
/// Binds <see cref="RedisInboxStore"/> to the SHIPPED <see cref="InboxStoreConformanceTestKit"/> against
/// a live Redis container.
/// </summary>
/// <remarks>
/// The key prefix is unique per suite instance, so the arms cannot collide with each other or with the
/// existing private-base suite sharing this container.
/// </remarks>
[Collection(RedisTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "Redis")]
[Trait("Pattern", "STORE")]
public sealed class RedisInboxStoreKitConformanceShould : InboxStoreConformanceTestKit, IAsyncLifetime
{
	private readonly RedisContainerFixture _fixture;
	private readonly string _keyPrefix = $"inbox-kit-{Guid.NewGuid():N}";
	private ConnectionMultiplexer? _connection;
	private IInboxStore? _store;

	/// <summary>
	/// Initializes a new instance of the <see cref="RedisInboxStoreKitConformanceShould"/> class.
	/// </summary>
	/// <param name="fixture">The Redis container fixture, shared by the collection.</param>
	public RedisInboxStoreKitConformanceShould(RedisContainerFixture fixture)
	{
		_fixture = fixture;
	}

	/// <inheritdoc/>
	/// <remarks>
	/// Not implementable synchronously: the multiplexer must be connected before the store can be built.
	/// <see cref="CreateStoreAsync"/> is the override this suite uses.
	/// </remarks>
	protected override IInboxStore CreateStore() =>
		throw new NotSupportedException(
			$"{nameof(RedisInboxStoreKitConformanceShould)} overrides {nameof(CreateStoreAsync)}; "
			+ "the store requires an awaited ConnectionMultiplexer and cannot be built synchronously.");

	/// <inheritdoc/>
	protected override async Task<IInboxStore> CreateStoreAsync()
	{
		if (_store is not null)
		{
			return _store;
		}

		_fixture.DockerAvailable.ShouldBeTrue(
			"Redis container must be available - real-infra conformance is never skipped. "
			+ _fixture.InitializationError);

		var options = Options.Create(new RedisInboxOptions
		{
			ConnectionString = _fixture.ConnectionString,
			KeyPrefix = _keyPrefix,
			DefaultTtlSeconds = 604800,
			ConnectTimeoutMs = 5000,
			SyncTimeoutMs = 5000,
			AbortOnConnectFail = false
		});

		_connection = await ConnectionMultiplexer.ConnectAsync(_fixture.ConnectionString)
			.ConfigureAwait(false);

		// The AMBIENT context: the store resolves its partition per call through
		// TenantScope.FromContext (RedisInboxStore.cs:153-154), so the kit's isolation arms need the
		// ambient scope to reach it. A fixed context addresses one partition for every arm.
		_store = new RedisInboxStore(
			_connection,
			options,
			NullLogger<RedisInboxStore>.Instance,
			new ConformanceAmbientTenantContext());

		return _store;
	}

	/// <inheritdoc/>
	/// <remarks>
	/// Deletes this suite's keys ONLY, and never closes the multiplexer. The kit runs this after building
	/// the store and before each arm; closing the connection here would hand every arm a dead handle,
	/// which the kit's own documentation names as the failure this override exists to prevent. Disposal
	/// belongs in <see cref="DisposeAsync"/>.
	/// </remarks>
	protected override async Task CleanupAsync()
	{
		if (_connection is null)
		{
			return;
		}

		var server = _connection.GetServer(_connection.GetEndPoints().First());
		var database = _connection.GetDatabase();

		await foreach (var key in server.KeysAsync(pattern: $"{_keyPrefix}*"))
		{
			_ = await database.KeyDeleteAsync(key).ConfigureAwait(false);
		}
	}

	/// <inheritdoc/>
	public ValueTask InitializeAsync() => ValueTask.CompletedTask;

	/// <inheritdoc/>
	public async ValueTask DisposeAsync()
	{
		if (_store is IAsyncDisposable storeAsyncDisposable)
		{
			await storeAsyncDisposable.DisposeAsync().ConfigureAwait(false);
		}
		else if (_store is IDisposable storeDisposable)
		{
			storeDisposable.Dispose();
		}

		_store = null;

		if (_connection is not null)
		{
			await _connection.CloseAsync().ConfigureAwait(false);
			_connection.Dispose();
			_connection = null;
		}
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
