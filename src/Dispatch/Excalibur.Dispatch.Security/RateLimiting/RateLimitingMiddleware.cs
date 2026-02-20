// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Security.Diagnostics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using MessageProblemDetails = Excalibur.Dispatch.Abstractions.MessageProblemDetails;

namespace Excalibur.Dispatch.Security;

/// <summary>
/// Middleware that enforces rate limiting on message processing to prevent abuse and ensure fair resource usage.
/// </summary>
/// <remarks>
/// This middleware provides:
/// <list type="bullet">
/// <item> Per-tenant rate limiting with configurable thresholds </item>
/// <item> Multiple rate limiting algorithms (token bucket, sliding window, fixed window) </item>
/// <item> Automatic backpressure when limits are exceeded </item>
/// <item> Burst allowance for temporary spikes </item>
/// <item> Metrics and monitoring integration </item>
/// </list>
/// </remarks>
public sealed partial class RateLimitingMiddleware : IDispatchMiddleware, IDisposable, IAsyncDisposable
{
	private static readonly CompositeFormat UnsupportedAlgorithmFormat =
			CompositeFormat.Parse(Resources.RateLimitingMiddleware_UnsupportedAlgorithmFormat);

	private readonly RateLimitingOptions _options;
	private readonly ILogger<RateLimitingMiddleware> _logger;
	private readonly ConcurrentDictionary<string, RateLimiter> _limiters = new(StringComparer.Ordinal);
	private readonly Timer _cleanupTimer;
	private readonly SemaphoreSlim _cleanupLock = new(1, 1);
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="RateLimitingMiddleware" /> class.
	/// </summary>
	/// <param name="options">The rate limiting options.</param>
	/// <param name="logger">The logger used for diagnostics.</param>
	public RateLimitingMiddleware(
		IOptions<RateLimitingOptions> options,
		ILogger<RateLimitingMiddleware> logger)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_options = options.Value;
		_logger = logger;

		// Start cleanup timer to remove inactive limiters
		_cleanupTimer = new Timer(
			CleanupInactiveLimiters,
			state: null,
			TimeSpan.FromMinutes(_options.CleanupIntervalMinutes),
			TimeSpan.FromMinutes(_options.CleanupIntervalMinutes));
	}

	/// <inheritdoc />
	public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.RateLimiting;

	/// <inheritdoc />
	public MessageKinds ApplicableMessageKinds => MessageKinds.All;

	/// <inheritdoc />
	public async ValueTask<IMessageResult> InvokeAsync(
		IDispatchMessage message,
		IMessageContext context,
		DispatchRequestDelegate nextDelegate,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(message);
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(nextDelegate);

		// Skip rate limiting if disabled
		if (!_options.Enabled)
		{
			return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}

		// Extract tenant/user identifier for rate limiting
		var rateLimitKey = ExtractRateLimitKey(message, context);
		if (string.IsNullOrEmpty(rateLimitKey))
		{
			// No tenant/user identified, apply global rate limit
			rateLimitKey = RateLimitKeyPrefixes.Global;
		}

		// Get or create rate limiter for this key
		var limiter = GetOrCreateLimiter(rateLimitKey);

		// Create activity for tracing
		using var activity = Activity.Current?.Source.StartActivity("RateLimiting.Check");
		_ = (activity?.SetTag("rate_limit.key", rateLimitKey));
		_ = (activity?.SetTag("rate_limit.algorithm", _options.Algorithm.ToString()));

		// Attempt to acquire permit
		using var lease = await limiter.AcquireAsync(1, cancellationToken).ConfigureAwait(false);

		if (lease.IsAcquired)
		{
			return await ProcessPermitAcquiredAsync(rateLimitKey, lease, message, context, nextDelegate, activity, cancellationToken).ConfigureAwait(false);
		}

		// Rate limit exceeded
		return HandleRateLimitExceeded(rateLimitKey, message, lease, activity);
	}

	/// <inheritdoc />
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		_cleanupTimer.Dispose();
		_cleanupLock.Dispose();

		foreach (var limiter in _limiters.Values)
		{
			limiter.Dispose();
		}

		_limiters.Clear();
	}

	/// <inheritdoc />
	public async ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;

		// Wait for any in-flight timer callback to complete
		await _cleanupTimer.DisposeAsync().ConfigureAwait(false);
		_cleanupLock.Dispose();

		foreach (var limiter in _limiters.Values)
		{
			limiter.Dispose();
		}

		_limiters.Clear();
	}

	private static string ExtractRateLimitKey(IDispatchMessage message, IMessageContext context)
	{
		// Try to get tenant ID from context
		if (context.TryGetValue<string>("TenantId", out var tenantId) && tenantId != null && !string.IsNullOrEmpty(tenantId))
		{
			return $"{RateLimitKeyPrefixes.Tenant}{tenantId}";
		}

		// Try to get user ID from context
		if (context.TryGetValue<string>("UserId", out var userId) && userId != null && !string.IsNullOrEmpty(userId))
		{
			return $"{RateLimitKeyPrefixes.User}{userId}";
		}

		// Try to get API key from context
		if (context.TryGetValue<string>("ApiKey", out var apiKey) && apiKey != null && !string.IsNullOrEmpty(apiKey))
		{
			// Hash the API key for security
			var hash = SHA256.HashData(Encoding.UTF8.GetBytes(apiKey));
			return $"{RateLimitKeyPrefixes.ApiKey}{Convert.ToBase64String(hash)[..8]}";
		}

		// Try to get client IP from context
		if (context.TryGetValue<string>("ClientIp", out var clientIp) && clientIp != null && !string.IsNullOrEmpty(clientIp))
		{
			return $"{RateLimitKeyPrefixes.Ip}{clientIp}";
		}

		// Default to message type
		return $"{RateLimitKeyPrefixes.MessageType}{message.GetType().Name}";
	}

	private static RateLimiter CreateLimiterForKey(string key, RateLimitingMiddleware middleware)
	{
		// Get limits for this key
		var limits = middleware.GetLimitsForKey(key);

		// Create limiter based on configured algorithm
		return middleware._options.Algorithm switch
		{
			RateLimitAlgorithm.TokenBucket => new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
			{
				TokenLimit = limits.TokenLimit,
				QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
				QueueLimit = limits.QueueLimit,
				ReplenishmentPeriod = TimeSpan.FromSeconds(limits.ReplenishmentPeriodSeconds),
				TokensPerPeriod = limits.TokensPerPeriod,
				AutoReplenishment = true,
			}),

			RateLimitAlgorithm.SlidingWindow => new SlidingWindowRateLimiter(new SlidingWindowRateLimiterOptions
			{
				PermitLimit = limits.PermitLimit,
				Window = TimeSpan.FromSeconds(limits.WindowSeconds),
				SegmentsPerWindow = limits.SegmentsPerWindow,
				QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
				QueueLimit = limits.QueueLimit,
				AutoReplenishment = true,
			}),

			RateLimitAlgorithm.FixedWindow => new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
			{
				PermitLimit = limits.PermitLimit,
				Window = TimeSpan.FromSeconds(limits.WindowSeconds),
				QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
				QueueLimit = limits.QueueLimit,
				AutoReplenishment = true,
			}),

			RateLimitAlgorithm.Concurrency => new ConcurrencyLimiter(new ConcurrencyLimiterOptions
			{
				PermitLimit = limits.ConcurrencyLimit,
				QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
				QueueLimit = limits.QueueLimit,
			}),

			_ => throw new NotSupportedException(
					string.Format(
							CultureInfo.InvariantCulture,
							UnsupportedAlgorithmFormat,
							middleware._options.Algorithm)),
		};
	}

	private static void RecordRateLimitExceeded(string key, string messageType)
	{
		// Record metrics using Activity API
		var activity = Activity.Current;
		_ = (activity?.AddEvent(new ActivityEvent(
			"RateLimitExceeded",
			DateTimeOffset.UtcNow,
			new ActivityTagsCollection { ["rate_limit.key"] = key, ["message.type"] = messageType })));

		// Could also emit custom metrics here using System.Diagnostics.Metrics
	}

	private async Task<IMessageResult> ProcessPermitAcquiredAsync(
		string rateLimitKey,
		RateLimitLease lease,
		IDispatchMessage message,
		IMessageContext context,
		DispatchRequestDelegate nextDelegate,
		Activity? activity,
		CancellationToken cancellationToken)
	{
		// Permission granted, continue processing
		var remainingInfo = lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
			? $"remaining: {retryAfter}"
			: "remaining: unknown";
		LogPermitAcquired(rateLimitKey, remainingInfo);

		_ = (activity?.SetTag("rate_limit.acquired", value: true));

		try
		{
			return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}
		finally
		{
			// Return the permit if using a replenishing algorithm
			lease.Dispose();
		}
	}

	private RateLimitExceededResult HandleRateLimitExceeded(
		string rateLimitKey,
		IDispatchMessage message,
		RateLimitLease lease,
		Activity? activity)
	{
		_ = (activity?.SetTag("rate_limit.acquired", value: false));

		// Get retry-after if available
		var retryAfterMs = lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfterValue)
			? (int)retryAfterValue.TotalMilliseconds
			: _options.DefaultRetryAfterMilliseconds;

		LogRateLimitExceeded(rateLimitKey, message.GetType().Name, retryAfterMs);

		// Record metrics
		RecordRateLimitExceeded(rateLimitKey, message.GetType().Name);

		// Return rate limit exceeded result
		return new RateLimitExceededResult
		{
			Succeeded = false,
			ProblemDetails = MessageProblemDetails.ValidationError($"Rate limit exceeded. Please retry after {retryAfterMs.ToString(CultureInfo.InvariantCulture)}ms"),
			RetryAfterMilliseconds = retryAfterMs,
			RateLimitKey = rateLimitKey,
		};
	}

	private RateLimiter GetOrCreateLimiter(string key) =>
		_limiters.GetOrAdd(key, CreateLimiterForKey, this);

	private RateLimits GetLimitsForKey(string key)
	{
		// Check for specific tenant limits
		if (key.StartsWith(RateLimitKeyPrefixes.Tenant, StringComparison.OrdinalIgnoreCase))
		{
			var tenantId = key[RateLimitKeyPrefixes.Tenant.Length..];
			if (_options.TenantLimits.TryGetValue(tenantId, out var tenantLimits))
			{
				return tenantLimits;
			}
		}

		// Check for tier-based limits
		if (key.StartsWith(RateLimitKeyPrefixes.Tier, StringComparison.OrdinalIgnoreCase))
		{
			var tier = key[RateLimitKeyPrefixes.Tier.Length..];
			if (_options.TierLimits.TryGetValue(tier, out var tierLimits))
			{
				return tierLimits;
			}
		}

		// Return default limits
		return _options.DefaultLimits;
	}

	private void CleanupInactiveLimiters(object? state)
	{
		// Use non-blocking approach - only proceed if we can immediately acquire the lock
		if (_cleanupLock.CurrentCount == 0 || !_cleanupLock.Wait(TimeSpan.Zero))
		{
			return; // Skip if cleanup is already running or can't acquire immediately
		}

		try
		{
			var keysToRemove = new List<string>();
			var cutoffTime = DateTimeOffset.UtcNow.AddMinutes(-_options.InactivityTimeoutMinutes);

			foreach (var kvp in _limiters)
			{
				// Check if limiter has been inactive
				// Note: This is simplified; in production you'd track last access time
				if (kvp.Value.GetStatistics()?.CurrentAvailablePermits == kvp.Value.GetStatistics()?.TotalSuccessfulLeases)
				{
					keysToRemove.Add(kvp.Key);
				}
			}

			foreach (var key in keysToRemove)
			{
				if (_limiters.TryRemove(key, out var limiter))
				{
					limiter.Dispose();
					LogInactiveLimiterRemoved(key);
				}
			}

			if (keysToRemove.Count > 0)
			{
				LogCleanupCompleted(keysToRemove.Count);
			}
		}
		catch (Exception ex)
		{
			LogCleanupError(ex);
		}
		finally
		{
			_ = _cleanupLock.Release();
		}
	}

	// Source-generated logging methods
	[LoggerMessage(SecurityEventId.RateLimitPermitAcquired, LogLevel.Debug, "Rate limit permit acquired for {RateLimitKey}. {RemainingInfo}")]
	private partial void LogPermitAcquired(string rateLimitKey, string remainingInfo);

	[LoggerMessage(SecurityEventId.RateLimitExceeded, LogLevel.Warning, "Rate limit exceeded for {RateLimitKey} (message type: {MessageType}). Retry after {RetryAfterMs}ms")]
	private partial void LogRateLimitExceeded(string rateLimitKey, string messageType, int retryAfterMs);

	[LoggerMessage(SecurityEventId.RateLimitInactiveLimiterRemoved, LogLevel.Debug, "Removed inactive rate limiter for {Key}")]
	private partial void LogInactiveLimiterRemoved(string key);

	[LoggerMessage(SecurityEventId.RateLimitCleanupCompleted, LogLevel.Information, "Cleaned up {Count} inactive rate limiters")]
	private partial void LogCleanupCompleted(int count);

	[LoggerMessage(SecurityEventId.RateLimitCleanupError, LogLevel.Error, "Error during rate limiter cleanup")]
	private partial void LogCleanupError(Exception ex);
}
