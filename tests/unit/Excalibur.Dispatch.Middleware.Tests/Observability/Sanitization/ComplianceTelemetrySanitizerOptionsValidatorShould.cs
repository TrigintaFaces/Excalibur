// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Observability.Sanitization;

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Middleware.Tests.Observability.Sanitization;

/// <summary>
/// Unit tests for <see cref="ComplianceTelemetrySanitizerOptionsValidator"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ComplianceTelemetrySanitizerOptionsValidatorShould : UnitTestBase
{
	private readonly ComplianceTelemetrySanitizerOptionsValidator _validator = new();

	[Fact]
	public void Validate_ThrowsArgumentNullException_WhenOptionsIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			_validator.Validate(null, null!));
	}

	[Fact]
	public void Validate_ReturnsSuccess_WhenOptionsAreValid()
	{
		// Arrange
		var options = new ComplianceTelemetrySanitizerOptions();

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.ShouldBe(ValidateOptionsResult.Success);
	}

	[Fact]
	public void Validate_ReturnsFail_WhenEnabledAndRedactedPlaceholderIsEmpty()
	{
		// Arrange
		var options = new ComplianceTelemetrySanitizerOptions
		{
			Enabled = true,
			RedactedPlaceholder = "",
		};

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("RedactedPlaceholder");
	}

	[Fact]
	public void Validate_ReturnsFail_WhenEnabledAndRedactedPlaceholderIsWhitespace()
	{
		// Arrange
		var options = new ComplianceTelemetrySanitizerOptions
		{
			Enabled = true,
			RedactedPlaceholder = "   ",
		};

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
	}

	[Fact]
	public void Validate_ReturnsSuccess_WhenDisabledAndRedactedPlaceholderIsEmpty()
	{
		// Arrange
		var options = new ComplianceTelemetrySanitizerOptions
		{
			Enabled = false,
			RedactedPlaceholder = "",
		};

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.ShouldBe(ValidateOptionsResult.Success);
	}

	[Fact]
	public void Validate_ReturnsFail_WhenCustomPatternIsInvalidRegex()
	{
		// Arrange
		var options = new ComplianceTelemetrySanitizerOptions
		{
			CustomPatterns = [@"[invalid(regex"],
		};

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("CustomPatterns[0]");
		result.FailureMessage.ShouldContain("valid regex");
	}

	[Fact]
	public void Validate_ReturnsFail_WhenCustomPatternIsNullOrWhitespace()
	{
		// Arrange
		var options = new ComplianceTelemetrySanitizerOptions
		{
			CustomPatterns = ["", "   "],
		};

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("CustomPatterns[0]");
		result.FailureMessage.ShouldContain("CustomPatterns[1]");
	}

	[Fact]
	public void Validate_ReturnsSuccess_WhenCustomPatternsAreValidRegex()
	{
		// Arrange
		var options = new ComplianceTelemetrySanitizerOptions
		{
			CustomPatterns = [@"\bAPI-KEY-\w+\b", @"\bTOKEN-\d+\b"],
		};

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.ShouldBe(ValidateOptionsResult.Success);
	}

	[Fact]
	public void Validate_CollectsMultipleFailures()
	{
		// Arrange
		var options = new ComplianceTelemetrySanitizerOptions
		{
			Enabled = true,
			RedactedPlaceholder = "",
			CustomPatterns = [@"[bad(regex", ""],
		};

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		// Should contain failures for RedactedPlaceholder, CustomPatterns[0], and CustomPatterns[1]
		result.FailureMessage.ShouldContain("RedactedPlaceholder");
		result.FailureMessage.ShouldContain("CustomPatterns");
	}
}
