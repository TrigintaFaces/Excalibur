// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Buffers;

namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Represents a buffer that is rented from an array pool.
/// </summary>
public sealed class PooledBuffer : IDisposablePooledBuffer
{
	private readonly IPooledBufferService? _manager;
	private readonly bool _clearOnReturn;
	private byte[]? _buffer;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="PooledBuffer" /> class.
	/// </summary>
	/// <param name="manager"> The buffer manager that owns this buffer. </param>
	/// <param name="buffer"> The buffer array. </param>
	public PooledBuffer(IPooledBufferService manager, byte[] buffer)
	{
		_manager = manager ?? throw new ArgumentNullException(nameof(manager));
		_buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
		_clearOnReturn = true;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="PooledBuffer" /> class.
	/// </summary>
	/// <param name="manager"> The buffer manager that owns this buffer. </param>
	/// <param name="buffer"> The buffer array. </param>
	/// <param name="clearOnReturn"> Whether to clear the buffer when returning to the pool. </param>
	public PooledBuffer(IPooledBufferService manager, byte[] buffer, bool clearOnReturn)
	{
		_manager = manager ?? throw new ArgumentNullException(nameof(manager));
		_buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
		_clearOnReturn = clearOnReturn;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="PooledBuffer" /> class using the shared array pool.
	/// </summary>
	/// <param name="size"> The minimum size of the buffer. </param>
	public PooledBuffer(int size)
	{
		_buffer = ArrayPool<byte>.Shared.Rent(size);
		_clearOnReturn = true;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="PooledBuffer" /> class using the specified array pool.
	/// </summary>
	/// <param name="size"> The minimum size of the buffer. </param>
	/// <param name="pool"> The array pool to use. </param>
	public PooledBuffer(int size, ArrayPool<byte> pool)
	{
		_buffer = (pool ?? ArrayPool<byte>.Shared).Rent(size);
		_clearOnReturn = true;
	}

	/// <summary>
	/// Gets the buffer array.
	/// </summary>
	/// <exception cref="ObjectDisposedException"> Thrown if the buffer has been disposed. </exception>
	public byte[] Buffer
	{
		get
		{
			ObjectDisposedException.ThrowIf(_disposed, this);

			return _buffer!;
		}
	}

	/// <summary>
	/// Gets the underlying byte array (alias for Buffer).
	/// </summary>
	/// <value> The current <see cref="Array" /> value. </value>
	public byte[] Array => Buffer;

	/// <summary>
	/// Gets the size of the buffer.
	/// </summary>
	/// <value> The current <see cref="Size" /> value. </value>
	public int Size => _buffer?.Length ?? 0;

	/// <summary>
	/// Gets the length of the buffer (alias for Size).
	/// </summary>
	/// <value> The current <see cref="Length" /> value. </value>
	public int Length => Size;

	/// <summary>
	/// Gets a Memory&lt;byte&gt; view of the buffer.
	/// </summary>
	public Memory<byte> Memory
	{
		get
		{
			ObjectDisposedException.ThrowIf(_disposed, this);

			return new Memory<byte>(_buffer);
		}
	}

	/// <summary>
	/// Gets a Span&lt;byte&gt; view of the buffer.
	/// </summary>
	public Span<byte> Span
	{
		get
		{
			ObjectDisposedException.ThrowIf(_disposed, this);

			return new Span<byte>(_buffer);
		}
	}

	/// <summary>
	/// Gets a span over the buffer.
	/// </summary>
	/// <returns> A span over the buffer. </returns>
	public Span<byte> AsSpan() => Span;

	/// <summary>
	/// Gets a memory over the buffer.
	/// </summary>
	/// <returns> A memory over the buffer. </returns>
	public Memory<byte> AsMemory() => Memory;

	/// <summary>
	/// Disposes the buffer and returns it to the pool.
	/// </summary>
	public void Dispose()
	{
		if (!_disposed)
		{
			if (_buffer != null)
			{
				if (_manager != null)
				{
					_manager.ReturnBuffer(this, clearBuffer: _clearOnReturn);
				}
				else
				{
					ArrayPool<byte>.Shared.Return(_buffer, clearArray: _clearOnReturn);
				}

				_buffer = null;
			}

			_disposed = true;
		}
	}
}
