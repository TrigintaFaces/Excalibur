// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Jobs.Workflows;

namespace Excalibur.Jobs.Tests.Workflows;

/// <summary>
/// Unit tests for <see cref="WorkflowResult{TOutput}"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Jobs")]
[Trait("Feature", "Workflows")]
public sealed class WorkflowResultShould : UnitTestBase
{
	[Fact]
	public void CreateSuccessResultWithOutput()
	{
		// Act
		var result = WorkflowResult<string>.Success("test output");

		// Assert
		result.IsSuccess.ShouldBeTrue();
		result.Output.ShouldBe("test output");
		result.Error.ShouldBeNull();
		result.Status.ShouldBe(WorkflowStatus.Completed);
	}

	[Fact]
	public void CreateFailureResultWithError()
	{
		// Arrange
		var exception = new InvalidOperationException("Test error");

		// Act
		var result = WorkflowResult<string>.Failure(exception);

		// Assert
		result.IsSuccess.ShouldBeFalse();
		result.Output.ShouldBeNull();
		result.Error.ShouldBe(exception);
		result.Status.ShouldBe(WorkflowStatus.Failed);
	}

	[Fact]
	public void CreateSuspendedResultWithReason()
	{
		// Act
		var result = WorkflowResult<string>.Suspended("Waiting for approval");

		// Assert
		result.IsSuccess.ShouldBeFalse();
		result.Output.ShouldBeNull();
		_ = result.Error.ShouldNotBeNull();
		result.Error.Message.ShouldBe("Waiting for approval");
		result.Status.ShouldBe(WorkflowStatus.Suspended);
	}

	[Fact]
	public void CreateSuspendedResultWithoutReason()
	{
		// Act
		var result = WorkflowResult<string>.Suspended();

		// Assert
		result.IsSuccess.ShouldBeFalse();
		result.Error.ShouldBeNull();
		result.Status.ShouldBe(WorkflowStatus.Suspended);
	}

	[Fact]
	public void HaveCompletedAtTimestamp()
	{
		// Arrange
		var before = DateTimeOffset.UtcNow;

		// Act
		var result = WorkflowResult<string>.Success("output");

		// Assert
		var after = DateTimeOffset.UtcNow;
		result.CompletedAt.ShouldBeGreaterThanOrEqualTo(before);
		result.CompletedAt.ShouldBeLessThanOrEqualTo(after);
	}

	[Fact]
	public void WorkWithIntOutput()
	{
		// Act
		var result = WorkflowResult<int>.Success(42);

		// Assert
		result.Output.ShouldBe(42);
	}

	[Fact]
	public void WorkWithComplexOutput()
	{
		// Arrange
		var complexOutput = new TestOutput { Id = 1, Name = "Test" };

		// Act
		var result = WorkflowResult<TestOutput>.Success(complexOutput);

		// Assert
		result.Output.ShouldBe(complexOutput);
	}

	[Fact]
	public void AllowNullOutputOnSuccess()
	{
		// Act
		var result = WorkflowResult<string?>.Success(null);

		// Assert
		result.IsSuccess.ShouldBeTrue();
		result.Output.ShouldBeNull();
	}

	[Fact]
	public void CreateViaConstructorWithAllParameters()
	{
		// Arrange
		var error = new InvalidOperationException("Error");

		// Act
		var result = new WorkflowResult<int>(
			IsSuccess: false,
			Output: 0,
			Error: error,
			Status: WorkflowStatus.Cancelled);

		// Assert
		result.IsSuccess.ShouldBeFalse();
		result.Output.ShouldBe(0);
		result.Error.ShouldBe(error);
		result.Status.ShouldBe(WorkflowStatus.Cancelled);
	}

	[Fact]
	public void CreateViaConstructorWithDefaults()
	{
		// Act
		var result = new WorkflowResult<string>(IsSuccess: true);

		// Assert
		result.IsSuccess.ShouldBeTrue();
		result.Output.ShouldBeNull();
		result.Error.ShouldBeNull();
		result.Status.ShouldBe(WorkflowStatus.Completed);
	}

	private sealed class TestOutput
	{
		public int Id { get; init; }
		public string Name { get; init; } = string.Empty;
	}
}
