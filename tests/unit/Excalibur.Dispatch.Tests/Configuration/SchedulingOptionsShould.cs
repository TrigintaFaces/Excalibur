// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Options.Scheduling;

namespace Excalibur.Dispatch.Tests.Configuration;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class SchedulingOptionsShould
{
	// --- SchedulerOptions ---

	[Fact]
	public void SchedulerOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new SchedulerOptions();

		// Assert
		options.PollInterval.ShouldBe(TimeSpan.FromSeconds(30));
		options.PastScheduleBehavior.ShouldBe(PastScheduleBehavior.ExecuteImmediately);
	}

	[Fact]
	public void SchedulerOptions_AllProperties_AreSettable()
	{
		// Act
		var options = new SchedulerOptions
		{
			PollInterval = TimeSpan.FromMinutes(1),
			PastScheduleBehavior = PastScheduleBehavior.Reject,
		};

		// Assert
		options.PollInterval.ShouldBe(TimeSpan.FromMinutes(1));
		options.PastScheduleBehavior.ShouldBe(PastScheduleBehavior.Reject);
	}

	// --- CronScheduleOptions ---

	[Fact]
	public void CronScheduleOptions_DefaultValues_AreCorrect()
	{
		// Act
		var options = new CronScheduleOptions();

		// Assert
		options.DefaultTimeZone.ShouldBe(TimeZoneInfo.Utc);
		options.IncludeSeconds.ShouldBeFalse();
		options.MissedExecutionBehavior.ShouldBe(MissedExecutionBehavior.SkipMissed);
		options.MaxMissedExecutions.ShouldBe(10);
		options.AutoAdjustForDaylightSaving.ShouldBeTrue();
		options.ExecutionToleranceWindow.ShouldBe(TimeSpan.FromSeconds(1));
		options.EnableExtendedSyntax.ShouldBeTrue();
		options.SupportedTimeZoneIds.ShouldNotBeNull();
		options.SupportedTimeZoneIds.ShouldBeEmpty();
		options.EnableDetailedLogging.ShouldBeFalse();
	}

	[Fact]
	public void CronScheduleOptions_AllProperties_AreSettable()
	{
		// Act
		var options = new CronScheduleOptions
		{
			DefaultTimeZone = TimeZoneInfo.Local,
			IncludeSeconds = true,
			MissedExecutionBehavior = MissedExecutionBehavior.ExecuteAllMissed,
			MaxMissedExecutions = 5,
			AutoAdjustForDaylightSaving = false,
			ExecutionToleranceWindow = TimeSpan.FromSeconds(5),
			EnableExtendedSyntax = false,
			EnableDetailedLogging = true,
		};

		// Assert
		options.DefaultTimeZone.ShouldBe(TimeZoneInfo.Local);
		options.IncludeSeconds.ShouldBeTrue();
		options.MissedExecutionBehavior.ShouldBe(MissedExecutionBehavior.ExecuteAllMissed);
		options.MaxMissedExecutions.ShouldBe(5);
		options.AutoAdjustForDaylightSaving.ShouldBeFalse();
		options.ExecutionToleranceWindow.ShouldBe(TimeSpan.FromSeconds(5));
		options.EnableExtendedSyntax.ShouldBeFalse();
		options.EnableDetailedLogging.ShouldBeTrue();
	}

	[Fact]
	public void CronScheduleOptions_SupportedTimeZoneIds_CanAddEntries()
	{
		// Arrange
		var options = new CronScheduleOptions();

		// Act
		options.SupportedTimeZoneIds.Add("UTC");
		options.SupportedTimeZoneIds.Add("America/New_York");

		// Assert
		options.SupportedTimeZoneIds.Count.ShouldBe(2);
		options.SupportedTimeZoneIds.ShouldContain("UTC");
	}

	// --- PastScheduleBehavior ---

	[Fact]
	public void PastScheduleBehavior_HaveExpectedValues()
	{
		// Assert
		PastScheduleBehavior.Reject.ShouldBe((PastScheduleBehavior)0);
		PastScheduleBehavior.ExecuteImmediately.ShouldBe((PastScheduleBehavior)1);
	}

	[Fact]
	public void PastScheduleBehavior_HaveTwoValues()
	{
		// Act
		var values = Enum.GetValues<PastScheduleBehavior>();

		// Assert
		values.Length.ShouldBe(2);
	}

	// --- MissedExecutionBehavior ---

	[Fact]
	public void MissedExecutionBehavior_HaveExpectedValues()
	{
		// Assert
		MissedExecutionBehavior.SkipMissed.ShouldBe((MissedExecutionBehavior)0);
		MissedExecutionBehavior.ExecuteLatestMissed.ShouldBe((MissedExecutionBehavior)1);
		MissedExecutionBehavior.ExecuteAllMissed.ShouldBe((MissedExecutionBehavior)2);
		MissedExecutionBehavior.DisableSchedule.ShouldBe((MissedExecutionBehavior)3);
	}

	[Fact]
	public void MissedExecutionBehavior_HaveFourValues()
	{
		// Act
		var values = Enum.GetValues<MissedExecutionBehavior>();

		// Assert
		values.Length.ShouldBe(4);
	}
}
