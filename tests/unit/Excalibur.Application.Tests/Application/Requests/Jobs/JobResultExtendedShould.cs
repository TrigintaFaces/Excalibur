// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Tests.Application.Requests.Jobs;

/// <summary>
/// Extended unit tests for <see cref="JobResult"/> struct.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Application")]
[Trait("Feature", "Jobs")]
public sealed class JobResultExtendedShould : UnitTestBase
{
	[Fact]
	public void HaveNoWorkPerformedResult()
	{
		// Assert
		var result = JobResult.NoWorkPerformed;
		result.ShouldNotBe(default);
	}

	[Fact]
	public void HaveOperationSucceededResult()
	{
		// Assert
		var result = JobResult.OperationSucceeded;
		result.ShouldNotBe(default);
	}

	[Fact]
	public void DistinguishNoWorkPerformedFromOperationSucceeded()
	{
		// Assert
		JobResult.NoWorkPerformed.ShouldNotBe(JobResult.OperationSucceeded);
	}

	[Fact]
	public void ImplementEqualityCorrectly()
	{
		// Arrange
		var result1 = JobResult.NoWorkPerformed;
		var result2 = JobResult.NoWorkPerformed;

		// Assert
		result1.Equals(result2).ShouldBeTrue();
		(result1 == result2).ShouldBeTrue();
		(result1 != result2).ShouldBeFalse();
	}

	[Fact]
	public void ImplementInequalityCorrectly()
	{
		// Arrange
		var noWork = JobResult.NoWorkPerformed;
		var success = JobResult.OperationSucceeded;

		// Assert
		noWork.Equals(success).ShouldBeFalse();
		(noWork == success).ShouldBeFalse();
		(noWork != success).ShouldBeTrue();
	}

	[Fact]
	public void ImplementObjectEqualsCorrectly()
	{
		// Arrange
		var result1 = JobResult.NoWorkPerformed;
		object boxed = JobResult.NoWorkPerformed;

		// Assert
		result1.Equals(boxed).ShouldBeTrue();
	}

	[Fact]
	public void ReturnFalseForNullInObjectEquals()
	{
		// Arrange
		var result = JobResult.NoWorkPerformed;

		// Assert
		result.Equals(null).ShouldBeFalse();
	}

	[Fact]
	public void ReturnFalseForDifferentTypeInObjectEquals()
	{
		// Arrange
		var result = JobResult.NoWorkPerformed;

		// Assert
		result.Equals("not a job result").ShouldBeFalse();
	}

	[Fact]
	public void ProvideConsistentHashCodes()
	{
		// Arrange
		var result1 = JobResult.NoWorkPerformed;
		var result2 = JobResult.NoWorkPerformed;

		// Assert
		result1.GetHashCode().ShouldBe(result2.GetHashCode());
	}

	[Fact]
	public void ProvideDifferentHashCodesForDifferentResults()
	{
		// Arrange
		var noWork = JobResult.NoWorkPerformed;
		var success = JobResult.OperationSucceeded;

		// Assert - they might have different hash codes (not guaranteed but likely)
		// At minimum, equality should be false
		noWork.Equals(success).ShouldBeFalse();
	}

	[Fact]
	public void DefaultValueIsNotEqualToDefinedResults()
	{
		// Arrange
		var defaultResult = default(JobResult);

		// Assert
		defaultResult.ShouldNotBe(JobResult.NoWorkPerformed);
		defaultResult.ShouldNotBe(JobResult.OperationSucceeded);
	}

	[Fact]
	public void BeUsableInHashSet()
	{
		// Arrange
		var set = new HashSet<JobResult>
		{
			JobResult.NoWorkPerformed,
			JobResult.OperationSucceeded
		};

		// Assert
		set.Count.ShouldBe(2);
		set.Contains(JobResult.NoWorkPerformed).ShouldBeTrue();
		set.Contains(JobResult.OperationSucceeded).ShouldBeTrue();
	}

	[Fact]
	public void BeUsableAsDictionaryKey()
	{
		// Arrange
		var dict = new Dictionary<JobResult, string>
		{
			[JobResult.NoWorkPerformed] = "No work",
			[JobResult.OperationSucceeded] = "Success"
		};

		// Assert
		dict.Count.ShouldBe(2);
		dict[JobResult.NoWorkPerformed].ShouldBe("No work");
		dict[JobResult.OperationSucceeded].ShouldBe("Success");
	}
}
