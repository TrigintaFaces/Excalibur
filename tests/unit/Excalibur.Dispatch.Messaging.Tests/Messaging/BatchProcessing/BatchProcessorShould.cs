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
public sealed class BatchProcessorShould : IAsyncDisposable
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
		var itemProcessed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
		var options = new MicroBatchOptions
		{
			MaxBatchSize = 1,
			MaxBatchDelay = TimeSpan.FromMilliseconds(25),
		};

		var processor = new BatchProcessor<string>(
			batch =>
			{
				processedBatches.Add(batch);
				_ = itemProcessed.TrySetResult(true);
				return ValueTask.CompletedTask;
			},
			_logger,
			options);

		_disposables.Add(processor);

		await processor.AddAsync("item1", CancellationToken.None).ConfigureAwait(false);
		await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
			itemProcessed.Task,
			global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(60))).ConfigureAwait(false);
		await processor.DisposeAsync().ConfigureAwait(false);
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
		var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

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

		var processedBatches = new ConcurrentQueue<IReadOnlyList<string>>();
		var batchObserved = new TaskCompletionSource<IReadOnlyList<string>>(TaskCreationOptions.RunContinuationsAsynchronously);

		var processor = new BatchProcessor<string>(
			batch =>
			{
				var processedBatch = batch.ToArray();
				processedBatches.Enqueue(processedBatch);
				batchObserved.TrySetResult(processedBatch);
				return ValueTask.CompletedTask;
			},
			_logger,
			options);

		_disposables.Add(processor);

		await processor.AddAsync("item1", CancellationToken.None).ConfigureAwait(false);
		await processor.AddAsync("item2", CancellationToken.None).ConfigureAwait(false);

		var batch = await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
			batchObserved.Task,
			global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(45))).ConfigureAwait(false);

		processedBatches.Count.ShouldBe(1);
		batch.ShouldNotBeNull();
		batch.Count.ShouldBe(2);
		batch.ShouldBe(TwoItemBatch);
	}

	[Fact]
	public async Task HandleBatchProcessorExceptions()
	{
		var callCount = 0;
		var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

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
		var processedItemCount = 0;

		var processor = new BatchProcessor<string>(
			batch =>
			{
				processedBatches.Add(batch);
				_ = Interlocked.Add(ref processedItemCount, batch.Count);
				return ValueTask.CompletedTask;
			},
			_logger,
			new MicroBatchOptions
			{
				MaxBatchSize = 10,
				MaxBatchDelay = TimeSpan.FromSeconds(10),
			});

		_disposables.Add(processor);

		await processor.AddAsync("item1", CancellationToken.None).ConfigureAwait(false);
		await processor.AddAsync("item2", CancellationToken.None).ConfigureAwait(false);
		await processor.DisposeAsync().ConfigureAwait(false);
		Volatile.Read(ref processedItemCount).ShouldBe(2);
		processedBatches.Count.ShouldBe(1);
		var batches = processedBatches.ToArray();
		batches[0].ShouldBe(TwoItemBatch);
	}

	[Fact]
	public async Task HandleConcurrentAdds()
	{
		var processedItems = new ConcurrentDictionary<string, byte>(StringComparer.Ordinal);
		var allItemsProcessed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		const int expectedItemCount = 32;
		var totalProcessed = 0;

		var processor = new BatchProcessor<string>(
			batch =>
			{
				foreach (var item in batch)
				{
					_ = processedItems.TryAdd(item, 0);
				}

				if (Interlocked.Add(ref totalProcessed, batch.Count) >= expectedItemCount)
				{
					_ = allItemsProcessed.TrySetResult();
				}

				return ValueTask.CompletedTask;
			},
			_logger,
			new MicroBatchOptions { MaxBatchSize = 8, MaxBatchDelay = TimeSpan.FromMilliseconds(10) });

		_disposables.Add(processor);

		var tasks = Enumerable.Range(0, expectedItemCount)
			.Select(async i => await processor.AddAsync($"item{i}", CancellationToken.None).ConfigureAwait(false));

		await Task.WhenAll(tasks).ConfigureAwait(false);
		await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
			allItemsProcessed.Task,
			global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(60)));
		Volatile.Read(ref totalProcessed).ShouldBe(expectedItemCount);
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
		var itemProcessed = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
		var options = new MicroBatchOptions
		{
			MaxBatchSize = 1,
			MaxBatchDelay = TimeSpan.FromSeconds(10),
		};

		var processor = new BatchProcessor<string>(
			batch =>
			{
				if (batch.Count > 0)
				{
					_ = itemProcessed.TrySetResult(true);
				}

				return ValueTask.CompletedTask;
			},
			_logger,
			options);

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
		var totalProcessed = 0;
		var allItemsProcessed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var options = new MicroBatchOptions { MaxBatchSize = 5, MaxBatchDelay = TimeSpan.FromMilliseconds(10) };

		var processor = new BatchProcessor<string>(
			batch =>
			{
				foreach (var item in batch)
				{
					processedItems.Add(item);
				}

				if (Interlocked.Add(ref totalProcessed, batch.Count) >= 50)
				{
					_ = allItemsProcessed.TrySetResult();
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

		await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
			allItemsProcessed.Task,
			global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(30))).ConfigureAwait(false);

		Volatile.Read(ref totalProcessed).ShouldBe(50);
		processedItems.Count.ShouldBe(50);
	}

	[Fact]
	public async Task RespectMaxBatchSizeExactly()
	{
		var options = new MicroBatchOptions { MaxBatchSize = 5, MaxBatchDelay = TimeSpan.FromSeconds(10) };

		var processedBatches = new ConcurrentQueue<IReadOnlyList<string>>();
		var batchCount = 0;
		var firstBatchProcessed = new TaskCompletionSource<IReadOnlyList<string>>(TaskCreationOptions.RunContinuationsAsynchronously);
		var secondBatchProcessed = new TaskCompletionSource<IReadOnlyList<string>>(TaskCreationOptions.RunContinuationsAsynchronously);

		var processor = new BatchProcessor<string>(
			batch =>
			{
				var processedBatch = batch.ToArray();
				processedBatches.Enqueue(processedBatch);
				var observedBatchCount = Interlocked.Increment(ref batchCount);
				if (observedBatchCount == 1)
				{
					_ = firstBatchProcessed.TrySetResult(processedBatch);
				}
				else if (observedBatchCount == 2)
				{
					_ = secondBatchProcessed.TrySetResult(processedBatch);
				}
				return ValueTask.CompletedTask;
			},
			_logger,
			options);

		_disposables.Add(processor);

		// Add the first full batch and wait until it is observed.
		for (var i = 0; i < 5; i++)
		{
			await processor.AddAsync($"item{i}", CancellationToken.None).ConfigureAwait(false);
		}

		var firstBatch = await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
			firstBatchProcessed.Task,
			global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(45))).ConfigureAwait(false);
		firstBatch.Count.ShouldBe(options.MaxBatchSize);

		// Add the second full batch and wait until it is observed.
		for (var i = 5; i < 10; i++)
		{
			await processor.AddAsync($"item{i}", CancellationToken.None).ConfigureAwait(false);
		}

		var secondBatch = await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
			secondBatchProcessed.Task,
			global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(45))).ConfigureAwait(false);
		secondBatch.Count.ShouldBe(options.MaxBatchSize);

		Volatile.Read(ref batchCount).ShouldBe(2);
		processedBatches.Count.ShouldBe(2);
	}

	[Fact]
	public async Task HandleAsyncBatchProcessorExceptions()
	{
		var callCount = 0;
		var processedItems = new ConcurrentBag<string>();
		var firstFailureObserved = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var successfulRetryObserved = new TaskCompletionSource<IReadOnlyList<string>>(TaskCreationOptions.RunContinuationsAsynchronously);

		// Use batch size of 1 to ensure each item is processed separately
		var options = new MicroBatchOptions { MaxBatchSize = 1, MaxBatchDelay = TimeSpan.FromMilliseconds(10) };

		var processor = new BatchProcessor<string>(
			async batch =>
			{
				var currentCall = Interlocked.Increment(ref callCount);
				if (currentCall == 1)
				{
					_ = firstFailureObserved.TrySetResult();
					await Task.Yield();
					throw new InvalidOperationException("Async test exception");
				}

				foreach (var item in batch)
				{
					processedItems.Add(item);
				}

				if (currentCall >= 2 && !processedItems.IsEmpty)
				{
					_ = successfulRetryObserved.TrySetResult(batch.ToArray());
				}
			},
			_logger,
			options);

		_disposables.Add(processor);

		await processor.AddAsync("item1", CancellationToken.None).ConfigureAwait(false);
		await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
			firstFailureObserved.Task,
			global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(45))).ConfigureAwait(false);
		await processor.AddAsync("item2", CancellationToken.None).ConfigureAwait(false);

		var successfulBatch = await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
			successfulRetryObserved.Task,
			global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(45))).ConfigureAwait(false);
		// First call throws but processor continues; second item should be processed
		successfulBatch.Count.ShouldBe(1);
		successfulBatch[0].ShouldBe("item2");
		processedItems.Count.ShouldBeGreaterThan(0);
		callCount.ShouldBeGreaterThan(1); // At least 2 calls (first fails, subsequent succeed)
	}

	[Fact]
	public async Task ProcessLargeBatchesEfficiently()
	{
		var processedItems = new ConcurrentDictionary<string, byte>();
		var options = new MicroBatchOptions { MaxBatchSize = 100, MaxBatchDelay = TimeSpan.FromSeconds(1) };
		var observedBatchSizes = new ConcurrentBag<int>();
		var totalProcessed = 0;
		var allItemsProcessed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

		var processor = new BatchProcessor<string>(
			batch =>
			{
				observedBatchSizes.Add(batch.Count);
				foreach (var item in batch)
				{
					_ = processedItems.TryAdd(item, 0);
				}

				if (Interlocked.Add(ref totalProcessed, batch.Count) >= 500)
				{
					_ = allItemsProcessed.TrySetResult();
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
		await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
			allItemsProcessed.Task,
			global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(60))).ConfigureAwait(false);

		Volatile.Read(ref totalProcessed).ShouldBe(500);
		processedItems.Count.ShouldBe(500);
		observedBatchSizes.Count.ShouldBeGreaterThan(0);
		observedBatchSizes.All(size => size > 0 && size <= options.MaxBatchSize).ShouldBeTrue();
	}

	[Fact]
	public async Task MaintainPerformanceUnderStress()
	{
		var processedCount = 0;
		var allItemsProcessed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		// Calculate actual item count to avoid integer division rounding issues
		const int targetItemCount = 1000;
		var threadCount = Math.Max(1, Math.Min(Environment.ProcessorCount, 8));
		var itemsPerThread = targetItemCount / threadCount;
		var actualItemCount = itemsPerThread * threadCount;
		var options = new MicroBatchOptions { MaxBatchSize = 50, MaxBatchDelay = TimeSpan.FromMilliseconds(100) };

		var processor = new BatchProcessor<string>(
			batch =>
			{
				if (Interlocked.Add(ref processedCount, batch.Count) >= actualItemCount)
				{
					_ = allItemsProcessed.TrySetResult();
				}
				return ValueTask.CompletedTask;
			},
			_logger,
			options);

		_disposables.Add(processor);

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

		await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
			allItemsProcessed.Task,
			global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(60))).ConfigureAwait(false);

		processedCount.ShouldBe(actualItemCount);
	}

	[Fact]
	public async Task HandleBackpressureGracefully()
	{
		var processedBatches = new ConcurrentQueue<IReadOnlyList<string>>();
		var firstBatchStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var releaseFirstBatch = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var secondBatchObserved = new TaskCompletionSource<IReadOnlyList<string>>(TaskCreationOptions.RunContinuationsAsynchronously);
		var batchCount = 0;
		var options = new MicroBatchOptions { MaxBatchSize = 2, MaxBatchDelay = TimeSpan.FromSeconds(1) };

		var processor = new BatchProcessor<string>(
			async batch =>
			{
				var processedBatch = batch.ToArray();
				processedBatches.Enqueue(processedBatch);
				var observedBatchCount = Interlocked.Increment(ref batchCount);
				if (observedBatchCount == 1)
				{
					_ = firstBatchStarted.TrySetResult();
					await releaseFirstBatch.Task.ConfigureAwait(false);
				}
				else if (observedBatchCount == 2)
				{
					_ = secondBatchObserved.TrySetResult(processedBatch);
				}
			},
			_logger,
			options);

		_disposables.Add(processor);

		await processor.AddAsync("item0", CancellationToken.None).ConfigureAwait(false);
		await processor.AddAsync("item1", CancellationToken.None).ConfigureAwait(false);
		await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
			firstBatchStarted.Task,
			global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(45))).ConfigureAwait(false);
		await processor.AddAsync("item2", CancellationToken.None).ConfigureAwait(false);
		await processor.AddAsync("item3", CancellationToken.None).ConfigureAwait(false);
		_ = releaseFirstBatch.TrySetResult();
		var secondBatch = await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
			secondBatchObserved.Task,
			global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(45))).ConfigureAwait(false);

		Volatile.Read(ref batchCount).ShouldBe(2);
		processedBatches.Count.ShouldBe(2);
		secondBatch.Count.ShouldBe(2);
		processedBatches.All(batch => batch.Count <= options.MaxBatchSize).ShouldBeTrue();
	}

	[Fact]
	public async Task ValidateBatchingLatency()
	{
		var processedBatches = new ConcurrentQueue<IReadOnlyList<string>>();
		var batchObserved = new TaskCompletionSource<IReadOnlyList<string>>(TaskCreationOptions.RunContinuationsAsynchronously);
		var options = new MicroBatchOptions { MaxBatchSize = 10, MaxBatchDelay = TimeSpan.FromMilliseconds(10) };

		var processor = new BatchProcessor<string>(
			batch =>
			{
				var processedBatch = batch.ToArray();
				processedBatches.Enqueue(processedBatch);
				_ = batchObserved.TrySetResult(processedBatch);
				return ValueTask.CompletedTask;
			},
			_logger,
			options);

		_disposables.Add(processor);

		// Add 3 items (less than max batch size) to trigger time-based batching
		await processor.AddAsync("item1", CancellationToken.None).ConfigureAwait(false);
		await processor.AddAsync("item2", CancellationToken.None).ConfigureAwait(false);
		await processor.AddAsync("item3", CancellationToken.None).ConfigureAwait(false);

		var batch = await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
			batchObserved.Task,
			global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(60))).ConfigureAwait(false);

		processedBatches.Count.ShouldBe(1);
		batch.ShouldNotBeNull();
		batch.Count.ShouldBe(3);
		batch.ShouldContain("item1");
		batch.ShouldContain("item2");
		batch.ShouldContain("item3");
	}

	[Fact]
	public async Task HandleMemoryPressureScenarios()
	{
		var processedCount = 0;
		var allItemsProcessed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		// Add many items to test memory usage
		var itemCount = 10000;
		var options = new MicroBatchOptions
		{
			MaxBatchSize = 1000, // Large batches to test memory efficiency
			MaxBatchDelay = TimeSpan.FromMilliseconds(100),
		};

		var processor = new BatchProcessor<string>(
			batch =>
			{
				if (Interlocked.Add(ref processedCount, batch.Count) >= itemCount)
				{
					_ = allItemsProcessed.TrySetResult();
				}
				// Simulate some memory allocation
				var buffer = new byte[1024];
				buffer[0] = 1; // Use the buffer to prevent optimization
				return ValueTask.CompletedTask;
			},
			_logger,
			options);

		_disposables.Add(processor);
		var tasks = Enumerable.Range(0, itemCount)
			.Select(async i => await processor.AddAsync($"large-payload-item-{i}-with-extra-data", CancellationToken.None).ConfigureAwait(false));

		await Task.WhenAll(tasks).ConfigureAwait(false);

		await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
			allItemsProcessed.Task,
			global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(30))).ConfigureAwait(false);

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
		// Use per-burst TCS signals instead of Task.Delay to enforce batch boundaries
		var burst1Processed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var burst2Processed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var options = new MicroBatchOptions { MaxBatchSize = 20, MaxBatchDelay = TimeSpan.FromMilliseconds(50) };

		var processor = new BatchProcessor<string>(
			batch =>
			{
				batchSizes.Add(batch.Count);
				var currentTotal = Interlocked.Add(ref totalProcessed, batch.Count);
				// Signal when burst1 items (2) are processed
				if (currentTotal >= 2)
				{
					_ = burst1Processed.TrySetResult();
				}
				// Signal when burst1 + burst2 items (2 + 5 = 7) are processed
				if (currentTotal >= 7)
				{
					_ = burst2Processed.TrySetResult();
				}
				if (currentTotal >= 32)
				{
					_ = allItemsProcessed.TrySetResult();
				}
				return ValueTask.CompletedTask;
			},
			_logger,
			options);

		_disposables.Add(processor);

		// Burst 1: Add 2 items, then wait for them to be processed before next burst
		await processor.AddAsync("burst1-item1", CancellationToken.None).ConfigureAwait(false);
		await processor.AddAsync("burst1-item2", CancellationToken.None).ConfigureAwait(false);
		await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
			burst1Processed.Task,
			global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(30))).ConfigureAwait(false);

		// Burst 2: Add 5 items, then wait for them to be processed
		await processor.AddAsync("burst2-item1", CancellationToken.None).ConfigureAwait(false);
		await processor.AddAsync("burst2-item2", CancellationToken.None).ConfigureAwait(false);
		await processor.AddAsync("burst2-item3", CancellationToken.None).ConfigureAwait(false);
		await processor.AddAsync("burst2-item4", CancellationToken.None).ConfigureAwait(false);
		await processor.AddAsync("burst2-item5", CancellationToken.None).ConfigureAwait(false);
		await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
			burst2Processed.Task,
			global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(30))).ConfigureAwait(false);

		// Burst 3: Add 25 items to trigger size-based batching
		for (var i = 0; i < 25; i++)
		{
			await processor.AddAsync($"burst3-item{i}", CancellationToken.None).ConfigureAwait(false);
		}

		await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
			allItemsProcessed.Task,
			global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(45)));
		batchSizes.Count.ShouldBeGreaterThan(1);
		batchSizes.All(size => size <= options.MaxBatchSize).ShouldBeTrue();
		Volatile.Read(ref totalProcessed).ShouldBe(32); // 2 + 5 + 25 = 32 total items
	}

	[Fact]
	public async Task HandleRapidAddRemovePatterns()
	{
		const int burstCount = 5;
		const int itemsPerBurst = 15;
		const int expectedProcessedItemCount = burstCount * itemsPerBurst;

		var processedItems = new ConcurrentBag<string>();
		var totalProcessed = 0;
		var allItemsProcessed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		var options = new MicroBatchOptions { MaxBatchSize = 10, MaxBatchDelay = TimeSpan.FromMilliseconds(25) };

		var processor = new BatchProcessor<string>(
			batch =>
			{
				foreach (var item in batch)
				{
					processedItems.Add(item);
				}

				if (Interlocked.Add(ref totalProcessed, batch.Count) >= expectedProcessedItemCount)
				{
					_ = allItemsProcessed.TrySetResult();
				}

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

		await global::Tests.Shared.Infrastructure.WaitHelpers.AwaitSignalAsync(
			allItemsProcessed.Task,
			global::Tests.Shared.Infrastructure.TestTimeouts.Scale(TimeSpan.FromSeconds(60))).ConfigureAwait(false);

		Volatile.Read(ref totalProcessed).ShouldBe(expectedProcessedItemCount);
		processedItems.Count.ShouldBe(expectedProcessedItemCount); // 5 bursts * 15 items each
	}

	[Fact]
	public async Task ValidateThreadSafetyUnderLoad()
	{
		var processedItems = new ConcurrentBag<string>();
		var exceptions = new ConcurrentBag<Exception>();
		var totalProcessed = 0;
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

					_ = Interlocked.Add(ref totalProcessed, batch.Count);

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
		var threadCount = Math.Min(Environment.ProcessorCount, 8);

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
		var expectedCount = threadCount * itemsPerThread;
		await processor.DisposeAsync().ConfigureAwait(false);
		_ = _disposables.Remove(processor);

		exceptions.ShouldBeEmpty();
		Volatile.Read(ref totalProcessed).ShouldBe(expectedCount);
		processedItems.Count.ShouldBe(expectedCount);

		// Verify no duplicate items (thread safety)
		var uniqueItems = processedItems.Distinct().Count();
		uniqueItems.ShouldBe(expectedCount);
	}

	public async ValueTask DisposeAsync()
	{
		foreach (var disposable in _disposables)
		{
			if (disposable is not null)
			{
				await disposable.DisposeAsync().ConfigureAwait(false);
			}
		}
	}
}

