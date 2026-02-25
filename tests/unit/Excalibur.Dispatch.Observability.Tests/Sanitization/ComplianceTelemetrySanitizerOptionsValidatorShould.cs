// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Observability.Sanitization;

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Observability.Tests.Sanitization;

/// <summary>
/// Unit tests for <see cref="ComplianceTelemetrySanitizerOptionsValidator"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
[Trait("Feature", "Sanitization")]
public sealed class ComplianceTelemetrySanitizerOptionsValidatorShould
{
	private readonly ComplianceTelemetrySanitizerOptionsValidator _validator = new();

	[Fact]
	public void SucceedWithDefaultOptions()
	{
		// Arrange
		var options = new ComplianceTelemetrySanitizerOptions();

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void Fail_WhenEnabledAndRedactedPlaceholderIsEmpty()
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
	public void Succeed_WhenDisabledAndRedactedPlaceholderIsEmpty()
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
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void Fail_WhenCustomPatternIsInvalidRegex()
	{
		// Arrange
		var options = new ComplianceTelemetrySanitizerOptions
		{
			CustomPatterns = ["[invalid-regex"],
		};

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("CustomPatterns[0]");
	}

	[Fact]
	public void Fail_WhenCustomPatternIsWhitespace()
	{
		// Arrange
		var options = new ComplianceTelemetrySanitizerOptions
		{
			CustomPatterns = ["   "],
		};

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Failed.ShouldBeTrue();
		result.FailureMessage.ShouldContain("null or whitespace");
	}

	[Fact]
	public void Succeed_WithValidCustomPatterns()
	{
		// Arrange
		var options = new ComplianceTelemetrySanitizerOptions
		{
			CustomPatterns = [@"\d{4}-\d{4}", @"[A-Z]{3}-\d+"],
		};

		// Act
		var result = _validator.Validate(null, options);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void ThrowOnNullOptions()
	{
		Should.Throw<ArgumentNullException>(() => _validator.Validate(null, null!));
	}

	[Fact]
	public void ImplementIValidateOptions()
	{
		_validator.ShouldBeAssignableTo<IValidateOptions<ComplianceTelemetrySanitizerOptions>>();
	}
}
