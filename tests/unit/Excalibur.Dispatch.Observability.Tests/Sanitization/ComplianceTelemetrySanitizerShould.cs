// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Observability.Sanitization;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Observability.Tests.Sanitization;

/// <summary>
/// Unit tests for <see cref="ComplianceTelemetrySanitizer"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
[Trait("Feature", "Sanitization")]
public sealed class ComplianceTelemetrySanitizerShould
{
	[Fact]
	public void RedactKnownPiiTagName()
	{
		// Arrange
		var sanitizer = CreateSanitizer();

		// Act
		var result = sanitizer.SanitizeTag("user.email", "john@example.com");

		// Assert
		result.ShouldBe("[REDACTED]");
	}

	[Fact]
	public void DetectAndRedactEmailInValue()
	{
		// Arrange
		var sanitizer = CreateSanitizer();

		// Act
		var result = sanitizer.SanitizeTag("description", "Contact user at john@example.com for details");

		// Assert
		result.ShouldNotBeNull();
		result.ShouldNotContain("john@example.com");
		result.ShouldContain("[REDACTED]");
	}

	[Fact]
	public void DetectAndRedactPhoneNumberInValue()
	{
		// Arrange
		var sanitizer = CreateSanitizer();

		// Act
		var result = sanitizer.SanitizeTag("info", "Call 555-123-4567 for support");

		// Assert
		result.ShouldNotBeNull();
		result.ShouldNotContain("555-123-4567");
	}

	[Fact]
	public void DetectAndRedactSsnInValue()
	{
		// Arrange
		var sanitizer = CreateSanitizer();

		// Act
		var result = sanitizer.SanitizeTag("data", "SSN is 123-45-6789");

		// Assert
		result.ShouldNotBeNull();
		result.ShouldNotContain("123-45-6789");
	}

	[Fact]
	public void HashDetectedPii_WhenHashModeEnabled()
	{
		// Arrange
		var sanitizer = CreateSanitizer(hashDetectedPii: true);

		// Act
		var result = sanitizer.SanitizeTag("user.email", "john@example.com");

		// Assert
		result.ShouldNotBeNull();
		result.ShouldStartWith("sha256:");
	}

	[Fact]
	public void PassThroughNonPiiValues()
	{
		// Arrange
		var sanitizer = CreateSanitizer();

		// Act
		var result = sanitizer.SanitizeTag("operation", "CreateOrder");

		// Assert
		result.ShouldBe("CreateOrder");
	}

	[Fact]
	public void ReturnNull_WhenBaselineReturnsNull()
	{
		// Arrange
		var sanitizer = CreateSanitizer();

		// Act
		var result = sanitizer.SanitizeTag("some-tag", null);

		// Assert — null input should still return null from baseline
		// (baseline HashingTelemetrySanitizer returns null for null)
		result.ShouldBeNull();
	}

	[Fact]
	public void ApplyPatternDetection_ToPayload()
	{
		// Arrange
		var sanitizer = CreateSanitizer();

		// Act
		var result = sanitizer.SanitizePayload("User email is test@example.com");

		// Assert
		result.ShouldNotContain("test@example.com");
	}

	[Fact]
	public void BypassComplianceRules_WhenDisabled()
	{
		// Arrange
		var sanitizer = CreateSanitizer(enabled: false);

		// Act — even PII tag names should pass through if compliance is disabled
		var result = sanitizer.SanitizeTag("user.email", "john@example.com");

		// Assert — baseline sanitizer still runs, so value may be hashed by baseline
		result.ShouldNotBeNull();
	}

	[Fact]
	public void ApplyCustomPatterns()
	{
		// Arrange
		var sanitizer = CreateSanitizer(customPatterns: ["INTERNAL-\\d+"]);

		// Act
		var result = sanitizer.SanitizeTag("ref", "See INTERNAL-12345 for details");

		// Assert
		result.ShouldNotBeNull();
		result.ShouldNotContain("INTERNAL-12345");
	}

	[Fact]
	public void ThrowOnNullOptions()
	{
		Should.Throw<ArgumentNullException>(() =>
			new ComplianceTelemetrySanitizer(null!, MsOptions.Create(new TelemetrySanitizerOptions())));
	}

	[Fact]
	public void ThrowOnNullSanitizerOptions()
	{
		Should.Throw<ArgumentNullException>(() =>
			new ComplianceTelemetrySanitizer(
				MsOptions.Create(new ComplianceTelemetrySanitizerOptions()),
				null!));
	}

	private static ComplianceTelemetrySanitizer CreateSanitizer(
		bool enabled = true,
		bool hashDetectedPii = false,
		IList<string>? customPatterns = null)
	{
		var complianceOptions = MsOptions.Create(new ComplianceTelemetrySanitizerOptions
		{
			Enabled = enabled,
			HashDetectedPii = hashDetectedPii,
			CustomPatterns = customPatterns ?? [],
		});
		var sanitizerOptions = MsOptions.Create(new TelemetrySanitizerOptions());

		return new ComplianceTelemetrySanitizer(complianceOptions, sanitizerOptions);
	}
}
