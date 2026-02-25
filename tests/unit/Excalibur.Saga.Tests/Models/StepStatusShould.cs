// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.Models;

namespace Excalibur.Saga.Tests.Models;

/// <summary>
/// Unit tests for <see cref="StepStatus"/> enum.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Saga")]
[Trait("Feature", "Models")]
public sealed class StepStatusShould : UnitTestBase
{
	[Fact]
	public void HaveNotStartedAsZero()
	{
		// Assert
		((int)StepStatus.NotStarted).ShouldBe(0);
	}

	[Fact]
	public void HaveRunningAsOne()
	{
		// Assert
		((int)StepStatus.Running).ShouldBe(1);
	}

	[Fact]
	public void HaveSucceededAsTwo()
	{
		// Assert
		((int)StepStatus.Succeeded).ShouldBe(2);
	}

	[Fact]
	public void HaveFailedAsThree()
	{
		// Assert
		((int)StepStatus.Failed).ShouldBe(3);
	}

	[Fact]
	public void HaveSkippedAsFour()
	{
		// Assert
		((int)StepStatus.Skipped).ShouldBe(4);
	}

	[Fact]
	public void HaveTimedOutAsFive()
	{
		// Assert
		((int)StepStatus.TimedOut).ShouldBe(5);
	}

	[Fact]
	public void HaveSixDefinedValues()
	{
		// Assert
		Enum.GetValues<StepStatus>().Length.ShouldBe(6);
	}

	[Fact]
	public void DefaultToNotStarted()
	{
		// Act
		var defaultValue = default(StepStatus);

		// Assert
		defaultValue.ShouldBe(StepStatus.NotStarted);
	}

	[Theory]
	[InlineData(StepStatus.NotStarted, "NotStarted")]
	[InlineData(StepStatus.Running, "Running")]
	[InlineData(StepStatus.Succeeded, "Succeeded")]
	[InlineData(StepStatus.Failed, "Failed")]
	[InlineData(StepStatus.Skipped, "Skipped")]
	[InlineData(StepStatus.TimedOut, "TimedOut")]
	public void HaveCorrectStringRepresentation(StepStatus status, string expected)
	{
		// Assert
		status.ToString().ShouldBe(expected);
	}
}
