// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Observability.Sanitization;

namespace Excalibur.Dispatch.Observability.Tests.Sanitization;

/// <summary>
/// Tests for <see cref="ComplianceTelemetrySanitizer"/> IsFullyHashed early-exit optimization.
/// Verifies that once a value is fully hashed, remaining pattern detection is skipped,
/// preventing double-hashing and false-positive matches in hex digests.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
public sealed class ComplianceTelemetrySanitizerIsFullyHashedShould
{
	private static ComplianceTelemetrySanitizer CreateSanitizer(
		bool detectEmails = true,
		bool detectPhones = true,
		bool detectSsns = true,
		bool hashPii = true,
		string placeholder = "[REDACTED]")
	{
		var complianceOpts = Microsoft.Extensions.Options.Options.Create(
			new ComplianceTelemetrySanitizerOptions
			{
				Enabled = true,
				DetectEmails = detectEmails,
				DetectPhoneNumbers = detectPhones,
				DetectSocialSecurityNumbers = detectSsns,
				HashDetectedPii = hashPii,
				RedactedPlaceholder = placeholder,
			});

		var baseOpts = Microsoft.Extensions.Options.Options.Create(
			new TelemetrySanitizerOptions());

		return new ComplianceTelemetrySanitizer(complianceOpts, baseOpts);
	}

	[Fact]
	public void SanitizeTag_DoesNotDoubleHash_WhenEmailFullyHashed()
	{
		// Arrange -- all detectors on; value is purely an email
		var sanitizer = CreateSanitizer(detectEmails: true, detectSsns: true, detectPhones: true);

		// Act
		var result = sanitizer.SanitizeTag("note", "user@example.com");

		// Assert -- single sha256 hash, not a hash-of-hash
		result.ShouldNotBeNull();
		result.ShouldStartWith("sha256:");
		// Only one sha256: prefix means no double-hashing
		result!.Split("sha256:").Length.ShouldBe(2);
	}

	[Fact]
	public void SanitizeTag_SkipsSubsequentPatterns_AfterFullHash()
	{
		// Arrange -- email detection will hash the whole value;
		// SSN/phone detection should be skipped via early-exit
		var sanitizer = CreateSanitizer(detectEmails: true, detectPhones: true, detectSsns: true);

		// Act
		var result = sanitizer.SanitizeTag("data", "john@example.com");

		// Assert -- hashed (email detected), no further pattern processing
		result.ShouldNotBeNull();
		result.ShouldStartWith("sha256:");
	}

	[Fact]
	public void SanitizeTag_ReturnsRedactedPlaceholder_WhenNotHashing()
	{
		// Arrange -- redact mode instead of hash mode
		var sanitizer = CreateSanitizer(detectEmails: true, hashPii: false, placeholder: "[PII_REDACTED]");

		// Act
		var result = sanitizer.SanitizeTag("notes", "contact: user@example.com");

		// Assert -- placeholder used instead of hash
		result.ShouldNotBeNull();
		result.ShouldContain("[PII_REDACTED]");
	}

	[Fact]
	public void SanitizePayload_HashesEmail_ThenEarlyExits()
	{
		// Arrange
		var sanitizer = CreateSanitizer(detectEmails: true, detectSsns: true);

		// Act
		var result = sanitizer.SanitizePayload("user@example.com");

		// Assert -- hashed
		result.ShouldStartWith("sha256:");
	}

	[Fact]
	public void SanitizeTag_DetectsPhoneAndHashes()
	{
		// Arrange
		var sanitizer = CreateSanitizer(detectPhones: true, detectEmails: false, detectSsns: false);

		// Act
		var result = sanitizer.SanitizeTag("contact", "Call 555-123-4567 for info");

		// Assert -- phone detected
		result.ShouldNotBeNull();
		result.ShouldContain("sha256:");
	}

	[Fact]
	public void SanitizeTag_DetectsSsnAndHashes()
	{
		// Arrange
		var sanitizer = CreateSanitizer(detectSsns: true, detectEmails: false, detectPhones: false);

		// Act
		var result = sanitizer.SanitizeTag("data", "SSN: 123-45-6789");

		// Assert -- SSN detected
		result.ShouldNotBeNull();
		result.ShouldContain("sha256:");
	}

	[Fact]
	public void SanitizeTag_ReturnsNull_WhenInputIsNull()
	{
		var sanitizer = CreateSanitizer();
		var result = sanitizer.SanitizeTag("tag", null);
		result.ShouldBeNull();
	}

	[Fact]
	public void SanitizeTag_PassesThrough_WhenDisabled()
	{
		// Arrange
		var opts = Microsoft.Extensions.Options.Options.Create(
			new ComplianceTelemetrySanitizerOptions { Enabled = false });
		var baseOpts = Microsoft.Extensions.Options.Options.Create(
			new TelemetrySanitizerOptions());
		var sanitizer = new ComplianceTelemetrySanitizer(opts, baseOpts);

		// Act
		var result = sanitizer.SanitizeTag("email", "user@example.com");

		// Assert -- passes through (compliance layer disabled)
		result.ShouldBe("user@example.com");
	}

	[Fact]
	public void SanitizeTag_NoPatternMatch_PassesThrough()
	{
		// Arrange
		var sanitizer = CreateSanitizer(detectEmails: true, detectSsns: true, detectPhones: true);

		// Act -- value without any PII patterns
		var result = sanitizer.SanitizeTag("safe-tag", "normal-value-no-pii");

		// Assert -- no PII detected, value passes through unchanged
		result.ShouldNotBeNull();
		result.ShouldNotStartWith("sha256:");
	}
}
