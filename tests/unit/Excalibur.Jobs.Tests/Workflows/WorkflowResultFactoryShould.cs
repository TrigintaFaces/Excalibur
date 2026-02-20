// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Jobs.Workflows;

namespace Excalibur.Jobs.Tests.Workflows;

/// <summary>
/// Unit tests for <see cref="WorkflowResultFactory"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Jobs")]
[Trait("Feature", "Workflows")]
public sealed class WorkflowResultFactoryShould
{
	[Fact]
	public void CreateSuccessWithStringOutput()
	{
		// Act
		var result = WorkflowResultFactory.Success("completed successfully");

		// Assert
		result.IsSuccess.ShouldBeTrue();
		result.Output.ShouldBe("completed successfully");
		result.Error.ShouldBeNull();
		result.Status.ShouldBe(WorkflowStatus.Completed);
	}

	[Fact]
	public void CreateSuccessWithIntOutput()
	{
		// Act
		var result = WorkflowResultFactory.Success(42);

		// Assert
		result.IsSuccess.ShouldBeTrue();
		result.Output.ShouldBe(42);
	}

	[Fact]
	public void CreateSuccessWithComplexOutput()
	{
		// Arrange
		var data = new TestData { Id = 1, Name = "Test" };

		// Act
		var result = WorkflowResultFactory.Success(data);

		// Assert
		result.IsSuccess.ShouldBeTrue();
		result.Output.ShouldBe(data);
	}

	[Fact]
	public void CreateSuccessWithNullOutput()
	{
		// Act
		var result = WorkflowResultFactory.Success<string?>(null);

		// Assert
		result.IsSuccess.ShouldBeTrue();
		result.Output.ShouldBeNull();
	}

	[Fact]
	public void CreateFailureWithException()
	{
		// Arrange
		var exception = new InvalidOperationException("Something went wrong");

		// Act
		var result = WorkflowResultFactory.Failure<string>(exception);

		// Assert
		result.IsSuccess.ShouldBeFalse();
		result.Output.ShouldBeNull();
		result.Error.ShouldBe(exception);
		result.Status.ShouldBe(WorkflowStatus.Failed);
	}

	[Fact]
	public void ThrowOnNullExceptionForFailure()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() => WorkflowResultFactory.Failure<string>(null!));
	}

	[Fact]
	public void CreateSuspendedWithReason()
	{
		// Act
		var result = WorkflowResultFactory.Suspended<int>("Waiting for external approval");

		// Assert
		result.IsSuccess.ShouldBeFalse();
		result.Status.ShouldBe(WorkflowStatus.Suspended);
		result.Error.ShouldNotBeNull();
		result.Error.Message.ShouldBe("Waiting for external approval");
	}

	[Fact]
	public void CreateSuspendedWithoutReason()
	{
		// Act
		var result = WorkflowResultFactory.Suspended<int>();

		// Assert
		result.IsSuccess.ShouldBeFalse();
		result.Status.ShouldBe(WorkflowStatus.Suspended);
		result.Error.ShouldBeNull();
	}

	[Fact]
	public void CreateFailureWithDifferentExceptionTypes()
	{
		// Arrange
		var invalidOp = new InvalidOperationException("Invalid");
		var argument = new ArgumentException("Bad argument");
		var notSupported = new NotSupportedException("Not supported");

		// Act
		var result1 = WorkflowResultFactory.Failure<int>(invalidOp);
		var result2 = WorkflowResultFactory.Failure<int>(argument);
		var result3 = WorkflowResultFactory.Failure<int>(notSupported);

		// Assert
		result1.Error.ShouldBeOfType<InvalidOperationException>();
		result2.Error.ShouldBeOfType<ArgumentException>();
		result3.Error.ShouldBeOfType<NotSupportedException>();
	}

	[Fact]
	public void MaintainOutputTypeOnFailure()
	{
		// Arrange
		var exception = new InvalidOperationException("Error");

		// Act
		var intResult = WorkflowResultFactory.Failure<int>(exception);
		var stringResult = WorkflowResultFactory.Failure<string>(exception);
		var complexResult = WorkflowResultFactory.Failure<TestData>(exception);

		// Assert
		intResult.Output.ShouldBe(default(int));
		stringResult.Output.ShouldBeNull();
		complexResult.Output.ShouldBeNull();
	}

	private sealed class TestData
	{
		public int Id { get; init; }
		public string Name { get; init; } = string.Empty;
	}
}
