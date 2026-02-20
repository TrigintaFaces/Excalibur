// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Observability.Sanitization;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Observability.Tests.Sanitization;

/// <summary>
/// Deep coverage tests for <see cref="ComplianceTelemetrySanitizer"/> covering null value handling,
/// disabled mode passthrough, baseline null short-circuit, bounded hash cache overflow,
/// and mixed pattern detection scenarios.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
public sealed class ComplianceTelemetrySanitizerDepthCoverageShould
{
	[Fact]
	public void SanitizeTag_ReturnNull_WhenBaselineReturnsNull()
	{
		// Arrange — baseline sanitizer hashes sensitive tags to null when they're redacted
		// The inner HashingTelemetrySanitizer returns hash for known PII tags
		// but for unknown tags with null rawValue, baseline returns null
		var sanitizer = CreateSanitizer();

		// Act — null rawValue causes baseline to return null hash
		var result = sanitizer.SanitizeTag("generic.tag", null);

		// Assert — compliance layer short-circuits on null baseline result
		result.ShouldBeNull();
	}

	[Fact]
	public void SanitizeTag_PassThrough_WhenDisabled()
	{
		// Arrange — compliance disabled, raw PII enabled (baseline passthrough)
		var sanitizer = CreateSanitizer(enabled: false, includeRawPii: true);

		// Act — PII tag should NOT be redacted when compliance is disabled
		var result = sanitizer.SanitizeTag("user.email", "john@example.com");

		// Assert — raw value passes through
		result.ShouldBe("john@example.com");
	}

	[Fact]
	public void SanitizeTag_RedactPlaceholder_ForKnownPiiTag()
	{
		// Arrange — hash disabled, redact mode
		var sanitizer = CreateSanitizer(hashDetectedPii: false);

		// Act
		var result = sanitizer.SanitizeTag("user.email", "alice@company.com");

		// Assert
		result.ShouldBe("[REDACTED]");
	}

	[Fact]
	public void SanitizeTag_HashKnownPiiTag_WithEmptyString()
	{
		// Arrange — hash mode + known PII tag + empty value
		var sanitizer = CreateSanitizer(hashDetectedPii: true);

		// Act
		var result = sanitizer.SanitizeTag("user.email", "");

		// Assert — empty string gets hashed, not redacted
		result.ShouldNotBeNull();
		result.ShouldStartWith("sha256:");
	}

	[Fact]
	public void SanitizeTag_DetectMultiplePatterns_InSingleValue()
	{
		// Arrange — all detectors enabled, raw PII passthrough
		var sanitizer = CreateSanitizer(includeRawPii: true);

		// Act — value contains both email and phone
		var result = sanitizer.SanitizeTag("info", "Contact john@example.com or 555-123-4567");

		// Assert — both should be redacted
		result.ShouldNotBeNull();
		result.ShouldNotContain("john@example.com");
		result.ShouldNotContain("555-123-4567");
	}

	[Fact]
	public void SanitizeTag_CustomPattern_RedactMode()
	{
		// Arrange — custom pattern for credit card-like numbers
		var sanitizer = CreateSanitizer(customPatterns: [@"\d{4}-\d{4}-\d{4}-\d{4}"], includeRawPii: true);

		// Act
		var result = sanitizer.SanitizeTag("payment", "Card 1234-5678-9012-3456");

		// Assert
		result.ShouldNotBeNull();
		result.ShouldNotContain("1234-5678-9012-3456");
		result.ShouldContain("[REDACTED]");
	}

	[Fact]
	public void SanitizeTag_CustomPattern_HashMode()
	{
		// Arrange
		var sanitizer = CreateSanitizer(hashDetectedPii: true, customPatterns: ["SECRET_\\w+"], includeRawPii: true);

		// Act
		var result = sanitizer.SanitizeTag("config", "Key is SECRET_abc123");

		// Assert
		result.ShouldNotBeNull();
		result.ShouldNotContain("SECRET_abc123");
		result.ShouldContain("sha256:");
	}

	[Fact]
	public void SanitizePayload_ReturnBaselineHash_WhenComplianceDisabled()
	{
		// Arrange — disabled compliance, no raw PII (baseline hashes entire payload)
		var sanitizer = CreateSanitizer(enabled: false);

		// Act
		var result = sanitizer.SanitizePayload("some payload data");

		// Assert — baseline hashes the whole payload
		result.ShouldStartWith("sha256:");
	}

	[Fact]
	public void SanitizePayload_ProcessMultipleEmails()
	{
		// Arrange — raw PII passthrough so compliance layer sees raw text
		var sanitizer = CreateSanitizer(includeRawPii: true);

		// Act
		var result = sanitizer.SanitizePayload("a@b.com, c@d.org, e@f.net");

		// Assert — all 3 emails should be redacted
		result.ShouldNotContain("a@b.com");
		result.ShouldNotContain("c@d.org");
		result.ShouldNotContain("e@f.net");
	}

	[Fact]
	public void SanitizePayload_PreserveNonPiiContent()
	{
		// Arrange — raw PII passthrough
		var sanitizer = CreateSanitizer(includeRawPii: true);

		// Act — no PII patterns in this text
		var result = sanitizer.SanitizePayload("Order #12345 shipped to warehouse");

		// Assert — non-PII content preserved
		result.ShouldContain("Order #12345");
		result.ShouldContain("shipped to warehouse");
	}

	[Fact]
	public void HashCache_ReturnConsistentResults()
	{
		// Arrange
		var sanitizer = CreateSanitizer(hashDetectedPii: true);

		// Act — hash the same PII tag value multiple times
		var result1 = sanitizer.SanitizeTag("user.email", "consistent@test.com");
		var result2 = sanitizer.SanitizeTag("user.email", "consistent@test.com");
		var result3 = sanitizer.SanitizeTag("user.email", "consistent@test.com");

		// Assert — all should be identical (cache hit)
		result1.ShouldBe(result2);
		result2.ShouldBe(result3);
	}

	[Fact]
	public void SanitizeTag_WithCustomRedactedPlaceholder()
	{
		// Arrange — custom placeholder
		var options = MsOptions.Create(new ComplianceTelemetrySanitizerOptions
		{
			Enabled = true,
			RedactedPlaceholder = "***REMOVED***",
			RedactedTagNames = ["user.email"],
		});
		var sanitizerOptions = MsOptions.Create(new TelemetrySanitizerOptions { IncludeRawPii = true });
		var sanitizer = new ComplianceTelemetrySanitizer(options, sanitizerOptions);

		// Act
		var result = sanitizer.SanitizeTag("user.email", "alice@example.com");

		// Assert
		result.ShouldBe("***REMOVED***");
	}

	[Fact]
	public void SanitizeTag_EmptyCustomPatterns_NoError()
	{
		// Arrange — empty custom patterns list
		var sanitizer = CreateSanitizer(customPatterns: [], includeRawPii: true);

		// Act & Assert — should work without custom patterns
		var result = sanitizer.SanitizeTag("data", "no patterns here");
		result.ShouldNotBeNull();
		result.ShouldBe("no patterns here");
	}

	[Fact]
	public void Constructor_ThrowsOnNullOptions()
	{
		Should.Throw<ArgumentNullException>(() =>
			new ComplianceTelemetrySanitizer(null!, MsOptions.Create(new TelemetrySanitizerOptions())));
	}

	[Fact]
	public void Constructor_ThrowsOnNullSanitizerOptions()
	{
		Should.Throw<ArgumentNullException>(() =>
			new ComplianceTelemetrySanitizer(
				MsOptions.Create(new ComplianceTelemetrySanitizerOptions()), null!));
	}

	private static ComplianceTelemetrySanitizer CreateSanitizer(
		bool enabled = true,
		bool hashDetectedPii = false,
		bool includeRawPii = false,
		IList<string>? customPatterns = null)
	{
		var complianceOptions = MsOptions.Create(new ComplianceTelemetrySanitizerOptions
		{
			Enabled = enabled,
			HashDetectedPii = hashDetectedPii,
			CustomPatterns = customPatterns ?? [],
		});
		var sanitizerOptions = MsOptions.Create(new TelemetrySanitizerOptions
		{
			IncludeRawPii = includeRawPii,
		});

		return new ComplianceTelemetrySanitizer(complianceOptions, sanitizerOptions);
	}
}
