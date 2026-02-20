// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Runtime.CompilerServices;

namespace Excalibur.Dispatch.Collections;

/// <summary>
/// Lock-free multiple producer single consumer buffer implementation.
/// </summary>
/// <typeparam name="T"> The type of elements in the buffer. </typeparam>
public sealed class LockFreeMpscBuffer<T>
{
	private volatile Node _head;

	private volatile Node _tail;

	/// <summary>
	/// Initializes a new instance of the <see cref="LockFreeMpscBuffer{T}" /> class.
	/// </summary>
	public LockFreeMpscBuffer()
	{
		var dummy = new Node();
		_head = dummy;
		_tail = dummy;
	}

	/// <summary>
	/// Gets a value indicating whether the buffer is empty.
	/// </summary>
	/// <value>The current <see cref="IsEmpty"/> value.</value>
	public bool IsEmpty => _head.Next == null;

	/// <summary>
	/// Enqueues an item into the buffer in a lock-free manner.
	/// </summary>
	/// <param name="item"> The item to enqueue. </param>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void Enqueue(T item)
	{
		var newNode = new Node { Value = item };
		Node? prevTail;

		do
		{
			prevTail = _tail;
			while (prevTail.Next != null)
			{
				prevTail = prevTail.Next;
			}
		}
		while (Interlocked.CompareExchange(ref prevTail.Next, newNode, comparand: null) != null);

		// Try to advance tail pointer
		_ = Interlocked.CompareExchange(ref _tail, newNode, prevTail);
	}

	/// <summary>
	/// Attempts to dequeue an item from the buffer in a lock-free manner.
	/// </summary>
	/// <param name="item"> When this method returns, contains the dequeued item if successful; otherwise, the default value. </param>
	/// <returns> True if an item was successfully dequeued; otherwise, false. </returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool TryDequeue(out T item)
	{
		Node head;
		Node? next;

		do
		{
			head = _head;
			next = head.Next;

			if (next == null)
			{
				item = default!;
				return false;
			}
		}
		while (Interlocked.CompareExchange(ref _head, next, head) != head);

		item = next.Value;
		next.Value = default!; // Clear reference
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

	private sealed class Node
	{
		public T Value = default!;
		public volatile Node? Next;
	}
}
