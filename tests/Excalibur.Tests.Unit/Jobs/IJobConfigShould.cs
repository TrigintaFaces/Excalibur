using Excalibur.Jobs;

using FakeItEasy;

using Shouldly;

namespace Excalibur.Tests.Unit.Jobs;

public class IJobConfigShould
{
	[Fact]
	public void ProvideJobNameProperty()
	{
		// Arrange
		var config = A.Fake<IJobConfig>();
		var expectedName = "TestJob";
		_ = A.CallTo(() => config.JobName).Returns(expectedName);

		// Act
		var jobName = config.JobName;

		// Assert
		jobName.ShouldBe(expectedName);
	}

	[Fact]
	public void ProvideJobGroupProperty()
	{
		// Arrange
		var config = A.Fake<IJobConfig>();
		var expectedGroup = "TestGroup";
		_ = A.CallTo(() => config.JobGroup).Returns(expectedGroup);

		// Act
		var jobGroup = config.JobGroup;

		// Assert
		jobGroup.ShouldBe(expectedGroup);
	}

	[Fact]
	public void ProvideCronScheduleProperty()
	{
		// Arrange
		var config = A.Fake<IJobConfig>();
		var expectedCron = "0 */5 * * *";
		_ = A.CallTo(() => config.CronSchedule).Returns(expectedCron);

		// Act
		var cronSchedule = config.CronSchedule;

		// Assert
		cronSchedule.ShouldBe(expectedCron);
	}

	[Fact]
	public void ProvideDegradedThresholdProperty()
	{
		// Arrange
		var config = A.Fake<IJobConfig>();
		var expectedThreshold = TimeSpan.FromMinutes(5);
		_ = A.CallTo(() => config.DegradedThreshold).Returns(expectedThreshold);

		// Act
		var degradedThreshold = config.DegradedThreshold;

		// Assert
		degradedThreshold.ShouldBe(expectedThreshold);
	}

	[Fact]
	public void ProvideDisabledProperty()
	{
		// Arrange
		var config = A.Fake<IJobConfig>();
		_ = A.CallTo(() => config.Disabled).Returns(true);

		// Act
		var disabled = config.Disabled;

		// Assert
		disabled.ShouldBeTrue();
	}

	[Fact]
	public void ProvideUnhealthyThresholdProperty()
	{
		// Arrange
		var config = A.Fake<IJobConfig>();
		var expectedThreshold = TimeSpan.FromMinutes(15);
		_ = A.CallTo(() => config.UnhealthyThreshold).Returns(expectedThreshold);

		// Act
		var unhealthyThreshold = config.UnhealthyThreshold;

		// Assert
		unhealthyThreshold.ShouldBe(expectedThreshold);
	}

	[Fact]
	public void BeImplementableByConcreteClass()
	{
		// Arrange
		var concreteConfig = new TestJobConfig
		{
			JobName = "TestJob",
			JobGroup = "TestGroup",
			CronSchedule = "0 */5 * * *",
			DegradedThreshold = TimeSpan.FromMinutes(5),
			Disabled = false,
			UnhealthyThreshold = TimeSpan.FromMinutes(15)
		};

		// Act & Assert
		_ = concreteConfig.ShouldBeAssignableTo<IJobConfig>();
		concreteConfig.JobName.ShouldBe("TestJob");
		concreteConfig.JobGroup.ShouldBe("TestGroup");
		concreteConfig.CronSchedule.ShouldBe("0 */5 * * *");
		concreteConfig.DegradedThreshold.ShouldBe(TimeSpan.FromMinutes(5));
		concreteConfig.Disabled.ShouldBeFalse();
		concreteConfig.UnhealthyThreshold.ShouldBe(TimeSpan.FromMinutes(15));
	}

	private sealed class TestJobConfig : IJobConfig
	{
		public string JobName { get; init; } = string.Empty;
		public string JobGroup { get; init; } = string.Empty;
		public string CronSchedule { get; init; } = string.Empty;
		public TimeSpan DegradedThreshold { get; init; }
		public bool Disabled { get; init; }
		public TimeSpan UnhealthyThreshold { get; init; }
	}
}
