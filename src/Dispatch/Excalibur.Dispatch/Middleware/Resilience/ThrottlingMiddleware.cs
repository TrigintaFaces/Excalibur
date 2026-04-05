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

namespace Excalibur.Dispatch.Middleware.Resilience;

/// <summary>
/// Middleware that implements throughput throttling to protect the system from excessive message processing and prevent resource exhaustion.
/// </summary>
/// <remarks>
/// <para>
/// This middleware provides system-level throughput protection using per-message-type, per-tenant,
/// and global rate limiting with multiple algorithms (token bucket, sliding window, fixed window, concurrency).
/// </para>
/// <para>
/// For identity-based abuse prevention (per-user, per-API-key, per-IP rate limiting),
/// see <c>Excalibur.Dispatch.Security.RateLimitingMiddleware</c> instead.
/// </para>
/// </remarks>
[AppliesTo(MessageKinds.Action | MessageKinds.Event)]
public sealed partial class ThrottlingMiddleware : IDispatchMiddleware, IAsyncDisposable, IDisposable
{
	/// <summary>
	/// Maximum number of per-key rate limiters to cache. Prevents unbounded memory growth
	/// under sustained load with many distinct keys. When the cap is reached, new keys
	/// bypass per-key limiting and fall through to the global limiter only.
	/// </summary>
	private const int MaxPerKeyLimiters = 1024;

	private readonly RateLimitingOptions _options;
	private readonly ILogger<ThrottlingMiddleware> _logger;
	private readonly ConcurrentDictionary<string, RateLimiter> _rateLimiters;
	private readonly RateLimiter _globalRateLimiter;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="ThrottlingMiddleware" /> class.
	/// </summary>
	public ThrottlingMiddleware(
		IOptions<RateLimitingOptions> options,
		ILogger<ThrottlingMiddleware> logger)
	{
		ArgumentNullException.ThrowIfNull(options);
		ArgumentNullException.ThrowIfNull(logger);

		_options = options.Value;
		_logger = logger;
		_rateLimiters = new ConcurrentDictionary<string, RateLimiter>(StringComparer.Ordinal);
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

		if (!_options.Enabled)
		{
			return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}

		if (BypassesRateLimiting(message))
		{
			LogBypassesRateLimiting(message.GetType().Name);
			return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
		}

		var rateLimiterKey = GetRateLimiterKey(message, context);
		var rateLimiter = GetOrCreateRateLimiter(rateLimiterKey, message);

		using var activity = Activity.Current;
		_ = activity?.SetTag("ratelimit.key", rateLimiterKey);
		_ = activity?.SetTag("ratelimit.message_type", message.GetType().Name);

		using var lease = await AcquireLeaseAsync(rateLimiter, _globalRateLimiter, cancellationToken)
			.ConfigureAwait(false);

		if (!lease.IsAcquired)
		{
			LogRateLimitExceeded(rateLimiterKey, message.GetType().Name);
			_ = activity?.SetTag("ratelimit.exceeded", value: true);

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

		return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
	}

	/// <summary>
	/// Disposes all rate limiters synchronously.
	/// </summary>
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;

		foreach (var limiter in _rateLimiters.Values)
		{
			limiter.Dispose();
		}

		_rateLimiters.Clear();
		_globalRateLimiter.Dispose();
	}

	/// <summary>
	/// Disposes all rate limiters asynchronously.
	/// </summary>
	public ValueTask DisposeAsync()
	{
		Dispose();
		return ValueTask.CompletedTask;
	}

	private static async Task<CombinedRateLimitLease> AcquireLeaseAsync(
		RateLimiter specificLimiter,
		RateLimiter globalLimiter,
		CancellationToken cancellationToken)
	{
		var specificLease = await specificLimiter.AcquireAsync(1, cancellationToken)
			.ConfigureAwait(false);

		if (!specificLease.IsAcquired)
		{
			return new CombinedRateLimitLease(specificLease, globalLease: null);
		}

		var globalLease = await globalLimiter.AcquireAsync(1, cancellationToken)
			.ConfigureAwait(false);

		if (!globalLease.IsAcquired)
		{
			specificLease.Dispose();
			return new CombinedRateLimitLease(specificLease: null, globalLease);
		}

		return new CombinedRateLimitLease(specificLease, globalLease);
	}

	private static RateLimiter CreateRateLimiter(RateLimitOptions config) =>
		config.Algorithm switch
		{
			MiddlewareRateLimitAlgorithm.TokenBucket => new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
			{
				TokenLimit = config.TokenLimit,
				ReplenishmentPeriod = config.ReplenishmentPeriod,
				TokensPerPeriod = config.TokensPerPeriod,
				AutoReplenishment = true,
			}),
			MiddlewareRateLimitAlgorithm.SlidingWindow => new SlidingWindowRateLimiter(new SlidingWindowRateLimiterOptions
			{
				PermitLimit = config.PermitLimit,
				Window = config.Window,
				SegmentsPerWindow = config.SegmentsPerWindow,
				AutoReplenishment = true,
			}),
			MiddlewareRateLimitAlgorithm.FixedWindow => new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
			{
				PermitLimit = config.PermitLimit,
				Window = config.Window,
				AutoReplenishment = true,
			}),
			_ => new ConcurrencyLimiter(new ConcurrencyLimiterOptions { PermitLimit = config.PermitLimit }),
		};

	[LoggerMessage(MiddlewareEventId.RateLimitPermitted, LogLevel.Debug,
		"Message type {MessageType} bypasses rate limiting")]
	private partial void LogBypassesRateLimiting(string messageType);

	[LoggerMessage(MiddlewareEventId.RateLimitRejected, LogLevel.Warning,
		"Rate limit exceeded for key {RateLimiterKey} processing message {MessageType}")]
	private partial void LogRateLimitExceeded(string rateLimiterKey, string messageType);

	[LoggerMessage(MiddlewareEventId.RateLimitLeaseAcquired, LogLevel.Debug,
		"Rate limit lease acquired for key {RateLimiterKey} processing message {MessageType}")]
	private partial void LogRateLimitLeaseAcquired(string rateLimiterKey, string messageType);

	private bool BypassesRateLimiting(IDispatchMessage message)
	{
		var messageType = message.GetType();

		if (messageType.GetCustomAttributes(typeof(BypassRateLimitingAttribute), inherit: true).Length != 0)
		{
			return true;
		}

		return _options.BypassRateLimitingForTypes?.Contains(messageType.Name) == true;
	}

	private string GetRateLimiterKey(IDispatchMessage message, IMessageContext context)
	{
		var tenantId = context.GetItem<string>("TenantId");
		if (!string.IsNullOrEmpty(tenantId) && _options.EnablePerTenantLimiting)
		{
			return $"tenant:{tenantId}";
		}

		return $"type:{message.GetType().Name}";
	}

	private RateLimiter GetOrCreateRateLimiter(string key, IDispatchMessage message)
	{
		// Bounded cache pattern (S543): skip caching when full to prevent unbounded memory growth.
		// Falls through to the global rate limiter only for uncached keys.
		if (_rateLimiters.Count >= MaxPerKeyLimiters && !_rateLimiters.ContainsKey(key))
		{
			// Return the global limiter as a fallback when the per-key cache is full.
			// The global limiter still applies, so the system remains protected.
			return _globalRateLimiter;
		}

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

			return TimeSpan.FromSeconds(1);
		}
	}
}
