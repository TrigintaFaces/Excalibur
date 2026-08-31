// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Buffers;

using Excalibur.Dispatch.Buffers;

namespace Excalibur.Dispatch.Tests.Messaging.Buffers;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
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
}
