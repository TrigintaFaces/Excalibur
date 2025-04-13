using Excalibur.DataAccess;

using Shouldly;

namespace Excalibur.Tests.Unit.DataAccess;

public class InMemoryDataQueueExtendedShould
{
	[Fact]
	public async Task DequeueBatchReturnsAvailableItemsWhenChannelCompleted()
	{
		// Arrange
		using var queue = new InMemoryDataQueue<int>(100);
		await queue.EnqueueAsync(1).ConfigureAwait(true);
		await queue.EnqueueAsync(2).ConfigureAwait(true);
		queue.CompleteWriter();

		// Act
		var batch = await queue.DequeueBatchAsync(10).ConfigureAwait(true);

		// Assert
		batch.Count.ShouldBe(2);
		batch.ShouldContain(1);
		batch.ShouldContain(2);
	}

	[Fact]
	public async Task DequeueAllExitsWhenChannelCompleted()
	{
		// Arrange
		using var queue = new InMemoryDataQueue<int>();
		await queue.EnqueueAsync(5).ConfigureAwait(true);
		queue.CompleteWriter();

		// Act
		var items = new List<int>();
		await foreach (var item in queue.DequeueAllAsync().ConfigureAwait(true))
		{
			items.Add(item);
		}

		// Assert
		items.ShouldBe(new[] { 5 });
	}

	[Fact]
	public async Task DequeueBatchShouldExitWhenDisposed()
	{
		// Arrange
		var queue = new InMemoryDataQueue<int>();

		var dequeueTask = Task.Run(async () => await queue.DequeueBatchAsync(10).ConfigureAwait(true));

		// Act
		await Task.Delay(50).ConfigureAwait(true); // Give it a moment to start waiting
		await queue.DisposeAsync().ConfigureAwait(true);

		// Assert
		var result = await dequeueTask.ConfigureAwait(true);
		result.ShouldBeEmpty();
	}

	[Fact]
	public async Task NullEnqueueDoesNotAffectCount()
	{
		// Arrange
		using var queue = new InMemoryDataQueue<string?>();

		// Act
		await queue.EnqueueAsync(null).ConfigureAwait(true);
		await queue.EnqueueAsync("test").ConfigureAwait(true);

		// Assert
		queue.Count.ShouldBe(1); // Only the non-null counts
	}

	[Fact]
	public async Task RespectCancellationTokenDuringEnqueue()
	{
		// Arrange
		var queue = new InMemoryDataQueue<int>(10);
		using var cts = new CancellationTokenSource();

		// Act
		await cts.CancelAsync().ConfigureAwait(true);

		// Assert
		_ = await Should.ThrowAsync<OperationCanceledException>(async () =>
		{
			await queue.EnqueueAsync(42, cts.Token).ConfigureAwait(true);
		}).ConfigureAwait(true);

		await queue.DisposeAsync().ConfigureAwait(true);
	}

	[Fact]
	public async Task RespectCancellationTokenDuringBatchEnqueue()
	{
		// Arrange
		var queue = new InMemoryDataQueue<int>(10);
		using var cts = new CancellationTokenSource();
		var items = Enumerable.Range(1, 100).ToList();

		// Act & Assert
		var task = queue.EnqueueBatchAsync(items, cts.Token);

		// Cancel during operation
		await Task.Delay(10).ConfigureAwait(true);
		await cts.CancelAsync().ConfigureAwait(true);

		_ = await Should.ThrowAsync<OperationCanceledException>(async () =>
		{
			await task.ConfigureAwait(true);
		}).ConfigureAwait(true);

		await queue.DisposeAsync().ConfigureAwait(true);
	}

	[Fact]
	public async Task MaintainCorrectCountDuringConcurrentOperations()
	{
		// Arrange
		var queue = new InMemoryDataQueue<int>(1000);
		const int itemCount = 500;

		// Act - Enqueue items concurrently
		var enqueueTask = Task.Run(async () =>
		{
			for (var i = 0; i < itemCount; i++)
			{
				await queue.EnqueueAsync(i).ConfigureAwait(true);
			}
		});

		// Give some time for enqueueing to start
		await Task.Delay(50).ConfigureAwait(true);

		// Start dequeuing concurrently
		var dequeueTask = Task.Run(async () =>
		{
			var items = new List<int>();
			while (items.Count < itemCount)
			{
				var batch = await queue.DequeueBatchAsync(50).ConfigureAwait(true);
				if (batch.Count > 0)
				{
					items.AddRange(batch);
				}
				else
				{
					await Task.Delay(10).ConfigureAwait(true);
				}
			}

			return items;
		});

		// Wait for both operations to complete
		await Task.WhenAll(enqueueTask, dequeueTask).ConfigureAwait(true);

		// Assert
		queue.Count.ShouldBe(0); // All items should be processed
		queue.HasPendingItems().ShouldBeFalse();

		var dequeuedItems = await dequeueTask.ConfigureAwait(true);
		dequeuedItems.Count.ShouldBe(itemCount);
		dequeuedItems.ShouldContain(0); // First item
		dequeuedItems.ShouldContain(itemCount - 1); // Last item

		await queue.DisposeAsync().ConfigureAwait(true);
	}

	[Fact]
	public async Task AllowMultipleDisposeCalls()
	{
		var queue = new InMemoryDataQueue<int>();
		await queue.EnqueueAsync(1).ConfigureAwait(true);

		await queue.DisposeAsync().ConfigureAwait(true);
		await queue.DisposeAsync().ConfigureAwait(true);

		queue.HasPendingItems().ShouldBeFalse();
		queue.Count.ShouldBe(0);
	}

	[Fact]
	public async Task HandleNullItemsGracefully()
	{
		// Arrange
		var queue = new InMemoryDataQueue<string?>();

		// Act
		await queue.EnqueueAsync(null).ConfigureAwait(true); // Null item
		await queue.EnqueueAsync("valid").ConfigureAwait(true);

		// Assert
		var batch = await queue.DequeueBatchAsync(10).ConfigureAwait(true);
		batch.Count.ShouldBe(1);
		batch[0].ShouldBe("valid");

		await queue.DisposeAsync().ConfigureAwait(true);
	}

	[Fact]
	public async Task ThrowWhenEnqueueBatchWithNullCollection()
	{
		// Arrange
		var queue = new InMemoryDataQueue<int>();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(async () =>
		{
			await queue.EnqueueBatchAsync(null!).ConfigureAwait(true);
		}).ConfigureAwait(true);

		await queue.DisposeAsync().ConfigureAwait(true);
	}

	[Fact]
	public async Task ExhaustReaderWhenQueueHasManyItems()
	{
		// Arrange
		var queue = new InMemoryDataQueue<int>(2000);
		const int itemCount = 1500;

		// Fill the queue
		for (var i = 0; i < itemCount; i++)
		{
			await queue.EnqueueAsync(i).ConfigureAwait(true);
		}

		queue.Count.ShouldBe(itemCount);

		// Act - Dequeue in multiple batches
		var totalItems = 0;
		var allItems = new List<int>();

		while (queue.HasPendingItems())
		{
			var batchSize = 200; // Smaller than total to ensure multiple batches
			var batch = await queue.DequeueBatchAsync(batchSize).ConfigureAwait(true);
			totalItems += batch.Count;
			allItems.AddRange(batch);
		}

		// Assert
		totalItems.ShouldBe(itemCount);
		queue.Count.ShouldBe(0);
		allItems.Count.ShouldBe(itemCount);
		allItems.ShouldContain(0); // First item
		allItems.ShouldContain(itemCount - 1); // Last item

		await queue.DisposeAsync().ConfigureAwait(true);
	}
}
