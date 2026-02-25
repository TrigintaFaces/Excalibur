// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Tests.Abstractions.Messaging;

/// <summary>
/// Unit tests for <see cref="MessagePriority"/> enum.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Transport.Abstractions")]
public sealed class MessagePriorityShould
{
	[Fact]
	public void HaveFourDistinctValues()
	{
		// Arrange
		var values = Enum.GetValues<MessagePriority>();

		// Assert
		values.Length.ShouldBe(4);
		values.ShouldContain(MessagePriority.Low);
		values.ShouldContain(MessagePriority.Normal);
		values.ShouldContain(MessagePriority.High);
		values.ShouldContain(MessagePriority.Critical);
	}

	[Fact]
	public void Low_HasExpectedValue()
	{
		// Assert
		((int)MessagePriority.Low).ShouldBe(0);
	}

	[Fact]
	public void Normal_HasExpectedValue()
	{
		// Assert
		((int)MessagePriority.Normal).ShouldBe(1);
	}

	[Fact]
	public void High_HasExpectedValue()
	{
		// Assert
		((int)MessagePriority.High).ShouldBe(2);
	}

	[Fact]
	public void Critical_HasExpectedValue()
	{
		// Assert
		((int)MessagePriority.Critical).ShouldBe(3);
	}

	[Fact]
	public void Low_IsDefaultValue()
	{
		// Arrange
		MessagePriority defaultPriority = default;

		// Assert
		defaultPriority.ShouldBe(MessagePriority.Low);
	}

	[Theory]
	[InlineData(MessagePriority.Low)]
	[InlineData(MessagePriority.Normal)]
	[InlineData(MessagePriority.High)]
	[InlineData(MessagePriority.Critical)]
	public void BeDefinedForAllValues(MessagePriority priority)
	{
		// Assert
		Enum.IsDefined(priority).ShouldBeTrue();
	}

	[Theory]
	[InlineData(0, MessagePriority.Low)]
	[InlineData(1, MessagePriority.Normal)]
	[InlineData(2, MessagePriority.High)]
	[InlineData(3, MessagePriority.Critical)]
	public void CastFromInt_ReturnsCorrectValue(int value, MessagePriority expected)
	{
		// Act
		var priority = (MessagePriority)value;

		// Assert
		priority.ShouldBe(expected);
	}

	[Fact]
	public void HaveCorrectPriorityOrder()
	{
		// Assert - values should increase with priority
		((int)MessagePriority.Low).ShouldBeLessThan((int)MessagePriority.Normal);
		((int)MessagePriority.Normal).ShouldBeLessThan((int)MessagePriority.High);
		((int)MessagePriority.High).ShouldBeLessThan((int)MessagePriority.Critical);
	}

	[Fact]
	public void Critical_IsHighestPriority()
	{
		// Assert
		var maxValue = Enum.GetValues<MessagePriority>().Max();
		maxValue.ShouldBe(MessagePriority.Critical);
	}
}
