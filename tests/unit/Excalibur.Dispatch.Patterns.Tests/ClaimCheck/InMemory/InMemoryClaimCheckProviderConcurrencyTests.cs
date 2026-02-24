// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Text;

using Excalibur.Dispatch.Patterns.ClaimCheck;
using Shouldly;

using Xunit;

namespace Excalibur.Dispatch.Patterns.Tests.ClaimCheck.InMemory;

/// <summary>
/// Unit tests for <see cref="InMemoryClaimCheckProvider"/> thread-safety and concurrency.
/// </summary>
[Trait("Category", "Unit")]
public sealed class InMemoryClaimCheckProviderConcurrencyTests
{
	[Fact]
	public async Task ConcurrentStores_ShouldAllSucceed()
	{
		// Arrange
		var provider = CreateProvider();
		var taskCount = 100;
		var tasks = new List<Task<ClaimCheckReference>>(taskCount);

		// Act
		for (var i = 0; i < taskCount; i++)
		{
			var index = i;
			tasks.Add(Task.Run(async () =>
			{
				var payload = Encoding.UTF8.GetBytes($"Payload {index}");
				return await provider.StoreAsync(payload, CancellationToken.None);
			}));
		}

		var references = await Task.WhenAll(tasks);

		// Assert
		references.Length.ShouldBe(taskCount);
		references.Select(r => r.Id).Distinct().Count().ShouldBe(taskCount); // All unique IDs
		provider.EntryCount.ShouldBe(taskCount);
	}

	[Fact]
	public async Task ConcurrentStoreAndRetrieve_ShouldWorkCorrectly()
	{
		// Arrange
		var provider = CreateProvider();
		var references = new ConcurrentBag<ClaimCheckReference>();
		var storeCount = 50;
		var retrieveCount = 50;

		// Act - Store phase
		var storeTasks = Enumerable.Range(0, storeCount).Select(i => Task.Run(async () =>
		{
			var payload = Encoding.UTF8.GetBytes($"Payload {i}");
			var reference = await provider.StoreAsync(payload, CancellationToken.None);
			references.Add(reference);
		}));

		await Task.WhenAll(storeTasks);

		// Act - Retrieve phase
		var retrieveTasks = references.Take(retrieveCount).Select(reference => Task.Run(async () =>
		{
			var payload = await provider.RetrieveAsync(reference, CancellationToken.None);
			return payload;
		}));

		var payloads = await Task.WhenAll(retrieveTasks);

		// Assert
		payloads.Length.ShouldBe(retrieveCount);
		payloads.All(p => p != null && p.Length > 0).ShouldBeTrue();
	}

	[Fact]
	public async Task ConcurrentStoreAndDelete_ShouldHandleCorrectly()
	{
		// Arrange
		var provider = CreateProvider();
		var storeCount = 100;

		// First, store entries
		var storeTasks = Enumerable.Range(0, storeCount).Select(i => Task.Run(async () =>
		{
			var payload = Encoding.UTF8.GetBytes($"Payload {i}");
			return await provider.StoreAsync(payload, CancellationToken.None);
		}));

		var references = await Task.WhenAll(storeTasks);

		// Act - Concurrent deletes
		var deleteTasks = references.Select(reference => Task.Run(async () =>
		{
			return await provider.DeleteAsync(reference, CancellationToken.None);
		}));

		var deleteResults = await Task.WhenAll(deleteTasks);

		// Assert
		deleteResults.All(r => r).ShouldBeTrue(); // All deletes successful
		provider.EntryCount.ShouldBe(0);
	}

	[Fact]
	public async Task ConcurrentStoreSamePayload_ShouldCreateUniqueEntries()
	{
		// Arrange
		var provider = CreateProvider();
		var payload = "Same payload for all"u8.ToArray();
		var taskCount = 50;

		// Act
		var storeTasks = Enumerable.Range(0, taskCount).Select(_ => Task.Run(async () =>
		{
			return await provider.StoreAsync(payload, CancellationToken.None);
		}));

		var references = await Task.WhenAll(storeTasks);

		// Assert
		references.Length.ShouldBe(taskCount);
		references.Select(r => r.Id).Distinct().Count().ShouldBe(taskCount); // All unique IDs
		provider.EntryCount.ShouldBe(taskCount);
	}

	[Fact]
	public async Task ConcurrentRetrieveSameEntry_ShouldAllSucceed()
	{
		// Arrange
		var provider = CreateProvider();
		var payload = "Shared payload"u8.ToArray();
		var reference = await provider.StoreAsync(payload, CancellationToken.None);
		var retrieveCount = 100;

		// Act
		var retrieveTasks = Enumerable.Range(0, retrieveCount).Select(_ => Task.Run(async () =>
		{
			return await provider.RetrieveAsync(reference, CancellationToken.None);
		}));

		var payloads = await Task.WhenAll(retrieveTasks);

		// Assert
		payloads.Length.ShouldBe(retrieveCount);
		payloads.All(p => p.SequenceEqual(payload)).ShouldBeTrue();
	}

	[Fact]
	public async Task ConcurrentDeleteSameEntry_ShouldOnlyOneSucceed()
	{
		// Arrange
		var provider = CreateProvider();
		var payload = "Payload to delete concurrently"u8.ToArray();
		var reference = await provider.StoreAsync(payload, CancellationToken.None);
		var deleteCount = 10;

		// Act
		var deleteTasks = Enumerable.Range(0, deleteCount).Select(_ => Task.Run(async () =>
		{
			return await provider.DeleteAsync(reference, CancellationToken.None);
		}));

		var deleteResults = await Task.WhenAll(deleteTasks);

		// Assert
		deleteResults.Count(r => r).ShouldBe(1); // Only one delete returns true
		deleteResults.Count(r => !r).ShouldBe(deleteCount - 1); // Rest return false
		provider.EntryCount.ShouldBe(0);
	}

	[Fact]
	public async Task ConcurrentStoreRetrieveDelete_ShouldWorkCorrectly()
	{
		// Arrange
		var provider = CreateProvider();
		var references = new ConcurrentBag<ClaimCheckReference>();
		var operationCount = 30;

		// Act - Mix of operations
		var tasks = new List<Task>();

		// Stores
		for (var i = 0; i < operationCount; i++)
		{
			var index = i;
			tasks.Add(Task.Run(async () =>
			{
				var payload = Encoding.UTF8.GetBytes($"Payload {index}");
				var reference = await provider.StoreAsync(payload, CancellationToken.None);
				references.Add(reference);
			}));
		}

		// Wait for some stores to complete
		await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(500);

		// Retrieves
		for (var i = 0; i < operationCount / 2; i++)
		{
			tasks.Add(Task.Run(async () =>
			{
				if (references.TryTake(out var reference))
				{
					_ = await provider.RetrieveAsync(reference, CancellationToken.None);
				}
			}));
		}

		// Deletes
		for (var i = 0; i < operationCount / 3; i++)
		{
			tasks.Add(Task.Run(async () =>
			{
				if (references.TryTake(out var reference))
				{
					_ = await provider.DeleteAsync(reference, CancellationToken.None);
				}
			}));
		}

		// Wait for all operations
		await Task.WhenAll(tasks);

		// Assert - No exceptions, provider still functional
		provider.EntryCount.ShouldBeGreaterThanOrEqualTo(0);
	}

	[Fact]
	public async Task ConcurrentRemoveExpiredEntries_ShouldBeSafe()
	{
		// Arrange
		var provider = CreateProvider(options =>
		{
			options.DefaultTtl = TimeSpan.FromMilliseconds(50);
		});

		// Store entries
		for (var i = 0; i < 20; i++)
		{
			_ = await provider.StoreAsync(Encoding.UTF8.GetBytes($"Payload {i}"), CancellationToken.None);
		}

		// Wait for expiration
		await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(1000);

		// Act - Concurrent cleanup calls
		var cleanupTasks = Enumerable.Range(0, 10).Select(_ => Task.Run(() =>
		{
			return provider.RemoveExpiredEntries();
		}));

		var results = await Task.WhenAll(cleanupTasks);

		// Assert
		results.Sum().ShouldBeGreaterThan(0); // At least some were removed
		provider.EntryCount.ShouldBe(0); // All expired entries removed
	}

	[Fact]
	public async Task ConcurrentClearAll_ShouldBeSafe()
	{
		// Arrange
		var provider = CreateProvider();

		// Store entries
		for (var i = 0; i < 50; i++)
		{
			_ = await provider.StoreAsync(Encoding.UTF8.GetBytes($"Payload {i}"), CancellationToken.None);
		}

		// Act - Concurrent clear calls
		var clearTasks = Enumerable.Range(0, 5).Select(_ => Task.Run(() =>
		{
			provider.ClearAll();
		}));

		await Task.WhenAll(clearTasks);

		// Assert
		provider.EntryCount.ShouldBe(0);
	}

	[Fact]
	public async Task ConcurrentStoreWithCompression_ShouldWorkCorrectly()
	{
		// Arrange
		var provider = CreateProvider(options =>
		{
			options.EnableCompression = true;
			options.CompressionThreshold = 1024;
		});

		var taskCount = 50;
		var largePayload = new string('A', 2048); // Compressible

		// Act
		var storeTasks = Enumerable.Range(0, taskCount).Select(i => Task.Run(async () =>
		{
			var payload = Encoding.UTF8.GetBytes($"{largePayload}-{i}");
			return await provider.StoreAsync(payload, CancellationToken.None);
		}));

		var references = await Task.WhenAll(storeTasks);

		// Assert
		references.Length.ShouldBe(taskCount);
		provider.EntryCount.ShouldBe(taskCount);

		// Verify retrieval works
		var retrieveTasks = references.Select(r => provider.RetrieveAsync(r, CancellationToken.None));
		var payloads = await Task.WhenAll(retrieveTasks);
		payloads.All(p => p != null && p.Length > 0).ShouldBeTrue();
	}

	[Fact]
	public async Task HighConcurrencyStressTest_ShouldHandleLoad()
	{
		// Arrange
		var provider = CreateProvider();
		var iterations = 1000;

		// Act
		var storeTasks = Enumerable.Range(0, iterations).Select(i => Task.Run(async () =>
		{
			var payload = Encoding.UTF8.GetBytes($"Stress test payload {i}");
			var reference = await provider.StoreAsync(payload, CancellationToken.None);
			var retrieved = await provider.RetrieveAsync(reference, CancellationToken.None);
			_ = await provider.DeleteAsync(reference, CancellationToken.None);
			return retrieved.SequenceEqual(payload);
		}));

		var results = await Task.WhenAll(storeTasks);

		// Assert
		results.All(r => r).ShouldBeTrue();
		provider.EntryCount.ShouldBe(0);
	}

	[Fact]
	public async Task ConcurrentStoreWithChecksum_ShouldWorkCorrectly()
	{
		// Arrange
		var provider = CreateProvider(options =>
		{
			options.ValidateChecksum = true;
		});

		var taskCount = 50;

		// Act
		var storeTasks = Enumerable.Range(0, taskCount).Select(i => Task.Run(async () =>
		{
			var payload = Encoding.UTF8.GetBytes($"Checksum payload {i}");
			var reference = await provider.StoreAsync(payload, CancellationToken.None);
			var retrieved = await provider.RetrieveAsync(reference, CancellationToken.None);
			return retrieved.SequenceEqual(payload);
		}));

		var results = await Task.WhenAll(storeTasks);

		// Assert
		results.All(r => r).ShouldBeTrue();
	}

	private static InMemoryClaimCheckProvider CreateProvider(Action<ClaimCheckOptions>? configure = null)
	{
		var options = new ClaimCheckOptions();
		configure?.Invoke(options);
		return new InMemoryClaimCheckProvider(Microsoft.Extensions.Options.Options.Create(options));
	}
}
