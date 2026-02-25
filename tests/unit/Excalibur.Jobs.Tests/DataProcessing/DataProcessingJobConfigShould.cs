// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Jobs.DataProcessing;

namespace Excalibur.Jobs.Tests.DataProcessing;

/// <summary>
/// Unit tests for <see cref="DataProcessingJobConfig"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Jobs")]
public sealed class DataProcessingJobConfigShould
{
	[Fact]
	public void HaveDefaultJobName_Empty()
	{
		var config = new DataProcessingJobConfig();

		config.JobName.ShouldBe(string.Empty);
	}

	[Fact]
	public void HaveDefaultJobGroup()
	{
		var config = new DataProcessingJobConfig();

		config.JobGroup.ShouldBe("Default");
	}

	[Fact]
	public void HaveDefaultCronSchedule_Empty()
	{
		var config = new DataProcessingJobConfig();

		config.CronSchedule.ShouldBe(string.Empty);
	}

	[Fact]
	public void AllowCustomJobName()
	{
		var config = new DataProcessingJobConfig { JobName = "CustomDataJob" };

		config.JobName.ShouldBe("CustomDataJob");
	}

	[Fact]
	public void NotBeDisabledByDefault()
	{
		var config = new DataProcessingJobConfig();

		config.Disabled.ShouldBeFalse();
	}

	[Fact]
	public void AllowDisabling()
	{
		var config = new DataProcessingJobConfig { Disabled = true };

		config.Disabled.ShouldBeTrue();
	}

	[Fact]
	public void HaveDefaultDegradedThreshold()
	{
		var config = new DataProcessingJobConfig();

		config.DegradedThreshold.ShouldBe(TimeSpan.FromMinutes(5));
	}

	[Fact]
	public void HaveDefaultUnhealthyThreshold()
	{
		var config = new DataProcessingJobConfig();

		config.UnhealthyThreshold.ShouldBe(TimeSpan.FromMinutes(10));
	}
}
