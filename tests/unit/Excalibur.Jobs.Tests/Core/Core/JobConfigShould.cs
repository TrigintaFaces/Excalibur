// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Jobs.Core;

namespace Excalibur.Jobs.Tests.Core;

/// <summary>
/// Unit tests for <see cref="JobConfig"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Jobs")]
[Trait("Feature", "Core")]
public sealed class JobConfigShould : UnitTestBase
{
	// Test class that inherits from JobConfig since it's abstract
	private sealed class TestJobConfig : JobConfig;

	[Fact]
	public void HaveEmptyJobNameByDefault()
	{
		// Act
		var config = new TestJobConfig();

		// Assert
		config.JobName.ShouldBe(string.Empty);
	}

	[Fact]
	public void HaveDefaultJobGroupByDefault()
	{
		// Act
		var config = new TestJobConfig();

		// Assert
		config.JobGroup.ShouldBe("Default");
	}

	[Fact]
	public void HaveEmptyCronScheduleByDefault()
	{
		// Act
		var config = new TestJobConfig();

		// Assert
		config.CronSchedule.ShouldBe(string.Empty);
	}

	[Fact]
	public void HaveFiveMinuteDegradedThresholdByDefault()
	{
		// Act
		var config = new TestJobConfig();

		// Assert
		config.DegradedThreshold.ShouldBe(TimeSpan.FromMinutes(5));
	}

	[Fact]
	public void HaveTenMinuteUnhealthyThresholdByDefault()
	{
		// Act
		var config = new TestJobConfig();

		// Assert
		config.UnhealthyThreshold.ShouldBe(TimeSpan.FromMinutes(10));
	}

	[Fact]
	public void NotBeDisabledByDefault()
	{
		// Act
		var config = new TestJobConfig();

		// Assert
		config.Disabled.ShouldBeFalse();
	}

	[Fact]
	public void AllowSettingJobName()
	{
		// Act
		var config = new TestJobConfig { JobName = "MyTestJob" };

		// Assert
		config.JobName.ShouldBe("MyTestJob");
	}

	[Fact]
	public void AllowSettingJobGroup()
	{
		// Act
		var config = new TestJobConfig { JobGroup = "DataProcessing" };

		// Assert
		config.JobGroup.ShouldBe("DataProcessing");
	}

	[Fact]
	public void AllowSettingCronSchedule()
	{
		// Act
		var config = new TestJobConfig { CronSchedule = "0 0 * * *" };

		// Assert
		config.CronSchedule.ShouldBe("0 0 * * *");
	}

	[Fact]
	public void AllowSettingDegradedThreshold()
	{
		// Act
		var config = new TestJobConfig { DegradedThreshold = TimeSpan.FromMinutes(2) };

		// Assert
		config.DegradedThreshold.ShouldBe(TimeSpan.FromMinutes(2));
	}

	[Fact]
	public void AllowSettingUnhealthyThreshold()
	{
		// Act
		var config = new TestJobConfig { UnhealthyThreshold = TimeSpan.FromMinutes(15) };

		// Assert
		config.UnhealthyThreshold.ShouldBe(TimeSpan.FromMinutes(15));
	}

	[Fact]
	public void AllowDisablingJob()
	{
		// Act
		var config = new TestJobConfig { Disabled = true };

		// Assert
		config.Disabled.ShouldBeTrue();
	}

	[Fact]
	public void ImplementIJobConfig()
	{
		// Act
		var config = new TestJobConfig();

		// Assert
		_ = config.ShouldBeAssignableTo<Excalibur.Jobs.Abstractions.IJobConfig>();
	}

	[Fact]
	public void CreateFullyConfiguredJob()
	{
		// Act
		var config = new TestJobConfig
		{
			JobName = "OrderProcessor",
			JobGroup = "Orders",
			CronSchedule = "*/5 * * * *",
			DegradedThreshold = TimeSpan.FromMinutes(3),
			UnhealthyThreshold = TimeSpan.FromMinutes(8),
			Disabled = false
		};

		// Assert
		config.JobName.ShouldBe("OrderProcessor");
		config.JobGroup.ShouldBe("Orders");
		config.CronSchedule.ShouldBe("*/5 * * * *");
		config.DegradedThreshold.ShouldBe(TimeSpan.FromMinutes(3));
		config.UnhealthyThreshold.ShouldBe(TimeSpan.FromMinutes(8));
		config.Disabled.ShouldBeFalse();
	}
}
