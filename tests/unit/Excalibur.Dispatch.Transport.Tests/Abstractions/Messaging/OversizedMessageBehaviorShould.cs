// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Tests.Abstractions.Messaging;

/// <summary>
/// Unit tests for <see cref="OversizedMessageBehavior"/> enum.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Transport.Abstractions")]
public sealed class OversizedMessageBehaviorShould
{
	[Fact]
	public void HaveThreeDistinctValues()
	{
		// Arrange
		var values = Enum.GetValues<OversizedMessageBehavior>();

		// Assert
		values.Length.ShouldBe(3);
		values.ShouldContain(OversizedMessageBehavior.SendSeparately);
		values.ShouldContain(OversizedMessageBehavior.Skip);
		values.ShouldContain(OversizedMessageBehavior.ThrowException);
	}

	[Fact]
	public void SendSeparately_HasExpectedValue()
	{
		// Assert
		((int)OversizedMessageBehavior.SendSeparately).ShouldBe(0);
	}

	[Fact]
	public void Skip_HasExpectedValue()
	{
		// Assert
		((int)OversizedMessageBehavior.Skip).ShouldBe(1);
	}

	[Fact]
	public void ThrowException_HasExpectedValue()
	{
		// Assert
		((int)OversizedMessageBehavior.ThrowException).ShouldBe(2);
	}

	[Fact]
	public void SendSeparately_IsDefaultValue()
	{
		// Arrange
		OversizedMessageBehavior defaultBehavior = default;

		// Assert
		defaultBehavior.ShouldBe(OversizedMessageBehavior.SendSeparately);
	}

	[Theory]
	[InlineData(OversizedMessageBehavior.SendSeparately)]
	[InlineData(OversizedMessageBehavior.Skip)]
	[InlineData(OversizedMessageBehavior.ThrowException)]
	public void BeDefinedForAllValues(OversizedMessageBehavior behavior)
	{
		// Assert
		Enum.IsDefined(behavior).ShouldBeTrue();
	}

	[Theory]
	[InlineData(0, OversizedMessageBehavior.SendSeparately)]
	[InlineData(1, OversizedMessageBehavior.Skip)]
	[InlineData(2, OversizedMessageBehavior.ThrowException)]
	public void CastFromInt_ReturnsCorrectValue(int value, OversizedMessageBehavior expected)
	{
		// Act
		var behavior = (OversizedMessageBehavior)value;

		// Assert
		behavior.ShouldBe(expected);
	}

	[Fact]
	public void SendSeparately_IsRecommendedDefault()
	{
		// Assert - SendSeparately is the safest default as it doesn't lose messages
		var defaultBehavior = default(OversizedMessageBehavior);
		defaultBehavior.ShouldBe(OversizedMessageBehavior.SendSeparately);
	}
}
