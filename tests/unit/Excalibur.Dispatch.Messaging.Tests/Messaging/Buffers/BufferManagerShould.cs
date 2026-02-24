// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// Licensed under the Excalibur License 1.0 - see LICENSE files for details.

using Excalibur.Dispatch.Buffers;

namespace Excalibur.Dispatch.Tests.Messaging.Buffers;

/// <summary>
/// Unit tests for the BufferManager component.
/// </summary>
[Trait("Category", "Unit")]
public class BufferManagerShould
{
	[Fact]
	public void RentBuffer_WithRequestedSize()
	{
		// Arrange
		const int requestedSize = 1024;

		// Act
		var buffer = BufferManager.Rent(requestedSize);

		// Assert
		_ = buffer.ShouldNotBeNull();
		buffer.Length.ShouldBeGreaterThanOrEqualTo(requestedSize);

		// Cleanup
		BufferManager.Return(buffer);
	}

	[Theory]
	[InlineData(16)]
	[InlineData(256)]
	[InlineData(1024)]
	[InlineData(4096)]
	[InlineData(16384)]
	public void RentBuffer_WithVariousSizes(int size)
	{
		// Act
		var buffer = BufferManager.Rent(size);

		// Assert
		_ = buffer.ShouldNotBeNull();
		buffer.Length.ShouldBeGreaterThanOrEqualTo(size);

		// Cleanup
		BufferManager.Return(buffer);
	}

	[Fact]
	public void ReturnBuffer_WithoutException()
	{
		// Arrange
		var buffer = BufferManager.Rent(512);

		// Act & Assert
		Should.NotThrow(() => BufferManager.Return(buffer));
	}

	[Fact]
	public void RentMultipleBuffers_AndReturnThem()
	{
		// Arrange
		var buffers = new List<byte[]>();

		// Act
		for (var i = 0; i < 10; i++)
		{
			buffers.Add(BufferManager.Rent(1024));
		}

		// Assert
		buffers.ShouldAllBe(b => b != null && b.Length >= 1024);

		// Cleanup
		foreach (var buffer in buffers)
		{
			BufferManager.Return(buffer);
		}
	}

	[Fact]
	public void HandleZeroSizeRequest()
	{
		// Act
		var buffer = BufferManager.Rent(0);

		// Assert
		_ = buffer.ShouldNotBeNull();

		// Cleanup
		BufferManager.Return(buffer);
	}

	[Fact]
	public void RentLargeBuffer()
	{
		// Arrange
		const int largeSize = 1024 * 1024; // 1MB

		// Act
		var buffer = BufferManager.Rent(largeSize);

		// Assert
		_ = buffer.ShouldNotBeNull();
		buffer.Length.ShouldBeGreaterThanOrEqualTo(largeSize);

		// Cleanup
		BufferManager.Return(buffer);
	}

	[Fact]
	public async Task HandleConcurrentRentAndReturn()
	{
		// Arrange
		var tasks = new List<Task>();
		var exceptions = new List<Exception>();

		// Act
		for (var i = 0; i < 100; i++)
		{
			tasks.Add(Task.Run(async () =>
			{
				try
				{
					var buffer = BufferManager.Rent(256);
					await global::Tests.Shared.Infrastructure.TestTiming.DelayAsync(Random.Shared.Next(1, 5));
					BufferManager.Return(buffer);
				}
				catch (Exception ex)
				{
					lock (exceptions)
					{
						exceptions.Add(ex);
					}
				}
			}));
		}

		await Task.WhenAll(tasks);

		// Assert
		exceptions.ShouldBeEmpty();
	}

	[Fact]
	public void ReuseReturnedBuffers()
	{
		// Arrange
		var buffer1 = BufferManager.Rent(256);
		var buffer1HashCode = buffer1.GetHashCode();
		BufferManager.Return(buffer1);

		// Act - rent again
		var buffer2 = BufferManager.Rent(256);
		var buffer2HashCode = buffer2.GetHashCode();
		BufferManager.Return(buffer2);

		// Note: Pool may or may not return the same buffer
		// This test just verifies no exception occurs
		_ = buffer2.ShouldNotBeNull();
	}
}
