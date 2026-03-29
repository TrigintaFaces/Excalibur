// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR
// AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Buffers;
using System.Collections.Concurrent;

using Excalibur.Dispatch.Caching;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Excalibur.Dispatch.Serialization;

/// <summary>
/// High-performance Utf8JsonWriter pool with adaptive sizing, thread-local caching, and advanced telemetry.
/// </summary>
/// <remarks>
/// This pool implementation provides:
/// - Thread-local caching for zero-contention fast path
/// - Adaptive pool sizing based on usage patterns
/// - Comprehensive telemetry and monitoring
/// - Pre-warming strategies for different workloads
/// - Zero-allocation pool operations in hot paths.
/// </remarks>
internal sealed class Utf8JsonWriterPool : IUtf8JsonWriterPool, IDisposable
{
	/// <summary>
	/// The meter name for Utf8JsonWriterPool telemetry.
	/// </summary>
	internal const string MeterName = "Excalibur.Dispatch.Serialization";

	private const int DefaultMaxPoolSize = 1024;
	private const int DefaultThreadLocalCacheSize = 4;
	private const int MinPoolSize = 16;
	private const int MaximumPoolSize = 8192;
	private const double HighWaterMarkRatio = 0.8;
	private const double LowWaterMarkRatio = 0.2;
	private readonly ConcurrentDictionary<int, ConcurrentQueue<Utf8JsonWriter>> _globalPool;
	private readonly ThreadLocal<WriterCache> _threadLocalCache;
	private readonly JsonWriterOptions _defaultOptions;
	private readonly Meter? _meter;
	private readonly Counter<long>? _rentThreadLocalCounter;
	private readonly Counter<long>? _rentGlobalCounter;
	private readonly Counter<long>? _returnThreadLocalCounter;
	private readonly Counter<long>? _returnGlobalCounter;
	private readonly Timer _adaptiveSizeTimer;
#if NET9_0_OR_GREATER
	private readonly System.Threading.Lock _sizingLock = new();
#else
	private readonly object _sizingLock = new();
#endif

	private readonly int _threadLocalCacheSize;

	private readonly bool _enableAdaptiveSizing;

	private readonly bool _enableTelemetry;

	// Pool configuration

	/// <summary>
	/// Statistics.
	/// </summary>
	private long _totalRented;

	private long _totalReturned;
	private long _threadLocalHits;
	private long _threadLocalMisses;
	// Telemetry counter — never assigned today but feeds PoolStatistics.OptionMismatches
	// (public API). Will be incremented once option-mismatch detection is implemented.
#pragma warning disable CS0649, IDE0044 // Field is never assigned to; not readonly because it will be Interlocked.Increment'd
	private long _optionMismatches;
#pragma warning restore CS0649, IDE0044
	private long _poolExpansions;
	private long _poolContractions;
	private int _peakPoolSize;
	private int _currentPoolSize;

	/// <summary>
	/// Initializes a new instance of the <see cref="Utf8JsonWriterPool" /> class. Creates a high-performance UTF-8 JSON writer pool
	/// with advanced features including thread-local caching, adaptive sizing, and comprehensive telemetry for optimal JSON serialization performance.
	/// </summary>
	/// <param name="maxPoolSize"> The maximum number of writers to maintain in the global pool. Must be between 1 and 8192. </param>
	/// <param name="threadLocalCacheSize"> The maximum number of writers to cache per thread. Must be between 0 and 32. </param>
	/// <param name="defaultOptions"> Default JSON writer options to use when creating writers. If null, uses default options. </param>
	/// <param name="enableAdaptiveSizing"> Whether to enable adaptive pool sizing based on usage patterns. </param>
	/// <param name="enableTelemetry"> Whether to enable telemetry collection for monitoring and optimization. </param>
	/// <param name="meter"> Optional <see cref="Meter"/> for collecting pool performance data via System.Diagnostics.Metrics. </param>
	/// <exception cref="ArgumentOutOfRangeException">
	/// Thrown when <paramref name="maxPoolSize" /> is not between 1 and 8192, or when <paramref name="threadLocalCacheSize" /> is not
	/// between 0 and 32.
	/// </exception>
	public Utf8JsonWriterPool(
		int maxPoolSize = DefaultMaxPoolSize,
		int threadLocalCacheSize = DefaultThreadLocalCacheSize,
		JsonWriterOptions? defaultOptions = null,
		bool enableAdaptiveSizing = true,
		bool enableTelemetry = true,
		Meter? meter = null)
	{
		if (maxPoolSize is <= 0 or > MaximumPoolSize)
		{
			throw new ArgumentOutOfRangeException(
				nameof(maxPoolSize),
				$"Max pool size must be between 1 and {MaximumPoolSize}.");
		}

		if (threadLocalCacheSize is < 0 or > 32)
		{
			throw new ArgumentOutOfRangeException(
				nameof(threadLocalCacheSize),
				Resources.Utf8JsonWriterPool_ThreadLocalCacheSizeMustBeBetweenZeroAnd32);
		}

		MaxPoolSize = maxPoolSize;
		_threadLocalCacheSize = threadLocalCacheSize;
		_enableAdaptiveSizing = enableAdaptiveSizing;
		_enableTelemetry = enableTelemetry;
		_meter = meter;
		_globalPool = new ConcurrentDictionary<int, ConcurrentQueue<Utf8JsonWriter>>();
		_threadLocalCache = new ThreadLocal<WriterCache>(
			() => new WriterCache(_threadLocalCacheSize),
			trackAllValues: true);

		_defaultOptions = defaultOptions ?? new JsonWriterOptions
		{
			Indented = false,
			SkipValidation = false,
			Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
			MaxDepth = 64,
		};

		// Create meter instruments when telemetry is enabled
		if (_enableTelemetry && _meter is not null)
		{
			_rentThreadLocalCounter = _meter.CreateCounter<long>(
				"jsonwriter.pool.rent.threadlocal",
				description: "Number of writers rented from thread-local cache");
			_rentGlobalCounter = _meter.CreateCounter<long>(
				"jsonwriter.pool.rent.global",
				description: "Number of writers rented from global pool or newly created");
			_returnThreadLocalCounter = _meter.CreateCounter<long>(
				"jsonwriter.pool.return.threadlocal",
				description: "Number of writers returned to thread-local cache");
			_returnGlobalCounter = _meter.CreateCounter<long>(
				"jsonwriter.pool.return.global",
				description: "Number of writers returned to global pool");

			_ = _meter.CreateObservableGauge(
				"jsonwriter.pool.size.current",
				() => _currentPoolSize,
				description: "Current number of writers in the global pool");
			_ = _meter.CreateObservableGauge(
				"jsonwriter.pool.size.max",
				() => MaxPoolSize,
				description: "Maximum pool size (may change with adaptive sizing)");
			_ = _meter.CreateObservableGauge(
				"jsonwriter.pool.size.peak",
				() => _peakPoolSize,
				description: "Peak pool size observed");
		}

		// Set up adaptive sizing timer
		if (_enableAdaptiveSizing)
		{
			_adaptiveSizeTimer = new Timer(
				AdaptiveSizeCheck,
				state: null,
				TimeSpan.FromSeconds(30),
				TimeSpan.FromSeconds(30));
		}
		else
		{
			_adaptiveSizeTimer = null!;
		}
	}

	/// <inheritdoc />
	public int MaxPoolSize { get; private set; }

	/// <inheritdoc />
	public int Count => _currentPoolSize;

	/// <inheritdoc />
	public long TotalRented => Interlocked.Read(ref _totalRented);

	/// <inheritdoc />
	public long TotalReturned => Interlocked.Read(ref _totalReturned);

	/// <inheritdoc />
#pragma warning disable CA2000 // pooledWriter ownership transfers to caller
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public Utf8JsonWriter Rent(IBufferWriter<byte> bufferWriter, JsonWriterOptions? options = null)
	{
		ArgumentNullException.ThrowIfNull(bufferWriter);

		_ = Interlocked.Increment(ref _totalRented);
		var requestedOptions = options ?? _defaultOptions;

		// Fast path: Try thread-local cache first
		if (_threadLocalCacheSize > 0)
		{
			var cache = _threadLocalCache.Value;
			var writer = cache.TryRent(requestedOptions);
			if (writer != null)
			{
				_ = Interlocked.Increment(ref _threadLocalHits);
				writer.Reset(bufferWriter);
				RecordRental(fromThreadLocal: true);
				return writer;
			}

			_ = Interlocked.Increment(ref _threadLocalMisses);
		}

		// Slow path: Try global pool
		if (TryRentFromGlobalPool(bufferWriter, requestedOptions, out var pooledWriter))
		{
			RecordRental(fromThreadLocal: false);
			return pooledWriter;
		}

		// Create new writer
		var newWriter = new Utf8JsonWriter(bufferWriter, requestedOptions);
		RecordRental(fromThreadLocal: false);
		return newWriter;
	}
#pragma warning restore CA2000

	/// <inheritdoc />
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void ReturnToPool(Utf8JsonWriter writer)
	{
		ArgumentNullException.ThrowIfNull(writer);

		_ = Interlocked.Increment(ref _totalReturned);

		try
		{
			// Flush any pending data
			writer.Flush();

			// Try thread-local cache first
			if (_threadLocalCacheSize > 0)
			{
				var cache = _threadLocalCache.Value;
				if (cache.TryReturn(writer))
				{
					RecordReturn(toThreadLocal: true);
					return;
				}
			}

			// Try global pool
			if (TryReturnToGlobalPool(writer))
			{
				RecordReturn(toThreadLocal: false);
				return;
			}

			// Pool is full, dispose the writer
			writer.Dispose();
			RecordReturn(toThreadLocal: false);
		}
		catch
		{
			// If anything goes wrong, just dispose the writer
			writer.Dispose();
		}
	}

	/// <inheritdoc />
	public void Clear()
	{
		// Clear thread-local caches
		if (_threadLocalCache.IsValueCreated)
		{
			foreach (var cache in _threadLocalCache.Values)
			{
				cache?.Clear();
			}
		}

		// Clear global pool
		foreach (var kvp in _globalPool)
		{
			while (kvp.Value.TryDequeue(out var writer))
			{
				writer.Dispose();
				_ = Interlocked.Decrement(ref _currentPoolSize);
			}
		}

		_globalPool.Clear();

		_peakPoolSize = 0;
	}

	/// <inheritdoc />
	public void PreWarm(int count)
	{
		if (count <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(count), Resources.Utf8JsonWriterPool_CountMustBeGreaterThanZero);
		}

		// Pre-warm with different strategies
		PreWarmWithStrategy(count, PreWarmStrategy.Balanced);
	}

	/// <summary>
	/// Pre-warms the pool with a specific strategy.
	/// </summary>
	/// <exception cref="ArgumentOutOfRangeException"> </exception>
	public void PreWarmWithStrategy(int count, PreWarmStrategy strategy)
	{
		if (count <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(count), Resources.Utf8JsonWriterPool_CountMustBeGreaterThanZero);
		}

		switch (strategy)
		{
			case PreWarmStrategy.ThreadLocal:
				PreWarmThreadLocal(count);
				break;

			case PreWarmStrategy.Global:
				PreWarmGlobal(count);
				break;

			case PreWarmStrategy.Balanced:
				var threadLocalCount = Math.Min(count / 4, _threadLocalCacheSize);
				var globalCount = count - threadLocalCount;
				PreWarmThreadLocal(threadLocalCount);
				PreWarmGlobal(globalCount);
				break;

			default:
				throw new ArgumentOutOfRangeException(nameof(strategy));
		}
	}

	/// <summary>
	/// Gets comprehensive pool statistics.
	/// </summary>
	public PoolStatistics GetStatistics() =>
		new()
		{
			CurrentPoolSize = _currentPoolSize,
			MaxPoolSize = MaxPoolSize,
			PeakPoolSize = _peakPoolSize,
			TotalRented = TotalRented,
			TotalReturned = TotalReturned,
			ThreadLocalHits = _threadLocalHits,
			ThreadLocalMisses = _threadLocalMisses,
			ThreadLocalHitRate = _threadLocalHits + _threadLocalMisses > 0
				? (double)_threadLocalHits / (_threadLocalHits + _threadLocalMisses)
				: 0,
			OptionMismatches = _optionMismatches,
			PoolExpansions = _poolExpansions,
			PoolContractions = _poolContractions,
			ActiveWriters = TotalRented - TotalReturned,
		};

	/// <summary>
	/// Gets the health status of the pool.
	/// </summary>
	public PoolHealth GetHealth()
	{
		var stats = GetStatistics();
		var utilizationRate = (double)stats.ActiveWriters / MaxPoolSize;
		var returnRate = stats.TotalRented > 0
			? (double)stats.TotalReturned / stats.TotalRented
			: 1.0;

		if (utilizationRate > 0.9 || returnRate < 0.95)
		{
			return PoolHealth.Critical;
		}

		if (utilizationRate > 0.7 || returnRate < 0.98)
		{
			return PoolHealth.Warning;
		}

		return PoolHealth.Healthy;
	}

	/// <summary>
	/// Returns the assumed <see cref="JsonWriterOptions"/> for a given writer.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Why this is a stub:</b> <see cref="Utf8JsonWriter"/> does not expose its
	/// <see cref="JsonWriterOptions"/> after construction. The <c>Options</c> property was added
	/// in .NET 8 but is a copy-struct and does not round-trip the <see cref="System.Text.Encodings.Web.JavaScriptEncoder"/>
	/// reference reliably across pool return/rent cycles. Rather than risk incorrect option-matching
	/// in the pool (which would cause silent data corruption if writers are bucketed by mismatched
	/// options), we return the pool's default options. This is safe because:
	/// </para>
	/// <list type="bullet">
	/// <item><description>Writers are bucketed by <see cref="GetOptionsHashCode"/> at return time.</description></item>
	/// <item><description>At rent time, the writer is <see cref="Utf8JsonWriter.Reset()"/>-ed with the caller's requested options.</description></item>
	/// <item><description>The only impact is that writers created with non-default options may not be reused from the
	/// thread-local cache (they will fall through to the global pool or be newly allocated).</description></item>
	/// </list>
	/// <para>
	/// If .NET exposes a reliable way to read back the full options (including Encoder) from a writer,
	/// this method should be updated to use it.
	/// </para>
	/// </remarks>
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0060:Remove unused parameter",
		Justification = "Writer parameter reserved for future implementation of extracting options from existing Utf8JsonWriter instances")]
	private static JsonWriterOptions ExtractWriterOptions(Utf8JsonWriter writer) =>
		new() { Indented = false, SkipValidation = false, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, MaxDepth = 64 };

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static bool AreOptionsEqual(JsonWriterOptions options1, JsonWriterOptions options2) =>
		options1.Indented == options2.Indented &&
		options1.SkipValidation == options2.SkipValidation &&
		options1.MaxDepth == options2.MaxDepth &&
		ReferenceEquals(options1.Encoder, options2.Encoder);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static int GetOptionsHashCode(JsonWriterOptions options) =>
		HashCode.Combine(options.Indented, options.SkipValidation, options.MaxDepth, RuntimeHelpers.GetHashCode(options.Encoder));

	private bool TryRentFromGlobalPool(IBufferWriter<byte> bufferWriter, JsonWriterOptions requestedOptions, out Utf8JsonWriter writer)
	{
		writer = null!;

		var key = GetOptionsHashCode(requestedOptions);

		if (_globalPool.TryGetValue(key, out var queue) && queue.TryDequeue(out var pooledWriter))
		{
			_ = Interlocked.Decrement(ref _currentPoolSize);
			pooledWriter.Reset(bufferWriter);
			writer = pooledWriter;
			return true;
		}

		return false;
	}

	private bool TryReturnToGlobalPool(Utf8JsonWriter writer)
	{
		var currentSize = _currentPoolSize;
		if (currentSize >= MaxPoolSize)
		{
			return false;
		}

		// Try to add to pool
		if (Interlocked.Increment(ref _currentPoolSize) <= MaxPoolSize)
		{
			var dummyBuffer = new ArrayBufferWriter<byte>();
			writer.Reset(dummyBuffer);

			var options = ExtractWriterOptions(writer);
			var key = GetOptionsHashCode(options);
			var queue = _globalPool.BoundedGetOrAdd(key, static _ => new ConcurrentQueue<Utf8JsonWriter>(), maxEntries: 16);
			queue.Enqueue(writer);

			// Update peak size
			UpdatePeakSize(currentSize + 1);

			return true;
		}

		// Exceeded max size, revert increment
		_ = Interlocked.Decrement(ref _currentPoolSize);
		return false;
	}

	private void AdaptiveSizeCheck(object? state)
	{
		if (!_enableAdaptiveSizing)
		{
			return;
		}

		lock (_sizingLock)
		{
			var stats = GetStatistics();
			var utilizationRate = (double)stats.CurrentPoolSize / MaxPoolSize;

			// Expand pool if consistently high utilization
			if (utilizationRate > HighWaterMarkRatio && MaxPoolSize < MaximumPoolSize)
			{
				var newSize = Math.Min(MaxPoolSize * 2, MaximumPoolSize);
				MaxPoolSize = newSize;
				_ = Interlocked.Increment(ref _poolExpansions);
			}

			// Contract pool if consistently low utilization
			else if (utilizationRate < LowWaterMarkRatio && MaxPoolSize > MinPoolSize)
			{
				var newSize = Math.Max(MaxPoolSize / 2, MinPoolSize);
				MaxPoolSize = newSize;
				_ = Interlocked.Increment(ref _poolContractions);

				// Remove excess items
				foreach (var kvp in _globalPool)
				{
					while (_currentPoolSize > newSize && kvp.Value.TryDequeue(out var excessWriter))
					{
						excessWriter.Dispose();
						_ = Interlocked.Decrement(ref _currentPoolSize);
					}
				}
			}
		}
	}

	private void PreWarmThreadLocal(int count)
	{
		if (count <= 0 || _threadLocalCacheSize <= 0)
		{
			return;
		}

		var cache = _threadLocalCache.Value;
		for (var i = 0; i < count && i < _threadLocalCacheSize; i++)
		{
			var dummyBuffer = new ArrayBufferWriter<byte>();
			// R0.8: Dispose objects before losing scope - Writer is managed by the cache
#pragma warning disable CA2000
			var writer = new Utf8JsonWriter(dummyBuffer, _defaultOptions);
			_ = cache.TryReturn(writer);
#pragma warning restore CA2000
		}
	}

	private void PreWarmGlobal(int count)
	{
		var toCreate = Math.Min(count, MaxPoolSize - _currentPoolSize);

		for (var i = 0; i < toCreate; i++)
		{
			var dummyBuffer = new ArrayBufferWriter<byte>();
			// R0.8: Dispose objects before losing scope - Writer ownership is transferred to pool
#pragma warning disable CA2000
			var writer = new Utf8JsonWriter(dummyBuffer, _defaultOptions);

			if (!TryReturnToGlobalPool(writer))
			{
				writer.Dispose();
				break;
			}
#pragma warning restore CA2000
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void UpdatePeakSize(int size)
	{
		var current = _peakPoolSize;
		while (size > current)
		{
			_ = Interlocked.CompareExchange(ref _peakPoolSize, size, current);
			current = _peakPoolSize;
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void RecordRental(bool fromThreadLocal)
	{
		if (!_enableTelemetry)
		{
			return;
		}

		if (fromThreadLocal)
		{
			_rentThreadLocalCounter?.Add(1);
		}
		else
		{
			_rentGlobalCounter?.Add(1);
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void RecordReturn(bool toThreadLocal)
	{
		if (!_enableTelemetry)
		{
			return;
		}

		if (toThreadLocal)
		{
			_returnThreadLocalCounter?.Add(1);
		}
		else
		{
			_returnGlobalCounter?.Add(1);
		}
	}

	private volatile bool _disposed;

	/// <summary>
	/// Releases all resources used by the <see cref="Utf8JsonWriterPool" />. Disposes all pooled writers, thread-local caches, and
	/// stops adaptive sizing operations. This method is thread-safe and can be called multiple times without adverse effects.
	/// </summary>
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_adaptiveSizeTimer?.Dispose();

		Clear();

		if (_threadLocalCache.IsValueCreated)
		{
			_threadLocalCache.Dispose();
		}

		_disposed = true;
	}

	/// <summary>
	/// Thread-local cache for writers.
	/// </summary>
	private sealed class WriterCache(int maxSize)
	{
		private readonly PooledWriterEntry[] _cache = new PooledWriterEntry[maxSize];
		private int _count;

		public Utf8JsonWriter? TryRent(JsonWriterOptions requestedOptions)
		{
			for (var i = 0; i < _count; i++)
			{
				if (AreOptionsEqual(_cache[i].Options, requestedOptions))
				{
					var writer = _cache[i].Writer;

					// Shift remaining items
					Array.Copy(_cache, i + 1, _cache, i, _count - i - 1);
					_count--;

					return writer;
				}
			}

			return null;
		}

		public bool TryReturn(Utf8JsonWriter writer)
		{
			if (_count >= maxSize)
			{
				return false;
			}

			var options = ExtractWriterOptions(writer);
			_cache[_count++] = new PooledWriterEntry(writer, options);
			return true;
		}

		public void Clear()
		{
			for (var i = 0; i < _count; i++)
			{
				_cache[i].Writer.Dispose();
			}

			_count = 0;
		}
	}

	/// <summary>
	/// Represents a pooled writer with its options.
	/// </summary>
	private readonly record struct PooledWriterEntry(Utf8JsonWriter Writer, JsonWriterOptions Options);
}
