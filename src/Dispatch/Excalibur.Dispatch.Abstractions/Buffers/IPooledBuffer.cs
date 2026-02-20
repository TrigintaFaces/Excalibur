// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Represents a pooled buffer abstraction providing read/write access to pooled memory.
/// </summary>
/// <remarks>
/// <para>
/// This interface defines the buffer abstraction without forcing disposal semantics.
/// For buffers that must be returned to a pool, use <see cref="IDisposablePooledBuffer"/>
/// which extends this interface with <see cref="IDisposable"/>.
/// </para>
/// <para>
/// Per Microsoft Interface Segregation guidelines, interfaces should not force
/// <see cref="IDisposable"/> inheritance unless disposal is fundamental to all implementations.
/// </para>
/// </remarks>
/// <seealso cref="IDisposablePooledBuffer"/>
/// <seealso cref="IPooledBufferService"/>
public interface IPooledBuffer
{
	/// <summary>
	/// Gets the underlying byte array.
	/// </summary>
	byte[] Buffer { get; }

	/// <summary>
	/// Gets the underlying byte array (alias for Buffer).
	/// </summary>
	byte[] Array { get; }

	/// <summary>
	/// Gets the size of the buffer.
	/// </summary>
	int Size { get; }

	/// <summary>
	/// Gets the length of the buffer (alias for Size).
	/// </summary>
	int Length { get; }

	/// <summary> Gets a Memory&lt;byte&gt; view of the buffer. </summary>
	Memory<byte> Memory { get; }

	/// <summary> Gets a Span&lt;byte&gt; view of the buffer. </summary>
	Span<byte> Span { get; }
}

/// <summary>
/// Represents a pooled buffer that must be disposed to return to the pool.
/// </summary>
/// <remarks>
/// <para>
/// This interface extends <see cref="IPooledBuffer"/> with <see cref="IDisposable"/>
/// for buffers that are rented from a pool and must be returned when no longer needed.
/// </para>
/// <para>
/// Disposing the buffer returns it to the originating pool. After disposal,
/// accessing the buffer properties will throw <see cref="ObjectDisposedException"/>.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// using var buffer = bufferService.RentBuffer(1024);
/// // Use buffer.Buffer, buffer.Memory, or buffer.Span
/// // Buffer is automatically returned to pool when disposed
/// </code>
/// </example>
/// <seealso cref="IPooledBuffer"/>
/// <seealso cref="IPooledBufferService"/>
public interface IDisposablePooledBuffer : IPooledBuffer, IDisposable
{
}
