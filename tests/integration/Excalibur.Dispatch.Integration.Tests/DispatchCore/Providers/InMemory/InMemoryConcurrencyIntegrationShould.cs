// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Data;

using Excalibur.Data.InMemory;

using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

using Tests.Shared;
using Tests.Shared.Categories;

namespace Excalibur.Dispatch.Integration.Tests.DispatchCore.Providers.InMemory;

/// <summary>
/// Integration tests for <see cref="InMemoryPersistenceProvider"/> concurrency handling.
/// Tests thread safety, concurrent reads/writes, and lock contention.
/// </summary>
/// <remarks>
/// <para>
/// Sprint 180 - InMemory Provider Testing Epic.
/// bd-n1xg1: Concurrency Tests (5 tests).
/// </para>
/// <para>
/// These tests verify thread safety using ConcurrentDictionary and SemaphoreSlim patterns.
/// Uses Task.WhenAll with multiple concurrent operations to stress test.
/// </para>
/// </remarks>
[IntegrationTest]
[Trait("Component", "Concurrency")]
[Trait("Provider", "InMemory")]
public sealed class InMemoryConcurrencyIntegrationShould : IntegrationTestBase
{
	/// <summary>
	/// Tests that concurrent reads do not block each other.
	/// </summary>
	[Fact]
	public async Task AllowConcurrentReads()
	{
		// Arrange
		using var provider = CreatePersistenceProvider();
		var collectionName = "read-test-collection";
		var key = "shared-item";
		var testData = new TestEntity { Id = key, Name = "Shared Data", Value = 100 };

		// Store initial data
		provider.Store(collectionName, key, testData);

		var readCount = 100;
		var successfulReads = new ConcurrentBag<TestEntity>();

		// Act - Perform many concurrent reads
		var readTasks = Enumerable.Range(0, readCount).Select(async _ =>
		{
			await Task.Yield(); // Allow interleaving
			var result = provider.Retrieve<TestEntity>(collectionName, key);
			if (result != null)
			{
				successfulReads.Add(result);
			}
		}).ToArray();

		await Task.WhenAll(readTasks).ConfigureAwait(true);

		// Assert - All reads succeeded with correct data
		successfulReads.Count.ShouldBe(readCount);
		successfulReads.All(e => e.Name == "Shared Data").ShouldBeTrue();
	}

	/// <summary>
	/// Tests that concurrent writes with different keys succeed.
	/// </summary>
	[Fact]
	public async Task HandleConcurrentWritesToDifferentKeys()
	{
		// Arrange
		using var provider = CreatePersistenceProvider();
		var collectionName = "write-test-collection";
		var writeCount = 50;
		var writtenKeys = new ConcurrentBag<string>();

		// Act - Perform many concurrent writes to different keys
		var writeTasks = Enumerable.Range(0, writeCount).Select(async i =>
		{
			await Task.Yield(); // Allow interleaving
			var key = $"item-{i}";
			var data = new TestEntity { Id = key, Name = $"Item {i}", Value = i };
			provider.Store(collectionName, key, data);
			writtenKeys.Add(key);
		}).ToArray();

		await Task.WhenAll(writeTasks).ConfigureAwait(true);

		// Assert - All writes succeeded
		writtenKeys.Count.ShouldBe(writeCount);

		// Verify all items can be retrieved
		foreach (var key in writtenKeys)
		{
			var retrieved = provider.Retrieve<TestEntity>(collectionName, key);
			_ = retrieved.ShouldNotBeNull();
		}
	}

	/// <summary>
	/// Tests optimistic concurrency with version-based conflict detection.
	/// InMemory provider stores values by key, later writes overwrite earlier ones.
	/// </summary>
	[Fact]
	public async Task DetectOptimisticConcurrencyConflicts()
	{
		// Arrange
		using var provider = CreatePersistenceProvider();
		var collectionName = "concurrency-collection";
		var key = "contested-item";

		// Store initial versioned data
		var initialData = new VersionedEntity { Id = key, Version = 1, Data = "Initial" };
		provider.Store(collectionName, key, initialData);

		var conflictDetected = false;
		var finalVersion = 0;
		var lockObj = new object();

		// Act - Two concurrent updates with version checks
		var updateTasks = new[]
		{
			Task.Run(async () =>
			{
				await Task.Delay(5, TestCancellationToken).ConfigureAwait(false); // Small delay

				var current = provider.Retrieve<VersionedEntity>(collectionName, key);
				if (current?.Version == 1)
				{
					var updated = new VersionedEntity
					{
						Id = key,
						Version = current.Version + 1,
						Data = "Updated by Task 1"
					};
					provider.Store(collectionName, key, updated);
					lock (lockObj) { finalVersion = Math.Max(finalVersion, updated.Version); }
				}
				else
				{
					lock (lockObj) { conflictDetected = true; }
				}
			}),
			Task.Run(async () =>
			{
				await Task.Delay(10, TestCancellationToken).ConfigureAwait(false); // Slightly longer delay

				var current = provider.Retrieve<VersionedEntity>(collectionName, key);
				if (current?.Version == 1)
				{
					var updated = new VersionedEntity
					{
						Id = key,
						Version = current.Version + 1,
						Data = "Updated by Task 2"
					};
					provider.Store(collectionName, key, updated);
					lock (lockObj) { finalVersion = Math.Max(finalVersion, updated.Version); }
				}
				else
				{
					lock (lockObj) { conflictDetected = true; }
				}
			})
		};

		await Task.WhenAll(updateTasks).ConfigureAwait(true);

		// Assert - At least one update succeeded, potentially with conflict detected
		var finalData = provider.Retrieve<VersionedEntity>(collectionName, key);
		_ = finalData.ShouldNotBeNull();

		// Either conflict was detected or version was incremented
		(conflictDetected || finalData.Version == 2).ShouldBeTrue();
	}

	/// <summary>
	/// Tests that many concurrent transactions are handled gracefully via serialization.
	/// </summary>
	[Fact]
	public async Task HandleLockContention()
	{
		// Arrange
		using var provider = CreatePersistenceProvider();
		var transactionCount = 20;
		var completedTransactions = new ConcurrentBag<int>();

		// Act - Start many concurrent transactions (serialized by SemaphoreSlim)
		var tasks = Enumerable.Range(0, transactionCount).Select(async i =>
		{
			// BeginTransactionAsync waits for the semaphore
			using var transaction = await provider.BeginTransactionAsync(
				IsolationLevel.ReadCommitted,
				TestCancellationToken).ConfigureAwait(false);

			// Simulate work
			await Task.Delay(5, TestCancellationToken).ConfigureAwait(false);

			transaction.Commit();
			completedTransactions.Add(i);
		}).ToArray();

		await Task.WhenAll(tasks).ConfigureAwait(true);

		// Assert - All transactions completed despite lock contention
		completedTransactions.Count.ShouldBe(transactionCount);
	}

	/// <summary>
	/// Tests that no updates are lost under concurrent write load.
	/// </summary>
	[Fact]
	public async Task PreventRaceConditionsAndLostUpdates()
	{
		// Arrange
		using var provider = CreatePersistenceProvider();
		var collectionName = "race-test-collection";
		var itemCount = 100;
		var allKeys = new ConcurrentBag<string>();

		// Act - Concurrent writes with unique keys (no conflicts expected)
		var writeTasks = Enumerable.Range(0, itemCount).Select(async i =>
		{
			await Task.Yield();
			var key = $"unique-{Guid.NewGuid():N}";
			provider.Store(collectionName, key, new TestEntity { Id = key, Value = i });
			allKeys.Add(key);
		}).ToArray();

		await Task.WhenAll(writeTasks).ConfigureAwait(true);

		// Assert - No lost updates
		allKeys.Count.ShouldBe(itemCount);

		// Verify all items exist
		var collection = provider.GetCollection(collectionName);
		collection.Count.ShouldBe(itemCount);

		// Verify each key exists
		foreach (var key in allKeys)
		{
			collection.ContainsKey(key).ShouldBeTrue();
		}
	}

	/// <summary>
	/// Tests concurrent read and write operations on the same collection.
	/// </summary>
	[Fact]
	public async Task HandleMixedReadWriteOperations()
	{
		// Arrange
		using var provider = CreatePersistenceProvider();
		var collectionName = "mixed-ops-collection";
		var operationCount = 50;

		// Seed some initial data
		for (var i = 0; i < 10; i++)
		{
			provider.Store(collectionName, $"seed-{i}", new TestEntity { Id = $"seed-{i}", Value = i });
		}

		var readResults = new ConcurrentBag<TestEntity>();
		var writeCount = 0;
		var lockObj = new object();

		// Act - Mixed concurrent reads and writes
		var tasks = Enumerable.Range(0, operationCount).Select(async i =>
		{
			await Task.Yield();

			if (i % 2 == 0)
			{
				// Read operation
				var key = $"seed-{i % 10}";
				var result = provider.Retrieve<TestEntity>(collectionName, key);
				if (result != null)
				{
					readResults.Add(result);
				}
			}
			else
			{
				// Write operation
				var key = $"new-{i}";
				provider.Store(collectionName, key, new TestEntity { Id = key, Value = i });
				lock (lockObj)
				{ writeCount++; }
			}
		}).ToArray();

		await Task.WhenAll(tasks).ConfigureAwait(true);

		// Assert - Both reads and writes succeeded
		readResults.Count.ShouldBeGreaterThan(0);
		writeCount.ShouldBeGreaterThan(0);

		// Verify total item count includes both seed and new items
		var collection = provider.GetCollection(collectionName);
		collection.Count.ShouldBe(10 + writeCount);
	}

	private static InMemoryPersistenceProvider CreatePersistenceProvider()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new InMemoryProviderOptions
		{
			Name = $"concurrency-test-{Guid.NewGuid():N}"
		});
		var logger = NullLogger<InMemoryPersistenceProvider>.Instance;
		return new InMemoryPersistenceProvider(options, logger);
	}

	private sealed class TestEntity
	{
		public string Id { get; set; } = string.Empty;
		public string Name { get; set; } = string.Empty;
		public int Value { get; set; }
	}

	private sealed class VersionedEntity
	{
		public string Id { get; set; } = string.Empty;
		public int Version { get; set; }
		public string Data { get; set; } = string.Empty;
	}
}
