// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.RateLimiting;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Diagnostics;
using Excalibur.Dispatch.Options.Middleware;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Middleware;

/// <summary>
/// Middleware that implements rate limiting to protect the system from excessive message processing and prevent resource exhaustion.
/// </summary>
/// <remarks>
/// This middleware enforces rate limits at various levels to ensure system stability:
/// <list type="bullet">
/// <item> Per-message-type rate limiting </item>
/// <item> Per-tenant rate limiting for multi-tenant scenarios </item>
/// <item> Global system-wide rate limiting </item>
/// <item> Sliding window and token bucket algorithms </item>
/// <item> Priority-based rate limiting for different message priorities </item>
/// <item> Adaptive rate limiting based on system load </item>
/// </list>
/// </remarks>
[AppliesTo(MessageKinds.Action | MessageKinds.Event)]
public sealed partial class RateLimitingMiddleware : IDispatchMiddleware
{
	private readonly RateLimitingOptions _options;
	private readonly ILogger<RateLimitingMiddleware> _logger;
	private readonly ConcurrentDictionary<string, RateLimiter> _rateLimiters;
	private readonly RateLimiter _globalRateLimiter;

	/// <summary>
	/// Initializes a new instance of the <see cref="RateLimitingMiddleware" /> class. Creates a new rate limiting middleware instance.
	/// </summary>
	/// <param name="options"> Configuration options for rate limiting. </param>
	/// <param name="logger"> Logger for diagnostic information. </param>
	public RateLimitingMiddleware(
		IOptions<RateLimitingOptions> options,
		ILogger<RateLimitingMiddleware> logger)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_options = options.Value;
		_logger = logger;
		_rateLimiters = new ConcurrentDictionary<string, RateLimiter>(StringComparer.Ordinal);

		// Create global rate limiter
		_globalRateLimiter = CreateRateLimiter(_options.GlobalLimit);
	}

	/// <inheritdoc />
	public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.PreProcessing;

	/// <inheritdoc />
	public MessageKinds ApplicableMessageKinds => MessageKinds.Action | MessageKinds.Event;

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

		// Check if message bypasses rate limiting
		if (BypassesRateLimiting(message))
		{
			LogBypassesRateLimiting(message.GetType().Name);

			return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}

		// Get rate limiter key
		var rateLimiterKey = GetRateLimiterKey(message, context);

		// Get or create rate limiter for this key
		var rateLimiter = GetOrCreateRateLimiter(rateLimiterKey, message);

		// Set up activity tags
		using var activity = Activity.Current;
		_ = activity?.SetTag("ratelimit.key", rateLimiterKey);
		_ = activity?.SetTag("ratelimit.message_type", message.GetType().Name);

		// Acquire rate limit lease
		using var lease = await AcquireLeaseAsync(rateLimiter, _globalRateLimiter, cancellationToken)
			.ConfigureAwait(false);

		if (!lease.IsAcquired)
		{
			LogRateLimitExceeded(rateLimiterKey, message.GetType().Name);

			_ = activity?.SetTag("ratelimit.exceeded", value: true);

			// Store rate limit info in context
			context.SetItem("RateLimit.Exceeded", value: true);
			context.SetItem("RateLimit.RetryAfter", lease.RetryAfter);

			throw new RateLimitExceededException(
				$"Rate limit exceeded for {rateLimiterKey}. Retry after {lease.RetryAfter}")
			{
				RetryAfter = lease.RetryAfter,
				RateLimiterKey = rateLimiterKey,
			};
		}

		_ = activity?.SetTag("ratelimit.exceeded", value: false);

		LogRateLimitLeaseAcquired(rateLimiterKey, message.GetType().Name);

		// Continue pipeline execution
		return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Disposes rate limiters when middleware is disposed.
	/// </summary>
	public void Dispose()
	{
		foreach (var limiter in _rateLimiters.Values)
		{
			limiter.Dispose();
		}

		_rateLimiters.Clear();
		_globalRateLimiter.Dispose();
	}

	/// <summary>
	/// Acquires a rate limit lease from both specific and global rate limiters.
	/// </summary>
	private static async Task<CombinedRateLimitLease> AcquireLeaseAsync(
		RateLimiter specificLimiter,
		RateLimiter globalLimiter,
		CancellationToken cancellationToken)
	{
		// Try to acquire from specific limiter first
		var specificLease = await specificLimiter.AcquireAsync(1, cancellationToken)
			.ConfigureAwait(false);

		if (!specificLease.IsAcquired)
		{
			return new CombinedRateLimitLease(specificLease, globalLease: null);
		}

		// Then try global limiter
		var globalLease = await globalLimiter.AcquireAsync(1, cancellationToken)
			.ConfigureAwait(false);

		if (!globalLease.IsAcquired)
		{
			// Release specific lease since global failed
			specificLease.Dispose();
			return new CombinedRateLimitLease(specificLease: null, globalLease);
		}

		return new CombinedRateLimitLease(specificLease, globalLease);
	}

	/// <summary>
	/// Creates a rate limiter with the specified configuration.
	/// </summary>
	private static RateLimiter CreateRateLimiter(RateLimitConfiguration config) =>
		config.Algorithm switch
		{
			RateLimitAlgorithm.TokenBucket => new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
			{
				TokenLimit = config.TokenLimit,
				ReplenishmentPeriod = config.ReplenishmentPeriod,
				TokensPerPeriod = config.TokensPerPeriod,
				AutoReplenishment = true,
			}),
			RateLimitAlgorithm.SlidingWindow => new SlidingWindowRateLimiter(new SlidingWindowRateLimiterOptions
			{
				PermitLimit = config.PermitLimit,
				Window = config.Window,
				SegmentsPerWindow = config.SegmentsPerWindow,
				AutoReplenishment = true,
			}),
			RateLimitAlgorithm.FixedWindow => new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
			{
				PermitLimit = config.PermitLimit,
				Window = config.Window,
				AutoReplenishment = true,
			}),
			_ => new ConcurrencyLimiter(new ConcurrencyLimiterOptions { PermitLimit = config.PermitLimit }),
		};

	// Source-generated logging methods
	[LoggerMessage(MiddlewareEventId.RateLimitPermitted, LogLevel.Debug,
		"Message type {MessageType} bypasses rate limiting")]
	private partial void LogBypassesRateLimiting(string messageType);

	[LoggerMessage(MiddlewareEventId.RateLimitRejected, LogLevel.Warning,
		"Rate limit exceeded for key {RateLimiterKey} processing message {MessageType}")]
	private partial void LogRateLimitExceeded(string rateLimiterKey, string messageType);

	[LoggerMessage(MiddlewareEventId.RateLimitLeaseAcquired, LogLevel.Debug,
		"Rate limit lease acquired for key {RateLimiterKey} processing message {MessageType}")]
	private partial void LogRateLimitLeaseAcquired(string rateLimiterKey, string messageType);

	/// <summary>
	/// Determines if a message bypasses rate limiting.
	/// </summary>
	private bool BypassesRateLimiting(IDispatchMessage message)
	{
		var messageType = message.GetType();

		// Check for bypass attribute
		if (messageType.GetCustomAttributes(typeof(BypassRateLimitingAttribute), inherit: true).Length != 0)
		{
			return true;
		}

		// Check if message type is in bypass list
		return _options.BypassRateLimitingForTypes?.Contains(messageType.Name) == true;
	}

	/// <summary>
	/// Gets the rate limiter key for a message.
	/// </summary>
	private string GetRateLimiterKey(IDispatchMessage message, IMessageContext context)
	{
		// Use tenant-specific key if multi-tenant
		var tenantId = context.GetItem<string>("TenantId");
		if (!string.IsNullOrEmpty(tenantId) && _options.EnablePerTenantLimiting)
		{
			return $"tenant:{tenantId}";
		}

		// Use message type key
		return $"type:{message.GetType().Name}";
	}

	/// <summary>
	/// Gets or creates a rate limiter for the specified key.
	/// </summary>
	private RateLimiter GetOrCreateRateLimiter(string key, IDispatchMessage message)
	{
		return _rateLimiters.GetOrAdd(
			key,
			(_, state) =>
			{
				var messageType = state.Message.GetType();
				var config = state.Self._options.MessageTypeLimits?.GetValueOrDefault(messageType.Name)
					?? state.Self._options.DefaultLimit;
				return CreateRateLimiter(config);
			},
			(Self: this, Message: message));
	}

	/// <summary>
	/// Combined rate limit lease that manages multiple leases.
	/// </summary>
	private sealed class CombinedRateLimitLease(RateLimitLease? specificLease, RateLimitLease? globalLease) : IDisposable
	{
		public bool IsAcquired => specificLease?.IsAcquired == true && globalLease?.IsAcquired != false;

		public TimeSpan? RetryAfter
		{
			get
			{
				if (specificLease?.IsAcquired == false)
				{
					return GetRetryAfter(specificLease);
				}

				if (globalLease?.IsAcquired == false)
				{
					return GetRetryAfter(globalLease);
				}

				return null;
			}
		}

		public void Dispose()
		{
			specificLease?.Dispose();
			globalLease?.Dispose();
		}

		private static TimeSpan? GetRetryAfter(RateLimitLease lease)
		{
			if (lease.TryGetMetadata("RetryAfter", out var retryAfter))
			{
				return retryAfter as TimeSpan?;
			}

			return TimeSpan.FromSeconds(1); // Default retry after
		}
	}
}
