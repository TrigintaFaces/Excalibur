// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Buffers;
using System.Collections.Concurrent;
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
/// - Thread-local caching with synchronized pool ownership and cleanup
/// - Adaptive pool sizing based on usage patterns
/// - Comprehensive telemetry and monitoring
/// - Pre-warming strategies for different workloads
/// - Writer reuse on pool hits; pool misses and telemetry may allocate.
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

	/// <summary>
	/// The number of distinct <see cref="JsonWriterOptions"/> shapes the global pool will keep a queue for.
	/// Options shapes come from configuration rather than from message content, so a handful is the normal
	/// case; the cap exists so an application that builds a fresh encoder per call cannot grow the pool's
	/// key set without limit.
	/// </summary>
	private const int MaxOptionShapes = 16;
	private readonly ConcurrentDictionary<int, ConcurrentQueue<Utf8JsonWriter>> _globalPool;
	private readonly ThreadLocal<WriterCache> _threadLocalCache;
	private readonly JsonWriterOptions _defaultOptions;
	private readonly JsonWriterOptions _normalizedDefaultOptions;
	private readonly Meter? _meter;
	private readonly Counter<long>? _rentThreadLocalCounter;
	private readonly Counter<long>? _rentGlobalCounter;
	private readonly Counter<long>? _returnThreadLocalCounter;
	private readonly Counter<long>? _returnGlobalCounter;
	private readonly Timer _adaptiveSizeTimer;
	// LOCK ORDER (I2): this lock is acquired BEFORE any WriterCache gate, never the reverse.
	// Three call paths nest them and all three nest in this order -- Clear and Dispose via
	// ClearCore, and PreWarmWithStrategy via PreWarmThreadLocal. The rent and return hot paths
	// nest nothing: they take a cache gate, RELEASE it, and only then take this lock for the
	// global-pool consult.
	//
	// The way to break this is specific and worth naming, because it looks like an optimisation:
	// moving the global-pool consult INSIDE the thread-local branch, to take one lock instead of
	// two, would give cache-then-lifecycle on the rent path against lifecycle-then-cache in
	// ClearCore -- a two-thread deadlock. No test can catch it, because the cycle is unreachable
	// until that edit exists, so this comment is the only instrument guarding it.
	private readonly Lock _lifecycleLock = new();

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

		_normalizedDefaultOptions = NormalizeOptions(_defaultOptions);

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
		// Disposal is observed through the volatile _disposed flag, not by holding the pool-wide
		// lifecycle lock for the whole rental. That lock used to wrap RentCore, which put TWO things
		// inside it that touch no shared state: the thread-local cache hit, which reads a per-thread
		// array and is the entire reason the cache exists, and the construction of a new writer on a
		// total miss, which extended the hold time for every waiter. With one serializer singleton
		// there is one pool and one lock, so every message in the process paid it twice -- once here
		// and once on return -- and throughput ceilinged at one-over-the-hold-time regardless of core
		// count.
		//
		// SCOPE, because the paragraph above otherwise reads as unconditional: what is now
		// lock-free is a WARM THREAD-LOCAL HIT. A cold thread's first rent, and every rent when
		// threadLocalCacheSize is 0 -- a supported setting, the documented range is 0 to 32 --
		// still consults the global pool under the lifecycle lock. The win is real on the path a
		// steady workload takes, and it is not universal.
		//
		// Residual race, stated rather than hidden: a Dispose concurrent with a rental may now complete
		// between this check and the cache access, in which case ThreadLocal.Value throws
		// ObjectDisposedException -- the same exception type a caller renting from a disposed pool
		// already expects, from a different line. Renting concurrently with disposal is a host
		// teardown-ordering fault either way, and this is the shape the BCL's own disposable caches
		// take: a flag check, not a monitor on the hot path.
		ObjectDisposedException.ThrowIf(_disposed, this);
		var writer = RentCore(bufferWriter, options, out var fromThreadLocal);

		// Meter listeners are caller code and must not run inside the ownership lock.
		try
		{
			RecordRental(fromThreadLocal);
		}
		catch
		{
			// The caller never received this exclusively held lease.
			writer.Dispose();
			throw;
		}
		
		// COUNT LEASES, NOT ATTEMPTS (I4). This sits on the path that actually hands a writer to
		// the caller, after RecordRental has succeeded, so no throw out of RentCore and no throw
		// out of a meter listener can leave a counted rent that was never issued. GetHealth
		// derives ActiveWriters as TotalRented - TotalReturned, nothing decrements _totalRented,
		// and Clear does not reset counters -- so an increment on a failed rental drifted the
		// health reading upward permanently, eventually reporting Critical with zero writers
		// outstanding. A statistics read taken mid-rent now UNDER-counts instead, so the reading
		// errs toward Healthy: a transient false-healthy beats a permanent false-critical for a
		// diagnostic that exists to be trusted.
		_ = Interlocked.Increment(ref _totalRented);
		return writer;
	}

	private Utf8JsonWriter RentCore(IBufferWriter<byte> bufferWriter, JsonWriterOptions? options, out bool fromThreadLocal)
	{

		var requestedOptions = options ?? _defaultOptions;

		// Pooled writers report their options through Utf8JsonWriter.Options, which the BCL has
		// normalized (MaxDepth 0 becomes 1000, NewLine picks up the platform default). Matching a
		// raw request against that never succeeds, so match on the normalized form instead.
		var matchOptions = options is null || AreOptionsEqual(requestedOptions, _defaultOptions)
			? _normalizedDefaultOptions
			: NormalizeOptions(requestedOptions);

		// Fast path: Try thread-local cache first
		if (_threadLocalCacheSize > 0)
		{
			var cache = _threadLocalCache.Value!;
			var writer = cache.TryRent(matchOptions);
			if (writer != null)
			{
				_ = Interlocked.Increment(ref _threadLocalHits);
				writer.Reset(bufferWriter);
				fromThreadLocal = true;
				return writer;
			}

			_ = Interlocked.Increment(ref _threadLocalMisses);
		}

		// Slow path: the global pool. THIS consult keeps the lifecycle lock, and it is the one place
		// in a rental that takes it, and the reason is OPTIMALITY RATHER THAN SAFETY -- stated
		// plainly so nobody later cites it as load-bearing for correctness. ConcurrentQueue already
		// hands each item to exactly one dequeuer, so no ownership or disposal property depends on
		// this lock. What it buys is the bucket scan's bound: the scan rotates mismatches back into
		// the queue,
		// bounded by the count read on entry, and that bound is only sound while the population is
		// stable. TryRentFromGlobalPool says so in its own comment. Hoisting this out as well would
		// turn a correct scan into one that can miss a matching writer and allocate instead.
		lock (_lifecycleLock)
		{
			ObjectDisposedException.ThrowIf(_disposed, this);
			if (TryRentFromGlobalPool(bufferWriter, matchOptions, out var pooledWriter))
			{
				fromThreadLocal = false;
				return pooledWriter;
			}
		}

		// Create new writer
		var newWriter = new Utf8JsonWriter(bufferWriter, requestedOptions);
		fromThreadLocal = false;
		return newWriter;
	}
#pragma warning restore CA2000

	/// <inheritdoc />
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void ReturnToPool(Utf8JsonWriter writer)
	{
		ArgumentNullException.ThrowIfNull(writer);
		_ = Interlocked.Increment(ref _totalReturned);
		var retained = false;
		var toThreadLocal = false;
		try
		{
			// Flush may call the caller's buffer writer. Ownership transfers only afterward.
			writer.Flush();
			if (!_disposed)
			{
				// Returning to the per-thread cache touches no pool-wide state, so it does not take the
				// lifecycle lock. Returning to the GLOBAL pool does, for the same population-stability
				// reason the rent-side consult does.
				if (_threadLocalCacheSize > 0 && _threadLocalCache.Value!.TryReturn(writer))
				{
					retained = true;
					toThreadLocal = true;
				}
				else
				{
					lock (_lifecycleLock)
					{
						if (!_disposed)
						{
							retained = TryReturnToGlobalPool(writer);
						}
					}
				}
			}

			if (!retained)
			{
				writer.Dispose();
			}

			RecordReturn(toThreadLocal);
		}
		catch
		{
			// A listener failure after transfer must not dispose a pooled or re-rented writer.
			if (!retained)
			{
				writer.Dispose();
			}
		}
	}

	/// <inheritdoc />
	public void Clear()
	{
		lock (_lifecycleLock)
		{
			ObjectDisposedException.ThrowIf(_disposed, this);
			ClearCore();
		}
	}

	private void ClearCore()
	{
		// Values covers every tracked thread, even if this thread never rented.
		foreach (var cache in _threadLocalCache.Values)
		{
			cache.Clear();
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

		lock (_lifecycleLock)
		{
			ObjectDisposedException.ThrowIf(_disposed, this);
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
	/// A writer parked in a pool must not keep the buffer of the caller that returned it alive, and must
	/// not be able to write into it. Returned writers are rebound to this sentinel until the next rent
	/// rebinds them to the new caller's buffer. Nothing is ever written to it, so sharing it is safe.
	/// </summary>
	private static readonly ArrayBufferWriter<byte> DetachedBuffer = new(1);

	/// <summary>
	/// Returns the options as <see cref="Utf8JsonWriter"/> itself reports them, which is the form every
	/// pooled writer is bucketed and matched by. Asking the writer is the only way to get this right --
	/// the normalization rules are the BCL's, not ours.
	/// </summary>
	private static JsonWriterOptions NormalizeOptions(JsonWriterOptions options)
	{
		using var probe = new Utf8JsonWriter(DetachedBuffer, options);
		return probe.Options;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static bool AreOptionsEqual(JsonWriterOptions options1, JsonWriterOptions options2) =>
		options1.Indented == options2.Indented &&
		options1.SkipValidation == options2.SkipValidation &&
		options1.MaxDepth == options2.MaxDepth &&
		options1.IndentCharacter == options2.IndentCharacter &&
		options1.IndentSize == options2.IndentSize &&
		options1.NewLine == options2.NewLine &&
		ReferenceEquals(options1.Encoder, options2.Encoder);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static int GetOptionsHashCode(JsonWriterOptions options) =>
		HashCode.Combine(
			options.Indented,
			options.SkipValidation,
			options.MaxDepth,
			options.IndentCharacter,
			options.IndentSize,
			options.NewLine,
			options.Encoder is null ? 0 : RuntimeHelpers.GetHashCode(options.Encoder));

	private bool TryRentFromGlobalPool(IBufferWriter<byte> bufferWriter, JsonWriterOptions requestedOptions, out Utf8JsonWriter writer)
	{
		writer = null!;

		var key = GetOptionsHashCode(requestedOptions);

		if (_globalPool.TryGetValue(key, out var queue))
		{
			// Hashes select a bucket, not an options identity. The lifecycle lock
			// keeps this population stable while mismatches rotate back into it.
			var remaining = queue.Count;
			while (remaining-- > 0 && queue.TryDequeue(out var pooledWriter))
			{
				if (!AreOptionsEqual(pooledWriter.Options, requestedOptions))
				{
					queue.Enqueue(pooledWriter);
					continue;
				}

				_ = Interlocked.Decrement(ref _currentPoolSize);
				pooledWriter.Reset(bufferWriter);
				writer = pooledWriter;
				return true;
			}
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
			writer.Reset(DetachedBuffer);

			var key = GetOptionsHashCode(writer.Options);

			if (!_globalPool.TryGetValue(key, out var queue))
			{
				if (_globalPool.Count >= MaxOptionShapes)
				{
					// No queue exists for this writer's options and there is no room to open one. The writer
					// cannot be pooled, and saying otherwise would lose it twice over: the caller stops
					// treating it as its own and never disposes it, and the size counter keeps the increment
					// for a writer the pool is not holding. That drift only grows, and once it reaches
					// MaxPoolSize the check above refuses every later return -- a pool that reports itself
					// full while holding nothing.
					_ = Interlocked.Decrement(ref _currentPoolSize);
					return false;
				}

				queue = _globalPool.GetOrAdd(key, static _ => new ConcurrentQueue<Utf8JsonWriter>());
			}

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

		lock (_lifecycleLock)
		{
			if (_disposed)
			{
				return;
			}

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

		var cache = _threadLocalCache.Value!;
		for (var i = 0; i < count && i < _threadLocalCacheSize; i++)
		{
			var dummyBuffer = new ArrayBufferWriter<byte>();
			// Dispose objects before losing scope - Writer is managed by the cache
#pragma warning disable CA2000
			var writer = new Utf8JsonWriter(dummyBuffer, _defaultOptions);
			if (!cache.TryReturn(writer))
			{
				writer.Dispose();
			}
#pragma warning restore CA2000
		}
	}

	private void PreWarmGlobal(int count)
	{
		var toCreate = Math.Min(count, MaxPoolSize - _currentPoolSize);

		for (var i = 0; i < toCreate; i++)
		{
			var dummyBuffer = new ArrayBufferWriter<byte>();
			// Dispose objects before losing scope - Writer ownership is transferred to pool
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
		lock (_lifecycleLock)
		{
			if (_disposed)
			{
				return;
			}

			_disposed = true;
			// Nonblocking shutdown: an already queued callback takes this lock and observes disposal.
			_adaptiveSizeTimer?.Dispose();
			ClearCore();
			_threadLocalCache.Dispose();
		}
	}

	/// <summary>
	/// Thread-local cache for writers.
	/// </summary>
	private sealed class WriterCache(int maxSize)
	{
		private readonly PooledWriterEntry[] _cache = new PooledWriterEntry[maxSize];

		// This cache is per-thread for TryRent and TryReturn and POOL-WIDE for Clear: ClearCore
		// walks ThreadLocal.Values (tracked) and clears every thread's cache, including threads
		// that never rented. So the array below is reachable from a foreign thread and every
		// access to it owes mutual exclusion -- it has a plain int count and no concurrency
		// guarantees of its own.
		//
		// That exclusion used to come incidentally from the pool-wide lifecycle lock, because all
		// three callers ran under it. Taking the rent and return paths off that lock for throughput
		// left Clear holding a lock the other two no longer took, which excludes nothing: a
		// foreign Clear could land between the options match and the Array.Copy below and leave
		// the copy with a negative length, or dispose a writer already promised to a caller.
		//
		// The gate restores the invariant at PER-THREAD granularity. Uncontended in the common
		// case -- only a cross-thread Clear contends, and only with one thread at a time -- so N
		// threads get N independent gates and there is no pool-wide ceiling. This is the shape the
		// BCL uses for a per-core bucket rather than a pool-wide monitor.
		// LOCK ORDER (I2): never acquire the pool's _lifecycleLock while holding this gate. See the
		// comment on that field for why, and for the one edit that would create a cycle.
		private readonly Lock _gate = new();

		private int _count;

		public Utf8JsonWriter? TryRent(JsonWriterOptions requestedOptions)
		{
			lock (_gate)
			{
				for (var i = 0; i < _count; i++)
				{
					if (AreOptionsEqual(_cache[i].Options, requestedOptions))
					{
						var writer = _cache[i].Writer;

						// Shift remaining items
						Array.Copy(_cache, i + 1, _cache, i, _count - i - 1);
						_cache[--_count] = default;

						return writer;
					}
				}

				return null;
			}
		}

		public bool TryReturn(Utf8JsonWriter writer)
		{
			lock (_gate)
			{
				if (_count >= maxSize)
				{
					return false;
				}

				// ORDERING (I3): rebind BEFORE the store, and both inside the gate. This is the only
				// store site into _cache, so this line is what guarantees the array never holds a
				// writer still bound to a caller's buffer. Store-then-rebind, or moving this Reset
				// outside the gate, opens a window where a foreign-thread Clear reaches a caller-bound
				// writer and flushes its pending bytes INTO THE CALLER'S BUFFER, then detaches it
				// mid-serialization. Wrong bytes in someone else's buffer, with no exception raised
				// anywhere -- the only byte-level failure mode in this class, and the one the gate alone
				// does not prevent. The global path holds the same property: TryReturnToGlobalPool
				// resets to DetachedBuffer before it enqueues.
				writer.Reset(DetachedBuffer);
				_cache[_count++] = new PooledWriterEntry(writer, writer.Options);
				return true;
			}
		}

		public void Clear()
		{
			lock (_gate)
			{
				for (var i = 0; i < _count; i++)
				{
					_cache[i].Writer.Dispose();
					_cache[i] = default;
				}

				_count = 0;
			}
		}
	}

	/// <summary>
	/// Represents a pooled writer with its options.
	/// </summary>
	private readonly record struct PooledWriterEntry(Utf8JsonWriter Writer, JsonWriterOptions Options);
}
