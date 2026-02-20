// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Delivery.Handlers;
using Excalibur.Dispatch.Delivery.Pipeline;
using Excalibur.Dispatch.Diagnostics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Performance;

/// <summary>
/// Default implementation of <see cref="IDispatchCacheManager"/> that coordinates
/// the freezing of all Dispatch performance caches.
/// </summary>
/// <remarks>
/// <para>
/// PERF-22: This implementation centralizes cache management for the Dispatch framework.
/// It tracks the freeze timestamp and provides status visibility for diagnostics.
/// </para>
/// </remarks>
public sealed partial class DispatchCacheManager : IDispatchCacheManager
{
	/// <summary>
	/// Default maximum time to wait for the freeze lock.
	/// </summary>
	private static readonly TimeSpan DefaultFreezeLockTimeout = TimeSpan.FromSeconds(30);

	private readonly ILogger<DispatchCacheManager> _logger;
	private readonly TimeSpan _freezeLockTimeout;
	private readonly object _freezeLock = new();
	private DateTimeOffset? _frozenAt;

	/// <summary>
	/// Initializes a new instance of the <see cref="DispatchCacheManager"/> class.
	/// </summary>
	/// <param name="logger">Optional logger for cache operations.</param>
	/// <param name="freezeLockTimeout">
	/// Maximum time to wait for the freeze lock. If exceeded, a warning is logged and the operation
	/// is skipped without throwing. Defaults to 30 seconds.
	/// </param>
	public DispatchCacheManager(ILogger<DispatchCacheManager>? logger = null, TimeSpan? freezeLockTimeout = null)
	{
		_logger = logger ?? NullLogger<DispatchCacheManager>.Instance;
		_freezeLockTimeout = freezeLockTimeout ?? DefaultFreezeLockTimeout;
	}

	/// <inheritdoc />
	public bool IsFrozen => GetStatus().AllFrozen;

	/// <inheritdoc />
	public CacheFreezeStatus GetStatus()
	{
		return new CacheFreezeStatus(
			HandlerInvokerFrozen: HandlerInvoker.IsCacheFrozen,
			HandlerRegistryFrozen: HandlerInvokerRegistry.IsCacheFrozen,
			HandlerActivatorFrozen: HandlerActivator.IsCacheFrozen,
			ResultFactoryFrozen: FinalDispatchHandler.IsResultFactoryCacheFrozen,
			MiddlewareEvaluatorFrozen: MiddlewareApplicabilityEvaluator.IsCacheFrozen,
			FrozenAt: _frozenAt);
	}

	/// <inheritdoc />
	public void FreezeAll()
	{
		var status = GetStatus();
		if (status.AllFrozen)
		{
			LogAlreadyFrozen(_frozenAt);
			return;
		}

		var lockTaken = false;
		try
		{
			Monitor.TryEnter(_freezeLock, _freezeLockTimeout, ref lockTaken);
			if (!lockTaken)
			{
				LogFreezeLockTimeout(_freezeLockTimeout.TotalSeconds);
				return;
			}

			// Double-check after acquiring lock
			status = GetStatus();
			if (status.AllFrozen)
			{
				LogAlreadyFrozen(_frozenAt);
				return;
			}

			LogFreezing();

			// Freeze all handler-related caches
			if (!status.HandlerInvokerFrozen)
			{
				HandlerInvoker.FreezeCache();
				LogCacheFrozen("HandlerInvoker");
			}

			if (!status.HandlerRegistryFrozen)
			{
				HandlerInvokerRegistry.FreezeCache();
				LogCacheFrozen("HandlerInvokerRegistry");
			}

			if (!status.HandlerActivatorFrozen)
			{
				HandlerActivator.FreezeCache();
				LogCacheFrozen("HandlerActivator");
			}

			// Freeze result factory cache
			if (!status.ResultFactoryFrozen)
			{
				FinalDispatchHandler.FreezeResultFactoryCache();
				LogCacheFrozen("ResultFactory");
			}

			// Freeze middleware metadata cache
			if (!status.MiddlewareEvaluatorFrozen)
			{
				MiddlewareApplicabilityEvaluator.FreezeCache();
				LogCacheFrozen("MiddlewareApplicabilityEvaluator");
			}

			_frozenAt = DateTimeOffset.UtcNow;
			LogFreezeComplete(_frozenAt);
		}
		finally
		{
			if (lockTaken)
			{
				Monitor.Exit(_freezeLock);
			}
		}
	}

	[LoggerMessage(PerformanceEventId.CachesAlreadyFrozen, LogLevel.Debug,
		"Dispatch caches already frozen at {FrozenAt}")]
	private partial void LogAlreadyFrozen(DateTimeOffset? frozenAt);

	[LoggerMessage(PerformanceEventId.CachesFreezing, LogLevel.Information,
		"Freezing Dispatch caches for optimized production performance")]
	private partial void LogFreezing();

	[LoggerMessage(PerformanceEventId.CacheFrozen, LogLevel.Debug,
		"Frozen {CacheName} cache")]
	private partial void LogCacheFrozen(string cacheName);

	[LoggerMessage(PerformanceEventId.CachesFreezeComplete, LogLevel.Information,
		"All Dispatch caches frozen successfully at {FrozenAt}")]
	private partial void LogFreezeComplete(DateTimeOffset? frozenAt);

	[LoggerMessage(PerformanceEventId.CacheFreezeLockTimeout, LogLevel.Warning,
		"Failed to acquire freeze lock within {TimeoutSeconds} seconds. " +
		"This may indicate a deadlock or a long-running freeze operation. Skipping freeze.")]
	private partial void LogFreezeLockTimeout(double timeoutSeconds);
}
