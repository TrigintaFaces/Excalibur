// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

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
	protected IInboxStore Store { get; private set; } = null!;

	/// <inheritdoc/>
	public async Task InitializeAsync()
	{
		Store = await CreateStoreAsync().ConfigureAwait(false);
	}

	/// <inheritdoc/>
	public async Task DisposeAsync()
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

		// Act
		var entry = await Store.CreateEntryAsync(
			messageId, handlerType, messageType, payload, metadata, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		_ = entry.ShouldNotBeNull();
		entry.MessageId.ShouldBe(messageId);
		entry.HandlerType.ShouldBe(handlerType);
		entry.MessageType.ShouldBe(messageType);
		entry.Payload.ShouldBe(payload);
		entry.Status.ShouldBe(InboxStatus.Received);
		entry.ReceivedAt.ShouldBeInRange(
			DateTimeOffset.UtcNow.AddSeconds(-5),
			DateTimeOffset.UtcNow.AddSeconds(1));
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
		var failedEntries = await Store.GetFailedEntriesAsync(
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
		var failedEntries = await Store.GetFailedEntriesAsync(
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
		var failedEntries = await Store.GetFailedEntriesAsync(
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
		var stats = await Store.GetStatisticsAsync(CancellationToken.None).ConfigureAwait(false);

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
		var stats = await Store.GetStatisticsAsync(CancellationToken.None).ConfigureAwait(false);

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
			removed += await Store.CleanupAsync(TimeSpan.Zero, CancellationToken.None).ConfigureAwait(false);
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
		var removed = await Store.CleanupAsync(TimeSpan.FromHours(1), CancellationToken.None)
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
		var removed = await Store.CleanupAsync(TimeSpan.Zero, CancellationToken.None)
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
		var removed = await Store.CleanupAsync(TimeSpan.Zero, CancellationToken.None)
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
		var allEntries = await Store.GetAllEntriesAsync(CancellationToken.None).ConfigureAwait(false);

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
		var allEntries = await Store.GetAllEntriesAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert
		allEntries.ShouldBeEmpty();
	}

	#endregion GetAllEntries Tests
}
