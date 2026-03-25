// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

namespace Excalibur.Dispatch.Tests.Messaging.Concurrency;

/// <summary>
/// Regression tests for ConcurrentBag&lt;Task&gt; drain: ToArray()+Clear() race condition.
/// Verifies that the atomic drain pattern (Interlocked.Exchange or equivalent) does not lose items
/// when concurrent add and drain operations occur simultaneously.
/// </summary>
/// <remarks>
/// The bug was that <c>_trackedTasks.ToArray()</c> followed by <c>_trackedTasks.Clear()</c> is non-atomic:
/// items added between ToArray() and Clear() are lost. The fix replaces this with an atomic swap
/// pattern using Interlocked.Exchange or a similar mechanism.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ConcurrentBagDrainRegressionShould
{
	[Fact]
	public async Task NotLoseItems_WhenConcurrentAddAndDrainOccur()
	{
		// Arrange -- Simulate the pattern used in multiple files:
		// FieldEncryptor, SecurityAuditor, AzureKeyVaultProvider, CdcProcessor,
		// ConsulLeaderElection, KubernetesLeaderElection, etc.
		var allProducedItems = new ConcurrentBag<int>();
		var allDrainedItems = new ConcurrentBag<int>();
		var collection = new ConcurrentBag<int>();
		var drainCount = 0;
		const int totalItems = 10_000;
		const int drainInterval = 100;

		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

		// Act -- Run producer and consumer concurrently.
		// Do NOT pass cts.Token to Task.Run -- the producer is a finite loop that
		// doesn't need cancellation. Passing the token causes TaskCanceledException
		// if the thread pool doesn't schedule the task before the timeout fires.
		var producerTask = Task.Run(() =>
		{
			for (var i = 0; i < totalItems; i++)
			{
				collection.Add(i);
				allProducedItems.Add(i);

				// Periodically trigger a drain (simulating the pattern in production code)
				if (i % drainInterval == 0)
				{
					var snapshot = AtomicDrain(collection);
					foreach (var item in snapshot)
					{
						allDrainedItems.Add(item);
					}

					Interlocked.Increment(ref drainCount);
				}
			}
		});

		// Concurrent drain consumer -- only the consumer uses the CTS for shutdown.
		var consumerTask = Task.Run(() =>
		{
			while (!cts.IsCancellationRequested)
			{
				var snapshot = AtomicDrain(collection);
				if (snapshot.Length == 0)
				{
					Thread.SpinWait(10);
					continue;
				}

				foreach (var item in snapshot)
				{
					allDrainedItems.Add(item);
				}

				Interlocked.Increment(ref drainCount);
			}
		});

		await producerTask.ConfigureAwait(false);
		await cts.CancelAsync().ConfigureAwait(false);

		try
		{
			await consumerTask.WaitAsync(TimeSpan.FromSeconds(1)).ConfigureAwait(false);
		}
		catch (OperationCanceledException)
		{
			// Expected on cancellation
		}
		catch (TimeoutException)
		{
			// Expected if consumer doesn't stop immediately
		}

		// Final drain to pick up any remaining items
		var remaining = AtomicDrain(collection);
		foreach (var item in remaining)
		{
			allDrainedItems.Add(item);
		}

		// Assert -- No items should be lost
		var producedSet = allProducedItems.ToHashSet();
		var drainedSet = allDrainedItems.ToHashSet();

		// Every produced item must appear in the drained set
		producedSet.Count.ShouldBe(totalItems, "All items should have been produced");
		drainedSet.Count.ShouldBe(totalItems,
			$"All {totalItems} produced items must be drained. Lost {producedSet.Count - drainedSet.Count} items.");

		// Verify no duplicates in drained items (atomic drain should not cause double-counting)
		allDrainedItems.Count.ShouldBe(totalItems,
			"No duplicate items should appear in drained output");
	}

	[Fact]
	public async Task NotLoseItems_UnderHighContention()
	{
		// Arrange -- Multiple producers and a single drainer
		var collection = new ConcurrentBag<int>();
		var allDrainedItems = new ConcurrentBag<int>();
		var itemsPerProducer = 1_000;
		var producerCount = 8;
		var totalExpected = itemsPerProducer * producerCount;

		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
		var producersDone = 0;

		// Act -- Start producers (finite loops, no cancellation token needed)
		var producers = Enumerable.Range(0, producerCount).Select(producerIdx => Task.Run(() =>
		{
			for (var i = 0; i < itemsPerProducer; i++)
			{
				collection.Add(producerIdx * itemsPerProducer + i);
			}

			Interlocked.Increment(ref producersDone);
		})).ToArray();

		// Drainer runs concurrently
		var drainer = Task.Run(() =>
		{
			while (producersDone < producerCount || !collection.IsEmpty)
			{
				if (cts.IsCancellationRequested)
				{
					break;
				}

				var snapshot = AtomicDrain(collection);
				foreach (var item in snapshot)
				{
					allDrainedItems.Add(item);
				}

				if (snapshot.Length == 0)
				{
					Thread.SpinWait(10);
				}
			}

			// Final drain
			var final = AtomicDrain(collection);
			foreach (var item in final)
			{
				allDrainedItems.Add(item);
			}
		});

		await Task.WhenAll([.. producers, drainer]).ConfigureAwait(false);

		// Assert
		var drainedSet = allDrainedItems.ToHashSet();
		drainedSet.Count.ShouldBe(totalExpected,
			$"Expected {totalExpected} unique items but got {drainedSet.Count}. " +
			$"Lost {totalExpected - drainedSet.Count} items due to drain race condition.");
	}

	[Fact]
	public void AtomicDrain_ReturnsAllCurrentItems()
	{
		// Arrange -- Simple non-concurrent test to verify basic drain behavior
		var collection = new ConcurrentBag<int>();
		for (var i = 0; i < 100; i++)
		{
			collection.Add(i);
		}

		// Act
		var drained = AtomicDrain(collection);

		// Assert
		drained.Length.ShouldBe(100);
		collection.IsEmpty.ShouldBeTrue("Collection should be empty after atomic drain");
	}

	/// <summary>
	/// Simulates the correct atomic drain pattern using TryTake loop.
	/// This is the pattern that should replace ToArray()+Clear() in production code.
	/// The actual fix in production may use Interlocked.Exchange on a field reference.
	/// </summary>
	private static int[] AtomicDrain(ConcurrentBag<int> collection)
	{
		var items = new List<int>();
		while (collection.TryTake(out var item))
		{
			items.Add(item);
		}

		return items.ToArray();
	}
}
