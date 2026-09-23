// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;

namespace Tests.Shared.Conformance.Inbox;

/// <summary>
/// Base class for IInboxStore conformance tests.
/// Implementations must provide a concrete IInboxStore instance for testing.
/// </summary>
/// <remarks>
/// <para>
/// This conformance test kit verifies that inbox store implementations
/// correctly implement the IInboxStore interface contract, including:
/// </para>
/// <list type="bullet">
///   <item>Idempotent message processing (at-most-once semantics)</item>
///   <item>Composite key behavior (messageId, handlerType)</item>
///   <item>Status transitions and state management</item>
///   <item>Cleanup and statistics</item>
///   <item>Concurrent access and atomicity</item>
/// </list>
/// <para>
/// To create conformance tests for your own IInboxStore implementation:
/// <list type="number">
///   <item>Inherit from InboxStoreConformanceTestBase</item>
///   <item>Override CreateStoreAsync() to create an instance of your IInboxStore implementation</item>
///   <item>Override CleanupAsync() to properly clean up the store between tests</item>
/// </list>
/// </para>
/// </remarks>
public abstract class InboxStoreConformanceTestBase : IAsyncLifetime
{
	/// <summary>
	/// The inbox store instance under test.
	/// </summary>
	// protected set so a provider whose fault is IRREVERSIBLE on the store instance (a disposed client,
	// a closed connection) can rebuild it in RemovePersistenceFaultAsync. The alternative is a fault that
	// destroys the backing store, which erases the evidence the durability arm's safety half inspects.
	protected IInboxStore Store { get; set; } = null!;

	/// <summary>
	/// The inbox store admin interface, resolved from the Store via cast.
	/// Available after <see cref="InitializeAsync"/> completes.
	/// </summary>
	protected IInboxStoreAdmin AdminStore { get; private set; } = null!;

	/// <summary>
	/// The tenant context this deriver hands its store.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Admin arms address a partition EXPLICITLY, while an entry created through <see cref="Store"/> lands
	/// in whatever partition the store derives from this context. The two must agree: addressed
	/// anywhere else, a correct store answers <see cref="InboxMarkFailedOutcome.EntryNotFound"/> for an
	/// entry that exists -- in another partition -- which is tenant isolation working, not a defect.
	/// </para>
	/// <para>
	/// Abstract so a deriver cannot omit it. A default would silently assume the store is untenanted,
	/// which is exactly the assumption that made this arm fail against every store whose context
	/// carries a tenant.
	/// </para>
	/// </remarks>
	protected abstract ITenantContext StoreTenantContext { get; }

	/// <summary>
	/// The partition <see cref="Store"/> writes ambient entries into, computed by the same mapping the
	/// stores use rather than asserted by the test.
	/// </summary>
	private KeyedTenantPartition StorePartition => KeyedTenantPartition.FromContext(StoreTenantContext);

	/// <inheritdoc/>
	public async ValueTask InitializeAsync()
	{
		Store = await CreateStoreAsync().ConfigureAwait(false);
		AdminStore = Store as IInboxStoreAdmin
			?? throw new InvalidOperationException(
				$"Conformance test requires the inbox store ({Store.GetType().Name}) " +
				$"to implement IInboxStoreAdmin for admin operation tests.");
	}

	/// <inheritdoc/>
	public async ValueTask DisposeAsync()
	{
		await CleanupAsync().ConfigureAwait(false);

		if (Store is IAsyncDisposable asyncDisposable)
		{
			await asyncDisposable.DisposeAsync().ConfigureAwait(false);
		}
		else if (Store is IDisposable disposable)
		{
			disposable.Dispose();
		}
	}

	/// <summary>
	/// Creates a new instance of the IInboxStore implementation under test.
	/// </summary>
	/// <returns>A configured IInboxStore instance.</returns>
	protected abstract Task<IInboxStore> CreateStoreAsync();

	/// <summary>
	/// Creates a second, independent store instance for the fresh-instance durability read-back in
	/// <see cref="ThrowNotNoOpOnPersistenceFailure"/>.
	/// </summary>
	/// <remarks>
	/// Defaults to <see cref="CreateStoreAsync"/>, which is correct whenever a fresh instance still points
	/// at the SAME physical backing store (a shared connection string / database / table or collection
	/// name) -- true for most providers. Override this instead when it does not -- for example a fixture
	/// that randomises a per-call namespace (a key prefix) for isolation BETWEEN DIFFERENT tests sharing
	/// one container: a naive second <see cref="CreateStoreAsync"/> call there would build a store pointed
	/// at a namespace of its own, and could never observe what the first instance wrote regardless of
	/// whether the write actually succeeded -- silently making the safety arm vacuous rather than red or
	/// green on the property it exists to check.
	/// </remarks>
	protected virtual Task<IInboxStore> CreateVerificationStoreAsync() => CreateStoreAsync();

	/// <summary>
	/// Cleans up the IInboxStore instance after each test.
	/// </summary>
	protected abstract Task CleanupAsync();

	#region Interface Implementation Tests

	[Fact]
	public void Store_ShouldImplementIInboxStore()
	{
		// Assert
		_ = Store.ShouldBeAssignableTo<IInboxStore>();
	}

	#endregion Interface Implementation Tests

	#region TryMarkAsProcessed Tests

	[Fact]
	public async Task TryMarkAsProcessed_FirstTime_ReturnsTrue()
	{
		// Arrange
		var messageId = Guid.NewGuid().ToString();
		var handlerType = "Handler.Type.A";

		// Act
		var result = await Store.TryMarkAsProcessedAsync(messageId, handlerType, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.ShouldBeTrue("First call should succeed and return true");
	}

	[Fact]
	public async Task TryMarkAsProcessed_SecondTime_ReturnsFalse()
	{
		// Arrange
		var messageId = Guid.NewGuid().ToString();
		var handlerType = "Handler.Type.A";

		_ = await Store.TryMarkAsProcessedAsync(messageId, handlerType, CancellationToken.None)
			.ConfigureAwait(false);

		// Act
		var result = await Store.TryMarkAsProcessedAsync(messageId, handlerType, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.ShouldBeFalse("Second call with same key should return false (duplicate)");
	}

	[Fact]
	public async Task TryMarkAsProcessed_DifferentHandlers_SameMessage_BothSucceed()
	{
		// Arrange
		var messageId = Guid.NewGuid().ToString();
		var handlerA = "Handler.Type.A";
		var handlerB = "Handler.Type.B";

		// Act
		var resultA = await Store.TryMarkAsProcessedAsync(messageId, handlerA, CancellationToken.None)
			.ConfigureAwait(false);
		var resultB = await Store.TryMarkAsProcessedAsync(messageId, handlerB, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		resultA.ShouldBeTrue("First handler should succeed");
		resultB.ShouldBeTrue("Second handler with different type should also succeed");
	}

	[Fact]
	public async Task TryMarkAsProcessed_DifferentMessages_SameHandler_BothSucceed()
	{
		// Arrange
		var messageId1 = Guid.NewGuid().ToString();
		var messageId2 = Guid.NewGuid().ToString();
		var handlerType = "Handler.Type.A";

		// Act
		var result1 = await Store.TryMarkAsProcessedAsync(messageId1, handlerType, CancellationToken.None)
			.ConfigureAwait(false);
		var result2 = await Store.TryMarkAsProcessedAsync(messageId2, handlerType, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result1.ShouldBeTrue("First message should succeed");
		result2.ShouldBeTrue("Second message with different ID should also succeed");
	}

	[Fact]
	public async Task TryMarkAsProcessed_ConcurrentCalls_OnlyOneSucceeds()
	{
		// Arrange
		var messageId = Guid.NewGuid().ToString();
		var handlerType = "Handler.Type.A";
		const int concurrentAttempts = 10;
		var tasks = new List<Task<bool>>();

		// Act - Launch concurrent attempts
		for (int i = 0; i < concurrentAttempts; i++)
		{
			tasks.Add(Store.TryMarkAsProcessedAsync(messageId, handlerType, CancellationToken.None).AsTask());
		}

		var results = await Task.WhenAll(tasks).ConfigureAwait(false);

		// Assert - Exactly one should succeed
		var successCount = results.Count(r => r);
		successCount.ShouldBe(1, "Exactly one concurrent attempt should succeed");
	}

	[Fact]
	public async Task TryMarkAsProcessed_WithNullMessageId_ThrowsArgumentException()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(async () =>
			await Store.TryMarkAsProcessedAsync(null!, "Handler.Type", CancellationToken.None)
				.ConfigureAwait(false));
	}

	[Fact]
	public async Task TryMarkAsProcessed_WithEmptyMessageId_ThrowsArgumentException()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(async () =>
			await Store.TryMarkAsProcessedAsync(string.Empty, "Handler.Type", CancellationToken.None)
				.ConfigureAwait(false));
	}

	[Fact]
	public async Task TryMarkAsProcessed_WithNullHandlerType_ThrowsArgumentException()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(async () =>
			await Store.TryMarkAsProcessedAsync("msg-1", null!, CancellationToken.None)
				.ConfigureAwait(false));
	}

	[Fact]
	public async Task TryMarkAsProcessed_WithEmptyHandlerType_ThrowsArgumentException()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(async () =>
			await Store.TryMarkAsProcessedAsync("msg-1", string.Empty, CancellationToken.None)
				.ConfigureAwait(false));
	}

	#endregion TryMarkAsProcessed Tests

	#region IsProcessed Tests

	[Fact]
	public async Task IsProcessed_AfterMarking_ReturnsTrue()
	{
		// Arrange
		var messageId = Guid.NewGuid().ToString();
		var handlerType = "Handler.Type.A";
		_ = await Store.TryMarkAsProcessedAsync(messageId, handlerType, CancellationToken.None)
			.ConfigureAwait(false);

		// Act
		var result = await Store.IsProcessedAsync(messageId, handlerType, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.ShouldBeTrue("Message should be marked as processed");
	}

	[Fact]
	public async Task IsProcessed_NeverMarked_ReturnsFalse()
	{
		// Arrange
		var messageId = Guid.NewGuid().ToString();
		var handlerType = "Handler.Type.A";

		// Act
		var result = await Store.IsProcessedAsync(messageId, handlerType, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.ShouldBeFalse("Message that was never marked should return false");
	}

	[Fact]
	public async Task IsProcessed_DifferentHandler_ReturnsFalse()
	{
		// Arrange
		var messageId = Guid.NewGuid().ToString();
		_ = await Store.TryMarkAsProcessedAsync(messageId, "Handler.Type.A", CancellationToken.None)
			.ConfigureAwait(false);

		// Act
		var result = await Store.IsProcessedAsync(messageId, "Handler.Type.B", CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		result.ShouldBeFalse("Different handler type should not be considered processed");
	}

	#endregion IsProcessed Tests

	#region CreateEntry Tests

	[Fact]
	public async Task CreateEntry_WithValidData_ReturnsEntry()
	{
		// Arrange
		var messageId = Guid.NewGuid().ToString();
		var handlerType = "Handler.Type.A";
		var messageType = "MyMessage.Type";
		var payload = "test-payload"u8.ToArray();
		var metadata = new Dictionary<string, object> { ["key"] = "value" };

		// Act. The bracketing timestamps are captured around the call rather than measured backwards from
		// the assertion: a fixed backward window silently encodes an assumption about how long the store
		// takes, and breaks when it is slower than the guess rather than when the timestamp is wrong. This
		// assertion previously allowed 5 seconds and failed on a container run that took 9 -- reporting a
		// correct ReceivedAt as a defect.
		var before = DateTimeOffset.UtcNow;
		var entry = await Store.CreateEntryAsync(
			messageId, handlerType, messageType, payload, metadata, CancellationToken.None)
			.ConfigureAwait(false);
		var after = DateTimeOffset.UtcNow;

		// Assert
		_ = entry.ShouldNotBeNull();
		entry.MessageId.ShouldBe(messageId);
		entry.HandlerType.ShouldBe(handlerType);
		entry.MessageType.ShouldBe(messageType);
		entry.Payload.ShouldBe(payload);
		entry.Status.ShouldBe(InboxStatus.Received);

		// One second of slack on each side absorbs clock granularity and any skew between this process and
		// a store that stamps the time itself; it does NOT absorb operation duration, which is what the
		// bracket is for. The assertion therefore holds however slow the backend is.
		entry.ReceivedAt.ShouldBeInRange(
			before.AddSeconds(-1),
			after.AddSeconds(1));
	}

	[Fact]
	public async Task CreateEntry_DuplicateKey_ThrowsInvalidOperationException()
	{
		// Arrange
		var messageId = Guid.NewGuid().ToString();
		var handlerType = "Handler.Type.A";
		var payload = "payload"u8.ToArray();
		var metadata = new Dictionary<string, object>();

		_ = await Store.CreateEntryAsync(messageId, handlerType, "Type1", payload, metadata, CancellationToken.None)
			.ConfigureAwait(false);

		// Act & Assert
		_ = await Should.ThrowAsync<InvalidOperationException>(async () =>
			await Store.CreateEntryAsync(messageId, handlerType, "Type2", payload, metadata, CancellationToken.None)
				.ConfigureAwait(false));
	}

	#endregion CreateEntry Tests

	#region MarkFailed set-count conformance (bd-v9jq1a)

	/// <summary>
	/// bd-v9jq1a (CEO condition 1 / AC-7): the no-increment
	/// <c>IInboxStoreAdmin.MarkFailedAsync(tenant, messageId, handlerType, errorMessage, retryCount, ct)</c> overload
	/// MUST <b>set</b> RetryCount to the supplied value <b>exactly</b> (never <c>+1</c>) and leave the entry
	/// re-admittable for retry. Runs uniformly across every <c>IInboxStoreAdmin</c> store via the kit (InMemory
	/// at IMPLEMENT; the 8 DB stores under TestContainers at TEST). Non-vacuity: a copy-pasted auto-incrementing
	/// body (<c>retryCount + 1</c> or <c>+= retryCount</c>) makes this RED.
	/// </summary>
	[Fact]
	public async Task MarkFailedWithRetryCount_SetsRetryCountExactly_NotIncremented()
	{
		// Arrange — the entry must already exist (the set-count contract is UPDATE-not-upsert).
		var messageId = Guid.NewGuid().ToString();
		var handlerType = "Handler.Type.SetCount";
		var payload = "payload"u8.ToArray();
		var metadata = new Dictionary<string, object>();
		_ = await Store.CreateEntryAsync(messageId, handlerType, "Type", payload, metadata, CancellationToken.None)
			.ConfigureAwait(false);

		// Act — set the retry count to an explicit value via the no-increment overload.
		var outcome = await AdminStore.MarkFailedAsync(
				StorePartition, messageId, handlerType, "transient cb-open", 7, CancellationToken.None)
			.ConfigureAwait(false);

		outcome.ShouldBe(
			InboxMarkFailedOutcome.Applied,
			"the entry exists in the partition that was passed, so the mark must be applied AND say so -- the "
			+ "retry-count assertions below are meaningless against a store that declined in silence");

		// Assert — RetryCount is SET to exactly 7 (an auto-increment body would yield 1 or 8).
		var entry = await Store.GetEntryAsync(messageId, handlerType, CancellationToken.None).ConfigureAwait(false);
		_ = entry.ShouldNotBeNull();
		entry.RetryCount.ShouldBe(7);

		// Idempotent set (not cumulative): calling again with the same value stays exactly 7.
		var secondOutcome = await AdminStore.MarkFailedAsync(
				StorePartition, messageId, handlerType, "transient cb-open", 7, CancellationToken.None)
			.ConfigureAwait(false);

		secondOutcome.ShouldBe(InboxMarkFailedOutcome.Applied);
		var entry2 = await Store.GetEntryAsync(messageId, handlerType, CancellationToken.None).ConfigureAwait(false);
		_ = entry2.ShouldNotBeNull();
		entry2.RetryCount.ShouldBe(7);
	}

	#endregion MarkFailed set-count conformance (bd-v9jq1a)

	#region MarkProcessed Tests

	[Fact]
	public async Task MarkProcessed_AfterCreate_UpdatesStatus()
	{
		// Arrange
		var messageId = Guid.NewGuid().ToString();
		var handlerType = "Handler.Type.A";
		var payload = "payload"u8.ToArray();
		var metadata = new Dictionary<string, object>();

		_ = await Store.CreateEntryAsync(messageId, handlerType, "Type", payload, metadata, CancellationToken.None)
			.ConfigureAwait(false);

		// Act
		await Store.MarkProcessedAsync(messageId, handlerType, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		var isProcessed = await Store.IsProcessedAsync(messageId, handlerType, CancellationToken.None)
			.ConfigureAwait(false);
		isProcessed.ShouldBeTrue();
	}

	[Fact]
	public async Task MarkProcessed_NonExistentEntry_ThrowsInvalidOperationException()
	{
		// Arrange
		var messageId = Guid.NewGuid().ToString();
		var handlerType = "Handler.Type.A";

		// Act & Assert
		_ = await Should.ThrowAsync<InvalidOperationException>(async () =>
			await Store.MarkProcessedAsync(messageId, handlerType, CancellationToken.None)
				.ConfigureAwait(false));
	}

	#endregion MarkProcessed Tests

	#region MarkFailed Tests

	[Fact]
	public async Task MarkFailed_AfterCreate_UpdatesStatusAndError()
	{
		// Arrange
		var messageId = Guid.NewGuid().ToString();
		var handlerType = "Handler.Type.A";
		var payload = "payload"u8.ToArray();
		var metadata = new Dictionary<string, object>();
		var errorMessage = "Processing failed: test error";

		_ = await Store.CreateEntryAsync(messageId, handlerType, "Type", payload, metadata, CancellationToken.None)
			.ConfigureAwait(false);

		// Act
		await Store.MarkFailedAsync(messageId, handlerType, errorMessage, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		var entry = await Store.GetEntryAsync(messageId, handlerType, CancellationToken.None)
			.ConfigureAwait(false);
		_ = entry.ShouldNotBeNull();
		entry.Status.ShouldBe(InboxStatus.Failed);
		entry.LastError.ShouldBe(errorMessage);
		entry.RetryCount.ShouldBe(1);
	}

	[Fact]
	public async Task MarkFailed_MultipleTimes_IncrementsRetryCount()
	{
		// Arrange
		var messageId = Guid.NewGuid().ToString();
		var handlerType = "Handler.Type.A";
		var payload = "payload"u8.ToArray();
		var metadata = new Dictionary<string, object>();

		_ = await Store.CreateEntryAsync(messageId, handlerType, "Type", payload, metadata, CancellationToken.None)
			.ConfigureAwait(false);

		// Act
		await Store.MarkFailedAsync(messageId, handlerType, "Error 1", CancellationToken.None)
			.ConfigureAwait(false);
		await Store.MarkFailedAsync(messageId, handlerType, "Error 2", CancellationToken.None)
			.ConfigureAwait(false);
		await Store.MarkFailedAsync(messageId, handlerType, "Error 3", CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		var entry = await Store.GetEntryAsync(messageId, handlerType, CancellationToken.None)
			.ConfigureAwait(false);
		_ = entry.ShouldNotBeNull();
		entry.RetryCount.ShouldBe(3);
		entry.LastError.ShouldBe("Error 3");
	}

	#endregion MarkFailed Tests

	#region GetEntry Tests

	[Fact]
	public async Task GetEntry_ExistingEntry_ReturnsEntry()
	{
		// Arrange
		var messageId = Guid.NewGuid().ToString();
		var handlerType = "Handler.Type.A";
		var payload = "test-data"u8.ToArray();
		var metadata = new Dictionary<string, object> { ["CorrelationId"] = "corr-123" };

		_ = await Store.CreateEntryAsync(messageId, handlerType, "MyType", payload, metadata, CancellationToken.None)
			.ConfigureAwait(false);

		// Act
		var entry = await Store.GetEntryAsync(messageId, handlerType, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		_ = entry.ShouldNotBeNull();
		entry.MessageId.ShouldBe(messageId);
		entry.HandlerType.ShouldBe(handlerType);
	}

	[Fact]
	public async Task GetEntry_NonExistentEntry_ReturnsNull()
	{
		// Arrange
		var messageId = Guid.NewGuid().ToString();
		var handlerType = "Handler.Type.A";

		// Act
		var entry = await Store.GetEntryAsync(messageId, handlerType, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		entry.ShouldBeNull();
	}

	#endregion GetEntry Tests

	#region GetFailedEntries Tests

	[Fact]
	public async Task GetFailedEntries_ReturnsOnlyFailedEntries()
	{
		// Arrange
		var payload = "payload"u8.ToArray();
		var metadata = new Dictionary<string, object>();

		// Create processed entry
		var processedId = Guid.NewGuid().ToString();
		_ = await Store.CreateEntryAsync(processedId, "Handler.A", "Type", payload, metadata, CancellationToken.None)
			.ConfigureAwait(false);
		await Store.MarkProcessedAsync(processedId, "Handler.A", CancellationToken.None)
			.ConfigureAwait(false);

		// Create failed entry
		var failedId = Guid.NewGuid().ToString();
		_ = await Store.CreateEntryAsync(failedId, "Handler.A", "Type", payload, metadata, CancellationToken.None)
			.ConfigureAwait(false);
		await Store.MarkFailedAsync(failedId, "Handler.A", "Test error", CancellationToken.None)
			.ConfigureAwait(false);

		// Act
		var failedEntries = await AdminStore.GetAllTenantsFailedEntriesAsync(
			maxRetries: 5, olderThan: null, batchSize: 100, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		var entriesList = failedEntries.ToList();
		entriesList.Count.ShouldBe(1);
		entriesList[0].MessageId.ShouldBe(failedId);
		entriesList[0].Status.ShouldBe(InboxStatus.Failed);
	}

	[Fact]
	public async Task GetFailedEntries_RespectsMaxRetries()
	{
		// Arrange
		var payload = "payload"u8.ToArray();
		var metadata = new Dictionary<string, object>();

		// Create entry that exceeds max retries
		var failedId = Guid.NewGuid().ToString();
		_ = await Store.CreateEntryAsync(failedId, "Handler.A", "Type", payload, metadata, CancellationToken.None)
			.ConfigureAwait(false);
		// Fail 5 times
		for (int i = 0; i < 5; i++)
		{
			await Store.MarkFailedAsync(failedId, "Handler.A", $"Error {i}", CancellationToken.None)
				.ConfigureAwait(false);
		}

		// Act - maxRetries = 3 means entries with retryCount >= 3 are excluded
		var failedEntries = await AdminStore.GetAllTenantsFailedEntriesAsync(
			maxRetries: 3, olderThan: null, batchSize: 100, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		failedEntries.ShouldBeEmpty("Entry with 5 retries should be excluded when maxRetries is 3");
	}

	[Fact]
	public async Task GetFailedEntries_RespectsBatchSize()
	{
		// Arrange
		var payload = "payload"u8.ToArray();
		var metadata = new Dictionary<string, object>();

		// Create 5 failed entries
		for (int i = 0; i < 5; i++)
		{
			var messageId = Guid.NewGuid().ToString();
			_ = await Store.CreateEntryAsync(messageId, "Handler.A", "Type", payload, metadata, CancellationToken.None)
				.ConfigureAwait(false);
			await Store.MarkFailedAsync(messageId, "Handler.A", "Error", CancellationToken.None)
				.ConfigureAwait(false);
		}

		// Act
		var failedEntries = await AdminStore.GetAllTenantsFailedEntriesAsync(
			maxRetries: 10, olderThan: null, batchSize: 2, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		failedEntries.Count().ShouldBe(2, "Should respect batch size limit");
	}

	#endregion GetFailedEntries Tests

	#region GetStatistics Tests

	[Fact]
	public async Task GetStatistics_ReturnsValidData()
	{
		// Arrange
		var payload = "payload"u8.ToArray();
		var metadata = new Dictionary<string, object>();

		// Create processed entry
		var processedId = Guid.NewGuid().ToString();
		_ = await Store.CreateEntryAsync(processedId, "Handler.A", "Type", payload, metadata, CancellationToken.None)
			.ConfigureAwait(false);
		await Store.MarkProcessedAsync(processedId, "Handler.A", CancellationToken.None)
			.ConfigureAwait(false);

		// Create failed entry
		var failedId = Guid.NewGuid().ToString();
		_ = await Store.CreateEntryAsync(failedId, "Handler.A", "Type", payload, metadata, CancellationToken.None)
			.ConfigureAwait(false);
		await Store.MarkFailedAsync(failedId, "Handler.A", "Error", CancellationToken.None)
			.ConfigureAwait(false);

		// Create pending entry
		var pendingId = Guid.NewGuid().ToString();
		_ = await Store.CreateEntryAsync(pendingId, "Handler.A", "Type", payload, metadata, CancellationToken.None)
			.ConfigureAwait(false);

		// Act
		var stats = await AdminStore.GetAllTenantsStatisticsAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert
		stats.TotalEntries.ShouldBe(3);
		stats.ProcessedEntries.ShouldBe(1);
		stats.FailedEntries.ShouldBe(1);
		stats.PendingEntries.ShouldBe(1);
	}

	[Fact]
	public async Task GetStatistics_EmptyStore_ReturnsZeroCounts()
	{
		// Act
		var stats = await AdminStore.GetAllTenantsStatisticsAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert
		stats.TotalEntries.ShouldBe(0);
		stats.ProcessedEntries.ShouldBe(0);
		stats.FailedEntries.ShouldBe(0);
		stats.PendingEntries.ShouldBe(0);
	}

	#endregion GetStatistics Tests

	#region Cleanup Tests

	[Fact]
	public async Task Cleanup_RemovesExpiredProcessedEntries()
	{
		// Arrange
		var payload = "payload"u8.ToArray();
		var metadata = new Dictionary<string, object>();

		var messageId = Guid.NewGuid().ToString();
		_ = await Store.CreateEntryAsync(messageId, "Handler.A", "Type", payload, metadata, CancellationToken.None)
			.ConfigureAwait(false);
		await Store.MarkProcessedAsync(messageId, "Handler.A", CancellationToken.None)
			.ConfigureAwait(false);

		// Act - Cleanup with 0 retention period should remove processed entries.
		// Retry briefly to avoid timestamp boundary races when ProcessedAt ~= cutoff.
		var removed = 0;
		InboxEntry? entry = null;
		var deadline = DateTimeOffset.UtcNow.AddSeconds(2);
		do
		{
			removed += await AdminStore.CleanupAllTenantsProcessedEntriesAsync(DateTimeOffset.UtcNow, CancellationToken.None).ConfigureAwait(false);
			entry = await Store.GetEntryAsync(messageId, "Handler.A", CancellationToken.None).ConfigureAwait(false);
			if (entry is null || removed > 0)
			{
				break;
			}

			await global::Tests.Shared.Infrastructure.TestTiming.PauseAsync(10).ConfigureAwait(false);
		}
		while (DateTimeOffset.UtcNow < deadline);

		// Assert
		removed.ShouldBeGreaterThanOrEqualTo(1);
		entry.ShouldBeNull("Entry should have been cleaned up");
	}

	[Fact]
	public async Task Cleanup_PreservesUnexpiredEntries()
	{
		// Arrange
		var payload = "payload"u8.ToArray();
		var metadata = new Dictionary<string, object>();

		var messageId = Guid.NewGuid().ToString();
		_ = await Store.CreateEntryAsync(messageId, "Handler.A", "Type", payload, metadata, CancellationToken.None)
			.ConfigureAwait(false);
		await Store.MarkProcessedAsync(messageId, "Handler.A", CancellationToken.None)
			.ConfigureAwait(false);

		// Act - Cleanup with 1 hour retention should preserve recent entries
		var removed = await AdminStore.CleanupAllTenantsProcessedEntriesAsync(DateTimeOffset.UtcNow.AddHours(-1), CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		removed.ShouldBe(0);

		var entry = await Store.GetEntryAsync(messageId, "Handler.A", CancellationToken.None)
			.ConfigureAwait(false);
		_ = entry.ShouldNotBeNull("Entry should still exist");
	}

	[Fact]
	public async Task Cleanup_PreservesFailedEntries()
	{
		// Arrange
		var payload = "payload"u8.ToArray();
		var metadata = new Dictionary<string, object>();

		var messageId = Guid.NewGuid().ToString();
		_ = await Store.CreateEntryAsync(messageId, "Handler.A", "Type", payload, metadata, CancellationToken.None)
			.ConfigureAwait(false);
		await Store.MarkFailedAsync(messageId, "Handler.A", "Error", CancellationToken.None)
			.ConfigureAwait(false);

		// Act - Cleanup should not remove failed entries
		var removed = await AdminStore.CleanupAllTenantsProcessedEntriesAsync(DateTimeOffset.UtcNow, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		var entry = await Store.GetEntryAsync(messageId, "Handler.A", CancellationToken.None)
			.ConfigureAwait(false);
		_ = entry.ShouldNotBeNull("Failed entries should be preserved");
		entry.Status.ShouldBe(InboxStatus.Failed);
	}

	[Fact]
	public async Task Cleanup_PreservesPendingEntries()
	{
		// Arrange
		var payload = "payload"u8.ToArray();
		var metadata = new Dictionary<string, object>();

		var messageId = Guid.NewGuid().ToString();
		_ = await Store.CreateEntryAsync(messageId, "Handler.A", "Type", payload, metadata, CancellationToken.None)
			.ConfigureAwait(false);

		// Act - Cleanup should not remove pending entries
		var removed = await AdminStore.CleanupAllTenantsProcessedEntriesAsync(DateTimeOffset.UtcNow, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		var entry = await Store.GetEntryAsync(messageId, "Handler.A", CancellationToken.None)
			.ConfigureAwait(false);
		_ = entry.ShouldNotBeNull("Pending entries should be preserved");
		entry.Status.ShouldBe(InboxStatus.Received);
	}

	#endregion Cleanup Tests

	#region GetAllEntries Tests

	[Fact]
	public async Task GetAllEntries_ReturnsAllEntries()
	{
		// Arrange
		var payload = "payload"u8.ToArray();
		var metadata = new Dictionary<string, object>();

		var id1 = Guid.NewGuid().ToString();
		var id2 = Guid.NewGuid().ToString();
		var id3 = Guid.NewGuid().ToString();

		_ = await Store.CreateEntryAsync(id1, "Handler.A", "Type", payload, metadata, CancellationToken.None)
			.ConfigureAwait(false);
		_ = await Store.CreateEntryAsync(id2, "Handler.A", "Type", payload, metadata, CancellationToken.None)
			.ConfigureAwait(false);
		_ = await Store.CreateEntryAsync(id3, "Handler.A", "Type", payload, metadata, CancellationToken.None)
			.ConfigureAwait(false);

		// Mark different statuses
		await Store.MarkProcessedAsync(id1, "Handler.A", CancellationToken.None)
			.ConfigureAwait(false);
		await Store.MarkFailedAsync(id2, "Handler.A", "Error", CancellationToken.None)
			.ConfigureAwait(false);
		// id3 remains pending

		// Act
		var allEntries = await AdminStore.GetAllTenantsEntriesAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert
		var entriesList = allEntries.ToList();
		entriesList.Count.ShouldBe(3);
		entriesList.Select(e => e.MessageId).ShouldContain(id1);
		entriesList.Select(e => e.MessageId).ShouldContain(id2);
		entriesList.Select(e => e.MessageId).ShouldContain(id3);
	}

	[Fact]
	public async Task GetAllEntries_EmptyStore_ReturnsEmptyCollection()
	{
		// Act
		var allEntries = await AdminStore.GetAllTenantsEntriesAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert
		allEntries.ShouldBeEmpty();
	}

	#endregion GetAllEntries Tests
	/// <summary>
	/// Concurrent first callers must not fault: lazy initialisation has to be serialised.
	/// </summary>
	/// <remarks>
	/// Deliberately builds a SECOND, fresh store rather than using the fixture's, because the window
	/// this exercises exists only before initialisation completes and the fixture's store is already
	/// past it. Reads a key that does not exist, so nothing is mutated and any fault is the finding.
	/// </para>
	/// <para>
	/// SCOPE, stated because a test that reads as broader than it is would be worse than none. This
	/// was measured against a store with a genuinely unsynchronised initialisation and did NOT detect
	/// it: 0 failures in 5 runs. The reason is structural -- that store assigns its client, database
	/// and collection in three consecutive SYNCHRONOUS statements with no await between them, so a
	/// second caller can only observe the half-built state through true parallelism in a window of a
	/// few instructions. CI hit it under load; a barrier on a quiet machine does not.
	/// </para>
	/// <para>
	/// It is also VACUOUS for a store the deriver hands back already initialised. 22 of the 77
	/// conformance derivers call InitializeAsync inside their factory (or share one document store
	/// across the class), so for those the store has already passed through the window before this
	/// fact runs and no number of concurrent callers can re-enter it. That is nearly a third, and it
	/// is not a defect in those derivers -- eager initialisation is what their production wiring does
	/// -- but it does mean this fact must not be read as covering them.
	/// </para>
	/// <para>
	/// So this is a guard against GROSS concurrency faults -- an operation that throws, deadlocks, or
	/// corrupts shared state when entered many times at once -- and it is NOT the detector for the
	/// narrow lazy-init race. The name said "Race First Use", which claimed exactly the thing the
	/// paragraphs above disclaim; it now says what it asserts. The race itself is bound by two tests
	/// that do not depend on observing it: LazyInitialisationRunsExactlyOnceShould forces the
	/// interleaving deterministically and asserts the body runs once (25/25 runs, no container), and
	/// LazyInitialisationIsGuardedTests asserts structurally that every store has the guard at all.
	/// </remarks>
	[Fact]
	public virtual async Task Should_Not_Fault_When_Many_Callers_Use_The_Store_Concurrently()
	{
		var store = await CreateStoreAsync().ConfigureAwait(false);
		var absent = Guid.NewGuid().ToString();

		await ConcurrentFirstUse.ShouldNotFaultAsync(
			async () => _ = await store.IsProcessedAsync(absent, "ConcurrentFirstUse", CancellationToken.None).ConfigureAwait(false),
			"the inbox store").ConfigureAwait(false);
	}

	#region Durability Fault Injection (2mek4x)

	/// <summary>
	/// Makes the NEXT durable write against the backing store fail with a real, provider-side rejection
	/// (for example renaming the backing table/collection out from under the store) — never a mocked
	/// client, which would return whatever it was told rather than reproduce a server's rejection.
	/// </summary>
	/// <remarks>
	/// The base default throws rather than skip: a provider that has not wired real fault injection fails
	/// <see cref="ThrowNotNoOpOnPersistenceFailure"/> loudly (RED-by-construction) instead of silently
	/// passing an arm it never actually ran. Override this together with
	/// <see cref="RemovePersistenceFaultAsync"/> to wire the arm for a real provider; override
	/// <see cref="ThrowNotNoOpOnPersistenceFailure"/> itself only for a store with no external persistence
	/// layer to fault (the in-memory store).
	/// </remarks>
	protected virtual Task InjectPersistenceFaultAsync() =>
		throw new NotSupportedException(
			$"{GetType().Name} has not wired durability fault injection (2mek4x) — override " +
			$"{nameof(InjectPersistenceFaultAsync)} and {nameof(RemovePersistenceFaultAsync)} with a real " +
			"provider-side fault (drop/rename the backing store, revoke a permission, sever the connection) " +
			$"rather than letting {nameof(ThrowNotNoOpOnPersistenceFailure)} skip.");

	/// <summary>
	/// Reverses <see cref="InjectPersistenceFaultAsync"/> so the fixture's shared backing store is usable
	/// by the next test again. Called from a <c>finally</c>, so it MUST be safe to call even when the
	/// fault was never actually put in place (for example because the write it was meant to break never
	/// happened).
	/// </summary>
	protected virtual Task RemovePersistenceFaultAsync() =>
		throw new NotSupportedException(
			$"{GetType().Name} has not wired durability fault injection (2mek4x) — see " +
			$"{nameof(InjectPersistenceFaultAsync)}.");

	/// <summary>
	/// SAFETY + LIVENESS: <see cref="IInboxStore"/>'s durability fault model
	/// (<see cref="IInboxStore"/> XML docs, "Durability fault model") requires that a persistence failure
	/// on <see cref="IInboxStore.CreateEntryAsync"/> surface as a thrown exception and leave nothing
	/// persisted — never a silent no-op that reports success while the write never landed. A no-op here
	/// converts at-least-once delivery into silent message loss, because the caller acks on the strength
	/// of the "successful" write.
	/// </summary>
	[Fact]
	public virtual async Task ThrowNotNoOpOnPersistenceFailure()
	{
		var messageId = $"fault-{Guid.NewGuid():N}";
		const string handlerType = "DurabilityFaultInjection";

		await InjectPersistenceFaultAsync().ConfigureAwait(false);
		try
		{
			// SAFETY: the write must throw, not return successfully having recorded nothing.
			_ = await Should.ThrowAsync<Exception>(
				() => Store.CreateEntryAsync(
						messageId, handlerType, "FaultMessageType", [1],
						new Dictionary<string, object>(StringComparer.Ordinal), CancellationToken.None)
					.AsTask())
				.ConfigureAwait(false);
		}
		finally
		{
			// Removed BEFORE the read-back below, deliberately: a fresh store instance's FIRST use against
			// several real providers re-verifies its own schema/topology (for example a multi-tenant
			// primary-key check), and that verification has no way to distinguish "the backing store is
			// mid-fault" from "the backing store was never provisioned" -- both look like a missing/
			// malformed schema. Removing the fault first keeps the read-back a clean test of "was anything
			// persisted", which is the property this arm actually needs to prove; restoring the backing
			// store does not fabricate a row that was never written.
			//
			// THE CONSTRAINT THAT ORDERING PUTS ON THE FAULT, stated here because it is only discoverable
			// by breaking it: A FAULT MUST NOT DESTROY THE EVIDENCE THE ASSERTION INSPECTS. That is the
			// property; the mechanism is free. Permission (an ACL revoke, a write block), routing (a severed
			// client route), lifecycle (a disposed client) and schema (a rename, a validator) all satisfy it
			// -- pick whichever the provider actually offers, since some offer only one. A destructive fault (drop the
			// table, delete the keyspace) erases the very rows the read-back below inspects, so the safety
			// assertion cannot fail whatever the faulted write did, and neither ordering rescues it:
			// repair-then-read queries an empty store, read-then-repair queries one that is not there.
			await RemovePersistenceFaultAsync().ConfigureAwait(false);
		}

		// Read back from a FRESH store instance -- never trust the faulted instance's own view -- and
		// confirm the throw did not leave a partial write behind.
		var duringFault = await CreateVerificationStoreAsync().ConfigureAwait(false);
		try
		{
			(await duringFault.GetEntryAsync(messageId, handlerType, CancellationToken.None).ConfigureAwait(false))
				.ShouldBeNull("a thrown CreateEntryAsync must not have persisted anything");
		}
		finally
		{
			await DisposeStoreAsync(duringFault).ConfigureAwait(false);
		}

		// LIVENESS: with the fault removed, the identical call succeeds and is durably readable from a
		// second fresh instance -- so a store that simply throws on everything cannot pass this arm.
		_ = await Store.CreateEntryAsync(
				messageId, handlerType, "FaultMessageType", [1],
				new Dictionary<string, object>(StringComparer.Ordinal), CancellationToken.None)
			.ConfigureAwait(false);

		var afterRepair = await CreateVerificationStoreAsync().ConfigureAwait(false);
		try
		{
			(await afterRepair.GetEntryAsync(messageId, handlerType, CancellationToken.None).ConfigureAwait(false))
				.ShouldNotBeNull("the identical write must succeed and be durably readable once the fault is gone");
		}
		finally
		{
			await DisposeStoreAsync(afterRepair).ConfigureAwait(false);
		}
	}

	private static async Task DisposeStoreAsync(IInboxStore store)
	{
		switch (store)
		{
			case IAsyncDisposable asyncDisposable:
				await asyncDisposable.DisposeAsync().ConfigureAwait(false);
				break;
			case IDisposable disposable:
				disposable.Dispose();
				break;
		}
	}

	#endregion Durability Fault Injection (2mek4x)
}
