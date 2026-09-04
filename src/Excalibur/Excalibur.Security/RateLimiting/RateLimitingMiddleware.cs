// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;

using Excalibur.Dispatch;
using Excalibur.Security.Diagnostics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using MessageProblemDetails = Excalibur.Dispatch.MessageProblemDetails;

namespace Excalibur.Security;

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
/// <para>
/// Per-key limiters are held by a <see cref="PartitionedRateLimiter{TResource}" />. A partition is created on first use of a key and
/// released once that key has been idle, so a caller who rotates identifiers (client IP, API key) cannot grow the limiter set without
/// bound. Because an idle partition is released, a key that stops sending for longer than the idle window starts again with a full
/// budget.
/// </para>
/// </remarks>
public sealed partial class RateLimitingMiddleware : IDispatchMiddleware, IDisposable, IAsyncDisposable
{
	private static readonly CompositeFormat UnsupportedAlgorithmFormat =
			CompositeFormat.Parse(Resources.RateLimitingMiddleware_UnsupportedAlgorithmFormat);

	private readonly RateLimitingOptions _options;
	private readonly ILogger<RateLimitingMiddleware> _logger;
	private readonly PartitionedRateLimiter<string> _limiter;
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
		_limiter = PartitionedRateLimiter.Create<string, string>(CreatePartition, StringComparer.Ordinal);
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

		// Create activity for tracing
		using var activity = Activity.Current?.Source.StartActivity("RateLimiting.Check");
		_ = (activity?.SetTag("rate_limit.key", rateLimitKey));
		_ = (activity?.SetTag("rate_limit.algorithm", _options.Algorithm.ToString()));

		// Attempt to acquire permit
		using var lease = await _limiter.AcquireAsync(rateLimitKey, 1, cancellationToken).ConfigureAwait(false);

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
		_limiter.Dispose();
	}

	/// <inheritdoc />
	public async ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		await _limiter.DisposeAsync().ConfigureAwait(false);
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

	private RateLimitPartition<string> CreatePartition(string key)
	{
		// Limits are resolved once per partition, from the same tenant/tier lookup the hand-rolled
		// dictionary used, so per-key configuration survives the move to the framework limiter.
		var limits = GetLimitsForKey(key);

		return _options.Algorithm switch
		{
			RateLimitAlgorithm.TokenBucket => RateLimitPartition.GetTokenBucketLimiter(key, _ => new TokenBucketRateLimiterOptions
			{
				TokenLimit = limits.TokenLimit,
				QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
				QueueLimit = limits.QueueLimit,
				ReplenishmentPeriod = TimeSpan.FromSeconds(limits.ReplenishmentPeriodSeconds),
				TokensPerPeriod = limits.TokensPerPeriod,
				AutoReplenishment = true,
			}),

			RateLimitAlgorithm.SlidingWindow => RateLimitPartition.GetSlidingWindowLimiter(key, _ => new SlidingWindowRateLimiterOptions
			{
				PermitLimit = limits.PermitLimit,
				Window = TimeSpan.FromSeconds(limits.WindowSeconds),
				SegmentsPerWindow = limits.SegmentsPerWindow,
				QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
				QueueLimit = limits.QueueLimit,
				AutoReplenishment = true,
			}),

			RateLimitAlgorithm.FixedWindow => RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
			{
				PermitLimit = limits.PermitLimit,
				Window = TimeSpan.FromSeconds(limits.WindowSeconds),
				QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
				QueueLimit = limits.QueueLimit,
				AutoReplenishment = true,
			}),

			RateLimitAlgorithm.Concurrency => RateLimitPartition.GetConcurrencyLimiter(key, _ => new ConcurrencyLimiterOptions
			{
				PermitLimit = limits.ConcurrencyLimit,
				QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
				QueueLimit = limits.QueueLimit,
			}),

			_ => throw new NotSupportedException(
					string.Format(
							CultureInfo.InvariantCulture,
							UnsupportedAlgorithmFormat,
							_options.Algorithm)),
		};
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

	// Source-generated logging methods
	[LoggerMessage(SecurityEventId.RateLimitPermitAcquired, LogLevel.Debug, "Rate limit permit acquired for {RateLimitKey}. {RemainingInfo}")]
	private partial void LogPermitAcquired(string rateLimitKey, string remainingInfo);

	[LoggerMessage(SecurityEventId.RateLimitExceeded, LogLevel.Warning, "Rate limit exceeded for {RateLimitKey} (message type: {MessageType}). Retry after {RetryAfterMs}ms")]
	private partial void LogRateLimitExceeded(string rateLimitKey, string messageType, int retryAfterMs);
}
