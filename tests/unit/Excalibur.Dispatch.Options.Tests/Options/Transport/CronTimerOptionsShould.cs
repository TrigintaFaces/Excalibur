// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.Transport;

namespace Excalibur.Dispatch.Tests.Options.Transport;

/// <summary>
/// Unit tests for <see cref="CronTimerOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Options")]
[Trait("Priority", "0")]
public sealed class CronTimerOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void Default_TimeZone_IsUtc()
	{
		// Arrange & Act
		var options = new CronTimerOptions();

		// Assert
		options.TimeZone.ShouldBe(TimeZoneInfo.Utc);
	}

	[Fact]
	public void Default_RunOnStartup_IsFalse()
	{
		// Arrange & Act
		var options = new CronTimerOptions();

		// Assert
		options.RunOnStartup.ShouldBeFalse();
	}

	[Fact]
	public void Default_PreventOverlap_IsTrue()
	{
		// Arrange & Act
		var options = new CronTimerOptions();

		// Assert
		options.PreventOverlap.ShouldBeTrue();
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void TimeZone_CanBeSet()
	{
		// Arrange
		var options = new CronTimerOptions();
		var pacificTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");

		// Act
		options.TimeZone = pacificTimeZone;

		// Assert
		options.TimeZone.ShouldBe(pacificTimeZone);
	}

	[Fact]
	public void RunOnStartup_CanBeSet()
	{
		// Arrange
		var options = new CronTimerOptions();

		// Act
		options.RunOnStartup = true;

		// Assert
		options.RunOnStartup.ShouldBeTrue();
	}

	[Fact]
	public void PreventOverlap_CanBeSet()
	{
		// Arrange
		var options = new CronTimerOptions();

		// Act
		options.PreventOverlap = false;

		// Assert
		options.PreventOverlap.ShouldBeFalse();
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void ObjectInitializer_SetsAllProperties()
	{
		// Act
		var options = new CronTimerOptions
		{
			TimeZone = TimeZoneInfo.Local,
			RunOnStartup = true,
			PreventOverlap = false,
		};

		// Assert
		options.TimeZone.ShouldBe(TimeZoneInfo.Local);
		options.RunOnStartup.ShouldBeTrue();
		options.PreventOverlap.ShouldBeFalse();
	}

	#endregion

	#region Real-World Scenario Tests

	[Fact]
	public void Options_ForImmediateStart_EnablesRunOnStartup()
	{
		// Act
		var options = new CronTimerOptions
		{
			RunOnStartup = true,
		};

		// Assert
		options.RunOnStartup.ShouldBeTrue();
	}

	[Fact]
	public void Options_ForConcurrentExecution_DisablesOverlapPrevention()
	{
		// Act
		var options = new CronTimerOptions
		{
			PreventOverlap = false,
		};

		// Assert
		options.PreventOverlap.ShouldBeFalse();
	}

	[Fact]
	public void Options_ForLocalTimeScheduling_UsesLocalTimeZone()
	{
		// Act
		var options = new CronTimerOptions
		{
			TimeZone = TimeZoneInfo.Local,
		};

		// Assert
		options.TimeZone.ShouldBe(TimeZoneInfo.Local);
	}

	#endregion
}
