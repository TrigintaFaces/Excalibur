using Excalibur.Jobs.Abstractions;

namespace Excalibur.Jobs.Tests.Abstractions;

/// <summary>
/// Unit tests for IJobOptions interface contract.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class IJobConfigShould : UnitTestBase
{
	[Fact]
	public void BeInterface()
	{
		// Assert
		typeof(IJobOptions).IsInterface.ShouldBeTrue();
	}

	[Fact]
	public void HaveJobNameProperty()
	{
		// Arrange
		var property = typeof(IJobOptions).GetProperty(nameof(IJobOptions.JobName));

		// Assert
		_ = property.ShouldNotBeNull();
		property.PropertyType.ShouldBe(typeof(string));
	}

	[Fact]
	public void HaveJobGroupProperty()
	{
		// Arrange
		var property = typeof(IJobOptions).GetProperty(nameof(IJobOptions.JobGroup));

		// Assert
		_ = property.ShouldNotBeNull();
		property.PropertyType.ShouldBe(typeof(string));
	}

	[Fact]
	public void HaveCronScheduleProperty()
	{
		// Arrange
		var property = typeof(IJobOptions).GetProperty(nameof(IJobOptions.CronSchedule));

		// Assert
		_ = property.ShouldNotBeNull();
		property.PropertyType.ShouldBe(typeof(string));
	}

	[Fact]
	public void HaveDegradedThresholdProperty()
	{
		// Arrange
		var property = typeof(IJobOptions).GetProperty(nameof(IJobOptions.DegradedThreshold));

		// Assert
		_ = property.ShouldNotBeNull();
		property.PropertyType.ShouldBe(typeof(TimeSpan));
	}

	[Fact]
	public void HaveUnhealthyThresholdProperty()
	{
		// Arrange
		var property = typeof(IJobOptions).GetProperty(nameof(IJobOptions.UnhealthyThreshold));

		// Assert
		_ = property.ShouldNotBeNull();
		property.PropertyType.ShouldBe(typeof(TimeSpan));
	}

	[Fact]
	public void HaveDisabledProperty()
	{
		// Arrange
		var property = typeof(IJobOptions).GetProperty(nameof(IJobOptions.Disabled));

		// Assert
		_ = property.ShouldNotBeNull();
		property.PropertyType.ShouldBe(typeof(bool));
	}
}
