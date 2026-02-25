// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Validation.Context;

namespace Excalibur.Dispatch.Tests.Messaging.Validation.Context;

/// <summary>
/// Unit tests for <see cref="ContextValidationResult"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ContextValidationResultShould
{
	[Fact]
	public void HaveDefaultValidState()
	{
		// Arrange & Act
		var result = new ContextValidationResult();

		// Assert
		result.IsValid.ShouldBeTrue();
		result.FailureReason.ShouldBe(string.Empty);
		result.MissingFields.ShouldBeEmpty();
		result.CorruptedFields.ShouldBeEmpty();
		result.Details.ShouldBeEmpty();
		result.Severity.ShouldBe(ValidationSeverity.Info);
	}

	[Fact]
	public void HaveTimestampByDefault()
	{
		// Arrange
		var before = DateTimeOffset.UtcNow;

		// Act
		var result = new ContextValidationResult();
		var after = DateTimeOffset.UtcNow;

		// Assert
		result.ValidationTimestamp.ShouldBeGreaterThanOrEqualTo(before);
		result.ValidationTimestamp.ShouldBeLessThanOrEqualTo(after);
	}

	[Fact]
	public void AllowSettingIsValid()
	{
		// Arrange
		var result = new ContextValidationResult();

		// Act
		result.IsValid = false;

		// Assert
		result.IsValid.ShouldBeFalse();
	}

	[Fact]
	public void AllowSettingFailureReason()
	{
		// Arrange
		var result = new ContextValidationResult();

		// Act
		result.FailureReason = "Missing required field";

		// Assert
		result.FailureReason.ShouldBe("Missing required field");
	}

	[Fact]
	public void AllowAddingMissingFields()
	{
		// Arrange
		var result = new ContextValidationResult();

		// Act
		result.MissingFields.Add("CorrelationId");
		result.MissingFields.Add("MessageId");

		// Assert
		result.MissingFields.Count.ShouldBe(2);
		result.MissingFields.ShouldContain("CorrelationId");
		result.MissingFields.ShouldContain("MessageId");
	}

	[Fact]
	public void AllowAddingCorruptedFields()
	{
		// Arrange
		var result = new ContextValidationResult();

		// Act
		result.CorruptedFields.Add("Timestamp");
		result.CorruptedFields.Add("Version");

		// Assert
		result.CorruptedFields.Count.ShouldBe(2);
		result.CorruptedFields.ShouldContain("Timestamp");
		result.CorruptedFields.ShouldContain("Version");
	}

	[Fact]
	public void AllowAddingDetails()
	{
		// Arrange
		var result = new ContextValidationResult();

		// Act
		result.Details["validatorName"] = "ContextValidator";
		result.Details["executionTime"] = 42;

		// Assert
		result.Details.Count.ShouldBe(2);
		result.Details["validatorName"].ShouldBe("ContextValidator");
		result.Details["executionTime"].ShouldBe(42);
	}

	[Fact]
	public void AllowSettingValidationTimestamp()
	{
		// Arrange
		var result = new ContextValidationResult();
		var customTimestamp = new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);

		// Act
		result.ValidationTimestamp = customTimestamp;

		// Assert
		result.ValidationTimestamp.ShouldBe(customTimestamp);
	}

	[Fact]
	public void AllowSettingSeverity()
	{
		// Arrange
		var result = new ContextValidationResult();

		// Act
		result.Severity = ValidationSeverity.Critical;

		// Assert
		result.Severity.ShouldBe(ValidationSeverity.Critical);
	}

	[Fact]
	public void Success_CreatesValidResult()
	{
		// Act
		var result = ContextValidationResult.Success();

		// Assert
		result.IsValid.ShouldBeTrue();
		result.Severity.ShouldBe(ValidationSeverity.Info);
		result.FailureReason.ShouldBe(string.Empty);
		result.MissingFields.ShouldBeEmpty();
		result.CorruptedFields.ShouldBeEmpty();
	}

	[Fact]
	public void Failure_CreatesInvalidResult()
	{
		// Arrange
		const string reason = "Context is missing required fields";

		// Act
		var result = ContextValidationResult.Failure(reason);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.FailureReason.ShouldBe(reason);
		result.Severity.ShouldBe(ValidationSeverity.Error);
	}

	[Theory]
	[InlineData(ValidationSeverity.Info)]
	[InlineData(ValidationSeverity.Warning)]
	[InlineData(ValidationSeverity.Error)]
	[InlineData(ValidationSeverity.Critical)]
	public void Failure_AcceptsCustomSeverity(ValidationSeverity severity)
	{
		// Act
		var result = ContextValidationResult.Failure("Test failure", severity);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Severity.ShouldBe(severity);
	}

	[Fact]
	public void FailureWithFields_IncludesMissingFields()
	{
		// Arrange
		var missingFields = new[] { "Field1", "Field2" };

		// Act
		var result = ContextValidationResult.FailureWithFields(
			"Validation failed",
			missingFields: missingFields);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.MissingFields.Count.ShouldBe(2);
		result.MissingFields.ShouldContain("Field1");
		result.MissingFields.ShouldContain("Field2");
	}

	[Fact]
	public void FailureWithFields_IncludesCorruptedFields()
	{
		// Arrange
		var corruptedFields = new[] { "Corrupt1", "Corrupt2" };

		// Act
		var result = ContextValidationResult.FailureWithFields(
			"Validation failed",
			corruptedFields: corruptedFields);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.CorruptedFields.Count.ShouldBe(2);
		result.CorruptedFields.ShouldContain("Corrupt1");
		result.CorruptedFields.ShouldContain("Corrupt2");
	}

	[Fact]
	public void FailureWithFields_IncludesBothFieldTypes()
	{
		// Arrange
		var missingFields = new[] { "Missing1" };
		var corruptedFields = new[] { "Corrupted1", "Corrupted2" };

		// Act
		var result = ContextValidationResult.FailureWithFields(
			"Multiple issues found",
			missingFields: missingFields,
			corruptedFields: corruptedFields,
			severity: ValidationSeverity.Critical);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.FailureReason.ShouldBe("Multiple issues found");
		result.MissingFields.Count.ShouldBe(1);
		result.CorruptedFields.Count.ShouldBe(2);
		result.Severity.ShouldBe(ValidationSeverity.Critical);
	}

	[Fact]
	public void FailureWithFields_HandlesNullMissingFields()
	{
		// Act
		var result = ContextValidationResult.FailureWithFields(
			"Validation failed",
			missingFields: null);

		// Assert
		result.MissingFields.ShouldBeEmpty();
	}

	[Fact]
	public void FailureWithFields_HandlesNullCorruptedFields()
	{
		// Act
		var result = ContextValidationResult.FailureWithFields(
			"Validation failed",
			corruptedFields: null);

		// Assert
		result.CorruptedFields.ShouldBeEmpty();
	}

	[Fact]
	public void FailureWithFields_DefaultsToErrorSeverity()
	{
		// Act
		var result = ContextValidationResult.FailureWithFields("Test");

		// Assert
		result.Severity.ShouldBe(ValidationSeverity.Error);
	}

	[Fact]
	public void SupportObjectInitializer()
	{
		// Arrange & Act
		var result = new ContextValidationResult
		{
			IsValid = false,
			FailureReason = "Custom failure",
			Severity = ValidationSeverity.Warning,
			MissingFields = ["Field1"],
			CorruptedFields = ["Field2"],
			Details = new Dictionary<string, object?>(StringComparer.Ordinal) { ["key"] = "value" },
		};

		// Assert
		result.IsValid.ShouldBeFalse();
		result.FailureReason.ShouldBe("Custom failure");
		result.Severity.ShouldBe(ValidationSeverity.Warning);
		result.MissingFields.ShouldContain("Field1");
		result.CorruptedFields.ShouldContain("Field2");
		result.Details["key"].ShouldBe("value");
	}
}
