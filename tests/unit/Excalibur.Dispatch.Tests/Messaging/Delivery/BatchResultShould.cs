using System.Buffers;

using Excalibur.Dispatch.Delivery.BatchProcessing;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class BatchResultShould
{
	[Fact]
	public void Empty_HasZeroCount()
	{
		var empty = BatchResult<int>.Empty;

		empty.Count.ShouldBe(0);
		empty.IsEmpty.ShouldBeTrue();
	}

	[Fact]
	public void Empty_MemoryIsEmpty()
	{
		var empty = BatchResult<int>.Empty;

		empty.Memory.Length.ShouldBe(0);
	}

	[Fact]
	public void Empty_SpanIsEmpty()
	{
		var empty = BatchResult<int>.Empty;

		empty.Span.Length.ShouldBe(0);
	}

	[Fact]
	public void Empty_ToArrayReturnsEmpty()
	{
		var empty = BatchResult<int>.Empty;

		var arr = empty.ToArray();

		arr.Length.ShouldBe(0);
	}

	[Fact]
	public void Empty_DisposeIsSafe()
	{
		var empty = BatchResult<int>.Empty;

		// Should not throw
		empty.Dispose();
	}

	[Fact]
	public void WithItems_HasCorrectCount()
	{
		var array = ArrayPool<int>.Shared.Rent(10);
		array[0] = 1;
		array[1] = 2;
		array[2] = 3;
		using var batch = new BatchResult<int>(array, 3);

		batch.Count.ShouldBe(3);
		batch.IsEmpty.ShouldBeFalse();
	}

	[Fact]
	public void WithItems_IndexerWorks()
	{
		var array = ArrayPool<int>.Shared.Rent(10);
		array[0] = 10;
		array[1] = 20;
		array[2] = 30;
		using var batch = new BatchResult<int>(array, 3);

		batch[0].ShouldBe(10);
		batch[1].ShouldBe(20);
		batch[2].ShouldBe(30);
	}

	[Fact]
	public void WithItems_IndexerThrowsOutOfRange()
	{
		var array = ArrayPool<int>.Shared.Rent(10);
		using var batch = new BatchResult<int>(array, 3);

		Should.Throw<ArgumentOutOfRangeException>(() => _ = batch[3]);
		Should.Throw<ArgumentOutOfRangeException>(() => _ = batch[-1]);
	}

	[Fact]
	public void WithItems_MemoryHasCorrectLength()
	{
		var array = ArrayPool<int>.Shared.Rent(10);
		array[0] = 1;
		array[1] = 2;
		using var batch = new BatchResult<int>(array, 2);

		batch.Memory.Length.ShouldBe(2);
	}

	[Fact]
	public void WithItems_SpanHasCorrectLength()
	{
		var array = ArrayPool<int>.Shared.Rent(10);
		array[0] = 1;
		array[1] = 2;
		using var batch = new BatchResult<int>(array, 2);

		batch.Span.Length.ShouldBe(2);
	}

	[Fact]
	public void WithItems_ToArrayCopiesItems()
	{
		var array = ArrayPool<int>.Shared.Rent(10);
		array[0] = 5;
		array[1] = 10;
		using var batch = new BatchResult<int>(array, 2);

		var result = batch.ToArray();

		result.Length.ShouldBe(2);
		result[0].ShouldBe(5);
		result[1].ShouldBe(10);
	}

	[Fact]
	public void WithItems_EnumeratorWorks()
	{
		var array = ArrayPool<int>.Shared.Rent(10);
		array[0] = 1;
		array[1] = 2;
		array[2] = 3;
		using var batch = new BatchResult<int>(array, 3);

		var items = new List<int>();
		foreach (var item in batch)
		{
			items.Add(item);
		}

		items.Count.ShouldBe(3);
		items[0].ShouldBe(1);
		items[1].ShouldBe(2);
		items[2].ShouldBe(3);
	}

	[Fact]
	public void WithReferenceTypes_DisposeClearsArray()
	{
		var array = ArrayPool<string>.Shared.Rent(10);
		array[0] = "hello";
		array[1] = "world";
		var batch = new BatchResult<string>(array, 2);

		batch.Dispose();

		// After dispose, array items should be cleared for reference types
		array[0].ShouldBeNull();
		array[1].ShouldBeNull();
	}
}
