// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Net.Sockets;

using Microsoft.Extensions.Options;

using OpenSearch.Net;

namespace Excalibur.Data.OpenSearch.Resilience;

/// <summary>
/// Implements retry policy for OpenSearch operations with exponential backoff and jitter.
/// </summary>
public sealed class OpenSearchRetryPolicy : IOpenSearchRetryPolicy
{
	private readonly OpenSearchRetryPolicyOptions _settings;

	/// <summary>
	/// Initializes a new instance of the <see cref="OpenSearchRetryPolicy" /> class.
	/// </summary>
	/// <param name="options"> The OpenSearch configuration options containing retry settings. </param>
	/// <exception cref="ArgumentNullException"> Thrown when options is null. </exception>
	public OpenSearchRetryPolicy(IOptions<OpenSearchConfigurationOptions> options)
	{
		ArgumentNullException.ThrowIfNull(options);
		_settings = options.Value.Resilience.Retry;
	}

	/// <inheritdoc />
	public int MaxAttempts => _settings.MaxAttempts;

	/// <inheritdoc />
	public TimeSpan GetRetryDelay(int attemptNumber)
	{
		if (attemptNumber < 0)
		{
			throw new ArgumentOutOfRangeException(nameof(attemptNumber), "Attempt number cannot be negative");
		}

		if (!_settings.Enabled || attemptNumber >= _settings.MaxAttempts)
		{
			return TimeSpan.Zero;
		}

		TimeSpan delay;

		if (_settings.UseExponentialBackoff)
		{
			// Calculate exponential backoff: baseDelay * 2^attemptNumber
			var exponentialDelay = TimeSpan.FromMilliseconds(
				_settings.BaseDelay.TotalMilliseconds * Math.Pow(2, attemptNumber));

			// Cap at maximum delay
			delay = exponentialDelay > _settings.MaxDelay ? _settings.MaxDelay : exponentialDelay;
		}
		else
		{
			// Use fixed delay
			delay = _settings.BaseDelay;
		}

		// Apply jitter to avoid thundering herd effect
		if (_settings.JitterFactor > 0)
		{
			var jitterRange = delay.TotalMilliseconds * _settings.JitterFactor;
			// Jitter for retry delay does not require cryptographic randomness
#pragma warning disable CA5394
			var jitterOffset = (Random.Shared.NextDouble() - 0.5) * 2 * jitterRange;
			var jitteredDelayMs = Math.Max(0, delay.TotalMilliseconds + jitterOffset);
#pragma warning restore CA5394
			delay = TimeSpan.FromMilliseconds(jitteredDelayMs);
		}

		return delay;
	}

	/// <inheritdoc />
	public bool ShouldRetry(Exception exception, int attemptNumber)
	{
		if (!_settings.Enabled)
		{
			return false;
		}

		if (attemptNumber >= _settings.MaxAttempts + 1) // +1 because attemptNumber is 1-based
		{
			return false;
		}

		return IsRetriableException(exception);
	}

	/// <summary>
	/// Determines whether an exception is retriable based on its type and characteristics.
	/// </summary>
	/// <param name="exception"> The exception to evaluate. </param>
	/// <returns> True if the exception is retriable, false otherwise. </returns>
	private static bool IsRetriableException(Exception exception) =>
		exception switch
		{
			// Timeout and cancellation exceptions are generally retriable
			TimeoutException => true,
			TaskCanceledException { CancellationToken.IsCancellationRequested: false } => true,

			// Network-related exceptions are retriable
			HttpRequestException => true,
			SocketException => true,

			// OpenSearch client exceptions - check HTTP status codes
			OpenSearchClientException ose => IsRetriableHttpStatusCode(ose.Response?.HttpStatusCode) || IsRetriableOpenSearchException(ose),

			// Generic IO exceptions might be retriable
			IOException => true,

			// Default to not retriable for unknown exceptions
			_ => false,
		};

	/// <summary>
	/// Determines whether an HTTP status code indicates a retriable condition.
	/// </summary>
	/// <param name="statusCode"> The HTTP status code to evaluate. </param>
	/// <returns> True if the status code indicates a retriable condition, false otherwise. </returns>
	private static bool IsRetriableHttpStatusCode(int? statusCode) =>
		statusCode switch
		{
			// Too Many Requests - definitely retriable
			429 => true,

			// Server errors that might be transient
			500 => true, // Internal Server Error
			502 => true, // Bad Gateway
			503 => true, // Service Unavailable
			504 => true, // Gateway Timeout

			// Connection-related errors
			408 => true, // Request Timeout

			// Client errors that are generally not retriable
			400 => false, // Bad Request
			401 => false, // Unauthorized
			403 => false, // Forbidden
			404 => false, // Not Found
			409 => false, // Conflict

			// Default for unknown status codes
			null => true, // No status code might indicate network issue
			_ => statusCode >= 500, // Other 5xx errors are potentially retriable
		};

	/// <summary>
	/// Determines whether an OpenSearch-specific exception is retriable.
	/// </summary>
	/// <param name="exception"> The OpenSearch client exception to evaluate. </param>
	/// <returns> True if the exception is retriable, false otherwise. </returns>
	private static bool IsRetriableOpenSearchException(OpenSearchClientException exception)
	{
		// Check the exception message for known retriable conditions
		// Case-insensitive comparison of error message substrings requires lowercase normalization
#pragma warning disable CA1308
		var message = exception.Message?.ToLowerInvariant() ?? string.Empty;

		// Connection-related errors
		if (message.Contains("connection", StringComparison.Ordinal) && (
				message.Contains("timeout", StringComparison.Ordinal) ||
				message.Contains("refused", StringComparison.Ordinal) ||
				message.Contains("reset", StringComparison.Ordinal) ||
				message.Contains("aborted", StringComparison.Ordinal)))
		{
			return true;
		}

		// Cluster-related temporary issues
		if (message.Contains("cluster", StringComparison.Ordinal) && (
				message.Contains("block", StringComparison.Ordinal) ||
				message.Contains("unavailable", StringComparison.Ordinal) ||
				message.Contains("red", StringComparison.Ordinal)))
		{
			return true;
		}

		// Shard-related temporary issues
		if (message.Contains("shard", StringComparison.Ordinal) && (
				message.Contains("not available", StringComparison.Ordinal) ||
				message.Contains("relocating", StringComparison.Ordinal) ||
				message.Contains("initializing", StringComparison.Ordinal)))
		{
			return true;
		}

		// Resource exhaustion that might be temporary
		if (message.Contains("too many", StringComparison.Ordinal) ||
			message.Contains("queue", StringComparison.Ordinal) ||
			message.Contains("rejected", StringComparison.Ordinal))
		{
			return true;
		}

		// Default to not retriable for unknown OpenSearch errors
		return false;
	}
}
