// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Threading.Channels;

using Excalibur.Dispatch.Delivery.BatchProcessing;

namespace Excalibur.Dispatch.Tests.Messaging.BatchProcessing;

/// <summary>
/// Unit tests for ChannelBatchUtilities to verify batch operations on channels.
/// </summary>
[Trait("Category", "Unit")]
public class ChannelBatchUtilitiesShould
{
	#region WriteBatchAsync Tests

	[Fact]
	public async Task WriteBatchAsyncShouldWriteAllItemsToChannel()
	{
		// Arrange
		var channel = Channel.CreateUnbounded<int>();
		var items = new[] { 1, 2, 3, 4, 5 };

		// Act
		var written = await ChannelBatchUtilities.WriteBatchAsync(channel.Writer, items, CancellationToken.None);
		channel.Writer.Complete();

		// Assert
		written.ShouldBe(5);
		var readItems = await ReadAllAsync(channel.Reader);
		readItems.ShouldBe(items);
	}

	[Fact]
	public async Task WriteBatchAsyncShouldHandleEmptyList()
	{
		// Arrange
		var channel = Channel.CreateUnbounded<int>();
		var items = Array.Empty<int>();

		// Act
		var written = await ChannelBatchUtilities.WriteBatchAsync(channel.Writer, items, CancellationToken.None);
		channel.Writer.Complete();

		// Assert
		written.ShouldBe(0);
		var readItems = await ReadAllAsync(channel.Reader);
		readItems.ShouldBeEmpty();
	}

	[Fact]
	public async Task WriteBatchAsyncShouldRespectCancellation()
	{
		// Arrange
		var channel = Channel.CreateUnbounded<int>();
		var items = Enumerable.Range(1, 1000).ToArray();
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync();

		// Act & Assert
		_ = await Should.ThrowAsync<OperationCanceledException>(
			() => ChannelBatchUtilities.WriteBatchAsync(channel.Writer, items, cts.Token).AsTask());
	}

	[Fact]
	public async Task WriteBatchAsyncShouldStopWhenChannelCompleted()
	{
		// Arrange
		var channel = Channel.CreateBounded<int>(2);
		var items = new[] { 1, 2, 3, 4, 5 };

		// Fill the channel and complete it
		await channel.Writer.WriteAsync(1);
		await channel.Writer.WriteAsync(2);
		channel.Writer.Complete();

		// Act
		var written = await ChannelBatchUtilities.WriteBatchAsync(channel.Writer, items, CancellationToken.None);

		// Assert
		written.ShouldBe(0); // Cannot write to completed channel
	}

	[Fact]
	public async Task WriteBatchAsyncShouldThrowOnNullWriter()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			() => ChannelBatchUtilities.WriteBatchAsync(null!, [1, 2, 3], CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task WriteBatchAsyncShouldThrowOnNullItems()
	{
		// Arrange
		var channel = Channel.CreateUnbounded<int>();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			() => ChannelBatchUtilities.WriteBatchAsync(channel.Writer, null!, CancellationToken.None).AsTask());
	}

	#endregion WriteBatchAsync Tests

	#region DequeueBatchAsync Tests

	[Fact]
	public async Task DequeueBatchAsyncShouldReadImmediatelyAvailableItems()
	{
		// Arrange
		var channel = Channel.CreateUnbounded<int>();
		await channel.Writer.WriteAsync(1);
		await channel.Writer.WriteAsync(2);
		await channel.Writer.WriteAsync(3);

		// Act
		var batch = await ChannelBatchUtilities.DequeueBatchAsync(channel.Reader, batchSize: 10, CancellationToken.None);

		// Assert
		batch.Length.ShouldBe(3);
		batch.ShouldBe([1, 2, 3]);
	}

	[Fact]
	public async Task DequeueBatchAsyncShouldRespectBatchSize()
	{
		// Arrange
		var channel = Channel.CreateUnbounded<int>();
		foreach (var i in Enumerable.Range(1, 10))
		{
			await channel.Writer.WriteAsync(i);
		}

		// Act
		var batch = await ChannelBatchUtilities.DequeueBatchAsync(channel.Reader, batchSize: 5, CancellationToken.None);

		// Assert
		batch.Length.ShouldBe(5);
		batch.ShouldBe([1, 2, 3, 4, 5]);
	}

	[Fact]
	public async Task DequeueBatchAsyncShouldWaitForItemsWhenEmpty()
	{
		// Arrange
		var channel = Channel.CreateUnbounded<int>();
		var readTask = ChannelBatchUtilities.DequeueBatchAsync(channel.Reader, batchSize: 5, CancellationToken.None);

		// Act
		await global::Tests.Shared.Infrastructure.TestTiming.PauseAsync(50); // Allow read to start waiting
		await channel.Writer.WriteAsync(42);

		var batch = await readTask;

		// Assert
		batch.Length.ShouldBe(1);
		batch[0].ShouldBe(42);
	}

	[Fact]
	public async Task DequeueBatchAsyncShouldReturnEmptyWhenChannelCompleted()
	{
		// Arrange
		var channel = Channel.CreateUnbounded<int>();
		channel.Writer.Complete();

		// Act
		var batch = await ChannelBatchUtilities.DequeueBatchAsync(channel.Reader, batchSize: 10, CancellationToken.None);

		// Assert
		batch.Length.ShouldBe(0);
	}

	[Fact]
	public async Task DequeueBatchAsyncShouldRespectCancellation()
	{
		// Arrange
		var channel = Channel.CreateUnbounded<int>();
		using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

		// Act & Assert
		_ = await Should.ThrowAsync<OperationCanceledException>(
			() => ChannelBatchUtilities.DequeueBatchAsync(channel.Reader, 10, cts.Token).AsTask());
	}

	[Fact]
	public async Task DequeueBatchAsyncShouldThrowOnNullReader()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			() => ChannelBatchUtilities.DequeueBatchAsync<int>(null!, 10, CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task DequeueBatchAsyncShouldThrowOnInvalidBatchSize()
	{
		// Arrange
		var channel = Channel.CreateUnbounded<int>();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentOutOfRangeException>(
			() => ChannelBatchUtilities.DequeueBatchAsync(channel.Reader, 0, CancellationToken.None).AsTask());
		_ = await Should.ThrowAsync<ArgumentOutOfRangeException>(
			() => ChannelBatchUtilities.DequeueBatchAsync(channel.Reader, -1, CancellationToken.None).AsTask());
	}

	#endregion DequeueBatchAsync Tests

	#region DequeueBatchAsync with Timeout Tests

	[Fact]
	public async Task DequeueBatchAsyncWithTimeoutShouldReturnEmptyOnTimeout()
	{
		// Arrange
		var channel = Channel.CreateUnbounded<int>();

		// Act
		var batch = await ChannelBatchUtilities.DequeueBatchAsync(
			channel.Reader,
			batchSize: 10,
			waitTimeout: TimeSpan.FromMilliseconds(50), CancellationToken.None);

		// Assert
		batch.Length.ShouldBe(0);
	}

	[Fact]
	public async Task DequeueBatchAsyncWithTimeoutShouldReadAvailableItems()
	{
		// Arrange
		var channel = Channel.CreateUnbounded<int>();
		await channel.Writer.WriteAsync(1);
		await channel.Writer.WriteAsync(2);

		// Act
		var batch = await ChannelBatchUtilities.DequeueBatchAsync(
			channel.Reader,
			batchSize: 10,
			waitTimeout: TimeSpan.FromSeconds(1), CancellationToken.None);

		// Assert
		batch.Length.ShouldBe(2);
		batch.ShouldBe([1, 2]);
	}

	[Fact]
	public async Task DequeueBatchAsyncWithTimeoutShouldWaitForItems()
	{
		// Arrange
		var channel = Channel.CreateUnbounded<int>();
		var readTask = ChannelBatchUtilities.DequeueBatchAsync(
			channel.Reader,
			batchSize: 5,
			waitTimeout: TimeSpan.FromSeconds(30), CancellationToken.None);

		// Act — generous delay for cross-process CPU starvation under full-suite VS Test Explorer load
		await global::Tests.Shared.Infrastructure.TestTiming.PauseAsync(200);
		await channel.Writer.WriteAsync(42);

		var batch = await readTask;

		// Assert
		batch.Length.ShouldBe(1);
		batch[0].ShouldBe(42);
	}

	#endregion DequeueBatchAsync with Timeout Tests

	#region ReadBatchesAsync Tests

	[Fact]
	public async Task ReadBatchesAsyncShouldYieldBatches()
	{
		// Arrange
		var channel = Channel.CreateUnbounded<int>();
		foreach (var i in Enumerable.Range(1, 10))
		{
			await channel.Writer.WriteAsync(i);
		}
		channel.Writer.Complete();

		// Act
		var batches = new List<IReadOnlyList<int>>();
		await foreach (var batch in ChannelBatchUtilities.ReadBatchesAsync(channel.Reader, batchSize: 3))
		{
			batches.Add(batch);
		}

		// Assert
		batches.Count.ShouldBeGreaterThanOrEqualTo(1);
		batches.SelectMany(b => b).Count().ShouldBe(10);
	}

	[Fact]
	public async Task ReadBatchesAsyncShouldRespectBatchSize()
	{
		// Arrange
		var channel = Channel.CreateUnbounded<int>();
		foreach (var i in Enumerable.Range(1, 6))
		{
			await channel.Writer.WriteAsync(i);
		}
		channel.Writer.Complete();

		// Act
		var batches = new List<IReadOnlyList<int>>();
		await foreach (var batch in ChannelBatchUtilities.ReadBatchesAsync(channel.Reader, batchSize: 3))
		{
			batches.Add(batch);
		}

		// Assert
		foreach (var batch in batches)
		{
			batch.Count.ShouldBeLessThanOrEqualTo(3);
		}
	}

	[Fact]
	public async Task ReadBatchesAsyncShouldStopWhenChannelCompleted()
	{
		// Arrange
		var channel = Channel.CreateUnbounded<int>();
		channel.Writer.Complete();

		// Act
		var batches = new List<IReadOnlyList<int>>();
		await foreach (var batch in ChannelBatchUtilities.ReadBatchesAsync(channel.Reader, batchSize: 10))
		{
			batches.Add(batch);
		}

		// Assert
		batches.ShouldBeEmpty();
	}

	[Fact]
	public async Task ReadBatchesAsyncShouldRespectCancellation()
	{
		// Arrange
		var channel = Channel.CreateUnbounded<int>();
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync();

		// Act
		var batches = new List<IReadOnlyList<int>>();
		await foreach (var batch in ChannelBatchUtilities.ReadBatchesAsync(channel.Reader, batchSize: 10, cts.Token))
		{
			batches.Add(batch);
		}

		// Assert
		batches.ShouldBeEmpty();
	}

	#endregion ReadBatchesAsync Tests

	#region DrainAvailable Tests

	[Fact]
	public void DrainAvailableShouldReadAllAvailableItems()
	{
		// Arrange
		var channel = Channel.CreateUnbounded<int>();
		_ = channel.Writer.TryWrite(1);
		_ = channel.Writer.TryWrite(2);
		_ = channel.Writer.TryWrite(3);

		// Act
		var items = ChannelBatchUtilities.DrainAvailable(channel.Reader);

		// Assert
		items.Length.ShouldBe(3);
		items.ShouldBe([1, 2, 3]);
	}

	[Fact]
	public void DrainAvailableShouldRespectMaxItems()
	{
		// Arrange
		var channel = Channel.CreateUnbounded<int>();
		foreach (var i in Enumerable.Range(1, 10))
		{
			_ = channel.Writer.TryWrite(i);
		}

		// Act
		var items = ChannelBatchUtilities.DrainAvailable(channel.Reader, maxItems: 5);

		// Assert
		items.Length.ShouldBe(5);
		items.ShouldBe([1, 2, 3, 4, 5]);
	}

	[Fact]
	public void DrainAvailableShouldReturnEmptyWhenChannelEmpty()
	{
		// Arrange
		var channel = Channel.CreateUnbounded<int>();

		// Act
		var items = ChannelBatchUtilities.DrainAvailable(channel.Reader);

		// Assert
		items.ShouldBeEmpty();
	}

	[Fact]
	public void DrainAvailableShouldThrowOnNullReader()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => ChannelBatchUtilities.DrainAvailable<int>(null!));
	}

	[Fact]
	public void DrainAvailableShouldThrowOnInvalidMaxItems()
	{
		// Arrange
		var channel = Channel.CreateUnbounded<int>();

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() => ChannelBatchUtilities.DrainAvailable(channel.Reader, 0));
		_ = Should.Throw<ArgumentOutOfRangeException>(() => ChannelBatchUtilities.DrainAvailable(channel.Reader, -1));
	}

	#endregion DrainAvailable Tests

	#region Reference Type Tests

	[Fact]
	public async Task DequeueBatchAsyncShouldHandleReferenceTypes()
	{
		// Arrange
		var channel = Channel.CreateUnbounded<string>();
		await channel.Writer.WriteAsync("hello");
		await channel.Writer.WriteAsync("world");

		// Act
		var batch = await ChannelBatchUtilities.DequeueBatchAsync(channel.Reader, batchSize: 10, CancellationToken.None);

		// Assert
		batch.Length.ShouldBe(2);
		batch[0].ShouldBe("hello");
		batch[1].ShouldBe("world");
	}

	[Fact]
	public async Task WriteBatchAsyncShouldHandleReferenceTypes()
	{
		// Arrange
		var channel = Channel.CreateUnbounded<TestMessage>();
		var items = new[]
		{
			new TestMessage { Id = 1, Content = "Message 1" },
			new TestMessage { Id = 2, Content = "Message 2" }
		};

		// Act
		var written = await ChannelBatchUtilities.WriteBatchAsync(channel.Writer, items, CancellationToken.None);
		channel.Writer.Complete();

		// Assert
		written.ShouldBe(2);
		var readItems = await ReadAllAsync(channel.Reader);
		readItems.Count.ShouldBe(2);
		readItems[0].Id.ShouldBe(1);
		readItems[1].Id.ShouldBe(2);
	}

	private sealed class TestMessage
	{
		public int Id { get; init; }
		public string Content { get; init; } = string.Empty;
	}

	#endregion Reference Type Tests

	#region Helper Methods

	private static async Task<List<T>> ReadAllAsync<T>(ChannelReader<T> reader)
	{
		var items = new List<T>();
		await foreach (var item in reader.ReadAllAsync())
		{
			items.Add(item);
		}
		return items;
	}

	#endregion Helper Methods

	#region DequeueBatchPooledAsync Tests

	[Fact]
	public async Task DequeueBatchPooledAsyncShouldReadImmediatelyAvailableItems()
	{
		// Arrange
		var channel = Channel.CreateUnbounded<int>();
		await channel.Writer.WriteAsync(1);
		await channel.Writer.WriteAsync(2);
		await channel.Writer.WriteAsync(3);

		// Act
		using var batch = await ChannelBatchUtilities.DequeueBatchPooledAsync(channel.Reader, batchSize: 10, CancellationToken.None);

		// Assert
		batch.Count.ShouldBe(3);
		batch.Span[0].ShouldBe(1);
		batch.Span[1].ShouldBe(2);
		batch.Span[2].ShouldBe(3);
	}

	[Fact]
	public async Task DequeueBatchPooledAsyncShouldRespectBatchSize()
	{
		// Arrange
		var channel = Channel.CreateUnbounded<int>();
		foreach (var i in Enumerable.Range(1, 10))
		{
			await channel.Writer.WriteAsync(i);
		}

		// Act
		using var batch = await ChannelBatchUtilities.DequeueBatchPooledAsync(channel.Reader, batchSize: 5, CancellationToken.None);

		// Assert
		batch.Count.ShouldBe(5);
		batch.Span.ToArray().ShouldBe([1, 2, 3, 4, 5]);
	}

	[Fact]
	public async Task DequeueBatchPooledAsyncShouldWaitForItemsWhenEmpty()
	{
		// Arrange
		var channel = Channel.CreateUnbounded<int>();
		var readTask = ChannelBatchUtilities.DequeueBatchPooledAsync(channel.Reader, batchSize: 5, CancellationToken.None);

		// Act
		await global::Tests.Shared.Infrastructure.TestTiming.PauseAsync(50); // Allow read to start waiting
		await channel.Writer.WriteAsync(42);

		using var batch = await readTask;

		// Assert
		batch.Count.ShouldBe(1);
		batch[0].ShouldBe(42);
	}

	[Fact]
	public async Task DequeueBatchPooledAsyncShouldReturnEmptyWhenChannelCompleted()
	{
		// Arrange
		var channel = Channel.CreateUnbounded<int>();
		channel.Writer.Complete();

		// Act
		using var batch = await ChannelBatchUtilities.DequeueBatchPooledAsync(channel.Reader, batchSize: 10, CancellationToken.None);

		// Assert
		batch.Count.ShouldBe(0);
		batch.IsEmpty.ShouldBeTrue();
	}

	[Fact]
	public async Task DequeueBatchPooledAsyncShouldRespectCancellation()
	{
		// Arrange
		var channel = Channel.CreateUnbounded<int>();
		using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

		// Act & Assert
		_ = await Should.ThrowAsync<OperationCanceledException>(
			() => ChannelBatchUtilities.DequeueBatchPooledAsync(channel.Reader, 10, cts.Token).AsTask());
	}

	[Fact]
	public async Task DequeueBatchPooledAsyncShouldThrowOnNullReader()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			() => ChannelBatchUtilities.DequeueBatchPooledAsync<int>(null!, 10, CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task DequeueBatchPooledAsyncShouldThrowOnInvalidBatchSize()
	{
		// Arrange
		var channel = Channel.CreateUnbounded<int>();

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentOutOfRangeException>(
			() => ChannelBatchUtilities.DequeueBatchPooledAsync(channel.Reader, 0, CancellationToken.None).AsTask());
		_ = await Should.ThrowAsync<ArgumentOutOfRangeException>(
			() => ChannelBatchUtilities.DequeueBatchPooledAsync(channel.Reader, -1, CancellationToken.None).AsTask());
	}

	#endregion DequeueBatchPooledAsync Tests

	#region DequeueBatchPooledAsync with Timeout Tests

	[Fact]
	public async Task DequeueBatchPooledAsyncWithTimeoutShouldReturnEmptyOnTimeout()
	{
		// Arrange
		var channel = Channel.CreateUnbounded<int>();

		// Act
		using var batch = await ChannelBatchUtilities.DequeueBatchPooledAsync(
			channel.Reader,
			batchSize: 10,
			waitTimeout: TimeSpan.FromMilliseconds(50), CancellationToken.None);

		// Assert
		batch.Count.ShouldBe(0);
		batch.IsEmpty.ShouldBeTrue();
	}

	[Fact]
	public async Task DequeueBatchPooledAsyncWithTimeoutShouldReadAvailableItems()
	{
		// Arrange
		var channel = Channel.CreateUnbounded<int>();
		await channel.Writer.WriteAsync(1);
		await channel.Writer.WriteAsync(2);

		// Act
		using var batch = await ChannelBatchUtilities.DequeueBatchPooledAsync(
			channel.Reader,
			batchSize: 10,
			waitTimeout: TimeSpan.FromSeconds(1), CancellationToken.None);

		// Assert
		batch.Count.ShouldBe(2);
		batch.Span.ToArray().ShouldBe([1, 2]);
	}

	#endregion DequeueBatchPooledAsync with Timeout Tests

	#region BatchResult Tests

	[Fact]
	public async Task BatchResultShouldSupportEnumeration()
	{
		// Arrange
		var channel = Channel.CreateUnbounded<int>();
		await channel.Writer.WriteAsync(1);
		await channel.Writer.WriteAsync(2);
		await channel.Writer.WriteAsync(3);

		// Act
		using var batch = await ChannelBatchUtilities.DequeueBatchPooledAsync(channel.Reader, batchSize: 10, CancellationToken.None);
		var items = new List<int>();
		foreach (var item in batch)
		{
			items.Add(item);
		}

		// Assert
		items.ShouldBe([1, 2, 3]);
	}

	[Fact]
	public async Task BatchResultToArrayShouldCreateNewArray()
	{
		// Arrange
		var channel = Channel.CreateUnbounded<int>();
		await channel.Writer.WriteAsync(1);
		await channel.Writer.WriteAsync(2);

		// Act
		using var batch = await ChannelBatchUtilities.DequeueBatchPooledAsync(channel.Reader, batchSize: 10, CancellationToken.None);
		var array = batch.ToArray();

		// Assert
		array.ShouldBe([1, 2]);
		array.Length.ShouldBe(2);
	}

	[Fact]
	public async Task BatchResultMemoryShouldProvideAccessToItems()
	{
		// Arrange
		var channel = Channel.CreateUnbounded<int>();
		await channel.Writer.WriteAsync(10);
		await channel.Writer.WriteAsync(20);

		// Act
		using var batch = await ChannelBatchUtilities.DequeueBatchPooledAsync(channel.Reader, batchSize: 10, CancellationToken.None);
		var memory = batch.Memory;

		// Assert
		memory.Length.ShouldBe(2);
		memory.Span[0].ShouldBe(10);
		memory.Span[1].ShouldBe(20);
	}

	[Fact]
	public async Task BatchResultIndexerShouldProvideAccessToItems()
	{
		// Arrange
		var channel = Channel.CreateUnbounded<string>();
		await channel.Writer.WriteAsync("hello");
		await channel.Writer.WriteAsync("world");

		// Act
		using var batch = await ChannelBatchUtilities.DequeueBatchPooledAsync(channel.Reader, batchSize: 10, CancellationToken.None);

		// Assert
		batch[0].ShouldBe("hello");
		batch[1].ShouldBe("world");
	}

	[Fact]
	public async Task BatchResultIndexerShouldThrowOnInvalidIndex()
	{
		// Arrange
		var channel = Channel.CreateUnbounded<int>();
		await channel.Writer.WriteAsync(1);

		// Act
		using var batch = await ChannelBatchUtilities.DequeueBatchPooledAsync(channel.Reader, batchSize: 10, CancellationToken.None);

		// Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() => _ = batch[-1]);
		_ = Should.Throw<ArgumentOutOfRangeException>(() => _ = batch[1]);
		_ = Should.Throw<ArgumentOutOfRangeException>(() => _ = batch[100]);
	}

	[Fact]
	public void BatchResultEmptyShouldHaveZeroCount()
	{
		// Act
		var empty = BatchResult<int>.Empty;

		// Assert
		empty.Count.ShouldBe(0);
		empty.IsEmpty.ShouldBeTrue();
		empty.Memory.Length.ShouldBe(0);
		empty.Span.Length.ShouldBe(0);
		empty.ToArray().ShouldBeEmpty();
	}

	[Fact]
	public void BatchResultEmptyDisposeShouldNotThrow()
	{
		// Act & Assert - should not throw
		var empty = BatchResult<int>.Empty;
		empty.Dispose();
	}

	#endregion BatchResult Tests
}

