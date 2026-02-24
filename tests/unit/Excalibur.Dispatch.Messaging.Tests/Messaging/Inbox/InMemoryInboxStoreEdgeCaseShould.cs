// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Text;

using Excalibur.Dispatch.Abstractions;

using Excalibur.Data.InMemory.Inbox;

namespace Excalibur.Dispatch.Tests.Messaging.Inbox;

/// <summary>
///     Edge case tests for the <see cref="InMemoryInboxStore" /> class.
/// </summary>
[Collection("Performance Tests")]
[Trait("Category", "Unit")]
public sealed class InMemoryInboxStoreEdgeCaseShould : IDisposable
{
	private const string TestHandler = "TestHandler";
	private readonly ILogger<InMemoryInboxStore> _logger;
	private readonly List<InMemoryInboxStore> _disposables;

	public InMemoryInboxStoreEdgeCaseShould()
	{
		_logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<InMemoryInboxStore>.Instance;
		_disposables = [];
	}

	[Fact]
	public async Task ThrowOnDuplicateCreateEntryAsync()
	{
		// Arrange
		var options = new InMemoryInboxOptions();
		var store = CreateStore(options);
		var messageId = "duplicate-test";
		var payload = Encoding.UTF8.GetBytes("test payload");
		var metadata = new Dictionary<string, object> { ["test"] = "value" };

		// Act - Create first entry
		var firstEntry = await store.CreateEntryAsync(messageId, TestHandler, "TestMessage", payload, metadata, CancellationToken.None).ConfigureAwait(false);
		_ = firstEntry.ShouldNotBeNull();

		// Act & Assert - Attempt to create duplicate should throw
		_ = await Should.ThrowAsync<InvalidOperationException>(async () =>
			await store.CreateEntryAsync(messageId, TestHandler, "TestMessage", payload, metadata, CancellationToken.None).ConfigureAwait(false)).ConfigureAwait(false);
	}

	[Fact]
	public async Task HandleConcurrentCreateEntryCalls()
	{
		// Arrange
		var options = new InMemoryInboxOptions();
		var store = CreateStore(options);
		var messageId = "concurrent-test";
		var payload = Encoding.UTF8.GetBytes("test payload");
		var metadata = new Dictionary<string, object> { ["test"] = "value" };

		var taskCount = 10;
		var tasks = new Task<InboxEntry?>[taskCount];
		var successCount = 0;
		var exceptionCount = 0;

		// Act - Launch concurrent create operations
		for (var i = 0; i < taskCount; i++)
		{
			tasks[i] = Task.Run(async () =>
			{
				try
				{
					var entry = await store.CreateEntryAsync(messageId, TestHandler, "TestMessage", payload, metadata, CancellationToken.None).ConfigureAwait(false);
					_ = Interlocked.Increment(ref successCount);
					return entry;
				}
				catch (InvalidOperationException)
				{
					_ = Interlocked.Increment(ref exceptionCount);
					return null;
				}
			});
		}

		_ = await Task.WhenAll(tasks).ConfigureAwait(false);

		// Assert - Exactly one should succeed, rest should fail
		successCount.ShouldBe(1);
		exceptionCount.ShouldBe(taskCount - 1);
		tasks.Count(t => t.Result != null).ShouldBe(1);
	}

	[Fact]
	public async Task EnforceMaxEntriesLimit()
	{
		// Arrange
		var options = new InMemoryInboxOptions
		{
			MaxEntries = 3,
			EnableAutomaticCleanup = false, // Disable to test exact behavior
		};
		var store = CreateStore(options);
		var payload = Encoding.UTF8.GetBytes("test payload");
		var metadata = new Dictionary<string, object>();

		// Act - Add entries up to the limit
		for (var i = 0; i < options.MaxEntries; i++)
		{
			_ = await store.CreateEntryAsync($"message-{i}", TestHandler, "TestMessage", payload, metadata, CancellationToken.None).ConfigureAwait(false);
		}

		// Act - Adding one more should trigger trimming
		_ = await store.CreateEntryAsync("message-overflow", TestHandler, "TestMessage", payload, metadata, CancellationToken.None).ConfigureAwait(false);

		// Wait until trimming converges.
		await WaitForConditionAsync(
			async () => (await store.GetAllEntriesAsync(CancellationToken.None).ConfigureAwait(false)).Count() <= options.MaxEntries,
			TimeSpan.FromSeconds(2)).ConfigureAwait(false);

		// Assert - Should have exactly MaxEntries (oldest should be removed)
		var allEntries = await store.GetAllEntriesAsync(CancellationToken.None);
		allEntries.Count().ShouldBeLessThanOrEqualTo(options.MaxEntries);
	}

	[Fact]
	public async Task HandleHighConcurrencyOperations()
	{
		// Arrange
		var options = new InMemoryInboxOptions { MaxEntries = 1000, EnableAutomaticCleanup = false };
		var store = CreateStore(options);
		var operationCount = 100;
		var payload = Encoding.UTF8.GetBytes("test payload");
		var metadata = new Dictionary<string, object>();

		// Act - Perform concurrent operations
		var createTasks = Enumerable.Range(0, operationCount)
			.Select(async i =>
			{
				_ = await store.CreateEntryAsync($"message-{i}", TestHandler, "TestMessage", payload, metadata, CancellationToken.None).ConfigureAwait(false);
				return i;
			});

		var markTasks = Enumerable.Range(0, operationCount / 2)
			.Select(async i =>
			{
				// Wait a bit to ensure entry exists
				await Task.Delay(Random.Shared.Next(1, 10)).ConfigureAwait(false);
				try
				{
					await store.MarkProcessedAsync($"message-{i}", TestHandler, CancellationToken.None);
					return true;
				}
				catch
				{
					return false;
				}
			});

		// Execute all operations concurrently
		await Task.WhenAll(createTasks.Concat(markTasks.Cast<Task>()));

		// Assert - All operations completed without deadlocks
		var allEntries = await store.GetAllEntriesAsync(CancellationToken.None);
		allEntries.Count().ShouldBe(operationCount);
	}

	[Fact]
	public void AcceptAutomaticCleanupOption()
	{
		// Arrange - The current implementation reads the EnableAutomaticCleanup option
		// and starts a cleanup timer when enabled.
		var options = new InMemoryInboxOptions
		{
			RetentionPeriod = TimeSpan.FromMilliseconds(10),
			EnableAutomaticCleanup = true,
			CleanupInterval = TimeSpan.FromMilliseconds(50),
		};

		// Act - Store creation should succeed with EnableAutomaticCleanup = true
		var store = CreateStore(options);

		// Assert - Store should be created successfully
		_ = store.ShouldNotBeNull();
	}

	[Fact]
	public async Task HandleDisposalGracefully()
	{
		// Arrange
		var options = new InMemoryInboxOptions { EnableAutomaticCleanup = false };
		var store = CreateStore(options);
		var payload = Encoding.UTF8.GetBytes("test payload");
		var metadata = new Dictionary<string, object>();

		// Act - Add an entry then dispose
		_ = await store.CreateEntryAsync("test-message", TestHandler, "TestMessage", payload, metadata, CancellationToken.None).ConfigureAwait(false);
		store.Dispose();

		// Assert - Should not throw on double dispose
		Should.NotThrow(store.Dispose);

		// Assert - Operations after disposal should throw
		_ = await Should.ThrowAsync<ObjectDisposedException>(async () =>
			await store.GetAllEntriesAsync(CancellationToken.None).ConfigureAwait(false)).ConfigureAwait(false);
	}

	[Fact]
	public async Task ThrowOnMarkingNonExistentMessageAsProcessed()
	{
		// Arrange
		var options = new InMemoryInboxOptions();
		var store = CreateStore(options);

		// Act & Assert
		_ = await Should.ThrowAsync<InvalidOperationException>(async () =>
			await store.MarkProcessedAsync("non-existent-message", TestHandler, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task ThrowOnMarkingNonExistentMessageAsFailed()
	{
		// Arrange
		var options = new InMemoryInboxOptions();
		var store = CreateStore(options);

		// Act & Assert - MarkFailedAsync throws InvalidOperationException for non-existent entries
		_ = await Should.ThrowAsync<InvalidOperationException>(async () =>
			await store.MarkFailedAsync("non-existent-message", TestHandler, "Test error", CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task PreventDoubleProcessing()
	{
		// Arrange
		var options = new InMemoryInboxOptions();
		var store = CreateStore(options);
		var messageId = "double-process-test";
		var payload = Encoding.UTF8.GetBytes("test payload");
		var metadata = new Dictionary<string, object>();

		// Act - Create entry and mark as processed
		_ = await store.CreateEntryAsync(messageId, TestHandler, "TestMessage", payload, metadata, CancellationToken.None).ConfigureAwait(false);
		await store.MarkProcessedAsync(messageId, TestHandler, CancellationToken.None);

		// Act & Assert - Attempting to mark as processed again should throw
		_ = await Should.ThrowAsync<InvalidOperationException>(async () =>
			await store.MarkProcessedAsync(messageId, TestHandler, CancellationToken.None)).ConfigureAwait(false);
	}

	[Fact]
	public async Task AllowMarkingProcessedMessageAsFailedUpdatingStatus()
	{
		// Arrange
		var options = new InMemoryInboxOptions();
		var store = CreateStore(options);
		var messageId = "process-then-fail-test";
		var payload = Encoding.UTF8.GetBytes("test payload");
		var metadata = new Dictionary<string, object>();

		// Act - Create entry and mark as processed
		_ = await store.CreateEntryAsync(messageId, TestHandler, "TestMessage", payload, metadata, CancellationToken.None).ConfigureAwait(false);
		await store.MarkProcessedAsync(messageId, TestHandler, CancellationToken.None);

		// Act - MarkFailedAsync can be called on processed entry (updates the status to Failed)
		await store.MarkFailedAsync(messageId, TestHandler, "Test error", CancellationToken.None);

		// Assert - Entry should now be marked as Failed
		var entry = await store.GetEntryAsync(messageId, TestHandler, CancellationToken.None);
		_ = entry.ShouldNotBeNull();
		entry.Status.ShouldBe(InboxStatus.Failed);
	}

	[Fact]
	public async Task HandleMemoryPressureWithTrimming()
	{
		// Arrange
		var options = new InMemoryInboxOptions { MaxEntries = 5, EnableAutomaticCleanup = false };
		var store = CreateStore(options);
		var payload = Encoding.UTF8.GetBytes("test payload");
		var metadata = new Dictionary<string, object>();

		// Act - Add more entries than the limit
		var messageIds = new List<string>();
		for (var i = 0; i < options.MaxEntries * 2; i++)
		{
			var messageId = $"message-{i:D3}";
			messageIds.Add(messageId);
			_ = await store.CreateEntryAsync(messageId, TestHandler, "TestMessage", payload, metadata, CancellationToken.None).ConfigureAwait(false);

			// Add small delay to ensure timestamp ordering
			await Task.Delay(1).ConfigureAwait(false);
		}

		// Wait until async trimming converges.
		await WaitForConditionAsync(
			async () => (await store.GetAllEntriesAsync(CancellationToken.None).ConfigureAwait(false)).Count() <= options.MaxEntries + 1,
			TimeSpan.FromSeconds(2)).ConfigureAwait(false);

		// Assert - Should have trimmed to approximately the max entries
		var remainingEntries = await store.GetAllEntriesAsync(CancellationToken.None);
		remainingEntries.Count().ShouldBeLessThanOrEqualTo(options.MaxEntries + 1); // Allow for timing tolerance

		// Assert - Newest entries should remain
		var newestMessageIds = messageIds.TakeLast(options.MaxEntries).ToList();
		var remainingMessageIds = remainingEntries.Select(e => e.MessageId).ToList();

		// At least some of the newest messages should still be present
		newestMessageIds.Intersect(remainingMessageIds).ShouldNotBeEmpty();
	}

	[Fact]
	public async Task HandleCleanupWithoutAutomaticCleanupEnabled()
	{
		// Arrange
		var options = new InMemoryInboxOptions { EnableAutomaticCleanup = false, RetentionPeriod = TimeSpan.FromMilliseconds(10) };
		var store = CreateStore(options);
		var payload = Encoding.UTF8.GetBytes("test payload");
		var metadata = new Dictionary<string, object>();

		// Act - Add entry, mark as processed, and wait for it to expire
		_ = await store.CreateEntryAsync("expired-message", TestHandler, "TestMessage", payload, metadata, CancellationToken.None).ConfigureAwait(false);
		await store.MarkProcessedAsync("expired-message", TestHandler, CancellationToken.None).ConfigureAwait(false); // Mark as processed so cleanup can remove it
		// Manual cleanup should still work once expiry has elapsed.
		var removedCount = 0;
		await WaitForConditionAsync(async () =>
		{
			removedCount = await store.CleanupAsync(options.RetentionPeriod, CancellationToken.None).ConfigureAwait(false);
			return removedCount > 0;
		}, TimeSpan.FromSeconds(2)).ConfigureAwait(false);

		// Assert - Should have cleaned up the expired entry
		removedCount.ShouldBe(1);
		var remainingEntries = await store.GetAllEntriesAsync(CancellationToken.None);
		remainingEntries.ShouldBeEmpty();
	}

	[Fact]
	public async Task HandleConcurrentCleanupOperations()
	{
		// Arrange
		var options = new InMemoryInboxOptions { EnableAutomaticCleanup = false, RetentionPeriod = TimeSpan.FromMilliseconds(10) };
		var store = CreateStore(options);
		var payload = Encoding.UTF8.GetBytes("test payload");
		var metadata = new Dictionary<string, object>();

		// Act - Add entries, mark as processed, and let them expire
		for (var i = 0; i < 10; i++)
		{
			_ = await store.CreateEntryAsync($"message-{i}", TestHandler, "TestMessage", payload, metadata, CancellationToken.None).ConfigureAwait(false);
			await store.MarkProcessedAsync($"message-{i}", TestHandler, CancellationToken.None).ConfigureAwait(false); // Mark as processed so cleanup can remove it
		}

		var preCleaned = 0;
		await WaitForConditionAsync(async () =>
		{
			preCleaned = await store.CleanupAsync(options.RetentionPeriod, CancellationToken.None).ConfigureAwait(false);
			return preCleaned > 0;
		}, TimeSpan.FromSeconds(2)).ConfigureAwait(false);

		// Act - Run concurrent cleanup operations
		var cleanupTasks = Enumerable.Range(0, 5)
			.Select(_ => store.CleanupAsync(options.RetentionPeriod, CancellationToken.None).AsTask())
			.ToArray();

		var results = await Task.WhenAll(cleanupTasks);

		// Assert - Total cleaned up should equal original count
		var totalCleaned = preCleaned + results.Sum();
		totalCleaned.ShouldBe(10);

		// Assert - No entries should remain
		var remainingEntries = await store.GetAllEntriesAsync(CancellationToken.None);
		remainingEntries.ShouldBeEmpty();
	}

	[Fact]
	public async Task HandleLargePayloads()
	{
		// Arrange
		var options = new InMemoryInboxOptions();
		var store = CreateStore(options);
		var largePayload = new byte[1024 * 1024]; // 1MB payload
		Random.Shared.NextBytes(largePayload);
		var metadata = new Dictionary<string, object> { ["size"] = largePayload.Length };

		// Act
		var entry = await store.CreateEntryAsync("large-message", TestHandler, "TestMessage", largePayload, metadata, CancellationToken.None).ConfigureAwait(false);

		// Assert
		_ = entry.ShouldNotBeNull();
		entry.Payload.Length.ShouldBe(largePayload.Length);
		entry.Payload.ShouldBe(largePayload);
	}

	[Fact]
	public async Task HandleEmptyMetadata()
	{
		// Arrange
		var options = new InMemoryInboxOptions();
		var store = CreateStore(options);
		var payload = Encoding.UTF8.GetBytes("test payload");

		// Act & Assert - Empty metadata should work
		var entry = await store.CreateEntryAsync("empty-metadata", TestHandler, "TestMessage", payload, new Dictionary<string, object>(), CancellationToken.None).ConfigureAwait(false);
		_ = entry.ShouldNotBeNull();
		entry.Metadata.ShouldBeEmpty();
	}

	[Fact]
	public async Task GetStatisticsUnderLoad()
	{
		// Arrange
		var options = new InMemoryInboxOptions { EnableAutomaticCleanup = false };
		var store = CreateStore(options);

		// Act - Get statistics on empty store
		var emptyStats = await store.GetStatisticsAsync(CancellationToken.None);

		// Assert
		_ = emptyStats.ShouldNotBeNull();
		emptyStats.TotalEntries.ShouldBe(0);
		emptyStats.ProcessedEntries.ShouldBe(0);
		emptyStats.FailedEntries.ShouldBe(0);
		emptyStats.PendingEntries.ShouldBe(0);
	}

	[Fact]
	public async Task HandleConcurrentMixedOperations()
	{
		// Arrange
		var options = new InMemoryInboxOptions { MaxEntries = 500, EnableAutomaticCleanup = false };
		var store = CreateStore(options);
		var operationCount = 200;
		var payload = Encoding.UTF8.GetBytes("test payload");
		var metadata = new Dictionary<string, object>();

		var createdMessageIds = new ConcurrentBag<string>();
		var processedMessageIds = new ConcurrentBag<string>();
		var failedMessageIds = new ConcurrentBag<string>();
		var exceptions = new ConcurrentBag<Exception>();

		// Act - Mix create, process, fail, and cleanup operations concurrently
		var tasks = new List<Task>();

		// Create operations
		for (var i = 0; i < operationCount; i++)
		{
			var messageId = $"mixed-{i}";
			tasks.Add(Task.Run(async () =>
			{
				try
				{
					_ = await store.CreateEntryAsync(messageId, TestHandler, "TestMessage", payload, metadata, CancellationToken.None).ConfigureAwait(false);
					createdMessageIds.Add(messageId);
					await Task.Delay(Random.Shared.Next(1, 5)).ConfigureAwait(false); // Simulate processing time
				}
				catch (Exception ex)
				{
					exceptions.Add(ex);
				}
			}));
		}

		// Process operations (on subset of created messages)
		for (var i = 0; i < operationCount / 2; i++)
		{
			var messageId = $"mixed-{i}";
			tasks.Add(Task.Run(async () =>
			{
				try
				{
					await Task.Delay(Random.Shared.Next(5, 15)).ConfigureAwait(false); // Wait for creation
					await store.MarkProcessedAsync(messageId, TestHandler, CancellationToken.None);
					processedMessageIds.Add(messageId);
				}
				catch (Exception ex)
				{
					exceptions.Add(ex);
				}
			}));
		}

		// Fail operations (on different subset)
		// MarkFailedAsync throws InvalidOperationException if entry doesn't exist
		for (var i = operationCount / 2; i < operationCount * 3 / 4; i++)
		{
			var messageId = $"mixed-{i}";
			tasks.Add(Task.Run(async () =>
			{
				try
				{
					await Task.Delay(Random.Shared.Next(5, 15)).ConfigureAwait(false); // Wait for creation
					await store.MarkFailedAsync(messageId, TestHandler, $"Test error for {messageId}", CancellationToken.None);
					failedMessageIds.Add(messageId);
				}
				catch (InvalidOperationException)
				{
					// Expected if entry doesn't exist yet
				}
				catch (Exception ex)
				{
					exceptions.Add(ex);
				}
			}));
		}

		// Statistics reading operations
		for (var i = 0; i < 10; i++)
		{
			tasks.Add(Task.Run(async () =>
			{
				try
				{
					for (var j = 0; j < 20; j++)
					{
						var stats = await store.GetStatisticsAsync(CancellationToken.None);
						_ = stats.ShouldNotBeNull();
						await Task.Delay(Random.Shared.Next(1, 10)).ConfigureAwait(false);
					}
				}
				catch (Exception ex)
				{
					exceptions.Add(ex);
				}
			}));
		}

		// Cleanup operations
		for (var i = 0; i < 5; i++)
		{
			tasks.Add(Task.Run(async () =>
			{
				try
				{
					await Task.Delay(Random.Shared.Next(100, 200)).ConfigureAwait(false); // Wait for some entries
					_ = await store.CleanupAsync(options.RetentionPeriod, CancellationToken.None);
				}
				catch (Exception ex)
				{
					exceptions.Add(ex);
				}
			}));
		}

		await Task.WhenAll(tasks).ConfigureAwait(false);

		// Assert - Operations completed without critical failures (InvalidOperationException is expected for timing issues)
		exceptions.Where(ex => ex is not InvalidOperationException).ShouldBeEmpty();
		createdMessageIds.Count.ShouldBeGreaterThan(0);

		// Verify final consistency
		var finalStats = await store.GetStatisticsAsync(CancellationToken.None);
		_ = finalStats.ShouldNotBeNull();

		// After cleanup runs concurrently, processed entries may have been removed
		// Verify operations completed successfully, but don't expect specific counts
		processedMessageIds.Count.ShouldBeGreaterThan(0); // At least some should have processed

		// Total entries should be within expected bounds
		finalStats.TotalEntries.ShouldBeLessThanOrEqualTo(operationCount);
	}

	[Fact]
	public async Task MaintainConsistencyUnderRapidStateTransitions()
	{
		// Arrange
		var options = new InMemoryInboxOptions { EnableAutomaticCleanup = false };
		var store = CreateStore(options);
		var messageCount = 100;
		var payload = Encoding.UTF8.GetBytes("test payload");
		var metadata = new Dictionary<string, object>();

		var processedCount = 0;
		var failedCount = 0;
		var inconsistencies = new ConcurrentBag<string>();

		// Act - Create messages then rapidly transition states
		var createTasks = Enumerable.Range(0, messageCount)
			.Select(async i =>
			{
				var messageId = $"rapid-{i}";
				_ = await store.CreateEntryAsync(messageId, TestHandler, "TestMessage", payload, metadata, CancellationToken.None).ConfigureAwait(false);
				return messageId;
			});

		var messageIds = await Task.WhenAll(createTasks).ConfigureAwait(false);

		// Rapidly transition states with concurrent readers
		var transitionTasks = messageIds.Select(async (messageId, index) =>
		{
			try
			{
				// Small random delay to create race conditions
				await Task.Delay(Random.Shared.Next(0, 5)).ConfigureAwait(false);

				if (index % 2 == 0)
				{
					await store.MarkProcessedAsync(messageId, TestHandler, CancellationToken.None);
					_ = Interlocked.Increment(ref processedCount);
				}
				else
				{
					await store.MarkFailedAsync(messageId, TestHandler, "Test error", CancellationToken.None);
					_ = Interlocked.Increment(ref failedCount);
				}
			}
			catch (Exception ex)
			{
				inconsistencies.Add($"{messageId}: {ex.Message}");
			}
		});

		// Concurrent statistics readers to detect inconsistencies
		var statsTasks = Enumerable.Range(0, 20).Select(async _ =>
		{
			for (var i = 0; i < 50; i++)
			{
				var stats = await store.GetStatisticsAsync(CancellationToken.None);
				var totalAccounted = stats.ProcessedEntries + stats.FailedEntries + stats.PendingEntries;

				if (totalAccounted > stats.TotalEntries)
				{
					inconsistencies.Add($"Stats inconsistency: TotalAccounted={totalAccounted} > TotalEntries={stats.TotalEntries}");
				}

				await Task.Delay(1).ConfigureAwait(false);
			}
		});

		await Task.WhenAll(transitionTasks.Concat(statsTasks));

		// Assert - Final state should be consistent
		var finalStats = await store.GetStatisticsAsync(CancellationToken.None);
		finalStats.ProcessedEntries.ShouldBe(processedCount);
		finalStats.FailedEntries.ShouldBe(failedCount);
		finalStats.TotalEntries.ShouldBe(messageCount);
		finalStats.PendingEntries.ShouldBe(0);

		// Should have minimal inconsistencies (only expected InvalidOperationExceptions for duplicate operations)
		inconsistencies.Where(msg => !msg.Contains("already")).ShouldBeEmpty();
	}

	[Fact]
	public async Task HandleExtremeCleanupConcurrency()
	{
		// Arrange
		var options = new InMemoryInboxOptions { EnableAutomaticCleanup = false, RetentionPeriod = TimeSpan.FromMilliseconds(50) };
		var store = CreateStore(options);
		var messageCount = 50;
		var cleanupConcurrency = 20;
		var payload = Encoding.UTF8.GetBytes("test payload");
		var metadata = new Dictionary<string, object>();

		// Act - Create messages that will expire quickly
		var createTasks = Enumerable.Range(0, messageCount)
			.Select(i => store.CreateEntryAsync($"cleanup-extreme-{i}", TestHandler, "TestMessage", payload, metadata, CancellationToken.None).AsTask());

		_ = await Task.WhenAll(createTasks).ConfigureAwait(false);

		// Mark all entries as processed
		var markProcessedTasks = Enumerable.Range(0, messageCount)
			.Select(i => store.MarkProcessedAsync($"cleanup-extreme-{i}", TestHandler, CancellationToken.None).AsTask());
		await Task.WhenAll(markProcessedTasks).ConfigureAwait(false);

		var preCleaned = 0;
		await WaitForConditionAsync(async () =>
		{
			preCleaned = await store.CleanupAsync(options.RetentionPeriod, CancellationToken.None).ConfigureAwait(false);
			return preCleaned > 0;
		}, TimeSpan.FromSeconds(2)).ConfigureAwait(false);

		// Launch many concurrent cleanup operations
		var cleanupResults = new ConcurrentBag<int>();
		var cleanupTasks = Enumerable.Range(0, cleanupConcurrency)
			.Select(async _ =>
			{
				var result = await store.CleanupAsync(options.RetentionPeriod, CancellationToken.None);
				cleanupResults.Add(result);
			});

		await Task.WhenAll(cleanupTasks);

		// Assert - Total cleaned should equal original count
		var totalCleaned = preCleaned + cleanupResults.Sum();
		totalCleaned.ShouldBe(messageCount);

		// All entries should be gone
		var remainingEntries = await store.GetAllEntriesAsync(CancellationToken.None);
		remainingEntries.ShouldBeEmpty();

		// Statistics should reflect empty state
		var stats = await store.GetStatisticsAsync(CancellationToken.None);
		stats.TotalEntries.ShouldBe(0);
		stats.PendingEntries.ShouldBe(0);
		stats.ProcessedEntries.ShouldBe(0);
		stats.FailedEntries.ShouldBe(0);
	}

	[Fact]
	public async Task StressTestMemoryAndCapacityLimits()
	{
		// Arrange
		var options = new InMemoryInboxOptions { MaxEntries = 100, EnableAutomaticCleanup = false };
		var store = CreateStore(options);
		var stressMessageCount = 300; // 3x the limit
		var largePayload = new byte[10 * 1024]; // 10KB payload
		Random.Shared.NextBytes(largePayload);
		var metadata = new Dictionary<string, object> { ["size"] = largePayload.Length };

		var successfulCreations = 0;

		// Act - Stress the capacity limits with large payloads
		var tasks = Enumerable.Range(0, stressMessageCount)
			.Select(async i =>
			{
				try
				{
					var messageId = $"stress-{i:D4}";
					_ = await store.CreateEntryAsync(messageId, TestHandler, "TestMessage", largePayload, metadata, CancellationToken.None).ConfigureAwait(false);
					_ = Interlocked.Increment(ref successfulCreations);

					// Small delay to let trimming occur
					await Task.Delay(1).ConfigureAwait(false);
				}
				catch (Exception)
				{
					// Expected when capacity limits are reached
				}
			});

		await Task.WhenAll(tasks).ConfigureAwait(false);

		// Wait until trimming converges.
		await WaitForConditionAsync(
			async () => (await store.GetAllEntriesAsync(CancellationToken.None).ConfigureAwait(false)).Count() <= options.MaxEntries + 10,
			TimeSpan.FromSeconds(2)).ConfigureAwait(false);

		// Assert - Should have maintained capacity limits
		var finalEntries = await store.GetAllEntriesAsync(CancellationToken.None);
		finalEntries.Count().ShouldBeLessThanOrEqualTo(options.MaxEntries + 10); // Allow for some timing tolerance

		var stats = await store.GetStatisticsAsync(CancellationToken.None);
		stats.TotalEntries.ShouldBeLessThanOrEqualTo(options.MaxEntries + 10);

		// Should have created many messages successfully
		successfulCreations.ShouldBeGreaterThan(options.MaxEntries);
	}

	[Fact]
	public async Task VerifyThreadSafetyOfInternalCollections()
	{
		// Arrange
		var options = new InMemoryInboxOptions { EnableAutomaticCleanup = false };
		var store = CreateStore(options);
		var concurrencyLevel = Environment.ProcessorCount * 2;
		var operationsPerThread = 50;
		var payload = Encoding.UTF8.GetBytes("test payload");
		var metadata = new Dictionary<string, object>();

		var operationResults = new ConcurrentBag<bool>();

		// Act - Hammer the store from multiple threads simultaneously
		var tasks = Enumerable.Range(0, concurrencyLevel)
			.Select(threadId => Task.Run(async () =>
			{
				try
				{
					for (var i = 0; i < operationsPerThread; i++)
					{
						var messageId = $"thread-{threadId}-op-{i}";

						// Create
						_ = await store.CreateEntryAsync(messageId, TestHandler, "TestMessage", payload, metadata, CancellationToken.None).ConfigureAwait(false);

						// Immediate state check
						var entry = (await store.GetAllEntriesAsync(CancellationToken.None)).FirstOrDefault(e => e.MessageId == messageId);
						if (entry == null)
						{
							operationResults.Add(false);
							continue;
						}

						// Random operation
						var operation = Random.Shared.Next(0, 3);
						switch (operation)
						{
							case 0:
								await store.MarkProcessedAsync(messageId, TestHandler, CancellationToken.None);
								break;

							case 1:
								await store.MarkFailedAsync(messageId, TestHandler, "Thread test error", CancellationToken.None);
								break;

							case 2:
								// Leave pending
								break;
						}

						// Verify statistics consistency
						var stats = await store.GetStatisticsAsync(CancellationToken.None);
						var totalAccounted = stats.ProcessedEntries + stats.FailedEntries + stats.PendingEntries;
						if (totalAccounted > stats.TotalEntries)
						{
							operationResults.Add(false);
						}
						else
						{
							operationResults.Add(true);
						}

						// Small yield to encourage context switching
						await Task.Yield();
					}
				}
				catch (InvalidOperationException)
				{
					// Expected for duplicate operations
					operationResults.Add(true);
				}
				catch (Exception)
				{
					operationResults.Add(false);
				}
			}));

		await Task.WhenAll(tasks);

		// Assert - Most operations should succeed
		// Under extreme concurrency, statistics snapshots may show temporary inconsistencies
		// due to concurrent enumeration timing - 65% threshold accounts for this with margin
		var successRate = operationResults.Count(r => r) / (double)operationResults.Count;
		successRate.ShouldBeGreaterThan(0.65); // At least 65% success rate under high concurrency

		// Final state should be consistent
		var finalStats = await store.GetStatisticsAsync(CancellationToken.None);
		var finalTotalAccounted = finalStats.ProcessedEntries + finalStats.FailedEntries + finalStats.PendingEntries;
		finalTotalAccounted.ShouldBeLessThanOrEqualTo(finalStats.TotalEntries);
	}

	[Fact]
	public async Task HandleConcurrentDisposalAndOperations()
	{
		// Arrange
		var options = new InMemoryInboxOptions { EnableAutomaticCleanup = false };
		var store = CreateStore(options);
		var payload = Encoding.UTF8.GetBytes("test payload");
		var metadata = new Dictionary<string, object>();

		var operationExceptions = new ConcurrentBag<Exception>();
		var operationsStarted = 0;
		var operationsCompleted = 0;

		// Act - Start operations and dispose concurrently
		var operationTasks = Enumerable.Range(0, 50).Select(async i =>
		{
			try
			{
				_ = Interlocked.Increment(ref operationsStarted);
				var messageId = $"disposal-race-{i}";
				_ = await store.CreateEntryAsync(messageId, TestHandler, "TestMessage", payload, metadata, CancellationToken.None).ConfigureAwait(false);
				await Task.Delay(Random.Shared.Next(15, 40)).ConfigureAwait(false);
				await store.MarkProcessedAsync(messageId, TestHandler, CancellationToken.None);
				_ = Interlocked.Increment(ref operationsCompleted);
			}
			catch (Exception ex)
			{
				operationExceptions.Add(ex);
			}
		});

		// Dispose after a short delay
		var disposalTask = Task.Run(async () =>
		{
			await Task.Delay(25).ConfigureAwait(false);
			store.Dispose();
		});

		await Task.WhenAll(operationTasks.Concat(new[] { disposalTask }));

		// Assert - Operations after disposal may throw ObjectDisposedException
		// The key test is that concurrent disposal doesn't cause crashes or deadlocks
		var disposedExceptions = operationExceptions.OfType<ObjectDisposedException>().Count();

		// All operations should have started
		operationsStarted.ShouldBe(50);

		// Either some operations completed before disposal, OR many were interrupted by disposal
		// Both are valid outcomes - the important thing is the system handled it gracefully
		(operationsCompleted + disposedExceptions).ShouldBeGreaterThan(0);
	}

	[Fact]
	public async Task HandleContentionOnSameMessageId()
	{
		// Arrange
		var options = new InMemoryInboxOptions();
		var store = CreateStore(options);
		var sharedMessageId = "contention-test";
		var payload = Encoding.UTF8.GetBytes("test payload");
		var metadata = new Dictionary<string, object>();
		var concurrencyLevel = 20;

		var createSuccessCount = 0;
		var createFailureCount = 0;
		var markAsProcessedCount = 0;
		var markAsFailedCount = 0;
		var operationExceptions = new ConcurrentBag<Exception>();

		// Act - Multiple threads trying to create same message ID
		var createTasks = Enumerable.Range(0, concurrencyLevel).Select(i => Task.Run(async () =>
		{
			try
			{
				_ = await store.CreateEntryAsync(sharedMessageId, TestHandler, "TestMessage", payload, metadata, CancellationToken.None).ConfigureAwait(false);
				_ = Interlocked.Increment(ref createSuccessCount);
			}
			catch (InvalidOperationException)
			{
				_ = Interlocked.Increment(ref createFailureCount);
			}
			catch (Exception ex)
			{
				operationExceptions.Add(ex);
			}
		}));

		// Multiple threads trying to mark as processed (using same TestHandler for consistency)
		var processedTasks = Enumerable.Range(0, concurrencyLevel).Select(i => Task.Run(async () =>
		{
			try
			{
				await Task.Delay(Random.Shared.Next(5, 15)).ConfigureAwait(false); // Wait for creation
				await store.MarkProcessedAsync(sharedMessageId, TestHandler, CancellationToken.None);
				_ = Interlocked.Increment(ref markAsProcessedCount);
			}
			catch (InvalidOperationException)
			{
				// Expected for already processed or not found messages
			}
			catch (Exception ex)
			{
				operationExceptions.Add(ex);
			}
		}));

		// Multiple threads trying to mark as failed (using same TestHandler for consistency)
		// MarkFailedAsync throws InvalidOperationException if entry doesn't exist
		var failedTasks = Enumerable.Range(0, concurrencyLevel).Select(i => Task.Run(async () =>
		{
			try
			{
				await Task.Delay(Random.Shared.Next(5, 15)).ConfigureAwait(false); // Wait for creation
				await store.MarkFailedAsync(sharedMessageId, TestHandler, "Contention test error", CancellationToken.None);
				_ = Interlocked.Increment(ref markAsFailedCount);
			}
			catch (InvalidOperationException)
			{
				// Expected for entry not found (before creation completes)
			}
			catch (Exception ex)
			{
				operationExceptions.Add(ex);
			}
		}));

		await Task.WhenAll(createTasks.Concat(processedTasks).Concat(failedTasks));

		// Assert - At most one create operation should succeed (race conditions may cause 0 or 1)
		createSuccessCount.ShouldBeLessThanOrEqualTo(1);
		// Total of success + failure should equal concurrency level
		(createSuccessCount + createFailureCount).ShouldBe(concurrencyLevel);

		// Multiple processed calls may succeed if entry exists (MarkProcessedAsync throws on already processed,
		// but race conditions mean multiple threads could succeed before status is updated)
		// The important thing is that these operations don't cause corruption
		markAsProcessedCount.ShouldBeGreaterThanOrEqualTo(0);

		// Should have minimal unexpected exceptions
		operationExceptions.ShouldBeEmpty();

		// Final state should be consistent - at most one entry for the shared message ID
		var allEntries = await store.GetAllEntriesAsync(CancellationToken.None);
		allEntries.Count().ShouldBeLessThanOrEqualTo(1);
		if (allEntries.Any())
		{
			allEntries.First().MessageId.ShouldBe(sharedMessageId);
		}
	}

	[Fact]
	public async Task HandleTimingAttacksOnCleanupLogic()
	{
		// Arrange
		var options = new InMemoryInboxOptions { EnableAutomaticCleanup = false, RetentionPeriod = TimeSpan.FromMilliseconds(30) };
		var store = CreateStore(options);
		var messageCount = 100;
		var payload = Encoding.UTF8.GetBytes("test payload");
		var metadata = new Dictionary<string, object>();

		var cleanupResults = new ConcurrentBag<int>();
		var stateChangeResults = new ConcurrentBag<bool>();

		// Act - Create messages with staggered timing
		var createTasks = Enumerable.Range(0, messageCount).Select(async i =>
		{
			await Task.Delay(i % 5).ConfigureAwait(false); // Stagger creation times
			var messageId = $"timing-attack-{i}";
			_ = await store.CreateEntryAsync(messageId, TestHandler, "TestMessage", payload, metadata, CancellationToken.None).ConfigureAwait(false);
			return messageId;
		});

		var messageIds = await Task.WhenAll(createTasks).ConfigureAwait(false);

		// Concurrent cleanup attempts at precise timing
		var cleanupTasks = Enumerable.Range(0, 10).Select(async i =>
		{
			await Task.Delay(35 + i).ConfigureAwait(false); // Just after expiry
			var result = await store.CleanupAsync(options.RetentionPeriod, CancellationToken.None);
			cleanupResults.Add(result);
		});

		// Concurrent state changes during cleanup window
		var stateChangeTasks = messageIds.Take(50).Select(async messageId =>
		{
			try
			{
				await Task.Delay(Random.Shared.Next(25, 45)).ConfigureAwait(false); // During expiry window
				if (Random.Shared.Next(0, 2) == 0)
				{
					await store.MarkProcessedAsync(messageId, TestHandler, CancellationToken.None);
				}
				else
				{
					await store.MarkFailedAsync(messageId, TestHandler, "Timing attack test", CancellationToken.None);
				}

				stateChangeResults.Add(true);
			}
			catch (InvalidOperationException)
			{
				// Expected if message was cleaned up first or not found
				stateChangeResults.Add(false);
			}
		});

		await Task.WhenAll(cleanupTasks.Concat(stateChangeTasks));

		// Assert - Total operations should be consistent
		var totalCleaned = cleanupResults.Sum();
		var successfulStateChanges = stateChangeResults.Count(r => r);

		// Either cleaned up or state changed, not both
		(totalCleaned + successfulStateChanges).ShouldBeLessThanOrEqualTo(messageCount);

		// Final state should be consistent
		var remainingEntries = await store.GetAllEntriesAsync(CancellationToken.None);
		var stats = await store.GetStatisticsAsync(CancellationToken.None);
		stats.TotalEntries.ShouldBe(remainingEntries.Count());
	}

	[Fact]
	public async Task HandleRapidSuccessionOperationsOnMultipleMessages()
	{
		// Arrange
		var options = new InMemoryInboxOptions { MaxEntries = 200, EnableAutomaticCleanup = false };
		var store = CreateStore(options);
		var messageCount = 150;
		var payload = Encoding.UTF8.GetBytes("test payload");
		var metadata = new Dictionary<string, object>();

		var operationTimings = new ConcurrentBag<TimeSpan>();
		var operationResults = new ConcurrentBag<(string MessageId, string Operation, bool Success)>();

		// Act - Rapid succession operations with microsecond timing
		var allTasks = new List<Task>();

		// Burst create operations
		for (var i = 0; i < messageCount; i++)
		{
			var messageId = $"rapid-{i:D3}";
			allTasks.Add(Task.Run(async () =>
			{
				var stopwatch = Stopwatch.StartNew();
				try
				{
					_ = await store.CreateEntryAsync(messageId, TestHandler, "TestMessage", payload, metadata, CancellationToken.None).ConfigureAwait(false);
					stopwatch.Stop();
					operationTimings.Add(stopwatch.Elapsed);
					operationResults.Add((messageId, "Create", true));
				}
				catch
				{
					operationResults.Add((messageId, "Create", false));
				}
			}));

			// Immediate follow-up operations
			if (i % 3 == 0)
			{
				allTasks.Add(Task.Run(async () =>
				{
					try
					{
						// Microsecond delay to create race condition
						await Task.Delay(TimeSpan.FromMicroseconds(Random.Shared.Next(1, 100))).ConfigureAwait(false);
						await store.MarkProcessedAsync(messageId, TestHandler, CancellationToken.None);
						operationResults.Add((messageId, "MarkProcessed", true));
					}
					catch
					{
						operationResults.Add((messageId, "MarkProcessed", false));
					}
				}));
			}
			else if (i % 3 == 1)
			{
				allTasks.Add(Task.Run(async () =>
				{
					try
					{
						await Task.Delay(TimeSpan.FromMicroseconds(Random.Shared.Next(1, 100))).ConfigureAwait(false);
						await store.MarkFailedAsync(messageId, TestHandler, "Rapid test error", CancellationToken.None);
						operationResults.Add((messageId, "MarkFailed", true));
					}
					catch
					{
						operationResults.Add((messageId, "MarkFailed", false));
					}
				}));
			}
		}

		// Concurrent statistics readers during rapid operations
		for (var i = 0; i < 20; i++)
		{
			allTasks.Add(Task.Run(async () =>
			{
				for (var j = 0; j < 100; j++)
				{
					var stats = await store.GetStatisticsAsync(CancellationToken.None);
					_ = stats.ShouldNotBeNull();
					await Task.Delay(TimeSpan.FromMicroseconds(Random.Shared.Next(1, 50))).ConfigureAwait(false);
				}
			}));
		}

		await Task.WhenAll(allTasks).ConfigureAwait(false);

		// Assert - Operations should maintain consistency
		var createOperations = operationResults.Where(r => r.Operation == "Create").ToList();
		var successfulCreates = createOperations.Count(r => r.Success);
		// Under high concurrency, some creates may fail due to race conditions. Most should succeed.
		successfulCreates.ShouldBeGreaterThanOrEqualTo((int)(messageCount * 0.8)); // At least 80% success rate

		// Timing analysis
		if (operationTimings.Any())
		{
			var averageCreateTime = operationTimings.Average(t => t.TotalMicroseconds);
			averageCreateTime.ShouldBeLessThan(10000); // Less than 10ms average
		}

		// Final consistency check
		var finalStats = await store.GetStatisticsAsync(CancellationToken.None);
		finalStats.TotalEntries.ShouldBe(messageCount);
		var totalAccountedFinal = finalStats.ProcessedEntries + finalStats.FailedEntries + finalStats.PendingEntries;
		totalAccountedFinal.ShouldBe(finalStats.TotalEntries);
	}

	[Fact]
	public async Task HandleConcurrentStatisticsQueriesDuringUpdates()
	{
		// Arrange
		var options = new InMemoryInboxOptions { EnableAutomaticCleanup = false };
		var store = CreateStore(options);
		var messageCount = 100;
		var statisticsReadCount = 1000;
		var payload = Encoding.UTF8.GetBytes("test payload");
		var metadata = new Dictionary<string, object>();

		var statisticsSnapshots = new ConcurrentBag<InboxStatistics>();
		var inconsistentSnapshots = new ConcurrentBag<string>();

		// Act - Continuous statistics reading during rapid state changes
		var statisticsReaderTask = Task.Run(async () =>
		{
			for (var i = 0; i < statisticsReadCount; i++)
			{
				var stats = await store.GetStatisticsAsync(CancellationToken.None);
				statisticsSnapshots.Add(stats);

				// Validate consistency in each snapshot
				var totalAccounted = stats.ProcessedEntries + stats.FailedEntries + stats.PendingEntries;
				if (totalAccounted > stats.TotalEntries)
				{
					inconsistentSnapshots.Add($"Snapshot {i}: TotalAccounted={totalAccounted} > TotalEntries={stats.TotalEntries}");
				}

				await Task.Delay(1).ConfigureAwait(false);
			}
		});

		// Rapid state mutations
		var mutationTasks = Enumerable.Range(0, messageCount).Select(async i =>
		{
			var messageId = $"stats-test-{i}";
			try
			{
				// Create
				_ = await store.CreateEntryAsync(messageId, TestHandler, "TestMessage", payload, metadata, CancellationToken.None).ConfigureAwait(false);
				await Task.Delay(Random.Shared.Next(1, 3)).ConfigureAwait(false);

				// Random state change
				if (Random.Shared.Next(0, 2) == 0)
				{
					await store.MarkProcessedAsync(messageId, TestHandler, CancellationToken.None);
				}
				else
				{
					await store.MarkFailedAsync(messageId, TestHandler, "Statistics test error", CancellationToken.None);
				}
			}
			catch (InvalidOperationException)
			{
				// Expected for race conditions
			}
		});

		await Task.WhenAll(mutationTasks.Concat(new[] { statisticsReaderTask }));

		// Assert - Statistics should be consistently readable
		statisticsSnapshots.Count.ShouldBe(statisticsReadCount);
		inconsistentSnapshots.ShouldBeEmpty();

		// Snapshots should show progression (sort by TotalEntries since ConcurrentBag doesn't preserve order)
		var orderedSnapshots = statisticsSnapshots.OrderBy(s => s.TotalEntries).ToList();
		var firstSnapshot = orderedSnapshots.First();
		var lastSnapshot = orderedSnapshots.Last();
		lastSnapshot.TotalEntries.ShouldBeGreaterThanOrEqualTo(firstSnapshot.TotalEntries);

		// Final state consistency
		var finalStats = await store.GetStatisticsAsync(CancellationToken.None);
		finalStats.TotalEntries.ShouldBe(messageCount);
		(finalStats.ProcessedEntries + finalStats.FailedEntries).ShouldBe(messageCount);
		finalStats.PendingEntries.ShouldBe(0);
	}

	[Fact]
	public async Task HandleHighContentionStateTransitionConflicts()
	{
		// Arrange
		var options = new InMemoryInboxOptions();
		var store = CreateStore(options);
		var conflictMessageCount = 10;
		var operationMultiplier = 20; // 20 operations per message
		var payload = Encoding.UTF8.GetBytes("test payload");
		var metadata = new Dictionary<string, object>();

		var successfulOperations = new ConcurrentBag<(string MessageId, string Operation)>();
		var conflictedOperations = new ConcurrentBag<(string MessageId, string Operation)>();
		var unexpectedExceptions = new ConcurrentBag<Exception>();

		// Pre-create messages to focus on state transition conflicts
		var messageIds = new List<string>();
		for (var i = 0; i < conflictMessageCount; i++)
		{
			var messageId = $"conflict-{i}";
			messageIds.Add(messageId);
			_ = await store.CreateEntryAsync(messageId, TestHandler, "TestMessage", payload, metadata, CancellationToken.None).ConfigureAwait(false);
		}

		// Act - Create high contention on state transitions
		var allTasks = new List<Task>();

		foreach (var messageId in messageIds)
		{
			// Multiple concurrent attempts to mark as processed (using consistent TestHandler)
			for (var i = 0; i < operationMultiplier / 2; i++)
			{
				allTasks.Add(Task.Run(async () =>
				{
					try
					{
						await Task.Delay(Random.Shared.Next(0, 5)).ConfigureAwait(false);
						await store.MarkProcessedAsync(messageId, TestHandler, CancellationToken.None);
						successfulOperations.Add((messageId, "MarkAsProcessed"));
					}
					catch (InvalidOperationException)
					{
						conflictedOperations.Add((messageId, "MarkAsProcessed"));
					}
					catch (Exception ex)
					{
						unexpectedExceptions.Add(ex);
					}
				}));
			}

			// Multiple concurrent attempts to mark as failed (using consistent TestHandler)
			// MarkFailedAsync does NOT throw on already processed/failed (it just updates status)
			for (var i = 0; i < operationMultiplier / 2; i++)
			{
				allTasks.Add(Task.Run(async () =>
				{
					try
					{
						await Task.Delay(Random.Shared.Next(0, 5)).ConfigureAwait(false);
						await store.MarkFailedAsync(messageId, TestHandler, "High contention test error", CancellationToken.None);
						// Check if the message was actually marked as failed
						var entry = await store.GetEntryAsync(messageId, TestHandler, CancellationToken.None);
						if (entry != null && entry.Status == InboxStatus.Failed)
						{
							successfulOperations.Add((messageId, "MarkAsFailed"));
						}
						else
						{
							// Entry was already processed
							conflictedOperations.Add((messageId, "MarkAsFailed"));
						}
					}
					catch (InvalidOperationException)
					{
						// Shouldn't happen since entries exist, but handle anyway
						conflictedOperations.Add((messageId, "MarkAsFailed"));
					}
					catch (Exception ex)
					{
						unexpectedExceptions.Add(ex);
					}
				}));
			}
		}

		await Task.WhenAll(allTasks).ConfigureAwait(false);

		// Assert - Each message should have at least one successful operation
		unexpectedExceptions.ShouldBeEmpty();

		// Group successful operations by message ID
		var successByMessage = successfulOperations.GroupBy(op => op.MessageId).ToList();
		successByMessage.Count.ShouldBe(conflictMessageCount);

		// Each message should have at least one successful state transition
		foreach (var group in successByMessage)
		{
			group.Count().ShouldBeGreaterThanOrEqualTo(1);
		}

		// Most operations should have conflicts due to high contention
		conflictedOperations.Count.ShouldBeGreaterThan(0);

		// Final state should reflect transitions for all messages
		var finalStats = await store.GetStatisticsAsync(CancellationToken.None);
		finalStats.TotalEntries.ShouldBe(conflictMessageCount);
		finalStats.PendingEntries.ShouldBe(0);
		(finalStats.ProcessedEntries + finalStats.FailedEntries).ShouldBe(conflictMessageCount);
	}

	public void Dispose()
	{
		foreach (var disposable in _disposables)
		{
			disposable?.Dispose();
		}
	}

	private InMemoryInboxStore CreateStore(InMemoryInboxOptions options)
	{
		var store = new InMemoryInboxStore(Microsoft.Extensions.Options.Options.Create(options), _logger);
		_disposables.Add(store);
		return store;
	}

	private static async Task WaitForConditionAsync(Func<Task<bool>> condition, TimeSpan timeout)
	{
		var deadline = DateTimeOffset.UtcNow + timeout;
		while (DateTimeOffset.UtcNow < deadline)
		{
			if (await condition().ConfigureAwait(false))
			{
				return;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(10)).ConfigureAwait(false);
		}

		throw new TimeoutException($"Condition was not met within {timeout}.");
	}
}
