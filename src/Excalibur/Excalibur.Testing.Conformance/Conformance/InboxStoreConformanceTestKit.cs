// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


#pragma warning disable IDE0270 // Null check can be simplified

using System.Text;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Testing.Conformance;

/// <summary>
/// Abstract base class for IInboxStore conformance testing.
/// </summary>
/// <remarks>
/// <para>
/// Inherit from this class and implement <see cref="CreateStore"/> to verify that
/// your inbox store implementation conforms to the IInboxStore contract.
/// </para>
/// <para>
/// The test kit verifies core inbox operations including create, process, fail,
/// query, and cleanup behavior.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// public class SqlServerInboxStoreConformanceTests : InboxStoreConformanceTestKit
/// {
///     private readonly SqlServerFixture _fixture;
///
///     protected override IInboxStore CreateStore() =>
///         new SqlServerInboxStore(_fixture.ConnectionString);
///
///     protected override async Task CleanupAsync() =>
///         await _fixture.CleanupAsync();
/// }
/// </code>
/// </example>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores",
	Justification = "Test method naming convention")]
public abstract class InboxStoreConformanceTestKit
{
	/// <summary>
	/// Creates a fresh inbox store instance for testing.
	/// </summary>
	/// <returns>An IInboxStore implementation to test.</returns>
	protected abstract IInboxStore CreateStore();

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
	/// Generates a unique handler type name for test isolation.
	/// </summary>
	/// <returns>A unique handler type name.</returns>
	protected virtual string GenerateHandlerType() => $"TestHandler_{Guid.NewGuid():N}";

	/// <summary>
	/// Creates a payload from the given content string.
	/// </summary>
	/// <param name="content">The content to encode.</param>
	/// <returns>The encoded payload bytes.</returns>
	protected virtual byte[] CreatePayload(string content) =>
		Encoding.UTF8.GetBytes(content);

	/// <summary>
	/// Creates default metadata for testing.
	/// </summary>
	/// <returns>A dictionary with default test metadata.</returns>
	protected virtual IDictionary<string, object> CreateDefaultMetadata() =>
		new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["TestKey"] = "TestValue",
			["Timestamp"] = DateTimeOffset.UtcNow.ToString("O")
		};

	#region Create Tests

	/// <summary>
	/// Verifies that creating a new inbox entry succeeds.
	/// </summary>
	public virtual async Task CreateEntryAsync_NewEntry_ShouldSucceed()
	{
		var store = CreateStore();
		var messageId = GenerateMessageId();
		var handlerType = GenerateHandlerType();
		var messageType = "TestMessageType";
		var payload = CreatePayload("Test payload content");
		var metadata = CreateDefaultMetadata();

		var entry = await store.CreateEntryAsync(
			messageId,
			handlerType,
			messageType,
			payload,
			metadata,
			CancellationToken.None).ConfigureAwait(false);

		if (entry is null)
		{
			throw new TestFixtureAssertionException("Expected inbox entry but got null");
		}

		if (entry.MessageId != messageId)
		{
			throw new TestFixtureAssertionException(
				$"MessageId mismatch: expected '{messageId}', got '{entry.MessageId}'");
		}

		if (entry.HandlerType != handlerType)
		{
			throw new TestFixtureAssertionException(
				$"HandlerType mismatch: expected '{handlerType}', got '{entry.HandlerType}'");
		}

		if (entry.MessageType != messageType)
		{
			throw new TestFixtureAssertionException(
				$"MessageType mismatch: expected '{messageType}', got '{entry.MessageType}'");
		}

		if (entry.Status != InboxStatus.Received)
		{
			throw new TestFixtureAssertionException(
				$"Expected status Received but got {entry.Status}");
		}
	}

	/// <summary>
	/// Verifies that creating a duplicate entry throws.
	/// </summary>
	public virtual async Task CreateEntryAsync_DuplicateEntry_ShouldThrow()
	{
		var store = CreateStore();
		var messageId = GenerateMessageId();
		var handlerType = GenerateHandlerType();
		var messageType = "TestMessageType";
		var payload = CreatePayload("Test payload");
		var metadata = CreateDefaultMetadata();

		_ = await store.CreateEntryAsync(
			messageId,
			handlerType,
			messageType,
			payload,
			metadata,
			CancellationToken.None).ConfigureAwait(false);

		var exceptionThrown = false;
		try
		{
			_ = await store.CreateEntryAsync(
				messageId,
				handlerType,
				messageType,
				payload,
				metadata,
				CancellationToken.None).ConfigureAwait(false);
		}
		catch (InvalidOperationException)
		{
			exceptionThrown = true;
		}

		if (!exceptionThrown)
		{
			throw new TestFixtureAssertionException(
				"Expected InvalidOperationException for duplicate entry but no exception was thrown");
		}
	}

	/// <summary>
	/// Verifies that creating an entry preserves all metadata.
	/// </summary>
	public virtual async Task CreateEntryAsync_WithAllMetadata_ShouldPreserve()
	{
		var store = CreateStore();
		var messageId = GenerateMessageId();
		var handlerType = GenerateHandlerType();
		var messageType = "TestMessageType";
		var payloadContent = "Full metadata test payload";
		var payload = CreatePayload(payloadContent);
		var metadata = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["StringKey"] = "StringValue",
			["IntKey"] = 42,
			["BoolKey"] = true
		};

		var entry = await store.CreateEntryAsync(
			messageId,
			handlerType,
			messageType,
			payload,
			metadata,
			CancellationToken.None).ConfigureAwait(false);

		if (entry is null)
		{
			throw new TestFixtureAssertionException("Expected inbox entry but got null");
		}

		// Verify payload is preserved
		var decodedPayload = Encoding.UTF8.GetString(entry.Payload);
		if (decodedPayload != payloadContent)
		{
			throw new TestFixtureAssertionException(
				$"Payload mismatch: expected '{payloadContent}', got '{decodedPayload}'");
		}

		// Verify metadata is preserved
		if (entry.Metadata is null || entry.Metadata.Count < 3)
		{
			throw new TestFixtureAssertionException(
				$"Expected at least 3 metadata entries but got {entry.Metadata?.Count ?? 0}");
		}

		if (!entry.Metadata.TryGetValue("StringKey", out var stringValue) ||
			stringValue?.ToString() != "StringValue")
		{
			throw new TestFixtureAssertionException("Expected Metadata['StringKey'] = 'StringValue'");
		}
	}

	#endregion

	#region Process Tests

	/// <summary>
	/// Verifies that marking an existing entry as processed succeeds.
	/// </summary>
	public virtual async Task MarkProcessedAsync_ExistingEntry_ShouldSucceed()
	{
		var store = CreateStore();
		var messageId = GenerateMessageId();
		var handlerType = GenerateHandlerType();
		var messageType = "TestMessageType";
		var payload = CreatePayload("Test payload");
		var metadata = CreateDefaultMetadata();

		_ = await store.CreateEntryAsync(
			messageId,
			handlerType,
			messageType,
			payload,
			metadata,
			CancellationToken.None).ConfigureAwait(false);

		await store.MarkProcessedAsync(messageId, handlerType, CancellationToken.None)
			.ConfigureAwait(false);

		var entry = await store.GetEntryAsync(messageId, handlerType, CancellationToken.None)
			.ConfigureAwait(false);

		if (entry is null)
		{
			throw new TestFixtureAssertionException("Expected inbox entry but got null");
		}

		if (entry.Status != InboxStatus.Processed)
		{
			throw new TestFixtureAssertionException(
				$"Expected status Processed but got {entry.Status}");
		}

		if (entry.ProcessedAt is null)
		{
			throw new TestFixtureAssertionException("Expected ProcessedAt to be set");
		}
	}

	/// <summary>
	/// Verifies that TryMarkAsProcessedAsync returns true for new messages.
	/// </summary>
	public virtual async Task TryMarkAsProcessedAsync_FirstTime_ShouldReturnTrue()
	{
		var store = CreateStore();
		var messageId = GenerateMessageId();
		var handlerType = GenerateHandlerType();

		var result = await store.TryMarkAsProcessedAsync(
			messageId,
			handlerType,
			CancellationToken.None).ConfigureAwait(false);

		if (!result)
		{
			throw new TestFixtureAssertionException(
				"Expected TryMarkAsProcessedAsync to return true for first call");
		}
	}

	/// <summary>
	/// Verifies that TryMarkAsProcessedAsync returns false for already processed messages.
	/// </summary>
	public virtual async Task TryMarkAsProcessedAsync_AlreadyProcessed_ShouldReturnFalse()
	{
		var store = CreateStore();
		var messageId = GenerateMessageId();
		var handlerType = GenerateHandlerType();

		// First call - should return true
		_ = await store.TryMarkAsProcessedAsync(
			messageId,
			handlerType,
			CancellationToken.None).ConfigureAwait(false);

		// Second call - should return false (duplicate)
		var result = await store.TryMarkAsProcessedAsync(
			messageId,
			handlerType,
			CancellationToken.None).ConfigureAwait(false);

		if (result)
		{
			throw new TestFixtureAssertionException(
				"Expected TryMarkAsProcessedAsync to return false for duplicate call");
		}
	}

	/// <summary>
	/// Verifies that IsProcessedAsync returns true for processed messages.
	/// </summary>
	public virtual async Task IsProcessedAsync_ProcessedMessage_ShouldReturnTrue()
	{
		var store = CreateStore();
		var messageId = GenerateMessageId();
		var handlerType = GenerateHandlerType();

		_ = await store.TryMarkAsProcessedAsync(
			messageId,
			handlerType,
			CancellationToken.None).ConfigureAwait(false);

		var isProcessed = await store.IsProcessedAsync(
			messageId,
			handlerType,
			CancellationToken.None).ConfigureAwait(false);

		if (!isProcessed)
		{
			throw new TestFixtureAssertionException(
				"Expected IsProcessedAsync to return true for processed message");
		}
	}

	/// <summary>
	/// Verifies that IsProcessedAsync returns false for unprocessed messages.
	/// </summary>
	public virtual async Task IsProcessedAsync_UnprocessedMessage_ShouldReturnFalse()
	{
		var store = CreateStore();
		var messageId = GenerateMessageId();
		var handlerType = GenerateHandlerType();

		var isProcessed = await store.IsProcessedAsync(
			messageId,
			handlerType,
			CancellationToken.None).ConfigureAwait(false);

		if (isProcessed)
		{
			throw new TestFixtureAssertionException(
				"Expected IsProcessedAsync to return false for unprocessed message");
		}
	}

	#endregion

	#region Fail Tests

	/// <summary>
	/// Verifies that marking an entry as failed sets the status and error.
	/// </summary>
	public virtual async Task MarkFailedAsync_ExistingEntry_ShouldSetStatusAndError()
	{
		var store = CreateStore();
		var messageId = GenerateMessageId();
		var handlerType = GenerateHandlerType();
		var messageType = "TestMessageType";
		var payload = CreatePayload("Test payload");
		var metadata = CreateDefaultMetadata();
		var errorMessage = "Test error message";

		_ = await store.CreateEntryAsync(
			messageId,
			handlerType,
			messageType,
			payload,
			metadata,
			CancellationToken.None).ConfigureAwait(false);

		await store.MarkFailedAsync(
			messageId,
			handlerType,
			errorMessage,
			CancellationToken.None).ConfigureAwait(false);

		var entry = await store.GetEntryAsync(messageId, handlerType, CancellationToken.None)
			.ConfigureAwait(false);

		if (entry is null)
		{
			throw new TestFixtureAssertionException("Expected inbox entry but got null");
		}

		if (entry.Status != InboxStatus.Failed)
		{
			throw new TestFixtureAssertionException(
				$"Expected status Failed but got {entry.Status}");
		}

		if (entry.LastError != errorMessage)
		{
			throw new TestFixtureAssertionException(
				$"Expected LastError '{errorMessage}' but got '{entry.LastError}'");
		}
	}

	/// <summary>
	/// Verifies that marking an entry as failed increments retry count.
	/// </summary>
	public virtual async Task MarkFailedAsync_ShouldIncrementRetryCount()
	{
		var store = CreateStore();
		var messageId = GenerateMessageId();
		var handlerType = GenerateHandlerType();
		var messageType = "TestMessageType";
		var payload = CreatePayload("Test payload");
		var metadata = CreateDefaultMetadata();

		_ = await store.CreateEntryAsync(
			messageId,
			handlerType,
			messageType,
			payload,
			metadata,
			CancellationToken.None).ConfigureAwait(false);

		// First failure
		await store.MarkFailedAsync(messageId, handlerType, "Error 1", CancellationToken.None)
			.ConfigureAwait(false);

		var entry1 = await store.GetEntryAsync(messageId, handlerType, CancellationToken.None)
			.ConfigureAwait(false);

		if (entry1 is null || entry1.RetryCount != 1)
		{
			throw new TestFixtureAssertionException(
				$"Expected RetryCount 1 after first failure but got {entry1?.RetryCount ?? -1}");
		}

		// Second failure
		await store.MarkFailedAsync(messageId, handlerType, "Error 2", CancellationToken.None)
			.ConfigureAwait(false);

		var entry2 = await store.GetEntryAsync(messageId, handlerType, CancellationToken.None)
			.ConfigureAwait(false);

		if (entry2 is null || entry2.RetryCount != 2)
		{
			throw new TestFixtureAssertionException(
				$"Expected RetryCount 2 after second failure but got {entry2?.RetryCount ?? -1}");
		}
	}

	/// <summary>
	/// Verifies that GetFailedEntriesAsync respects maxRetries filter.
	/// </summary>
	public virtual async Task GetFailedEntriesAsync_ShouldRespectMaxRetries()
	{
		var store = CreateStore();
		var handlerType = GenerateHandlerType();
		var messageType = "TestMessageType";
		var payload = CreatePayload("Test payload");
		var metadata = CreateDefaultMetadata();

		// Create entry 1 with 1 retry
		var messageId1 = GenerateMessageId();
		_ = await store.CreateEntryAsync(messageId1, handlerType, messageType, payload, metadata, CancellationToken.None)
			.ConfigureAwait(false);
		await store.MarkFailedAsync(messageId1, handlerType, "Error", CancellationToken.None)
			.ConfigureAwait(false);

		// Create entry 2 with 3 retries
		var messageId2 = GenerateMessageId();
		_ = await store.CreateEntryAsync(messageId2, handlerType, messageType, payload, metadata, CancellationToken.None)
			.ConfigureAwait(false);
		await store.MarkFailedAsync(messageId2, handlerType, "Error 1", CancellationToken.None).ConfigureAwait(false);
		await store.MarkFailedAsync(messageId2, handlerType, "Error 2", CancellationToken.None).ConfigureAwait(false);
		await store.MarkFailedAsync(messageId2, handlerType, "Error 3", CancellationToken.None).ConfigureAwait(false);

		// Query with maxRetries=2 - should only return entry1 (1 retry <= 2)
		var failedEntries = await store.GetFailedEntriesAsync(
			maxRetries: 2,
			olderThan: null,
			batchSize: 100,
			CancellationToken.None).ConfigureAwait(false);

		var entriesList = failedEntries.ToList();
		var hasEntryWithExcessiveRetries = entriesList.Any(e => e.RetryCount > 2);

		if (hasEntryWithExcessiveRetries)
		{
			throw new TestFixtureAssertionException(
				"GetFailedEntriesAsync returned entries exceeding maxRetries");
		}
	}

	#endregion

	#region Query Tests

	/// <summary>
	/// Verifies that GetEntryAsync returns an existing entry.
	/// </summary>
	public virtual async Task GetEntryAsync_Existing_ShouldReturnEntry()
	{
		var store = CreateStore();
		var messageId = GenerateMessageId();
		var handlerType = GenerateHandlerType();
		var messageType = "TestMessageType";
		var payload = CreatePayload("Test payload");
		var metadata = CreateDefaultMetadata();

		_ = await store.CreateEntryAsync(
			messageId,
			handlerType,
			messageType,
			payload,
			metadata,
			CancellationToken.None).ConfigureAwait(false);

		var entry = await store.GetEntryAsync(messageId, handlerType, CancellationToken.None)
			.ConfigureAwait(false);

		if (entry is null)
		{
			throw new TestFixtureAssertionException("Expected inbox entry but got null");
		}

		if (entry.MessageId != messageId)
		{
			throw new TestFixtureAssertionException(
				$"MessageId mismatch: expected '{messageId}', got '{entry.MessageId}'");
		}
	}

	/// <summary>
	/// Verifies that GetEntryAsync returns null for non-existent entry.
	/// </summary>
	public virtual async Task GetEntryAsync_NonExistent_ShouldReturnNull()
	{
		var store = CreateStore();
		var messageId = GenerateMessageId();
		var handlerType = GenerateHandlerType();

		var entry = await store.GetEntryAsync(messageId, handlerType, CancellationToken.None)
			.ConfigureAwait(false);

		if (entry is not null)
		{
			throw new TestFixtureAssertionException(
				$"Expected null for non-existent entry but got entry with status {entry.Status}");
		}
	}

	/// <summary>
	/// Verifies that GetStatisticsAsync returns correct counts.
	/// </summary>
	public virtual async Task GetStatisticsAsync_ShouldReturnCorrectCounts()
	{
		var store = CreateStore();
		var handlerType = GenerateHandlerType();
		var messageType = "TestMessageType";
		var payload = CreatePayload("Test payload");
		var metadata = CreateDefaultMetadata();

		// Create received entry
		var receivedMsgId = GenerateMessageId();
		_ = await store.CreateEntryAsync(receivedMsgId, handlerType, messageType, payload, metadata, CancellationToken.None)
			.ConfigureAwait(false);

		// Create and process an entry
		var processedMsgId = GenerateMessageId();
		_ = await store.CreateEntryAsync(processedMsgId, handlerType, messageType, payload, metadata, CancellationToken.None)
			.ConfigureAwait(false);
		await store.MarkProcessedAsync(processedMsgId, handlerType, CancellationToken.None)
			.ConfigureAwait(false);

		// Create and fail an entry
		var failedMsgId = GenerateMessageId();
		_ = await store.CreateEntryAsync(failedMsgId, handlerType, messageType, payload, metadata, CancellationToken.None)
			.ConfigureAwait(false);
		await store.MarkFailedAsync(failedMsgId, handlerType, "Test error", CancellationToken.None)
			.ConfigureAwait(false);

		var stats = await store.GetStatisticsAsync(CancellationToken.None).ConfigureAwait(false);

		if (stats is null)
		{
			throw new TestFixtureAssertionException("Expected statistics but got null");
		}

		// We created 3 entries total in this test
		if (stats.TotalEntries < 3)
		{
			throw new TestFixtureAssertionException(
				$"Expected at least 3 total entries but got {stats.TotalEntries}");
		}

		if (stats.ProcessedEntries < 1)
		{
			throw new TestFixtureAssertionException(
				$"Expected at least 1 processed entry but got {stats.ProcessedEntries}");
		}

		if (stats.FailedEntries < 1)
		{
			throw new TestFixtureAssertionException(
				$"Expected at least 1 failed entry but got {stats.FailedEntries}");
		}
	}

	#endregion

	#region Cleanup Tests

	/// <summary>
	/// Verifies that CleanupAsync removes old processed entries.
	/// </summary>
	public virtual async Task CleanupAsync_OldProcessed_ShouldRemove()
	{
		var store = CreateStore();
		var messageId = GenerateMessageId();
		var handlerType = GenerateHandlerType();
		var messageType = "TestMessageType";
		var payload = CreatePayload("Test payload");
		var metadata = CreateDefaultMetadata();

		// Create and process an entry
		_ = await store.CreateEntryAsync(messageId, handlerType, messageType, payload, metadata, CancellationToken.None)
			.ConfigureAwait(false);
		await store.MarkProcessedAsync(messageId, handlerType, CancellationToken.None)
			.ConfigureAwait(false);

		// Cleanup with very short retention (0 seconds) - should remove processed entries.
		// Retry briefly to avoid timestamp boundary races when ProcessedAt ~= cutoff.
		var removedCount = 0;
		var deadline = DateTimeOffset.UtcNow.AddSeconds(2);
		var removedOrMissing = false;
		do
		{
			removedCount += await store.CleanupAsync(TimeSpan.Zero, CancellationToken.None).ConfigureAwait(false);
			var currentEntry = await store.GetEntryAsync(messageId, handlerType, CancellationToken.None).ConfigureAwait(false);
			if (currentEntry is null || removedCount > 0)
			{
				removedOrMissing = true;
				break;
			}

			await Task.Yield();
		}
		while (DateTimeOffset.UtcNow < deadline);

		// Either the entry should be removed, or removedCount should be >= 1
		if (!removedOrMissing && removedCount == 0)
		{
			throw new TestFixtureAssertionException(
				"Expected CleanupAsync to remove processed entries with zero retention");
		}
	}

	/// <summary>
	/// Verifies that CleanupAsync preserves recent entries.
	/// </summary>
	public virtual async Task CleanupAsync_ShouldPreserveRecent()
	{
		var store = CreateStore();
		var messageId = GenerateMessageId();
		var handlerType = GenerateHandlerType();
		var messageType = "TestMessageType";
		var payload = CreatePayload("Test payload");
		var metadata = CreateDefaultMetadata();

		// Create and process an entry
		_ = await store.CreateEntryAsync(messageId, handlerType, messageType, payload, metadata, CancellationToken.None)
			.ConfigureAwait(false);
		await store.MarkProcessedAsync(messageId, handlerType, CancellationToken.None)
			.ConfigureAwait(false);

		// Cleanup with long retention (1 hour) - should preserve recent entries
		_ = await store.CleanupAsync(TimeSpan.FromHours(1), CancellationToken.None)
			.ConfigureAwait(false);

		// Entry should still exist (was just created)
		var entry = await store.GetEntryAsync(messageId, handlerType, CancellationToken.None)
			.ConfigureAwait(false);

		if (entry is null)
		{
			throw new TestFixtureAssertionException(
				"Expected recent entry to be preserved but it was removed");
		}
	}

	#endregion

	#region Isolation Tests

	/// <summary>
	/// Verifies that entries are isolated by (messageId, handlerType) composite key.
	/// </summary>
	public virtual async Task Entries_ShouldIsolateByMessageIdAndHandlerType()
	{
		var store = CreateStore();
		var messageId1 = GenerateMessageId();
		var messageId2 = GenerateMessageId();
		var handlerType = GenerateHandlerType();
		var messageType = "TestMessageType";
		var payload = CreatePayload("Test payload");
		var metadata = CreateDefaultMetadata();

		_ = await store.CreateEntryAsync(messageId1, handlerType, messageType, payload, metadata, CancellationToken.None)
			.ConfigureAwait(false);
		_ = await store.CreateEntryAsync(messageId2, handlerType, messageType, payload, metadata, CancellationToken.None)
			.ConfigureAwait(false);

		// Process only messageId1
		await store.MarkProcessedAsync(messageId1, handlerType, CancellationToken.None)
			.ConfigureAwait(false);

		// messageId2 should still be in Received status
		var entry2 = await store.GetEntryAsync(messageId2, handlerType, CancellationToken.None)
			.ConfigureAwait(false);

		if (entry2 is null)
		{
			throw new TestFixtureAssertionException("Expected entry2 but got null");
		}

		if (entry2.Status != InboxStatus.Received)
		{
			throw new TestFixtureAssertionException(
				$"Expected entry2 status Received but got {entry2.Status}");
		}
	}

	/// <summary>
	/// Verifies that the same messageId can be processed by different handlers independently.
	/// </summary>
	public virtual async Task SameMessageId_DifferentHandlers_ShouldBeIndependent()
	{
		var store = CreateStore();
		var messageId = GenerateMessageId();
		var handlerType1 = GenerateHandlerType();
		var handlerType2 = GenerateHandlerType();
		var messageType = "TestMessageType";
		var payload = CreatePayload("Test payload");
		var metadata = CreateDefaultMetadata();

		// Create entries for same message but different handlers
		_ = await store.CreateEntryAsync(messageId, handlerType1, messageType, payload, metadata, CancellationToken.None)
			.ConfigureAwait(false);
		_ = await store.CreateEntryAsync(messageId, handlerType2, messageType, payload, metadata, CancellationToken.None)
			.ConfigureAwait(false);

		// Process only handler1
		await store.MarkProcessedAsync(messageId, handlerType1, CancellationToken.None)
			.ConfigureAwait(false);

		// handler2 should still be in Received status
		var entry1 = await store.GetEntryAsync(messageId, handlerType1, CancellationToken.None)
			.ConfigureAwait(false);
		var entry2 = await store.GetEntryAsync(messageId, handlerType2, CancellationToken.None)
			.ConfigureAwait(false);

		if (entry1 is null || entry1.Status != InboxStatus.Processed)
		{
			throw new TestFixtureAssertionException(
				$"Expected handler1 status Processed but got {entry1?.Status}");
		}

		if (entry2 is null || entry2.Status != InboxStatus.Received)
		{
			throw new TestFixtureAssertionException(
				$"Expected handler2 status Received but got {entry2?.Status}");
		}
	}

	#endregion

	#region Edge Cases

	/// <summary>
	/// Verifies that GetAllEntriesAsync returns all entries.
	/// </summary>
	public virtual async Task GetAllEntriesAsync_ShouldReturnAllEntries()
	{
		var store = CreateStore();
		var handlerType = GenerateHandlerType();
		var messageType = "TestMessageType";
		var payload = CreatePayload("Test payload");
		var metadata = CreateDefaultMetadata();

		// Create multiple entries
		var messageIds = new List<string>();
		for (var i = 0; i < 3; i++)
		{
			var msgId = GenerateMessageId();
			messageIds.Add(msgId);
			_ = await store.CreateEntryAsync(msgId, handlerType, messageType, payload, metadata, CancellationToken.None)
				.ConfigureAwait(false);
		}

		var allEntries = await store.GetAllEntriesAsync(CancellationToken.None).ConfigureAwait(false);
		var entriesList = allEntries.ToList();

		// Should contain at least our 3 entries
		var foundCount = messageIds.Count(msgId =>
			entriesList.Any(e => e.MessageId == msgId && e.HandlerType == handlerType));

		if (foundCount != 3)
		{
			throw new TestFixtureAssertionException(
				$"Expected to find all 3 created entries but found {foundCount}");
		}
	}

	#endregion
}
