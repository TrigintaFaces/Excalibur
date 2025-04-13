using Excalibur.Jobs;

using Microsoft.Extensions.Configuration;

using Shouldly;

namespace Excalibur.Tests.Unit.Jobs;

public class ConfigurationExtensionsShould
{
	[Fact]
	public void GetJobConfigurationThrowsArgumentNullExceptionWhenConfigIsNull()
	{
		// Arrange
		IConfiguration config = null;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			config.GetJobConfiguration<JobConfig>("TestSection"));
	}

	[Fact]
	public void GetJobConfigurationThrowsArgumentNullExceptionWhenSectionKeyIsNull()
	{
		// Arrange
		var config = new ConfigurationBuilder().Build();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			config.GetJobConfiguration<JobConfig>(null));
	}

	[Fact]
	public void GetJobConfigurationThrowsInvalidOperationExceptionWhenSectionNotFound()
	{
		// Arrange
		var config = new ConfigurationBuilder().Build();

		// Act & Assert
		_ = Should.Throw<InvalidOperationException>(() =>
			config.GetJobConfiguration<JobConfig>("NonExistentSection"));
	}

	[Fact]
	public void GetJobConfigurationReturnsConfiguredValues()
	{
		// Arrange
		var configBuilder = new ConfigurationBuilder();
		_ = configBuilder.AddInMemoryCollection(new Dictionary<string, string>
		{
			{ "TestJobSection:JobName", "TestJob" },
			{ "TestJobSection:JobGroup", "TestGroup" },
			{ "TestJobSection:CronSchedule", "0 0/5 * * * ?" },
			{ "TestJobSection:Disabled", "true" },
			{ "TestJobSection:DegradedThreshold", "00:03:00" },
			{ "TestJobSection:UnhealthyThreshold", "00:05:00" }
		}!);

		var config = configBuilder.Build();

		// Act
		var result = config.GetJobConfiguration<JobConfig>("TestJobSection");

		// Assert
		result.ShouldSatisfyAllConditions(
			() => result.Disabled.ShouldBeTrue(),
			() => result.JobName.ShouldBe("TestJob"),
			() => result.JobGroup.ShouldBe("TestGroup"),
			() => result.CronSchedule.ShouldBe("0 0/5 * * * ?"),
			() => result.DegradedThreshold.ShouldBe(TimeSpan.FromMinutes(3)),
			() => result.UnhealthyThreshold.ShouldBe(TimeSpan.FromMinutes(5)));
	}
}
