// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Tests.Abstractions.Messaging;

/// <summary>
/// Unit tests for <see cref="DestinationType"/> enum.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Transport.Abstractions")]
public sealed class DestinationTypeShould
{
	[Fact]
	public void HaveThreeDistinctValues()
	{
		// Arrange
		var values = Enum.GetValues<DestinationType>();

		// Assert
		values.Length.ShouldBe(3);
		values.ShouldContain(DestinationType.Queue);
		values.ShouldContain(DestinationType.Topic);
		values.ShouldContain(DestinationType.Subscription);
	}

	[Fact]
	public void Queue_HasExpectedValue()
	{
		// Assert
		((int)DestinationType.Queue).ShouldBe(0);
	}

	[Fact]
	public void Topic_HasExpectedValue()
	{
		// Assert
		((int)DestinationType.Topic).ShouldBe(1);
	}

	[Fact]
	public void Subscription_HasExpectedValue()
	{
		// Assert
		((int)DestinationType.Subscription).ShouldBe(2);
	}

	[Fact]
	public void Queue_IsDefaultValue()
	{
		// Arrange
		DestinationType defaultType = default;

		// Assert
		defaultType.ShouldBe(DestinationType.Queue);
	}

	[Theory]
	[InlineData(DestinationType.Queue)]
	[InlineData(DestinationType.Topic)]
	[InlineData(DestinationType.Subscription)]
	public void BeDefinedForAllValues(DestinationType type)
	{
		// Assert
		Enum.IsDefined(type).ShouldBeTrue();
	}

	[Theory]
	[InlineData(0, DestinationType.Queue)]
	[InlineData(1, DestinationType.Topic)]
	[InlineData(2, DestinationType.Subscription)]
	public void CastFromInt_ReturnsCorrectValue(int value, DestinationType expected)
	{
		// Act
		var type = (DestinationType)value;

		// Assert
		type.ShouldBe(expected);
	}

	[Fact]
	public void Queue_RepresentsPointToPoint()
	{
		// Assert - Queue is for point-to-point messaging (one consumer per message)
		Enum.IsDefined(DestinationType.Queue).ShouldBeTrue();
	}

	[Fact]
	public void Topic_RepresentsPublishSubscribe()
	{
		// Assert - Topic is for publish-subscribe messaging (multiple consumers)
		Enum.IsDefined(DestinationType.Topic).ShouldBeTrue();
	}
}
