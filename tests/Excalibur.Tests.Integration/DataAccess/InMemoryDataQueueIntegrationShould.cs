using Excalibur.DataAccess;

using Shouldly;

namespace Excalibur.Tests.Integration.DataAccess;

public class InMemoryDataQueueIntegrationShould
{
	[Fact]
	public async Task EnqueueAndDequeueItems()
	{
		// Arrange
		using var queue = new InMemoryDataQueue<string>();
		var itemsToEnqueue = new[] { "Item1", "Item2", "Item3" };

		// Act
		foreach (var item in itemsToEnqueue)
		{
			await queue.EnqueueAsync(item).ConfigureAwait(true);
		}

		var dequeued = await queue.DequeueBatchAsync(10).ConfigureAwait(true);

		// Assert
		_ = dequeued.ShouldNotBeNull();
		// Using Count instead of Length since we're working with a collection
		dequeued.Count.ShouldBe(3);
		dequeued.ShouldContain("Item1");
		dequeued.ShouldContain("Item2");
		dequeued.ShouldContain("Item3");
		queue.IsEmpty().ShouldBeTrue();
	}

	[Fact]
	public async Task EnqueueBatchAndDequeueAllAsync()
	{
		// Arrange
		using var queue = new InMemoryDataQueue<string>();
		var itemsToEnqueue = new[] { "Item1", "Item2", "Item3", "Item4", "Item5" };

		// Act
		await queue.EnqueueBatchAsync(itemsToEnqueue).ConfigureAwait(true);

		var dequeued = new List<string>();
		await foreach (var item in queue.DequeueAllAsync().ConfigureAwait(true))
		{
			dequeued.Add(item);
		}

		// Assert
		dequeued.Count.ShouldBe(5);
		dequeued.ShouldBe(itemsToEnqueue);
		queue.IsEmpty().ShouldBeTrue();
	}

	[Fact]
	public async Task CompleteWriterAndDequeueRemainingItems()
	{
		// Arrange
		using var queue = new InMemoryDataQueue<string>();
		await queue.EnqueueAsync("Item1").ConfigureAwait(true);
		await queue.EnqueueAsync("Item2").ConfigureAwait(true);

		// Act
		queue.CompleteWriter(); // Mark the channel as complete for writing

		var items = new List<string>();
		await foreach (var item in queue.DequeueAllAsync().ConfigureAwait(true))
		{
			items.Add(item);
		}

		// Assert
		items.Count.ShouldBe(2);
		items.ShouldContain("Item1");
		items.ShouldContain("Item2");
		queue.IsEmpty().ShouldBeTrue();
	}

	[Fact]
	public async Task DequeueBatchWithSpecificSize()
	{
		// Arrange
		using var queue = new InMemoryDataQueue<int>();
		for (var i = 0; i < 10; i++)
		{
			await queue.EnqueueAsync(i).ConfigureAwait(true);
		}

		// Act
		var firstBatch = await queue.DequeueBatchAsync(3).ConfigureAwait(true);
		var secondBatch = await queue.DequeueBatchAsync(5).ConfigureAwait(true);
		var remainingItems = await queue.DequeueBatchAsync(10).ConfigureAwait(true);

		// Assert
		firstBatch.Count.ShouldBe(3);
		secondBatch.Count.ShouldBe(5);
		remainingItems.Count.ShouldBe(2);
		queue.IsEmpty().ShouldBeTrue();
	}

	[Fact]
	public async Task HasPendingItemsReturnTrueWhenItemsExist()
	{
		// Arrange
		using var queue = new InMemoryDataQueue<string>();

		// Act & Assert
		queue.HasPendingItems().ShouldBeFalse();

		await queue.EnqueueAsync("Test").ConfigureAwait(true);

		queue.HasPendingItems().ShouldBeTrue();
		queue.IsEmpty().ShouldBeFalse();

		_ = await queue.DequeueBatchAsync(1).ConfigureAwait(true);

		queue.HasPendingItems().ShouldBeFalse();
		queue.IsEmpty().ShouldBeTrue();
	}

	[Fact]
	public async Task HandleCancellationDuringDequeue()
	{
		// Arrange
		using var queue = new InMemoryDataQueue<string>();
		using var cts = new CancellationTokenSource();

		await queue.EnqueueAsync("Item1", cts.Token).ConfigureAwait(true);

		var dequeueTask = Task.Run(async () =>
		{
			var items = new List<string>();

			await foreach (var item in queue.DequeueAllAsync(cts.Token).ConfigureAwait(true))
			{
				items.Add(item);
				await cts.CancelAsync().ConfigureAwait(true);
			}

			return items;
		}, cts.Token);

		// Act
		var results = await dequeueTask.ConfigureAwait(true);

		// Assert
		results.ShouldContain("Item1");
		results.Count.ShouldBe(1);
		queue.IsEmpty().ShouldBeTrue();
	}

	[Fact]
	public async Task RespectCapacityLimit()
	{
		// Arrange
		const int capacity = 5;
		using var queue = new InMemoryDataQueue<int>(capacity);

		// Act
		for (var i = 0; i < capacity; i++)
		{
			await queue.EnqueueAsync(i).ConfigureAwait(true);
		}

		// Assert
		queue.HasPendingItems().ShouldBeTrue();

		// Act & Assert - Dequeue should work normally
		var items = await queue.DequeueBatchAsync(capacity).ConfigureAwait(true);
		items.Count.ShouldBe(capacity);
		queue.IsEmpty().ShouldBeTrue();
	}

	[Fact]
	public void ThrowArgumentOutOfRangeExceptionWhenCapacityIsZeroOrNegative()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() => new InMemoryDataQueue<int>(0));
		_ = Should.Throw<ArgumentOutOfRangeException>(() => new InMemoryDataQueue<int>(-1));
	}
}
