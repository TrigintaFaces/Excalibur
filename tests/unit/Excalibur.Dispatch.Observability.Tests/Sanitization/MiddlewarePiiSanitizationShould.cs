// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

using Excalibur.Dispatch.Abstractions.Telemetry;
using Excalibur.Dispatch.Extensions;
using Excalibur.Dispatch.Observability.Sanitization;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Observability.Tests.Sanitization;

/// <summary>
/// Tests for P1 middleware PII sanitization fixes:
/// C.8: AuthenticationMiddleware spans contain sha256:HASH, not raw PII
/// C.9: Payload sanitization in logging middleware
/// C.10: Exception message sanitization in spans
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Observability")]
[Trait("Feature", "Sanitization")]
public sealed class MiddlewarePiiSanitizationShould : IDisposable
{
	private readonly ActivityListener _listener;
	private readonly ActivitySource _testSource = new("Test.Middleware.Sanitization", "1.0.0");

	public MiddlewarePiiSanitizationShould()
	{
		_listener = new ActivityListener
		{
			ShouldListenTo = _ => true,
			Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
		};
		ActivitySource.AddActivityListener(_listener);
	}

	public void Dispose()
	{
		_listener.Dispose();
		_testSource.Dispose();
	}

	private static string ExpectedHash(string input)
	{
		var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
		return "sha256:" + Convert.ToHexStringLower(bytes);
	}

	private static HashingTelemetrySanitizer CreateSanitizer(Action<TelemetrySanitizerOptions>? configure = null)
	{
		var options = new TelemetrySanitizerOptions();
		configure?.Invoke(options);
		return new HashingTelemetrySanitizer(MsOptions.Create(options));
	}

	#region C.10: Exception Message Sanitization

	[Fact]
	public void GetSanitizedErrorDescription_ReturnTypeNameOnlyForSystemException()
	{
		// Arrange
		var sanitizer = CreateSanitizer();
		var ex = new InvalidOperationException("User alice@example.com failed validation");

		// Act — SystemException derivatives use type name only
		var result = ex.GetSanitizedErrorDescription(sanitizer);

		// Assert
		result.ShouldBe("InvalidOperationException");
	}

	[Fact]
	public void GetSanitizedErrorDescription_ReturnTypeNameOnlyForOperationCanceledException()
	{
		// Arrange
		var sanitizer = CreateSanitizer();
		var ex = new OperationCanceledException("Cancelled by user alice");

		// Act
		var result = ex.GetSanitizedErrorDescription(sanitizer);

		// Assert
		result.ShouldBe("OperationCanceledException");
	}

	[Fact]
	public void GetSanitizedErrorDescription_ReturnTypeNameOnlyForTimeoutException()
	{
		// Arrange
		var sanitizer = CreateSanitizer();
		var ex = new TimeoutException("Timeout waiting for user alice");

		// Act
		var result = ex.GetSanitizedErrorDescription(sanitizer);

		// Assert
		result.ShouldBe("TimeoutException");
	}

	[Fact]
	public void GetSanitizedErrorDescription_ReturnTypeNameForArgumentException()
	{
		// Arrange — ArgumentException derives from SystemException
		var sanitizer = CreateSanitizer();
		var ex = new ArgumentException("Invalid param for user alice");

		// Act
		var result = ex.GetSanitizedErrorDescription(sanitizer);

		// Assert
		result.ShouldBe("ArgumentException");
	}

	[Fact]
	public void GetSanitizedErrorDescription_SanitizeMessageForNonSystemException()
	{
		// Arrange — custom exception does not inherit from SystemException
		var sanitizer = CreateSanitizer();
		var ex = new CustomBusinessException("User alice@example.com has insufficient funds");

		// Act
		var result = ex.GetSanitizedErrorDescription(sanitizer);

		// Assert
		result.ShouldStartWith("sha256:");
		result.ShouldBe(ExpectedHash("User alice@example.com has insufficient funds"));
	}

	[Fact]
	public void GetSanitizedErrorDescription_ThrowOnNullException()
	{
		// Arrange
		var sanitizer = CreateSanitizer();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			((Exception)null!).GetSanitizedErrorDescription(sanitizer));
	}

	[Fact]
	public void GetSanitizedErrorDescription_ThrowOnNullSanitizer()
	{
		// Arrange
		var ex = new CustomBusinessException("test");

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			ex.GetSanitizedErrorDescription(null!));
	}

	#endregion

	#region C.10: SetSanitizedErrorStatus

	[Fact]
	public void SetSanitizedErrorStatus_SetStatusToErrorWithSanitizedMessage()
	{
		// Arrange
		var sanitizer = CreateSanitizer();
		using var activity = _testSource.StartActivity("test-op");
		activity.ShouldNotBeNull();
		var ex = new CustomBusinessException("User PII data here");

		// Act
		activity.SetSanitizedErrorStatus(ex, sanitizer);

		// Assert
		activity.Status.ShouldBe(ActivityStatusCode.Error);
		activity.StatusDescription.ShouldStartWith("sha256:");
		activity.StatusDescription.ShouldBe(ExpectedHash("User PII data here"));
	}

	[Fact]
	public void SetSanitizedErrorStatus_RecordExceptionEventWithSanitizedMessage()
	{
		// Arrange
		var sanitizer = CreateSanitizer();
		using var activity = _testSource.StartActivity("test-op");
		activity.ShouldNotBeNull();
		var ex = new CustomBusinessException("Sensitive error message");

		// Act
		activity.SetSanitizedErrorStatus(ex, sanitizer);

		// Assert — should have an "exception" event
		var events = activity.Events.ToList();
		events.ShouldContain(e => e.Name == "exception");

		var exceptionEvent = events.First(e => e.Name == "exception");
		var messageTags = exceptionEvent.Tags.ToDictionary(t => t.Key, t => t.Value);

		messageTags["exception.type"].ShouldBe(typeof(CustomBusinessException).FullName);
		messageTags["exception.message"].ShouldNotBeNull();
		((string)messageTags["exception.message"]!).ShouldStartWith("sha256:");
	}

	[Fact]
	public void SetSanitizedErrorStatus_UseTypeNameForSystemExceptions()
	{
		// Arrange
		var sanitizer = CreateSanitizer();
		using var activity = _testSource.StartActivity("test-op");
		activity.ShouldNotBeNull();
		var ex = new InvalidOperationException("Some operation with user data");

		// Act
		activity.SetSanitizedErrorStatus(ex, sanitizer);

		// Assert
		activity.Status.ShouldBe(ActivityStatusCode.Error);
		activity.StatusDescription.ShouldBe("InvalidOperationException");
	}

	[Fact]
	public void SetSanitizedErrorStatus_HandleNullActivityGracefully()
	{
		// Arrange
		var sanitizer = CreateSanitizer();
		Activity? nullActivity = null;
		var ex = new CustomBusinessException("test");

		// Act — should not throw
		nullActivity.SetSanitizedErrorStatus(ex, sanitizer);
	}

	#endregion

	#region C.10: RecordSanitizedException

	[Fact]
	public void RecordSanitizedException_SanitizeExceptionMessage()
	{
		// Arrange
		var sanitizer = CreateSanitizer();
		using var activity = _testSource.StartActivity("test-op");
		activity.ShouldNotBeNull();
		var ex = new CustomBusinessException("PII in exception message");

		// Act
		activity.RecordSanitizedException(ex, sanitizer);

		// Assert
		var events = activity.Events.ToList();
		events.ShouldContain(e => e.Name == "exception");

		var exceptionEvent = events.First(e => e.Name == "exception");
		var messageTags = exceptionEvent.Tags.ToDictionary(t => t.Key, t => t.Value);

		((string)messageTags["exception.message"]!).ShouldStartWith("sha256:");
	}

	[Fact]
	public void RecordSanitizedException_SetErrorStatusIfUnset()
	{
		// Arrange
		var sanitizer = CreateSanitizer();
		using var activity = _testSource.StartActivity("test-op");
		activity.ShouldNotBeNull();
		var ex = new CustomBusinessException("error message");

		// Act
		activity.RecordSanitizedException(ex, sanitizer);

		// Assert
		activity.Status.ShouldBe(ActivityStatusCode.Error);
		activity.StatusDescription.ShouldStartWith("sha256:");
	}

	[Fact]
	public void RecordSanitizedException_HandleNullActivityGracefully()
	{
		// Arrange
		var sanitizer = CreateSanitizer();
		Activity? nullActivity = null;

		// Act — should not throw
		nullActivity.RecordSanitizedException(new CustomBusinessException("test"), sanitizer);
	}

	#endregion

	#region C.10: IncludeRawPii Pass-Through for Exceptions

	[Fact]
	public void GetSanitizedErrorDescription_PassThroughWithIncludeRawPii()
	{
		// Arrange
		var sanitizer = CreateSanitizer(o => o.IncludeRawPii = true);
		var ex = new CustomBusinessException("User alice has insufficient funds");

		// Act
		var result = ex.GetSanitizedErrorDescription(sanitizer);

		// Assert — raw message, not hashed
		result.ShouldBe("User alice has insufficient funds");
	}

	[Fact]
	public void GetSanitizedErrorDescription_StillUseTypeNameForSystemExceptionsWithIncludeRawPii()
	{
		// Arrange — system exceptions always use type name, regardless of IncludeRawPii
		var sanitizer = CreateSanitizer(o => o.IncludeRawPii = true);
		var ex = new InvalidOperationException("Some operation");

		// Act
		var result = ex.GetSanitizedErrorDescription(sanitizer);

		// Assert
		result.ShouldBe("InvalidOperationException");
	}

	#endregion

	#region C.10: NullTelemetrySanitizer for exceptions

	[Fact]
	public void GetSanitizedErrorDescription_PassThroughWithNullTelemetrySanitizer()
	{
		// Arrange
		var ex = new CustomBusinessException("User PII data here");

		// Act
		var result = ex.GetSanitizedErrorDescription(NullTelemetrySanitizer.Instance);

		// Assert — NullTelemetrySanitizer returns the raw message
		result.ShouldBe("User PII data here");
	}

	#endregion

	#region C.8: AuthenticationMiddleware — Sensitive Tags Hashed, Suppressed Tags Omitted

	[Theory]
	[InlineData("auth.identity_name", "alice@example.com")]
	[InlineData("auth.user_id", "user-12345")]
	[InlineData("auth.tenant_id", "tenant-abc")]
	[InlineData("user.id", "uid-999")]
	public void SanitizeTag_HashSensitiveAuthTags(string tagName, string rawValue)
	{
		// Arrange — default options have these as sensitive tags
		var sanitizer = CreateSanitizer();

		// Act
		var result = sanitizer.SanitizeTag(tagName, rawValue);

		// Assert — hashed, not raw
		result.ShouldNotBeNull();
		result.ShouldStartWith("sha256:");
		result.ShouldNotBe(rawValue);
		result.ShouldBe(ExpectedHash(rawValue));
	}

	[Theory]
	[InlineData("auth.email", "alice@example.com")]
	[InlineData("auth.token", "Bearer eyJhbG...")]
	public void SanitizeTag_SuppressHighlySensitiveTags(string tagName, string rawValue)
	{
		// Arrange — default options have these as suppressed tags
		var sanitizer = CreateSanitizer();

		// Act
		var result = sanitizer.SanitizeTag(tagName, rawValue);

		// Assert — suppressed entirely (null)
		result.ShouldBeNull();
	}

	[Theory]
	[InlineData("auth.is_authenticated", "true")]
	[InlineData("auth.authentication_type", "Bearer")]
	[InlineData("message.type", "CreateOrderCommand")]
	public void SanitizeTag_PassThroughNonSensitiveTags(string tagName, string rawValue)
	{
		// Arrange
		var sanitizer = CreateSanitizer();

		// Act
		var result = sanitizer.SanitizeTag(tagName, rawValue);

		// Assert — passthrough, unchanged
		result.ShouldBe(rawValue);
	}

	[Fact]
	public void SanitizeTag_SpanContainsHashedAuthTagsNotRawPii()
	{
		// Arrange — simulate what AuthenticationMiddleware.SetAuthenticationActivityTags does
		var sanitizer = CreateSanitizer();
		using var activity = _testSource.StartActivity("AuthenticationMiddleware.Invoke");
		activity.ShouldNotBeNull();

		var rawIdentityName = "alice@example.com";
		var rawUserId = "user-12345";
		var rawEmail = "alice@example.com";

		// Act — mirror AuthenticationMiddleware.SetSanitizedTag pattern
		var sanitizedIdentity = sanitizer.SanitizeTag("auth.identity_name", rawIdentityName);
		if (sanitizedIdentity is not null)
		{
			_ = activity.SetTag("auth.identity_name", sanitizedIdentity);
		}

		var sanitizedUserId = sanitizer.SanitizeTag("auth.user_id", rawUserId);
		if (sanitizedUserId is not null)
		{
			_ = activity.SetTag("auth.user_id", sanitizedUserId);
		}

		var sanitizedEmail = sanitizer.SanitizeTag("auth.email", rawEmail);
		if (sanitizedEmail is not null)
		{
			_ = activity.SetTag("auth.email", sanitizedEmail);
		}

		// Assert — identity_name and user_id are hashed
		var tags = activity.Tags.ToDictionary(t => t.Key, t => t.Value);

		tags["auth.identity_name"].ShouldStartWith("sha256:");
		tags["auth.identity_name"].ShouldBe(ExpectedHash(rawIdentityName));

		tags["auth.user_id"].ShouldStartWith("sha256:");
		tags["auth.user_id"].ShouldBe(ExpectedHash(rawUserId));

		// Assert — email is suppressed (tag not present)
		tags.ShouldNotContainKey("auth.email");
	}

	[Fact]
	public void SanitizeTag_DeterministicHashAllowsCorrelation()
	{
		// Arrange — same input should produce same hash (enabling trace correlation)
		var sanitizer = CreateSanitizer();
		var rawValue = "user-12345";

		// Act
		var hash1 = sanitizer.SanitizeTag("user.id", rawValue);
		var hash2 = sanitizer.SanitizeTag("auth.user_id", rawValue);

		// Assert — same raw value = same hash regardless of tag name
		hash1.ShouldBe(hash2);
	}

	[Fact]
	public void SanitizeTag_IncludeRawPiiBypassesAllSanitization()
	{
		// Arrange
		var sanitizer = CreateSanitizer(o => o.IncludeRawPii = true);

		// Act — even sensitive tags pass through
		var result = sanitizer.SanitizeTag("auth.identity_name", "alice@example.com");

		// Assert
		result.ShouldBe("alice@example.com");
	}

	[Fact]
	public void SanitizeTag_IncludeRawPiiBypassesSuppression()
	{
		// Arrange
		var sanitizer = CreateSanitizer(o => o.IncludeRawPii = true);

		// Act — even suppressed tags pass through
		var result = sanitizer.SanitizeTag("auth.email", "alice@example.com");

		// Assert
		result.ShouldBe("alice@example.com");
	}

	#endregion

	#region C.9: Payload Sanitization via SanitizePayload

	[Fact]
	public void SanitizePayload_HashPayloadByDefault()
	{
		// Arrange
		var sanitizer = CreateSanitizer();
		var rawPayload = "User alice@example.com submitted order #12345";

		// Act
		var result = sanitizer.SanitizePayload(rawPayload);

		// Assert
		result.ShouldStartWith("sha256:");
		result.ShouldBe(ExpectedHash(rawPayload));
	}

	[Fact]
	public void SanitizePayload_PassThroughWithIncludeRawPii()
	{
		// Arrange
		var sanitizer = CreateSanitizer(o => o.IncludeRawPii = true);
		var rawPayload = "User alice@example.com submitted order #12345";

		// Act
		var result = sanitizer.SanitizePayload(rawPayload);

		// Assert
		result.ShouldBe(rawPayload);
	}

	#endregion

	#region Test Helpers

	private sealed class CustomBusinessException : Exception
	{
		public CustomBusinessException(string message) : base(message)
		{
		}
	}

	#endregion
}
