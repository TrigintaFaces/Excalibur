// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Buffers;

namespace Excalibur.Dispatch.Buffers;

/// <summary>
/// Represents a rented buffer that automatically returns to the pool when disposed.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Value-Type Disposal Warning:</strong> This is a <c>readonly struct</c> implementing
/// <see cref="IDisposable"/>. Value-type semantics apply:
/// </para>
/// <list type="bullet">
/// <item><description>Copying this struct creates a shallow copy sharing the same underlying buffer reference.</description></item>
/// <item><description>Disposing any copy returns the buffer to the pool, invalidating all copies.</description></item>
/// <item><description>After disposal, accessing <see cref="Buffer"/> on any copy may return stale or reused data.</description></item>
/// </list>
/// <para>
/// <strong>Best Practice:</strong> Use with <c>using</c> statement and avoid copying:
/// <code>
/// using var buffer = pool.RentBuffer(size);
/// // Use buffer.Span or buffer.Memory directly
/// </code>
/// </para>
/// </remarks>
public readonly struct RentedBuffer : IDisposable, IEquatable<RentedBuffer>
{
	private readonly ArrayPool<byte>? _arrayPool;
	private readonly Pooling.MessageBufferPool? _messageBufferPool;

	/// <summary>
	/// Initializes a new instance of the <see cref="RentedBuffer" /> struct with an ArrayPool.
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
		Buffer = buffer;
		Length = length;
		_arrayPool = pool;
		_messageBufferPool = null;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="RentedBuffer" /> struct with a MessageBufferPool.
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
		_messageBufferPool = pool;
		_arrayPool = null;
		Buffer = buffer;
		Length = length;
	}

	/// <summary>
	/// Gets the underlying buffer array.
	/// </summary>
	/// <value>The current <see cref="Buffer"/> value.</value>
	public byte[] Buffer { get; }

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
	public Span<byte> Span => Buffer.AsSpan(0, Length);

	/// <summary>
	/// Gets a memory representing the valid portion of the buffer.
	/// </summary>
	/// <value>
	/// A memory representing the valid portion of the buffer.
	/// </value>
	public Memory<byte> Memory => Buffer.AsMemory(0, Length);

	/// <summary>
	/// Determines whether two buffers are equal.
	/// </summary>
	public static bool operator ==(RentedBuffer left, RentedBuffer right) => left.Equals(right);

	/// <summary>
	/// Determines whether two buffers are not equal.
	/// </summary>
	public static bool operator !=(RentedBuffer left, RentedBuffer right) => !left.Equals(right);

	/// <summary>
	/// Returns the buffer to the pool.
	/// </summary>
	public void Dispose()
	{
		if (Buffer == null)
		{
			return;
		}

		// If we have a MessageBufferPool, use it to track statistics
		if (_messageBufferPool != null)
		{
			_messageBufferPool.Return(Buffer, clearBuffer: true);
		}
		else if (_arrayPool != null)
		{
			// Direct ArrayPool return for BufferPool usage
			_arrayPool.Return(Buffer, clearArray: true);
		}
	}

	/// <summary>
	/// Determines whether the specified buffer is equal to the current buffer.
	/// </summary>
	public bool Equals(RentedBuffer other) =>
		ReferenceEquals(Buffer, other.Buffer) &&
		Length == other.Length &&
		ReferenceEquals(_arrayPool, other._arrayPool) &&
		ReferenceEquals(_messageBufferPool, other._messageBufferPool);

	/// <summary>
	/// Determines whether the specified object is equal to the current buffer.
	/// </summary>
	public override bool Equals(object? obj) => obj is RentedBuffer other && Equals(other);

	/// <summary>
	/// Returns the hash code for this buffer.
	/// </summary>
	public override int GetHashCode() => HashCode.Combine(Buffer, Length, _arrayPool, _messageBufferPool);
}
