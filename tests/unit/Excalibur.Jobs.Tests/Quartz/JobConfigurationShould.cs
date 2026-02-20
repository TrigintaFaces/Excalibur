// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Jobs.Quartz;

namespace Excalibur.Jobs.Tests.Quartz;

/// <summary>
/// Unit tests for <see cref="JobConfiguration"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Jobs")]
[Trait("Feature", "Quartz")]
public sealed class JobConfigurationShould
{
	[Fact]
	public void CreateWithRequiredParameters()
	{
		// Arrange & Act
		var config = new JobConfiguration("my-job", "0 * * * * ?");

		// Assert
		config.JobKey.ShouldBe("my-job");
		config.CronExpression.ShouldBe("0 * * * * ?");
	}

	[Fact]
	public void HaveDefaultEnabledTrue()
	{
		// Act
		var config = new JobConfiguration("job", "0 0 * * * ?");

		// Assert
		config.Enabled.ShouldBeTrue();
	}

	[Fact]
	public void HaveNullJobDataByDefault()
	{
		// Act
		var config = new JobConfiguration("job", "0 0 * * * ?");

		// Assert
		config.JobData.ShouldBeNull();
	}

	[Fact]
	public void HaveNullDescriptionByDefault()
	{
		// Act
		var config = new JobConfiguration("job", "0 0 * * * ?");

		// Assert
		config.Description.ShouldBeNull();
	}

	[Fact]
	public void AllowSettingJobData()
	{
		// Arrange
		var jobData = new { Key = "Value", Count = 42 };

		// Act
		var config = new JobConfiguration("job", "0 0 * * * ?")
		{
			JobData = jobData
		};

		// Assert
		config.JobData.ShouldBe(jobData);
	}

	[Fact]
	public void AllowSettingDescription()
	{
		// Act
		var config = new JobConfiguration("job", "0 0 * * * ?")
		{
			Description = "Process daily reports"
		};

		// Assert
		config.Description.ShouldBe("Process daily reports");
	}

	[Fact]
	public void AllowDisablingJob()
	{
		// Act
		var config = new JobConfiguration("job", "0 0 * * * ?")
		{
			Enabled = false
		};

		// Assert
		config.Enabled.ShouldBeFalse();
	}

	[Fact]
	public void ThrowOnNullJobKey()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => new JobConfiguration(null!, "0 0 * * * ?"));
	}

	[Fact]
	public void ThrowOnNullCronExpression()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => new JobConfiguration("job", null!));
	}

	[Fact]
	public void SupportComplexJobData()
	{
		// Arrange
		var complexData = new Dictionary<string, object>
		{
			["param1"] = "value1",
			["param2"] = 123,
			["param3"] = new List<string> { "a", "b", "c" }
		};

		// Act
		var config = new JobConfiguration("job", "0 0 * * * ?")
		{
			JobData = complexData
		};

		// Assert
		config.JobData.ShouldBe(complexData);
	}

	[Fact]
	public void SetAllPropertiesAtOnce()
	{
		// Arrange
		var data = new { Id = 1 };

		// Act
		var config = new JobConfiguration("my-job-key", "0 30 8 * * ?")
		{
			JobData = data,
			Description = "Daily morning job",
			Enabled = true
		};

		// Assert
		config.JobKey.ShouldBe("my-job-key");
		config.CronExpression.ShouldBe("0 30 8 * * ?");
		config.JobData.ShouldBe(data);
		config.Description.ShouldBe("Daily morning job");
		config.Enabled.ShouldBeTrue();
	}
}
