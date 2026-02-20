// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Transport.Tests.Abstractions.Scheduling;

/// <summary>
/// Unit tests for <see cref="ScheduleStatus"/> enum.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Transport.Abstractions")]
public sealed class ScheduleStatusShould
{
	[Fact]
	public void HaveFiveDistinctValues()
	{
		// Arrange
		var values = Enum.GetValues<ScheduleStatus>();

		// Assert
		values.Length.ShouldBe(5);
		values.ShouldContain(ScheduleStatus.Scheduled);
		values.ShouldContain(ScheduleStatus.InProgress);
		values.ShouldContain(ScheduleStatus.Completed);
		values.ShouldContain(ScheduleStatus.Failed);
		values.ShouldContain(ScheduleStatus.Cancelled);
	}

	[Fact]
	public void Scheduled_HasExpectedValue()
	{
		// Assert
		((int)ScheduleStatus.Scheduled).ShouldBe(0);
	}

	[Fact]
	public void InProgress_HasExpectedValue()
	{
		// Assert
		((int)ScheduleStatus.InProgress).ShouldBe(1);
	}

	[Fact]
	public void Completed_HasExpectedValue()
	{
		// Assert
		((int)ScheduleStatus.Completed).ShouldBe(2);
	}

	[Fact]
	public void Failed_HasExpectedValue()
	{
		// Assert
		((int)ScheduleStatus.Failed).ShouldBe(3);
	}

	[Fact]
	public void Cancelled_HasExpectedValue()
	{
		// Assert
		((int)ScheduleStatus.Cancelled).ShouldBe(4);
	}

	[Fact]
	public void Scheduled_IsDefaultValue()
	{
		// Arrange
		ScheduleStatus defaultStatus = default;

		// Assert
		defaultStatus.ShouldBe(ScheduleStatus.Scheduled);
	}

	[Theory]
	[InlineData(ScheduleStatus.Scheduled)]
	[InlineData(ScheduleStatus.InProgress)]
	[InlineData(ScheduleStatus.Completed)]
	[InlineData(ScheduleStatus.Failed)]
	[InlineData(ScheduleStatus.Cancelled)]
	public void BeDefinedForAllValues(ScheduleStatus status)
	{
		// Assert
		Enum.IsDefined(status).ShouldBeTrue();
	}

	[Theory]
	[InlineData(0, ScheduleStatus.Scheduled)]
	[InlineData(1, ScheduleStatus.InProgress)]
	[InlineData(2, ScheduleStatus.Completed)]
	[InlineData(3, ScheduleStatus.Failed)]
	[InlineData(4, ScheduleStatus.Cancelled)]
	public void CastFromInt_ReturnsCorrectValue(int value, ScheduleStatus expected)
	{
		// Act
		var status = (ScheduleStatus)value;

		// Assert
		status.ShouldBe(expected);
	}

	[Fact]
	public void HaveScheduleStatesOrderedByProgression()
	{
		// Assert - States progress from scheduled through in-progress to terminal states
		(ScheduleStatus.Scheduled < ScheduleStatus.InProgress).ShouldBeTrue();
		(ScheduleStatus.InProgress < ScheduleStatus.Completed).ShouldBeTrue();
		(ScheduleStatus.Completed < ScheduleStatus.Failed).ShouldBeTrue();
		(ScheduleStatus.Failed < ScheduleStatus.Cancelled).ShouldBeTrue();
	}
}
