// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Options.Scheduling;

namespace Excalibur.Dispatch.Tests.Options.Scheduling;

/// <summary>
/// Unit tests for <see cref="CronScheduleOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Options")]
[Trait("Priority", "0")]
public sealed class CronScheduleOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void Default_DefaultTimeZone_IsUtc()
	{
		// Arrange & Act
		var options = new CronScheduleOptions();

		// Assert
		options.DefaultTimeZone.ShouldBe(TimeZoneInfo.Utc);
	}

	[Fact]
	public void Default_IncludeSeconds_IsFalse()
	{
		// Arrange & Act
		var options = new CronScheduleOptions();

		// Assert
		options.IncludeSeconds.ShouldBeFalse();
	}

	[Fact]
	public void Default_MissedExecutionBehavior_IsSkipMissed()
	{
		// Arrange & Act
		var options = new CronScheduleOptions();

		// Assert
		options.MissedExecutionBehavior.ShouldBe(MissedExecutionBehavior.SkipMissed);
	}

	[Fact]
	public void Default_MaxMissedExecutions_Is10()
	{
		// Arrange & Act
		var options = new CronScheduleOptions();

		// Assert
		options.MaxMissedExecutions.ShouldBe(10);
	}

	[Fact]
	public void Default_AutoAdjustForDaylightSaving_IsTrue()
	{
		// Arrange & Act
		var options = new CronScheduleOptions();

		// Assert
		options.AutoAdjustForDaylightSaving.ShouldBeTrue();
	}

	[Fact]
	public void Default_ExecutionToleranceWindow_Is1Second()
	{
		// Arrange & Act
		var options = new CronScheduleOptions();

		// Assert
		options.ExecutionToleranceWindow.ShouldBe(TimeSpan.FromSeconds(1));
	}

	[Fact]
	public void Default_EnableExtendedSyntax_IsTrue()
	{
		// Arrange & Act
		var options = new CronScheduleOptions();

		// Assert
		options.EnableExtendedSyntax.ShouldBeTrue();
	}

	[Fact]
	public void Default_SupportedTimeZoneIds_IsEmpty()
	{
		// Arrange & Act
		var options = new CronScheduleOptions();

		// Assert
		_ = options.SupportedTimeZoneIds.ShouldNotBeNull();
		options.SupportedTimeZoneIds.ShouldBeEmpty();
	}

	[Fact]
	public void Default_EnableDetailedLogging_IsFalse()
	{
		// Arrange & Act
		var options = new CronScheduleOptions();

		// Assert
		options.EnableDetailedLogging.ShouldBeFalse();
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void DefaultTimeZone_CanBeSet()
	{
		// Arrange
		var options = new CronScheduleOptions();
		var easternTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");

		// Act
		options.DefaultTimeZone = easternTimeZone;

		// Assert
		options.DefaultTimeZone.ShouldBe(easternTimeZone);
	}

	[Fact]
	public void IncludeSeconds_CanBeSet()
	{
		// Arrange
		var options = new CronScheduleOptions();

		// Act
		options.IncludeSeconds = true;

		// Assert
		options.IncludeSeconds.ShouldBeTrue();
	}

	[Fact]
	public void MissedExecutionBehavior_CanBeSet()
	{
		// Arrange
		var options = new CronScheduleOptions();

		// Act
		options.MissedExecutionBehavior = MissedExecutionBehavior.ExecuteAllMissed;

		// Assert
		options.MissedExecutionBehavior.ShouldBe(MissedExecutionBehavior.ExecuteAllMissed);
	}

	[Fact]
	public void MaxMissedExecutions_CanBeSet()
	{
		// Arrange
		var options = new CronScheduleOptions();

		// Act
		options.MaxMissedExecutions = 5;

		// Assert
		options.MaxMissedExecutions.ShouldBe(5);
	}

	[Fact]
	public void AutoAdjustForDaylightSaving_CanBeSet()
	{
		// Arrange
		var options = new CronScheduleOptions();

		// Act
		options.AutoAdjustForDaylightSaving = false;

		// Assert
		options.AutoAdjustForDaylightSaving.ShouldBeFalse();
	}

	[Fact]
	public void ExecutionToleranceWindow_CanBeSet()
	{
		// Arrange
		var options = new CronScheduleOptions();

		// Act
		options.ExecutionToleranceWindow = TimeSpan.FromSeconds(5);

		// Assert
		options.ExecutionToleranceWindow.ShouldBe(TimeSpan.FromSeconds(5));
	}

	[Fact]
	public void EnableExtendedSyntax_CanBeSet()
	{
		// Arrange
		var options = new CronScheduleOptions();

		// Act
		options.EnableExtendedSyntax = false;

		// Assert
		options.EnableExtendedSyntax.ShouldBeFalse();
	}

	[Fact]
	public void SupportedTimeZoneIds_CanAddItems()
	{
		// Arrange
		var options = new CronScheduleOptions();

		// Act
		_ = options.SupportedTimeZoneIds.Add("UTC");
		_ = options.SupportedTimeZoneIds.Add("Eastern Standard Time");

		// Assert
		options.SupportedTimeZoneIds.Count.ShouldBe(2);
		options.SupportedTimeZoneIds.ShouldContain("UTC");
	}

	[Fact]
	public void EnableDetailedLogging_CanBeSet()
	{
		// Arrange
		var options = new CronScheduleOptions();

		// Act
		options.EnableDetailedLogging = true;

		// Assert
		options.EnableDetailedLogging.ShouldBeTrue();
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void ObjectInitializer_SetsScalarProperties()
	{
		// Act
		var options = new CronScheduleOptions
		{
			DefaultTimeZone = TimeZoneInfo.Utc,
			IncludeSeconds = true,
			MissedExecutionBehavior = MissedExecutionBehavior.ExecuteAllMissed,
			MaxMissedExecutions = 5,
			AutoAdjustForDaylightSaving = false,
			ExecutionToleranceWindow = TimeSpan.FromSeconds(2),
			EnableExtendedSyntax = false,
			EnableDetailedLogging = true,
		};

		// Assert
		options.DefaultTimeZone.ShouldBe(TimeZoneInfo.Utc);
		options.IncludeSeconds.ShouldBeTrue();
		options.MissedExecutionBehavior.ShouldBe(MissedExecutionBehavior.ExecuteAllMissed);
		options.MaxMissedExecutions.ShouldBe(5);
		options.AutoAdjustForDaylightSaving.ShouldBeFalse();
		options.ExecutionToleranceWindow.ShouldBe(TimeSpan.FromSeconds(2));
		options.EnableExtendedSyntax.ShouldBeFalse();
		options.EnableDetailedLogging.ShouldBeTrue();
	}

	#endregion

	#region Real-World Scenario Tests

	[Fact]
	public void Options_ForPreciseScheduling_IncludesSeconds()
	{
		// Act
		var options = new CronScheduleOptions
		{
			IncludeSeconds = true,
			ExecutionToleranceWindow = TimeSpan.FromMilliseconds(100),
		};

		// Assert
		options.IncludeSeconds.ShouldBeTrue();
		options.ExecutionToleranceWindow.ShouldBeLessThan(TimeSpan.FromSeconds(1));
	}

	[Fact]
	public void Options_ForCatchUpScheduling_ExecutesMissed()
	{
		// Act
		var options = new CronScheduleOptions
		{
			MissedExecutionBehavior = MissedExecutionBehavior.ExecuteAllMissed,
			MaxMissedExecutions = 20,
		};

		// Assert
		options.MissedExecutionBehavior.ShouldBe(MissedExecutionBehavior.ExecuteAllMissed);
		options.MaxMissedExecutions.ShouldBeGreaterThan(10);
	}

	[Fact]
	public void Options_ForSimpleCron_DisablesExtendedSyntax()
	{
		// Act
		var options = new CronScheduleOptions
		{
			EnableExtendedSyntax = false,
			IncludeSeconds = false,
		};

		// Assert
		options.EnableExtendedSyntax.ShouldBeFalse();
		options.IncludeSeconds.ShouldBeFalse();
	}

	#endregion
}
