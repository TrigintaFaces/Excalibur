using Excalibur.Jobs.Workflows;

namespace Excalibur.Jobs.Tests;

/// <summary>
/// Unit tests for WorkflowStatus enum.
/// </summary>
[Trait("Category", "Unit")]
public sealed class WorkflowStatusShould : UnitTestBase
{
	[Fact]
	public void HaveExpectedValues()
	{
		// Assert
		((int)WorkflowStatus.Running).ShouldBe(0);
		((int)WorkflowStatus.Completed).ShouldBe(1);
		((int)WorkflowStatus.Failed).ShouldBe(2);
		((int)WorkflowStatus.Suspended).ShouldBe(3);
		((int)WorkflowStatus.Cancelled).ShouldBe(4);
	}

	[Theory]
	[InlineData(WorkflowStatus.Running)]
	[InlineData(WorkflowStatus.Completed)]
	[InlineData(WorkflowStatus.Failed)]
	[InlineData(WorkflowStatus.Suspended)]
	[InlineData(WorkflowStatus.Cancelled)]
	public void BeDefinedForAllValues(WorkflowStatus status)
	{
		// Act & Assert
		Enum.IsDefined(status).ShouldBeTrue();
	}

	[Fact]
	public void Running_BeDefaultValue()
	{
		// Arrange & Act
		var defaultStatus = default(WorkflowStatus);

		// Assert
		defaultStatus.ShouldBe(WorkflowStatus.Running);
	}

	[Theory]
	[InlineData(0, WorkflowStatus.Running)]
	[InlineData(1, WorkflowStatus.Completed)]
	[InlineData(2, WorkflowStatus.Failed)]
	[InlineData(3, WorkflowStatus.Suspended)]
	[InlineData(4, WorkflowStatus.Cancelled)]
	public void CastFromInt_ReturnsCorrectValue(int value, WorkflowStatus expected)
	{
		// Act
		var status = (WorkflowStatus)value;

		// Assert
		status.ShouldBe(expected);
	}

	[Fact]
	public void HaveFiveDistinctValues()
	{
		// Arrange
		var values = Enum.GetValues<WorkflowStatus>();

		// Assert
		values.Length.ShouldBe(5);
		values.Distinct().Count().ShouldBe(5);
	}
}
