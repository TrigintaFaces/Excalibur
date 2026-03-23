// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Threading.Channels;

using Excalibur.Dispatch.Channels;
using Excalibur.Dispatch.Options.Channels;

namespace Excalibur.Dispatch.Tests.Channels;

/// <summary>
/// Tests for DispatchChannel, DispatchChannelReader, and DispatchChannelWriter.
/// Validates bounded channel backpressure, read/write concurrency, and cancellation.
/// </summary>
/// <remarks>
/// Sprint 693, Task T.5 (bd-wq2wo): Tests for the 682-line untested channel infrastructure.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class DispatchChannelShould : IDisposable
{
	private readonly CancellationTokenSource _cts = new();

	public void Dispose() => _cts.Dispose();

	#region Construction

	[Fact]
	public void CreateBoundedChannel_WithSpecifiedCapacity()
	{
		// Arrange & Act
		using var channel = new DispatchChannel<string>(new DispatchChannelOptions
		{
			Mode = ChannelMode.Bounded,
			Capacity = 5,
		});

		// Assert
		channel.ShouldNotBeNull();
		channel.Reader.ShouldNotBeNull();
		channel.Writer.ShouldNotBeNull();
	}

	[Fact]
	public void CreateUnboundedChannel()
	{
		// Arrange & Act
		using var channel = new DispatchChannel<string>(new DispatchChannelOptions
		{
			Mode = ChannelMode.Unbounded,
		});

		// Assert
		channel.ShouldNotBeNull();
	}

	[Fact]
	public void ThrowOnNullOptions()
	{
		Should.Throw<ArgumentNullException>(() => new DispatchChannel<string>(null!));
	}

	#endregion

	#region Write and Read

	[Fact]
	public async Task WriteAndReadSingleItem()
	{
		// Arrange
		using var channel = new DispatchChannel<string>(new DispatchChannelOptions
		{
			Mode = ChannelMode.Bounded,
			Capacity = 10,
		});

		// Act
		await channel.Writer.WriteAsync("item-1", _cts.Token).ConfigureAwait(false);
		var item = await channel.Reader.ReadAsync(_cts.Token).ConfigureAwait(false);

		// Assert
		item.ShouldBe("item-1");
	}

	[Fact]
	public async Task WriteAndReadMultipleItems_InOrder()
	{
		// Arrange
		using var channel = new DispatchChannel<int>(new DispatchChannelOptions
		{
			Mode = ChannelMode.Bounded,
			Capacity = 100,
		});

		// Act
		for (var i = 0; i < 50; i++)
		{
			await channel.Writer.WriteAsync(i, _cts.Token).ConfigureAwait(false);
		}

		// Assert - items come out in FIFO order
		for (var i = 0; i < 50; i++)
		{
			var item = await channel.Reader.ReadAsync(_cts.Token).ConfigureAwait(false);
			item.ShouldBe(i);
		}
	}

	#endregion

	#region Bounded Channel Backpressure

	[Fact]
	public async Task ExertBackpressure_WhenBoundedChannelIsFull()
	{
		// Arrange - Small capacity to trigger backpressure quickly
		using var channel = new DispatchChannel<int>(new DispatchChannelOptions
		{
			Mode = ChannelMode.Bounded,
			Capacity = 3,
			FullMode = BoundedChannelFullMode.Wait,
		});

		// Act - Fill the channel
		await channel.Writer.WriteAsync(1, _cts.Token).ConfigureAwait(false);
		await channel.Writer.WriteAsync(2, _cts.Token).ConfigureAwait(false);
		await channel.Writer.WriteAsync(3, _cts.Token).ConfigureAwait(false);

		// Next write should block (channel is full with capacity 3)
		using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
		var writeTask = channel.Writer.WriteAsync(4, timeoutCts.Token).AsTask();

		// Assert - The write should not complete immediately (backpressure)
		var completedTask = await Task.WhenAny(writeTask, Task.Delay(100, _cts.Token)).ConfigureAwait(false);
		completedTask.ShouldNotBe(writeTask, "Write should be blocked by backpressure");

		// Drain one item to unblock
		_ = await channel.Reader.ReadAsync(_cts.Token).ConfigureAwait(false);

		// Now the write should complete
		await writeTask.ConfigureAwait(false);
	}

	#endregion

	#region Cancellation

	[Fact]
	public async Task RespectCancellation_WhenReadingFromEmptyChannel()
	{
		// Arrange
		using var channel = new DispatchChannel<string>(new DispatchChannelOptions
		{
			Mode = ChannelMode.Bounded,
			Capacity = 10,
		});

		using var readCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

		// Act & Assert - Reading from empty channel with short timeout should throw
		await Should.ThrowAsync<OperationCanceledException>(async () =>
		{
			_ = await channel.Reader.ReadAsync(readCts.Token).ConfigureAwait(false);
		}).ConfigureAwait(false);
	}

	#endregion

	#region Concurrent Read/Write

	[Fact]
	public async Task HandleConcurrentWritersAndReaders()
	{
		// Arrange
		using var channel = new DispatchChannel<int>(new DispatchChannelOptions
		{
			Mode = ChannelMode.Bounded,
			Capacity = 100,
		});

		const int itemCount = 200;
		var receivedItems = new System.Collections.Concurrent.ConcurrentBag<int>();

		// Act - Start readers and writers concurrently
		var writerTask = Task.Run(async () =>
		{
			for (var i = 0; i < itemCount; i++)
			{
				await channel.Writer.WriteAsync(i, _cts.Token).ConfigureAwait(false);
			}

			channel.Writer.Complete();
		});

		var readerTask = Task.Run(async () =>
		{
			await foreach (var item in channel.AsChannel().Reader.ReadAllAsync(_cts.Token).ConfigureAwait(false))
			{
				receivedItems.Add(item);
			}
		});

		await Task.WhenAll(writerTask, readerTask).ConfigureAwait(false);

		// Assert - All items received
		receivedItems.Count.ShouldBe(itemCount);
	}

	#endregion

	#region Disposal

	[Fact]
	public void DisposeGracefully()
	{
		// Arrange
		var channel = new DispatchChannel<string>(new DispatchChannelOptions
		{
			Mode = ChannelMode.Bounded,
			Capacity = 10,
		});

		// Act & Assert - Should not throw
		Should.NotThrow(() => channel.Dispose());
	}

	[Fact]
	public void HandleDoubleDispose()
	{
		// Arrange
		var channel = new DispatchChannel<string>(new DispatchChannelOptions
		{
			Mode = ChannelMode.Bounded,
			Capacity = 10,
		});

		// Act & Assert
		Should.NotThrow(() =>
		{
			channel.Dispose();
			channel.Dispose(); // Double dispose should be safe
		});
	}

	#endregion
}
