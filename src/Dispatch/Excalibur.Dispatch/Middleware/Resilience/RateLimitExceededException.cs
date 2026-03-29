// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Middleware.Resilience;

/// <summary>
/// Exception thrown when rate limit is exceeded.
/// </summary>
/// <remarks>Initializes a new instance of the <see cref="RateLimitExceededException" /> class.</remarks>
/// <param name="message">The exception message.</param>
public sealed class RateLimitExceededException(string message) : Exception(message)
{
	/// <summary>
	/// Initializes a new instance of the <see cref="RateLimitExceededException" /> class.
	/// </summary>
	public RateLimitExceededException() : this(string.Empty)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="RateLimitExceededException" /> class with a message and inner exception.
	/// </summary>
	/// <param name="message">The exception message.</param>
	/// <param name="innerException">The inner exception.</param>
	/// <remarks>
	/// The <paramref name="innerException"/> parameter cannot be forwarded to the base <see cref="Exception(string, Exception)"/>
	/// constructor because the primary constructor locks the base call to <c>Exception(message)</c>.
	/// This overload exists to satisfy the standard exception constructor pattern (CA1032).
	/// </remarks>
#pragma warning disable IDE0060 // innerException cannot be forwarded — primary constructor limits base call to Exception(message)
	public RateLimitExceededException(string? message, Exception? innerException) : this(message ?? string.Empty)
	{
	}
#pragma warning restore IDE0060

	/// <summary>
	/// Gets or sets the time after which to retry.
	/// </summary>
	/// <value>The current <see cref="RetryAfter"/> value.</value>
	public TimeSpan? RetryAfter { get; set; }

	/// <summary>
	/// Gets or sets the rate limiter key that was exceeded.
	/// </summary>
	/// <value>The current <see cref="RateLimiterKey"/> value.</value>
	public string? RateLimiterKey { get; set; }
}
