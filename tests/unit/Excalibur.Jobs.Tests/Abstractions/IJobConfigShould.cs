using Excalibur.Jobs.Abstractions;

namespace Excalibur.Jobs.Tests.Abstractions;

/// <summary>
/// Unit tests for IJobConfig interface contract.
/// </summary>
[Trait("Category", "Unit")]
public sealed class IJobConfigShould : UnitTestBase
{
	[Fact]
	public void BeInterface()
	{
		// Assert
		typeof(IJobConfig).IsInterface.ShouldBeTrue();
	}

	[Fact]
	public void HaveJobNameProperty()
	{
		// Arrange
		var property = typeof(IJobConfig).GetProperty(nameof(IJobConfig.JobName));

		// Assert
		_ = property.ShouldNotBeNull();
		property.PropertyType.ShouldBe(typeof(string));
	}

	[Fact]
	public void HaveJobGroupProperty()
	{
		// Arrange
		var property = typeof(IJobConfig).GetProperty(nameof(IJobConfig.JobGroup));

		// Assert
		_ = property.ShouldNotBeNull();
		property.PropertyType.ShouldBe(typeof(string));
	}

	[Fact]
	public void HaveCronScheduleProperty()
	{
		// Arrange
		var property = typeof(IJobConfig).GetProperty(nameof(IJobConfig.CronSchedule));

		// Assert
		_ = property.ShouldNotBeNull();
		property.PropertyType.ShouldBe(typeof(string));
	}

	[Fact]
	public void HaveDegradedThresholdProperty()
	{
		// Arrange
		var property = typeof(IJobConfig).GetProperty(nameof(IJobConfig.DegradedThreshold));

		// Assert
		_ = property.ShouldNotBeNull();
		property.PropertyType.ShouldBe(typeof(TimeSpan));
	}

	[Fact]
	public void HaveUnhealthyThresholdProperty()
	{
		// Arrange
		var property = typeof(IJobConfig).GetProperty(nameof(IJobConfig.UnhealthyThreshold));

		// Assert
		_ = property.ShouldNotBeNull();
		property.PropertyType.ShouldBe(typeof(TimeSpan));
	}

	[Fact]
	public void HaveDisabledProperty()
	{
		// Arrange
		var property = typeof(IJobConfig).GetProperty(nameof(IJobConfig.Disabled));

		// Assert
		_ = property.ShouldNotBeNull();
		property.PropertyType.ShouldBe(typeof(bool));
	}
}
