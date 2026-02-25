using Excalibur.Application.Requests.Jobs;

namespace Excalibur.Tests.Application;

/// <summary>
/// Unit tests for JobResult struct.
/// </summary>
[Trait("Category", "Unit")]
public sealed class JobResultShould : UnitTestBase
{
	[Fact]
	public void NoWorkPerformed_IsDistinctFromOperationSucceeded()
	{
		// Arrange & Act & Assert
		JobResult.NoWorkPerformed.ShouldNotBe(JobResult.OperationSucceeded);
	}

	[Fact]
	public void Equals_SameJobResult_ReturnsTrue()
	{
		// Arrange
		var result1 = JobResult.NoWorkPerformed;
		var result2 = JobResult.NoWorkPerformed;

		// Act & Assert
		result1.Equals(result2).ShouldBeTrue();
	}

	[Fact]
	public void Equals_DifferentJobResult_ReturnsFalse()
	{
		// Arrange
		var result1 = JobResult.NoWorkPerformed;
		var result2 = JobResult.OperationSucceeded;

		// Act & Assert
		result1.Equals(result2).ShouldBeFalse();
	}

	[Fact]
	public void EqualityOperator_SameJobResult_ReturnsTrue()
	{
		// Arrange
		var result1 = JobResult.NoWorkPerformed;
		var result2 = JobResult.NoWorkPerformed;

		// Act & Assert
		(result1 == result2).ShouldBeTrue();
	}

	[Fact]
	public void InequalityOperator_DifferentJobResult_ReturnsTrue()
	{
		// Arrange
		var result1 = JobResult.NoWorkPerformed;
		var result2 = JobResult.OperationSucceeded;

		// Act & Assert
		(result1 != result2).ShouldBeTrue();
	}

	[Fact]
	public void GetHashCode_SameJobResult_ReturnsSameHash()
	{
		// Arrange
		var result1 = JobResult.NoWorkPerformed;
		var result2 = JobResult.NoWorkPerformed;

		// Act & Assert
		result1.GetHashCode().ShouldBe(result2.GetHashCode());
	}

	[Fact]
	public void Equals_WithObject_ReturnsCorrectResult()
	{
		// Arrange
		var result = JobResult.NoWorkPerformed;
		object boxedResult = JobResult.NoWorkPerformed;

		// Act & Assert
		result.Equals(boxedResult).ShouldBeTrue();
	}

	[Fact]
	public void Equals_WithNull_ReturnsFalse()
	{
		// Arrange
		var result = JobResult.NoWorkPerformed;

		// Act & Assert
		result.Equals(null).ShouldBeFalse();
	}
}
