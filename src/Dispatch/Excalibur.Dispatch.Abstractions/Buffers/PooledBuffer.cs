// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Buffers;

namespace Excalibur.Dispatch;

/// <summary>
/// Represents a buffer that is rented from an array pool.
/// </summary>
public sealed class PooledBuffer : IDisposablePooledBuffer
{
	// Exactly one of these owns the array and is non-null; every constructor sets one and only one.
	// _pool is the pool the array was RENTED from, so disposal can return it there rather than
	// guessing at ArrayPool<byte>.Shared -- returning a foreign array to a process-wide pool starves
	// the pool it came from and hands Shared an array it never allocated.
	private readonly IPooledBufferService? _manager;
	private readonly ArrayPool<byte>? _pool;
	private readonly bool _clearOnReturn;

	// The array is the single source of truth for "do I still own a buffer": null means returned.
	// Volatile so a reader on another thread eventually observes the release rather than a stale array.
	private volatile byte[]? _buffer;

	// The claim token for the return. This is deliberately NOT a mirror of "_buffer is null" -- the two
	// differ for exactly the duration of the return call, because IPooledBufferService.ReturnBuffer takes
	// this wrapper and implementations read the array back off it.
	private int _returnClaimed;

	/// <summary>
	/// Initializes a new instance of the <see cref="PooledBuffer" /> class.
	/// </summary>
	/// <param name="manager"> The buffer manager that owns this buffer. </param>
	/// <param name="buffer"> The buffer array. </param>
	public PooledBuffer(IPooledBufferService manager, byte[] buffer)
	{
		_manager = manager ?? throw new ArgumentNullException(nameof(manager));
		_buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));
		_pool = null;
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
		_pool = null;
		_clearOnReturn = clearOnReturn;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="PooledBuffer" /> class using the shared array pool.
	/// </summary>
	/// <param name="size"> The minimum size of the buffer. </param>
	public PooledBuffer(int size)
	{
		_pool = ArrayPool<byte>.Shared;
		_buffer = _pool.Rent(size);
		_clearOnReturn = true;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="PooledBuffer" /> class using the specified array pool.
	/// </summary>
	/// <param name="size"> The minimum size of the buffer. </param>
	/// <param name="pool"> The array pool to use. </param>
	public PooledBuffer(int size, ArrayPool<byte> pool)
	{
		_pool = pool ?? ArrayPool<byte>.Shared;
		_buffer = _pool.Rent(size);
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
			var buffer = _buffer;
			ObjectDisposedException.ThrowIf(buffer is null, this);

			return buffer;
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
	public Memory<byte> Memory => new(Buffer);

	/// <summary>
	/// Gets a Span&lt;byte&gt; view of the buffer.
	/// </summary>
	public Span<byte> Span => new(Buffer);

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
	/// Disposes the buffer and returns it to the pool. Safe to call more than once, and safe to call
	/// from more than one thread: the buffer is returned exactly once.
	/// </summary>
	public void Dispose()
	{
		// Claim the return atomically. A read of a flag followed by a separate write is not atomic, so
		// two threads could both observe "not yet returned" and both return the same array to the pool --
		// which hands one array to two renters, and (because the return clears) lets one of them zero the
		// other's live data. Exactly one caller can observe 0 here, however many arrive at once.
		if (Interlocked.Exchange(ref _returnClaimed, 1) != 0)
		{
			return;
		}

		var buffer = _buffer;
		if (buffer is null)
		{
			return;
		}

		// The array stays reachable through this instance until the return completes: ReturnBuffer takes
		// the wrapper, and a buffer service reads the array back off it. Releasing the field first would
		// make that read throw straight out of Dispose and leak the buffer.
		if (_manager != null)
		{
			_manager.ReturnBuffer(this, clearBuffer: _clearOnReturn);
		}
		else
		{
			// Non-null whenever _manager is null: every constructor sets exactly one of the two.
			_pool!.Return(buffer, clearArray: _clearOnReturn);
		}

		_buffer = null;
	}
}
