// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Observability.Sanitization;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Observability.Tests.Sanitization;

/// <summary>
/// Functional tests for <see cref="ComplianceTelemetrySanitizer"/> verifying PII detection and compliance sanitization.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
[Trait("Feature", "Sanitization")]
public sealed class ComplianceTelemetrySanitizerFunctionalShould
{
	[Fact]
	public void ThrowOnNullOptions()
	{
		Should.Throw<ArgumentNullException>(() => new ComplianceTelemetrySanitizer(null!, MsOptions.Create(new TelemetrySanitizerOptions())));
	}

	[Fact]
	public void ThrowOnNullSanitizerOptions()
	{
		Should.Throw<ArgumentNullException>(() => new ComplianceTelemetrySanitizer(MsOptions.Create(new ComplianceTelemetrySanitizerOptions()), null!));
	}

	[Fact]
	public void DetectAndRedactEmails()
	{
		var sanitizer = CreateSanitizer();

		var result = sanitizer.SanitizeTag("description", "Contact john@example.com for details");

		result.ShouldNotBeNull();
		result.ShouldNotContain("john@example.com");
		result.ShouldContain("[REDACTED]");
	}

	[Fact]
	public void DetectAndRedactPhoneNumbers()
	{
		var sanitizer = CreateSanitizer();

		var result = sanitizer.SanitizeTag("description", "Call 555-123-4567 for support");

		result.ShouldNotBeNull();
		result.ShouldNotContain("555-123-4567");
		result.ShouldContain("[REDACTED]");
	}

	[Fact]
	public void DetectAndRedactSSNs()
	{
		var sanitizer = CreateSanitizer();

		var result = sanitizer.SanitizeTag("info", "SSN is 123-45-6789");

		result.ShouldNotBeNull();
		result.ShouldNotContain("123-45-6789");
		result.ShouldContain("[REDACTED]");
	}

	[Fact]
	public void HashDetectedPii_WhenHashDetectedPiiEnabled()
	{
		var sanitizer = CreateSanitizer(hashDetectedPii: true);

		var result = sanitizer.SanitizeTag("info", "Email: user@test.com");

		result.ShouldNotBeNull();
		result.ShouldNotContain("user@test.com");
		result.ShouldContain("sha256:");
	}

	[Fact]
	public void RedactConfiguredTagNames()
	{
		var sanitizer = CreateSanitizer();

		var result = sanitizer.SanitizeTag("user.email", "secret@private.com");

		result.ShouldNotBeNull();
		result.ShouldBe("[REDACTED]");
	}

	[Fact]
	public void HashConfiguredTagNames_WhenHashEnabled()
	{
		var sanitizer = CreateSanitizer(hashDetectedPii: true);

		var result = sanitizer.SanitizeTag("user.email", "secret@private.com");

		result.ShouldNotBeNull();
		result.ShouldStartWith("sha256:");
	}

	[Fact]
	public void PassthroughNonPiiValues()
	{
		var sanitizer = CreateSanitizer();

		var result = sanitizer.SanitizeTag("message.type", "OrderCreated");

		result.ShouldBe("OrderCreated");
	}

	[Fact]
	public void NotDetectPii_WhenDisabled()
	{
		var sanitizer = CreateSanitizer(enabled: false);

		// Baseline sanitization still runs, but compliance detection does not
		var result = sanitizer.SanitizeTag("description", "Contact john@example.com");

		// Tag name is not in sensitive/suppressed lists, so baseline passes it through
		result.ShouldBe("Contact john@example.com");
	}

	[Fact]
	public void SanitizePayload_DetectsEmails()
	{
		var sanitizer = CreateSanitizer();

		var result = sanitizer.SanitizePayload("User email is user@test.org and phone 555-123-4567");

		result.ShouldNotContain("user@test.org");
		result.ShouldNotContain("555-123-4567");
	}

	[Fact]
	public void SanitizePayload_PassthroughWhenDisabled()
	{
		var sanitizer = CreateSanitizer(enabled: false);

		// Baseline sanitizer hashes the entire payload
		var result = sanitizer.SanitizePayload("some payload");

		// When compliance is disabled, baseline still runs (it hashes the payload)
		result.ShouldStartWith("sha256:");
	}

	[Fact]
	public void HandleNullRawValue()
	{
		var sanitizer = CreateSanitizer();

		var result = sanitizer.SanitizeTag("any.tag", null);

		result.ShouldBeNull();
	}

	[Fact]
	public void ApplyCustomPatterns()
	{
		var sanitizer = CreateSanitizer(customPatterns: [@"ACCT-\d+"]);

		var result = sanitizer.SanitizeTag("details", "Account ACCT-12345 is active");

		result.ShouldNotBeNull();
		result.ShouldNotContain("ACCT-12345");
		result.ShouldContain("[REDACTED]");
	}

	[Fact]
	public void DisableEmailDetection()
	{
		var opts = new ComplianceTelemetrySanitizerOptions
		{
			Enabled = true,
			DetectEmails = false,
			DetectPhoneNumbers = false,
			DetectSocialSecurityNumbers = false,
			RedactedTagNames = [],
		};

		var sanitizer = new ComplianceTelemetrySanitizer(
			MsOptions.Create(opts),
			MsOptions.Create(new TelemetrySanitizerOptions { SensitiveTagNames = [], SuppressedTagNames = [] }));

		var result = sanitizer.SanitizeTag("info", "Contact john@example.com");

		// No detection enabled, tag name not in lists -> passthrough
		result.ShouldBe("Contact john@example.com");
	}

	private static ComplianceTelemetrySanitizer CreateSanitizer(
		bool enabled = true,
		bool hashDetectedPii = false,
		IList<string>? customPatterns = null)
	{
		var complianceOpts = new ComplianceTelemetrySanitizerOptions
		{
			Enabled = enabled,
			HashDetectedPii = hashDetectedPii,
			DetectEmails = true,
			DetectPhoneNumbers = true,
			DetectSocialSecurityNumbers = true,
			CustomPatterns = customPatterns ?? [],
		};

		var sanitizerOpts = new TelemetrySanitizerOptions
		{
			SensitiveTagNames = [],
			SuppressedTagNames = [],
		};

		return new ComplianceTelemetrySanitizer(
			MsOptions.Create(complianceOpts),
			MsOptions.Create(sanitizerOpts));
	}
}
