// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Buffers;

/// <summary>
/// BCL-based pooled buffer implementation.
/// </summary>
/// <param name="pool"> The owning pool used to return buffers on dispose. </param>
/// <param name="buffer"> The underlying byte array buffer. </param>
internal sealed class BclPooledBuffer(BclBufferPoolAdapter pool, byte[] buffer) : IDisposablePooledBuffer
{
	private readonly BclBufferPoolAdapter _pool = pool ?? throw new ArgumentNullException(nameof(pool));
	private byte[]? _buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));

	/// <inheritdoc />
	public byte[] Buffer => _buffer ?? throw new ObjectDisposedException(nameof(BclPooledBuffer));

	/// <inheritdoc />
	public byte[] Array => Buffer;

	/// <inheritdoc />
	public int Size => Buffer.Length;

	/// <inheritdoc />
	public int Length => Buffer.Length;

	/// <inheritdoc />
	public Memory<byte> Memory => new(Buffer);

	/// <inheritdoc />
	public Span<byte> Span => Buffer.AsSpan();

	/// <inheritdoc />
	public void Dispose()
	{
		var buffer = Interlocked.Exchange(ref _buffer, value: null);
		if (buffer != null)
		{
			_pool.ReturnBuffer(buffer, clearBuffer: false);
		}
	}
}
