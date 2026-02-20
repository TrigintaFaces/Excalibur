// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.RabbitMQ;

namespace Excalibur.Dispatch.Transport.Tests.RabbitMQ.Streams;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class StreamOffsetShould
{
	[Fact]
	public void CreateFirstOffset()
	{
		// Arrange & Act
		var offset = StreamOffset.First();

		// Assert
		offset.Type.ShouldBe(StreamOffsetType.First);
		offset.Offset.ShouldBeNull();
		offset.Timestamp.ShouldBeNull();
	}

	[Fact]
	public void CreateLastOffset()
	{
		// Arrange & Act
		var offset = StreamOffset.Last();

		// Assert
		offset.Type.ShouldBe(StreamOffsetType.Last);
		offset.Offset.ShouldBeNull();
		offset.Timestamp.ShouldBeNull();
	}

	[Fact]
	public void CreateNextOffset()
	{
		// Arrange & Act
		var offset = StreamOffset.Next();

		// Assert
		offset.Type.ShouldBe(StreamOffsetType.Next);
		offset.Offset.ShouldBeNull();
		offset.Timestamp.ShouldBeNull();
	}

	[Fact]
	public void CreateFromNumericOffset()
	{
		// Arrange & Act
		var offset = StreamOffset.FromOffset(42);

		// Assert
		offset.Type.ShouldBe(StreamOffsetType.Offset);
		offset.Offset.ShouldBe(42);
		offset.Timestamp.ShouldBeNull();
	}

	[Fact]
	public void CreateFromTimestamp()
	{
		// Arrange
		var ts = DateTimeOffset.UtcNow;

		// Act
		var offset = StreamOffset.FromTimestamp(ts);

		// Assert
		offset.Type.ShouldBe(StreamOffsetType.Timestamp);
		offset.Offset.ShouldBeNull();
		offset.Timestamp.ShouldBe(ts);
	}

	[Theory]
	[InlineData(StreamOffsetType.First, 0)]
	[InlineData(StreamOffsetType.Last, 1)]
	[InlineData(StreamOffsetType.Next, 2)]
	[InlineData(StreamOffsetType.Offset, 3)]
	[InlineData(StreamOffsetType.Timestamp, 4)]
	public void HaveCorrectStreamOffsetTypeValues(StreamOffsetType type, int expected)
	{
		((int)type).ShouldBe(expected);
	}

	[Fact]
	public void HaveAllStreamOffsetTypeMembers()
	{
		Enum.GetValues<StreamOffsetType>().Length.ShouldBe(5);
	}
}
