// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Buffers;
using System.Runtime.CompilerServices;

namespace Excalibur.Dispatch.Delivery.BatchProcessing;

/// <summary>
/// Represents a batch of items read from a channel with ArrayPool-backed memory.
/// </summary>
/// <typeparam name="T">The type of items in the batch.</typeparam>
/// <remarks>
/// <para>
/// This struct wraps an array rented from <see cref="ArrayPool{T}"/> and provides
/// a <see cref="Memory{T}"/> view over the actual items. The caller is responsible
/// for disposing the result to return the array to the pool.
/// </para>
/// <para>
/// <b>CRITICAL</b>: Callers MUST dispose this result to return the array to the pool.
/// Failure to dispose will cause memory to accumulate until GC runs.
/// </para>
/// <code>
/// // Correct usage pattern:
/// using var batch = await ChannelBatchUtilities.DequeueBatchAsync(reader, 100, ct);
/// foreach (var item in batch.Span)
/// {
///     await ProcessAsync(item);
/// }
/// // batch is disposed here, returning the array to the pool
/// </code>
/// </remarks>
// CA1815: This struct intentionally does not implement equality - it represents a disposable
// resource with ownership semantics, not a value type for comparison.
#pragma warning disable CA1815
public readonly struct BatchResult<T> : IDisposable
#pragma warning restore CA1815
{
	private readonly T[]? _array;
	private readonly int _count;

	/// <summary>
	/// Represents an empty batch result with no items.
	/// </summary>
	public static readonly BatchResult<T> Empty = new(null, 0);

	/// <summary>
	/// Initializes a new instance of the <see cref="BatchResult{T}"/> struct.
	/// </summary>
	/// <param name="array">The rented array from ArrayPool, or null for empty result.</param>
	/// <param name="count">The number of valid items in the array.</param>
	internal BatchResult(T[]? array, int count)
	{
		_array = array;
		_count = count;
	}

	/// <summary>
	/// Gets the number of items in the batch.
	/// </summary>
	/// <value>The count of items.</value>
	public int Count => _count;

	/// <summary>
	/// Gets a value indicating whether the batch is empty.
	/// </summary>
	/// <value><see langword="true"/> if the batch contains no items; otherwise, <see langword="false"/>.</value>
	public bool IsEmpty => _count == 0;

	/// <summary>
	/// Gets a <see cref="Memory{T}"/> over the batch items.
	/// </summary>
	/// <value>A memory region containing the batch items.</value>
	public Memory<T> Memory => _array is null ? Memory<T>.Empty : _array.AsMemory(0, _count);

	/// <summary>
	/// Gets a <see cref="Span{T}"/> over the batch items.
	/// </summary>
	/// <value>A span containing the batch items.</value>
	public Span<T> Span => _array is null ? [] : _array.AsSpan(0, _count);

	/// <summary>
	/// Gets the item at the specified index.
	/// </summary>
	/// <param name="index">The zero-based index of the item to get.</param>
	/// <returns>The item at the specified index.</returns>
	/// <exception cref="IndexOutOfRangeException">
	/// Thrown when <paramref name="index"/> is less than zero or greater than or equal to <see cref="Count"/>.
	/// </exception>
	public T this[int index]
	{
		get
		{
			if ((uint)index >= (uint)_count)
			{
				ThrowIndexOutOfRange(index);
			}

			return _array[index];
		}
	}

	/// <summary>
	/// Returns the rented array to the ArrayPool.
	/// </summary>
	/// <remarks>
	/// This method clears references in the array before returning it to the pool
	/// when T is a reference type or contains references, preventing memory leaks.
	/// </remarks>
	public void Dispose()
	{
		if (_array is null)
		{
			return;
		}

		// Clear references before returning to pool (important for reference types)
		if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
		{
			Array.Clear(_array, 0, _count);
		}

		ArrayPool<T>.Shared.Return(_array);
	}

	/// <summary>
	/// Gets an enumerator for iterating over the batch items.
	/// </summary>
	/// <returns>An enumerator for the batch items.</returns>
	public Enumerator GetEnumerator() => new(this);

	/// <summary>
	/// Copies the batch items to a new array.
	/// </summary>
	/// <returns>A new array containing the batch items.</returns>
	/// <remarks>
	/// This method allocates a new array. Use <see cref="Span"/> or <see cref="Memory"/>
	/// for zero-allocation access when possible.
	/// </remarks>
	public T[] ToArray()
	{
		if (_count == 0)
		{
			return [];
		}

		var result = new T[_count];
		Array.Copy(_array, result, _count);
		return result;
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static void ThrowIndexOutOfRange(int index) =>
		throw new ArgumentOutOfRangeException(
			nameof(index),
			Resources.BatchResult_IndexOutOfRange);

	/// <summary>
	/// Enumerator for iterating over batch items.
	/// </summary>
	// CA1034: Nested type is required for ref struct enumerator pattern - cannot be external.
#pragma warning disable CA1034
	public ref struct Enumerator
#pragma warning restore CA1034
	{
		private readonly BatchResult<T> _batch;
		private int _index;

		internal Enumerator(BatchResult<T> batch)
		{
			_batch = batch;
			_index = -1;
		}

		/// <summary>
		/// Gets the current item in the enumeration.
		/// </summary>
		public readonly T Current => _batch._array[_index];

		/// <summary>
		/// Advances the enumerator to the next item.
		/// </summary>
		/// <returns><see langword="true"/> if there is a next item; otherwise, <see langword="false"/>.</returns>
		public bool MoveNext()
		{
			var index = _index + 1;
			if (index < _batch._count)
			{
				_index = index;
				return true;
			}

			return false;
		}
	}
}
