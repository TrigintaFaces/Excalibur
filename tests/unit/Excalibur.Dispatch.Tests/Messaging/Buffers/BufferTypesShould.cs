// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Buffers;

namespace Excalibur.Dispatch.Tests.Messaging.Buffers;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class BufferTypesShould
{
	// --- BufferStatistics ---

	[Fact]
	public void BufferStatistics_DefaultValues_AreCorrect()
	{
		// Act
		var stats = new BufferStatistics();

		// Assert
		stats.TotalRented.ShouldBe(0);
		stats.TotalReturned.ShouldBe(0);
		stats.OutstandingBuffers.ShouldBe(0);
		stats.SizeDistribution.ShouldNotBeNull();
		stats.SizeDistribution.ShouldBeEmpty();
	}

	[Fact]
	public void BufferStatistics_AllProperties_AreSettable()
	{
		// Act
		var stats = new BufferStatistics
		{
			TotalRented = 100,
			TotalReturned = 90,
			OutstandingBuffers = 10,
		};

		// Assert
		stats.TotalRented.ShouldBe(100);
		stats.TotalReturned.ShouldBe(90);
		stats.OutstandingBuffers.ShouldBe(10);
	}

	[Fact]
	public void BufferStatistics_SizeDistribution_CanAddEntries()
	{
		// Arrange
		var stats = new BufferStatistics();

		// Act
		stats.SizeDistribution[256] = 50;
		stats.SizeDistribution[1024] = 30;

		// Assert
		stats.SizeDistribution.Count.ShouldBe(2);
		stats.SizeDistribution[256].ShouldBe(50);
		stats.SizeDistribution[1024].ShouldBe(30);
	}

	// --- BufferSegment ---

	[Fact]
	public void BufferSegment_Constructor_SetsProperties()
	{
		// Arrange
		var buffer = new byte[100];

		// Act
		var segment = new BufferSegment(buffer, 10, 50);

		// Assert
		segment.Buffer.ShouldBeSameAs(buffer);
		segment.Offset.ShouldBe(10);
		segment.Length.ShouldBe(50);
		segment.PooledBuffer.ShouldBeNull();
	}

	[Fact]
	public void BufferSegment_Constructor_WithNullBuffer_Throws()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => new BufferSegment((byte[])null!, 0, 0));
	}

	[Fact]
	public void BufferSegment_Constructor_WithNegativeOffset_Throws()
	{
		// Arrange
		var buffer = new byte[100];

		// Act & Assert
		Should.Throw<ArgumentOutOfRangeException>(() => new BufferSegment(buffer, -1, 10));
	}

	[Fact]
	public void BufferSegment_Constructor_WithOffsetBeyondLength_Throws()
	{
		// Arrange
		var buffer = new byte[100];

		// Act & Assert
		Should.Throw<ArgumentOutOfRangeException>(() => new BufferSegment(buffer, 101, 0));
	}

	[Fact]
	public void BufferSegment_Constructor_WithNegativeLength_Throws()
	{
		// Arrange
		var buffer = new byte[100];

		// Act & Assert
		Should.Throw<ArgumentOutOfRangeException>(() => new BufferSegment(buffer, 0, -1));
	}

	[Fact]
	public void BufferSegment_Constructor_WithLengthBeyondBuffer_Throws()
	{
		// Arrange
		var buffer = new byte[100];

		// Act & Assert
		Should.Throw<ArgumentOutOfRangeException>(() => new BufferSegment(buffer, 50, 60));
	}

	[Fact]
	public void BufferSegment_Memory_ReturnsCorrectSlice()
	{
		// Arrange
		var buffer = new byte[100];
		buffer[10] = 42;
		var segment = new BufferSegment(buffer, 10, 50);

		// Act
		var memory = segment.Memory;

		// Assert
		memory.Length.ShouldBe(50);
		memory.Span[0].ShouldBe((byte)42);
	}

	[Fact]
	public void BufferSegment_AsArraySegment_ReturnsCorrectSegment()
	{
		// Arrange
		var buffer = new byte[100];
		var segment = new BufferSegment(buffer, 10, 50);

		// Act
		var arraySegment = segment.AsArraySegment();

		// Assert
		arraySegment.Array.ShouldBeSameAs(buffer);
		arraySegment.Offset.ShouldBe(10);
		arraySegment.Count.ShouldBe(50);
	}

	[Fact]
	public void BufferSegment_Equality_SameSegments_AreEqual()
	{
		// Arrange
		var buffer = new byte[100];
		var segment1 = new BufferSegment(buffer, 10, 50);
		var segment2 = new BufferSegment(buffer, 10, 50);

		// Assert
		segment1.Equals(segment2).ShouldBeTrue();
		(segment1 == segment2).ShouldBeTrue();
		(segment1 != segment2).ShouldBeFalse();
	}

	[Fact]
	public void BufferSegment_Equality_DifferentOffset_AreNotEqual()
	{
		// Arrange
		var buffer = new byte[100];
		var segment1 = new BufferSegment(buffer, 10, 50);
		var segment2 = new BufferSegment(buffer, 20, 50);

		// Assert
		segment1.Equals(segment2).ShouldBeFalse();
		(segment1 != segment2).ShouldBeTrue();
	}

	[Fact]
	public void BufferSegment_Equality_DifferentLength_AreNotEqual()
	{
		// Arrange
		var buffer = new byte[100];
		var segment1 = new BufferSegment(buffer, 10, 50);
		var segment2 = new BufferSegment(buffer, 10, 40);

		// Assert
		segment1.Equals(segment2).ShouldBeFalse();
	}

	[Fact]
	public void BufferSegment_Equality_DifferentBuffer_AreNotEqual()
	{
		// Arrange
		var buffer1 = new byte[100];
		var buffer2 = new byte[100];
		var segment1 = new BufferSegment(buffer1, 0, 50);
		var segment2 = new BufferSegment(buffer2, 0, 50);

		// Assert
		segment1.Equals(segment2).ShouldBeFalse();
	}

	[Fact]
	public void BufferSegment_Equals_WithObject_WorksCorrectly()
	{
		// Arrange
		var buffer = new byte[100];
		var segment = new BufferSegment(buffer, 10, 50);
		object boxedSegment = new BufferSegment(buffer, 10, 50);

		// Assert
		segment.Equals(boxedSegment).ShouldBeTrue();
		segment.Equals("not a segment").ShouldBeFalse();
		segment.Equals(null).ShouldBeFalse();
	}

	[Fact]
	public void BufferSegment_GetHashCode_SameSegments_SameHash()
	{
		// Arrange
		var buffer = new byte[100];
		var segment1 = new BufferSegment(buffer, 10, 50);
		var segment2 = new BufferSegment(buffer, 10, 50);

		// Assert
		segment1.GetHashCode().ShouldBe(segment2.GetHashCode());
	}

	[Fact]
	public void BufferSegment_WithPooledBuffer_SetsPooledBuffer()
	{
		// Arrange
		var buffer = new byte[100];
		var pooledBuffer = A.Fake<Excalibur.Dispatch.Abstractions.IPooledBuffer>();
		A.CallTo(() => pooledBuffer.Buffer).Returns(buffer);

		// Act
		var segment = new BufferSegment(pooledBuffer, 0, 50);

		// Assert
		segment.PooledBuffer.ShouldBe(pooledBuffer);
		segment.Buffer.ShouldBeSameAs(buffer);
	}

	[Fact]
	public void BufferSegment_WithNullPooledBuffer_Throws()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new BufferSegment((Excalibur.Dispatch.Abstractions.IPooledBuffer?)null, 0, 0));
	}
}
