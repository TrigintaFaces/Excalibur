// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


#pragma warning disable IDE0270 // Null check can be simplified

using System.Text;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Testing.Conformance;

/// <summary>
/// Abstract base class for IOutboxStore conformance testing.
/// </summary>
/// <remarks>
/// <para>
/// Inherit from this class and implement <see cref="CreateStore"/> to verify that
/// your outbox store implementation conforms to the IOutboxStore contract.
/// </para>
/// <para>
/// The test kit verifies core outbox operations including stage, mark sent, mark failed,
/// retrieval, cleanup, and statistics behavior.
/// </para>
/// <para>
/// Note: EnqueueAsync is intentionally excluded as it requires IUtf8JsonSerializer
/// dependency. Use StageMessageAsync for conformance testing.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public class SqlServerOutboxStoreConformanceTests : OutboxStoreConformanceTestKit
/// {
///     private readonly SqlServerFixture _fixture;
///
///     protected override IOutboxStore CreateStore() =>
///         new SqlServerOutboxStore(_fixture.ConnectionString);
///
///     protected override async Task CleanupAsync() =>
///         await _fixture.CleanupAsync();
/// }
/// </code>
/// </example>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Test method naming convention")]
public abstract class OutboxStoreConformanceTestKit
{
	/// <summary>
	/// Creates a fresh outbox store instance for testing.
	/// </summary>
	/// <returns>An IOutboxStore implementation to test.</returns>
	protected abstract IOutboxStore CreateStore();

	/// <summary>
	/// Creates the admin interface for the outbox store.
	/// </summary>
	/// <remarks>
	/// The default implementation casts <see cref="CreateStore"/> to <see cref="IOutboxStoreAdmin"/>.
	/// Override if the admin interface is registered separately.
	/// </remarks>
	/// <returns>An IOutboxStoreAdmin implementation to test, or null if not supported.</returns>
	protected virtual IOutboxStoreAdmin? CreateAdminStore()
		=> CreateStore() as IOutboxStoreAdmin;

	/// <summary>
	/// Optional cleanup after each test.
	/// </summary>
	/// <returns>A task representing the cleanup operation.</returns>
	protected virtual Task CleanupAsync() => Task.CompletedTask;

	/// <summary>
	/// Generates a unique message ID for test isolation.
	/// </summary>
	/// <returns>A unique message identifier.</returns>
	protected virtual string GenerateMessageId() => Guid.NewGuid().ToString();

	/// <summary>
	/// Creates a payload from the given content string.
	/// </summary>
	/// <param name="content">The content to encode.</param>
	/// <returns>The encoded payload bytes.</returns>
	protected virtual byte[] CreatePayload(string content) =>
		Encoding.UTF8.GetBytes(content);

	/// <summary>
	/// Creates a test outbound message with default values.
	/// </summary>
	/// <returns>A new OutboundMessage for testing.</returns>
	protected virtual OutboundMessage CreateTestMessage()
	{
		return new OutboundMessage(
			messageType: "TestMessageType",
			payload: CreatePayload("Test payload content"),
			destination: "test-destination")
		{
			Id = GenerateMessageId()
		};
	}

	/// <summary>
	/// Creates a test outbound message with specified message ID.
	/// </summary>
	/// <param name="messageId">The message ID to use.</param>
	/// <returns>A new OutboundMessage for testing.</returns>
	protected virtual OutboundMessage CreateTestMessage(string messageId)
	{
		return new OutboundMessage(
			messageType: "TestMessageType",
			payload: CreatePayload("Test payload content"),
			destination: "test-destination")
		{
			Id = messageId
		};
	}

	/// <summary>
	/// Creates a test outbound message with scheduled delivery time.
	/// </summary>
	/// <param name="scheduledAt">The scheduled delivery time.</param>
	/// <returns>A new OutboundMessage scheduled for future delivery.</returns>
	protected virtual OutboundMessage CreateScheduledMessage(DateTimeOffset scheduledAt)
	{
		return new OutboundMessage(
			messageType: "ScheduledTestMessage",
			payload: CreatePayload("Scheduled payload"),
			destination: "test-destination")
		{
			Id = GenerateMessageId(),
			ScheduledAt = scheduledAt
		};
	}

	#region Stage Tests

	/// <summary>
	/// Verifies that staging a new message succeeds.
	/// </summary>
	public virtual async Task StageMessageAsync_NewMessage_ShouldSucceed()
	{
		var store = CreateStore();
		var message = CreateTestMessage();

		await store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);

		var unsent = await store.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false);
		var unsentList = unsent.ToList();

		var found = unsentList.Any(m => m.Id == message.Id);
		if (!found)
		{
			throw new TestFixtureAssertionException(
				$"Expected to find staged message with ID '{message.Id}' in unsent messages");
		}
	}

	/// <summary>
	/// Verifies that staging a duplicate message ID throws.
	/// </summary>
	public virtual async Task StageMessageAsync_DuplicateId_ShouldThrowInvalidOperationException()
	{
		var store = CreateStore();
		var messageId = GenerateMessageId();
		var message1 = CreateTestMessage(messageId);
		var message2 = CreateTestMessage(messageId);

		await store.StageMessageAsync(message1, CancellationToken.None).ConfigureAwait(false);

		var threw = false;
		try
		{
			await store.StageMessageAsync(message2, CancellationToken.None).ConfigureAwait(false);
		}
		catch (InvalidOperationException)
		{
			threw = true;
		}

		if (!threw)
		{
			throw new TestFixtureAssertionException(
				$"Expected duplicate stage for message '{messageId}' to throw InvalidOperationException.");
		}
	}

	/// <summary>
	/// Verifies that staging a scheduled message stores it correctly.
	/// </summary>
	/// <remarks>
	/// Scheduled messages should appear in GetScheduledMessagesAsync results.
	/// Some providers may also include them in GetUnsentMessagesAsync - both behaviors are acceptable.
	/// </remarks>
	public virtual async Task StageMessageAsync_WithScheduledAt_ShouldStoreCorrectly()
	{
		var store = CreateStore();
		var admin = store as IOutboxStoreAdmin ?? CreateAdminStore();
		if (admin is null)
		{
			return; // Admin interface not supported
		}

		var futureTime = DateTimeOffset.UtcNow.AddHours(1);
		var message = CreateScheduledMessage(futureTime);

		await store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);

		// Scheduled messages SHOULD appear in GetScheduledMessagesAsync
		var scheduled = await admin.GetScheduledMessagesAsync(
			DateTimeOffset.UtcNow.AddHours(2),
			10,
			CancellationToken.None).ConfigureAwait(false);
		var inScheduled = scheduled.Any(m => m.Id == message.Id);

		if (!inScheduled)
		{
			throw new TestFixtureAssertionException(
				"Scheduled message should appear in GetScheduledMessagesAsync results");
		}

		// Verify the ScheduledAt property was preserved
		var foundMessage = scheduled.First(m => m.Id == message.Id);
		if (foundMessage.ScheduledAt is null)
		{
			throw new TestFixtureAssertionException(
				"Scheduled message should have ScheduledAt property set");
		}
	}

	#endregion

	#region Retrieval Tests

	/// <summary>
	/// Verifies that GetUnsentMessagesAsync returns staged messages.
	/// </summary>
	public virtual async Task GetUnsentMessagesAsync_ShouldReturnStagedMessages()
	{
		var store = CreateStore();
		var message1 = CreateTestMessage();
		var message2 = CreateTestMessage();

		await store.StageMessageAsync(message1, CancellationToken.None).ConfigureAwait(false);
		await store.StageMessageAsync(message2, CancellationToken.None).ConfigureAwait(false);

		var unsent = await store.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false);
		var unsentList = unsent.ToList();

		var hasMessage1 = unsentList.Any(m => m.Id == message1.Id);
		var hasMessage2 = unsentList.Any(m => m.Id == message2.Id);

		if (!hasMessage1 || !hasMessage2)
		{
			throw new TestFixtureAssertionException(
				$"Expected both staged messages to be returned. Found message1: {hasMessage1}, message2: {hasMessage2}");
		}
	}

	/// <summary>
	/// Verifies that GetUnsentMessagesAsync respects batch size.
	/// </summary>
	public virtual async Task GetUnsentMessagesAsync_ShouldRespectBatchSize()
	{
		var store = CreateStore();

		// Stage 5 messages
		for (var i = 0; i < 5; i++)
		{
			await store.StageMessageAsync(CreateTestMessage(), CancellationToken.None).ConfigureAwait(false);
		}

		var unsent = await store.GetUnsentMessagesAsync(2, CancellationToken.None).ConfigureAwait(false);
		var unsentList = unsent.ToList();

		if (unsentList.Count > 2)
		{
			throw new TestFixtureAssertionException(
				$"Expected at most 2 messages due to batch size limit but got {unsentList.Count}");
		}
	}

	#endregion

	#region Sent Tests

	/// <summary>
	/// Verifies that MarkSentAsync sets SentAt timestamp.
	/// </summary>
	public virtual async Task MarkSentAsync_ExistingMessage_ShouldSetSentAt()
	{
		var store = CreateStore();
		var message = CreateTestMessage();

		await store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);
		await store.MarkSentAsync(message.Id, CancellationToken.None).ConfigureAwait(false);

		// Sent messages should not appear in unsent
		var unsent = await store.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false);
		var stillUnsent = unsent.Any(m => m.Id == message.Id);

		if (stillUnsent)
		{
			throw new TestFixtureAssertionException(
				"Message marked as sent should not appear in unsent messages");
		}
	}

	/// <summary>
	/// Verifies that marking a sent message excludes it from unsent.
	/// </summary>
	public virtual async Task MarkSentAsync_ShouldExcludeFromUnsent()
	{
		var store = CreateStore();
		var message1 = CreateTestMessage();
		var message2 = CreateTestMessage();

		await store.StageMessageAsync(message1, CancellationToken.None).ConfigureAwait(false);
		await store.StageMessageAsync(message2, CancellationToken.None).ConfigureAwait(false);

		// Mark only message1 as sent
		await store.MarkSentAsync(message1.Id, CancellationToken.None).ConfigureAwait(false);

		var unsent = await store.GetUnsentMessagesAsync(10, CancellationToken.None).ConfigureAwait(false);
		var unsentList = unsent.ToList();

		var hasMessage1 = unsentList.Any(m => m.Id == message1.Id);
		var hasMessage2 = unsentList.Any(m => m.Id == message2.Id);

		if (hasMessage1)
		{
			throw new TestFixtureAssertionException(
				"Sent message should not appear in unsent list");
		}

		if (!hasMessage2)
		{
			throw new TestFixtureAssertionException(
				"Unsent message2 should still be in unsent list");
		}
	}

	/// <summary>
	/// Verifies that MarkSentAsync for non-existent message throws.
	/// </summary>
	public virtual async Task MarkSentAsync_NonExistent_ShouldThrowInvalidOperationException()
	{
		var store = CreateStore();
		var nonExistentId = GenerateMessageId();

		var threw = false;
		try
		{
			await store.MarkSentAsync(nonExistentId, CancellationToken.None).ConfigureAwait(false);
		}
		catch (InvalidOperationException)
		{
			threw = true;
		}

		if (!threw)
		{
			throw new TestFixtureAssertionException(
				$"Expected MarkSentAsync to throw InvalidOperationException for message '{nonExistentId}'.");
		}
	}

	#endregion

	#region Failure Tests

	/// <summary>
	/// Verifies that MarkFailedAsync sets error message.
	/// </summary>
	public virtual async Task MarkFailedAsync_ShouldSetErrorMessage()
	{
		var store = CreateStore();
		var admin = store as IOutboxStoreAdmin ?? CreateAdminStore();
		if (admin is null)
		{
			return; // Admin interface not supported
		}

		var message = CreateTestMessage();
		var errorMessage = "Test error message";

		await store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);
		await store.MarkFailedAsync(message.Id, errorMessage, 1, CancellationToken.None).ConfigureAwait(false);

		var failed = await admin.GetFailedMessagesAsync(10, null, 10, CancellationToken.None).ConfigureAwait(false);
		var failedMessage = failed.FirstOrDefault(m => m.Id == message.Id);

		if (failedMessage is null)
		{
			throw new TestFixtureAssertionException(
				"Expected failed message in GetFailedMessagesAsync results");
		}

		if (failedMessage.LastError != errorMessage)
		{
			throw new TestFixtureAssertionException(
				$"Expected LastError '{errorMessage}' but got '{failedMessage.LastError}'");
		}
	}

	/// <summary>
	/// Verifies that MarkFailedAsync sets retry count.
	/// </summary>
	public virtual async Task MarkFailedAsync_ShouldSetRetryCount()
	{
		var store = CreateStore();
		var admin = store as IOutboxStoreAdmin ?? CreateAdminStore();
		if (admin is null)
		{
			return; // Admin interface not supported
		}

		var message = CreateTestMessage();

		await store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);
		await store.MarkFailedAsync(message.Id, "Error 1", 1, CancellationToken.None).ConfigureAwait(false);

		var failed = await admin.GetFailedMessagesAsync(10, null, 10, CancellationToken.None).ConfigureAwait(false);
		var failedMessage = failed.FirstOrDefault(m => m.Id == message.Id);

		if (failedMessage is null)
		{
			throw new TestFixtureAssertionException("Expected failed message");
		}

		if (failedMessage.RetryCount != 1)
		{
			throw new TestFixtureAssertionException(
				$"Expected RetryCount 1 but got {failedMessage.RetryCount}");
		}
	}

	/// <summary>
	/// Verifies that GetFailedMessagesAsync respects maxRetries filter.
	/// </summary>
	public virtual async Task GetFailedMessagesAsync_ShouldRespectMaxRetries()
	{
		var store = CreateStore();
		var admin = store as IOutboxStoreAdmin ?? CreateAdminStore();
		if (admin is null)
		{
			return; // Admin interface not supported
		}

		var message1 = CreateTestMessage();
		var message2 = CreateTestMessage();

		await store.StageMessageAsync(message1, CancellationToken.None).ConfigureAwait(false);
		await store.StageMessageAsync(message2, CancellationToken.None).ConfigureAwait(false);

		// Fail message1 with 2 retries, message2 with 5 retries
		await store.MarkFailedAsync(message1.Id, "Error", 2, CancellationToken.None).ConfigureAwait(false);
		await store.MarkFailedAsync(message2.Id, "Error", 5, CancellationToken.None).ConfigureAwait(false);

		// Query with maxRetries=3 - should only return message1
		var failed = await admin.GetFailedMessagesAsync(3, null, 10, CancellationToken.None).ConfigureAwait(false);
		var failedList = failed.ToList();

		var hasExcessiveRetries = failedList.Any(m => m.RetryCount > 3);
		if (hasExcessiveRetries)
		{
			throw new TestFixtureAssertionException(
				"GetFailedMessagesAsync should not return messages exceeding maxRetries");
		}
	}

	/// <summary>
	/// Verifies that GetFailedMessagesAsync respects olderThan filter.
	/// </summary>
	public virtual async Task GetFailedMessagesAsync_ShouldRespectOlderThan()
	{
		var store = CreateStore();
		var admin = store as IOutboxStoreAdmin ?? CreateAdminStore();
		if (admin is null)
		{
			return; // Admin interface not supported
		}

		var message = CreateTestMessage();

		await store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);
		await store.MarkFailedAsync(message.Id, "Error", 1, CancellationToken.None).ConfigureAwait(false);

		// Query for messages older than 1 minute ago - our just-failed message should NOT match
		var pastThreshold = DateTimeOffset.UtcNow.AddMinutes(-1);
		var failed = await admin.GetFailedMessagesAsync(10, pastThreshold, 10, CancellationToken.None).ConfigureAwait(false);
		var hasRecentMessage = failed.Any(m => m.Id == message.Id);

		if (hasRecentMessage)
		{
			throw new TestFixtureAssertionException(
				"Recently failed message should not appear when olderThan is in the past");
		}
	}

	#endregion

	#region Scheduled Tests

	/// <summary>
	/// Verifies that GetScheduledMessagesAsync returns scheduled messages before threshold.
	/// </summary>
	public virtual async Task GetScheduledMessagesAsync_ShouldReturnScheduledBeforeThreshold()
	{
		var store = CreateStore();
		var admin = store as IOutboxStoreAdmin ?? CreateAdminStore();
		if (admin is null)
		{
			return; // Admin interface not supported
		}

		// Schedule message for 30 minutes from now
		var scheduledTime = DateTimeOffset.UtcNow.AddMinutes(30);
		var message = CreateScheduledMessage(scheduledTime);

		await store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);

		// Query for messages scheduled before 1 hour from now - should include our message
		var scheduled = await admin.GetScheduledMessagesAsync(
			DateTimeOffset.UtcNow.AddHours(1),
			10,
			CancellationToken.None).ConfigureAwait(false);

		var found = scheduled.Any(m => m.Id == message.Id);
		if (!found)
		{
			throw new TestFixtureAssertionException(
				"Scheduled message should be returned when its schedule time is before the threshold");
		}
	}

	/// <summary>
	/// Verifies that GetScheduledMessagesAsync does not return immediate messages.
	/// </summary>
	public virtual async Task GetScheduledMessagesAsync_ShouldNotReturnImmediateMessages()
	{
		var store = CreateStore();
		var admin = store as IOutboxStoreAdmin ?? CreateAdminStore();
		if (admin is null)
		{
			return; // Admin interface not supported
		}

		// Stage an immediate message (no ScheduledAt)
		var immediateMessage = CreateTestMessage();

		await store.StageMessageAsync(immediateMessage, CancellationToken.None).ConfigureAwait(false);

		var scheduled = await admin.GetScheduledMessagesAsync(
			DateTimeOffset.UtcNow.AddHours(1),
			10,
			CancellationToken.None).ConfigureAwait(false);

		var hasImmediate = scheduled.Any(m => m.Id == immediateMessage.Id);
		if (hasImmediate)
		{
			throw new TestFixtureAssertionException(
				"Immediate messages (no ScheduledAt) should not appear in scheduled messages");
		}
	}

	#endregion

	#region Cleanup Tests

	/// <summary>
	/// Verifies that CleanupSentMessagesAsync removes old sent messages.
	/// </summary>
	public virtual async Task CleanupSentMessagesAsync_ShouldRemoveOldMessages()
	{
		var store = CreateStore();
		var admin = store as IOutboxStoreAdmin ?? CreateAdminStore();
		if (admin is null)
		{
			return; // Admin interface not supported
		}

		var message = CreateTestMessage();

		await store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);
		await store.MarkSentAsync(message.Id, CancellationToken.None).ConfigureAwait(false);

		// Cleanup with future threshold - should remove our just-sent message
		var futureThreshold = DateTimeOffset.UtcNow.AddHours(1);
		var removed = await admin.CleanupSentMessagesAsync(futureThreshold, 100, CancellationToken.None)
			.ConfigureAwait(false);

		// Should have removed at least 1 message
		if (removed < 1)
		{
			throw new TestFixtureAssertionException(
				"Expected CleanupSentMessagesAsync to remove at least 1 message with future threshold");
		}
	}

	/// <summary>
	/// Verifies that CleanupSentMessagesAsync respects batch size.
	/// </summary>
	public virtual async Task CleanupSentMessagesAsync_ShouldRespectBatchSize()
	{
		var store = CreateStore();
		var admin = store as IOutboxStoreAdmin ?? CreateAdminStore();
		if (admin is null)
		{
			return; // Admin interface not supported
		}

		// Stage and send 5 messages
		for (var i = 0; i < 5; i++)
		{
			var message = CreateTestMessage();
			await store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);
			await store.MarkSentAsync(message.Id, CancellationToken.None).ConfigureAwait(false);
		}

		// Cleanup with batch size of 2
		var futureThreshold = DateTimeOffset.UtcNow.AddHours(1);
		var removed = await admin.CleanupSentMessagesAsync(futureThreshold, 2, CancellationToken.None)
			.ConfigureAwait(false);

		if (removed > 2)
		{
			throw new TestFixtureAssertionException(
				$"Expected at most 2 removed due to batch size but got {removed}");
		}
	}

	#endregion

	#region Statistics Tests

	/// <summary>
	/// Verifies that GetStatisticsAsync reflects message counts.
	/// </summary>
	public virtual async Task GetStatisticsAsync_ShouldReflectMessageCounts()
	{
		var store = CreateStore();
		var admin = store as IOutboxStoreAdmin ?? CreateAdminStore();
		if (admin is null)
		{
			return; // Admin interface not supported
		}

		// Stage a message
		var stagedMessage = CreateTestMessage();
		await store.StageMessageAsync(stagedMessage, CancellationToken.None).ConfigureAwait(false);

		// Stage and send a message
		var sentMessage = CreateTestMessage();
		await store.StageMessageAsync(sentMessage, CancellationToken.None).ConfigureAwait(false);
		await store.MarkSentAsync(sentMessage.Id, CancellationToken.None).ConfigureAwait(false);

		// Stage and fail a message
		var failedMessage = CreateTestMessage();
		await store.StageMessageAsync(failedMessage, CancellationToken.None).ConfigureAwait(false);
		await store.MarkFailedAsync(failedMessage.Id, "Error", 1, CancellationToken.None).ConfigureAwait(false);

		var stats = await admin.GetStatisticsAsync(CancellationToken.None).ConfigureAwait(false);

		if (stats is null)
		{
			throw new TestFixtureAssertionException("Expected statistics but got null");
		}

		if (stats.StagedMessageCount < 1)
		{
			throw new TestFixtureAssertionException(
				$"Expected at least 1 staged message but got {stats.StagedMessageCount}");
		}

		if (stats.SentMessageCount < 1)
		{
			throw new TestFixtureAssertionException(
				$"Expected at least 1 sent message but got {stats.SentMessageCount}");
		}

		if (stats.FailedMessageCount < 1)
		{
			throw new TestFixtureAssertionException(
				$"Expected at least 1 failed message but got {stats.FailedMessageCount}");
		}
	}

	/// <summary>
	/// Verifies that GetStatisticsAsync updates accurately after operations.
	/// </summary>
	public virtual async Task GetStatisticsAsync_AfterOperations_ShouldUpdateAccurately()
	{
		var store = CreateStore();
		var admin = store as IOutboxStoreAdmin ?? CreateAdminStore();
		if (admin is null)
		{
			return; // Admin interface not supported
		}

		// Get initial stats
		var initialStats = await admin.GetStatisticsAsync(CancellationToken.None).ConfigureAwait(false);
		var initialTotal = initialStats?.TotalMessageCount ?? 0;

		// Stage a message
		var message = CreateTestMessage();
		await store.StageMessageAsync(message, CancellationToken.None).ConfigureAwait(false);

		// Get stats after staging
		var afterStageStats = await admin.GetStatisticsAsync(CancellationToken.None).ConfigureAwait(false);

		if (afterStageStats is null)
		{
			throw new TestFixtureAssertionException("Expected statistics after staging but got null");
		}

		if (afterStageStats.TotalMessageCount <= initialTotal)
		{
			throw new TestFixtureAssertionException(
				$"Expected total count to increase after staging. Initial: {initialTotal}, After: {afterStageStats.TotalMessageCount}");
		}
	}

	#endregion
}
