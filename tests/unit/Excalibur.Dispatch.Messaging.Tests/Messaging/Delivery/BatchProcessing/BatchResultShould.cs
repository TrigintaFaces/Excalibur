// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Buffers;

using Excalibur.Dispatch.Delivery.BatchProcessing;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery.BatchProcessing;

/// <summary>
/// Unit tests for <see cref="BatchResult{T}"/>.
/// </summary>
/// <remarks>
/// Tests the batch result struct for ArrayPool-backed batch processing.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "BatchProcessing")]
[Trait("Priority", "0")]
public sealed class BatchResultShould
{
	#region Empty Tests

	[Fact]
	public void Empty_ReturnsEmptyBatchResult()
	{
		// Arrange & Act
		var empty = BatchResult<int>.Empty;

		// Assert
		empty.Count.ShouldBe(0);
		empty.IsEmpty.ShouldBeTrue();
	}

	[Fact]
	public void Empty_Memory_IsEmpty()
	{
		// Arrange & Act
		var empty = BatchResult<int>.Empty;

		// Assert
		empty.Memory.IsEmpty.ShouldBeTrue();
		empty.Memory.Length.ShouldBe(0);
	}

	[Fact]
	public void Empty_Span_IsEmpty()
	{
		// Arrange & Act
		var empty = BatchResult<int>.Empty;

		// Assert
		empty.Span.IsEmpty.ShouldBeTrue();
		empty.Span.Length.ShouldBe(0);
	}

	[Fact]
	public void Empty_Dispose_DoesNotThrow()
	{
		// Arrange
		var empty = BatchResult<int>.Empty;

		// Act & Assert
		Should.NotThrow(() => empty.Dispose());
	}

	[Fact]
	public void Empty_ToArray_ReturnsEmptyArray()
	{
		// Arrange
		var empty = BatchResult<int>.Empty;

		// Act
		var result = empty.ToArray();

		// Assert
		result.ShouldBeEmpty();
	}

	#endregion

	#region Count and IsEmpty Property Tests

	[Fact]
	public void Count_ReturnsCorrectValue()
	{
		// Arrange
		var array = ArrayPool<int>.Shared.Rent(10);
		for (var i = 0; i < 5; i++)
		{
			array[i] = i + 1;
		}

		var batch = CreateBatchResult(array, 5);

		// Act & Assert
		try
		{
			batch.Count.ShouldBe(5);
		}
		finally
		{
			batch.Dispose();
		}
	}

	[Fact]
	public void IsEmpty_WithItems_ReturnsFalse()
	{
		// Arrange
		var array = ArrayPool<int>.Shared.Rent(10);
		array[0] = 42;
		var batch = CreateBatchResult(array, 1);

		// Act & Assert
		try
		{
			batch.IsEmpty.ShouldBeFalse();
		}
		finally
		{
			batch.Dispose();
		}
	}

	[Fact]
	public void IsEmpty_WithZeroCount_ReturnsTrue()
	{
		// Arrange
		var array = ArrayPool<int>.Shared.Rent(10);
		var batch = CreateBatchResult(array, 0);

		// Act & Assert
		try
		{
			batch.IsEmpty.ShouldBeTrue();
		}
		finally
		{
			batch.Dispose();
		}
	}

	#endregion

	#region Memory Property Tests

	[Fact]
	public void Memory_ReturnsCorrectSlice()
	{
		// Arrange
		var array = ArrayPool<int>.Shared.Rent(10);
		for (var i = 0; i < 3; i++)
		{
			array[i] = (i + 1) * 10;
		}

		var batch = CreateBatchResult(array, 3);

		// Act & Assert
		try
		{
			batch.Memory.Length.ShouldBe(3);
			batch.Memory.Span[0].ShouldBe(10);
			batch.Memory.Span[1].ShouldBe(20);
			batch.Memory.Span[2].ShouldBe(30);
		}
		finally
		{
			batch.Dispose();
		}
	}

	#endregion

	#region Span Property Tests

	[Fact]
	public void Span_ReturnsCorrectSlice()
	{
		// Arrange
		var array = ArrayPool<string>.Shared.Rent(10);
		array[0] = "first";
		array[1] = "second";
		var batch = CreateBatchResult(array, 2);

		// Act & Assert
		try
		{
			batch.Span.Length.ShouldBe(2);
			batch.Span[0].ShouldBe("first");
			batch.Span[1].ShouldBe("second");
		}
		finally
		{
			batch.Dispose();
		}
	}

	#endregion

	#region Indexer Tests

	[Fact]
	public void Indexer_ReturnsCorrectItem()
	{
		// Arrange
		var array = ArrayPool<int>.Shared.Rent(10);
		array[0] = 100;
		array[1] = 200;
		array[2] = 300;
		var batch = CreateBatchResult(array, 3);

		// Act & Assert
		try
		{
			batch[0].ShouldBe(100);
			batch[1].ShouldBe(200);
			batch[2].ShouldBe(300);
		}
		finally
		{
			batch.Dispose();
		}
	}

	[Fact]
	public void Indexer_WithNegativeIndex_ThrowsArgumentOutOfRangeException()
	{
		// Arrange
		var array = ArrayPool<int>.Shared.Rent(10);
		array[0] = 42;
		var batch = CreateBatchResult(array, 1);

		// Act & Assert
		try
		{
			_ = Should.Throw<ArgumentOutOfRangeException>(() => _ = batch[-1]);
		}
		finally
		{
			batch.Dispose();
		}
	}

	[Fact]
	public void Indexer_WithIndexEqualToCount_ThrowsArgumentOutOfRangeException()
	{
		// Arrange
		var array = ArrayPool<int>.Shared.Rent(10);
		array[0] = 42;
		var batch = CreateBatchResult(array, 1);

		// Act & Assert
		try
		{
			_ = Should.Throw<ArgumentOutOfRangeException>(() => _ = batch[1]);
		}
		finally
		{
			batch.Dispose();
		}
	}

	[Fact]
	public void Indexer_WithIndexGreaterThanCount_ThrowsArgumentOutOfRangeException()
	{
		// Arrange
		var array = ArrayPool<int>.Shared.Rent(10);
		array[0] = 42;
		var batch = CreateBatchResult(array, 1);

		// Act & Assert
		try
		{
			_ = Should.Throw<ArgumentOutOfRangeException>(() => _ = batch[5]);
		}
		finally
		{
			batch.Dispose();
		}
	}

	#endregion

	#region ToArray Tests

	[Fact]
	public void ToArray_ReturnsCorrectCopy()
	{
		// Arrange
		var array = ArrayPool<int>.Shared.Rent(10);
		array[0] = 1;
		array[1] = 2;
		array[2] = 3;
		var batch = CreateBatchResult(array, 3);

		// Act
		int[] result;
		try
		{
			result = batch.ToArray();
		}
		finally
		{
			batch.Dispose();
		}

		// Assert
		result.Length.ShouldBe(3);
		result[0].ShouldBe(1);
		result[1].ShouldBe(2);
		result[2].ShouldBe(3);
	}

	[Fact]
	public void ToArray_WithEmptyBatch_ReturnsEmptyArray()
	{
		// Arrange
		var array = ArrayPool<int>.Shared.Rent(10);
		var batch = CreateBatchResult(array, 0);

		// Act
		int[] result;
		try
		{
			result = batch.ToArray();
		}
		finally
		{
			batch.Dispose();
		}

		// Assert
		result.ShouldBeEmpty();
	}

	#endregion

	#region Enumerator Tests

	[Fact]
	public void GetEnumerator_IteratesCorrectly()
	{
		// Arrange
		var array = ArrayPool<int>.Shared.Rent(10);
		array[0] = 10;
		array[1] = 20;
		array[2] = 30;
		var batch = CreateBatchResult(array, 3);

		// Act
		var items = new List<int>();
		try
		{
			foreach (var item in batch)
			{
				items.Add(item);
			}
		}
		finally
		{
			batch.Dispose();
		}

		// Assert
		items.Count.ShouldBe(3);
		items[0].ShouldBe(10);
		items[1].ShouldBe(20);
		items[2].ShouldBe(30);
	}

	[Fact]
	public void GetEnumerator_EmptyBatch_IteratesZeroTimes()
	{
		// Arrange
		var array = ArrayPool<int>.Shared.Rent(10);
		var batch = CreateBatchResult(array, 0);

		// Act
		var count = 0;
		try
		{
			foreach (var _ in batch)
			{
				count++;
			}
		}
		finally
		{
			batch.Dispose();
		}

		// Assert
		count.ShouldBe(0);
	}

	#endregion

	#region Dispose Tests

	[Fact]
	public void Dispose_MultipleTimes_DoesNotThrow()
	{
		// Arrange
		var array = ArrayPool<int>.Shared.Rent(10);
		var batch = CreateBatchResult(array, 5);

		// Act & Assert - Multiple disposes should not throw
		Should.NotThrow(() =>
		{
			batch.Dispose();
			batch.Dispose();
			batch.Dispose();
		});
	}

	#endregion

	#region Reference Type Tests

	[Fact]
	public void WithReferenceType_WorksCorrectly()
	{
		// Arrange
		var array = ArrayPool<string>.Shared.Rent(10);
		array[0] = "hello";
		array[1] = "world";
		var batch = CreateBatchResult(array, 2);

		// Act & Assert
		try
		{
			batch.Count.ShouldBe(2);
			batch[0].ShouldBe("hello");
			batch[1].ShouldBe("world");
		}
		finally
		{
			batch.Dispose();
		}
	}

	[Fact]
	public void WithReferenceType_ToArray_WorksCorrectly()
	{
		// Arrange
		var array = ArrayPool<string>.Shared.Rent(10);
		array[0] = "a";
		array[1] = "b";
		var batch = CreateBatchResult(array, 2);

		// Act
		string[] result;
		try
		{
			result = batch.ToArray();
		}
		finally
		{
			batch.Dispose();
		}

		// Assert
		result.Length.ShouldBe(2);
		result[0].ShouldBe("a");
		result[1].ShouldBe("b");
	}

	#endregion

	#region Helper Methods

	/// <summary>
	/// Creates a BatchResult using reflection since the constructor is internal.
	/// </summary>
	private static BatchResult<T> CreateBatchResult<T>(T[]? array, int count)
	{
		// Use reflection to call internal constructor
		var constructor = typeof(BatchResult<T>).GetConstructor(
			System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
			null,
			[typeof(T[]), typeof(int)],
			null);

		if (constructor == null)
		{
			throw new InvalidOperationException("Could not find internal constructor for BatchResult<T>");
		}

		return (BatchResult<T>)constructor.Invoke([array, count]);
	}

	#endregion
}
