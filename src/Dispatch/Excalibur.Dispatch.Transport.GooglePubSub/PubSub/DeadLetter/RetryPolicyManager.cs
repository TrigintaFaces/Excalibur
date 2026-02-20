// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using Excalibur.Dispatch.Transport.GooglePubSub;

using Google.Cloud.PubSub.V1;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Polly;

// using Polly.Contrib.WaitAndRetry; // Not available, using built-in Polly patterns instead
namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Manages intelligent retry policies for dead letter message processing.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="RetryPolicyManager" /> class. </remarks>
public sealed partial class RetryPolicyManager(
	ILogger<RetryPolicyManager> logger,
	IOptions<RetryPolicyOptions> options) : IRetryPolicyManager
{
	private readonly ILogger<RetryPolicyManager> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
	private readonly IOptions<RetryPolicyOptions> _options = options ?? throw new ArgumentNullException(nameof(options));
	private readonly ConcurrentDictionary<string, IAsyncPolicy> _policyCache = new(StringComparer.Ordinal);
	private readonly ConcurrentDictionary<string, RetryStatistics> _retryStats = new(StringComparer.Ordinal);

	/// <summary>
	/// Gets or creates a retry policy for a specific message type or pattern.
	/// </summary>
	public IAsyncPolicy GetRetryPolicy(PubsubMessage message, Exception? lastException = null)
	{
		var policyKey = DeterminePolicyKey(message, lastException);

		return _policyCache.GetOrAdd(policyKey, key =>
		{
			var strategy = DetermineRetryStrategy(message, lastException);
			return BuildPolicy(strategy, key);
		});
	}

	/// <summary>
	/// Records retry attempt for analytics.
	/// </summary>
	public void RecordRetryAttempt(
		string messageId,
		string policyKey,
		int attemptNumber,
		TimeSpan duration,
		bool success)
	{
		var stats = _retryStats.GetOrAdd(policyKey, static _ => new RetryStatistics());

		// Use lock for thread-safe updates since we can't use Interlocked with properties
		lock (stats)
		{
			stats.TotalAttempts++;

			if (success)
			{
				stats.SuccessfulAttempts++;
			}
			else
			{
				stats.FailedAttempts++;
			}

			// Update average duration
			stats.TotalDuration += duration;
			stats.AverageDuration = stats.TotalDuration / stats.TotalAttempts;
		}

		LogRetryAttempt(attemptNumber, messageId);
	}

	/// <summary>
	/// Gets retry statistics for monitoring.
	/// </summary>
	public RetryPolicyStatistics GetStatistics()
	{
		var stats = new RetryPolicyStatistics
		{
			PolicyCount = _policyCache.Count,
			TotalRetryAttempts = _retryStats.Sum(static kvp => kvp.Value.TotalAttempts),
			SuccessRate = CalculateOverallSuccessRate(),

			// Get per-policy statistics
			PolicyStatistics =
			[
				.. _retryStats
					.Select(static kvp => new PolicyStatistic
					{
						PolicyKey = kvp.Key,
						TotalAttempts = kvp.Value.TotalAttempts,
						SuccessfulAttempts = kvp.Value.SuccessfulAttempts,
						FailedAttempts = kvp.Value.FailedAttempts,
						AverageDuration = kvp.Value.AverageDuration,
						SuccessRate = kvp.Value.TotalAttempts > 0
							? (double)kvp.Value.SuccessfulAttempts / kvp.Value.TotalAttempts
							: 0,
					})
					.OrderByDescending(static p => p.TotalAttempts),
			],
		};

		return stats;
	}

	/// <summary>
	/// Creates an adaptive retry policy based on historical performance.
	/// </summary>
	public IAsyncPolicy CreateAdaptivePolicy(string policyKey)
	{
		var baseStrategy = _options.Value.DefaultStrategy;

		// Adapt based on historical performance
		if (_retryStats.TryGetValue(policyKey, out var stats) && stats.TotalAttempts > 10)
		{
			var successRate = (double)stats.SuccessfulAttempts / stats.TotalAttempts;

			if (successRate < 0.2)
			{
				// Very low success rate - use more aggressive backoff
				baseStrategy = new RetryStrategy
				{
					MaxRetryAttempts = Math.Min(baseStrategy.MaxRetryAttempts, 3),
					InitialDelay = TimeSpan.FromSeconds(30),
					MaxDelay = TimeSpan.FromMinutes(10),
					BackoffType = BackoffType.Exponential,
					JitterEnabled = true,
				};

				LogAdaptedLowSuccessRate(policyKey, successRate, baseStrategy.InitialDelay.TotalSeconds);
			}
			else if (successRate > 0.8)
			{
				// High success rate - can be more aggressive
				baseStrategy = new RetryStrategy
				{
					MaxRetryAttempts = baseStrategy.MaxRetryAttempts + 2,
					InitialDelay = TimeSpan.FromSeconds(5),
					MaxDelay = TimeSpan.FromMinutes(2),
					BackoffType = BackoffType.Linear,
					JitterEnabled = true,
				};

				LogAdaptedHighSuccessRate(policyKey, successRate, baseStrategy.InitialDelay.TotalSeconds);
			}
		}

		return BuildPolicy(baseStrategy, policyKey);
	}

	/// <summary>
	/// Determines if a message should be retried.
	/// </summary>
	public bool ShouldRetry(PubsubMessage message, Exception exception)
	{
		if (IsPermanentException(exception))
		{
			return false;
		}

		// Check retry count from message attributes
		if (message.Attributes.TryGetValue("retryCount", out var retryCountStr) &&
			int.TryParse(retryCountStr, out var retryCount))
		{
			var strategy = DetermineRetryStrategy(message, exception);
			return retryCount < strategy.MaxRetryAttempts;
		}

		return true;
	}

	/// <summary>
	/// Gets the retry policy for a specific message type.
	/// </summary>
	public IAsyncPolicy GetRetryPolicy(string messageType)
	{
		var policyKey = $"{messageType}:default";
		return _policyCache.GetOrAdd(policyKey, key =>
		{
			var strategy = _options.Value.CustomStrategies.GetValueOrDefault(messageType, _options.Value.DefaultStrategy);
			return BuildPolicy(strategy, key);
		});
	}

	/// <summary>
	/// Executes an action with the appropriate retry policy.
	/// </summary>
	/// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
	public async Task<TResult> ExecuteWithRetryAsync<TResult>(
		string messageType,
		Func<CancellationToken, Task<TResult>> action,
		CancellationToken cancellationToken)
	{
		var policy = GetRetryPolicy(messageType);
		var context = new Context { ["MessageType"] = messageType };

		return await policy.ExecuteAsync(async (ctx, ct) => await action(ct).ConfigureAwait(false), context, cancellationToken)
			.ConfigureAwait(false);
	}

	[System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA5394:Do not use insecure randomness",
		Justification = "Random is used for retry jitter timing, not for security purposes. Cryptographic randomness is unnecessary for backoff delays.")]
	private static IEnumerable<TimeSpan> GenerateDelays(RetryStrategy strategy)
	{
		var delays = strategy.BackoffType switch
		{
			BackoffType.Constant => Enumerable.Repeat(strategy.InitialDelay, strategy.MaxRetryAttempts),

			BackoffType.Linear => Enumerable.Range(1, strategy.MaxRetryAttempts)
				.Select(i => TimeSpan.FromMilliseconds(strategy.InitialDelay.TotalMilliseconds * i)),

			BackoffType.Exponential => Enumerable.Range(1, strategy.MaxRetryAttempts)
				.Select(i => TimeSpan.FromMilliseconds(strategy.InitialDelay.TotalMilliseconds * Math.Pow(2.0, i - 1))),

			BackoffType.DecorrelatedJitter => Enumerable.Range(1, strategy.MaxRetryAttempts)
				.Select(i => TimeSpan.FromMilliseconds(strategy.InitialDelay.TotalMilliseconds * Math.Pow(2.0, i - 1)))
				.Select(delay =>
					TimeSpan.FromMilliseconds(delay.TotalMilliseconds + (Random.Shared.NextDouble() * delay.TotalMilliseconds * 0.1))),

			_ => throw new InvalidOperationException($"Unknown backoff type: {strategy.BackoffType}"),
		};

		// Apply max delay cap
		delays = delays.Select(d => d > strategy.MaxDelay ? strategy.MaxDelay : d);

		// Add jitter if enabled
		if (strategy.JitterEnabled && strategy.BackoffType != BackoffType.DecorrelatedJitter)
		{
			var random = new Random();
			delays = delays.Select(d =>
				d + TimeSpan.FromMilliseconds(random.Next(0, (int)(d.TotalMilliseconds * 0.2))));
		}

		return delays;
	}

	private static bool IsTimeoutException(Exception? exception)
	{
		if (exception == null)
		{
			return false;
		}

		return exception is OperationCanceledException ||
			   exception is TimeoutException ||
			   exception.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
			   exception.Message.Contains("deadline exceeded", StringComparison.OrdinalIgnoreCase);
	}

	private static bool IsTransientException(Exception? exception)
	{
		if (exception == null)
		{
			return false;
		}

		// Common transient error patterns
		var transientPatterns = new[]
		{
			"TEMPORARILY UNAVAILABLE", "TOO MANY REQUESTS", "SERVICE UNAVAILABLE", "INTERNAL SERVER ERROR", "BAD GATEWAY",
			"GATEWAY TIMEOUT",
		};

		var message = exception.Message.ToUpperInvariant();
		return transientPatterns.Any(p => message.Contains(p, StringComparison.Ordinal));
	}

	private static bool IsResourceException(Exception? exception)
	{
		if (exception == null)
		{
			return false;
		}

		return exception is OutOfMemoryException ||
			   exception.Message.Contains("resource", StringComparison.OrdinalIgnoreCase) ||
			   exception.Message.Contains("quota", StringComparison.OrdinalIgnoreCase) ||
			   exception.Message.Contains("limit", StringComparison.OrdinalIgnoreCase);
	}

	private static bool IsPermanentException(Exception exception)
	{
		// Identify exceptions that should not be retried
		if (exception.Message.Contains("invalid", StringComparison.OrdinalIgnoreCase) ||
			exception.Message.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
			exception.Message.Contains("unauthorized", StringComparison.OrdinalIgnoreCase) ||
			exception.Message.Contains("forbidden", StringComparison.OrdinalIgnoreCase))
		{
			return true;
		}

		// Check for specific exception types
		return exception is ArgumentException or
			InvalidOperationException or
			NotSupportedException or
			UnauthorizedAccessException;
	}

	private string DeterminePolicyKey(PubsubMessage message, Exception? lastException)
	{
		// Determine policy key based on message attributes and exception type
		var messageType = message.Attributes.GetValueOrDefault("message_type", "unknown");
		var exceptionType = lastException?.GetType().Name ?? "none";

		// Check for specific patterns that need special handling
		if (IsTimeoutException(lastException))
		{
			return $"{messageType}:timeout";
		}

		if (IsTransientException(lastException))
		{
			return $"{messageType}:transient";
		}

		if (IsResourceException(lastException))
		{
			return $"{messageType}:resource";
		}

		return $"{messageType}:{exceptionType}";
	}

	private RetryStrategy DetermineRetryStrategy(PubsubMessage message, Exception? lastException)
	{
		// Check custom strategies first
		var messageType = message.Attributes.GetValueOrDefault("message_type", "unknown");

		if (_options.Value.CustomStrategies.TryGetValue(messageType, out var customStrategy))
		{
			return customStrategy;
		}

		// Determine strategy based on exception type
		if (IsTimeoutException(lastException))
		{
			return _options.Value.TimeoutStrategy;
		}

		if (IsTransientException(lastException))
		{
			return _options.Value.TransientErrorStrategy;
		}

		if (IsResourceException(lastException))
		{
			return _options.Value.ResourceExhaustionStrategy;
		}

		return _options.Value.DefaultStrategy;
	}

	private IAsyncPolicy BuildPolicy(RetryStrategy strategy, string policyKey)
	{
		var delays = GenerateDelays(strategy);

		var retryPolicy = Policy
			.Handle<Exception>(ex => !IsPermanentException(ex))
			.WaitAndRetryAsync(
				delays,
				onRetry: (outcome, delay, retryCount, context) =>
				{
					var messageId = context.TryGetValue("MessageId", out var messageIdObj)
						? messageIdObj?.ToString() ?? "unknown"
						: "unknown";

					LogRetryWarning(retryCount, messageId, "unknown");
				});

		// Add circuit breaker if configured
		if (strategy.CircuitBreakerEnabled)
		{
			var circuitBreaker = Policy
				.Handle<Exception>(ex => !IsPermanentException(ex))
				.CircuitBreakerAsync(
					strategy.CircuitBreakerThreshold,
					strategy.CircuitBreakerDuration,
					onBreak: (result, duration) => LogCircuitBreakerOpened(policyKey, duration),
					onReset: () => LogCircuitBreakerReset(policyKey));

			return Policy.WrapAsync(retryPolicy, circuitBreaker);
		}

		return retryPolicy;
	}

	private double CalculateOverallSuccessRate()
	{
		var totalAttempts = _retryStats.Sum(static kvp => kvp.Value.TotalAttempts);
		var successfulAttempts = _retryStats.Sum(static kvp => kvp.Value.SuccessfulAttempts);

		return totalAttempts > 0 ? (double)successfulAttempts / totalAttempts : 0;
	}

	// Source-generated logging methods (Sprint 363 - EventId Migration)
	[LoggerMessage(GooglePubSubEventId.RetryAttemptLogged, LogLevel.Debug,
		"Retry attempt {Attempt} for message {MessageId}")]
	private partial void LogRetryAttempt(int attempt, string messageId);

	[LoggerMessage(GooglePubSubEventId.RetryAdaptedLowSuccessRate, LogLevel.Information,
		"Adapted retry policy for {PolicyKey} due to low success rate. Success rate: {SuccessRate:P2}, new backoff: {NewBackoffSeconds}s")]
	private partial void LogAdaptedLowSuccessRate(string policyKey, double successRate, double newBackoffSeconds);

	[LoggerMessage(GooglePubSubEventId.RetryAdaptedHighSuccessRate, LogLevel.Information,
		"Adapted retry policy for {PolicyKey} due to high success rate. Success rate: {SuccessRate:P2}, new backoff: {NewBackoffSeconds}s")]
	private partial void LogAdaptedHighSuccessRate(string policyKey, double successRate, double newBackoffSeconds);

	[LoggerMessage(GooglePubSubEventId.RetryWarning, LogLevel.Warning,
		"Retry attempt {RetryCount} for message {MessageId} with policy {PolicyKey}")]
	private partial void LogRetryWarning(int retryCount, string messageId, string policyKey);

	[LoggerMessage(GooglePubSubEventId.CircuitBreakerOpened, LogLevel.Warning,
		"Circuit breaker opened for policy {PolicyKey} for {Duration}")]
	private partial void LogCircuitBreakerOpened(string policyKey, TimeSpan duration);

	[LoggerMessage(GooglePubSubEventId.CircuitBreakerReset, LogLevel.Information,
		"Circuit breaker reset for policy {PolicyKey}")]
	private partial void LogCircuitBreakerReset(string policyKey);
}

// Internal statistics tracking
