// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Observability.Sanitization;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Observability.Tests.Sanitization;

/// <summary>
/// In-depth unit tests for <see cref="ComplianceTelemetrySanitizer"/> covering uncovered code paths.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
[Trait("Feature", "Sanitization")]
public sealed class ComplianceTelemetrySanitizerDepthShould
{
	[Fact]
	public void HashKnownPiiTag_WhenHashModeEnabled()
	{
		// Arrange
		var sanitizer = CreateSanitizer(hashDetectedPii: true);

		// Act
		var result = sanitizer.SanitizeTag("user.email", "john@example.com");

		// Assert
		result.ShouldNotBeNull();
		result.ShouldStartWith("sha256:");
		result.Length.ShouldBe(71); // "sha256:" (7) + 64 hex chars
	}

	[Fact]
	public void HashCacheIsConsistent_SameValueSameHash()
	{
		// Arrange
		var sanitizer = CreateSanitizer(hashDetectedPii: true);

		// Act — hash the same value twice
		var result1 = sanitizer.SanitizeTag("user.email", "john@example.com");
		var result2 = sanitizer.SanitizeTag("user.email", "john@example.com");

		// Assert
		result1.ShouldBe(result2);
	}

	[Fact]
	public void HashPhoneNumbers_WhenHashEnabled()
	{
		// Arrange
		var sanitizer = CreateSanitizer(hashDetectedPii: true);

		// Act
		var result = sanitizer.SanitizeTag("info", "Call 555-123-4567");

		// Assert — phone number should be hashed
		result.ShouldNotBeNull();
		result.ShouldContain("sha256:");
		result.ShouldNotContain("555-123-4567");
	}

	[Fact]
	public void HashSsns_WhenHashEnabled()
	{
		// Arrange
		var sanitizer = CreateSanitizer(hashDetectedPii: true);

		// Act
		var result = sanitizer.SanitizeTag("data", "SSN is 123-45-6789");

		// Assert
		result.ShouldNotBeNull();
		result.ShouldNotContain("123-45-6789");
	}

	[Fact]
	public void SanitizePayload_BaselineHashesEntirePayload()
	{
		// Arrange — default baseline hashes all payloads
		var sanitizer = CreateSanitizer();

		// Act
		var result = sanitizer.SanitizePayload("Send to user@example.com and admin@test.org");

		// Assert — baseline sanitizer hashes the entire payload
		result.ShouldStartWith("sha256:");
		result.ShouldNotContain("user@example.com");
	}

	[Fact]
	public void SanitizePayload_DetectsEmails_WhenBaselinePassesThrough()
	{
		// Arrange — IncludeRawPii=true lets raw payload pass to compliance layer
		var sanitizer = CreateSanitizer(includeRawPii: true);

		// Act
		var result = sanitizer.SanitizePayload("Send to user@example.com and admin@test.org");

		// Assert — compliance layer redacts emails in the raw payload
		result.ShouldNotContain("user@example.com");
		result.ShouldNotContain("admin@test.org");
		result.ShouldContain("[REDACTED]");
	}

	[Fact]
	public void SanitizePayload_DetectsPhoneNumbers_WhenBaselinePassesThrough()
	{
		// Arrange
		var sanitizer = CreateSanitizer(includeRawPii: true);

		// Act
		var result = sanitizer.SanitizePayload("Call support at 555-123-4567");

		// Assert
		result.ShouldNotContain("555-123-4567");
	}

	[Fact]
	public void SanitizePayload_DetectsSsns_WhenBaselinePassesThrough()
	{
		// Arrange
		var sanitizer = CreateSanitizer(includeRawPii: true);

		// Act
		var result = sanitizer.SanitizePayload("SSN: 123-45-6789 on record");

		// Assert
		result.ShouldNotContain("123-45-6789");
	}

	[Fact]
	public void SanitizePayload_HashesDetectedPii_WhenHashEnabled()
	{
		// Arrange — both includeRawPii (to get raw text) and hashDetectedPii (to hash matches)
		var sanitizer = CreateSanitizer(hashDetectedPii: true, includeRawPii: true);

		// Act
		var result = sanitizer.SanitizePayload("Contact alice@example.com for details");

		// Assert
		result.ShouldContain("sha256:");
		result.ShouldNotContain("alice@example.com");
	}

	[Fact]
	public void SanitizePayload_PassesThrough_WhenDisabled()
	{
		// Arrange — compliance disabled, baseline passes through with IncludeRawPii
		var sanitizer = CreateSanitizer(enabled: false, includeRawPii: true);

		// Act
		var result = sanitizer.SanitizePayload("test@example.com is here");

		// Assert — compliance detection is off, baseline passes through
		result.ShouldBe("test@example.com is here");
	}

	[Fact]
	public void ApplyCustomPatterns_ToPayload_WhenBaselinePassesThrough()
	{
		// Arrange
		var sanitizer = CreateSanitizer(customPatterns: ["SECRET-\\d+"], includeRawPii: true);

		// Act
		var result = sanitizer.SanitizePayload("Reference SECRET-42 and SECRET-99");

		// Assert
		result.ShouldNotContain("SECRET-42");
		result.ShouldNotContain("SECRET-99");
	}

	[Fact]
	public void ApplyCustomPatterns_InHashMode()
	{
		// Arrange
		var sanitizer = CreateSanitizer(hashDetectedPii: true, customPatterns: ["TOKEN-\\w+"]);

		// Act
		var result = sanitizer.SanitizeTag("ref", "See TOKEN-abc123");

		// Assert
		result.ShouldNotBeNull();
		result.ShouldNotContain("TOKEN-abc123");
		result.ShouldContain("sha256:");
	}

	[Fact]
	public void SanitizeTag_WithEmptyValue_WhenRedactedTagName()
	{
		// Arrange
		var sanitizer = CreateSanitizer();

		// Act — user.email is a redacted tag name with empty value
		var result = sanitizer.SanitizeTag("user.email", "");

		// Assert — empty string gets redacted placeholder
		result.ShouldBe("[REDACTED]");
	}

	[Fact]
	public void SanitizeTag_CaseInsensitiveTagNameRedaction()
	{
		// Arrange
		var options = MsOptions.Create(new ComplianceTelemetrySanitizerOptions
		{
			Enabled = true,
			RedactedTagNames = ["User.Email", "user.ssn"],
		});
		var sanitizerOptions = MsOptions.Create(new TelemetrySanitizerOptions());
		var sanitizer = new ComplianceTelemetrySanitizer(options, sanitizerOptions);

		// Act — case-insensitive match
		var result = sanitizer.SanitizeTag("USER.EMAIL", "test@test.com");

		// Assert
		result.ShouldBe("[REDACTED]");
	}

	[Fact]
	public void PreserveSafePayload_WhenBaselinePassesThrough()
	{
		// Arrange — IncludeRawPii=true so baseline doesn't hash
		var sanitizer = CreateSanitizer(includeRawPii: true);

		// Act
		var result = sanitizer.SanitizePayload("OrderId=12345, Status=Completed");

		// Assert — no PII patterns, so payload preserved
		result.ShouldContain("OrderId=12345");
		result.ShouldContain("Status=Completed");
	}

	[Fact]
	public void DisableEmailDetection()
	{
		// Arrange
		var options = MsOptions.Create(new ComplianceTelemetrySanitizerOptions
		{
			Enabled = true,
			DetectEmails = false,
			DetectPhoneNumbers = true,
			DetectSocialSecurityNumbers = true,
		});
		var sanitizerOptions = MsOptions.Create(new TelemetrySanitizerOptions { IncludeRawPii = true });
		var sanitizer = new ComplianceTelemetrySanitizer(options, sanitizerOptions);

		// Act
		var result = sanitizer.SanitizeTag("info", "Contact user@example.com");

		// Assert — email detection disabled, value passes through
		result.ShouldNotBeNull();
		result.ShouldContain("user@example.com");
	}

	[Fact]
	public void DisablePhoneDetection()
	{
		// Arrange
		var options = MsOptions.Create(new ComplianceTelemetrySanitizerOptions
		{
			Enabled = true,
			DetectEmails = true,
			DetectPhoneNumbers = false,
			DetectSocialSecurityNumbers = true,
		});
		var sanitizerOptions = MsOptions.Create(new TelemetrySanitizerOptions { IncludeRawPii = true });
		var sanitizer = new ComplianceTelemetrySanitizer(options, sanitizerOptions);

		// Act — value with phone number but no email
		var result = sanitizer.SanitizeTag("info", "Call 5551234567 for help");

		// Assert — phone detection disabled, value should contain the phone number
		result.ShouldNotBeNull();
		result.ShouldContain("5551234567");
	}

	[Fact]
	public void DisableSsnDetection()
	{
		// Arrange
		var options = MsOptions.Create(new ComplianceTelemetrySanitizerOptions
		{
			Enabled = true,
			DetectEmails = true,
			DetectPhoneNumbers = true,
			DetectSocialSecurityNumbers = false,
		});
		var sanitizerOptions = MsOptions.Create(new TelemetrySanitizerOptions { IncludeRawPii = true });
		var sanitizer = new ComplianceTelemetrySanitizer(options, sanitizerOptions);

		// Act
		var result = sanitizer.SanitizeTag("data", "SSN 123-45-6789");

		// Assert — SSN detection disabled
		result.ShouldNotBeNull();
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
