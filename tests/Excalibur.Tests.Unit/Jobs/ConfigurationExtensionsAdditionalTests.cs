using Excalibur.Jobs;

using Microsoft.Extensions.Configuration;

using Shouldly;

namespace Excalibur.Tests.Unit.Jobs;

public class ConfigurationExtensionsAdditionalTests
{
	[Fact]
	public void GetJobConfigurationHandlesNullableTimeSpanProperties()
	{
		// Arrange
		var configBuilder = new ConfigurationBuilder();
		_ = configBuilder.AddInMemoryCollection(new Dictionary<string, string>
		{
			{ "NullableTimeSpanConfig:JobName", "TimeSpanTestJob" },
			{ "NullableTimeSpanConfig:JobGroup", "TestGroup" },
			{ "NullableTimeSpanConfig:CronSchedule", "0 0 * * *" },
			{ "NullableTimeSpanConfig:Disabled", "false" }
            // Deliberately omit DegradedThreshold and UnhealthyThreshold
        }!);

		var config = configBuilder.Build();

		// Act
		var result = config.GetJobConfiguration<JobConfig>("NullableTimeSpanConfig");

		// Assert
		result.ShouldSatisfyAllConditions(
			() => result.JobName.ShouldBe("TimeSpanTestJob"),
			() => result.JobGroup.ShouldBe("TestGroup"),
			() => result.CronSchedule.ShouldBe("0 0 * * *"),
			() => result.Disabled.ShouldBeFalse(),
			() => result.DegradedThreshold.ShouldBe(default),
			() => result.UnhealthyThreshold.ShouldBe(default));
	}

	[Fact]
	public void GetJobConfigurationHandlesEmptySections()
	{
		// Arrange
		var configBuilder = new ConfigurationBuilder();
		// Create an empty section with no values
		var config = configBuilder.Build();

		// Act & Assert
		_ = Should.Throw<InvalidOperationException>(() =>
			config.GetJobConfiguration<JobConfig>("EmptySection"));
	}

	[Fact]
	public void GetJobConfigurationHandlesMixedCaseSectionNames()
	{
		// Arrange - Configuration is case-insensitive by default
		var configBuilder = new ConfigurationBuilder();
		_ = configBuilder.AddInMemoryCollection(new Dictionary<string, string>
		{
			{ "MixedCaseSection:JobName", "CaseTestJob" },
			{ "MixedCaseSection:JobGroup", "TestGroup" },
			{ "MixedCaseSection:CronSchedule", "0 0 * * *" },
			{ "MixedCaseSection:Disabled", "false" },
			{ "MixedCaseSection:DegradedThreshold", "00:03:00" },
			{ "MixedCaseSection:UnhealthyThreshold", "00:05:00" }
		}!);

		var config = configBuilder.Build();

		// Act - Use different casing for section name
		var result = config.GetJobConfiguration<JobConfig>("mixedcasesection");

		// Assert
		result.ShouldSatisfyAllConditions(
			() => result.JobName.ShouldBe("CaseTestJob"),
			() => result.JobGroup.ShouldBe("TestGroup"),
			() => result.CronSchedule.ShouldBe("0 0 * * *"),
			() => result.Disabled.ShouldBeFalse(),
			() => result.DegradedThreshold.ShouldBe(TimeSpan.FromMinutes(3)),
			() => result.UnhealthyThreshold.ShouldBe(TimeSpan.FromMinutes(5)));
	}
}