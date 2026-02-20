// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Buffers;
using System.Text.Json;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Serialization;

/// <summary>
/// Represents a context that manages both a pooled JSON writer and its buffer.
/// </summary>
public sealed class PooledJsonWriterContext : IDisposable
{
	private readonly IUtf8JsonWriterPool _pool;
	private readonly ArrayBufferWriter<byte> _bufferWriter;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="PooledJsonWriterContext" /> class.
	/// </summary>
	internal PooledJsonWriterContext(IUtf8JsonWriterPool pool, Utf8JsonWriter writer, ArrayBufferWriter<byte> bufferWriter)
	{
		_pool = pool ?? throw new ArgumentNullException(nameof(pool));
		Writer = writer ?? throw new ArgumentNullException(nameof(writer));
		_bufferWriter = bufferWriter ?? throw new ArgumentNullException(nameof(bufferWriter));
	}

	/// <summary>
	/// Gets the JSON writer.
	/// </summary>
	/// <value>The current <see cref="Writer"/> value.</value>
	public Utf8JsonWriter Writer { get; }

	/// <summary>
	/// Gets the written bytes as a ReadOnlyMemory.
	/// </summary>
	/// <value>The current <see cref="WrittenMemory"/> value.</value>
	public ReadOnlyMemory<byte> WrittenMemory => _bufferWriter.WrittenMemory;

	/// <summary>
	/// Gets the written bytes as a ReadOnlySpan.
	/// </summary>
	/// <value>The current <see cref="WrittenSpan"/> value.</value>
	public ReadOnlySpan<byte> WrittenSpan => _bufferWriter.WrittenSpan;

	/// <summary>
	/// Gets the number of bytes written.
	/// </summary>
	/// <value>The current <see cref="WrittenCount"/> value.</value>
	public int WrittenCount => _bufferWriter.WrittenCount;

	/// <summary>
	/// Gets a pooled buffer containing the written data.
	/// </summary>
	/// <param name="bufferManager"> The buffer manager to use. </param>
	/// <returns> A <see cref="PooledBuffer" /> containing the written data. </returns>
	public PooledBuffer GetPooledBuffer(IPooledBufferService bufferManager)
	{
		ArgumentNullException.ThrowIfNull(bufferManager);

		Writer.Flush();
		var writtenMemory = _bufferWriter.WrittenMemory;
		var pooledBuffer = bufferManager.RentBuffer(writtenMemory.Length);
		writtenMemory.CopyTo(pooledBuffer.Memory);

		return (PooledBuffer)pooledBuffer;
	}

	/// <summary>
	/// Copies the written data to a destination span.
	/// </summary>
	/// <param name="destination"> The destination span. </param>
	public void CopyTo(Span<byte> destination)
	{
		Writer.Flush();
		_bufferWriter.WrittenSpan.CopyTo(destination);
	}

	/// <summary>
	/// Returns the writer to the pool.
	/// </summary>
	public void Dispose()
	{
		if (!_disposed)
		{
			_pool.ReturnToPool(Writer);
			_disposed = true;
		}
	}
}
