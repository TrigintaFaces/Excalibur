// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Buffers;

using Excalibur.Dispatch.Buffers;

namespace Excalibur.Dispatch.Tests.Messaging.Buffers;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class BufferPoolShould
{
	// --- BufferPool ---

	[Fact]
	public void Constructor_WithNullPool_Throws()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => new BufferPool(null!));
	}

	[Fact]
	public void Default_IsNotNull()
	{
		// Assert
		BufferPool.Default.ShouldNotBeNull();
	}

	[Fact]
	public void Rent_ReturnsBufferOfAtLeastRequestedSize()
	{
		// Arrange
		var pool = new BufferPool(ArrayPool<byte>.Shared);

		// Act
		var buffer = pool.Rent(100);

		// Assert
		buffer.ShouldNotBeNull();
		buffer.Length.ShouldBeGreaterThanOrEqualTo(100);

		pool.Return(buffer);
	}

	[Fact]
	public void Rent_WithZeroSize_ReturnsBuffer()
	{
		// Arrange
		var pool = new BufferPool(ArrayPool<byte>.Shared);

		// Act
		var buffer = pool.Rent(0);

		// Assert
		buffer.ShouldNotBeNull();

		pool.Return(buffer);
	}

	[Fact]
	public void Rent_WithNegativeSize_Throws()
	{
		// Arrange
		var pool = new BufferPool(ArrayPool<byte>.Shared);

		// Act & Assert
		Should.Throw<ArgumentOutOfRangeException>(() => pool.Rent(-1));
	}

	[Fact]
	public void Return_WithNullBuffer_Throws()
	{
		// Arrange
		var pool = new BufferPool(ArrayPool<byte>.Shared);

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => pool.Return(null!));
	}

	[Fact]
	public void Return_WithClearBuffer_DoesNotThrow()
	{
		// Arrange
		var pool = new BufferPool(ArrayPool<byte>.Shared);
		var buffer = pool.Rent(100);
		buffer[0] = 42;

		// Act
		pool.Return(buffer, clearBuffer: true);

		// Assert - buffer was returned successfully (no exception)
	}

	[Fact]
	public void RentBuffer_ReturnsRentedBuffer()
	{
		// Arrange
		var pool = new BufferPool(ArrayPool<byte>.Shared);

		// Act
		var rented = pool.RentBuffer(256);

		// Assert
		rented.Length.ShouldBeGreaterThanOrEqualTo(256);

		rented.Dispose();
	}

	// --- BufferManager (static) ---

	[Fact]
	public void BufferManager_Rent_ReturnsBuffer()
	{
		// Act
		var buffer = BufferManager.Rent(100);

		// Assert
		buffer.ShouldNotBeNull();
		buffer.Length.ShouldBeGreaterThanOrEqualTo(100);

		BufferManager.Return(buffer);
	}

	[Fact]
	public void BufferManager_RentBuffer_ReturnsDisposableBuffer()
	{
		// Act
		var rented = BufferManager.RentBuffer(200);

		// Assert
		rented.Length.ShouldBeGreaterThanOrEqualTo(200);

		rented.Dispose();
	}

	[Fact]
	public void BufferManager_Return_AcceptsBuffer()
	{
		// Arrange
		var buffer = BufferManager.Rent(50);

		// Act & Assert - should not throw
		BufferManager.Return(buffer);
	}

	[Fact]
	public void BufferManager_Return_WithClear_AcceptsBuffer()
	{
		// Arrange
		var buffer = BufferManager.Rent(50);

		// Act & Assert - should not throw
		BufferManager.Return(buffer, clearBuffer: true);
	}

	// --- MessageBufferPool ---

	[Fact]
	public void MessageBufferPool_DefaultConstructor_UsesDefaults()
	{
		// Act
		var pool = new MessageBufferPool();

		// Assert
		pool.BufferManager.ShouldNotBeNull();
		pool.Encoding.ShouldBe(System.Text.Encoding.UTF8);
	}

	[Fact]
	public void MessageBufferPool_RentSmallMessageBuffer_ReturnsBuffer()
	{
		// Arrange
		var pool = new MessageBufferPool();

		// Act
		var buffer = pool.RentSmallMessageBuffer();

		// Assert
		buffer.ShouldNotBeNull();
		buffer.Buffer.Length.ShouldBeGreaterThanOrEqualTo(1024);

		(buffer as IDisposable)?.Dispose();
	}

	[Fact]
	public void MessageBufferPool_RentMediumMessageBuffer_ReturnsBuffer()
	{
		// Arrange
		var pool = new MessageBufferPool();

		// Act
		var buffer = pool.RentMediumMessageBuffer();

		// Assert
		buffer.ShouldNotBeNull();
		buffer.Buffer.Length.ShouldBeGreaterThanOrEqualTo(4096);

		(buffer as IDisposable)?.Dispose();
	}

	[Fact]
	public void MessageBufferPool_RentLargeMessageBuffer_ReturnsBuffer()
	{
		// Arrange
		var pool = new MessageBufferPool();

		// Act
		var buffer = pool.RentLargeMessageBuffer();

		// Assert
		buffer.ShouldNotBeNull();
		buffer.Buffer.Length.ShouldBeGreaterThanOrEqualTo(16384);

		(buffer as IDisposable)?.Dispose();
	}

	[Fact]
	public void MessageBufferPool_RentJsonBuffer_ReturnsBuffer()
	{
		// Arrange
		var pool = new MessageBufferPool();

		// Act
		var buffer = pool.RentJsonBuffer();

		// Assert
		buffer.ShouldNotBeNull();

		(buffer as IDisposable)?.Dispose();
	}

	[Fact]
	public void MessageBufferPool_RentHeaderBuffer_ReturnsBuffer()
	{
		// Arrange
		var pool = new MessageBufferPool();

		// Act
		var buffer = pool.RentHeaderBuffer();

		// Assert
		buffer.ShouldNotBeNull();

		(buffer as IDisposable)?.Dispose();
	}

	[Fact]
	public void MessageBufferPool_EstimateBufferSize_EmptyString_ReturnsSmall()
	{
		// Arrange
		var pool = new MessageBufferPool();

		// Act
		var size = pool.EstimateBufferSize("");

		// Assert
		size.ShouldBe(1024);
	}

	[Fact]
	public void MessageBufferPool_EstimateBufferSize_NullString_ReturnsSmall()
	{
		// Arrange
		var pool = new MessageBufferPool();

		// Act
		var size = pool.EstimateBufferSize(null!);

		// Assert
		size.ShouldBe(1024);
	}

	[Fact]
	public void MessageBufferPool_EstimateBufferSize_SmallContent_ReturnsSmall()
	{
		// Arrange
		var pool = new MessageBufferPool();

		// Act
		var size = pool.EstimateBufferSize("hello");

		// Assert
		size.ShouldBe(1024);
	}

	[Fact]
	public void MessageBufferPool_EstimateBufferSize_MediumContent_ReturnsMedium()
	{
		// Arrange
		var pool = new MessageBufferPool();
		var content = new string('A', 2000);

		// Act
		var size = pool.EstimateBufferSize(content);

		// Assert
		size.ShouldBe(4096);
	}

	[Fact]
	public void MessageBufferPool_EstimateBufferSize_LargeContent_ReturnsLarge()
	{
		// Arrange
		var pool = new MessageBufferPool();
		var content = new string('A', 5000);

		// Act
		var size = pool.EstimateBufferSize(content);

		// Assert
		size.ShouldBe(16384);
	}

	[Fact]
	public void MessageBufferPool_EstimateBufferSize_VeryLargeContent_ReturnsPowerOfTwo()
	{
		// Arrange
		var pool = new MessageBufferPool();
		var content = new string('A', 20000);

		// Act
		var size = pool.EstimateBufferSize(content);

		// Assert - should round up to next power of 2
		size.ShouldBeGreaterThan(20000);
		(size & (size - 1)).ShouldBe(0); // Power of 2 check
	}
}
