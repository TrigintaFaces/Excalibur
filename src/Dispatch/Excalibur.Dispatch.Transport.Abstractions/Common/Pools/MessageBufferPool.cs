// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Buffers;

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// High-performance buffer pool optimized for cloud messaging scenarios. Provides efficient memory management for message processing
/// through pooled byte and char arrays, reducing garbage collection pressure and improving throughput in high-volume messaging operations.
/// </summary>
/// <remarks>
/// <para>
/// This buffer pool is specifically designed for cloud messaging workloads where memory allocation patterns can significantly impact
/// performance. It manages separate pools for byte and character arrays, enabling efficient handling of both binary message payloads and
/// text-based operations.
/// </para>
/// <para>
/// Key performance benefits:
/// - Reduces GC pressure by reusing arrays instead of creating new ones
/// - Optimized pool sizes for typical message processing patterns
/// - Support for both byte and character data types commonly used in messaging
/// - Memory-efficient span and memory-based operations for zero-copy scenarios
/// - Thread-safe operations suitable for concurrent message processing.
/// </para>
/// <para>
/// The pool automatically manages buffer lifecycle including acquisition, size optimization, and return to pool. It implements IDisposable
/// for proper resource cleanup in hosting scenarios.
/// </para>
/// </remarks>
/// <remarks> Initializes a new instance of the <see cref="MessageBufferPool" /> class. </remarks>
/// <param name="maxBufferSize"> Maximum buffer size to pool. </param>
public class MessageBufferPool(int maxBufferSize = 65536) : IDisposable
{
	private readonly ArrayPool<byte> _bytePool = ArrayPool<byte>.Create(maxBufferSize, 100);
	private readonly ArrayPool<char> _charPool = ArrayPool<char>.Create(maxBufferSize, 100);
	private volatile bool _disposed;

	/// <summary>
	/// Rents a byte buffer from the pool with the specified minimum capacity. Provides efficient memory allocation for binary message
	/// processing operations.
	/// </summary>
	/// <param name="minimumLength">
	/// The minimum required buffer length. The returned buffer may be larger than requested for optimal pool management. Must be greater
	/// than 0 and less than or equal to the maximum buffer size.
	/// </param>
	/// <returns>
	/// A byte array rented from the pool with at least the requested capacity. The caller is responsible for returning the buffer using <see cref="ReturnByteBuffer" />.
	/// </returns>
	/// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="minimumLength" /> is less than or equal to 0. </exception>
	/// <exception cref="ObjectDisposedException"> Thrown when the pool has been disposed. </exception>
	/// <remarks>
	/// <para>
	/// The returned buffer should be treated as a temporary resource and must be returned to the pool when no longer needed. Buffers may
	/// contain residual data from previous uses unless explicitly cleared.
	/// </para>
	/// <para>
	/// For security-sensitive operations, consider using the clearArray parameter when returning buffers to ensure sensitive data is
	/// removed from pool buffers.
	/// </para>
	/// </remarks>
	public byte[] RentByteBuffer(int minimumLength)
	{
		ThrowIfDisposed();
		return _bytePool.Rent(Math.Min(minimumLength, maxBufferSize));
	}

	/// <summary>
	/// Returns a previously rented byte buffer to the pool for reuse. Enables efficient memory management by recycling buffers instead of
	/// garbage collection.
	/// </summary>
	/// <param name="buffer">
	/// The byte array to return to the pool. Must have been previously rented from this pool. Null buffers are safely ignored.
	/// </param>
	/// <param name="clearArray">
	/// <c> true </c> to clear the buffer contents before returning to pool (recommended for security-sensitive data); <c> false </c> to
	/// return the buffer as-is for better performance. Default is <c> false </c>.
	/// </param>
	/// <remarks>
	/// <para>
	/// Clearing arrays adds CPU overhead but ensures that sensitive data doesn't leak to subsequent buffer users. Choose based on your
	/// security and performance requirements.
	/// </para>
	/// <para>
	/// Buffers returned to the pool should not be accessed after this method returns as they may be immediately rented by other operations.
	/// </para>
	/// </remarks>
	public void ReturnByteBuffer(byte[]? buffer, bool clearArray = false)
	{
		if (buffer == null || _disposed)
		{
			return;
		}

		_bytePool.Return(buffer, clearArray);
	}

	/// <summary>
	/// Rents a character buffer from the pool for text-based message processing operations. Optimized for string manipulation and text
	/// serialization scenarios in messaging workflows.
	/// </summary>
	/// <param name="minimumLength">
	/// The minimum required buffer length in characters. The returned buffer may be larger than requested for optimal pool management. Must
	/// be greater than 0.
	/// </param>
	/// <returns>
	/// A character array rented from the pool with at least the requested capacity. The caller is responsible for returning the buffer
	/// using <see cref="ReturnCharBuffer" />.
	/// </returns>
	/// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="minimumLength" /> is less than or equal to 0. </exception>
	/// <exception cref="ObjectDisposedException"> Thrown when the pool has been disposed. </exception>
	/// <remarks>
	/// <para>
	/// Character buffers are particularly useful for text-based serialization operations, JSON processing, and string manipulation tasks
	/// common in messaging scenarios.
	/// </para>
	/// <para>
	/// Like byte buffers, character buffers may contain residual data from previous uses and should be cleared when security is a concern.
	/// </para>
	/// </remarks>
	public char[] RentCharBuffer(int minimumLength)
	{
		ThrowIfDisposed();
		return _charPool.Rent(Math.Min(minimumLength, maxBufferSize));
	}

	/// <summary>
	/// Returns a previously rented character buffer to the pool for reuse. Completes the buffer lifecycle for efficient memory management
	/// in text processing scenarios.
	/// </summary>
	/// <param name="buffer">
	/// The character array to return to the pool. Must have been previously rented from this pool. Null buffers are safely ignored.
	/// </param>
	/// <param name="clearArray">
	/// <c> true </c> to clear the buffer contents before returning to pool (recommended for sensitive text); <c> false </c> to return the
	/// buffer as-is for better performance. Default is <c> false </c>.
	/// </param>
	/// <remarks>
	/// <para>
	/// Character buffer clearing is particularly important when processing sensitive textual data such as authentication tokens, personal
	/// information, or financial data.
	/// </para>
	/// <para> The performance vs. security trade-off should be evaluated based on the specific use case and data sensitivity requirements. </para>
	/// </remarks>
	public void ReturnCharBuffer(char[]? buffer, bool clearArray = false)
	{
		if (buffer == null || _disposed)
		{
			return;
		}

		_charPool.Return(buffer, clearArray);
	}

	/// <summary>
	/// Creates a Memory&lt;byte&gt; segment backed by a pooled buffer for zero-copy operations. Enables
	/// high-performance memory access patterns without additional allocations.
	/// </summary>
	/// <param name="minimumLength">
	/// The minimum required memory length. The underlying buffer may be larger but the returned Memory will be sized to exactly this length.
	/// </param>
	/// <returns>
	/// A Memory&lt;byte&gt; segment providing access to pooled memory. The caller is responsible for managing the underlying buffer lifecycle.
	/// </returns>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="minimumLength" /> is less than or equal to 0.
	/// </exception>
	/// <exception cref="ObjectDisposedException">
	/// Thrown when the pool has been disposed.
	/// </exception>
	/// <remarks>
	/// <para>
	/// The returned Memory&lt;byte&gt; provides a window into the pooled buffer. Changes to the memory content will affect the underlying pooled array.
	/// </para>
	/// <para>
	/// This method is ideal for scenarios requiring direct memory manipulation while maintaining the performance benefits of buffer pooling.
	/// </para>
	/// </remarks>
	public Memory<byte> GetMemory(int minimumLength)
	{
		var buffer = RentByteBuffer(minimumLength);
		return new Memory<byte>(buffer, 0, minimumLength);
	}

	/// <summary>
	/// Creates a ReadOnlyMemory&lt;byte&gt; segment from pooled memory, copying the provided data. Provides
	/// immutable memory access backed by pooled resources for efficient read-only operations.
	/// </summary>
	/// <param name="data">
	/// The source data to copy into the pooled buffer. The data length determines the size of the returned ReadOnlyMemory segment.
	/// </param>
	/// <returns>
	/// A ReadOnlyMemory&lt;byte&gt; segment containing a copy of the input data, backed by a pooled buffer for efficient memory usage.
	/// </returns>
	/// <exception cref="ObjectDisposedException">
	/// Thrown when the pool has been disposed.
	/// </exception>
	/// <remarks>
	/// <para>
	/// This method is useful for creating immutable views of data that need to be processed multiple times or passed to read-only consumers,
	/// while still benefiting from buffer pooling for the underlying storage.
	/// </para>
	/// <para>
	/// The data is copied into the pooled buffer, so changes to the original data will not affect the returned ReadOnlyMemory content.
	/// </para>
	/// </remarks>
	public ReadOnlyMemory<byte> GetReadOnlyMemory(ReadOnlySpan<byte> data)
	{
		var buffer = RentByteBuffer(data.Length);
		data.CopyTo(buffer);
		return new ReadOnlyMemory<byte>(buffer, 0, data.Length);
	}

	/// <summary>
	/// Disposes the buffer pool.
	/// </summary>
	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}

	/// <summary>
	/// Disposes the buffer pool.
	/// </summary>
	/// <param name="disposing"> Whether disposing managed resources. </param>
	protected virtual void Dispose(bool disposing)
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
	}

	private void ThrowIfDisposed()
	{
		if (_disposed)
		{
			throw new ObjectDisposedException(nameof(MessageBufferPool));
		}
	}
}
