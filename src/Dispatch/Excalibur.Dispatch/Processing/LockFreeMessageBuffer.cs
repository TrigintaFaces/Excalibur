// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Excalibur.Dispatch.Processing;

/// <summary>
/// Lock-free bounded message buffer optimized for single-producer/multi-consumer scenarios. Uses cache-aligned structures to avoid false sharing.
/// </summary>
public sealed class LockFreeMessageBuffer<TMessage> : IDisposable
	where TMessage : unmanaged
{
	private readonly int _capacity;
	private readonly int _mask;
	private readonly Node[] _nodes;

	private ProducerPadding _producer;

	private ConsumerPadding _consumer;

	/// <summary>
	/// Initializes a new instance of the <see cref="LockFreeMessageBuffer{TMessage}"/> class.
	/// Initializes a new instance of the lock-free message buffer with the specified capacity. The capacity will be rounded up to the next
	/// power of 2 for efficient masking operations.
	/// </summary>
	/// <param name="capacity"> The minimum capacity of the buffer. Must be at least 2. </param>
	/// <exception cref="ArgumentException"> Thrown when capacity is less than 2. </exception>
	public LockFreeMessageBuffer(int capacity)
	{
		if (capacity < 2)
		{
			throw new ArgumentException(Resources.LockFreeMessageBuffer_CapacityMustBeAtLeastTwo, nameof(capacity));
		}

		// Round up to next power of 2 for efficient masking
		capacity = RoundUpToPowerOf2(capacity);
		_capacity = capacity;
		_mask = capacity - 1;

		// Allocate nodes
		_nodes = new Node[capacity];

		// Initialize sequences
		for (var i = 0; i < capacity; i++)
		{
			_nodes[i].Sequence = i;
		}

		_producer = new ProducerPadding { Head = 0 };
		_consumer = new ConsumerPadding { Tail = 0 };
	}

	/// <summary>
	/// Gets get the approximate number of items in the queue. This is an estimate and may be inaccurate due to concurrent operations.
	/// </summary>
	/// <value>
	/// Get the approximate number of items in the queue. This is an estimate and may be inaccurate due to concurrent operations.
	/// </value>
	public int ApproximateCount
	{
		get
		{
			var head = Volatile.Read(ref _producer.Head);
			var tail = Volatile.Read(ref _consumer.Tail);
			return Math.Max(0, (int)(head - tail));
		}
	}

	/// <summary>
	/// Gets a value indicating whether check if the queue is empty (approximate).
	/// </summary>
	/// <value>The current <see cref="IsEmpty"/> value.</value>
	public bool IsEmpty => ApproximateCount == 0;

	/// <summary>
	/// Gets a value indicating whether check if the queue is full (approximate).
	/// </summary>
	/// <value>The current <see cref="IsFull"/> value.</value>
	public bool IsFull => ApproximateCount >= _capacity;

	/// <summary>
	/// Try to enqueue a message. This method is wait-free for the producer.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool TryEnqueue(in TMessage message)
	{
		var head = Volatile.Read(ref _producer.Head);

		for (; ; )
		{
			ref var node = ref _nodes[head & _mask];
			var sequence = Volatile.Read(ref node.Sequence);
			var diff = sequence - head;

			if (diff == 0)
			{
				// Try to claim this slot
				var newHead = head + 1;
				if (Interlocked.CompareExchange(ref _producer.Head, newHead, head) == head)
				{
					// Successfully claimed the slot
					node.Message = message;

					// Make the item available to consumers
					Volatile.Write(ref node.Sequence, newHead);
					return true;
				}

				// Another producer got there first, retry
				head = Volatile.Read(ref _producer.Head);
			}
			else
			{
				if (diff < 0)
				{
					// Queue is full
					return false;
				}

				// Another producer is ahead, update and retry
				head = Volatile.Read(ref _producer.Head);
			}
		}
	}

	/// <summary>
	/// Try to dequeue a message. This method is lock-free for consumers.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool TryDequeue(out TMessage message)
	{
		message = default;
		var tail = Volatile.Read(ref _consumer.Tail);

		for (; ; )
		{
			ref var node = ref _nodes[tail & _mask];
			var sequence = Volatile.Read(ref node.Sequence);
			var diff = sequence - (tail + 1);

			if (diff == 0)
			{
				// Try to claim this slot
				var newTail = tail + 1;
				if (Interlocked.CompareExchange(ref _consumer.Tail, newTail, tail) == tail)
				{
					// Successfully claimed the slot
					message = node.Message;

					// Make the slot available to producers
					Volatile.Write(ref node.Sequence, tail + _capacity);
					return true;
				}

				// Another consumer got there first, retry
				tail = Volatile.Read(ref _consumer.Tail);
			}
			else
			{
				if (diff < 0)
				{
					// Queue is empty
					return false;
				}

				// Another consumer is ahead, update and retry
				tail = Volatile.Read(ref _consumer.Tail);
			}
		}
	}

	/// <summary>
	/// Disposes the lock-free message buffer. Since all memory is managed, this method performs no operations.
	/// </summary>
	public void Dispose()
	{
		// Nothing to dispose - all memory is managed
	}

	private static int RoundUpToPowerOf2(int value)
	{
		value--;
		value |= value >> 1;
		value |= value >> 2;
		value |= value >> 4;
		value |= value >> 8;
		value |= value >> 16;
		value++;
		return value;
	}

	/// <summary>
	/// Separate cache lines for producer and consumer counters.
	/// </summary>
	[StructLayout(LayoutKind.Explicit, Size = 128)]
	private struct ProducerPadding
	{
		[FieldOffset(0)]
		public long Head;
	}

	[StructLayout(LayoutKind.Explicit, Size = 128)]
	private struct ConsumerPadding
	{
		[FieldOffset(0)]
		public long Tail;
	}

	/// <summary>
	/// Node structure with padding to avoid false sharing.
	/// </summary>
	[StructLayout(LayoutKind.Explicit, Size = 128)]
	private struct Node
	{
		[FieldOffset(0)]
		public TMessage Message;

		[FieldOffset(64)]
		public long Sequence;
	}
}
