// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Buffers;

namespace Excalibur.Dispatch.Buffers;

/// <summary>
/// Owns a buffer rented from a pool and returns it to that pool exactly once, when disposed.
/// </summary>
/// <remarks>
/// <para>
/// Ownership is single and disposal is idempotent: the first <see cref="Dispose" /> atomically claims the
/// array and returns it, and every subsequent <see cref="Dispose" /> -- on this instance or on any other
/// reference to it -- is a no-op. A buffer therefore cannot be returned to the pool twice, which would
/// otherwise hand one array to two concurrent renters and let one renter's return clear the other's live data.
/// </para>
/// <para>
/// This is a reference type for that reason. The idempotency guarantee requires mutable state that every
/// reference observes; a value type cannot provide it, because each copy would carry its own independent
/// "already returned" flag while sharing the same array. The single small allocation per rental is the same
/// trade the BCL makes for <see cref="IMemoryOwner{T}" /> rentals from <see cref="MemoryPool{T}" />.
/// </para>
/// <para>
/// After disposal the buffer is no longer owned, so <see cref="Buffer" />, <see cref="Span" /> and
/// <see cref="Memory" /> throw <see cref="ObjectDisposedException" /> rather than exposing an array that
/// now belongs to whoever rented it next.
/// </para>
/// <example>
/// <code>
/// using var buffer = pool.RentBuffer(size);
/// // Use buffer.Span or buffer.Memory directly
/// </code>
/// </example>
/// </remarks>
internal sealed class RentedBuffer : IMemoryOwner<byte>
{
	private readonly ArrayPool<byte>? _arrayPool;
	private readonly Pooling.MessageBufferPool? _messageBufferPool;

	private byte[]? _buffer;

	/// <summary>
	/// Initializes a new instance of the <see cref="RentedBuffer" /> class with an ArrayPool.
	/// </summary>
	/// <param name="buffer"> The rented buffer array. </param>
	/// <param name="length"> The requested length of the buffer. </param>
	/// <param name="pool"> The array pool that owns the buffer. </param>
	/// <remarks>
	/// This constructor is used by <see cref="BufferPool"/> which uses raw ArrayPool
	/// without statistics tracking.
	/// </remarks>
	internal RentedBuffer(byte[] buffer, int length, ArrayPool<byte> pool)
	{
		ArgumentNullException.ThrowIfNull(buffer);
		ArgumentNullException.ThrowIfNull(pool);

		_buffer = buffer;
		Length = length;
		_arrayPool = pool;
		_messageBufferPool = null;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="RentedBuffer" /> class with a MessageBufferPool.
	/// </summary>
	/// <param name="pool"> The message buffer pool that owns the buffer. </param>
	/// <param name="buffer"> The rented buffer array. </param>
	/// <param name="length"> The requested length of the buffer. </param>
	/// <remarks>
	/// This constructor is used by <see cref="Pooling.MessageBufferPool"/> which tracks statistics
	/// for monitoring pool usage.
	/// </remarks>
	internal RentedBuffer(Pooling.MessageBufferPool pool, byte[] buffer, int length)
	{
		ArgumentNullException.ThrowIfNull(pool);
		ArgumentNullException.ThrowIfNull(buffer);

		_messageBufferPool = pool;
		_arrayPool = null;
		_buffer = buffer;
		Length = length;
	}

	/// <summary>
	/// Gets the underlying buffer array.
	/// </summary>
	/// <value>The current <see cref="Buffer"/> value.</value>
	/// <exception cref="ObjectDisposedException"> Thrown when the buffer has been returned to the pool. </exception>
	public byte[] Buffer => _buffer ?? throw new ObjectDisposedException(nameof(RentedBuffer));

	/// <summary>
	/// Gets the requested length of the buffer.
	/// </summary>
	/// <value>The current <see cref="Length"/> value.</value>
	public int Length { get; }

	/// <summary>
	/// Gets a span representing the valid portion of the buffer.
	/// </summary>
	/// <value>
	/// A span representing the valid portion of the buffer.
	/// </value>
	/// <exception cref="ObjectDisposedException"> Thrown when the buffer has been returned to the pool. </exception>
	public Span<byte> Span => Buffer.AsSpan(0, Length);

	/// <summary>
	/// Gets a memory representing the valid portion of the buffer.
	/// </summary>
	/// <value>
	/// A memory representing the valid portion of the buffer.
	/// </value>
	/// <exception cref="ObjectDisposedException"> Thrown when the buffer has been returned to the pool. </exception>
	public Memory<byte> Memory => Buffer.AsMemory(0, Length);

	/// <summary>
	/// Returns the buffer to the pool. Safe to call more than once; only the first call returns the buffer.
	/// </summary>
	public void Dispose()
	{
		// Atomically claim the array so exactly one caller can return it, however many callers race here.
		var buffer = Interlocked.Exchange(ref _buffer, value: null);
		if (buffer is null)
		{
			return;
		}

		// If we have a MessageBufferPool, use it to track statistics
		if (_messageBufferPool != null)
		{
			_messageBufferPool.Return(buffer, clearBuffer: true);
		}
		else
		{
			// Direct ArrayPool return for BufferPool usage
			_arrayPool!.Return(buffer, clearArray: true);
		}
	}
}
