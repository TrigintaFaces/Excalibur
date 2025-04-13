using Excalibur.Jobs;

using Shouldly;

namespace Excalibur.Tests.Unit.Jobs;

public class JobConfigShould
{
	[Fact]
	public void InitializeWithDefaultValues()
	{
		// Act
		var jobConfig = new JobConfig();

		// Assert
		jobConfig.ShouldSatisfyAllConditions(
			() => jobConfig.Disabled.ShouldBeFalse(),
			() => jobConfig.JobName.ShouldBeNull(),
			() => jobConfig.JobGroup.ShouldBeNull(),
			() => jobConfig.CronSchedule.ShouldBeNull(),
			() => jobConfig.DegradedThreshold.ShouldBe(default),
			() => jobConfig.UnhealthyThreshold.ShouldBe(default));
	}

	[Fact]
	public void SetAndGetPropertiesCorrectly()
	{
		// Arrange
		var jobConfig = new JobConfig
		{
			Disabled = true,
			JobName = "TestJob",
			JobGroup = "TestGroup",
			CronSchedule = "0 0/15 * * * ?",
			DegradedThreshold = TimeSpan.FromMinutes(3),
			UnhealthyThreshold = TimeSpan.FromMinutes(10)
		};

		// Assert
		jobConfig.ShouldSatisfyAllConditions(
			() => jobConfig.Disabled.ShouldBeTrue(),
			() => jobConfig.JobName.ShouldBe("TestJob"),
			() => jobConfig.JobGroup.ShouldBe("TestGroup"),
			() => jobConfig.CronSchedule.ShouldBe("0 0/15 * * * ?"),
			() => jobConfig.DegradedThreshold.ShouldBe(TimeSpan.FromMinutes(3)),
			() => jobConfig.UnhealthyThreshold.ShouldBe(TimeSpan.FromMinutes(10)));
	}

	[Fact]
	public void AllowSettingCustomHealthThresholds()
	{
		// Arrange
		var degradedThreshold = TimeSpan.FromMinutes(8);
		var unhealthyThreshold = TimeSpan.FromMinutes(15);

		// Act
		var jobConfig = new JobConfig { DegradedThreshold = degradedThreshold, UnhealthyThreshold = unhealthyThreshold };

		// Assert
		jobConfig.DegradedThreshold.ShouldBe(degradedThreshold);
		jobConfig.UnhealthyThreshold.ShouldBe(unhealthyThreshold);
	}

	[Fact]
	public void ImplementIJobConfigInterface()
	{
		// Arrange & Act
		var jobConfig = new JobConfig();

		// Assert
		_ = jobConfig.ShouldBeAssignableTo<IJobConfig>();
	}
}
