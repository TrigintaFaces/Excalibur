using Excalibur.Jobs.Coordination;

namespace Excalibur.Jobs.Tests;

/// <summary>
/// Unit tests for JobInstanceStatus enum.
/// </summary>
[Trait("Category", "Unit")]
public sealed class JobInstanceStatusShould : UnitTestBase
{
	[Fact]
	public void HaveExpectedValues()
	{
		// Assert
		((int)JobInstanceStatus.Active).ShouldBe(0);
		((int)JobInstanceStatus.Draining).ShouldBe(1);
		((int)JobInstanceStatus.Inactive).ShouldBe(2);
		((int)JobInstanceStatus.Failed).ShouldBe(3);
	}

	[Theory]
	[InlineData(JobInstanceStatus.Active)]
	[InlineData(JobInstanceStatus.Draining)]
	[InlineData(JobInstanceStatus.Inactive)]
	[InlineData(JobInstanceStatus.Failed)]
	public void BeDefinedForAllValues(JobInstanceStatus status)
	{
		// Act & Assert
		Enum.IsDefined(status).ShouldBeTrue();
	}

	[Fact]
	public void Active_BeDefaultValue()
	{
		// Arrange & Act
		var defaultStatus = default(JobInstanceStatus);

		// Assert
		defaultStatus.ShouldBe(JobInstanceStatus.Active);
	}

	[Theory]
	[InlineData(0, JobInstanceStatus.Active)]
	[InlineData(1, JobInstanceStatus.Draining)]
	[InlineData(2, JobInstanceStatus.Inactive)]
	[InlineData(3, JobInstanceStatus.Failed)]
	public void CastFromInt_ReturnsCorrectValue(int value, JobInstanceStatus expected)
	{
		// Act
		var status = (JobInstanceStatus)value;

		// Assert
		status.ShouldBe(expected);
	}

	[Theory]
	[InlineData(JobInstanceStatus.Active, "Active")]
	[InlineData(JobInstanceStatus.Draining, "Draining")]
	[InlineData(JobInstanceStatus.Inactive, "Inactive")]
	[InlineData(JobInstanceStatus.Failed, "Failed")]
	public void ToString_ReturnsExpectedName(JobInstanceStatus status, string expected)
	{
		// Act & Assert
		status.ToString().ShouldBe(expected);
	}
}
