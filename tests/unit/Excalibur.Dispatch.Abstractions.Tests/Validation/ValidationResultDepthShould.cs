// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Validation;

namespace Excalibur.Dispatch.Abstractions.Tests.Validation;

/// <summary>
/// Depth coverage tests for <see cref="ValidationResult"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ValidationResultDepthShould
{
	[Fact]
	public void Success_ReturnsValidResult()
	{
		var result = ValidationResult.Success();
		result.IsValid.ShouldBeTrue();
		result.Errors.ShouldBeEmpty();
		result.Warnings.ShouldBeEmpty();
	}

	[Fact]
	public void SuccessWithWarnings_ReturnsValidResultWithWarnings()
	{
		var result = ValidationResult.SuccessWithWarnings("warn1", "warn2");
		result.IsValid.ShouldBeTrue();
		result.Errors.ShouldBeEmpty();
		result.Warnings.Count.ShouldBe(2);
		result.Warnings[0].ShouldBe("warn1");
		result.Warnings[1].ShouldBe("warn2");
	}

	[Fact]
	public void Failure_WithValidationErrors_ReturnsInvalidResult()
	{
		var error1 = new ValidationError("Field is required");
		var error2 = new ValidationError("Value out of range");

		var result = ValidationResult.Failure(error1, error2);
		result.IsValid.ShouldBeFalse();
		result.Errors.Count.ShouldBe(2);
		result.Errors[0].Message.ShouldBe("Field is required");
		result.Errors[1].Message.ShouldBe("Value out of range");
	}

	[Fact]
	public void Failure_WithStringMessages_CreatesValidationErrors()
	{
		var result = ValidationResult.Failure("Error A", "Error B");
		result.IsValid.ShouldBeFalse();
		result.Errors.Count.ShouldBe(2);
		result.Errors[0].Message.ShouldBe("Error A");
		result.Errors[1].Message.ShouldBe("Error B");
	}

	[Fact]
	public void Failure_WithEmptyStringMessages_ReturnsEmptyErrors()
	{
		var result = ValidationResult.Failure(Array.Empty<string>());
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldBeEmpty();
	}

	[Fact]
	public void FailureWithWarnings_SetsErrorsAndWarnings()
	{
		var errors = new[] { new ValidationError("err1") };
		var warnings = new[] { "warn1", "warn2" };

		var result = ValidationResult.FailureWithWarnings(errors, warnings);
		result.IsValid.ShouldBeFalse();
		result.Errors.Count.ShouldBe(1);
		result.Warnings.Count.ShouldBe(2);
	}
}
