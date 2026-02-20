// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Security.Cryptography;
using System.Text;

using Excalibur.Dispatch.Abstractions.Telemetry;
using Excalibur.Dispatch.Observability.Sanitization;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Tests.Observability.Sanitization;

/// <summary>
/// Unit tests for <see cref="ComplianceTelemetrySanitizer"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
[Trait("Feature", "Sanitization")]
public sealed class ComplianceTelemetrySanitizerShould
{
	private static ComplianceTelemetrySanitizer CreateSanitizer(
		Action<ComplianceTelemetrySanitizerOptions>? configureCompliance = null,
		Action<TelemetrySanitizerOptions>? configureBase = null)
	{
		var complianceOptions = new ComplianceTelemetrySanitizerOptions();
		configureCompliance?.Invoke(complianceOptions);

		var baseOptions = new TelemetrySanitizerOptions();
		configureBase?.Invoke(baseOptions);

		return new ComplianceTelemetrySanitizer(
			MsOptions.Create(complianceOptions),
			MsOptions.Create(baseOptions));
	}

	private static string ExpectedHash(string input)
	{
		var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
		return "sha256:" + Convert.ToHexStringLower(bytes);
	}

	#region Email Detection and Redaction

	[Theory]
	[InlineData("user@example.com")]
	[InlineData("alice.bob@domain.co.uk")]
	[InlineData("test+tag@sub.domain.com")]
	[InlineData("user123@test.org")]
	public void SanitizeTag_RedactEmailAddressesInValues(string email)
	{
		// Arrange — use a non-sensitive, non-suppressed tag so inner sanitizer passes through
		var sanitizer = CreateSanitizer();

		// Act
		var result = sanitizer.SanitizeTag("message.body", $"Contact: {email}");

		// Assert
		result.ShouldNotBeNull();
		result.ShouldNotContain(email);
		result.ShouldContain("[REDACTED]");
	}

	[Fact]
	public void SanitizePayload_RedactEmailAddresses()
	{
		// Arrange
		var sanitizer = CreateSanitizer();
		var payload = "Send email to alice@example.com for details";

		// Act — payload gets hashed by inner sanitizer first, so we need a passthrough inner
		// Actually, inner hashes all payloads. The compliance sanitizer applies pattern detection
		// to the already-hashed result. But a sha256 hash won't contain email patterns.
		// So we need to test with IncludeRawPii on the base options to see the compliance layer work.
		var passthroughSanitizer = CreateSanitizer(
			configureBase: o => o.IncludeRawPii = true);
		var result = passthroughSanitizer.SanitizePayload(payload);

		// Assert
		result.ShouldNotContain("alice@example.com");
		result.ShouldContain("[REDACTED]");
	}

	[Fact]
	public void SanitizeTag_HashEmailWhenHashDetectedPiiIsTrue()
	{
		// Arrange
		var sanitizer = CreateSanitizer(o => o.HashDetectedPii = true);

		// Act
		var result = sanitizer.SanitizeTag("description", "Contact alice@example.com");

		// Assert
		result.ShouldNotBeNull();
		result.ShouldNotContain("alice@example.com");
		result.ShouldContain("sha256:");
	}

	[Fact]
	public void SanitizeTag_NotDetectEmailsWhenDetectEmailsIsFalse()
	{
		// Arrange
		var sanitizer = CreateSanitizer(o => o.DetectEmails = false);

		// Act — use a non-sensitive tag
		var result = sanitizer.SanitizeTag("description", "alice@example.com");

		// Assert — email should pass through since detection is disabled
		result.ShouldNotBeNull();
		result.ShouldContain("alice@example.com");
	}

	#endregion

	#region Phone Number Detection and Redaction

	[Theory]
	[InlineData("555-123-4567")]
	[InlineData("(555) 123-4567")]
	[InlineData("+1-555-123-4567")]
	[InlineData("555.123.4567")]
	public void SanitizeTag_RedactPhoneNumbersInValues(string phone)
	{
		// Arrange
		var sanitizer = CreateSanitizer();

		// Act
		var result = sanitizer.SanitizeTag("notes", $"Call {phone} for info");

		// Assert
		result.ShouldNotBeNull();
		result.ShouldNotContain(phone);
		result.ShouldContain("[REDACTED]");
	}

	[Fact]
	public void SanitizeTag_NotDetectPhoneNumbersWhenDetectPhoneNumbersIsFalse()
	{
		// Arrange
		var sanitizer = CreateSanitizer(o => o.DetectPhoneNumbers = false);

		// Act
		var result = sanitizer.SanitizeTag("notes", "Call 555-123-4567");

		// Assert
		result.ShouldNotBeNull();
		result.ShouldContain("555-123-4567");
	}

	[Fact]
	public void SanitizeTag_HashPhoneNumberWhenHashDetectedPiiIsTrue()
	{
		// Arrange
		var sanitizer = CreateSanitizer(o => o.HashDetectedPii = true);

		// Act
		var result = sanitizer.SanitizeTag("notes", "Call 555-123-4567 now");

		// Assert
		result.ShouldNotBeNull();
		result.ShouldNotContain("555-123-4567");
		result.ShouldContain("sha256:");
	}

	#endregion

	#region SSN Detection and Redaction

	[Theory]
	[InlineData("123-45-6789")]
	[InlineData("123 45 6789")]
	[InlineData("123456789")]
	public void SanitizeTag_RedactSsnInValues(string ssn)
	{
		// Arrange
		var sanitizer = CreateSanitizer();

		// Act
		var result = sanitizer.SanitizeTag("notes", $"SSN is {ssn}");

		// Assert
		result.ShouldNotBeNull();
		result.ShouldNotContain(ssn);
		result.ShouldContain("[REDACTED]");
	}

	[Fact]
	public void SanitizeTag_NotDetectSsnWhenDetectSsnIsFalse()
	{
		// Arrange
		var sanitizer = CreateSanitizer(o => o.DetectSocialSecurityNumbers = false);

		// Act
		var result = sanitizer.SanitizeTag("notes", "SSN: 123-45-6789");

		// Assert
		result.ShouldNotBeNull();
		result.ShouldContain("123-45-6789");
	}

	[Fact]
	public void SanitizeTag_HashSsnWhenHashDetectedPiiIsTrue()
	{
		// Arrange
		var sanitizer = CreateSanitizer(o => o.HashDetectedPii = true);

		// Act
		var result = sanitizer.SanitizeTag("notes", "SSN: 123-45-6789");

		// Assert
		result.ShouldNotBeNull();
		result.ShouldNotContain("123-45-6789");
		result.ShouldContain("sha256:");
	}

	#endregion

	#region Hash Mode vs Redaction Mode

	[Fact]
	public void SanitizeTag_UseRedactedPlaceholderByDefault()
	{
		// Arrange — default HashDetectedPii=false
		var sanitizer = CreateSanitizer();

		// Act
		var result = sanitizer.SanitizeTag("message", "alice@example.com");

		// Assert
		result.ShouldBe("[REDACTED]");
	}

	[Fact]
	public void SanitizeTag_UseCustomRedactedPlaceholder()
	{
		// Arrange
		var sanitizer = CreateSanitizer(o => o.RedactedPlaceholder = "***PII***");

		// Act
		var result = sanitizer.SanitizeTag("message", "alice@example.com");

		// Assert
		result.ShouldBe("***PII***");
	}

	[Fact]
	public void SanitizeTag_HashInsteadOfRedactWhenHashDetectedPiiIsTrue()
	{
		// Arrange
		var sanitizer = CreateSanitizer(o => o.HashDetectedPii = true);

		// Act
		var result = sanitizer.SanitizeTag("message", "alice@example.com");

		// Assert
		result.ShouldNotBeNull();
		result.ShouldStartWith("sha256:");
		result.ShouldBe(ExpectedHash("alice@example.com"));
	}

	[Fact]
	public void SanitizeTag_ProduceDeterministicHashesForSamePiiValue()
	{
		// Arrange
		var sanitizer = CreateSanitizer(o => o.HashDetectedPii = true);

		// Act
		var result1 = sanitizer.SanitizeTag("msg1", "alice@example.com");
		var result2 = sanitizer.SanitizeTag("msg2", "alice@example.com");

		// Assert
		result1.ShouldBe(result2);
	}

	#endregion

	#region Custom Patterns

	[Fact]
	public void SanitizeTag_RedactCustomPatternMatches()
	{
		// Arrange — detect credit card-like patterns (simplified)
		var sanitizer = CreateSanitizer(o =>
			o.CustomPatterns = [@"\b\d{4}[\s\-]?\d{4}[\s\-]?\d{4}[\s\-]?\d{4}\b"]);

		// Act
		var result = sanitizer.SanitizeTag("notes", "Card: 4111-1111-1111-1111");

		// Assert
		result.ShouldNotBeNull();
		result.ShouldNotContain("4111-1111-1111-1111");
		result.ShouldContain("[REDACTED]");
	}

	[Fact]
	public void SanitizeTag_HashCustomPatternMatchesWhenHashModeEnabled()
	{
		// Arrange
		var sanitizer = CreateSanitizer(o =>
		{
			o.HashDetectedPii = true;
			o.CustomPatterns = [@"SECRET-\w+"];
		});

		// Act
		var result = sanitizer.SanitizeTag("notes", "Token: SECRET-abc123");

		// Assert
		result.ShouldNotBeNull();
		result.ShouldNotContain("SECRET-abc123");
		result.ShouldContain("sha256:");
	}

	[Fact]
	public void SanitizeTag_ApplyMultipleCustomPatterns()
	{
		// Arrange
		var sanitizer = CreateSanitizer(o =>
			o.CustomPatterns = [@"CUSTOM1-\d+", @"CUSTOM2-\w+"]);

		// Act
		var result = sanitizer.SanitizeTag("notes", "Data: CUSTOM1-123 and CUSTOM2-abc");

		// Assert
		result.ShouldNotBeNull();
		result.ShouldNotContain("CUSTOM1-123");
		result.ShouldNotContain("CUSTOM2-abc");
	}

	[Fact]
	public void SanitizePayload_ApplyCustomPatternsWithBasePassthrough()
	{
		// Arrange — IncludeRawPii on base so payload reaches compliance layer
		var sanitizer = CreateSanitizer(
			configureCompliance: o => o.CustomPatterns = [@"SECRET-\w+"],
			configureBase: o => o.IncludeRawPii = true);

		// Act
		var result = sanitizer.SanitizePayload("Token: SECRET-abc123 found");

		// Assert
		result.ShouldNotContain("SECRET-abc123");
		result.ShouldContain("[REDACTED]");
	}

	#endregion

	#region Tag Name-Based Redaction

	[Theory]
	[InlineData("user.email")]
	[InlineData("user.phone")]
	[InlineData("user.ssn")]
	[InlineData("user.ip_address")]
	[InlineData("user.address")]
	[InlineData("http.client_ip")]
	[InlineData("enduser.id")]
	[InlineData("enduser.email")]
	public void SanitizeTag_RedactDefaultRedactedTagNames(string tagName)
	{
		// Arrange
		var sanitizer = CreateSanitizer();

		// Act
		var result = sanitizer.SanitizeTag(tagName, "sensitive-value");

		// Assert
		result.ShouldNotBeNull();
		result.ShouldBe("[REDACTED]");
	}

	[Fact]
	public void SanitizeTag_HashRedactedTagNameWhenHashDetectedPiiIsTrue()
	{
		// Arrange
		var sanitizer = CreateSanitizer(o => o.HashDetectedPii = true);

		// Act
		var result = sanitizer.SanitizeTag("user.email", "alice@example.com");

		// Assert — tag-name redaction hashes rawValue, not the base-sanitized value
		result.ShouldNotBeNull();
		result.ShouldStartWith("sha256:");
		result.ShouldBe(ExpectedHash("alice@example.com"));
	}

	[Fact]
	public void SanitizeTag_BeCaseInsensitiveForRedactedTagNames()
	{
		// Arrange
		var sanitizer = CreateSanitizer();

		// Act
		var lower = sanitizer.SanitizeTag("user.email", "alice@test.com");
		var upper = sanitizer.SanitizeTag("USER.EMAIL", "alice@test.com");
		var mixed = sanitizer.SanitizeTag("User.Email", "alice@test.com");

		// Assert
		lower.ShouldBe("[REDACTED]");
		upper.ShouldBe("[REDACTED]");
		mixed.ShouldBe("[REDACTED]");
	}

	[Fact]
	public void SanitizeTag_RedactCustomRedactedTagNames()
	{
		// Arrange
		var sanitizer = CreateSanitizer(o =>
			o.RedactedTagNames = ["custom.pii", "custom.secret"]);

		// Act
		var result1 = sanitizer.SanitizeTag("custom.pii", "secret-data");
		var result2 = sanitizer.SanitizeTag("custom.secret", "another-secret");

		// Assert
		result1.ShouldBe("[REDACTED]");
		result2.ShouldBe("[REDACTED]");
	}

	[Fact]
	public void SanitizeTag_ReturnNullForRedactedTagNameWhenRawValueIsNull()
	{
		// Arrange — "user.email" is a redacted tag name but null rawValue causes early return
		// because the inner sanitizer passes through null for non-sensitive tags, so
		// baseResult is null, and the compliance sanitizer's early-return check
		// (if baseResult is null) triggers before the redacted tag name check.
		var sanitizer = CreateSanitizer(o => o.HashDetectedPii = true);

		// Act
		var result = sanitizer.SanitizeTag("user.email", null);

		// Assert
		result.ShouldBeNull();
	}

	#endregion

	#region Disabled Mode (Passthrough)

	[Fact]
	public void SanitizeTag_PassthroughWhenDisabled()
	{
		// Arrange — compliance disabled, but base sanitizer still runs
		var sanitizer = CreateSanitizer(o => o.Enabled = false);

		// Act — non-sensitive tag with email in value
		var result = sanitizer.SanitizeTag("notes", "alice@example.com");

		// Assert — compliance layer is skipped, only base sanitizer applies (passthrough for non-sensitive)
		result.ShouldBe("alice@example.com");
	}

	[Fact]
	public void SanitizePayload_DelegateToInnerOnlyWhenDisabled()
	{
		// Arrange
		var sanitizer = CreateSanitizer(o => o.Enabled = false);

		// Act — payload always gets hashed by inner sanitizer
		var result = sanitizer.SanitizePayload("alice@example.com is PII");

		// Assert — inner sanitizer hashes the entire payload
		result.ShouldStartWith("sha256:");
	}

	[Fact]
	public void SanitizeTag_StillApplyBaseSanitizationWhenDisabled()
	{
		// Arrange — compliance disabled, base sanitizer still hashes sensitive tags
		var sanitizer = CreateSanitizer(o => o.Enabled = false);

		// Act — user.id is sensitive in the base sanitizer
		var result = sanitizer.SanitizeTag("user.id", "alice");

		// Assert — base sanitizer still hashes this
		result.ShouldNotBeNull();
		result.ShouldStartWith("sha256:");
		result.ShouldBe(ExpectedHash("alice"));
	}

	[Fact]
	public void SanitizeTag_StillSuppressBaseSuppressedTagsWhenDisabled()
	{
		// Arrange — compliance disabled
		var sanitizer = CreateSanitizer(o => o.Enabled = false);

		// Act — auth.email is suppressed in the base sanitizer
		var result = sanitizer.SanitizeTag("auth.email", "alice@example.com");

		// Assert — base sanitizer suppresses this
		result.ShouldBeNull();
	}

	#endregion

	#region DI Registration

	[Fact]
	public void DiRegistration_RegisterComplianceSanitizerAsITelemetrySanitizer()
	{
		// Arrange
		var services = new ServiceCollection();
		// Register base options first (normally done by AddDispatchObservability)
		_ = services.AddOptions<TelemetrySanitizerOptions>()
			.ValidateDataAnnotations()
			.ValidateOnStart();
		services.AddComplianceTelemetrySanitizer();

		using var provider = services.BuildServiceProvider();

		// Act
		var sanitizer = provider.GetService<ITelemetrySanitizer>();

		// Assert
		sanitizer.ShouldNotBeNull();
		sanitizer.ShouldBeOfType<ComplianceTelemetrySanitizer>();
	}

	[Fact]
	public void DiRegistration_ReplaceDefaultHashingSanitizer()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddOptions<TelemetrySanitizerOptions>()
			.ValidateDataAnnotations()
			.ValidateOnStart();
		services.TryAddSingleton<ITelemetrySanitizer, HashingTelemetrySanitizer>();
		services.AddComplianceTelemetrySanitizer();

		using var provider = services.BuildServiceProvider();

		// Act
		var sanitizer = provider.GetRequiredService<ITelemetrySanitizer>();

		// Assert — compliance sanitizer replaces hashing sanitizer
		sanitizer.ShouldBeOfType<ComplianceTelemetrySanitizer>();
	}

	[Fact]
	public void DiRegistration_RegisterAsSingleton()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddComplianceTelemetrySanitizer();

		using var provider = services.BuildServiceProvider();

		// Act
		var first = provider.GetRequiredService<ITelemetrySanitizer>();
		var second = provider.GetRequiredService<ITelemetrySanitizer>();

		// Assert
		first.ShouldBeSameAs(second);
	}

	[Fact]
	public void DiRegistration_ApplyCustomOptionsConfiguration()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddComplianceTelemetrySanitizer(o =>
		{
			o.RedactedPlaceholder = "***";
			o.DetectEmails = false;
		});

		using var provider = services.BuildServiceProvider();
		var sanitizer = provider.GetRequiredService<ITelemetrySanitizer>();

		// Act — email should pass through since detection is disabled
		var result = sanitizer.SanitizeTag("notes", "alice@example.com");

		// Assert
		result.ShouldBe("alice@example.com");
	}

	[Fact]
	public void DiRegistration_RegisterOptionsWithValidateOnStart()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddComplianceTelemetrySanitizer();

		using var provider = services.BuildServiceProvider();

		// Act
		var options = provider.GetService<IOptions<ComplianceTelemetrySanitizerOptions>>();

		// Assert
		options.ShouldNotBeNull();
		options.Value.ShouldNotBeNull();
		options.Value.Enabled.ShouldBeTrue();
	}

	#endregion

	#region Bounded Hash Cache Behavior

	[Fact]
	public void StillReturnCorrectHashWhenCacheIsFull()
	{
		// Arrange — fill cache with 1024+ unique hashed values
		var sanitizer = CreateSanitizer(o => o.HashDetectedPii = true);

		// Hash 1025 unique email-like values to fill the compliance sanitizer's cache
		for (var i = 0; i < 1025; i++)
		{
			sanitizer.SanitizeTag("notes", $"user{i}@example.com");
		}

		// Act — new value beyond cache limit
		var result = sanitizer.SanitizeTag("notes", "beyond-cache@example.com");

		// Assert
		result.ShouldNotBeNull();
		result.ShouldBe(ExpectedHash("beyond-cache@example.com"));
	}

	[Fact]
	public void ReturnCachedValueForRepeatedHashedInput()
	{
		// Arrange
		var sanitizer = CreateSanitizer(o => o.HashDetectedPii = true);

		// Act — first call computes, second returns from cache
		var first = sanitizer.SanitizeTag("notes", "repeat@example.com");
		var second = sanitizer.SanitizeTag("notes", "repeat@example.com");

		// Assert
		first.ShouldBe(second);
		first.ShouldBe(ExpectedHash("repeat@example.com"));
	}

	[Fact]
	public void ReturnCorrectHashForRedactedTagNameBeyondCacheLimit()
	{
		// Arrange — hash mode for tag-name redaction
		var sanitizer = CreateSanitizer(o => o.HashDetectedPii = true);

		// Fill cache via tag-name redaction
		for (var i = 0; i < 1025; i++)
		{
			sanitizer.SanitizeTag("user.email", $"user{i}@example.com");
		}

		// Act
		var result = sanitizer.SanitizeTag("user.email", "overflow@example.com");

		// Assert
		result.ShouldBe(ExpectedHash("overflow@example.com"));
	}

	#endregion

	#region Null/Empty Input Handling

	[Fact]
	public void SanitizeTag_ReturnNullWhenRawValueIsNull()
	{
		// Arrange
		var sanitizer = CreateSanitizer();

		// Act — non-sensitive, non-suppressed tag with null value
		var result = sanitizer.SanitizeTag("unknown.tag", null);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void SanitizeTag_HandleEmptyStringValue()
	{
		// Arrange
		var sanitizer = CreateSanitizer();

		// Act — empty string has no PII patterns
		var result = sanitizer.SanitizeTag("unknown.tag", "");

		// Assert — passthrough since no pattern matches
		result.ShouldBe("");
	}

	[Fact]
	public void SanitizeTag_RedactEmptyValueForRedactedTagName()
	{
		// Arrange
		var sanitizer = CreateSanitizer();

		// Act — user.email is a redacted tag name; inner sanitizer passes through empty string
		var result = sanitizer.SanitizeTag("user.email", "");

		// Assert
		result.ShouldBe("[REDACTED]");
	}

	[Fact]
	public void SanitizePayload_HandleEmptyString()
	{
		// Arrange — with base IncludeRawPii to test compliance layer on empty
		var sanitizer = CreateSanitizer(configureBase: o => o.IncludeRawPii = true);

		// Act
		var result = sanitizer.SanitizePayload("");

		// Assert
		result.ShouldBe("");
	}

	#endregion

	#region Constructor Validation

	[Fact]
	public void ThrowArgumentNullExceptionWhenOptionsIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new ComplianceTelemetrySanitizer(null!, MsOptions.Create(new TelemetrySanitizerOptions())));
	}

	[Fact]
	public void ThrowArgumentNullExceptionWhenSanitizerOptionsIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new ComplianceTelemetrySanitizer(
				MsOptions.Create(new ComplianceTelemetrySanitizerOptions()), null!));
	}

	#endregion

	#region Inner Sanitizer Interaction

	[Fact]
	public void SanitizeTag_ApplyInnerSanitizationBeforeComplianceRules()
	{
		// Arrange — auth.token is suppressed by the inner sanitizer
		var sanitizer = CreateSanitizer();

		// Act — inner sanitizer suppresses auth.token (returns null), compliance skips
		var result = sanitizer.SanitizeTag("auth.token", "my-secret-token");

		// Assert — suppressed by inner sanitizer
		result.ShouldBeNull();
	}

	[Fact]
	public void SanitizeTag_ApplyPatternDetectionAfterBaselineSanitization()
	{
		// Arrange — a non-sensitive tag with email that passes through the inner sanitizer
		var sanitizer = CreateSanitizer();

		// Act — inner sanitizer passes through non-sensitive tag, then compliance detects email
		var result = sanitizer.SanitizeTag("description", "Contact alice@example.com please");

		// Assert
		result.ShouldNotBeNull();
		result.ShouldNotContain("alice@example.com");
		result.ShouldBe("Contact [REDACTED] please");
	}

	[Fact]
	public void SanitizePayload_InnerSanitizerHashesThenComplianceApplies()
	{
		// Arrange — default base sanitizer hashes all payloads
		var sanitizer = CreateSanitizer();

		// Act — inner sanitizer hashes the payload first;
		// the sha256 output won't match email/phone/SSN patterns
		var result = sanitizer.SanitizePayload("alice@example.com");

		// Assert — result should be a hash (from inner), not the redacted placeholder
		result.ShouldStartWith("sha256:");
	}

	#endregion

	#region Multiple PII Patterns in Same Value

	[Fact]
	public void SanitizeTag_RedactMultiplePiiTypesInSameValue()
	{
		// Arrange
		var sanitizer = CreateSanitizer();

		// Act — value contains both email and phone
		var result = sanitizer.SanitizeTag("notes",
			"Contact alice@example.com or call 555-123-4567");

		// Assert
		result.ShouldNotBeNull();
		result.ShouldNotContain("alice@example.com");
		result.ShouldNotContain("555-123-4567");
	}

	[Fact]
	public void SanitizeTag_RedactMultipleEmailsInSameValue()
	{
		// Arrange
		var sanitizer = CreateSanitizer();

		// Act
		var result = sanitizer.SanitizeTag("notes",
			"CC: alice@example.com and bob@example.com");

		// Assert
		result.ShouldNotBeNull();
		result.ShouldNotContain("alice@example.com");
		result.ShouldNotContain("bob@example.com");
	}

	#endregion

	#region Thread Safety

	[Fact]
	public void SanitizeTag_ProduceCorrectResultsUnderConcurrency()
	{
		// Arrange — use redaction mode (not hash mode) to avoid hash-on-hash cascading
		var sanitizer = CreateSanitizer();
		var inputs = Enumerable.Range(0, 100).Select(i => $"user{i}@example.com").ToArray();
		var results = new string?[inputs.Length];

		// Act
		Parallel.For(0, inputs.Length, i =>
		{
			results[i] = sanitizer.SanitizeTag("notes", inputs[i]);
		});

		// Assert — each email should be redacted
		for (var i = 0; i < inputs.Length; i++)
		{
			results[i].ShouldNotBeNull();
			results[i].ShouldNotContain(inputs[i]);
			results[i].ShouldBe("[REDACTED]");
		}
	}

	[Fact]
	public void SanitizeTag_ProduceDeterministicResultsUnderConcurrency()
	{
		// Arrange — hash mode, verify determinism by running same inputs twice
		var sanitizer = CreateSanitizer(o => o.HashDetectedPii = true);
		var inputs = Enumerable.Range(0, 100).Select(i => $"user{i}@example.com").ToArray();
		var results1 = new string?[inputs.Length];
		var results2 = new string?[inputs.Length];

		// Act — two concurrent passes
		Parallel.For(0, inputs.Length, i =>
		{
			results1[i] = sanitizer.SanitizeTag("notes", inputs[i]);
		});

		Parallel.For(0, inputs.Length, i =>
		{
			results2[i] = sanitizer.SanitizeTag("notes", inputs[i]);
		});

		// Assert — same input always produces same output
		for (var i = 0; i < inputs.Length; i++)
		{
			results1[i].ShouldNotBeNull();
			results1[i].ShouldNotContain(inputs[i]);
			results1[i].ShouldBe(results2[i]);
		}
	}

	[Fact]
	public void SanitizeTag_HandleConcurrentCacheOverflow()
	{
		// Arrange — concurrent writes that exceed cache capacity, use redaction mode
		var sanitizer = CreateSanitizer();
		var uniqueCount = 2000;
		var results = new string?[uniqueCount];

		// Act
		Parallel.For(0, uniqueCount, i =>
		{
			results[i] = sanitizer.SanitizeTag("notes", $"user{i}@example.com");
		});

		// Assert — all results should be redacted regardless of cache state
		for (var i = 0; i < uniqueCount; i++)
		{
			results[i].ShouldNotBeNull();
			results[i].ShouldNotContain($"user{i}@example.com");
			results[i].ShouldBe("[REDACTED]");
		}
	}

	#endregion

	#region Edge Cases

	[Fact]
	public void SanitizeTag_HandleValueWithNoPiiPatterns()
	{
		// Arrange
		var sanitizer = CreateSanitizer();

		// Act
		var result = sanitizer.SanitizeTag("status", "healthy");

		// Assert — no PII detected, passthrough
		result.ShouldBe("healthy");
	}

	[Fact]
	public void SanitizeTag_HandleUnicodeContent()
	{
		// Arrange
		var sanitizer = CreateSanitizer();
		var unicode = "\u00e9\u00e8\u00ea\u00eb\u2603\ud83d\ude00";

		// Act
		var result = sanitizer.SanitizeTag("notes", unicode);

		// Assert — no PII patterns in unicode, passthrough
		result.ShouldBe(unicode);
	}

	[Fact]
	public void SanitizeTag_HandleVeryLongInput()
	{
		// Arrange
		var sanitizer = CreateSanitizer();
		var longValue = new string('x', 10000) + "alice@example.com" + new string('y', 10000);

		// Act
		var result = sanitizer.SanitizeTag("notes", longValue);

		// Assert — email should still be detected and redacted
		result.ShouldNotBeNull();
		result.ShouldNotContain("alice@example.com");
		result.ShouldContain("[REDACTED]");
	}

	[Fact]
	public void SanitizeTag_NotMatchPartialEmailPatterns()
	{
		// Arrange
		var sanitizer = CreateSanitizer();

		// Act — not a valid email (no TLD)
		var result = sanitizer.SanitizeTag("notes", "user@");

		// Assert — should not be redacted
		result.ShouldBe("user@");
	}

	[Fact]
	public void SanitizeTag_RedactedTagNameTakesPriorityOverPatternDetection()
	{
		// Arrange — user.email is both a redacted tag name AND the value might contain patterns
		var sanitizer = CreateSanitizer();

		// Act
		var result = sanitizer.SanitizeTag("user.email", "not-an-email");

		// Assert — redacted because of tag name, not pattern
		result.ShouldBe("[REDACTED]");
	}

	#endregion
}
