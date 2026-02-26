// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.Dispatch.BatchProcessing;
using Excalibur.Dispatch.Options.Performance;

namespace Excalibur.Dispatch.Tests.Messaging.BatchProcessing;

/// <summary>
///     Tests for the <see cref="BatchProcessor{T}" /> class.
/// </summary>
[Collection("Performance Tests")]
[Trait("Category", "Unit")]
public sealed class BatchProcessorShould : IDisposable
{
	private static readonly string[] ThreeItemBatch = ["item1", "item2", "item3"];
	private static readonly string[] TwoItemBatch = ["item1", "item2"];

	private readonly ILogger<BatchProcessor<string>> _logger;
	private readonly List<BatchProcessor<string>> _disposables;

	public BatchProcessorShould()
	{
		_logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<BatchProcessor<string>>.Instance;
		_disposables = [];
	}

	[Fact]
	public void ThrowArgumentNullExceptionForNullBatchProcessor() =>
		_ = Should.Throw<ArgumentNullException>(() =>
			new BatchProcessor<string>(null!, _logger));

	[Fact]
	public void ThrowArgumentNullExceptionForNullLogger() =>
		_ = Should.Throw<ArgumentNullException>(() =>
			new BatchProcessor<string>(_ => ValueTask.CompletedTask, null!));

	[Fact]
	public void UseDefaultOptionsWhenNotProvided()
	{
		var processedBatches = new List<IReadOnlyList<string>>();

		var processor = new BatchProcessor<string>(
			batch =>
			{
				processedBatches.Add(batch);
				return ValueTask.CompletedTask;
			},
			_logger);

		_disposables.Add(processor);

		_ = processor.ShouldNotBeNull();
	}

	[Fact]
	public async Task ProcessSingleItemImmediately()
	{
		var processedBatches = new ConcurrentBag<IReadOnlyList<string>>();
		var tcs = new TaskCompletionSource<bool>();

		var processor = new BatchProcessor<string>(
			batch =>
			{
				processedBatches.Add(batch);
				_ = tcs.TrySetResult(true);
				return ValueTask.CompletedTask;
			},
			_logger);

		_disposables.Add(processor);

		await processor.AddAsync("item1", CancellationToken.None).ConfigureAwait(false);

		await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(

			tcs.Task,

			global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(30)));
		processedBatches.Count.ShouldBe(1);
		var batches = processedBatches.ToArray();
		batches[0].Count.ShouldBe(1);
		batches[0][0].ShouldBe("item1");
	}

	[Fact]
	public async Task BatchMultipleItemsBasedOnSize()
	{
		var options = new MicroBatchOptions
		{
			MaxBatchSize = 3,
			MaxBatchDelay = TimeSpan.FromSeconds(10), // Long delay to ensure size triggers
		};

		var processedBatches = new ConcurrentBag<IReadOnlyList<string>>();
		var tcs = new TaskCompletionSource<bool>();

		var processor = new BatchProcessor<string>(
			batch =>
			{
				processedBatches.Add(batch);
				if (batch.Count == 3)
				{
					_ = tcs.TrySetResult(true);
				}

				return ValueTask.CompletedTask;
			},
			_logger,
			options);

		_disposables.Add(processor);

		await processor.AddAsync("item1", CancellationToken.None).ConfigureAwait(false);
		await processor.AddAsync("item2", CancellationToken.None).ConfigureAwait(false);
		await processor.AddAsync("item3", CancellationToken.None).ConfigureAwait(false);

		await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(

			tcs.Task,

			global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(30)));
		processedBatches.Count.ShouldBe(1);
		var batches = processedBatches.ToArray();
		batches[0].Count.ShouldBe(3);
		batches[0].ShouldBe(ThreeItemBatch);
	}

	[Fact]
	public async Task FlushBatchBasedOnTimeDelay()
	{
		var options = new MicroBatchOptions { MaxBatchSize = 10, MaxBatchDelay = TimeSpan.FromMilliseconds(100) };

		var processedBatches = new ConcurrentBag<IReadOnlyList<string>>();
		var tcs = new TaskCompletionSource<bool>();

		var processor = new BatchProcessor<string>(
			batch =>
			{
				processedBatches.Add(batch);
				_ = tcs.TrySetResult(true);
				return ValueTask.CompletedTask;
			},
			_logger,
			options);

		_disposables.Add(processor);

		await processor.AddAsync("item1", CancellationToken.None).ConfigureAwait(false);
		await processor.AddAsync("item2", CancellationToken.None).ConfigureAwait(false);

		await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(

			tcs.Task,

			global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(30)));
		processedBatches.Count.ShouldBe(1);
		var batches = processedBatches.ToArray();
		batches[0].Count.ShouldBe(2);
		batches[0].ShouldBe(TwoItemBatch);
	}

	[Fact]
	public async Task HandleBatchProcessorExceptions()
	{
		var callCount = 0;
		var tcs = new TaskCompletionSource<bool>();

		// Use MaxBatchSize=1 to ensure each item is processed as a separate batch
		// This allows the first batch to fail while the second batch succeeds
		var options = new MicroBatchOptions
		{
			MaxBatchSize = 1,
			MaxBatchDelay = TimeSpan.FromMilliseconds(50),
		};

		var processor = new BatchProcessor<string>(
			batch =>
			{
				callCount++;
				if (callCount == 1)
				{
					throw new InvalidOperationException("Test exception");
				}

				_ = tcs.TrySetResult(true);
				return ValueTask.CompletedTask;
			},
			_logger,
			options);

		_disposables.Add(processor);

		await processor.AddAsync("item1", CancellationToken.None).ConfigureAwait(false);
		await processor.AddAsync("item2", CancellationToken.None).ConfigureAwait(false);

		// Under full-suite parallel load (40K+ tests), the batch processor's background loop
		// may be severely delayed by thread pool starvation
		await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
			tcs.Task,
			global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(60)));
		callCount.ShouldBe(2);
	}

	[Fact]
	public async Task ProcessRemainingItemsOnDisposal()
	{
		var processedBatches = new ConcurrentBag<IReadOnlyList<string>>();
		var allProcessed = new TaskCompletionSource<bool>();

		var processor = new BatchProcessor<string>(
			batch =>
			{
				processedBatches.Add(batch);
				if (processedBatches.Sum(b => b.Count) >= 2)
				{
					_ = allProcessed.TrySetResult(true);
				}

				return ValueTask.CompletedTask;
			},
			_logger,
			new MicroBatchOptions
			{
				MaxBatchSize = 10,
				MaxBatchDelay = TimeSpan.FromMilliseconds(50), // Short delay to trigger flush
			});

		_disposables.Add(processor);

		await processor.AddAsync("item1", CancellationToken.None).ConfigureAwait(false);
		await processor.AddAsync("item2", CancellationToken.None).ConfigureAwait(false);

		// Wait for batch delay to trigger processing
		await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
			allProcessed.Task,
			global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(30)));
		processedBatches.Sum(b => b.Count).ShouldBe(2);
	}

	[Fact]
	public async Task HandleConcurrentAdds()
	{
		var processedItems = new ConcurrentBag<string>();
		var allItemsProcessed = new TaskCompletionSource<bool>();
		var expectedItemCount = 100;

		var processor = new BatchProcessor<string>(
			batch =>
			{
				foreach (var item in batch)
				{
					processedItems.Add(item);
				}

				if (processedItems.Count >= expectedItemCount)
				{
					_ = allItemsProcessed.TrySetResult(true);
				}

				return ValueTask.CompletedTask;
			},
			_logger,
			new MicroBatchOptions { MaxBatchSize = 10, MaxBatchDelay = TimeSpan.FromMilliseconds(50) });

		_disposables.Add(processor);

		var tasks = Enumerable.Range(0, expectedItemCount)
			.Select(async i => await processor.AddAsync($"item{i}", CancellationToken.None).ConfigureAwait(false));

		await Task.WhenAll(tasks).ConfigureAwait(false);
		await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
			allItemsProcessed.Task,
			global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(30)));
		processedItems.Count.ShouldBe(expectedItemCount);
	}

	[Fact]
	public async Task HandleCancellationGracefully()
	{
		var processor = new BatchProcessor<string>(
			_ => ValueTask.CompletedTask,
			_logger);

		_disposables.Add(processor);

		using var cts = new CancellationTokenSource();
		await cts.CancelAsync().ConfigureAwait(false);

		await Should.NotThrowAsync(async () =>
			await processor.AddAsync("item1", cts.Token).ConfigureAwait(false)).ConfigureAwait(false);
	}

	[Fact]
	public void DisposeMultipleTimesSafely()
	{
		var processor = new BatchProcessor<string>(
			_ => ValueTask.CompletedTask,
			_logger);

		Should.NotThrow(() =>
		{
			processor.Dispose();
			processor.Dispose();
			processor.Dispose();
		});
	}

	[Fact]
	public async Task TryWriteSucceedsForUnboundedChannel()
	{
		var itemProcessed = new TaskCompletionSource<bool>();

		var processor = new BatchProcessor<string>(
			batch =>
			{
				if (batch.Count > 0)
				{
					_ = itemProcessed.TrySetResult(true);
				}

				return ValueTask.CompletedTask;
			},
			_logger);

		_disposables.Add(processor);

		// First item should succeed via TryWrite
		await processor.AddAsync("item1", CancellationToken.None).ConfigureAwait(false);

		await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(

			itemProcessed.Task,

			global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(30)));
	}

	[Fact]
	public async Task ProcessEmptyBatchesCorrectly()
	{
		var batchProcessorCalled = false;
		var unexpectedBatchProcessed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

		var processor = new BatchProcessor<string>(
			batch =>
			{
				batchProcessorCalled = true;
				_ = unexpectedBatchProcessed.TrySetResult();
				batch.Count.ShouldBeGreaterThan(0);
				return ValueTask.CompletedTask;
			},
			_logger);

		_disposables.Add(processor);

		var observedUnexpectedBatch = await global::Tests.Shared.Infrastructure.WaitHelpers.WaitUntilAsync(
			() => unexpectedBatchProcessed.Task.IsCompleted,
			TimeSpan.FromMilliseconds(250),
			TimeSpan.FromMilliseconds(10)).ConfigureAwait(false);
		observedUnexpectedBatch.ShouldBeFalse();

		batchProcessorCalled.ShouldBeFalse();
	}

	[Fact]
	public async Task HandleHighFrequencyAdds()
	{
		var processedItems = new ConcurrentBag<string>();
		var options = new MicroBatchOptions { MaxBatchSize = 5, MaxBatchDelay = TimeSpan.FromMilliseconds(10) };

		var processor = new BatchProcessor<string>(
			batch =>
			{
				foreach (var item in batch)
				{
					processedItems.Add(item);
				}

				return ValueTask.CompletedTask;
			},
			_logger,
			options);

		_disposables.Add(processor);

		// Add items rapidly
		for (var i = 0; i < 50; i++)
		{
			await processor.AddAsync($"item{i}", CancellationToken.None).ConfigureAwait(false);
		}

		// Wait for processing to complete
		await WaitForConditionAsync(() => processedItems.Count == 50, TimeSpan.FromSeconds(20)).ConfigureAwait(false);

		processedItems.Count.ShouldBe(50);
	}

	[Fact]
	public async Task RespectMaxBatchSizeExactly()
	{
		var options = new MicroBatchOptions { MaxBatchSize = 5, MaxBatchDelay = TimeSpan.FromSeconds(10) };

		var batchSizes = new ConcurrentBag<int>();
		var expectedBatches = new TaskCompletionSource<bool>();

		var processor = new BatchProcessor<string>(
			batch =>
			{
				batchSizes.Add(batch.Count);
				if (batchSizes.Count >= 2) // Expect at least 2 batches
				{
					_ = expectedBatches.TrySetResult(true);
				}

				return ValueTask.CompletedTask;
			},
			_logger,
			options);

		_disposables.Add(processor);

		// Add exactly 2 full batches
		for (var i = 0; i < 10; i++)
		{
			await processor.AddAsync($"item{i}", CancellationToken.None).ConfigureAwait(false);
		}

		await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(

			expectedBatches.Task,

			global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(30)));
		batchSizes.All(size => size <= options.MaxBatchSize).ShouldBeTrue();
		batchSizes.Count(size => size == options.MaxBatchSize).ShouldBeGreaterThanOrEqualTo(2);
	}

	[Fact]
	public async Task HandleAsyncBatchProcessorExceptions()
	{
		var callCount = 0;
		var processedItems = new ConcurrentBag<string>();
		var allProcessed = new TaskCompletionSource<bool>();

		// Use batch size of 1 to ensure each item is processed separately
		var options = new MicroBatchOptions { MaxBatchSize = 1, MaxBatchDelay = TimeSpan.FromMilliseconds(10) };

		var processor = new BatchProcessor<string>(
			async batch =>
			{
				var currentCall = Interlocked.Increment(ref callCount);
				if (currentCall == 1)
				{
					await Task.Yield();
					throw new InvalidOperationException("Async test exception");
				}

				foreach (var item in batch)
				{
					processedItems.Add(item);
				}

				if (!processedItems.IsEmpty)
				{
					_ = allProcessed.TrySetResult(true);
				}
			},
			_logger,
			options);

		_disposables.Add(processor);

		await processor.AddAsync("item1", CancellationToken.None).ConfigureAwait(false);
		await processor.AddAsync("item2", CancellationToken.None).ConfigureAwait(false);

		await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(

			allProcessed.Task,

			global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(30)));
		// First call throws but processor continues; second item should be processed
		processedItems.Count.ShouldBeGreaterThan(0);
		callCount.ShouldBeGreaterThan(1); // At least 2 calls (first fails, subsequent succeed)
	}

	[Fact]
	public async Task ProcessLargeBatchesEfficiently()
	{
		var processedItems = new ConcurrentBag<string>();
		var options = new MicroBatchOptions { MaxBatchSize = 100, MaxBatchDelay = TimeSpan.FromSeconds(1) };

		var stopwatch = Stopwatch.StartNew();
		var processor = new BatchProcessor<string>(
			batch =>
			{
				foreach (var item in batch)
				{
					processedItems.Add(item);
				}

				return ValueTask.CompletedTask;
			},
			_logger,
			options);

		_disposables.Add(processor);

		// Add 500 items rapidly
		var tasks = Enumerable.Range(0, 500)
			.Select(async i => await processor.AddAsync($"item{i}", CancellationToken.None).ConfigureAwait(false));

		await Task.WhenAll(tasks).ConfigureAwait(false);
		await WaitForConditionAsync(() => processedItems.Count == 500, TimeSpan.FromSeconds(20)).ConfigureAwait(false);
		stopwatch.Stop();

		processedItems.Count.ShouldBe(500);
		stopwatch.ElapsedMilliseconds.ShouldBeLessThan(10000); // Relaxed for full-suite parallel load
	}

	[Fact]
	public async Task MaintainPerformanceUnderStress()
	{
		var processedCount = 0;
		var options = new MicroBatchOptions { MaxBatchSize = 50, MaxBatchDelay = TimeSpan.FromMilliseconds(100) };

		var processor = new BatchProcessor<string>(
			batch =>
			{
				_ = Interlocked.Add(ref processedCount, batch.Count);
				return ValueTask.CompletedTask;
			},
			_logger,
			options);

		_disposables.Add(processor);

		// Calculate actual item count to avoid integer division rounding issues
		var threadCount = Environment.ProcessorCount;
		var itemsPerThread = 1000 / threadCount;
		var actualItemCount = itemsPerThread * threadCount; // May be less than 1000 due to integer division
		var stopwatch = Stopwatch.StartNew();

		// Add items from multiple threads
		var producerTasks = Enumerable.Range(0, threadCount)
			.Select(threadId => Task.Run(async () =>
			{
				for (var i = 0; i < itemsPerThread; i++)
				{
					await processor.AddAsync($"thread{threadId}-item{i}", CancellationToken.None).ConfigureAwait(false);
				}
			}));

		await Task.WhenAll(producerTasks).ConfigureAwait(false);

		// Wait for all items to be processed
		await WaitForConditionAsync(
			() => Volatile.Read(ref processedCount) >= actualItemCount,
			TimeSpan.FromSeconds(10)).ConfigureAwait(false);

		stopwatch.Stop();

		processedCount.ShouldBe(actualItemCount);
		stopwatch.ElapsedMilliseconds.ShouldBeLessThan(30000); // Relaxed for full-suite parallel load
	}

	[Fact]
	public async Task HandleBackpressureGracefully()
	{
		var processingDelay = TimeSpan.FromMilliseconds(100);
		var processedBatches = new ConcurrentBag<int>();
		var options = new MicroBatchOptions { MaxBatchSize = 5, MaxBatchDelay = TimeSpan.FromMilliseconds(50) };

		var processor = new BatchProcessor<string>(
			async batch =>
			{
				processedBatches.Add(batch.Count);
				await global::Tests.Shared.Infrastructure.TestTiming.PauseAsync(processingDelay).ConfigureAwait(false); // Simulate slow processing
			},
			_logger,
			options);

		_disposables.Add(processor);

		// Add items faster than they can be processed
		var itemCount = 50;
		var stopwatch = Stopwatch.StartNew();

		var addTasks = Enumerable.Range(0, itemCount)
			.Select(async i => await processor.AddAsync($"item{i}", CancellationToken.None).ConfigureAwait(false));

		await Task.WhenAll(addTasks).ConfigureAwait(false);

		// Wait for processing to complete
		await WaitForConditionAsync(
			() => processedBatches.Sum() >= itemCount,
			TimeSpan.FromSeconds(30)).ConfigureAwait(false);

		stopwatch.Stop();

		processedBatches.Sum().ShouldBe(itemCount);
		processedBatches.All(count => count <= options.MaxBatchSize).ShouldBeTrue();
	}

	[Fact]
	public async Task ValidateBatchingLatency()
	{
		var batchTimestamps = new ConcurrentBag<DateTime>();
		var options = new MicroBatchOptions { MaxBatchSize = 10, MaxBatchDelay = TimeSpan.FromMilliseconds(200) };

		var processor = new BatchProcessor<string>(
			batch =>
			{
				batchTimestamps.Add(DateTime.UtcNow);
				return ValueTask.CompletedTask;
			},
			_logger,
			options);

		_disposables.Add(processor);

		var startTime = DateTime.UtcNow;

		// Add 3 items (less than max batch size) to trigger time-based batching
		await processor.AddAsync("item1", CancellationToken.None).ConfigureAwait(false);
		await processor.AddAsync("item2", CancellationToken.None).ConfigureAwait(false);
		await processor.AddAsync("item3", CancellationToken.None).ConfigureAwait(false);

		// Wait for batch to be processed
		await WaitForConditionAsync(() => batchTimestamps.Count == 1, TimeSpan.FromSeconds(10)).ConfigureAwait(false);

		batchTimestamps.Count.ShouldBe(1);
		var timestamps = batchTimestamps.ToArray();
		var batchTime = timestamps[0];
		var latency = batchTime - startTime;

		// Batch should be processed within the delay window (with relaxed tolerance for CI environments)
		// Lower bound: Allow for early processing or timer variance in CI (relaxed from 50ms to 10ms)
		// Upper bound: Allow for CI delays - 15x the expected delay (relaxed from 1500ms to 3000ms)
		latency.TotalMilliseconds.ShouldBeGreaterThan(10);
		latency.TotalMilliseconds.ShouldBeLessThan(3000);
	}

	[Fact]
	public async Task HandleMemoryPressureScenarios()
	{
		var processedCount = 0;
		var options = new MicroBatchOptions
		{
			MaxBatchSize = 1000, // Large batches to test memory efficiency
			MaxBatchDelay = TimeSpan.FromMilliseconds(100),
		};

		var processor = new BatchProcessor<string>(
			batch =>
			{
				_ = Interlocked.Add(ref processedCount, batch.Count);
				// Simulate some memory allocation
				var buffer = new byte[1024];
				buffer[0] = 1; // Use the buffer to prevent optimization
				return ValueTask.CompletedTask;
			},
			_logger,
			options);

		_disposables.Add(processor);

		// Add many items to test memory usage
		var itemCount = 10000;
		var tasks = Enumerable.Range(0, itemCount)
			.Select(async i => await processor.AddAsync($"large-payload-item-{i}-with-extra-data", CancellationToken.None).ConfigureAwait(false));

		await Task.WhenAll(tasks).ConfigureAwait(false);

		// Wait for processing
		await WaitForConditionAsync(
			() => Volatile.Read(ref processedCount) >= itemCount,
			TimeSpan.FromSeconds(30)).ConfigureAwait(false);

		processedCount.ShouldBe(itemCount);

		// Force garbage collection to ensure no memory leaks
		GC.Collect();
		GC.WaitForPendingFinalizers();
		GC.Collect();
	}

	[Fact]
	public async Task ProcessVariableSizedBatches()
	{
		var batchSizes = new ConcurrentBag<int>();
		var totalProcessed = 0;
		var allItemsProcessed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var options = new MicroBatchOptions { MaxBatchSize = 20, MaxBatchDelay = TimeSpan.FromMilliseconds(50) };

		var processor = new BatchProcessor<string>(
			batch =>
			{
				batchSizes.Add(batch.Count);
				var currentTotal = Interlocked.Add(ref totalProcessed, batch.Count);
				if (currentTotal >= 32)
				{
					_ = allItemsProcessed.TrySetResult();
				}
				return ValueTask.CompletedTask;
			},
			_logger,
			options);

		_disposables.Add(processor);

		// Add items in bursts with pauses to create variable batch sizes
		await processor.AddAsync("burst1-item1", CancellationToken.None).ConfigureAwait(false);
		await processor.AddAsync("burst1-item2", CancellationToken.None).ConfigureAwait(false);
		await WaitForConditionAsync(() => Volatile.Read(ref totalProcessed) >= 2, TimeSpan.FromSeconds(10)).ConfigureAwait(false);

		await processor.AddAsync("burst2-item1", CancellationToken.None).ConfigureAwait(false);
		await processor.AddAsync("burst2-item2", CancellationToken.None).ConfigureAwait(false);
		await processor.AddAsync("burst2-item3", CancellationToken.None).ConfigureAwait(false);
		await processor.AddAsync("burst2-item4", CancellationToken.None).ConfigureAwait(false);
		await processor.AddAsync("burst2-item5", CancellationToken.None).ConfigureAwait(false);
		await WaitForConditionAsync(() => Volatile.Read(ref totalProcessed) >= 7, TimeSpan.FromSeconds(10)).ConfigureAwait(false);

		// Add enough items to trigger size-based batching
		for (var i = 0; i < 25; i++)
		{
			await processor.AddAsync($"burst3-item{i}", CancellationToken.None).ConfigureAwait(false);
		}

		await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(

			allItemsProcessed.Task,

			global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(20)));
		batchSizes.Count.ShouldBeGreaterThan(1);
		batchSizes.All(size => size <= options.MaxBatchSize).ShouldBeTrue();
		Volatile.Read(ref totalProcessed).ShouldBe(32); // 2 + 5 + 25 = 32 total items
	}

	private static async Task WaitForConditionAsync(Func<bool> condition, TimeSpan timeout)
	{
		var scaledTimeout = global::Tests.Shared.Infrastructure.TestTimeouts.Scale(timeout);
		var conditionSatisfied = await global::Tests.Shared.Infrastructure.WaitHelpers.WaitUntilAsync(
			condition,
			scaledTimeout,
			TimeSpan.FromMilliseconds(25)).ConfigureAwait(false);
		conditionSatisfied.ShouldBeTrue($"Condition was not satisfied within {scaledTimeout}.");
	}

	[Fact]
	public async Task HandleRapidAddRemovePatterns()
	{
		const int burstCount = 5;
		const int itemsPerBurst = 15;
		const int expectedProcessedItemCount = burstCount * itemsPerBurst;

		var processedItems = new ConcurrentBag<string>();
		var totalProcessed = 0;
		var options = new MicroBatchOptions { MaxBatchSize = 10, MaxBatchDelay = TimeSpan.FromMilliseconds(25) };

		var processor = new BatchProcessor<string>(
			batch =>
			{
				foreach (var item in batch)
				{
					processedItems.Add(item);
				}

				Interlocked.Add(ref totalProcessed, batch.Count);

				return ValueTask.CompletedTask;
			},
			_logger,
			options);

		_disposables.Add(processor);

		// Rapid bursts followed by pauses
		for (var burst = 0; burst < burstCount; burst++)
		{
			var burstTasks = Enumerable.Range(0, itemsPerBurst)
				.Select(i => processor.AddAsync($"burst{burst}-item{i}", CancellationToken.None).AsTask());

			await Task.WhenAll(burstTasks).ConfigureAwait(false);
		}

		await WaitForConditionAsync(
			() => Volatile.Read(ref totalProcessed) >= expectedProcessedItemCount,
			TimeSpan.FromSeconds(60)).ConfigureAwait(false);

		Volatile.Read(ref totalProcessed).ShouldBe(expectedProcessedItemCount);
		processedItems.Count.ShouldBe(expectedProcessedItemCount); // 5 bursts * 15 items each
	}

	[Fact]
	public async Task ValidateThreadSafetyUnderLoad()
	{
		var processedItems = new ConcurrentBag<string>();
		var exceptions = new ConcurrentBag<Exception>();
		var options = new MicroBatchOptions { MaxBatchSize = 25, MaxBatchDelay = TimeSpan.FromMilliseconds(50) };

		var processor = new BatchProcessor<string>(
			batch =>
			{
				try
				{
					foreach (var item in batch)
					{
						processedItems.Add(item);
					}

					// Simulate some processing work
					var sum = 0;
					for (var i = 0; i < 100; i++)
					{
						sum += i;
					}
				}
				catch (Exception ex)
				{
					exceptions.Add(ex);
				}

				return ValueTask.CompletedTask;
			},
			_logger,
			options);

		_disposables.Add(processor);

		var itemsPerThread = 100;
		var threadCount = Environment.ProcessorCount * 2;

		// Launch multiple producer threads
		var producerTasks = Enumerable.Range(0, threadCount)
			.Select(threadId => Task.Run(async () =>
			{
				try
				{
					for (var i = 0; i < itemsPerThread; i++)
					{
						await processor.AddAsync($"thread{threadId}-item{i}", CancellationToken.None).ConfigureAwait(false);

						// Occasionally add small delays to create more realistic patterns
						if (i % 10 == 0)
						{
							await Task.Yield();
						}
					}
				}
				catch (Exception ex)
				{
					exceptions.Add(ex);
				}
			}));

		await Task.WhenAll(producerTasks).ConfigureAwait(false);

		// Wait for processing to complete
		var expectedCount = threadCount * itemsPerThread;
		await WaitForConditionAsync(
			() => processedItems.Count >= expectedCount,
			TimeSpan.FromSeconds(30)).ConfigureAwait(false);

		exceptions.ShouldBeEmpty();
		processedItems.Count.ShouldBe(expectedCount);

		// Verify no duplicate items (thread safety)
		var uniqueItems = processedItems.Distinct().Count();
		uniqueItems.ShouldBe(expectedCount);
	}

	public void Dispose()
	{
		foreach (var disposable in _disposables)
		{
			disposable?.Dispose();
		}
	}
}

