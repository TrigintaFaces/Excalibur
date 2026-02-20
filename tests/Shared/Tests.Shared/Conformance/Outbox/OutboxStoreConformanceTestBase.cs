// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace Tests.Shared.Conformance.Outbox;

/// <summary>
/// Base class for IOutboxStore conformance tests.
/// Implementations must provide a concrete IOutboxStore instance for testing.
/// </summary>
/// <remarks>
/// <para>
/// This conformance test kit verifies that outbox store implementations
/// correctly implement the IOutboxStore interface contract, including:
/// </para>
/// <list type="bullet">
///   <item>Entry staging and enqueuing</item>
///   <item>Status transitions (Staged -> Sending -> Sent/Failed)</item>
///   <item>Message retrieval by status</item>
///   <item>Cleanup operations</item>
///   <item>Statistics tracking</item>
///   <item>Concurrent access and atomicity</item>
/// </list>
/// <para>
/// To create conformance tests for your own IOutboxStore implementation:
/// <list type="number">
///   <item>Inherit from OutboxStoreConformanceTestBase</item>
///   <item>Override CreateStoreAsync() to create an instance of your IOutboxStore implementation</item>
///   <item>Override CleanupAsync() to properly clean up the store between tests</item>
/// </list>
/// </para>
/// </remarks>
public abstract class OutboxStoreConformanceTestBase : IAsyncLifetime
{
	/// <summary>
	/// The outbox store instance under test.
	/// </summary>
	protected IOutboxStore Store { get; private set; } = null!;

	/// <summary>
	/// The admin interface for the outbox store under test.
	/// </summary>
	protected IOutboxStoreAdmin? Admin { get; private set; }

	/// <inheritdoc/>
	public async Task InitializeAsync()
	{
		Store = await CreateStoreAsync().ConfigureAwait(false);
		Admin = Store as IOutboxStoreAdmin;
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
	/// Creates a new instance of the IOutboxStore implementation under test.
	/// </summary>
	/// <returns>A configured IOutboxStore instance.</returns>
	protected abstract Task<IOutboxStore> CreateStoreAsync();

	/// <summary>
	/// Cleans up the IOutboxStore instance after each test.
	/// </summary>
	protected abstract Task CleanupAsync();

	#region Helper Methods

	/// <summary>
	/// Creates a test outbound message with the given parameters.
	/// </summary>
	protected static OutboundMessage CreateTestMessage(
		string? id = null,
		string? messageType = null,
		string? destination = null,
		DateTimeOffset? scheduledAt = null)
	{
		return new OutboundMessage(
			messageType ?? "Test.MessageType",
			"test-payload"u8.ToArray(),
			destination ?? "test-queue")
		{
			Id = id ?? Guid.NewGuid().ToString(),
			ScheduledAt = scheduledAt
		};
	}

	#endregion Helper Methods

	#region Interface Implementation Tests

	[Fact]
	public void Store_ShouldImplementIOutboxStore()
	{
		// Assert
		_ = Store.ShouldBeAssignableTo<IOutboxStore>();
	}

	#endregion Interface Implementation Tests

	#region StageMessage Tests

	[Fact]
	public async Task StageMessage_ValidMessage_StagesSuccessfully()
	{
		// Arrange
		var message = CreateTestMessage();

		// Act
		await Store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);

		// Assert - Message should be retrievable as unsent
		var unsent = await Store.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false);
		unsent.ShouldContain(m => m.Id == message.Id);
	}

	[Fact]
	public async Task StageMessage_WithNullMessage_ThrowsArgumentNullException()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(async () =>
			await Store.StageMessageAsync(null!, CancellationToken.None).ConfigureAwait(false));
	}

	[Fact]
	public async Task StageMessage_DuplicateId_ThrowsInvalidOperationException()
	{
		// Arrange
		var message = CreateTestMessage();
		await Store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);

		// Act & Assert - Staging with same ID should fail
		var duplicate = CreateTestMessage(id: message.Id);
		_ = await Should.ThrowAsync<InvalidOperationException>(async () =>
			await Store.StageMessageAsync(duplicate, CancellationToken.None).ConfigureAwait(false));
	}

	[Fact]
	public async Task StageMessage_WithScheduledTime_SetsScheduledAt()
	{
		// Arrange
		var scheduledTime = DateTimeOffset.UtcNow.AddHours(1);
		var message = CreateTestMessage(scheduledAt: scheduledTime);

		// Act
		await Store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);

		// Assert - Should not appear in unsent (because it's scheduled)
		var unsent = await Store.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false);
		unsent.ShouldNotContain(m => m.Id == message.Id, "Scheduled messages should not appear in unsent");

		// Should appear in scheduled
		var scheduled = await Admin.GetScheduledMessagesAsync(
			scheduledTime.AddMinutes(1), 10, CancellationToken.None).ConfigureAwait(false);
		scheduled.ShouldContain(m => m.Id == message.Id);
	}

	[Fact]
	public async Task StageMessage_MultipleMessages_AllStaged()
	{
		// Arrange
		var messages = Enumerable.Range(0, 5)
			.Select(_ => CreateTestMessage())
			.ToList();

		// Act
		foreach (var message in messages)
		{
			await Store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);
		}

		// Assert
		var unsent = await Store.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false);
		unsent.Count().ShouldBe(5);
	}

	[Fact]
	public async Task StageMessage_ConcurrentStaging_AllSucceed()
	{
		// Arrange
		const int concurrentMessages = 10;
		var messages = Enumerable.Range(0, concurrentMessages)
			.Select(_ => CreateTestMessage())
			.ToList();

		// Act - Stage all concurrently
		var tasks = messages.Select(m =>
			Store.StageMessageAsync(m, CancellationToken.None).AsTask());
		await Task.WhenAll(tasks).ConfigureAwait(false);

		// Assert
		var unsent = await Store.GetUnsentMessagesAsync(20, CancellationToken.None).ConfigureAwait(false);
		unsent.Count().ShouldBe(concurrentMessages);
	}

	[Fact]
	public async Task StageMessage_PreservesMessageProperties()
	{
		// Arrange
		var message = new OutboundMessage(
			"Test.OrderPlaced",
			"order-data"u8.ToArray(),
			"orders-queue")
		{
			CorrelationId = "corr-123",
			CausationId = "cause-456",
			TenantId = "tenant-abc",
			Priority = 5
		};

		// Act
		await Store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);

		// Assert
		var unsent = await Store.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false);
		var retrieved = unsent.FirstOrDefault(m => m.Id == message.Id);

		_ = retrieved.ShouldNotBeNull();
		retrieved.MessageType.ShouldBe("Test.OrderPlaced");
		retrieved.Destination.ShouldBe("orders-queue");
		retrieved.CorrelationId.ShouldBe("corr-123");
		retrieved.CausationId.ShouldBe("cause-456");
		retrieved.TenantId.ShouldBe("tenant-abc");
		retrieved.Priority.ShouldBe(5);
	}

	#endregion StageMessage Tests

	#region GetUnsentMessages Tests

	[Fact]
	public async Task GetUnsentMessages_EmptyStore_ReturnsEmpty()
	{
		// Act
		var messages = await Store.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false);

		// Assert
		messages.ShouldBeEmpty();
	}

	[Fact]
	public async Task GetUnsentMessages_RespectsBatchSize()
	{
		// Arrange
		for (int i = 0; i < 10; i++)
		{
			await Store.StageMessageAsync(CreateTestMessage(), CancellationToken.None).ConfigureAwait(false);
		}

		// Act
		var messages = await Store.GetUnsentMessagesAsync(3, CancellationToken.None).ConfigureAwait(false);

		// Assert
		messages.Count().ShouldBe(3);
	}

	[Fact]
	public async Task GetUnsentMessages_WithInvalidBatchSize_ThrowsArgumentOutOfRangeException()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentOutOfRangeException>(async () =>
			await Store.GetUnsentMessagesAsync(0, CancellationToken.None).ConfigureAwait(false));
	}

	[Fact]
	public async Task GetUnsentMessages_ExcludesSentMessages()
	{
		// Arrange
		var message = CreateTestMessage();
		await Store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);
		await Store.MarkSentAsync(message.Id, CancellationToken.None).ConfigureAwait(false);

		// Act
		var unsent = await Store.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false);

		// Assert
		unsent.ShouldNotContain(m => m.Id == message.Id);
	}

	[Fact]
	public async Task GetUnsentMessages_OrdersByCreatedAt()
	{
		// Arrange
		var messages = new List<OutboundMessage>();
		for (int i = 0; i < 3; i++)
		{
			var message = CreateTestMessage();
			messages.Add(message);
			await Store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);
			await Task.Delay(10).ConfigureAwait(false); // Small delay to ensure different timestamps
		}

		// Act
		var unsent = await Store.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false);
		var unsentList = unsent.ToList();

		// Assert - Should be ordered by creation time (oldest first)
		for (int i = 0; i < unsentList.Count - 1; i++)
		{
			unsentList[i].CreatedAt.ShouldBeLessThanOrEqualTo(unsentList[i + 1].CreatedAt);
		}
	}

	#endregion GetUnsentMessages Tests

	#region MarkSent Tests

	[Fact]
	public async Task MarkSent_ValidMessage_UpdatesStatus()
	{
		// Arrange
		var message = CreateTestMessage();
		await Store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);

		// Act
		await Store.MarkSentAsync(message.Id, CancellationToken.None).ConfigureAwait(false);

		// Assert
		var unsent = await Store.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false);
		unsent.ShouldNotContain(m => m.Id == message.Id);
	}

	[Fact]
	public async Task MarkSent_WithNullMessageId_ThrowsArgumentException()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(async () =>
			await Store.MarkSentAsync(null!, CancellationToken.None).ConfigureAwait(false));
	}

	[Fact]
	public async Task MarkSent_WithEmptyMessageId_ThrowsArgumentException()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(async () =>
			await Store.MarkSentAsync(string.Empty, CancellationToken.None).ConfigureAwait(false));
	}

	[Fact]
	public async Task MarkSent_NonExistentMessage_ThrowsInvalidOperationException()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<InvalidOperationException>(async () =>
			await Store.MarkSentAsync("non-existent-id", CancellationToken.None).ConfigureAwait(false));
	}

	[Fact]
	public async Task MarkSent_AlreadySent_ThrowsInvalidOperationException()
	{
		// Arrange
		var message = CreateTestMessage();
		await Store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);
		await Store.MarkSentAsync(message.Id, CancellationToken.None).ConfigureAwait(false);

		// Act & Assert
		_ = await Should.ThrowAsync<InvalidOperationException>(async () =>
			await Store.MarkSentAsync(message.Id, CancellationToken.None).ConfigureAwait(false));
	}

	#endregion MarkSent Tests

	#region MarkFailed Tests

	[Fact]
	public async Task MarkFailed_ValidMessage_UpdatesStatusAndError()
	{
		// Arrange
		var message = CreateTestMessage();
		await Store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);

		// Act
		await Store.MarkFailedAsync(message.Id, "Connection timeout", 1, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		var failed = await Admin.GetFailedMessagesAsync(5, null, 10, CancellationToken.None)
			.ConfigureAwait(false);
		var failedMessage = failed.FirstOrDefault(m => m.Id == message.Id);

		_ = failedMessage.ShouldNotBeNull();
		failedMessage.LastError.ShouldBe("Connection timeout");
		failedMessage.RetryCount.ShouldBe(1);
	}

	[Fact]
	public async Task MarkFailed_WithNullMessageId_ThrowsArgumentException()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(async () =>
			await Store.MarkFailedAsync(null!, "error", 1, CancellationToken.None).ConfigureAwait(false));
	}

	[Fact]
	public async Task MarkFailed_WithNullErrorMessage_ThrowsArgumentNullException()
	{
		// Arrange
		var message = CreateTestMessage();
		await Store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(async () =>
			await Store.MarkFailedAsync(message.Id, null!, 1, CancellationToken.None).ConfigureAwait(false));
	}

	[Fact]
	public async Task MarkFailed_IncrementingRetryCount_TracksRetries()
	{
		// Arrange
		var message = CreateTestMessage();
		await Store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);

		// Act - Fail multiple times
		await Store.MarkFailedAsync(message.Id, "Error 1", 1, CancellationToken.None).ConfigureAwait(false);
		await Store.MarkFailedAsync(message.Id, "Error 2", 2, CancellationToken.None).ConfigureAwait(false);
		await Store.MarkFailedAsync(message.Id, "Error 3", 3, CancellationToken.None).ConfigureAwait(false);

		// Assert
		var failed = await Admin.GetFailedMessagesAsync(10, null, 10, CancellationToken.None)
			.ConfigureAwait(false);
		var failedMessage = failed.FirstOrDefault(m => m.Id == message.Id);

		_ = failedMessage.ShouldNotBeNull();
		failedMessage.RetryCount.ShouldBe(3);
		failedMessage.LastError.ShouldBe("Error 3");
	}

	#endregion MarkFailed Tests

	#region GetFailedMessages Tests

	[Fact]
	public async Task GetFailedMessages_ReturnsOnlyFailed()
	{
		// Arrange
		var stagedMessage = CreateTestMessage();
		var sentMessage = CreateTestMessage();
		var failedMessage = CreateTestMessage();

		await Store.StageMessageAsync(stagedMessage, CancellationToken.None).ConfigureAwait(false);
		await Store.StageMessageAsync(sentMessage, CancellationToken.None).ConfigureAwait(false);
		await Store.StageMessageAsync(failedMessage, CancellationToken.None).ConfigureAwait(false);

		await Store.MarkSentAsync(sentMessage.Id, CancellationToken.None).ConfigureAwait(false);
		await Store.MarkFailedAsync(failedMessage.Id, "Error", 1, CancellationToken.None).ConfigureAwait(false);

		// Act
		var failed = await Admin.GetFailedMessagesAsync(5, null, 10, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		failed.Count().ShouldBe(1);
		failed.First().Id.ShouldBe(failedMessage.Id);
	}

	[Fact]
	public async Task GetFailedMessages_RespectsMaxRetries()
	{
		// Arrange
		var lowRetryMessage = CreateTestMessage();
		var highRetryMessage = CreateTestMessage();

		await Store.StageMessageAsync(lowRetryMessage, CancellationToken.None).ConfigureAwait(false);
		await Store.StageMessageAsync(highRetryMessage, CancellationToken.None).ConfigureAwait(false);

		await Store.MarkFailedAsync(lowRetryMessage.Id, "Error", 2, CancellationToken.None).ConfigureAwait(false);
		await Store.MarkFailedAsync(highRetryMessage.Id, "Error", 5, CancellationToken.None).ConfigureAwait(false);

		// Act - Get only messages with < 3 retries
		var failed = await Admin.GetFailedMessagesAsync(3, null, 10, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		failed.ShouldContain(m => m.Id == lowRetryMessage.Id);
		failed.ShouldNotContain(m => m.Id == highRetryMessage.Id);
	}

	[Fact]
	public async Task GetFailedMessages_RespectsOlderThan()
	{
		// Arrange
		var message = CreateTestMessage();
		await Store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);
		await Store.MarkFailedAsync(message.Id, "Error", 1, CancellationToken.None).ConfigureAwait(false);

		// Act - Use future timestamp (should exclude all current failures)
		var failed = await Admin.GetFailedMessagesAsync(
			10,
			DateTimeOffset.UtcNow.AddSeconds(-1),
			10,
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		failed.ShouldBeEmpty();
	}

	[Fact]
	public async Task GetFailedMessages_RespectsBatchSize()
	{
		// Arrange
		for (int i = 0; i < 5; i++)
		{
			var message = CreateTestMessage();
			await Store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);
			await Store.MarkFailedAsync(message.Id, "Error", 1, CancellationToken.None).ConfigureAwait(false);
		}

		// Act
		var failed = await Admin.GetFailedMessagesAsync(10, null, 2, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert
		failed.Count().ShouldBe(2);
	}

	#endregion GetFailedMessages Tests

	#region GetScheduledMessages Tests

	[Fact]
	public async Task GetScheduledMessages_ReturnsScheduledBeforeTimestamp()
	{
		// Arrange
		var pastScheduled = CreateTestMessage(scheduledAt: DateTimeOffset.UtcNow.AddHours(-1));
		var futureScheduled = CreateTestMessage(scheduledAt: DateTimeOffset.UtcNow.AddHours(2));

		await Store.StageMessageAsync(pastScheduled, CancellationToken.None).ConfigureAwait(false);
		await Store.StageMessageAsync(futureScheduled, CancellationToken.None).ConfigureAwait(false);

		// Act - Get messages scheduled before now + 1 hour
		var scheduled = await Admin.GetScheduledMessagesAsync(
			DateTimeOffset.UtcNow.AddHours(1),
			10,
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		scheduled.ShouldContain(m => m.Id == pastScheduled.Id);
		scheduled.ShouldNotContain(m => m.Id == futureScheduled.Id);
	}

	[Fact]
	public async Task GetScheduledMessages_RespectsBatchSize()
	{
		// Arrange
		for (int i = 0; i < 5; i++)
		{
			var message = CreateTestMessage(scheduledAt: DateTimeOffset.UtcNow.AddMinutes(-10 + i));
			await Store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);
		}

		// Act
		var scheduled = await Admin.GetScheduledMessagesAsync(
			DateTimeOffset.UtcNow.AddHours(1),
			2,
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		scheduled.Count().ShouldBe(2);
	}

	#endregion GetScheduledMessages Tests

	#region CleanupSentMessages Tests

	[Fact]
	public async Task CleanupSentMessages_RemovesOldSentMessages()
	{
		// Arrange
		var message = CreateTestMessage();
		await Store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);
		await Store.MarkSentAsync(message.Id, CancellationToken.None).ConfigureAwait(false);

		// Act - Cleanup messages older than 0 seconds (all sent messages)
		var removed = await Admin.CleanupSentMessagesAsync(
			DateTimeOffset.UtcNow.AddSeconds(1),
			100,
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		removed.ShouldBeGreaterThanOrEqualTo(1);
	}

	[Fact]
	public async Task CleanupSentMessages_PreservesRecentMessages()
	{
		// Arrange
		var message = CreateTestMessage();
		await Store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);
		await Store.MarkSentAsync(message.Id, CancellationToken.None).ConfigureAwait(false);

		// Act - Cleanup only messages older than 1 hour ago (should preserve current)
		var removed = await Admin.CleanupSentMessagesAsync(
			DateTimeOffset.UtcNow.AddHours(-1),
			100,
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		removed.ShouldBe(0);
	}

	[Fact]
	public async Task CleanupSentMessages_PreservesPendingMessages()
	{
		// Arrange
		var message = CreateTestMessage();
		await Store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);

		// Act - Cleanup all
		var removed = await Admin.CleanupSentMessagesAsync(
			DateTimeOffset.UtcNow.AddSeconds(1),
			100,
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		removed.ShouldBe(0);

		var unsent = await Store.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false);
		unsent.ShouldContain(m => m.Id == message.Id);
	}

	[Fact]
	public async Task CleanupSentMessages_PreservesFailedMessages()
	{
		// Arrange
		var message = CreateTestMessage();
		await Store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);
		await Store.MarkFailedAsync(message.Id, "Error", 1, CancellationToken.None).ConfigureAwait(false);

		// Act - Cleanup all
		var removed = await Admin.CleanupSentMessagesAsync(
			DateTimeOffset.UtcNow.AddSeconds(1),
			100,
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		removed.ShouldBe(0);

		var failed = await Admin.GetFailedMessagesAsync(5, null, 10, CancellationToken.None)
			.ConfigureAwait(false);
		failed.ShouldContain(m => m.Id == message.Id);
	}

	[Fact]
	public async Task CleanupSentMessages_RespectsBatchSize()
	{
		// Arrange
		for (int i = 0; i < 5; i++)
		{
			var message = CreateTestMessage();
			await Store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);
			await Store.MarkSentAsync(message.Id, CancellationToken.None).ConfigureAwait(false);
		}

		// Act - Cleanup with batch size of 2
		var removed = await Admin.CleanupSentMessagesAsync(
			DateTimeOffset.UtcNow.AddSeconds(1),
			2,
			CancellationToken.None).ConfigureAwait(false);

		// Assert
		removed.ShouldBe(2);
	}

	#endregion CleanupSentMessages Tests

	#region GetStatistics Tests

	[Fact]
	public async Task GetStatistics_EmptyStore_ReturnsZeroCounts()
	{
		// Act
		var stats = await Admin.GetStatisticsAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert
		stats.TotalMessageCount.ShouldBe(0);
		stats.StagedMessageCount.ShouldBe(0);
		stats.SentMessageCount.ShouldBe(0);
		stats.FailedMessageCount.ShouldBe(0);
	}

	[Fact]
	public async Task GetStatistics_TracksStagedMessages()
	{
		// Arrange
		for (int i = 0; i < 3; i++)
		{
			await Store.StageMessageAsync(CreateTestMessage(), CancellationToken.None).ConfigureAwait(false);
		}

		// Act
		var stats = await Admin.GetStatisticsAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert
		stats.StagedMessageCount.ShouldBe(3);
	}

	[Fact]
	public async Task GetStatistics_TracksSentMessages()
	{
		// Arrange
		var message = CreateTestMessage();
		await Store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);
		await Store.MarkSentAsync(message.Id, CancellationToken.None).ConfigureAwait(false);

		// Act
		var stats = await Admin.GetStatisticsAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert
		stats.SentMessageCount.ShouldBe(1);
		stats.StagedMessageCount.ShouldBe(0);
	}

	[Fact]
	public async Task GetStatistics_TracksFailedMessages()
	{
		// Arrange
		var message = CreateTestMessage();
		await Store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);
		await Store.MarkFailedAsync(message.Id, "Error", 1, CancellationToken.None).ConfigureAwait(false);

		// Act
		var stats = await Admin.GetStatisticsAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert
		stats.FailedMessageCount.ShouldBe(1);
	}

	[Fact]
	public async Task GetStatistics_TracksAllStates()
	{
		// Arrange
		var staged = CreateTestMessage();
		var sent = CreateTestMessage();
		var failed = CreateTestMessage();

		await Store.StageMessageAsync(staged, CancellationToken.None).ConfigureAwait(false);
		await Store.StageMessageAsync(sent, CancellationToken.None).ConfigureAwait(false);
		await Store.StageMessageAsync(failed, CancellationToken.None).ConfigureAwait(false);

		await Store.MarkSentAsync(sent.Id, CancellationToken.None).ConfigureAwait(false);
		await Store.MarkFailedAsync(failed.Id, "Error", 1, CancellationToken.None).ConfigureAwait(false);

		// Act
		var stats = await Admin.GetStatisticsAsync(CancellationToken.None).ConfigureAwait(false);

		// Assert
		stats.TotalMessageCount.ShouldBe(3);
		stats.StagedMessageCount.ShouldBe(1);
		stats.SentMessageCount.ShouldBe(1);
		stats.FailedMessageCount.ShouldBe(1);
	}

	#endregion GetStatistics Tests

	#region Concurrency Tests

	[Fact]
	public async Task ConcurrentMarkSent_OnlyOneSucceeds()
	{
		// Arrange
		var message = CreateTestMessage();
		await Store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);

		const int concurrentAttempts = 10;
		var tasks = new List<Task<bool>>();

		// Act - Try to mark sent concurrently
		for (int i = 0; i < concurrentAttempts; i++)
		{
			tasks.Add(Task.Run(async () =>
			{
				try
				{
					await Store.MarkSentAsync(message.Id, CancellationToken.None).ConfigureAwait(false);
					return true;
				}
				catch (InvalidOperationException)
				{
					return false;
				}
			}));
		}

		var results = await Task.WhenAll(tasks).ConfigureAwait(false);

		// Assert - Exactly one should succeed
		var successCount = results.Count(r => r);
		successCount.ShouldBe(1, "Only one concurrent MarkSent should succeed");
	}

	[Fact]
	public async Task ConcurrentStagingDifferentMessages_AllSucceed()
	{
		// Arrange
		const int concurrentMessages = 20;
		var messages = Enumerable.Range(0, concurrentMessages)
			.Select(_ => CreateTestMessage())
			.ToList();

		// Act
		var tasks = messages.Select(m =>
			Store.StageMessageAsync(m, CancellationToken.None).AsTask());
		await Task.WhenAll(tasks).ConfigureAwait(false);

		// Assert
		var stats = await Admin.GetStatisticsAsync(CancellationToken.None).ConfigureAwait(false);
		stats.StagedMessageCount.ShouldBe(concurrentMessages);
	}

	[Fact]
	public async Task ConcurrentMixedOperations_MaintainsConsistency()
	{
		// Arrange
		const int messageCount = 10;
		var messages = Enumerable.Range(0, messageCount)
			.Select(_ => CreateTestMessage())
			.ToList();

		// Stage all first
		foreach (var message in messages)
		{
			await Store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);
		}

		// Act - Concurrently mark some sent and some failed
		var tasks = new List<Task>();
		for (int i = 0; i < messageCount; i++)
		{
			var idx = i;
			if (idx % 2 == 0)
			{
				tasks.Add(Store.MarkSentAsync(messages[idx].Id, CancellationToken.None).AsTask());
			}
			else
			{
				tasks.Add(Store.MarkFailedAsync(messages[idx].Id, "Error", 1, CancellationToken.None).AsTask());
			}
		}

		await Task.WhenAll(tasks).ConfigureAwait(false);

		// Assert
		var stats = await Admin.GetStatisticsAsync(CancellationToken.None).ConfigureAwait(false);
		stats.StagedMessageCount.ShouldBe(0);
		stats.SentMessageCount.ShouldBe(messageCount / 2);
		stats.FailedMessageCount.ShouldBe(messageCount / 2);
	}

	#endregion Concurrency Tests
}
