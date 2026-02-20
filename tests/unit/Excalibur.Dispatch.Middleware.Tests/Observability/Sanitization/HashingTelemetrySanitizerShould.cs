// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Observability.Sanitization;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Middleware.Tests.Observability.Sanitization;

/// <summary>
/// Unit tests for <see cref="HashingTelemetrySanitizer"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class HashingTelemetrySanitizerShould : UnitTestBase
{
	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenOptionsIsNull()
	{
		Should.Throw<ArgumentNullException>(() => new HashingTelemetrySanitizer(null!));
	}

	[Fact]
	public void SanitizeTag_ReturnsRawValue_WhenIncludeRawPiiIsTrue()
	{
		// Arrange
		var options = MsOptions.Create(new TelemetrySanitizerOptions { IncludeRawPii = true });
		var sanitizer = new HashingTelemetrySanitizer(options);

		// Act
		var result = sanitizer.SanitizeTag("user.id", "john-doe-123");

		// Assert
		result.ShouldBe("john-doe-123");
	}

	[Fact]
	public void SanitizeTag_ReturnsNull_WhenTagIsSuppressed()
	{
		// Arrange
		var options = MsOptions.Create(new TelemetrySanitizerOptions
		{
			IncludeRawPii = false,
			SuppressedTagNames = ["auth.email", "auth.token"],
		});
		var sanitizer = new HashingTelemetrySanitizer(options);

		// Act
		var result = sanitizer.SanitizeTag("auth.email", "user@example.com");

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void SanitizeTag_HashesValue_WhenTagIsSensitive()
	{
		// Arrange
		var options = MsOptions.Create(new TelemetrySanitizerOptions
		{
			IncludeRawPii = false,
			SensitiveTagNames = ["user.id"],
		});
		var sanitizer = new HashingTelemetrySanitizer(options);

		// Act
		var result = sanitizer.SanitizeTag("user.id", "john-doe-123");

		// Assert
		result.ShouldNotBeNull();
		result.ShouldStartWith("sha256:");
		result.Length.ShouldBe(71); // "sha256:" (7) + 64 hex chars
	}

	[Fact]
	public void SanitizeTag_ReturnsUnchanged_WhenTagIsNotSensitiveOrSuppressed()
	{
		// Arrange
		var options = MsOptions.Create(new TelemetrySanitizerOptions
		{
			IncludeRawPii = false,
			SensitiveTagNames = ["user.id"],
			SuppressedTagNames = ["auth.token"],
		});
		var sanitizer = new HashingTelemetrySanitizer(options);

		// Act
		var result = sanitizer.SanitizeTag("message.type", "OrderCreated");

		// Assert
		result.ShouldBe("OrderCreated");
	}

	[Fact]
	public void SanitizeTag_ReturnsNullRawValue_WhenSensitiveButValueIsNull()
	{
		// Arrange
		var options = MsOptions.Create(new TelemetrySanitizerOptions
		{
			IncludeRawPii = false,
			SensitiveTagNames = ["user.id"],
		});
		var sanitizer = new HashingTelemetrySanitizer(options);

		// Act
		var result = sanitizer.SanitizeTag("user.id", null);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void SanitizeTag_IsCaseInsensitive_ForTagNames()
	{
		// Arrange
		var options = MsOptions.Create(new TelemetrySanitizerOptions
		{
			IncludeRawPii = false,
			SensitiveTagNames = ["user.id"],
		});
		var sanitizer = new HashingTelemetrySanitizer(options);

		// Act
		var result = sanitizer.SanitizeTag("USER.ID", "test-value");

		// Assert
		result.ShouldStartWith("sha256:");
	}

	[Fact]
	public void SanitizeTag_ReturnsSameHash_ForSameValue()
	{
		// Arrange
		var options = MsOptions.Create(new TelemetrySanitizerOptions
		{
			IncludeRawPii = false,
			SensitiveTagNames = ["user.id"],
		});
		var sanitizer = new HashingTelemetrySanitizer(options);

		// Act
		var result1 = sanitizer.SanitizeTag("user.id", "same-value");
		var result2 = sanitizer.SanitizeTag("user.id", "same-value");

		// Assert
		result1.ShouldBe(result2);
	}

	[Fact]
	public void SanitizeTag_ReturnsDifferentHash_ForDifferentValues()
	{
		// Arrange
		var options = MsOptions.Create(new TelemetrySanitizerOptions
		{
			IncludeRawPii = false,
			SensitiveTagNames = ["user.id"],
		});
		var sanitizer = new HashingTelemetrySanitizer(options);

		// Act
		var result1 = sanitizer.SanitizeTag("user.id", "value-1");
		var result2 = sanitizer.SanitizeTag("user.id", "value-2");

		// Assert
		result1.ShouldNotBe(result2);
	}

	[Fact]
	public void SanitizePayload_ReturnsRawValue_WhenIncludeRawPiiIsTrue()
	{
		// Arrange
		var options = MsOptions.Create(new TelemetrySanitizerOptions { IncludeRawPii = true });
		var sanitizer = new HashingTelemetrySanitizer(options);

		// Act
		var result = sanitizer.SanitizePayload("sensitive-payload-data");

		// Assert
		result.ShouldBe("sensitive-payload-data");
	}

	[Fact]
	public void SanitizePayload_HashesValue_WhenIncludeRawPiiIsFalse()
	{
		// Arrange
		var options = MsOptions.Create(new TelemetrySanitizerOptions { IncludeRawPii = false });
		var sanitizer = new HashingTelemetrySanitizer(options);

		// Act
		var result = sanitizer.SanitizePayload("sensitive-payload-data");

		// Assert
		result.ShouldStartWith("sha256:");
		result.Length.ShouldBe(71);
	}
}
