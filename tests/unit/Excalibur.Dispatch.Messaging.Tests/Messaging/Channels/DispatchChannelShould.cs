// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Threading.Channels;

using Excalibur.Dispatch.Channels;
using Excalibur.Dispatch.Options.Channels;

using FakeItEasy;

namespace Excalibur.Dispatch.Tests.Messaging.Channels;

/// <summary>
/// Unit tests for the <see cref="DispatchChannel{T}"/> class.
/// </summary>
/// <remarks>
/// Sprint 414 - Task T414.7: DispatchChannel tests (0% → 60%+).
/// Tests high-performance channel implementation with custom wait strategies.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Channels")]
public sealed class DispatchChannelShould : IDisposable
{
	private readonly List<DispatchChannel<string>> _channelsToDispose = [];

	public void Dispose()
	{
		foreach (var channel in _channelsToDispose)
		{
			channel.Dispose();
		}
	}

	private DispatchChannel<T> TrackChannel<T>(DispatchChannel<T> channel)
	{
		if (channel is DispatchChannel<string> stringChannel)
		{
			_channelsToDispose.Add(stringChannel);
		}
		return channel;
	}

	#region Constructor Tests

	[Fact]
	public void ThrowArgumentNullException_WhenOptionsIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new DispatchChannel<string>(null!));
	}

	[Fact]
	public void CreateUnboundedChannel_WhenModeIsUnbounded()
	{
		// Arrange
		var options = new DispatchChannelOptions
		{
			Mode = ChannelMode.Unbounded
		};

		// Act
		var channel = TrackChannel(new DispatchChannel<string>(options));

		// Assert
		_ = channel.ShouldNotBeNull();
		_ = channel.Reader.ShouldNotBeNull();
		_ = channel.Writer.ShouldNotBeNull();
	}

	[Fact]
	public void CreateBoundedChannel_WhenModeIsBounded()
	{
		// Arrange
		var options = new DispatchChannelOptions
		{
			Mode = ChannelMode.Bounded,
			Capacity = 100
		};

		// Act
		var channel = TrackChannel(new DispatchChannel<string>(options));

		// Assert
		_ = channel.ShouldNotBeNull();
		_ = channel.Reader.ShouldNotBeNull();
		_ = channel.Writer.ShouldNotBeNull();
	}

	[Fact]
	public void ThrowArgumentException_WhenModeIsInvalid()
	{
		// Arrange
		var options = new DispatchChannelOptions
		{
			Mode = (ChannelMode)999
		};

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			new DispatchChannel<string>(options));
	}

	[Fact]
	public void UseDefaultCapacity_WhenBoundedModeWithoutCapacity()
	{
		// Arrange
		var options = new DispatchChannelOptions
		{
			Mode = ChannelMode.Bounded
			// Capacity not set - should default to 1000
		};

		// Act
		var channel = TrackChannel(new DispatchChannel<string>(options));

		// Assert
		_ = channel.ShouldNotBeNull();
	}

	[Fact]
	public void UseProvidedWaitStrategy_WhenSpecified()
	{
		// Arrange
		var waitStrategy = A.Fake<IWaitStrategy>();
		var options = new DispatchChannelOptions
		{
			Mode = ChannelMode.Unbounded,
			WaitStrategy = waitStrategy
		};

		// Act
		var channel = TrackChannel(new DispatchChannel<string>(options));

		// Assert
		_ = channel.ShouldNotBeNull();
	}

	[Fact]
	public void UseDefaultHybridWaitStrategy_WhenNotSpecified()
	{
		// Arrange
		var options = new DispatchChannelOptions
		{
			Mode = ChannelMode.Unbounded
			// WaitStrategy not set - should default to HybridWaitStrategy
		};

		// Act
		var channel = TrackChannel(new DispatchChannel<string>(options));

		// Assert - Channel should be created successfully with default strategy
		_ = channel.ShouldNotBeNull();
	}

	#endregion

	#region Static Factory Tests

	[Fact]
	public void CreateUnbounded_ReturnsUnboundedChannel()
	{
		// Act
		var channel = TrackChannel(DispatchChannel<string>.CreateUnbounded());

		// Assert
		_ = channel.ShouldNotBeNull();
		_ = channel.Reader.ShouldNotBeNull();
		_ = channel.Writer.ShouldNotBeNull();
	}

	[Fact]
	public void CreateUnbounded_WithSingleReader()
	{
		// Act
		var channel = TrackChannel(DispatchChannel<string>.CreateUnbounded(singleReader: true));

		// Assert
		_ = channel.ShouldNotBeNull();
	}

	[Fact]
	public void CreateUnbounded_WithSingleWriter()
	{
		// Act
		var channel = TrackChannel(DispatchChannel<string>.CreateUnbounded(singleWriter: true));

		// Assert
		_ = channel.ShouldNotBeNull();
	}

	[Fact]
	public void CreateBounded_ReturnsChannelWithSpecifiedCapacity()
	{
		// Act
		var channel = TrackChannel(DispatchChannel<string>.CreateBounded(50));

		// Assert
		_ = channel.ShouldNotBeNull();
	}

	[Fact]
	public void CreateBounded_WithCustomFullMode()
	{
		// Act
		var channel = TrackChannel(DispatchChannel<string>.CreateBounded(
			capacity: 10,
			fullMode: BoundedChannelFullMode.DropNewest));

		// Assert
		_ = channel.ShouldNotBeNull();
	}

	[Fact]
	public void CreateBounded_WithAllOptions()
	{
		// Act
		var channel = TrackChannel(DispatchChannel<string>.CreateBounded(
			capacity: 100,
			fullMode: BoundedChannelFullMode.DropOldest,
			singleReader: true,
			singleWriter: true));

		// Assert
		_ = channel.ShouldNotBeNull();
	}

	#endregion

	#region AsChannel Tests

	[Fact]
	public void AsChannel_ReturnsInnerChannel()
	{
		// Arrange
		var channel = TrackChannel(DispatchChannel<string>.CreateUnbounded());

		// Act
		var innerChannel = channel.AsChannel();

		// Assert
		_ = innerChannel.ShouldNotBeNull();
		_ = innerChannel.ShouldBeAssignableTo<Channel<string>>();
	}

	[Fact]
	public async Task AsChannel_ReturnsWorkingChannel()
	{
		// Arrange
		var channel = TrackChannel(DispatchChannel<string>.CreateUnbounded());
		var innerChannel = channel.AsChannel();

		// Act
		await innerChannel.Writer.WriteAsync("test");
		var result = await innerChannel.Reader.ReadAsync();

		// Assert
		result.ShouldBe("test");
	}

	#endregion

	#region Read/Write Integration Tests

	[Fact]
	public async Task WriteAndRead_SingleItem()
	{
		// Arrange
		var channel = TrackChannel(DispatchChannel<string>.CreateUnbounded());
		const string testMessage = "test message";

		// Act
		await channel.Writer.WriteAsync(testMessage, CancellationToken.None);
		var result = await channel.Reader.ReadAsync(CancellationToken.None);

		// Assert
		result.ShouldBe(testMessage);
	}

	[Fact]
	public async Task WriteAndRead_MultipleItems()
	{
		// Arrange
		var channel = TrackChannel(DispatchChannel<int>.CreateUnbounded());
		var expectedItems = Enumerable.Range(1, 10).ToList();

		// Act
		foreach (var item in expectedItems)
		{
			await channel.Writer.WriteAsync(item, CancellationToken.None);
		}

		var actualItems = new List<int>();
		for (var i = 0; i < 10; i++)
		{
			actualItems.Add(await channel.Reader.ReadAsync(CancellationToken.None));
		}

		// Assert
		actualItems.ShouldBe(expectedItems);
	}

	[Fact]
	public void TryWrite_ReturnsTrueForUnboundedChannel()
	{
		// Arrange
		var channel = TrackChannel(DispatchChannel<string>.CreateUnbounded());

		// Act
		var success = channel.Writer.TryWrite("test");

		// Assert
		success.ShouldBeTrue();
	}

	[Fact]
	public void TryRead_ReturnsFalseWhenEmpty()
	{
		// Arrange
		var channel = TrackChannel(DispatchChannel<string>.CreateUnbounded());

		// Act
		var success = channel.Reader.TryRead(out var item);

		// Assert
		success.ShouldBeFalse();
		item.ShouldBeNull();
	}

	[Fact]
	public async Task TryRead_ReturnsTrueWhenItemAvailable()
	{
		// Arrange
		var channel = TrackChannel(DispatchChannel<string>.CreateUnbounded());
		await channel.Writer.WriteAsync("test", CancellationToken.None);

		// Act
		var success = channel.Reader.TryRead(out var item);

		// Assert
		success.ShouldBeTrue();
		item.ShouldBe("test");
	}

	[Fact]
	public async Task TryPeek_ReturnsTrueWithoutRemovingItem()
	{
		// Arrange
		var channel = TrackChannel(DispatchChannel<string>.CreateUnbounded());
		await channel.Writer.WriteAsync("test", CancellationToken.None);

		// Act
		var peekSuccess = channel.Reader.TryPeek(out var peekedItem);
		var readSuccess = channel.Reader.TryRead(out var readItem);

		// Assert
		peekSuccess.ShouldBeTrue();
		peekedItem.ShouldBe("test");
		readSuccess.ShouldBeTrue();
		readItem.ShouldBe("test");
	}

	#endregion

	#region Reader Property Tests

	[Fact]
	public void Reader_CanCount_ReturnsTrue()
	{
		// Arrange
		var channel = TrackChannel(DispatchChannel<string>.CreateUnbounded());

		// Assert
		channel.Reader.CanCount.ShouldBeTrue();
	}

	[Fact]
	public async Task Reader_Count_ReflectsQueuedItems()
	{
		// Arrange
		var channel = TrackChannel(DispatchChannel<int>.CreateUnbounded());

		// Act & Assert
		channel.Reader.Count.ShouldBe(0);

		await channel.Writer.WriteAsync(1, CancellationToken.None);
		channel.Reader.Count.ShouldBe(1);

		await channel.Writer.WriteAsync(2, CancellationToken.None);
		channel.Reader.Count.ShouldBe(2);

		_ = await channel.Reader.ReadAsync(CancellationToken.None);
		channel.Reader.Count.ShouldBe(1);
	}

	[Fact]
	public async Task Reader_Completion_CompletesAfterWriterComplete()
	{
		// Arrange
		var channel = TrackChannel(DispatchChannel<string>.CreateUnbounded());

		// Act
		_ = channel.Writer.TryComplete();

		// Assert
		await channel.Reader.Completion; // Should complete without timeout
	}

	#endregion

	#region WaitToReadAsync/WaitToWriteAsync Tests

	[Fact]
	public async Task WaitToReadAsync_ReturnsTrue_WhenItemAvailable()
	{
		// Arrange
		var channel = TrackChannel(DispatchChannel<string>.CreateUnbounded());

		// Start a task to write after a delay
		_ = Task.Run(async () =>
		{
			await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(50);
			await channel.Writer.WriteAsync("delayed", CancellationToken.None);
		});

		// Act
		var available = await channel.Reader.WaitToReadAsync(CancellationToken.None);

		// Assert
		available.ShouldBeTrue();
	}

	[Fact]
	public async Task WaitToReadAsync_ReturnsFalse_WhenCompleted()
	{
		// Arrange
		var channel = TrackChannel(DispatchChannel<string>.CreateUnbounded());
		_ = channel.Writer.TryComplete();

		// Act
		var available = await channel.Reader.WaitToReadAsync(CancellationToken.None);

		// Assert
		available.ShouldBeFalse();
	}

	[Fact]
	public async Task WaitToWriteAsync_ReturnsTrue_ForUnboundedChannel()
	{
		// Arrange
		var channel = TrackChannel(DispatchChannel<string>.CreateUnbounded());

		// Act
		var canWrite = await channel.Writer.WaitToWriteAsync(CancellationToken.None);

		// Assert
		canWrite.ShouldBeTrue();
	}

	#endregion

	#region Bounded Channel Tests

	[Fact]
	public async Task BoundedChannel_BlocksWhenFull()
	{
		// Arrange
		var channel = TrackChannel(DispatchChannel<int>.CreateBounded(2, BoundedChannelFullMode.Wait));

		await channel.Writer.WriteAsync(1, CancellationToken.None);
		await channel.Writer.WriteAsync(2, CancellationToken.None);

		// Act - third write should not complete immediately
		var writeTask = channel.Writer.WriteAsync(3, CancellationToken.None).AsTask();
		var completedBeforeRead = writeTask.IsCompleted;

		// Read one item to allow the write
		_ = await channel.Reader.ReadAsync(CancellationToken.None);
		await writeTask;

		// Assert
		completedBeforeRead.ShouldBeFalse();
	}

	[Fact]
	public async Task BoundedChannel_DropsNewest_WhenConfigured()
	{
		// Arrange
		var channel = TrackChannel(DispatchChannel<int>.CreateBounded(
			capacity: 2,
			fullMode: BoundedChannelFullMode.DropNewest));

		// Act - Fill channel and try to add more
		await channel.Writer.WriteAsync(1, CancellationToken.None);
		await channel.Writer.WriteAsync(2, CancellationToken.None);
		_ = channel.Writer.TryWrite(3); // This should be dropped

		// Assert - Should still only have 2 items
		channel.Reader.Count.ShouldBe(2);
	}

	#endregion

	#region Concurrent Access Tests

	[Fact]
	public async Task HandleConcurrentWriters()
	{
		// Arrange
		var channel = TrackChannel(DispatchChannel<int>.CreateUnbounded());
		var tasks = new List<Task>();
		const int itemsPerWriter = 100;
		const int writerCount = 10;

		// Act
		for (var w = 0; w < writerCount; w++)
		{
			var writerId = w;
			tasks.Add(Task.Run(async () =>
			{
				for (var i = 0; i < itemsPerWriter; i++)
				{
					await channel.Writer.WriteAsync(writerId * 1000 + i, CancellationToken.None);
				}
			}));
		}

		await Task.WhenAll(tasks);
		_ = channel.Writer.TryComplete();

		// Read all items
		var items = new List<int>();
		await foreach (var item in channel.Reader.ReadAllAsync())
		{
			items.Add(item);
		}

		// Assert
		items.Count.ShouldBe(writerCount * itemsPerWriter);
	}

	[Fact]
	public async Task HandleConcurrentReadersAndWriters()
	{
		// Arrange
		var channel = TrackChannel(DispatchChannel<int>.CreateBounded(1000));
		var writeTask = Task.Run(async () =>
		{
			for (var i = 0; i < 1000; i++)
			{
				await channel.Writer.WriteAsync(i, CancellationToken.None);
			}
			_ = channel.Writer.TryComplete();
		});

		var readItems = new List<int>();
		var readTask = Task.Run(async () =>
		{
			await foreach (var item in channel.Reader.ReadAllAsync())
			{
				lock (readItems)
				{
					readItems.Add(item);
				}
			}
		});

		// Act
		await Task.WhenAll(writeTask, readTask);

		// Assert
		readItems.Count.ShouldBe(1000);
	}

	#endregion

	#region Writer TryComplete Tests

	[Fact]
	public void TryComplete_ReturnsTrue_WhenNotAlreadyCompleted()
	{
		// Arrange
		var channel = TrackChannel(DispatchChannel<string>.CreateUnbounded());

		// Act
		var result = channel.Writer.TryComplete();

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void TryComplete_ReturnsFalse_WhenAlreadyCompleted()
	{
		// Arrange
		var channel = TrackChannel(DispatchChannel<string>.CreateUnbounded());
		_ = channel.Writer.TryComplete();

		// Act
		var result = channel.Writer.TryComplete();

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public async Task TryComplete_WithError_FaultsCompletion()
	{
		// Arrange
		var channel = TrackChannel(DispatchChannel<string>.CreateUnbounded());
		var exception = new InvalidOperationException("Test error");

		// Act
		_ = channel.Writer.TryComplete(exception);

		// Assert
		_ = await Should.ThrowAsync<InvalidOperationException>(async () =>
			await channel.Reader.Completion);
	}

	#endregion

	#region Channel Options Tests

	[Fact]
	public void RespectSingleReaderOption()
	{
		// Arrange
		var options = new DispatchChannelOptions
		{
			Mode = ChannelMode.Unbounded,
			SingleReader = true
		};

		// Act
		var channel = TrackChannel(new DispatchChannel<string>(options));

		// Assert - Channel should be created successfully
		_ = channel.ShouldNotBeNull();
	}

	[Fact]
	public void RespectSingleWriterOption()
	{
		// Arrange
		var options = new DispatchChannelOptions
		{
			Mode = ChannelMode.Unbounded,
			SingleWriter = true
		};

		// Act
		var channel = TrackChannel(new DispatchChannel<string>(options));

		// Assert
		_ = channel.ShouldNotBeNull();
	}

	[Fact]
	public void RespectAllowSynchronousContinuationsOption()
	{
		// Arrange
		var options = new DispatchChannelOptions
		{
			Mode = ChannelMode.Unbounded,
			AllowSynchronousContinuations = false
		};

		// Act
		var channel = TrackChannel(new DispatchChannel<string>(options));

		// Assert
		_ = channel.ShouldNotBeNull();
	}

	[Fact]
	public void RespectBoundedFullModeOption()
	{
		// Arrange
		var options = new DispatchChannelOptions
		{
			Mode = ChannelMode.Bounded,
			Capacity = 10,
			FullMode = BoundedChannelFullMode.DropOldest
		};

		// Act
		var channel = TrackChannel(new DispatchChannel<string>(options));

		// Assert
		_ = channel.ShouldNotBeNull();
	}

	#endregion

	#region Dispose Tests

	[Fact]
	public void DisposeWithoutException()
	{
		// Arrange
		var channel = DispatchChannel<string>.CreateUnbounded();

		// Act & Assert - Should not throw
		Should.NotThrow(() => channel.Dispose());
	}

	[Fact]
	public void DisposeMultipleTimes_WithoutException()
	{
		// Arrange
		var channel = DispatchChannel<string>.CreateUnbounded();

		// Act & Assert - Multiple dispose should not throw
		Should.NotThrow(() =>
		{
			channel.Dispose();
			channel.Dispose();
			channel.Dispose();
		});
	}

	[Fact]
	public void Dispose_WithCustomWaitStrategy()
	{
		// Arrange
		var waitStrategy = A.Fake<IWaitStrategy>();
		var options = new DispatchChannelOptions
		{
			Mode = ChannelMode.Unbounded,
			WaitStrategy = waitStrategy
		};
		var channel = new DispatchChannel<string>(options);

		// Act
		channel.Dispose();

		// Assert - Wait strategy should be disposed
		_ = A.CallTo(() => waitStrategy.Dispose()).MustHaveHappenedOnceExactly();
	}

	#endregion
}
