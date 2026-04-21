// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Jobs.DataProcessing;

namespace Excalibur.Jobs.Tests.DataProcessing;

/// <summary>
/// Unit tests for <see cref="DataProcessingJobOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Jobs")]
public sealed class DataProcessingJobConfigShould
{
	[Fact]
	public void HaveDefaultJobName_Empty()
	{
		var config = new DataProcessingJobOptions();

		config.JobName.ShouldBe(string.Empty);
	}

	[Fact]
	public void HaveDefaultJobGroup()
	{
		var config = new DataProcessingJobOptions();

		config.JobGroup.ShouldBe("Default");
	}

	[Fact]
	public void HaveDefaultCronSchedule_Empty()
	{
		var config = new DataProcessingJobOptions();

		config.CronSchedule.ShouldBe(string.Empty);
	}

	[Fact]
	public void AllowCustomJobName()
	{
		var config = new DataProcessingJobOptions { JobName = "CustomDataJob" };

		config.JobName.ShouldBe("CustomDataJob");
	}

	[Fact]
	public void NotBeDisabledByDefault()
	{
		var config = new DataProcessingJobOptions();

		config.Disabled.ShouldBeFalse();
	}

	[Fact]
	public void AllowDisabling()
	{
		var config = new DataProcessingJobOptions { Disabled = true };

		config.Disabled.ShouldBeTrue();
	}

	[Fact]
	public void HaveDefaultDegradedThreshold()
	{
		var config = new DataProcessingJobOptions();

		config.DegradedThreshold.ShouldBe(TimeSpan.FromMinutes(5));
	}

	[Fact]
	public void HaveDefaultUnhealthyThreshold()
	{
		var config = new DataProcessingJobOptions();

		config.UnhealthyThreshold.ShouldBe(TimeSpan.FromMinutes(10));
	}
}
