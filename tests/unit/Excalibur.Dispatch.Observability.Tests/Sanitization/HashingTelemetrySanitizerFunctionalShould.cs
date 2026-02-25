// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Observability.Sanitization;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Observability.Tests.Sanitization;

/// <summary>
/// Functional tests for <see cref="HashingTelemetrySanitizer"/> verifying hashing, suppression, and passthrough.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
[Trait("Feature", "Sanitization")]
public sealed class HashingTelemetrySanitizerFunctionalShould
{
	[Fact]
	public void ThrowOnNullOptions()
	{
		Should.Throw<ArgumentNullException>(() => new HashingTelemetrySanitizer(null!));
	}

	[Fact]
	public void HashSensitiveTagValues()
	{
		var sanitizer = CreateSanitizer(sensitiveTagNames: ["user.id"]);

		var result = sanitizer.SanitizeTag("user.id", "john@example.com");

		result.ShouldNotBeNull();
		result.ShouldStartWith("sha256:");
		result.Length.ShouldBe(71); // "sha256:" + 64 hex chars
	}

	[Fact]
	public void SuppressTagValues()
	{
		var sanitizer = CreateSanitizer(suppressedTagNames: ["auth.token"]);

		var result = sanitizer.SanitizeTag("auth.token", "secret-token-value");

		result.ShouldBeNull();
	}

	[Fact]
	public void PassthroughNonSensitiveTags()
	{
		var sanitizer = CreateSanitizer();

		var result = sanitizer.SanitizeTag("message.type", "OrderCreated");

		result.ShouldBe("OrderCreated");
	}

	[Fact]
	public void ReturnNullForNullValue_OnSensitiveTag()
	{
		var sanitizer = CreateSanitizer(sensitiveTagNames: ["user.id"]);

		var result = sanitizer.SanitizeTag("user.id", null);

		// null rawValue on sensitive tag returns null (nothing to hash)
		result.ShouldBeNull();
	}

	[Fact]
	public void BypassAllSanitization_WhenIncludeRawPiiIsTrue()
	{
		var sanitizer = CreateSanitizer(includeRawPii: true, sensitiveTagNames: ["user.id"]);

		var result = sanitizer.SanitizeTag("user.id", "john@example.com");

		result.ShouldBe("john@example.com");
	}

	[Fact]
	public void HashPayload_WhenNotRawPii()
	{
		var sanitizer = CreateSanitizer();

		var result = sanitizer.SanitizePayload("some sensitive payload data");

		result.ShouldStartWith("sha256:");
		result.Length.ShouldBe(71);
	}

	[Fact]
	public void ReturnRawPayload_WhenIncludeRawPiiIsTrue()
	{
		var sanitizer = CreateSanitizer(includeRawPii: true);

		var result = sanitizer.SanitizePayload("sensitive data");

		result.ShouldBe("sensitive data");
	}

	[Fact]
	public void ProduceDeterministicHashes()
	{
		var sanitizer = CreateSanitizer(sensitiveTagNames: ["user.id"]);

		var hash1 = sanitizer.SanitizeTag("user.id", "same-value");
		var hash2 = sanitizer.SanitizeTag("user.id", "same-value");

		hash1.ShouldBe(hash2);
	}

	[Fact]
	public void ProduceDifferentHashes_ForDifferentValues()
	{
		var sanitizer = CreateSanitizer(sensitiveTagNames: ["user.id"]);

		var hash1 = sanitizer.SanitizeTag("user.id", "value-a");
		var hash2 = sanitizer.SanitizeTag("user.id", "value-b");

		hash1.ShouldNotBe(hash2);
	}

	[Fact]
	public void BeCaseInsensitive_ForTagNames()
	{
		var sanitizer = CreateSanitizer(sensitiveTagNames: ["User.Id"]);

		var result = sanitizer.SanitizeTag("user.id", "test-value");

		result.ShouldStartWith("sha256:");
	}

	[Fact]
	public void HandleDefaultSensitiveTagNames()
	{
		// Use default options which include common PII tag names
		var sanitizer = new HashingTelemetrySanitizer(MsOptions.Create(new TelemetrySanitizerOptions()));

		var result = sanitizer.SanitizeTag("user.id", "test-user");

		result.ShouldStartWith("sha256:");
	}

	[Fact]
	public void HandleDefaultSuppressedTagNames()
	{
		var sanitizer = new HashingTelemetrySanitizer(MsOptions.Create(new TelemetrySanitizerOptions()));

		var result = sanitizer.SanitizeTag("auth.token", "my-secret-token");

		result.ShouldBeNull();
	}

	private static HashingTelemetrySanitizer CreateSanitizer(
		bool includeRawPii = false,
		IList<string>? sensitiveTagNames = null,
		IList<string>? suppressedTagNames = null)
	{
		var opts = new TelemetrySanitizerOptions
		{
			IncludeRawPii = includeRawPii,
			SensitiveTagNames = sensitiveTagNames ?? [],
			SuppressedTagNames = suppressedTagNames ?? [],
		};

		return new HashingTelemetrySanitizer(MsOptions.Create(opts));
	}
}
