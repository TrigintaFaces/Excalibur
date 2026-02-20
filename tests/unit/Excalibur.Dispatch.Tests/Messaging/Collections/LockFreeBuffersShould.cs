// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Collections;

namespace Excalibur.Dispatch.Tests.Messaging.Collections;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class LockFreeBuffersShould
{
	// --- LockFreeMpscBuffer ---

	[Fact]
	public void MpscBuffer_NewInstance_IsEmpty()
	{
		// Arrange & Act
		var buffer = new LockFreeMpscBuffer<int>();

		// Assert
		buffer.IsEmpty.ShouldBeTrue();
	}

	[Fact]
	public void MpscBuffer_Enqueue_MakesBufferNonEmpty()
	{
		// Arrange
		var buffer = new LockFreeMpscBuffer<int>();

		// Act
		buffer.Enqueue(42);

		// Assert
		buffer.IsEmpty.ShouldBeFalse();
	}

	[Fact]
	public void MpscBuffer_TryDequeue_WhenEmpty_ReturnsFalse()
	{
		// Arrange
		var buffer = new LockFreeMpscBuffer<int>();

		// Act
		var result = buffer.TryDequeue(out var item);

		// Assert
		result.ShouldBeFalse();
		item.ShouldBe(0);
	}

	[Fact]
	public void MpscBuffer_EnqueueThenDequeue_ReturnsSameItem()
	{
		// Arrange
		var buffer = new LockFreeMpscBuffer<string>();
		buffer.Enqueue("hello");

		// Act
		var result = buffer.TryDequeue(out var item);

		// Assert
		result.ShouldBeTrue();
		item.ShouldBe("hello");
	}

	[Fact]
	public void MpscBuffer_MultipleEnqueues_DequeueInOrder()
	{
		// Arrange
		var buffer = new LockFreeMpscBuffer<int>();
		buffer.Enqueue(1);
		buffer.Enqueue(2);
		buffer.Enqueue(3);

		// Act & Assert
		buffer.TryDequeue(out var item1).ShouldBeTrue();
		item1.ShouldBe(1);

		buffer.TryDequeue(out var item2).ShouldBeTrue();
		item2.ShouldBe(2);

		buffer.TryDequeue(out var item3).ShouldBeTrue();
		item3.ShouldBe(3);

		buffer.TryDequeue(out _).ShouldBeFalse();
	}

	[Fact]
	public void MpscBuffer_Clear_EmptiesBuffer()
	{
		// Arrange
		var buffer = new LockFreeMpscBuffer<int>();
		buffer.Enqueue(1);
		buffer.Enqueue(2);
		buffer.Enqueue(3);

		// Act
		buffer.Clear();

		// Assert
		buffer.IsEmpty.ShouldBeTrue();
		buffer.TryDequeue(out _).ShouldBeFalse();
	}

	[Fact]
	public void MpscBuffer_AfterDequeueAll_IsEmpty()
	{
		// Arrange
		var buffer = new LockFreeMpscBuffer<int>();
		buffer.Enqueue(1);
		buffer.TryDequeue(out _);

		// Assert
		buffer.IsEmpty.ShouldBeTrue();
	}

	[Fact]
	public void MpscBuffer_ReferenceType_HandlesNull()
	{
		// Arrange
		var buffer = new LockFreeMpscBuffer<string?>();
		buffer.Enqueue(null);

		// Act
		var result = buffer.TryDequeue(out var item);

		// Assert
		result.ShouldBeTrue();
		item.ShouldBeNull();
	}

	// --- LockFreeSpscBuffer ---

	[Fact]
	public void SpscBuffer_NewInstance_IsEmpty()
	{
		// Arrange & Act
		var buffer = new LockFreeSpscBuffer<int>(4);

		// Assert
		buffer.IsEmpty.ShouldBeTrue();
		buffer.Count.ShouldBe(0);
	}

	[Fact]
	public void SpscBuffer_CapacityLessThanTwo_Throws()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() => new LockFreeSpscBuffer<int>(1));
	}

	[Fact]
	public void SpscBuffer_CapacityZero_Throws()
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() => new LockFreeSpscBuffer<int>(0));
	}

	[Fact]
	public void SpscBuffer_TryEnqueue_MakesBufferNonEmpty()
	{
		// Arrange
		var buffer = new LockFreeSpscBuffer<int>(4);

		// Act
		var result = buffer.TryEnqueue(42);

		// Assert
		result.ShouldBeTrue();
		buffer.IsEmpty.ShouldBeFalse();
		buffer.Count.ShouldBe(1);
	}

	[Fact]
	public void SpscBuffer_TryDequeue_WhenEmpty_ReturnsFalse()
	{
		// Arrange
		var buffer = new LockFreeSpscBuffer<int>(4);

		// Act
		var result = buffer.TryDequeue(out var item);

		// Assert
		result.ShouldBeFalse();
		item.ShouldBe(0);
	}

	[Fact]
	public void SpscBuffer_EnqueueThenDequeue_ReturnsSameItem()
	{
		// Arrange
		var buffer = new LockFreeSpscBuffer<string>(4);
		buffer.TryEnqueue("hello");

		// Act
		var result = buffer.TryDequeue(out var item);

		// Assert
		result.ShouldBeTrue();
		item.ShouldBe("hello");
	}

	[Fact]
	public void SpscBuffer_MultipleItems_DequeueInOrder()
	{
		// Arrange
		var buffer = new LockFreeSpscBuffer<int>(8);
		buffer.TryEnqueue(1);
		buffer.TryEnqueue(2);
		buffer.TryEnqueue(3);

		// Act & Assert
		buffer.Count.ShouldBe(3);

		buffer.TryDequeue(out var item1).ShouldBeTrue();
		item1.ShouldBe(1);

		buffer.TryDequeue(out var item2).ShouldBeTrue();
		item2.ShouldBe(2);

		buffer.TryDequeue(out var item3).ShouldBeTrue();
		item3.ShouldBe(3);

		buffer.TryDequeue(out _).ShouldBeFalse();
	}

	[Fact]
	public void SpscBuffer_WhenFull_TryEnqueueReturnsFalse()
	{
		// Arrange - capacity rounds up to 2
		var buffer = new LockFreeSpscBuffer<int>(2);
		buffer.TryEnqueue(1);
		buffer.TryEnqueue(2);

		// Act
		var result = buffer.TryEnqueue(3);

		// Assert
		result.ShouldBeFalse();
		buffer.Count.ShouldBe(2);
	}

	[Fact]
	public void SpscBuffer_Clear_EmptiesBuffer()
	{
		// Arrange
		var buffer = new LockFreeSpscBuffer<int>(8);
		buffer.TryEnqueue(1);
		buffer.TryEnqueue(2);
		buffer.TryEnqueue(3);

		// Act
		buffer.Clear();

		// Assert
		buffer.IsEmpty.ShouldBeTrue();
		buffer.Count.ShouldBe(0);
	}

	[Fact]
	public void SpscBuffer_RoundsCapacityToPowerOfTwo()
	{
		// Arrange - capacity 3 should round to 4
		var buffer = new LockFreeSpscBuffer<int>(3);

		// Act - should be able to hold 4 items
		buffer.TryEnqueue(1).ShouldBeTrue();
		buffer.TryEnqueue(2).ShouldBeTrue();
		buffer.TryEnqueue(3).ShouldBeTrue();
		buffer.TryEnqueue(4).ShouldBeTrue();

		// Assert - 5th should fail
		buffer.TryEnqueue(5).ShouldBeFalse();
		buffer.Count.ShouldBe(4);
	}

	[Fact]
	public void SpscBuffer_WrapAround_WorksCorrectly()
	{
		// Arrange - buffer of size 4
		var buffer = new LockFreeSpscBuffer<int>(4);

		// Fill and drain to advance indices
		buffer.TryEnqueue(1);
		buffer.TryEnqueue(2);
		buffer.TryDequeue(out _);
		buffer.TryDequeue(out _);

		// Now enqueue more to force wrap-around
		buffer.TryEnqueue(3).ShouldBeTrue();
		buffer.TryEnqueue(4).ShouldBeTrue();
		buffer.TryEnqueue(5).ShouldBeTrue();
		buffer.TryEnqueue(6).ShouldBeTrue();

		// Assert
		buffer.Count.ShouldBe(4);
		buffer.TryDequeue(out var item).ShouldBeTrue();
		item.ShouldBe(3);
	}
}
