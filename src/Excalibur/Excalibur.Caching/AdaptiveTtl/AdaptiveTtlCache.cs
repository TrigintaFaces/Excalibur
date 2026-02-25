// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;

using Excalibur.Caching.Diagnostics;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace Excalibur.Caching.AdaptiveTtl;

/// <summary>
/// A distributed cache wrapper that automatically adjusts TTL based on access patterns.
/// </summary>
public sealed partial class AdaptiveTtlCache : IDistributedCache, IDisposable, IAsyncDisposable
{
	private static readonly Meter CacheMeter = new(CachingTelemetryConstants.MeterName, CachingTelemetryConstants.Version);
	private static readonly ActivitySource CacheActivitySource = new(CachingTelemetryConstants.ActivitySourceName, CachingTelemetryConstants.Version);

	private static readonly Counter<long> HitCounter =
		CacheMeter.CreateCounter<long>("caching.adaptive_ttl.hits", "hits", "Number of adaptive TTL cache hits");

	private static readonly Counter<long> MissCounter =
		CacheMeter.CreateCounter<long>("caching.adaptive_ttl.misses", "misses", "Number of adaptive TTL cache misses");

	private static readonly Histogram<double> TtlHistogram =
		CacheMeter.CreateHistogram<double>("caching.adaptive_ttl.ttl_seconds", "s", "Adaptive TTL values in seconds");

	private static readonly Counter<long> MetadataCleanupCounter =
		CacheMeter.CreateCounter<long>("caching.adaptive_ttl.metadata_cleanups", "cleanups", "Number of metadata entries cleaned up");

	private readonly IDistributedCache _innerCache;
	private readonly IAdaptiveTtlStrategy _ttlStrategy;
	private readonly ILogger<AdaptiveTtlCache> _logger;
	private readonly ISystemLoadMonitor _loadMonitor;
	private readonly TimeProvider _timeProvider;
	private readonly ConcurrentDictionary<string, CacheEntryMetadata> _metadata;
	private readonly Timer _cleanupTimer;
	private long _lastKnownSystemLoadBits = BitConverter.DoubleToInt64Bits(0.5d);
	private volatile bool _disposed;

	private double LastKnownSystemLoad
	{
		get => BitConverter.Int64BitsToDouble(Interlocked.Read(ref _lastKnownSystemLoadBits));
		set => Interlocked.Exchange(ref _lastKnownSystemLoadBits, BitConverter.DoubleToInt64Bits(value));
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="AdaptiveTtlCache" /> class.
	/// </summary>
	/// <param name="innerCache"> The underlying distributed cache. </param>
	/// <param name="ttlStrategy"> The adaptive TTL strategy to use. </param>
	/// <param name="logger"> The logger instance. </param>
	/// <param name="loadMonitor"> The system load monitor. </param>
	/// <param name="timeProvider"> The time provider for testability. </param>
	public AdaptiveTtlCache(
		IDistributedCache innerCache,
		IAdaptiveTtlStrategy ttlStrategy,
		ILogger<AdaptiveTtlCache> logger,
		ISystemLoadMonitor loadMonitor,
		TimeProvider? timeProvider = null)
	{
		_innerCache = innerCache ?? throw new ArgumentNullException(nameof(innerCache));
		_ttlStrategy = ttlStrategy ?? throw new ArgumentNullException(nameof(ttlStrategy));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_loadMonitor = loadMonitor ?? throw new ArgumentNullException(nameof(loadMonitor));
		_timeProvider = timeProvider ?? TimeProvider.System;
		_metadata = new ConcurrentDictionary<string, CacheEntryMetadata>(StringComparer.Ordinal);

		// Cleanup old metadata every 5 minutes
		_cleanupTimer = new Timer(CleanupMetadata, state: null, dueTime: TimeSpan.FromMinutes(5), period: TimeSpan.FromMinutes(5));
	}

	/// <summary>
	/// Returns simple, aggregate metrics derived from the cached metadata to support diagnostics.
	/// </summary>
	/// <returns> Aggregated metrics for the adaptive TTL cache. </returns>
	public AdaptiveTtlMetrics GetMetrics()
	{
		var result = new AdaptiveTtlMetrics();
		long entries = 0;
		double totalHitRate = 0;

		foreach (var kvp in _metadata.ToArray())
		{
			entries++;
			var meta = kvp.Value;

			// Approximate hit rate and access frequency via extension methods
			var hitRate = meta.GetHitRate();
			var freq = meta.GetAccessFrequency();
			totalHitRate += hitRate;

			result.CustomMetrics["AccessFrequencyPerMin"] = freq;
		}

		result.TotalCalculations = entries;
		result.AverageHitRate = entries > 0 ? totalHitRate / entries : 0d;
		result.AverageAdjustmentFactor = 1.0; // Not tracked at this layer
		return result;
	}

	/// <inheritdoc />
	[SuppressMessage("AsyncUsage", "VSTHRD002:Avoid problematic synchronous waits",
		Justification = "IDistributedCache.Get() is synchronous by interface contract. Prefer GetAsync when possible. (AD-221-3)")]
	public byte[]? Get(string key)
	{
		if (string.IsNullOrEmpty(key))
		{
			throw new ArgumentNullException(nameof(key));
		}

		var startTime = _timeProvider.GetUtcNow();
		var entryMetadata = GetOrCreateMetadata(key);

		try
		{
			var value = _innerCache.Get(key);
			var isHit = value != null;

			if (isHit)
			{
				HitCounter.Add(1);
			}
			else
			{
				MissCounter.Add(1);
			}

			entryMetadata.RecordAccess(isHit);

			var feedback = new CachePerformanceFeedback
			{
				Key = key,
				IsHit = isHit,
				ResponseTime = _timeProvider.GetUtcNow() - startTime,
				CurrentTtl = entryMetadata.LastTtl,
				WasStale = false,
			};

			_ttlStrategy.UpdateStrategy(feedback);
			LogCacheOperation("GET", key, isHit, (long)(_timeProvider.GetUtcNow() - startTime).TotalMilliseconds);

			return value;
		}
		catch (Exception ex)
		{
			LogCacheError(key, ex);
			throw;
		}
	}

	/// <inheritdoc />
	public async Task<byte[]?> GetAsync(string key, CancellationToken token)
	{
		if (string.IsNullOrEmpty(key))
		{
			throw new ArgumentNullException(nameof(key));
		}

		var startTime = _timeProvider.GetUtcNow();
		var entryMetadata = GetOrCreateMetadata(key);

		try
		{
			// Get from inner cache
			var value = await _innerCache.GetAsync(key, token).ConfigureAwait(false);
			var isHit = value != null;

			// Record OTel metrics
			if (isHit)
			{
				HitCounter.Add(1);
			}
			else
			{
				MissCounter.Add(1);
			}

			// Update access statistics
			entryMetadata.RecordAccess(isHit);

			// Provide feedback to strategy
			var feedback = new CachePerformanceFeedback
			{
				Key = key,
				IsHit = isHit,
				ResponseTime = _timeProvider.GetUtcNow() - startTime,
				CurrentTtl = entryMetadata.LastTtl,
				WasStale = false, // Would need additional logic to detect staleness
			};

			_ttlStrategy.UpdateStrategy(feedback);

			LogCacheOperation("GET", key, isHit, (long)(_timeProvider.GetUtcNow() - startTime).TotalMilliseconds);

			return value;
		}
		catch (Exception ex)
		{
			LogCacheError(key, ex);
			throw;
		}
	}

	/// <inheritdoc />
	[SuppressMessage("AsyncUsage", "VSTHRD002:Avoid problematic synchronous waits",
		Justification = "IDistributedCache.Set() is synchronous by interface contract. Prefer SetAsync when possible. (AD-221-3)")]
	public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
	{
		if (string.IsNullOrEmpty(key))
		{
			throw new ArgumentNullException(nameof(key));
		}

		ArgumentNullException.ThrowIfNull(value);
		ArgumentNullException.ThrowIfNull(options);

		var entryMetadata = GetOrCreateMetadata(key);
		var context = BuildAdaptiveTtlContext(key, options, value.Length, entryMetadata);

		var adaptiveTtl = _ttlStrategy.CalculateTtl(context);
		entryMetadata.LastTtl = adaptiveTtl;
		TtlHistogram.Record(adaptiveTtl.TotalSeconds);

		var adaptiveOptions = new DistributedCacheEntryOptions
		{
			AbsoluteExpirationRelativeToNow = adaptiveTtl,
			SlidingExpiration = options.SlidingExpiration,
		};

		if (options.AbsoluteExpirationRelativeToNow.HasValue)
		{
			var originalExpiration = options.AbsoluteExpirationRelativeToNow.Value;
			if (originalExpiration < adaptiveTtl)
			{
				adaptiveOptions.AbsoluteExpirationRelativeToNow = originalExpiration;
			}
		}

		LogSetCacheKey(key, adaptiveTtl, options.AbsoluteExpirationRelativeToNow);
		_innerCache.Set(key, value, adaptiveOptions);
	}

	/// <inheritdoc />
	public async Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token)
	{
		if (string.IsNullOrEmpty(key))
		{
			throw new ArgumentNullException(nameof(key));
		}

		ArgumentNullException.ThrowIfNull(value);

		ArgumentNullException.ThrowIfNull(options);

		var entryMetadata = GetOrCreateMetadata(key);
		var context = await BuildAdaptiveTtlContextAsync(key, options, value.Length, entryMetadata).ConfigureAwait(false);

		// Calculate adaptive TTL
		var adaptiveTtl = _ttlStrategy.CalculateTtl(context);
		entryMetadata.LastTtl = adaptiveTtl;

		TtlHistogram.Record(adaptiveTtl.TotalSeconds);

		// Create new options with adaptive TTL
		var adaptiveOptions = new DistributedCacheEntryOptions
		{
			AbsoluteExpirationRelativeToNow = adaptiveTtl,
			SlidingExpiration = options.SlidingExpiration,
		};

		// If original had absolute expiration, use the shorter of the two
		if (options.AbsoluteExpirationRelativeToNow.HasValue)
		{
			var originalExpiration = options.AbsoluteExpirationRelativeToNow.Value;
			if (originalExpiration < adaptiveTtl)
			{
				adaptiveOptions.AbsoluteExpirationRelativeToNow = originalExpiration;
			}
		}

		LogSetCacheKey(key, adaptiveTtl, options.AbsoluteExpirationRelativeToNow);

		await _innerCache.SetAsync(key, value, adaptiveOptions, token).ConfigureAwait(false);
	}

	/// <inheritdoc />
	[SuppressMessage("AsyncUsage", "VSTHRD002:Avoid problematic synchronous waits",
		Justification = "IDistributedCache.Refresh() is synchronous by interface contract. Prefer RefreshAsync when possible. (AD-221-3)")]
	public void Refresh(string key)
	{
		if (string.IsNullOrEmpty(key))
		{
			throw new ArgumentNullException(nameof(key));
		}

		_innerCache.Refresh(key);

		var entryMetadata = GetOrCreateMetadata(key);
		entryMetadata.RecordAccess(isHit: true);
	}

	/// <inheritdoc />
	public async Task RefreshAsync(string key, CancellationToken token)
	{
		if (string.IsNullOrEmpty(key))
		{
			throw new ArgumentNullException(nameof(key));
		}

		await _innerCache.RefreshAsync(key, token).ConfigureAwait(false);

		// Record refresh as an access
		var entryMetadata = GetOrCreateMetadata(key);
		entryMetadata.RecordAccess(isHit: true);
	}

	/// <inheritdoc />
	[SuppressMessage("AsyncUsage", "VSTHRD002:Avoid problematic synchronous waits",
		Justification = "IDistributedCache.Remove() is synchronous by interface contract. Prefer RemoveAsync when possible. (AD-221-3)")]
	public void Remove(string key)
	{
		if (string.IsNullOrEmpty(key))
		{
			throw new ArgumentNullException(nameof(key));
		}

		_innerCache.Remove(key);
		_ = _metadata.TryRemove(key, out _);
	}

	/// <inheritdoc />
	public async Task RemoveAsync(string key, CancellationToken token)
	{
		if (string.IsNullOrEmpty(key))
		{
			throw new ArgumentNullException(nameof(key));
		}

		await _innerCache.RemoveAsync(key, token).ConfigureAwait(false);

		// Remove metadata
		_ = _metadata.TryRemove(key, out _);
	}

	/// <summary>
	/// Releases resources used by this cache wrapper.
	/// </summary>
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		_cleanupTimer.Dispose();
	}

	/// <summary>
	/// Asynchronously releases resources used by this cache wrapper, ensuring the cleanup timer callback has completed.
	/// </summary>
	public async ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;

		// Disable timer and wait for any in-flight callback to complete
		await _cleanupTimer.DisposeAsync().ConfigureAwait(false);
	}

	private CacheEntryMetadata GetOrCreateMetadata(string key) =>
		_metadata.GetOrAdd(key, static (_, tp) => new CacheEntryMetadata(tp), _timeProvider);

	private async Task<AdaptiveTtlContext> BuildAdaptiveTtlContextAsync(
		string key,
		DistributedCacheEntryOptions options,
		long contentSize,
		CacheEntryMetadata metadata)
	{
		// Get system load
		var systemLoad = await _loadMonitor.GetCurrentLoadAsync().ConfigureAwait(false);
		LastKnownSystemLoad = systemLoad;

		// Calculate base TTL from options
		var baseTtl = options.AbsoluteExpirationRelativeToNow ?? TimeSpan.FromMinutes(5);
		if (options.SlidingExpiration < baseTtl)
		{
			baseTtl = options.SlidingExpiration.Value;
		}

		// Estimate miss cost (this is simplified - real implementation might measure actual costs)
		var missCost = TimeSpan.FromMilliseconds(50); // Default estimate

		return new AdaptiveTtlContext
		{
			Key = key,
			BaseTtl = baseTtl,
			AccessFrequency = metadata.GetAccessFrequency(),
			HitRate = metadata.GetHitRate(),
			LastUpdate = metadata.LastUpdate,
			ContentSize = contentSize,
			MissCost = missCost,
			SystemLoad = systemLoad,
			CurrentTime = _timeProvider.GetUtcNow(),
		};
	}

	private AdaptiveTtlContext BuildAdaptiveTtlContext(
		string key,
		DistributedCacheEntryOptions options,
		long contentSize,
		CacheEntryMetadata metadata)
	{
		var baseTtl = options.AbsoluteExpirationRelativeToNow ?? TimeSpan.FromMinutes(5);
		if (options.SlidingExpiration < baseTtl)
		{
			baseTtl = options.SlidingExpiration.Value;
		}

		var missCost = TimeSpan.FromMilliseconds(50);

		return new AdaptiveTtlContext
		{
			Key = key,
			BaseTtl = baseTtl,
			AccessFrequency = metadata.GetAccessFrequency(),
			HitRate = metadata.GetHitRate(),
			LastUpdate = metadata.LastUpdate,
			ContentSize = contentSize,
			MissCost = missCost,
			SystemLoad = LastKnownSystemLoad,
			CurrentTime = _timeProvider.GetUtcNow(),
		};
	}

	private void CleanupMetadata(object? state)
	{
		try
		{
			var cutoff = _timeProvider.GetUtcNow().AddHours(-1);
			var keysToRemove = new List<string>();

			foreach (var kvp in _metadata.ToArray())
			{
				if (kvp.Value.LastAccess < cutoff)
				{
					keysToRemove.Add(kvp.Key);
				}
			}

			foreach (var key in keysToRemove)
			{
				_ = _metadata.TryRemove(key, out _);
			}

			if (keysToRemove.Count > 0)
			{
				MetadataCleanupCounter.Add(keysToRemove.Count);
				LogMetadataCleanup(keysToRemove.Count);
			}
		}
		catch (Exception ex)
		{
			LogCleanupError(ex);
		}
	}

	/// <summary>
	/// Metadata tracked for each cache entry.
	/// </summary>
	private sealed class CacheEntryMetadata
	{
		/// <summary>
		/// Cleanup runs at most once per this interval to avoid cleanup on every cache hit.
		/// </summary>
		private const int CleanupIntervalSeconds = 60;

#if NET9_0_OR_GREATER

		private readonly Lock _lockObj = new();

#else
		private readonly object _lockObj = new();

#endif
		private readonly Queue<DateTimeOffset> _recentAccesses = new();
		private readonly TimeProvider _timeProvider;

		// Performance optimization: use Interlocked for hot counters
		private long _accessCount;
		private long _hitCount;
		private long _lastAccessTicks;
		private long _lastCleanupTicks;

		public CacheEntryMetadata(TimeProvider timeProvider)
		{
			_timeProvider = timeProvider;
			var now = timeProvider.GetUtcNow();
			_lastAccessTicks = now.UtcTicks;
			_lastCleanupTicks = now.UtcTicks;
			LastUpdate = now;
		}

		public DateTimeOffset LastAccess => new(Interlocked.Read(ref _lastAccessTicks), TimeSpan.Zero);

		public DateTimeOffset LastUpdate { get; }

		public TimeSpan LastTtl { get; set; } = TimeSpan.FromMinutes(5);

		public void RecordAccess(bool isHit)
		{
			var now = _timeProvider.GetUtcNow();

			// Performance optimization: use Interlocked for counters (lock-free hot path)
			Interlocked.Exchange(ref _lastAccessTicks, now.UtcTicks);
			Interlocked.Increment(ref _accessCount);

			if (isHit)
			{
				Interlocked.Increment(ref _hitCount);
			}

			// Enqueue the access timestamp (lock required for Queue<T>)
			lock (_lockObj)
			{
				_recentAccesses.Enqueue(now);
			}

			// Only run cleanup periodically, not on every access.
			// Use Interlocked.CompareExchange to ensure only one thread runs cleanup.
			var lastCleanup = Interlocked.Read(ref _lastCleanupTicks);
			var elapsed = now.UtcTicks - lastCleanup;
			if (elapsed > CleanupIntervalSeconds * TimeSpan.TicksPerSecond)
			{
				if (Interlocked.CompareExchange(ref _lastCleanupTicks, now.UtcTicks, lastCleanup) == lastCleanup)
				{
					CleanupStaleEntries(now);
				}
			}
		}

		public double GetHitRate()
		{
			// Performance optimization: use Volatile reads for lock-free access
			var access = Interlocked.Read(ref _accessCount);
			var hits = Interlocked.Read(ref _hitCount);
			return access > 0 ? (double)hits / access : 0;
		}

		public double GetAccessFrequency()
		{
			lock (_lockObj)
			{
				// Return accesses per minute based on recent history
				return _recentAccesses.Count / 60.0;
			}
		}

		private void CleanupStaleEntries(DateTimeOffset now)
		{
			lock (_lockObj)
			{
				var cutoff = now.AddHours(-1);
				while (_recentAccesses.Count > 0 && _recentAccesses.Peek() < cutoff)
				{
					_ = _recentAccesses.Dequeue();
				}
			}
		}
	}

	// Source-generated logging methods
	[LoggerMessage(CachingEventId.AdaptiveCacheOperation, LogLevel.Debug,
		"Cache {Operation} for key {Key}: hit={Hit}, time={Time}ms")]
	private partial void LogCacheOperation(string operation, string key, bool hit, long time);

	[LoggerMessage(CachingEventId.AdaptiveCacheError, LogLevel.Error,
		"Error getting key {Key} from cache")]
	private partial void LogCacheError(string key, Exception ex);

	[LoggerMessage(CachingEventId.SetCacheKey, LogLevel.Debug,
		"Setting key {Key} with adaptive TTL {Ttl} (original: {OriginalTtl})")]
	private partial void LogSetCacheKey(string key, TimeSpan ttl, TimeSpan? originalTtl);

	[LoggerMessage(CachingEventId.MetadataCleanup, LogLevel.Debug,
		"Cleaned up metadata for {Count} inactive cache keys")]
	private partial void LogMetadataCleanup(int count);

	[LoggerMessage(CachingEventId.CleanupError, LogLevel.Error,
		"Error during metadata cleanup")]
	private partial void LogCleanupError(Exception ex);
}
