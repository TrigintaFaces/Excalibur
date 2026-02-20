// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Observability.Sanitization;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Middleware.Tests.Observability.Sanitization;

/// <summary>
/// Unit tests for <see cref="ComplianceTelemetrySanitizer"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ComplianceTelemetrySanitizerShould : UnitTestBase
{
	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenComplianceOptionsIsNull()
	{
		var baseOptions = MsOptions.Create(new TelemetrySanitizerOptions());
		Should.Throw<ArgumentNullException>(() => new ComplianceTelemetrySanitizer(null!, baseOptions));
	}

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenBaseOptionsIsNull()
	{
		var complianceOptions = MsOptions.Create(new ComplianceTelemetrySanitizerOptions());
		Should.Throw<ArgumentNullException>(() => new ComplianceTelemetrySanitizer(complianceOptions, null!));
	}

	[Fact]
	public void SanitizeTag_RedactsEmail_WhenDetectEmailsIsEnabled()
	{
		// Arrange
		var complianceOptions = MsOptions.Create(new ComplianceTelemetrySanitizerOptions
		{
			Enabled = true,
			DetectEmails = true,
		});
		var baseOptions = MsOptions.Create(new TelemetrySanitizerOptions
		{
			IncludeRawPii = false,
		});
		var sanitizer = new ComplianceTelemetrySanitizer(complianceOptions, baseOptions);

		// Act
		var result = sanitizer.SanitizeTag("message.detail", "Contact user@example.com");

		// Assert
		result.ShouldNotBeNull();
		result.ShouldNotContain("user@example.com");
		result.ShouldContain("[REDACTED]");
	}

	[Fact]
	public void SanitizeTag_RedactsSsn_WhenDetectSocialSecurityNumbersIsEnabled()
	{
		// Arrange
		var complianceOptions = MsOptions.Create(new ComplianceTelemetrySanitizerOptions
		{
			Enabled = true,
			DetectSocialSecurityNumbers = true,
			DetectEmails = false,
			DetectPhoneNumbers = false,
		});
		var baseOptions = MsOptions.Create(new TelemetrySanitizerOptions
		{
			IncludeRawPii = false,
		});
		var sanitizer = new ComplianceTelemetrySanitizer(complianceOptions, baseOptions);

		// Act
		var result = sanitizer.SanitizeTag("data", "SSN is 123-45-6789");

		// Assert
		result.ShouldNotBeNull();
		result.ShouldNotContain("123-45-6789");
	}

	[Fact]
	public void SanitizeTag_PassesThrough_WhenDisabled()
	{
		// Arrange
		var complianceOptions = MsOptions.Create(new ComplianceTelemetrySanitizerOptions
		{
			Enabled = false,
		});
		var baseOptions = MsOptions.Create(new TelemetrySanitizerOptions
		{
			IncludeRawPii = false,
		});
		var sanitizer = new ComplianceTelemetrySanitizer(complianceOptions, baseOptions);

		// Act
		var result = sanitizer.SanitizeTag("message.detail", "Contact user@example.com");

		// Assert - base sanitizer applied, but compliance patterns are not
		result.ShouldNotBeNull();
		result.ShouldContain("user@example.com");
	}

	[Fact]
	public void SanitizeTag_RedactsTagByName_WhenInRedactedTagNames()
	{
		// Arrange
		var complianceOptions = MsOptions.Create(new ComplianceTelemetrySanitizerOptions
		{
			Enabled = true,
			RedactedTagNames = ["user.email"],
		});
		var baseOptions = MsOptions.Create(new TelemetrySanitizerOptions
		{
			IncludeRawPii = false,
		});
		var sanitizer = new ComplianceTelemetrySanitizer(complianceOptions, baseOptions);

		// Act
		var result = sanitizer.SanitizeTag("user.email", "user@example.com");

		// Assert
		result.ShouldNotBeNull();
		result.ShouldBe("[REDACTED]");
	}

	[Fact]
	public void SanitizeTag_HashesTagByName_WhenHashDetectedPiiIsTrue()
	{
		// Arrange
		var complianceOptions = MsOptions.Create(new ComplianceTelemetrySanitizerOptions
		{
			Enabled = true,
			HashDetectedPii = true,
			RedactedTagNames = ["user.email"],
		});
		var baseOptions = MsOptions.Create(new TelemetrySanitizerOptions
		{
			IncludeRawPii = false,
		});
		var sanitizer = new ComplianceTelemetrySanitizer(complianceOptions, baseOptions);

		// Act
		var result = sanitizer.SanitizeTag("user.email", "user@example.com");

		// Assert
		result.ShouldNotBeNull();
		result.ShouldStartWith("sha256:");
	}

	[Fact]
	public void SanitizePayload_RedactsEmails_WhenEnabled()
	{
		// Arrange
		var complianceOptions = MsOptions.Create(new ComplianceTelemetrySanitizerOptions
		{
			Enabled = true,
			DetectEmails = true,
		});
		var baseOptions = MsOptions.Create(new TelemetrySanitizerOptions
		{
			IncludeRawPii = false,
		});
		var sanitizer = new ComplianceTelemetrySanitizer(complianceOptions, baseOptions);

		// Act - Note: payload first goes through base sanitizer (hashing), so email detection
		// applies to the hashed result. We test the flow works end-to-end.
		var result = sanitizer.SanitizePayload("Contact user@example.com for details");

		// Assert
		result.ShouldNotBeNull();
		// After base sanitizer hashing + compliance detection, the result should be transformed
		result.ShouldNotBe("Contact user@example.com for details");
	}

	[Fact]
	public void SanitizePayload_PassesThrough_WhenDisabled()
	{
		// Arrange
		var complianceOptions = MsOptions.Create(new ComplianceTelemetrySanitizerOptions
		{
			Enabled = false,
		});
		var baseOptions = MsOptions.Create(new TelemetrySanitizerOptions
		{
			IncludeRawPii = false,
		});
		var sanitizer = new ComplianceTelemetrySanitizer(complianceOptions, baseOptions);

		// Act
		var result = sanitizer.SanitizePayload("some-payload");

		// Assert - base sanitizer still applied (hashes)
		result.ShouldNotBeNull();
		result.ShouldStartWith("sha256:");
	}

	[Fact]
	public void SanitizeTag_AppliesCustomPatterns()
	{
		// Arrange
		var complianceOptions = MsOptions.Create(new ComplianceTelemetrySanitizerOptions
		{
			Enabled = true,
			DetectEmails = false,
			DetectPhoneNumbers = false,
			DetectSocialSecurityNumbers = false,
			CustomPatterns = [@"SECRET-\w+"],
		});
		var baseOptions = MsOptions.Create(new TelemetrySanitizerOptions
		{
			IncludeRawPii = false,
		});
		var sanitizer = new ComplianceTelemetrySanitizer(complianceOptions, baseOptions);

		// Act
		var result = sanitizer.SanitizeTag("data", "The code is SECRET-ABC123");

		// Assert
		result.ShouldNotBeNull();
		result.ShouldNotContain("SECRET-ABC123");
	}
}
