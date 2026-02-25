// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Validation.Context;

namespace Excalibur.Dispatch.Tests.Validation;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ContextValidationResultShould
{
	[Fact]
	public void DefaultConstructor_SetValidToTrue()
	{
		// Act
		var result = new ContextValidationResult();

		// Assert
		result.IsValid.ShouldBeTrue();
		result.FailureReason.ShouldBe(string.Empty);
		result.MissingFields.ShouldNotBeNull();
		result.MissingFields.ShouldBeEmpty();
		result.CorruptedFields.ShouldNotBeNull();
		result.CorruptedFields.ShouldBeEmpty();
		result.Details.ShouldNotBeNull();
		result.Details.ShouldBeEmpty();
		result.Severity.ShouldBe(ValidationSeverity.Info);
		result.ValidationTimestamp.ShouldBeGreaterThan(DateTimeOffset.UtcNow.AddMinutes(-1));
	}

	[Fact]
	public void Success_ReturnValidResult()
	{
		// Act
		var result = ContextValidationResult.Success();

		// Assert
		result.IsValid.ShouldBeTrue();
		result.Severity.ShouldBe(ValidationSeverity.Info);
	}

	[Fact]
	public void Failure_ReturnInvalidResult_WithReasonAndDefaultSeverity()
	{
		// Act
		var result = ContextValidationResult.Failure("Missing correlation ID");

		// Assert
		result.IsValid.ShouldBeFalse();
		result.FailureReason.ShouldBe("Missing correlation ID");
		result.Severity.ShouldBe(ValidationSeverity.Error);
	}

	[Fact]
	public void Failure_ReturnInvalidResult_WithCustomSeverity()
	{
		// Act
		var result = ContextValidationResult.Failure("Minor issue", ValidationSeverity.Warning);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.FailureReason.ShouldBe("Minor issue");
		result.Severity.ShouldBe(ValidationSeverity.Warning);
	}

	[Fact]
	public void FailureWithFields_SetMissingFields()
	{
		// Act
		var result = ContextValidationResult.FailureWithFields(
			"Validation failed",
			missingFields: ["MessageId", "CorrelationId"]);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.FailureReason.ShouldBe("Validation failed");
		result.MissingFields.Count.ShouldBe(2);
		result.MissingFields.ShouldContain("MessageId");
		result.MissingFields.ShouldContain("CorrelationId");
		result.CorruptedFields.ShouldBeEmpty();
		result.Severity.ShouldBe(ValidationSeverity.Error);
	}

	[Fact]
	public void FailureWithFields_SetCorruptedFields()
	{
		// Act
		var result = ContextValidationResult.FailureWithFields(
			"Corrupted context",
			corruptedFields: ["TraceContext", "Metadata"]);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.CorruptedFields.Count.ShouldBe(2);
		result.CorruptedFields.ShouldContain("TraceContext");
		result.CorruptedFields.ShouldContain("Metadata");
		result.MissingFields.ShouldBeEmpty();
	}

	[Fact]
	public void FailureWithFields_SetBothMissingAndCorruptedFields()
	{
		// Act
		var result = ContextValidationResult.FailureWithFields(
			"Multiple issues",
			missingFields: ["MessageId"],
			corruptedFields: ["TraceContext"],
			severity: ValidationSeverity.Critical);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.MissingFields.Count.ShouldBe(1);
		result.CorruptedFields.Count.ShouldBe(1);
		result.Severity.ShouldBe(ValidationSeverity.Critical);
	}

	[Fact]
	public void FailureWithFields_HandleNullFieldLists()
	{
		// Act
		var result = ContextValidationResult.FailureWithFields(
			"Failure",
			missingFields: null,
			corruptedFields: null);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.MissingFields.ShouldBeEmpty();
		result.CorruptedFields.ShouldBeEmpty();
	}

	[Fact]
	public void Details_CanAddEntries()
	{
		// Arrange
		var result = new ContextValidationResult();

		// Act
		result.Details["key1"] = "value1";
		result.Details["key2"] = 42;

		// Assert
		result.Details.Count.ShouldBe(2);
		result.Details["key1"].ShouldBe("value1");
		result.Details["key2"].ShouldBe(42);
	}
}
