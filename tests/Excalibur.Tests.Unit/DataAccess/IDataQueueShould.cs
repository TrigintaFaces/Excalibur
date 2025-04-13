using Excalibur.DataAccess;

using FakeItEasy;

using Shouldly;

namespace Excalibur.Tests.Unit.DataAccess;

public class IDataQueueShould
{
	[Fact]
	public async Task ImplementEnqueueAsyncMethodForSingleItems()
	{
		// Arrange
		var queue = A.Fake<IDataQueue<int>>();
		var record = 42;
		var cancellationToken = CancellationToken.None;

		// Act
		await queue.EnqueueAsync(record, cancellationToken).ConfigureAwait(true);

		// Assert
		_ = A.CallTo(() => queue.EnqueueAsync(record, cancellationToken)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ImplementEnqueueBatchAsyncMethodForMultipleItems()
	{
		// Arrange
		var queue = A.Fake<IDataQueue<int>>();
		var records = new[] { 1, 2, 3, 4, 5 };
		var cancellationToken = CancellationToken.None;

		// Act
		await queue.EnqueueBatchAsync(records, cancellationToken).ConfigureAwait(true);

		// Assert
		_ = A.CallTo(() => queue.EnqueueBatchAsync(records, cancellationToken)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ImplementDequeueAllAsyncMethodForStreamingItems()
	{
		// Arrange
		var queue = A.Fake<IDataQueue<int>>();
		var records = new[] { 1, 2, 3 };
		var cancellationToken = CancellationToken.None;

		_ = A.CallTo(() => queue.DequeueAllAsync(cancellationToken))
			.Returns(new TestAsyncEnumerable<int>(records));

		// Act
		var result = new List<int>();
		await foreach (var item in queue.DequeueAllAsync(cancellationToken).ConfigureAwait(true))
		{
			result.Add(item);
		}

		// Assert
		result.ShouldBe(records);
		_ = A.CallTo(() => queue.DequeueAllAsync(cancellationToken)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ImplementDequeueBatchAsyncMethodForLimitedItems()
	{
		// Arrange
		var queue = A.Fake<IDataQueue<int>>();
		var records = new List<int> { 1, 2, 3 };
		var batchSize = 3;
		var cancellationToken = CancellationToken.None;

		_ = A.CallTo(() => queue.DequeueBatchAsync(batchSize, cancellationToken))
			.Returns(Task.FromResult<IList<int>>(records));

		// Act
		var result = await queue.DequeueBatchAsync(batchSize, cancellationToken).ConfigureAwait(true);

		// Assert
		result.ShouldBe(records);
		_ = A.CallTo(() => queue.DequeueBatchAsync(batchSize, cancellationToken)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void ImplementHasPendingItemsMethod()
	{
		// Arrange
		var queue = A.Fake<IDataQueue<int>>();
		_ = A.CallTo(() => queue.HasPendingItems()).Returns(true);

		// Act
		var result = queue.HasPendingItems();

		// Assert
		result.ShouldBeTrue();
		_ = A.CallTo(() => queue.HasPendingItems()).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void ImplementIDisposable()
	{
		// Arrange
		var queue = A.Fake<IDataQueue<int>>();

		// Act & Assert
		_ = queue.ShouldBeAssignableTo<IDisposable>();
	}

	[Fact]
	public void ImplementIAsyncDisposable()
	{
		// Arrange
		var queue = A.Fake<IDataQueue<int>>();

		// Act & Assert
		_ = queue.ShouldBeAssignableTo<IAsyncDisposable>();
	}

	[Fact]
	public async Task DisposeResourcesAsynchronously()
	{
		// Arrange
		var queue = A.Fake<IDataQueue<int>>();

		// Act
		await queue.DisposeAsync().ConfigureAwait(true);

		// Assert
		_ = A.CallTo(() => queue.DisposeAsync()).MustHaveHappenedOnceExactly();
	}

	// Helper class to create an IAsyncEnumerable
	private sealed class TestAsyncEnumerable<T>(IEnumerable<T> enumerable) : IAsyncEnumerable<T>
	{
		public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
		{
			return new TestAsyncEnumerator<T>(enumerable.GetEnumerator());
		}
	}

	private sealed class TestAsyncEnumerator<T>(IEnumerator<T> enumerator) : IAsyncEnumerator<T>
	{
		public T Current => enumerator.Current;

		public ValueTask DisposeAsync()
		{
			enumerator.Dispose();
			return ValueTask.CompletedTask;
		}

		public ValueTask<bool> MoveNextAsync()
		{
			return new ValueTask<bool>(enumerator.MoveNext());
		}
	}
}
