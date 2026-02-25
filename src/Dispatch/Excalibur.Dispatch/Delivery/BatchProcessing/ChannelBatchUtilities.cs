// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Buffers;
using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace Excalibur.Dispatch.Delivery.BatchProcessing;

/// <summary>
/// Utility methods for efficient batch operations on <see cref="Channel{T}" />.
/// Provides optimized batch reading and writing with ArrayPool support for reduced allocations.
/// </summary>
/// <remarks>
/// <para>
/// This class provides two sets of batch reading methods:
/// </para>
/// <list type="bullet">
/// <item>
/// <description>
/// <see cref="DequeueBatchPooledAsync{T}(ChannelReader{T}, int, CancellationToken)"/> -
/// Returns <see cref="BatchResult{T}"/> with zero-copy access via <see cref="Memory{T}"/>.
/// Caller MUST dispose the result to return the array to the pool.
/// </description>
/// </item>
/// <item>
/// <description>
/// <see cref="DequeueBatchAsync{T}(ChannelReader{T}, int, CancellationToken)"/> -
/// Returns a new <c>T[]</c> array. Simpler API but allocates on each call.
/// </description>
/// </item>
/// </list>
/// <para>
/// Use the pooled variants for high-throughput scenarios where allocation reduction is important.
/// </para>
/// </remarks>
public static class ChannelBatchUtilities
{
	/// <summary>
	/// Writes a batch of items to a channel writer.
	/// </summary>
	/// <typeparam name="T"> The type of items to write. </typeparam>
	/// <param name="writer"> The channel writer to write to. </param>
	/// <param name="items"> The items to write. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> The number of items successfully written. </returns>
	public static async ValueTask<int> WriteBatchAsync<T>(
		ChannelWriter<T> writer,
		IReadOnlyList<T> items,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(writer);
		ArgumentNullException.ThrowIfNull(items);

		var written = 0;

		for (var i = 0; i < items.Count; i++)
		{
			cancellationToken.ThrowIfCancellationRequested();

			// Try synchronous write first for better performance
			if (writer.TryWrite(items[i]))
			{
				written++;
				continue;
			}

			// Fall back to async write if channel is full
			if (await writer.WaitToWriteAsync(cancellationToken).ConfigureAwait(false))
			{
				if (writer.TryWrite(items[i]))
				{
					written++;
				}
			}
			else
			{
				// Channel is completed, stop writing
				break;
			}
		}

		return written;
	}

	/// <summary>
	/// Reads batches of items from a channel reader as an async enumerable.
	/// Uses ArrayPool for memory efficiency.
	/// </summary>
	/// <typeparam name="T"> The type of items to read. </typeparam>
	/// <param name="reader"> The channel reader to read from. </param>
	/// <param name="batchSize"> The maximum size of each batch. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> An async enumerable of read-only lists containing batched items. </returns>
	public static async IAsyncEnumerable<IReadOnlyList<T>> ReadBatchesAsync<T>(
		ChannelReader<T> reader,
		int batchSize,
		[EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		ArgumentNullException.ThrowIfNull(reader);
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(batchSize, 0);

		while (!cancellationToken.IsCancellationRequested)
		{
			var batch = await DequeueBatchAsync(reader, batchSize, cancellationToken).ConfigureAwait(false);

			if (batch.Length == 0)
			{
				yield break;
			}

			yield return batch;
		}
	}

	/// <summary>
	/// Dequeues a batch of items from a channel reader with pooled memory.
	/// Reads immediately available items, waiting for at least one if none are available.
	/// </summary>
	/// <typeparam name="T">The type of items to read.</typeparam>
	/// <param name="reader">The channel reader to read from.</param>
	/// <param name="batchSize">The maximum size of the batch.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>
	/// A <see cref="BatchResult{T}"/> containing the dequeued items.
	/// The caller MUST dispose the result to return the array to the pool.
	/// </returns>
	/// <remarks>
	/// <para>
	/// This method returns a <see cref="BatchResult{T}"/> that wraps an array rented from
	/// <see cref="ArrayPool{T}"/>. The caller is responsible for disposing the result to
	/// return the array to the pool.
	/// </para>
	/// <para>
	/// <b>CRITICAL</b>: Callers MUST dispose this result to return the array to the pool.
	/// Use the <c>using</c> statement or call <see cref="BatchResult{T}.Dispose"/> explicitly.
	/// </para>
	/// <code>
	/// // Correct usage:
	/// using var batch = await ChannelBatchUtilities.DequeueBatchPooledAsync(reader, 100, ct);
	/// foreach (var item in batch.Span)
	/// {
	///     await ProcessAsync(item);
	/// }
	/// </code>
	/// </remarks>
	public static async ValueTask<BatchResult<T>> DequeueBatchPooledAsync<T>(
		ChannelReader<T> reader,
		int batchSize,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(reader);
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(batchSize, 0);

		// Rent from ArrayPool for reduced allocations
		var buffer = ArrayPool<T>.Shared.Rent(batchSize);
		var count = 0;

		try
		{
			// Read immediately available items (non-blocking)
			while (count < batchSize && reader.TryRead(out var item))
			{
				buffer[count++] = item;
			}

			// If we got items, return them
			if (count > 0)
			{
				// Transfer ownership to BatchResult
				var result = new BatchResult<T>(buffer, count);
				buffer = null; // Prevent return to pool in finally block
				return result;
			}

			// Otherwise, wait for at least one item
			if (await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
			{
				// Read items that became available
				while (count < batchSize && reader.TryRead(out var item))
				{
					buffer[count++] = item;
				}
			}

			if (count > 0)
			{
				// Transfer ownership to BatchResult
				var result = new BatchResult<T>(buffer, count);
				buffer = null; // Prevent return to pool in finally block
				return result;
			}

			// No items read, return empty
			return BatchResult<T>.Empty;
		}
		finally
		{
			// Only return buffer if ownership wasn't transferred
			if (buffer is not null)
			{
				// Clear references before returning to pool (important for reference types)
				if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
				{
					Array.Clear(buffer, 0, count);
				}

				ArrayPool<T>.Shared.Return(buffer);
			}
		}
	}

	/// <summary>
	/// Dequeues a batch of items from a channel reader with pooled memory and timeout.
	/// </summary>
	/// <typeparam name="T">The type of items to read.</typeparam>
	/// <param name="reader">The channel reader to read from.</param>
	/// <param name="batchSize">The maximum size of the batch.</param>
	/// <param name="waitTimeout">The maximum time to wait for items if none are immediately available.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>
	/// A <see cref="BatchResult{T}"/> containing the dequeued items.
	/// The caller MUST dispose the result to return the array to the pool.
	/// </returns>
	/// <remarks>
	/// <para>
	/// <b>CRITICAL</b>: Callers MUST dispose this result to return the array to the pool.
	/// </para>
	/// </remarks>
	public static async ValueTask<BatchResult<T>> DequeueBatchPooledAsync<T>(
		ChannelReader<T> reader,
		int batchSize,
		TimeSpan waitTimeout,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(reader);
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(batchSize, 0);

		var buffer = ArrayPool<T>.Shared.Rent(batchSize);
		var count = 0;

		try
		{
			// Read immediately available items
			while (count < batchSize && reader.TryRead(out var item))
			{
				buffer[count++] = item;
			}

			// If we got items, return them
			if (count > 0)
			{
				var result = new BatchResult<T>(buffer, count);
				buffer = null;
				return result;
			}

			// Wait with timeout for at least one item
			using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			timeoutCts.CancelAfter(waitTimeout);

			try
			{
				if (await reader.WaitToReadAsync(timeoutCts.Token).ConfigureAwait(false))
				{
					while (count < batchSize && reader.TryRead(out var item))
					{
						buffer[count++] = item;
					}
				}
			}
			catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
			{
				// Timeout occurred, return whatever we have (empty)
			}

			if (count > 0)
			{
				var result = new BatchResult<T>(buffer, count);
				buffer = null;
				return result;
			}

			return BatchResult<T>.Empty;
		}
		finally
		{
			if (buffer is not null)
			{
				if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
				{
					Array.Clear(buffer, 0, count);
				}

				ArrayPool<T>.Shared.Return(buffer);
			}
		}
	}

	/// <summary>
	/// Dequeues a batch of items from a channel reader.
	/// Reads immediately available items, waiting for at least one if none are available.
	/// </summary>
	/// <typeparam name="T"> The type of items to read. </typeparam>
	/// <param name="reader"> The channel reader to read from. </param>
	/// <param name="batchSize"> The maximum size of the batch. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> An array containing the dequeued items. </returns>
	public static async ValueTask<T[]> DequeueBatchAsync<T>(
		ChannelReader<T> reader,
		int batchSize,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(reader);
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(batchSize, 0);

		// Rent from ArrayPool for reduced allocations
		var buffer = ArrayPool<T>.Shared.Rent(batchSize);
		var count = 0;

		try
		{
			// Read immediately available items (non-blocking)
			while (count < batchSize && reader.TryRead(out var item))
			{
				buffer[count++] = item;
			}

			// If we got items, return them
			if (count > 0)
			{
				return CreateResultArray(buffer, count);
			}

			// Otherwise, wait for at least one item
			if (await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
			{
				// Read items that became available
				while (count < batchSize && reader.TryRead(out var item))
				{
					buffer[count++] = item;
				}
			}

			return CreateResultArray(buffer, count);
		}
		finally
		{
			// Clear references before returning to pool (important for reference types)
			if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
			{
				Array.Clear(buffer, 0, count);
			}

			ArrayPool<T>.Shared.Return(buffer);
		}
	}

	/// <summary>
	/// Dequeues a batch of items from a channel reader with a timeout for waiting.
	/// </summary>
	/// <typeparam name="T"> The type of items to read. </typeparam>
	/// <param name="reader"> The channel reader to read from. </param>
	/// <param name="batchSize"> The maximum size of the batch. </param>
	/// <param name="waitTimeout"> The maximum time to wait for items if none are immediately available. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> An array containing the dequeued items. </returns>
	public static async ValueTask<T[]> DequeueBatchAsync<T>(
		ChannelReader<T> reader,
		int batchSize,
		TimeSpan waitTimeout,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(reader);
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(batchSize, 0);

		var buffer = ArrayPool<T>.Shared.Rent(batchSize);
		var count = 0;

		try
		{
			// Read immediately available items
			while (count < batchSize && reader.TryRead(out var item))
			{
				buffer[count++] = item;
			}

			// If we got items, return them
			if (count > 0)
			{
				return CreateResultArray(buffer, count);
			}

			// Wait with timeout for at least one item
			using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			timeoutCts.CancelAfter(waitTimeout);

			try
			{
				if (await reader.WaitToReadAsync(timeoutCts.Token).ConfigureAwait(false))
				{
					while (count < batchSize && reader.TryRead(out var item))
					{
						buffer[count++] = item;
					}
				}
			}
			catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
			{
				// Timeout occurred, return whatever we have (empty array)
			}

			return CreateResultArray(buffer, count);
		}
		finally
		{
			if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
			{
				Array.Clear(buffer, 0, count);
			}

			ArrayPool<T>.Shared.Return(buffer);
		}
	}

	/// <summary>
	/// Drains all currently available items from a channel reader without waiting.
	/// </summary>
	/// <typeparam name="T"> The type of items to read. </typeparam>
	/// <param name="reader"> The channel reader to drain. </param>
	/// <param name="maxItems"> The maximum number of items to drain. </param>
	/// <returns> An array containing the drained items. </returns>
	public static T[] DrainAvailable<T>(ChannelReader<T> reader, int maxItems = int.MaxValue)
	{
		ArgumentNullException.ThrowIfNull(reader);
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maxItems, 0);

		var items = new List<T>();

		while (items.Count < maxItems && reader.TryRead(out var item))
		{
			items.Add(item);
		}

		return [.. items];
	}

	private static T[] CreateResultArray<T>(T[] buffer, int count)
	{
		if (count == 0)
		{
			return [];
		}

		var result = new T[count];
		Array.Copy(buffer, result, count);
		return result;
	}
}
