// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Security.Cryptography;
using System.Text;

using Excalibur.Dispatch.Observability.Sanitization;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Observability.Tests.Sanitization;

/// <summary>
/// Unit tests for <see cref="HashingTelemetrySanitizer"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
[Trait("Feature", "Sanitization")]
public sealed class HashingTelemetrySanitizerShould
{
	private static HashingTelemetrySanitizer CreateSanitizer(Action<TelemetrySanitizerOptions>? configure = null)
	{
		var options = new TelemetrySanitizerOptions();
		configure?.Invoke(options);
		return new HashingTelemetrySanitizer(MsOptions.Create(options));
	}

	private static string ExpectedHash(string input)
	{
		var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
		return "sha256:" + Convert.ToHexStringLower(bytes);
	}

	#region SanitizeTag — Sensitive Tag Hashing

	[Fact]
	public void SanitizeTag_ReturnSha256HashForSensitiveTag()
	{
		// Arrange
		var sanitizer = CreateSanitizer();

		// Act
		var result = sanitizer.SanitizeTag("user.id", "alice@example.com");

		// Assert
		result.ShouldNotBeNull();
		result.ShouldStartWith("sha256:");
		result.ShouldBe(ExpectedHash("alice@example.com"));
	}

	[Theory]
	[InlineData("user.id")]
	[InlineData("user.name")]
	[InlineData("auth.user_id")]
	[InlineData("auth.subject_id")]
	[InlineData("auth.identity_name")]
	[InlineData("auth.tenant_id")]
	[InlineData("audit.user_id")]
	[InlineData("tenant.id")]
	[InlineData("tenant.name")]
	[InlineData("dispatch.messaging.tenant_id")]
	public void SanitizeTag_HashAllDefaultSensitiveTagNames(string tagName)
	{
		// Arrange
		var sanitizer = CreateSanitizer();
		var rawValue = "test-value";

		// Act
		var result = sanitizer.SanitizeTag(tagName, rawValue);

		// Assert
		result.ShouldBe(ExpectedHash(rawValue));
	}

	[Fact]
	public void SanitizeTag_BeCaseInsensitiveForSensitiveTagNames()
	{
		// Arrange
		var sanitizer = CreateSanitizer();

		// Act
		var lower = sanitizer.SanitizeTag("user.id", "alice");
		var upper = sanitizer.SanitizeTag("USER.ID", "alice");
		var mixed = sanitizer.SanitizeTag("User.Id", "alice");

		// Assert
		lower.ShouldBe(upper);
		lower.ShouldBe(mixed);
		lower.ShouldBe(ExpectedHash("alice"));
	}

	[Fact]
	public void SanitizeTag_ReturnNullForSensitiveTagWithNullValue()
	{
		// Arrange
		var sanitizer = CreateSanitizer();

		// Act
		var result = sanitizer.SanitizeTag("user.id", null);

		// Assert — null input returns null, not a hash
		result.ShouldBeNull();
	}

	#endregion

	#region SanitizeTag — Suppressed Tags

	[Theory]
	[InlineData("auth.email")]
	[InlineData("auth.token")]
	public void SanitizeTag_ReturnNullForDefaultSuppressedTags(string tagName)
	{
		// Arrange
		var sanitizer = CreateSanitizer();

		// Act
		var result = sanitizer.SanitizeTag(tagName, "some-value");

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void SanitizeTag_BeCaseInsensitiveForSuppressedTagNames()
	{
		// Arrange
		var sanitizer = CreateSanitizer();

		// Act & Assert
		sanitizer.SanitizeTag("AUTH.EMAIL", "test@test.com").ShouldBeNull();
		sanitizer.SanitizeTag("Auth.Email", "test@test.com").ShouldBeNull();
	}

	[Fact]
	public void SanitizeTag_ReturnNullForSuppressedTagEvenIfValueIsNull()
	{
		// Arrange
		var sanitizer = CreateSanitizer();

		// Act — suppressed wins before null check on value
		var result = sanitizer.SanitizeTag("auth.email", null);

		// Assert
		result.ShouldBeNull();
	}

	#endregion

	#region SanitizeTag — Passthrough (Non-Sensitive, Non-Suppressed)

	[Theory]
	[InlineData("message.id", "msg-123")]
	[InlineData("correlation.id", "corr-456")]
	[InlineData("unknown.tag", "any-value")]
	public void SanitizeTag_PassthroughNonSensitiveNonSuppressedTags(string tagName, string rawValue)
	{
		// Arrange
		var sanitizer = CreateSanitizer();

		// Act
		var result = sanitizer.SanitizeTag(tagName, rawValue);

		// Assert
		result.ShouldBe(rawValue);
	}

	[Fact]
	public void SanitizeTag_PassthroughNullValueForNonSensitiveTag()
	{
		// Arrange
		var sanitizer = CreateSanitizer();

		// Act
		var result = sanitizer.SanitizeTag("unknown.tag", null);

		// Assert
		result.ShouldBeNull();
	}

	#endregion

	#region SanitizePayload

	[Fact]
	public void SanitizePayload_ReturnSha256HashFormat()
	{
		// Arrange
		var sanitizer = CreateSanitizer();
		var payload = "{\"userId\":\"alice\",\"action\":\"login\"}";

		// Act
		var result = sanitizer.SanitizePayload(payload);

		// Assert
		result.ShouldStartWith("sha256:");
		result.ShouldBe(ExpectedHash(payload));
	}

	[Fact]
	public void SanitizePayload_ProduceLowercaseHex()
	{
		// Arrange
		var sanitizer = CreateSanitizer();

		// Act
		var result = sanitizer.SanitizePayload("test-payload");

		// Assert — hex portion must be all lowercase
		// Assert — hex portion must be all lowercase (regex validates charset)
		result["sha256:".Length..].ShouldMatch("^[0-9a-f]{64}$");
	}

	[Fact]
	public void SanitizePayload_ProduceExactly71Characters()
	{
		// Arrange — sha256: (7 chars) + 64 hex chars = 71
		var sanitizer = CreateSanitizer();

		// Act
		var result = sanitizer.SanitizePayload("anything");

		// Assert
		result.Length.ShouldBe(71);
	}

	#endregion

	#region Determinism

	[Fact]
	public void SanitizeTag_ProduceSameHashForSameInput()
	{
		// Arrange
		var sanitizer = CreateSanitizer();

		// Act
		var first = sanitizer.SanitizeTag("user.id", "alice");
		var second = sanitizer.SanitizeTag("user.id", "alice");

		// Assert
		first.ShouldBe(second);
	}

	[Fact]
	public void SanitizePayload_ProduceSameHashForSameInput()
	{
		// Arrange
		var sanitizer = CreateSanitizer();

		// Act
		var first = sanitizer.SanitizePayload("same-payload");
		var second = sanitizer.SanitizePayload("same-payload");

		// Assert
		first.ShouldBe(second);
	}

	[Fact]
	public void SanitizeTag_ProduceDifferentHashesForDifferentInputs()
	{
		// Arrange
		var sanitizer = CreateSanitizer();

		// Act
		var hash1 = sanitizer.SanitizeTag("user.id", "alice");
		var hash2 = sanitizer.SanitizeTag("user.id", "bob");

		// Assert
		hash1.ShouldNotBe(hash2);
	}

	[Fact]
	public void SanitizePayload_ProduceDifferentHashesForDifferentInputs()
	{
		// Arrange
		var sanitizer = CreateSanitizer();

		// Act
		var hash1 = sanitizer.SanitizePayload("payload-a");
		var hash2 = sanitizer.SanitizePayload("payload-b");

		// Assert
		hash1.ShouldNotBe(hash2);
	}

	[Fact]
	public void ProduceConsistentHashAcrossInstances()
	{
		// Arrange
		var sanitizer1 = CreateSanitizer();
		var sanitizer2 = CreateSanitizer();

		// Act
		var hash1 = sanitizer1.SanitizeTag("user.id", "alice");
		var hash2 = sanitizer2.SanitizeTag("user.id", "alice");

		// Assert
		hash1.ShouldBe(hash2);
	}

	#endregion

	#region Bounded Cache

	[Fact]
	public void StillReturnCorrectHashWhenCacheIsFull()
	{
		// Arrange — fill cache with 1024 unique values, then hash one more
		var sanitizer = CreateSanitizer();

		for (var i = 0; i < 1025; i++)
		{
			sanitizer.SanitizePayload($"fill-cache-{i}");
		}

		// Act — 1025th unique value should still produce correct hash
		var result = sanitizer.SanitizePayload("beyond-cache-limit");

		// Assert
		result.ShouldBe(ExpectedHash("beyond-cache-limit"));
	}

	[Fact]
	public void ReturnCachedValueForRepeatedInput()
	{
		// Arrange
		var sanitizer = CreateSanitizer();

		// Act — first call computes, second returns from cache
		var first = sanitizer.SanitizePayload("cached-value");
		var second = sanitizer.SanitizePayload("cached-value");

		// Assert
		first.ShouldBe(second);
		first.ShouldBe(ExpectedHash("cached-value"));
	}

	#endregion

	#region Thread Safety

	[Fact]
	public void SanitizeTag_ProduceCorrectResultsUnderConcurrency()
	{
		// Arrange
		var sanitizer = CreateSanitizer();
		var inputs = Enumerable.Range(0, 100).Select(i => $"user-{i}").ToArray();
		var results = new string?[inputs.Length];

		// Act
		Parallel.For(0, inputs.Length, i =>
		{
			results[i] = sanitizer.SanitizeTag("user.id", inputs[i]);
		});

		// Assert
		for (var i = 0; i < inputs.Length; i++)
		{
			results[i].ShouldBe(ExpectedHash(inputs[i]));
		}
	}

	[Fact]
	public void SanitizePayload_ProduceCorrectResultsUnderConcurrency()
	{
		// Arrange
		var sanitizer = CreateSanitizer();
		var inputs = Enumerable.Range(0, 100).Select(i => $"payload-{i}").ToArray();
		var results = new string[inputs.Length];

		// Act
		Parallel.For(0, inputs.Length, i =>
		{
			results[i] = sanitizer.SanitizePayload(inputs[i]);
		});

		// Assert
		for (var i = 0; i < inputs.Length; i++)
		{
			results[i].ShouldBe(ExpectedHash(inputs[i]));
		}
	}

	[Fact]
	public void HandleConcurrentCacheEvictionCorrectly()
	{
		// Arrange — concurrent writes that exceed cache capacity
		var sanitizer = CreateSanitizer();
		var uniqueCount = 2000;
		var results = new string[uniqueCount];

		// Act
		Parallel.For(0, uniqueCount, i =>
		{
			results[i] = sanitizer.SanitizePayload($"concurrent-{i}");
		});

		// Assert — all results should be correct regardless of cache state
		for (var i = 0; i < uniqueCount; i++)
		{
			results[i].ShouldBe(ExpectedHash($"concurrent-{i}"));
		}
	}

	#endregion

	#region Custom Configuration

	[Fact]
	public void SanitizeTag_HashCustomSensitiveTagNames()
	{
		// Arrange
		var sanitizer = CreateSanitizer(o => o.SensitiveTagNames = ["custom.pii"]);

		// Act
		var result = sanitizer.SanitizeTag("custom.pii", "secret-data");

		// Assert
		result.ShouldBe(ExpectedHash("secret-data"));
	}

	[Fact]
	public void SanitizeTag_SuppressCustomSuppressedTagNames()
	{
		// Arrange
		var sanitizer = CreateSanitizer(o => o.SuppressedTagNames = ["custom.suppressed"]);

		// Act
		var result = sanitizer.SanitizeTag("custom.suppressed", "should-be-null");

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void SanitizeTag_NotHashNonConfiguredTags()
	{
		// Arrange — only custom.pii is sensitive, so user.id is passthrough
		var sanitizer = CreateSanitizer(o => o.SensitiveTagNames = ["custom.pii"]);

		// Act
		var result = sanitizer.SanitizeTag("user.id", "alice");

		// Assert
		result.ShouldBe("alice");
	}

	#endregion

	#region IncludeRawPii Bypass

	[Fact]
	public void SanitizeTag_BypassHashingWhenIncludeRawPiiIsTrue()
	{
		// Arrange
		var sanitizer = CreateSanitizer(o => o.IncludeRawPii = true);

		// Act
		var result = sanitizer.SanitizeTag("user.id", "alice@example.com");

		// Assert — raw value returned, not hashed
		result.ShouldBe("alice@example.com");
	}

	[Fact]
	public void SanitizeTag_BypassSuppressionWhenIncludeRawPiiIsTrue()
	{
		// Arrange
		var sanitizer = CreateSanitizer(o => o.IncludeRawPii = true);

		// Act
		var result = sanitizer.SanitizeTag("auth.email", "alice@example.com");

		// Assert — suppressed tag now returns raw value
		result.ShouldBe("alice@example.com");
	}

	[Fact]
	public void SanitizeTag_ReturnNullForNullValueEvenWhenIncludeRawPiiIsTrue()
	{
		// Arrange
		var sanitizer = CreateSanitizer(o => o.IncludeRawPii = true);

		// Act
		var result = sanitizer.SanitizeTag("user.id", null);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void SanitizePayload_BypassHashingWhenIncludeRawPiiIsTrue()
	{
		// Arrange
		var sanitizer = CreateSanitizer(o => o.IncludeRawPii = true);
		var payload = "{\"userId\":\"alice\"}";

		// Act
		var result = sanitizer.SanitizePayload(payload);

		// Assert
		result.ShouldBe(payload);
	}

	#endregion

	#region Constructor Validation

	[Fact]
	public void ThrowArgumentNullExceptionWhenOptionsIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new HashingTelemetrySanitizer(null!));
	}

	#endregion

	#region Edge Cases

	[Fact]
	public void SanitizeTag_HashEmptyStringValueForSensitiveTag()
	{
		// Arrange
		var sanitizer = CreateSanitizer();

		// Act
		var result = sanitizer.SanitizeTag("user.id", "");

		// Assert — empty string is not null, so it gets hashed
		result.ShouldBe(ExpectedHash(""));
	}

	[Fact]
	public void SanitizePayload_HashEmptyString()
	{
		// Arrange
		var sanitizer = CreateSanitizer();

		// Act
		var result = sanitizer.SanitizePayload("");

		// Assert
		result.ShouldBe(ExpectedHash(""));
	}

	[Fact]
	public void SanitizePayload_HandleUnicodeInput()
	{
		// Arrange
		var sanitizer = CreateSanitizer();
		var unicode = "\u00e9\u00e8\u00ea\u00eb\u2603\ud83d\ude00";

		// Act
		var result = sanitizer.SanitizePayload(unicode);

		// Assert
		result.ShouldBe(ExpectedHash(unicode));
	}

	[Fact]
	public void SanitizeTag_SuppressionTakesPriorityOverSensitiveForSameTag()
	{
		// Arrange — tag is in BOTH sensitive and suppressed
		var sanitizer = CreateSanitizer(o =>
		{
			o.SensitiveTagNames = ["overlap.tag"];
			o.SuppressedTagNames = ["overlap.tag"];
		});

		// Act
		var result = sanitizer.SanitizeTag("overlap.tag", "some-value");

		// Assert — suppression check happens before sensitive check
		result.ShouldBeNull();
	}

	#endregion
}
