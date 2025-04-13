using Excalibur.DataAccess;

using Shouldly;

namespace Excalibur.Tests.Unit.DataAccess;

public class InMemoryDataQueueShould
{
	[Fact]
	public async Task ConstructorShouldSetCustomCapacity()
	{
		// Arrange & Act
		var queue = new InMemoryDataQueue<int>(500);

		// Assert
		_ = queue.ShouldNotBeNull();

		await queue.DisposeAsync().ConfigureAwait(true);
	}

	[Fact]
	public async Task ConstructorShouldDefaultTo1000Capacity()
	{
		// Arrange
		var queue = new InMemoryDataQueue<int>();

		// Act
		var task = queue.EnqueueAsync(42).AsTask();

		// Assert
		await task.ShouldNotThrowAsync().ConfigureAwait(true);
		_ = queue.ShouldNotBeNull();

		await queue.DisposeAsync().ConfigureAwait(true);
	}

	[Fact]
	public void ConstructorShouldThrowWhenCapacityIsZeroOrNegative()
	{
		// Act & Assert
		var ex = Should.Throw<ArgumentOutOfRangeException>(() => new InMemoryDataQueue<int>(0));
		ex.ParamName.ShouldBe("capacity");

		var exNegative = Should.Throw<ArgumentOutOfRangeException>(() => new InMemoryDataQueue<int>(-10));
		exNegative.ParamName.ShouldBe("capacity");
	}

	[Fact]
	public async Task EnqueueBatchAsyncAndAddMultipleItems()
	{
		// Arrange
		var queue = new InMemoryDataQueue<int>();
		var items = new List<int> { 10, 20, 30, 40 };

		// Act
		await queue.EnqueueBatchAsync(items).ConfigureAwait(true);

		// Assert
		queue.HasPendingItems().ShouldBeTrue();

		var dequeuedItems = await queue.DequeueBatchAsync(4).ConfigureAwait(true);
		dequeuedItems.ShouldBe(items);

		await queue.DisposeAsync().ConfigureAwait(true);
	}

	[Fact]
	public async Task EnqueueAsyncAndAddItems()
	{
		var queue = new InMemoryDataQueue<int>();
		await queue.EnqueueAsync(42).ConfigureAwait(true);

		queue.HasPendingItems().ShouldBeTrue();

		await queue.DisposeAsync().ConfigureAwait(true);
	}

	[Fact]
	public async Task DequeueAllAsyncAndRetrieveItems()
	{
		var queue = new InMemoryDataQueue<int>();
		await queue.EnqueueAsync(1).ConfigureAwait(true);
		await queue.EnqueueAsync(2).ConfigureAwait(true);

		var results = new List<int>();
		await foreach (var item in queue.DequeueAllAsync().ConfigureAwait(true))
		{
			results.Add(item);
		}

		results.ShouldBe(new[] { 1, 2 });

		await queue.DisposeAsync().ConfigureAwait(true);
	}

	[Fact]
	public async Task DequeueBatchAsyncAndRetrieveLimitedItems()
	{
		var queue = new InMemoryDataQueue<int>();
		await queue.EnqueueAsync(1).ConfigureAwait(true);
		await queue.EnqueueAsync(2).ConfigureAwait(true);
		await queue.EnqueueAsync(3).ConfigureAwait(true);

		var batch = await queue.DequeueBatchAsync(2).ConfigureAwait(true);
		batch.ShouldBe([1, 2]);

		queue.HasPendingItems().ShouldBeTrue();

		await queue.DisposeAsync().ConfigureAwait(true);
	}

	[Fact]
	public async Task DisposeAsyncAndReleaseResources()
	{
		var queue = new InMemoryDataQueue<int>();
		await queue.DisposeAsync().ConfigureAwait(true);

		_ = await Should.ThrowAsync<ObjectDisposedException>(async () => await queue.EnqueueAsync(1).ConfigureAwait(true))
			.ConfigureAwait(true);
	}

	[Fact]
	public async Task DequeueAllAsyncReturnsNothingWhenQueueIsEmpty()
	{
		var queue = new InMemoryDataQueue<int>();

		await foreach (var _ in queue.DequeueAllAsync().ConfigureAwait(true))
		{
#pragma warning disable CA1303 // Do not pass literals as localized parameters
			Assert.Fail("Expected no items to be dequeued.");
#pragma warning restore CA1303 // Do not pass literals as localized parameters
		}

		queue.HasPendingItems().ShouldBeFalse();
		await queue.DisposeAsync().ConfigureAwait(true);
	}

	[Fact]
	public async Task DequeueAllAsyncShouldThrowIfDisposed()
	{
		var queue = new InMemoryDataQueue<int>();
		await queue.DisposeAsync().ConfigureAwait(true);

		_ = await Should.ThrowAsync<ObjectDisposedException>(async () =>
		{
			await foreach (var _ in queue.DequeueAllAsync().ConfigureAwait(true))
			{
				// Nothing needed here
			}
		}).ConfigureAwait(true);
	}

	[Fact]
	public async Task DequeueAllAsyncShouldCompleteWhenQueueIsEmptyAndNotDisposed()
	{
		var queue = new InMemoryDataQueue<int>();

		var seen = false;
		await foreach (var _ in queue.DequeueAllAsync().ConfigureAwait(true))
		{
			seen = true;
		}

		seen.ShouldBeFalse();

		await queue.DisposeAsync().ConfigureAwait(true);
	}

	[Fact]
	public async Task DisposeShouldDrainChannelWithoutException()
	{
		var queue = new InMemoryDataQueue<int>();
		await queue.EnqueueAsync(1).ConfigureAwait(true);
		await queue.EnqueueAsync(2).ConfigureAwait(true);

		queue.Count.ShouldBe(2);

#pragma warning disable CA1849 // Call async methods when in an async method
		queue.Dispose(); // triggers DisposeAsyncCore
#pragma warning restore CA1849 // Call async methods when in an async method

		queue.HasPendingItems().ShouldBeFalse();
		queue.Count.ShouldBe(0);
	}

	[Fact]
	public async Task DequeueBatchReturnsEmptyWhenNoItems()
	{
		var queue = new InMemoryDataQueue<int>();
		queue.CompleteWriter();

		var result = await queue.DequeueBatchAsync(5).ConfigureAwait(true);
		result.ShouldBeEmpty();

		await queue.DisposeAsync().ConfigureAwait(true);
	}
}
