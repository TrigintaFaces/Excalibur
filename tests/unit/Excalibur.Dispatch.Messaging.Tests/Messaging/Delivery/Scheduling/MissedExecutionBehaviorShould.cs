// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Delivery;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery.Scheduling;

/// <summary>
/// Unit tests for <see cref="MissedExecutionBehavior"/> enum.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Dispatch")]
public sealed class MissedExecutionBehaviorShould
{
	[Fact]
	public void HaveFourDistinctValues()
	{
		// Arrange
		var values = Enum.GetValues<MissedExecutionBehavior>();

		// Assert
		values.Length.ShouldBe(4);
		values.ShouldContain(MissedExecutionBehavior.SkipMissed);
		values.ShouldContain(MissedExecutionBehavior.ExecuteLatestMissed);
		values.ShouldContain(MissedExecutionBehavior.ExecuteAllMissed);
		values.ShouldContain(MissedExecutionBehavior.DisableSchedule);
	}

	[Fact]
	public void SkipMissed_HasExpectedValue()
	{
		// Assert
		((int)MissedExecutionBehavior.SkipMissed).ShouldBe(0);
	}

	[Fact]
	public void ExecuteLatestMissed_HasExpectedValue()
	{
		// Assert
		((int)MissedExecutionBehavior.ExecuteLatestMissed).ShouldBe(1);
	}

	[Fact]
	public void ExecuteAllMissed_HasExpectedValue()
	{
		// Assert
		((int)MissedExecutionBehavior.ExecuteAllMissed).ShouldBe(2);
	}

	[Fact]
	public void DisableSchedule_HasExpectedValue()
	{
		// Assert
		((int)MissedExecutionBehavior.DisableSchedule).ShouldBe(3);
	}

	[Fact]
	public void SkipMissed_IsDefaultValue()
	{
		// Arrange
		MissedExecutionBehavior defaultBehavior = default;

		// Assert
		defaultBehavior.ShouldBe(MissedExecutionBehavior.SkipMissed);
	}

	[Theory]
	[InlineData(MissedExecutionBehavior.SkipMissed)]
	[InlineData(MissedExecutionBehavior.ExecuteLatestMissed)]
	[InlineData(MissedExecutionBehavior.ExecuteAllMissed)]
	[InlineData(MissedExecutionBehavior.DisableSchedule)]
	public void BeDefinedForAllValues(MissedExecutionBehavior behavior)
	{
		// Assert
		Enum.IsDefined(behavior).ShouldBeTrue();
	}

	[Theory]
	[InlineData(0, MissedExecutionBehavior.SkipMissed)]
	[InlineData(1, MissedExecutionBehavior.ExecuteLatestMissed)]
	[InlineData(2, MissedExecutionBehavior.ExecuteAllMissed)]
	[InlineData(3, MissedExecutionBehavior.DisableSchedule)]
	public void CastFromInt_ReturnsCorrectValue(int value, MissedExecutionBehavior expected)
	{
		// Act
		var behavior = (MissedExecutionBehavior)value;

		// Assert
		behavior.ShouldBe(expected);
	}
}
