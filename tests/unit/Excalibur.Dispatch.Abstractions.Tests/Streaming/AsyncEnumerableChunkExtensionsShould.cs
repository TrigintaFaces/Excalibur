// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Streaming;

namespace Excalibur.Dispatch.Abstractions.Tests.Streaming;

/// <summary>
/// Unit tests for the <see cref="AsyncEnumerableChunkExtensions"/> class.
/// Validates WithChunkInfo and AsSingleChunk extension methods.
/// </summary>
/// <remarks>
/// Sprint 445 S445.4: Unit tests for streaming helper types.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Streaming")]
public sealed class AsyncEnumerableChunkExtensionsShould : UnitTestBase
{
	#region WithChunkInfo - Empty Stream

	[Fact]
	public async Task WithChunkInfo_YieldNothing_ForEmptyStream()
	{
		// Arrange
		var source = EmptyAsyncEnumerable<int>();

		// Act
		var chunks = await source.WithChunkInfo().ToListAsync();

		// Assert
		chunks.ShouldBeEmpty();
	}

	private static async IAsyncEnumerable<T> EmptyAsyncEnumerable<T>()
	{
		await Task.CompletedTask;
		yield break;
	}

	#endregion

	#region WithChunkInfo - Single Item

	[Fact]
	public async Task WithChunkInfo_ReturnSingleChunk_ForSingleItemStream()
	{
		// Arrange
		var source = AsyncEnumerableFrom(42);

		// Act
		var chunks = await source.WithChunkInfo().ToListAsync();

		// Assert
		chunks.Count.ShouldBe(1);
	}

	[Fact]
	public async Task WithChunkInfo_SetIsFirstAndIsLast_ForSingleItemStream()
	{
		// Arrange
		var source = AsyncEnumerableFrom("only-item");

		// Act
		var chunk = await source.WithChunkInfo().FirstAsync();

		// Assert
		chunk.IsFirst.ShouldBeTrue();
		chunk.IsLast.ShouldBeTrue();
		chunk.IsSingle.ShouldBeTrue();
		chunk.IsMiddle.ShouldBeFalse();
	}

	[Fact]
	public async Task WithChunkInfo_SetIndexToZero_ForSingleItemStream()
	{
		// Arrange
		var source = AsyncEnumerableFrom("only-item");

		// Act
		var chunk = await source.WithChunkInfo().FirstAsync();

		// Assert
		chunk.Index.ShouldBe(0);
	}

	[Fact]
	public async Task WithChunkInfo_PreserveData_ForSingleItemStream()
	{
		// Arrange
		var source = AsyncEnumerableFrom("test-data");

		// Act
		var chunk = await source.WithChunkInfo().FirstAsync();

		// Assert
		chunk.Data.ShouldBe("test-data");
	}

	#endregion

	#region WithChunkInfo - Two Items

	[Fact]
	public async Task WithChunkInfo_ReturnTwoChunks_ForTwoItemStream()
	{
		// Arrange
		var source = AsyncEnumerableFrom(1, 2);

		// Act
		var chunks = await source.WithChunkInfo().ToListAsync();

		// Assert
		chunks.Count.ShouldBe(2);
	}

	[Fact]
	public async Task WithChunkInfo_SetCorrectFlags_ForFirstOfTwoItems()
	{
		// Arrange
		var source = AsyncEnumerableFrom("first", "last");

		// Act
		var chunks = await source.WithChunkInfo().ToListAsync();

		// Assert
		chunks[0].IsFirst.ShouldBeTrue();
		chunks[0].IsLast.ShouldBeFalse();
		chunks[0].IsSingle.ShouldBeFalse();
	}

	[Fact]
	public async Task WithChunkInfo_SetCorrectFlags_ForLastOfTwoItems()
	{
		// Arrange
		var source = AsyncEnumerableFrom("first", "last");

		// Act
		var chunks = await source.WithChunkInfo().ToListAsync();

		// Assert
		chunks[1].IsFirst.ShouldBeFalse();
		chunks[1].IsLast.ShouldBeTrue();
		chunks[1].IsSingle.ShouldBeFalse();
	}

	[Fact]
	public async Task WithChunkInfo_SetCorrectIndices_ForTwoItems()
	{
		// Arrange
		var source = AsyncEnumerableFrom("a", "b");

		// Act
		var chunks = await source.WithChunkInfo().ToListAsync();

		// Assert
		chunks[0].Index.ShouldBe(0);
		chunks[1].Index.ShouldBe(1);
	}

	#endregion

	#region WithChunkInfo - Multiple Items

	[Fact]
	public async Task WithChunkInfo_SetCorrectFlags_ForMiddleItems()
	{
		// Arrange
		var source = AsyncEnumerableFrom(1, 2, 3, 4, 5);

		// Act
		var chunks = await source.WithChunkInfo().ToListAsync();

		// Assert - Middle items (indices 1, 2, 3)
		for (var i = 1; i < 4; i++)
		{
			chunks[i].IsFirst.ShouldBeFalse();
			chunks[i].IsLast.ShouldBeFalse();
			chunks[i].IsMiddle.ShouldBeTrue();
		}
	}

	[Fact]
	public async Task WithChunkInfo_SetCorrectIndices_ForMultipleItems()
	{
		// Arrange
		var source = AsyncEnumerableFrom("a", "b", "c", "d");

		// Act
		var chunks = await source.WithChunkInfo().ToListAsync();

		// Assert
		for (var i = 0; i < 4; i++)
		{
			chunks[i].Index.ShouldBe(i);
		}
	}

	[Fact]
	public async Task WithChunkInfo_PreserveAllData_ForMultipleItems()
	{
		// Arrange
		var source = AsyncEnumerableFrom("one", "two", "three");

		// Act
		var chunks = await source.WithChunkInfo().ToListAsync();

		// Assert
		chunks[0].Data.ShouldBe("one");
		chunks[1].Data.ShouldBe("two");
		chunks[2].Data.ShouldBe("three");
	}

	#endregion

	#region WithChunkInfo - Null Handling

	[Fact]
	public async Task WithChunkInfo_ThrowArgumentNullException_WhenSourceIsNull()
	{
		// Arrange
		IAsyncEnumerable<int> source = null!;

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(async () =>
		{
			await foreach (var _ in source.WithChunkInfo())
			{
				// Should not reach here
			}
		});
	}

	#endregion

	#region WithChunkInfo - Cancellation

	[Fact]
	public async Task WithChunkInfo_AcceptCancellationToken()
	{
		// Arrange
		var cts = new CancellationTokenSource();
		var source = AsyncEnumerableFrom(1, 2, 3);

		// Act - Should complete normally when not cancelled
		var chunks = await source.WithChunkInfo(cts.Token).ToListAsync();

		// Assert
		chunks.Count.ShouldBe(3);
	}

	[Fact]
	public async Task WithChunkInfo_StopOnPreCancelledToken()
	{
		// Arrange
		var cts = new CancellationTokenSource();
		cts.Cancel();
		var source = AsyncEnumerableWithCancellation(cts.Token);

		// Act & Assert - Cancellation propagates through the enumerator
		_ = await Should.ThrowAsync<OperationCanceledException>(async () =>
		{
			await foreach (var _ in source.WithChunkInfo(cts.Token))
			{
				// Should not reach here with pre-cancelled token
			}
		});
	}

	private static async IAsyncEnumerable<int> AsyncEnumerableWithCancellation(
		[System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
	{
		cancellationToken.ThrowIfCancellationRequested();
		await Task.Yield();
		yield return 1;
	}

	#endregion

	#region AsSingleChunk

	[Fact]
	public async Task AsSingleChunk_ReturnSingleChunk()
	{
		// Arrange
		var value = "test-value";

		// Act
		var chunks = await value.AsSingleChunk().ToListAsync();

		// Assert
		chunks.Count.ShouldBe(1);
	}

	[Fact]
	public async Task AsSingleChunk_SetBothIsFirstAndIsLast()
	{
		// Arrange
		var value = 42;

		// Act
		var chunk = await value.AsSingleChunk().FirstAsync();

		// Assert
		chunk.IsFirst.ShouldBeTrue();
		chunk.IsLast.ShouldBeTrue();
		chunk.IsSingle.ShouldBeTrue();
	}

	[Fact]
	public async Task AsSingleChunk_SetIndexToZero()
	{
		// Arrange
		var value = "data";

		// Act
		var chunk = await value.AsSingleChunk().FirstAsync();

		// Assert
		chunk.Index.ShouldBe(0);
	}

	[Fact]
	public async Task AsSingleChunk_PreserveData()
	{
		// Arrange
		var value = "my-data";

		// Act
		var chunk = await value.AsSingleChunk().FirstAsync();

		// Assert
		chunk.Data.ShouldBe("my-data");
	}

	[Fact]
	public async Task AsSingleChunk_SupportNullData()
	{
		// Arrange
		string? value = null;

		// Act
		var chunk = await value.AsSingleChunk().FirstAsync();

		// Assert
		chunk.Data.ShouldBeNull();
	}

	[Fact]
	public async Task AsSingleChunk_ThrowOnCancellation_WhenTokenIsCancelled()
	{
		// Arrange
		var cts = new CancellationTokenSource();
		cts.Cancel();

		// Act & Assert
		_ = await Should.ThrowAsync<OperationCanceledException>(async () =>
		{
			await foreach (var _ in "value".AsSingleChunk(cts.Token))
			{
				// Should not reach here
			}
		});
	}

	#endregion

	#region Helper Methods

	private static async IAsyncEnumerable<T> AsyncEnumerableFrom<T>(params T[] items)
	{
		foreach (var item in items)
		{
			await Task.Yield();
			yield return item;
		}
	}

	#endregion
}

#region Extension Methods for Testing

internal static class TestAsyncEnumerableExtensions
{
	public static async Task<List<T>> ToListAsync<T>(this IAsyncEnumerable<T> source)
	{
		var list = new List<T>();
		await foreach (var item in source)
		{
			list.Add(item);
		}

		return list;
	}

	public static async Task<T> FirstAsync<T>(this IAsyncEnumerable<T> source)
	{
		await foreach (var item in source)
		{
			return item;
		}

		throw new InvalidOperationException("Sequence contains no elements");
	}
}

#endregion
