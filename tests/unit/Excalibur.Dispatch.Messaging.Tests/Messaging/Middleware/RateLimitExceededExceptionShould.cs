// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Middleware;

namespace Excalibur.Dispatch.Tests.Messaging.Middleware;

/// <summary>
/// Unit tests for <see cref="RateLimitExceededException"/>.
/// </summary>
/// <remarks>
/// Tests the exception thrown when rate limiting is exceeded.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Middleware")]
[Trait("Priority", "0")]
public sealed class RateLimitExceededExceptionShould
{
	#region Constructor Tests - Default

	[Fact]
	public void Constructor_Default_CreatesInstance()
	{
		// Act
		var exception = new RateLimitExceededException();

		// Assert
		_ = exception.ShouldNotBeNull();
	}

	[Fact]
	public void Constructor_Default_HasEmptyMessage()
	{
		// Act
		var exception = new RateLimitExceededException();

		// Assert
		exception.Message.ShouldBe(string.Empty);
	}

	[Fact]
	public void Constructor_Default_HasNullRetryAfter()
	{
		// Act
		var exception = new RateLimitExceededException();

		// Assert
		exception.RetryAfter.ShouldBeNull();
	}

	[Fact]
	public void Constructor_Default_HasNullRateLimiterKey()
	{
		// Act
		var exception = new RateLimitExceededException();

		// Assert
		exception.RateLimiterKey.ShouldBeNull();
	}

	#endregion

	#region Constructor Tests - With Message

	[Fact]
	public void Constructor_WithMessage_SetsMessage()
	{
		// Arrange
		const string message = "Rate limit exceeded: 100 requests per minute";

		// Act
		var exception = new RateLimitExceededException(message);

		// Assert
		exception.Message.ShouldBe(message);
	}

	[Fact]
	public void Constructor_WithEmptyMessage_AcceptsEmptyString()
	{
		// Act
		var exception = new RateLimitExceededException(string.Empty);

		// Assert
		exception.Message.ShouldBe(string.Empty);
	}

	[Theory]
	[InlineData("Too many requests")]
	[InlineData("API quota exhausted")]
	[InlineData("Request throttled")]
	public void Constructor_WithVariousMessages_PreservesMessage(string message)
	{
		// Act
		var exception = new RateLimitExceededException(message);

		// Assert
		exception.Message.ShouldBe(message);
	}

	#endregion

	#region Constructor Tests - With Message and InnerException

	[Fact]
	public void Constructor_WithMessageAndInnerException_SetsMessage()
	{
		// Arrange
		const string message = "Rate limit exceeded";
		var innerException = new InvalidOperationException("Inner");

		// Act
		var exception = new RateLimitExceededException(message, innerException);

		// Assert
		exception.Message.ShouldBe(message);
	}

	[Fact]
	public void Constructor_WithNullMessage_UsesEmptyString()
	{
		// Act
		var exception = new RateLimitExceededException(null, new InvalidOperationException());

		// Assert
		exception.Message.ShouldBe(string.Empty);
	}

	#endregion

	#region RetryAfter Property Tests

	[Fact]
	public void RetryAfter_CanBeSet()
	{
		// Arrange
		var exception = new RateLimitExceededException("Rate limited");

		// Act
		exception.RetryAfter = TimeSpan.FromSeconds(60);

		// Assert
		exception.RetryAfter.ShouldBe(TimeSpan.FromSeconds(60));
	}

	[Fact]
	public void RetryAfter_CanBeSetToZero()
	{
		// Arrange
		var exception = new RateLimitExceededException("Rate limited");

		// Act
		exception.RetryAfter = TimeSpan.Zero;

		// Assert
		exception.RetryAfter.ShouldBe(TimeSpan.Zero);
	}

	[Fact]
	public void RetryAfter_CanBeSetToNull()
	{
		// Arrange
		var exception = new RateLimitExceededException("Rate limited")
		{
			RetryAfter = TimeSpan.FromSeconds(30),
		};

		// Act
		exception.RetryAfter = null;

		// Assert
		exception.RetryAfter.ShouldBeNull();
	}

	[Fact]
	public void RetryAfter_CanBeSetToLargeValue()
	{
		// Arrange
		var exception = new RateLimitExceededException("Rate limited");

		// Act
		exception.RetryAfter = TimeSpan.FromHours(24);

		// Assert
		exception.RetryAfter.ShouldBe(TimeSpan.FromHours(24));
	}

	[Fact]
	public void RetryAfter_CanBeSetViaObjectInitializer()
	{
		// Act
		var exception = new RateLimitExceededException("Rate limited")
		{
			RetryAfter = TimeSpan.FromMinutes(5),
		};

		// Assert
		exception.RetryAfter.ShouldBe(TimeSpan.FromMinutes(5));
	}

	#endregion

	#region RateLimiterKey Property Tests

	[Fact]
	public void RateLimiterKey_CanBeSet()
	{
		// Arrange
		var exception = new RateLimitExceededException("Rate limited");

		// Act
		exception.RateLimiterKey = "user:12345";

		// Assert
		exception.RateLimiterKey.ShouldBe("user:12345");
	}

	[Fact]
	public void RateLimiterKey_CanBeSetToNull()
	{
		// Arrange
		var exception = new RateLimitExceededException("Rate limited")
		{
			RateLimiterKey = "test-key",
		};

		// Act
		exception.RateLimiterKey = null;

		// Assert
		exception.RateLimiterKey.ShouldBeNull();
	}

	[Fact]
	public void RateLimiterKey_CanBeSetToEmptyString()
	{
		// Arrange
		var exception = new RateLimitExceededException("Rate limited");

		// Act
		exception.RateLimiterKey = string.Empty;

		// Assert
		exception.RateLimiterKey.ShouldBe(string.Empty);
	}

	[Theory]
	[InlineData("api-key:abc123")]
	[InlineData("ip:192.168.1.1")]
	[InlineData("tenant:acme-corp")]
	[InlineData("endpoint:/api/orders")]
	public void RateLimiterKey_AcceptsVariousKeyFormats(string key)
	{
		// Arrange
		var exception = new RateLimitExceededException("Rate limited");

		// Act
		exception.RateLimiterKey = key;

		// Assert
		exception.RateLimiterKey.ShouldBe(key);
	}

	#endregion

	#region Inheritance Tests

	[Fact]
	public void InheritsFromException()
	{
		// Act
		var exception = new RateLimitExceededException("test");

		// Assert
		_ = exception.ShouldBeAssignableTo<Exception>();
	}

	[Fact]
	public void CanBeCaughtAsException()
	{
		// Act & Assert
		_ = Should.Throw<Exception>(() => throw new RateLimitExceededException("test"));
	}

	[Fact]
	public void CanBeCaughtAsRateLimitExceededException()
	{
		// Act & Assert
		_ = Should.Throw<RateLimitExceededException>(() => throw new RateLimitExceededException("test"));
	}

	#endregion

	#region Usage Scenario Tests

	[Fact]
	public void CanBeInitializedWithAllProperties()
	{
		// Act
		var exception = new RateLimitExceededException("Rate limit exceeded for user")
		{
			RetryAfter = TimeSpan.FromSeconds(60),
			RateLimiterKey = "user:john.doe",
		};

		// Assert
		exception.Message.ShouldBe("Rate limit exceeded for user");
		exception.RetryAfter.ShouldBe(TimeSpan.FromSeconds(60));
		exception.RateLimiterKey.ShouldBe("user:john.doe");
	}

	[Fact]
	public void CanCalculateRetryTime()
	{
		// Arrange
		var exception = new RateLimitExceededException("Too many requests")
		{
			RetryAfter = TimeSpan.FromSeconds(30),
		};

		// Act
		var retryAt = DateTimeOffset.UtcNow.Add(exception.RetryAfter.Value);

		// Assert
		retryAt.ShouldBeGreaterThan(DateTimeOffset.UtcNow);
	}

	[Fact]
	public void PreservesStackTraceWhenThrown()
	{
		// Arrange
		RateLimitExceededException? caught = null;

		// Act
		try
		{
			ThrowRateLimitException();
		}
		catch (RateLimitExceededException ex)
		{
			caught = ex;
		}

		// Assert
		_ = caught.ShouldNotBeNull();
		_ = caught.StackTrace.ShouldNotBeNull();
	}

	private static void ThrowRateLimitException()
	{
		throw new RateLimitExceededException("Thrown from helper");
	}

	#endregion
}
