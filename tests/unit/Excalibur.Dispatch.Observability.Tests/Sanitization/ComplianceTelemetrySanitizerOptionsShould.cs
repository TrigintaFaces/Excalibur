// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Observability.Sanitization;

namespace Excalibur.Dispatch.Observability.Tests.Sanitization;

/// <summary>
/// Unit tests for <see cref="ComplianceTelemetrySanitizerOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
[Trait("Feature", "Sanitization")]
public sealed class ComplianceTelemetrySanitizerOptionsShould
{
	[Fact]
	public void HaveEnabledDefaultTrue()
	{
		var options = new ComplianceTelemetrySanitizerOptions();
		options.Enabled.ShouldBeTrue();
	}

	[Fact]
	public void HaveDefaultRedactedPlaceholder()
	{
		var options = new ComplianceTelemetrySanitizerOptions();
		options.RedactedPlaceholder.ShouldBe("[REDACTED]");
	}

	[Fact]
	public void HaveHashDetectedPiiDefaultFalse()
	{
		var options = new ComplianceTelemetrySanitizerOptions();
		options.HashDetectedPii.ShouldBeFalse();
	}

	[Fact]
	public void HaveDetectEmailsDefaultTrue()
	{
		var options = new ComplianceTelemetrySanitizerOptions();
		options.DetectEmails.ShouldBeTrue();
	}

	[Fact]
	public void HaveDetectPhoneNumbersDefaultTrue()
	{
		var options = new ComplianceTelemetrySanitizerOptions();
		options.DetectPhoneNumbers.ShouldBeTrue();
	}

	[Fact]
	public void HaveDetectSocialSecurityNumbersDefaultTrue()
	{
		var options = new ComplianceTelemetrySanitizerOptions();
		options.DetectSocialSecurityNumbers.ShouldBeTrue();
	}

	[Fact]
	public void HaveDefaultRedactedTagNames()
	{
		var options = new ComplianceTelemetrySanitizerOptions();
		options.RedactedTagNames.ShouldNotBeEmpty();
		options.RedactedTagNames.ShouldContain("user.email");
		options.RedactedTagNames.ShouldContain("user.phone");
		options.RedactedTagNames.ShouldContain("user.ssn");
		options.RedactedTagNames.ShouldContain("user.ip_address");
		options.RedactedTagNames.ShouldContain("enduser.id");
	}

	[Fact]
	public void HaveEmptyCustomPatternsDefault()
	{
		var options = new ComplianceTelemetrySanitizerOptions();
		options.CustomPatterns.ShouldBeEmpty();
	}
}
