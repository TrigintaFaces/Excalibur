// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Buffers;

/// <summary>
/// Represents a segment of a buffer with offset and length.
/// </summary>
public readonly struct BufferSegment : IEquatable<BufferSegment>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="BufferSegment"/> struct.
	/// Initializes a new instance of BufferSegment.
	/// </summary>
	public BufferSegment(byte[] buffer, int offset, int length)
	{
		Buffer = buffer ?? throw new ArgumentNullException(nameof(buffer));

		if (offset < 0 || offset > buffer.Length)
		{
			throw new ArgumentOutOfRangeException(nameof(offset));
		}

		if (length < 0 || offset + length > buffer.Length)
		{
			throw new ArgumentOutOfRangeException(nameof(length));
		}

		Offset = offset;
		Length = length;
		PooledBuffer = null;
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="BufferSegment"/> struct.
	/// Initializes a new instance of BufferSegment from a pooled buffer.
	/// </summary>
	public BufferSegment(IPooledBuffer? pooledBuffer, int offset, int length)
		: this(pooledBuffer?.Buffer ?? throw new ArgumentNullException(nameof(pooledBuffer)), offset, length) =>
		PooledBuffer = pooledBuffer;

	/// <summary>
	/// Gets the buffer containing the segment.
	/// </summary>
	/// <value>The current <see cref="Buffer"/> value.</value>
	public byte[] Buffer { get; }

	/// <summary>
	/// Gets the offset within the buffer.
	/// </summary>
	/// <value>The current <see cref="Offset"/> value.</value>
	public int Offset { get; }

	/// <summary>
	/// Gets the length of the segment.
	/// </summary>
	/// <value>The current <see cref="Length"/> value.</value>
	public int Length { get; }

	/// <summary>
	/// Gets the pooled buffer if this segment is from a pooled buffer.
	/// </summary>
	/// <value>The current <see cref="PooledBuffer"/> value.</value>
	public IPooledBuffer? PooledBuffer { get; }

	/// <summary>
	/// Gets a Memory&lt;byte&gt; view of the segment.
	/// </summary>
	/// <value>A Memory&lt;byte&gt; view of the segment.</value>
	public Memory<byte> Memory => Buffer.AsMemory(Offset, Length);

	/// <summary>
	/// Gets a Span&lt;byte&gt; view of the segment.
	/// </summary>
	/// <value>A Span&lt;byte&gt; view of the segment.</value>
	public Span<byte> Span => Buffer.AsSpan(Offset, Length);

	/// <summary>
	/// Determines whether two BufferSegment instances are equal.
	/// </summary>
	public static bool operator ==(BufferSegment left, BufferSegment right) => left.Equals(right);

	/// <summary>
	/// Determines whether two BufferSegment instances are not equal.
	/// </summary>
	public static bool operator !=(BufferSegment left, BufferSegment right) => !left.Equals(right);

	/// <summary>
	/// Creates an ArraySegment from this buffer segment.
	/// </summary>
	public ArraySegment<byte> AsArraySegment() => new(Buffer, Offset, Length);

	/// <summary>
	/// Determines whether this BufferSegment is equal to another BufferSegment.
	/// </summary>
	public bool Equals(BufferSegment other) =>
		ReferenceEquals(Buffer, other.Buffer) &&
		Offset == other.Offset &&
		Length == other.Length &&
		ReferenceEquals(PooledBuffer, other.PooledBuffer);

	/// <summary>
	/// Determines whether this BufferSegment is equal to the specified object.
	/// </summary>
	public override bool Equals(object? obj) => obj is BufferSegment other && Equals(other);

	/// <summary>
	/// Returns the hash code for this BufferSegment.
	/// </summary>
	public override int GetHashCode() => HashCode.Combine(Buffer, Offset, Length, PooledBuffer);
}
