// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text.RegularExpressions;

using Excalibur.Dispatch.Observability.Context;

namespace Excalibur.Dispatch.Observability.Tests.Sanitization;

/// <summary>
/// Tests that <see cref="ContextTracingOptions.SensitiveFieldPatterns"/> default collection
/// includes common PII field patterns.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
[Trait("Feature", "Sanitization")]
public sealed class SensitiveFieldPatternsShould
{
	[Fact]
	public void HaveDefaultPatternsForCommonPiiFields()
	{
		// Arrange
		var options = new ContextTracingOptions();

		// Assert
		options.SensitiveFieldPatterns.ShouldNotBeNull();
		options.SensitiveFieldPatterns.ShouldNotBeEmpty();
	}

	[Theory]
	[InlineData("(?i)password")]
	[InlineData("(?i)secret")]
	[InlineData("(?i)token")]
	[InlineData("(?i)credential")]
	[InlineData("(?i)ssn")]
	[InlineData("(?i)credit.?card")]
	public void ContainExpectedDefaultPattern(string expectedPattern)
	{
		// Arrange
		var options = new ContextTracingOptions();

		// Assert
		options.SensitiveFieldPatterns.ShouldContain(expectedPattern);
	}

	[Fact]
	public void Have6DefaultPatterns()
	{
		// Arrange
		var options = new ContextTracingOptions();

		// Assert
		options.SensitiveFieldPatterns.Length.ShouldBe(6);
	}

	[Theory]
	[InlineData("UserPassword")]
	[InlineData("password_hash")]
	[InlineData("PASSWORD")]
	[InlineData("api_secret")]
	[InlineData("Secret")]
	[InlineData("auth_token")]
	[InlineData("refreshToken")]
	[InlineData("aws_credential")]
	[InlineData("CREDENTIAL")]
	[InlineData("ssn")]
	[InlineData("SSN")]
	[InlineData("creditCard")]
	[InlineData("credit_card")]
	[InlineData("CreditCard")]
	public void MatchCommonPiiFieldNamesWithDefaultPatterns(string fieldName)
	{
		// Arrange
		var options = new ContextTracingOptions();

		// Act
		var isMatched = options.SensitiveFieldPatterns.Any(pattern =>
			Regex.IsMatch(fieldName, pattern, RegexOptions.None, TimeSpan.FromMilliseconds(100)));

		// Assert
		isMatched.ShouldBeTrue($"Field name '{fieldName}' should match at least one sensitive pattern");
	}

	[Theory]
	[InlineData("message.id")]
	[InlineData("correlation.id")]
	[InlineData("timestamp")]
	[InlineData("Status")]
	[InlineData("count")]
	public void NotMatchNonSensitiveFieldNames(string fieldName)
	{
		// Arrange
		var options = new ContextTracingOptions();

		// Act
		var isMatched = options.SensitiveFieldPatterns.Any(pattern =>
			Regex.IsMatch(fieldName, pattern, RegexOptions.None, TimeSpan.FromMilliseconds(100)));

		// Assert
		isMatched.ShouldBeFalse($"Field name '{fieldName}' should NOT match any sensitive pattern");
	}

	[Fact]
	public void AllowOverridingWithCustomPatterns()
	{
		// Arrange
		var options = new ContextTracingOptions
		{
			SensitiveFieldPatterns = ["(?i)custom_pii", "(?i)internal_id"],
		};

		// Assert
		options.SensitiveFieldPatterns.Length.ShouldBe(2);
		options.SensitiveFieldPatterns.ShouldContain("(?i)custom_pii");
		options.SensitiveFieldPatterns.ShouldContain("(?i)internal_id");
	}
}
