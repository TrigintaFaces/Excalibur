// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Configuration;

namespace Excalibur.Dispatch.Tests.Messaging.Configuration;

/// <summary>
/// Unit tests for <see cref="PipelineSynthesisException"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class PipelineSynthesisExceptionShould
{
	[Fact]
	public void BeAssignableToException()
	{
		// Arrange & Act
		var exception = new PipelineSynthesisException();

		// Assert
		exception.ShouldBeAssignableTo<Exception>();
	}

	[Fact]
	public void StoreMessageProperty()
	{
		// Arrange
		var message = "Pipeline synthesis failed";
		var issues = Array.Empty<ValidationIssue>();

		// Act
		var exception = new PipelineSynthesisException(message, issues);

		// Assert
		exception.Message.ShouldBe(message);
	}

	[Fact]
	public void StoreValidationIssuesProperty()
	{
		// Arrange
		var issues = new[]
		{
			new ValidationIssue(ValidationSeverity.Error, "Error 1"),
			new ValidationIssue(ValidationSeverity.Warning, "Warning 1"),
		};

		// Act
		var exception = new PipelineSynthesisException("Synthesis failed", issues);

		// Assert
		exception.ValidationIssues.ShouldBe(issues);
		exception.ValidationIssues.Length.ShouldBe(2);
	}

	[Fact]
	public void ThrowOnNullValidationIssues()
	{
		// Arrange & Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new PipelineSynthesisException("message", (ValidationIssue[])null!));
	}

	[Fact]
	public void SupportParameterlessConstructor()
	{
		// Arrange & Act
		var exception = new PipelineSynthesisException();

		// Assert
		exception.Message.ShouldBe(string.Empty);
		exception.ValidationIssues.ShouldBeEmpty();
	}

	[Fact]
	public void SupportSingleParameterConstructor()
	{
		// Arrange
		var message = "Custom error message";

		// Act
		var exception = new PipelineSynthesisException(message);

		// Assert
		exception.Message.ShouldBe(message);
		exception.ValidationIssues.ShouldBeEmpty();
	}

	[Fact]
	public void SupportMessageAndInnerExceptionConstructor()
	{
		// Arrange
		var message = "Pipeline failed";
		Exception innerException = new InvalidOperationException("Inner error");

		// Act
		var exception = new PipelineSynthesisException(message, innerException);

		// Assert
		exception.Message.ShouldBe(message);
		exception.ValidationIssues.ShouldBeEmpty();
	}

	[Fact]
	public void HandleNullMessageInSingleParameterConstructor()
	{
		// Arrange & Act
		var exception = new PipelineSynthesisException(null);

		// Assert
		exception.Message.ShouldBe(string.Empty);
	}

	[Fact]
	public void HandleNullMessageInMessageAndInnerExceptionConstructor()
	{
		// Arrange & Act
		Exception innerException = new InvalidOperationException("Inner");
		var exception = new PipelineSynthesisException(null, innerException);

		// Assert
		exception.Message.ShouldBe(string.Empty);
	}

	[Fact]
	public void AcceptEmptyValidationIssuesArray()
	{
		// Arrange
		var issues = Array.Empty<ValidationIssue>();

		// Act
		var exception = new PipelineSynthesisException("message", issues);

		// Assert
		exception.ValidationIssues.ShouldBeEmpty();
	}

	[Fact]
	public void AcceptSingleValidationIssue()
	{
		// Arrange
		var issues = new[]
		{
			new ValidationIssue(ValidationSeverity.Error, "Single error"),
		};

		// Act
		var exception = new PipelineSynthesisException("message", issues);

		// Assert
		exception.ValidationIssues.Length.ShouldBe(1);
		exception.ValidationIssues[0].Severity.ShouldBe(ValidationSeverity.Error);
		exception.ValidationIssues[0].Message.ShouldBe("Single error");
	}

	[Fact]
	public void AcceptMultipleValidationIssuesOfDifferentSeverities()
	{
		// Arrange
		var issues = new[]
		{
			new ValidationIssue(ValidationSeverity.Error, "Error message"),
			new ValidationIssue(ValidationSeverity.Warning, "Warning message"),
			new ValidationIssue(ValidationSeverity.Info, "Info message"),
		};

		// Act
		var exception = new PipelineSynthesisException("Synthesis failed", issues);

		// Assert
		exception.ValidationIssues.Length.ShouldBe(3);
		exception.ValidationIssues.Count(i => i.Severity == ValidationSeverity.Error).ShouldBe(1);
		exception.ValidationIssues.Count(i => i.Severity == ValidationSeverity.Warning).ShouldBe(1);
		exception.ValidationIssues.Count(i => i.Severity == ValidationSeverity.Info).ShouldBe(1);
	}

	[Fact]
	public void SimulateTypicalSynthesisFailure()
	{
		// Arrange
		var issues = new[]
		{
			new ValidationIssue(ValidationSeverity.Error, "Middleware 'Auth' requires 'Identity' middleware"),
			new ValidationIssue(ValidationSeverity.Error, "Circular dependency detected: A -> B -> A"),
			new ValidationIssue(ValidationSeverity.Warning, "Middleware 'Logging' has no downstream consumers"),
		};

		// Act
		var exception = new PipelineSynthesisException("Pipeline synthesis failed with 2 errors and 1 warning", issues);

		// Assert
		exception.Message.ShouldContain("Pipeline synthesis failed");
		exception.ValidationIssues.Length.ShouldBe(3);
		exception.ValidationIssues.Count(i => i.Severity == ValidationSeverity.Error).ShouldBe(2);
		exception.ValidationIssues.Count(i => i.Severity == ValidationSeverity.Warning).ShouldBe(1);
	}

	[Fact]
	public void MaintainValidationIssuesOrder()
	{
		// Arrange
		var issues = new[]
		{
			new ValidationIssue(ValidationSeverity.Error, "First"),
			new ValidationIssue(ValidationSeverity.Warning, "Second"),
			new ValidationIssue(ValidationSeverity.Info, "Third"),
		};

		// Act
		var exception = new PipelineSynthesisException("message", issues);

		// Assert
		exception.ValidationIssues[0].Message.ShouldBe("First");
		exception.ValidationIssues[1].Message.ShouldBe("Second");
		exception.ValidationIssues[2].Message.ShouldBe("Third");
	}

	[Fact]
	public void BeCatchableAsException()
	{
		// Arrange
		var issues = new[] { new ValidationIssue(ValidationSeverity.Error, "Test error") };

		// Act & Assert
		var exception = Should.Throw<Exception>(() =>
		{
			throw new PipelineSynthesisException("Test", issues);
		});

		exception.ShouldBeOfType<PipelineSynthesisException>();
	}

	[Fact]
	public void SupportExceptionFiltering()
	{
		// Arrange
		var issues = new[] { new ValidationIssue(ValidationSeverity.Error, "Test error") };
		var exception = new PipelineSynthesisException("Test", issues);

		// Act
		var hasErrors = exception.ValidationIssues
			.Any(i => i.Severity == ValidationSeverity.Error);

		// Assert
		hasErrors.ShouldBeTrue();
	}
}
