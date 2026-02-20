// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Runtime.CompilerServices;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Collections;

/// <summary>
/// Lock-free single producer single consumer buffer implementation.
/// </summary>
/// <typeparam name="T"> The type of elements in the buffer. </typeparam>
public sealed class LockFreeSpscBuffer<T>
{
	private readonly T[] _buffer;
	private readonly int _mask;
	private long _head;
	private long _tail;

	/// <summary>
	/// Initializes a new instance of the <see cref="LockFreeSpscBuffer{T}" /> class with the specified capacity.
	/// </summary>
	/// <param name="capacity"> The buffer capacity (will be rounded up to the next power of 2). </param>
	/// <exception cref="ArgumentException"> Thrown when capacity is less than 2. </exception>
	public LockFreeSpscBuffer(int capacity)
	{
		if (capacity < 2)
		{
			throw new ArgumentException(ErrorMessages.CapacityMustBeAtLeastTwo, nameof(capacity));
		}

		// Ensure power of 2
		var actualCapacity = 1;
		while (actualCapacity < capacity)
		{
			actualCapacity <<= 1;
		}

		_buffer = new T[actualCapacity];
		_mask = actualCapacity - 1;
		_head = 0;
		_tail = 0;
	}

	/// <summary>
	/// Gets the current number of items in the buffer.
	/// </summary>
	/// <value>
	/// The current number of items in the buffer.
	/// </value>
	public int Count => (int)(Volatile.Read(ref _tail) - Volatile.Read(ref _head));

	/// <summary>
	/// Gets a value indicating whether the buffer is empty.
	/// </summary>
	/// <value>
	/// A value indicating whether the buffer is empty.
	/// </value>
	public bool IsEmpty => Volatile.Read(ref _head) == Volatile.Read(ref _tail);

	/// <summary>
	/// Attempts to enqueue an item into the buffer.
	/// </summary>
	/// <param name="item"> The item to enqueue. </param>
	/// <returns> True if the item was successfully enqueued; otherwise, false if the buffer is full. </returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool TryEnqueue(T item)
	{
		var currentTail = _tail;
		var nextTail = currentTail + 1;

		if (nextTail - Volatile.Read(ref _head) > _buffer.Length)
		{
			return false;
		}

		_buffer[currentTail & _mask] = item;
		Volatile.Write(ref _tail, nextTail);
		return true;
	}

	/// <summary>
	/// Attempts to dequeue an item from the buffer.
	/// </summary>
	/// <param name="item"> When this method returns, contains the dequeued item if successful; otherwise, the default value. </param>
	/// <returns> True if an item was successfully dequeued; otherwise, false if the buffer is empty. </returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool TryDequeue(out T item)
	{
		var currentHead = _head;

		if (currentHead == Volatile.Read(ref _tail))
		{
			item = default!;
			return false;
		}

		item = _buffer[currentHead & _mask];
		_buffer[currentHead & _mask] = default!; // Clear reference
		Volatile.Write(ref _head, currentHead + 1);
		return true;
	}

	/// <summary>
	/// Clears all items from the buffer.
	/// </summary>
	public void Clear()
	{
		while (TryDequeue(out _))
		{
		}
	}
}
