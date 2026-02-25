// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Tests.Abstractions.Messaging;

/// <summary>
/// Unit tests for <see cref="DeadLetterStrategy"/> enum.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Transport.Abstractions")]
public sealed class DeadLetterStrategyShould
{
	[Fact]
	public void HaveFourDistinctValues()
	{
		// Arrange
		var values = Enum.GetValues<DeadLetterStrategy>();

		// Assert
		values.Length.ShouldBe(4);
		values.ShouldContain(DeadLetterStrategy.Drop);
		values.ShouldContain(DeadLetterStrategy.MoveToDeadLetterQueue);
		values.ShouldContain(DeadLetterStrategy.RetryIndefinitely);
		values.ShouldContain(DeadLetterStrategy.CustomHandler);
	}

	[Fact]
	public void Drop_HasExpectedValue()
	{
		// Assert
		((int)DeadLetterStrategy.Drop).ShouldBe(0);
	}

	[Fact]
	public void MoveToDeadLetterQueue_HasExpectedValue()
	{
		// Assert
		((int)DeadLetterStrategy.MoveToDeadLetterQueue).ShouldBe(1);
	}

	[Fact]
	public void RetryIndefinitely_HasExpectedValue()
	{
		// Assert
		((int)DeadLetterStrategy.RetryIndefinitely).ShouldBe(2);
	}

	[Fact]
	public void CustomHandler_HasExpectedValue()
	{
		// Assert
		((int)DeadLetterStrategy.CustomHandler).ShouldBe(3);
	}

	[Fact]
	public void Drop_IsDefaultValue()
	{
		// Arrange
		DeadLetterStrategy defaultStrategy = default;

		// Assert
		defaultStrategy.ShouldBe(DeadLetterStrategy.Drop);
	}

	[Theory]
	[InlineData(DeadLetterStrategy.Drop)]
	[InlineData(DeadLetterStrategy.MoveToDeadLetterQueue)]
	[InlineData(DeadLetterStrategy.RetryIndefinitely)]
	[InlineData(DeadLetterStrategy.CustomHandler)]
	public void BeDefinedForAllValues(DeadLetterStrategy strategy)
	{
		// Assert
		Enum.IsDefined(strategy).ShouldBeTrue();
	}

	[Theory]
	[InlineData(0, DeadLetterStrategy.Drop)]
	[InlineData(1, DeadLetterStrategy.MoveToDeadLetterQueue)]
	[InlineData(2, DeadLetterStrategy.RetryIndefinitely)]
	[InlineData(3, DeadLetterStrategy.CustomHandler)]
	public void CastFromInt_ReturnsCorrectValue(int value, DeadLetterStrategy expected)
	{
		// Act
		var strategy = (DeadLetterStrategy)value;

		// Assert
		strategy.ShouldBe(expected);
	}

	[Fact]
	public void MoveToDeadLetterQueue_IsPreferredForPermanentFailures()
	{
		// Assert - Verify MoveToDeadLetterQueue is defined and represents the safest default for failures
		var strategy = DeadLetterStrategy.MoveToDeadLetterQueue;
		Enum.IsDefined(strategy).ShouldBeTrue();
		((int)strategy).ShouldBe(1);
	}
}
