// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Jobs.Core;
using Excalibur.Jobs.DataProcessing;

namespace Excalibur.Jobs.Tests.Core;

/// <summary>
/// Unit tests for <see cref="DataProcessingJobConfig"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Jobs")]
[Trait("Feature", "DataProcessing")]
public sealed class DataProcessingJobConfigShould
{
	[Fact]
	public void InheritFromJobConfig()
	{
		// Assert
		typeof(DataProcessingJobConfig).BaseType.ShouldBe(typeof(JobConfig));
	}

	[Fact]
	public void CreateSuccessfully()
	{
		// Act
		var config = new DataProcessingJobConfig();

		// Assert
		config.ShouldNotBeNull();
	}

	[Fact]
	public void InheritCronScheduleProperty()
	{
		// Act
		var config = new DataProcessingJobConfig
		{
			CronSchedule = "0 0 * * * ?"
		};

		// Assert
		config.CronSchedule.ShouldBe("0 0 * * * ?");
	}

	[Fact]
	public void InheritJobNameProperty()
	{
		// Act
		var config = new DataProcessingJobConfig
		{
			JobName = "DataProcessor"
		};

		// Assert
		config.JobName.ShouldBe("DataProcessor");
	}

	[Fact]
	public void InheritJobGroupProperty()
	{
		// Act
		var config = new DataProcessingJobConfig
		{
			JobGroup = "Processing"
		};

		// Assert
		config.JobGroup.ShouldBe("Processing");
	}

	[Fact]
	public void BeAssignableToJobConfig()
	{
		// Arrange
		var config = new DataProcessingJobConfig();

		// Act & Assert
		config.ShouldBeAssignableTo<JobConfig>();
	}
}
