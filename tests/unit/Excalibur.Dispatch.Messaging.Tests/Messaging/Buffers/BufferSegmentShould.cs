// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Buffers;

namespace Excalibur.Dispatch.Tests.Messaging.Buffers;

/// <summary>
/// Unit tests for <see cref="BufferSegment"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class BufferSegmentShould
{
	[Fact]
	public void CreateFromByteArray()
	{
		// Arrange
		var buffer = new byte[] { 1, 2, 3, 4, 5 };

		// Act
		var segment = new BufferSegment(buffer, 1, 3);

		// Assert
		segment.Buffer.ShouldBeSameAs(buffer);
		segment.Offset.ShouldBe(1);
		segment.Length.ShouldBe(3);
		segment.PooledBuffer.ShouldBeNull();
	}

	[Fact]
	public void CreateFromPooledBuffer()
	{
		// Arrange
		var buffer = new byte[] { 1, 2, 3, 4, 5 };
		var pooledBuffer = new TestPooledBuffer(buffer);

		// Act
		var segment = new BufferSegment(pooledBuffer, 0, 5);

		// Assert
		segment.Buffer.ShouldBeSameAs(buffer);
		segment.Offset.ShouldBe(0);
		segment.Length.ShouldBe(5);
		segment.PooledBuffer.ShouldBeSameAs(pooledBuffer);
	}

	[Fact]
	public void ThrowOnNullBuffer()
	{
		// Arrange & Act & Assert
		Should.Throw<ArgumentNullException>(() => new BufferSegment((byte[])null!, 0, 0));
	}

	[Fact]
	public void ThrowOnNullPooledBuffer()
	{
		// Arrange & Act & Assert
		Should.Throw<ArgumentNullException>(() => new BufferSegment((IPooledBuffer?)null, 0, 0));
	}

	[Theory]
	[InlineData(-1, 0)]
	[InlineData(6, 0)]
	public void ThrowOnInvalidOffset(int offset, int length)
	{
		// Arrange
		var buffer = new byte[] { 1, 2, 3, 4, 5 };

		// Act & Assert
		Should.Throw<ArgumentOutOfRangeException>(() => new BufferSegment(buffer, offset, length));
	}

	[Theory]
	[InlineData(0, -1)]
	[InlineData(0, 6)]
	[InlineData(3, 3)]
	public void ThrowOnInvalidLength(int offset, int length)
	{
		// Arrange
		var buffer = new byte[] { 1, 2, 3, 4, 5 };

		// Act & Assert
		Should.Throw<ArgumentOutOfRangeException>(() => new BufferSegment(buffer, offset, length));
	}

	[Fact]
	public void ProvideMemoryView()
	{
		// Arrange
		var buffer = new byte[] { 1, 2, 3, 4, 5 };
		var segment = new BufferSegment(buffer, 1, 3);

		// Act
		var memory = segment.Memory;

		// Assert
		memory.Length.ShouldBe(3);
		memory.Span[0].ShouldBe((byte)2);
		memory.Span[1].ShouldBe((byte)3);
		memory.Span[2].ShouldBe((byte)4);
	}

	[Fact]
	public void ProvideSpanView()
	{
		// Arrange
		var buffer = new byte[] { 1, 2, 3, 4, 5 };
		var segment = new BufferSegment(buffer, 1, 3);

		// Act
		var span = segment.Span;

		// Assert
		span.Length.ShouldBe(3);
		span[0].ShouldBe((byte)2);
		span[1].ShouldBe((byte)3);
		span[2].ShouldBe((byte)4);
	}

	[Fact]
	public void CreateArraySegment()
	{
		// Arrange
		var buffer = new byte[] { 1, 2, 3, 4, 5 };
		var segment = new BufferSegment(buffer, 1, 3);

		// Act
		var arraySegment = segment.AsArraySegment();

		// Assert
		arraySegment.Array.ShouldBeSameAs(buffer);
		arraySegment.Offset.ShouldBe(1);
		arraySegment.Count.ShouldBe(3);
	}

	[Fact]
	public void BeEqualToSameSegment()
	{
		// Arrange
		var buffer = new byte[] { 1, 2, 3, 4, 5 };
		var segment1 = new BufferSegment(buffer, 1, 3);
		var segment2 = new BufferSegment(buffer, 1, 3);

		// Act & Assert
		segment1.Equals(segment2).ShouldBeTrue();
		(segment1 == segment2).ShouldBeTrue();
		(segment1 != segment2).ShouldBeFalse();
	}

	[Fact]
	public void NotBeEqualToDifferentBuffer()
	{
		// Arrange
		var buffer1 = new byte[] { 1, 2, 3, 4, 5 };
		var buffer2 = new byte[] { 1, 2, 3, 4, 5 };
		var segment1 = new BufferSegment(buffer1, 0, 5);
		var segment2 = new BufferSegment(buffer2, 0, 5);

		// Act & Assert
		segment1.Equals(segment2).ShouldBeFalse();
		(segment1 == segment2).ShouldBeFalse();
		(segment1 != segment2).ShouldBeTrue();
	}

	[Fact]
	public void NotBeEqualToDifferentOffset()
	{
		// Arrange
		var buffer = new byte[] { 1, 2, 3, 4, 5 };
		var segment1 = new BufferSegment(buffer, 0, 3);
		var segment2 = new BufferSegment(buffer, 1, 3);

		// Act & Assert
		segment1.Equals(segment2).ShouldBeFalse();
	}

	[Fact]
	public void NotBeEqualToDifferentLength()
	{
		// Arrange
		var buffer = new byte[] { 1, 2, 3, 4, 5 };
		var segment1 = new BufferSegment(buffer, 0, 3);
		var segment2 = new BufferSegment(buffer, 0, 4);

		// Act & Assert
		segment1.Equals(segment2).ShouldBeFalse();
	}

	[Fact]
	public void NotBeEqualToDifferentPooledBuffer()
	{
		// Arrange
		var buffer = new byte[] { 1, 2, 3, 4, 5 };
		var pooledBuffer1 = new TestPooledBuffer(buffer);
		var pooledBuffer2 = new TestPooledBuffer(buffer);
		var segment1 = new BufferSegment(pooledBuffer1, 0, 5);
		var segment2 = new BufferSegment(pooledBuffer2, 0, 5);

		// Act & Assert
		segment1.Equals(segment2).ShouldBeFalse();
	}

	[Fact]
	public void ImplementObjectEqualsCorrectly()
	{
		// Arrange
		var buffer = new byte[] { 1, 2, 3, 4, 5 };
		var segment = new BufferSegment(buffer, 0, 5);

		// Act & Assert
		segment.Equals((object)segment).ShouldBeTrue();
		segment.Equals(null).ShouldBeFalse();
		segment.Equals("not a segment").ShouldBeFalse();
	}

	[Fact]
	public void HaveConsistentHashCode()
	{
		// Arrange
		var buffer = new byte[] { 1, 2, 3, 4, 5 };
		var segment1 = new BufferSegment(buffer, 0, 5);
		var segment2 = new BufferSegment(buffer, 0, 5);

		// Act & Assert
		segment1.GetHashCode().ShouldBe(segment2.GetHashCode());
	}

	[Fact]
	public void HaveDifferentHashCodesForDifferentSegments()
	{
		// Arrange
		var buffer = new byte[] { 1, 2, 3, 4, 5 };
		var segment1 = new BufferSegment(buffer, 0, 3);
		var segment2 = new BufferSegment(buffer, 0, 4);

		// Act & Assert
		segment1.GetHashCode().ShouldNotBe(segment2.GetHashCode());
	}

	[Fact]
	public void AllowZeroLengthSegment()
	{
		// Arrange
		var buffer = new byte[] { 1, 2, 3 };

		// Act
		var segment = new BufferSegment(buffer, 0, 0);

		// Assert
		segment.Length.ShouldBe(0);
		segment.Memory.Length.ShouldBe(0);
	}

	[Fact]
	public void AllowOffsetAtEndOfBuffer()
	{
		// Arrange
		var buffer = new byte[] { 1, 2, 3 };

		// Act
		var segment = new BufferSegment(buffer, 3, 0);

		// Assert
		segment.Offset.ShouldBe(3);
		segment.Length.ShouldBe(0);
	}

	// Test helper class
	private sealed class TestPooledBuffer : IPooledBuffer
	{
		public TestPooledBuffer(byte[] buffer) => Buffer = buffer;

		public byte[] Buffer { get; }

		public byte[] Array => Buffer;

		public int Size => Buffer.Length;

		public int Length => Buffer.Length;

		public Memory<byte> Memory => Buffer.AsMemory();

		public Span<byte> Span => Buffer.AsSpan();
	}
}
