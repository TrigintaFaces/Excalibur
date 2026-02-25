// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Observability.Sanitization;

namespace Excalibur.Dispatch.Middleware.Tests.Observability.Sanitization;

/// <summary>
/// Unit tests for <see cref="ComplianceTelemetrySanitizerOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ComplianceTelemetrySanitizerOptionsShould : UnitTestBase
{
	[Fact]
	public void Create_WithDefaults_HasExpectedValues()
	{
		// Arrange & Act
		var options = new ComplianceTelemetrySanitizerOptions();

		// Assert
		options.Enabled.ShouldBeTrue();
		options.RedactedPlaceholder.ShouldBe("[REDACTED]");
		options.HashDetectedPii.ShouldBeFalse();
		options.DetectEmails.ShouldBeTrue();
		options.DetectPhoneNumbers.ShouldBeTrue();
		options.DetectSocialSecurityNumbers.ShouldBeTrue();
		options.RedactedTagNames.ShouldNotBeNull();
		options.RedactedTagNames.ShouldNotBeEmpty();
		options.CustomPatterns.ShouldNotBeNull();
		options.CustomPatterns.ShouldBeEmpty();
	}

	[Fact]
	public void RedactedTagNames_ContainsCommonPiiTagNames()
	{
		// Arrange & Act
		var options = new ComplianceTelemetrySanitizerOptions();

		// Assert
		options.RedactedTagNames.ShouldContain("user.email");
		options.RedactedTagNames.ShouldContain("user.phone");
		options.RedactedTagNames.ShouldContain("user.ssn");
		options.RedactedTagNames.ShouldContain("user.ip_address");
		options.RedactedTagNames.ShouldContain("http.client_ip");
		options.RedactedTagNames.ShouldContain("enduser.id");
	}

	[Fact]
	public void Enabled_CanBeDisabled()
	{
		// Arrange
		var options = new ComplianceTelemetrySanitizerOptions { Enabled = false };

		// Assert
		options.Enabled.ShouldBeFalse();
	}

	[Fact]
	public void RedactedPlaceholder_CanBeCustomized()
	{
		// Arrange
		var options = new ComplianceTelemetrySanitizerOptions { RedactedPlaceholder = "***" };

		// Assert
		options.RedactedPlaceholder.ShouldBe("***");
	}

	[Fact]
	public void HashDetectedPii_CanBeEnabled()
	{
		// Arrange
		var options = new ComplianceTelemetrySanitizerOptions { HashDetectedPii = true };

		// Assert
		options.HashDetectedPii.ShouldBeTrue();
	}

	[Fact]
	public void DetectEmails_CanBeDisabled()
	{
		// Arrange
		var options = new ComplianceTelemetrySanitizerOptions { DetectEmails = false };

		// Assert
		options.DetectEmails.ShouldBeFalse();
	}

	[Fact]
	public void CustomPatterns_CanBeAdded()
	{
		// Arrange
		var options = new ComplianceTelemetrySanitizerOptions
		{
			CustomPatterns = [@"\bAPI-KEY-\w+\b", @"\bTOKEN-\d+\b"],
		};

		// Assert
		options.CustomPatterns.Count.ShouldBe(2);
	}
}
