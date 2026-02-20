// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Excalibur.Dispatch.Processing;

/// <summary>
/// Multi-producer multi-consumer lock-free buffer using CAS operations. Optimized for high-throughput scenarios with multiple threads.
/// </summary>
/// <typeparam name="TMessage"> The type of messages stored in the buffer, must be an unmanaged type. </typeparam>
public sealed class MpmcLockFreeBuffer<TMessage> : IDisposable
	where TMessage : unmanaged
{
	private readonly int _bufferMask;
	private readonly Cell[] _buffer;
	private PaddedLong _enqueuePos;
	private PaddedLong _dequeuePos;

	/// <summary>
	/// Initializes a new instance of the <see cref="MpmcLockFreeBuffer{TMessage}"/> class.
	/// Initializes a new instance of the lock-free buffer with the specified capacity. The buffer size is automatically rounded up to the
	/// nearest power of 2 for optimal performance.
	/// </summary>
	/// <param name="bufferSize"> The desired buffer size, minimum value is 2. </param>
	/// <exception cref="ArgumentException"> Thrown when bufferSize is less than 2. </exception>
	public MpmcLockFreeBuffer(int bufferSize)
	{
		if (bufferSize < 2)
		{
			throw new ArgumentException(Resources.MpmcLockFreeBuffer_BufferSizeMustBeAtLeastTwo, nameof(bufferSize));
		}

		// Ensure power of 2
		bufferSize = RoundUpToPowerOf2(bufferSize);
		_bufferMask = bufferSize - 1;

		_buffer = new Cell[bufferSize];

		for (long i = 0; i < bufferSize; i++)
		{
			_buffer[i].Sequence = i;
		}

		_enqueuePos = default(PaddedLong);
		_dequeuePos = default(PaddedLong);
	}

	/// <summary>
	/// Gets an approximate count of items currently in the buffer. This is a snapshot value and may not be accurate in high-concurrency scenarios.
	/// </summary>
	/// <value>
	/// An approximate count of items currently in the buffer. This is a snapshot value and may not be accurate in high-concurrency scenarios.
	/// </value>
	public int ApproximateCount
	{
		get
		{
			var enqueue = Volatile.Read(ref _enqueuePos.Value);
			var dequeue = Volatile.Read(ref _dequeuePos.Value);
			return Math.Max(0, (int)(enqueue - dequeue));
		}
	}

	/// <summary>
	/// Attempts to enqueue a message into the buffer in a thread-safe, lock-free manner. Uses compare-and-swap operations to ensure
	/// atomicity across multiple producer threads.
	/// </summary>
	/// <param name="message"> The message to enqueue. </param>
	/// <returns> true if the message was successfully enqueued; false if the buffer is full. </returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool TryEnqueue(in TMessage message)
	{
		var buffer = _buffer;
		var pos = Volatile.Read(ref _enqueuePos.Value);

		for (; ; )
		{
			ref var cell = ref buffer[pos & _bufferMask];
			var seq = Volatile.Read(ref cell.Sequence);
			var diff = seq - pos;

			if (diff == 0)
			{
				if (Interlocked.CompareExchange(ref _enqueuePos.Value, pos + 1, pos) == pos)
				{
					cell.Message = message;
					Volatile.Write(ref cell.Sequence, pos + 1);
					return true;
				}
			}
			else if (diff < 0)
			{
				return false;
			}

			pos = Volatile.Read(ref _enqueuePos.Value);
		}
	}

	/// <summary>
	/// Attempts to dequeue a message from the buffer in a thread-safe, lock-free manner. Uses compare-and-swap operations to ensure
	/// atomicity across multiple consumer threads.
	/// </summary>
	/// <param name="message"> When this method returns, contains the dequeued message if successful. </param>
	/// <returns> true if a message was successfully dequeued; false if the buffer is empty. </returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public bool TryDequeue(out TMessage message)
	{
		message = default;
		var buffer = _buffer;
		var pos = Volatile.Read(ref _dequeuePos.Value);

		for (; ; )
		{
			ref var cell = ref buffer[pos & _bufferMask];
			var seq = Volatile.Read(ref cell.Sequence);
			var diff = seq - (pos + 1);

			if (diff == 0)
			{
				if (Interlocked.CompareExchange(ref _dequeuePos.Value, pos + 1, pos) == pos)
				{
					message = cell.Message;
					Volatile.Write(ref cell.Sequence, pos + _bufferMask + 1);
					return true;
				}
			}
			else if (diff < 0)
			{
				return false;
			}

			pos = Volatile.Read(ref _dequeuePos.Value);
		}
	}

	/// <summary>
	/// Releases all resources used by the buffer. This implementation has no managed or unmanaged resources to dispose.
	/// </summary>
	public void Dispose()
	{
		// Nothing to dispose
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

	[StructLayout(LayoutKind.Explicit, Size = 128)]
	private struct Cell
	{
		[FieldOffset(0)]
		public long Sequence;

		[FieldOffset(64)]
		public TMessage Message;
	}

	[StructLayout(LayoutKind.Explicit, Size = 128)]
	private struct PaddedLong
	{
		[FieldOffset(64)]
		public long Value;
	}
}
