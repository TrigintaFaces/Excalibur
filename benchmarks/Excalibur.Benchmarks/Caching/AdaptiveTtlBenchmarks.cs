// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

using Excalibur.Caching.AdaptiveTtl;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Benchmarks.Caching;

/// <summary>
/// Benchmarks for Adaptive TTL caching operations.
/// </summary>
/// <remarks>
/// AD-221-6: Caching benchmark category.
/// - Adaptive TTL calculation overhead
/// - Cache hit/miss latency
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.HostProcess)]
public class AdaptiveTtlBenchmarks
{
	private AdaptiveTtlCache _adaptiveCache = null!;
	private MemoryDistributedCache _innerCache = null!;
	private IAdaptiveTtlStrategy _strategy = null!;
	private TestSystemLoadMonitor _loadMonitor = null!;
	private byte[] _testData = null!;
	private string[] _keys = null!;

	[Params(100, 1000)]
	public int CacheSize { get; set; }

	[GlobalSetup]
	public void GlobalSetup()
	{
		// Create inner memory cache
		var cacheOptions = Options.Create(new MemoryDistributedCacheOptions());
		_innerCache = new MemoryDistributedCache(cacheOptions);

		// Create adaptive TTL strategy with correct options
		var ttlOptions = new RuleBasedTtlOptions
		{
			MinTtl = TimeSpan.FromSeconds(30),
			MaxTtl = TimeSpan.FromHours(1),
			HitRate = { HighHitRateThreshold = 0.9, LowHitRateThreshold = 0.5 },
		};
		_strategy = new RuleBasedAdaptiveTtlStrategy(
			NullLogger<RuleBasedAdaptiveTtlStrategy>.Instance,
			ttlOptions);

		// Create load monitor
		_loadMonitor = new TestSystemLoadMonitor();

		// Create adaptive cache
		_adaptiveCache = new AdaptiveTtlCache(
			_innerCache,
			_strategy,
			NullLogger<AdaptiveTtlCache>.Instance,
			_loadMonitor);

		// Pre-populate cache
		_testData = new byte[1024]; // 1KB test data
		Random.Shared.NextBytes(_testData);

		_keys = new string[CacheSize];
		for (int i = 0; i < CacheSize; i++)
		{
			_keys[i] = $"key-{i}";
			_innerCache.Set(_keys[i], _testData, new DistributedCacheEntryOptions
			{
				AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
			});
		}
	}

	[GlobalCleanup]
	public void GlobalCleanup()
	{
		_adaptiveCache?.Dispose();
	}

	#region TTL Calculation Benchmarks

	/// <summary>
	/// Benchmark: TTL calculation overhead.
	/// </summary>
	[Benchmark(Baseline = true)]
	public TimeSpan CalculateTtl()
	{
		var context = new AdaptiveTtlContext
		{
			Key = "test-key",
			BaseTtl = TimeSpan.FromMinutes(5),
			AccessFrequency = 100,
			HitRate = 0.85,
			ContentSize = 1024,
			SystemLoad = 0.5,
			CurrentTime = DateTime.UtcNow
		};
		return _strategy.CalculateTtl(context);
	}

	/// <summary>
	/// Benchmark: TTL calculation with high load.
	/// </summary>
	[Benchmark]
	public TimeSpan CalculateTtlHighLoad()
	{
		_loadMonitor.SetLoad(0.9); // 90% load

		var context = new AdaptiveTtlContext
		{
			Key = "test-key",
			BaseTtl = TimeSpan.FromMinutes(5),
			AccessFrequency = 100,
			HitRate = 0.85,
			ContentSize = 1024,
			SystemLoad = 0.9,
			CurrentTime = DateTime.UtcNow
		};
		return _strategy.CalculateTtl(context);
	}

	/// <summary>
	/// Benchmark: Get strategy metrics.
	/// </summary>
	[Benchmark]
	public AdaptiveTtlMetrics GetMetrics()
	{
		return _strategy.GetMetrics();
	}

	#endregion

	#region Cache Operation Benchmarks

	/// <summary>
	/// Benchmark: Cache hit (async).
	/// </summary>
	[Benchmark]
	public async Task<byte[]?> CacheHit()
	{
		return await _adaptiveCache.GetAsync(_keys[0], CancellationToken.None);
	}

	/// <summary>
	/// Benchmark: Cache miss (async).
	/// </summary>
	[Benchmark]
	public async Task<byte[]?> CacheMiss()
	{
		return await _adaptiveCache.GetAsync("nonexistent-key", CancellationToken.None);
	}

	/// <summary>
	/// Benchmark: Cache set with adaptive TTL.
	/// </summary>
	[Benchmark]
	public async Task CacheSet()
	{
		var key = $"new-key-{Guid.NewGuid():N}";
		await _adaptiveCache.SetAsync(key, _testData, new DistributedCacheEntryOptions
		{
			AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
		}, CancellationToken.None);
	}

	/// <summary>
	/// Benchmark: Cache remove.
	/// </summary>
	[Benchmark]
	public async Task CacheRemove()
	{
		await _adaptiveCache.RemoveAsync(_keys[CacheSize - 1], CancellationToken.None);
		// Re-add for next iteration
		await _innerCache.SetAsync(_keys[CacheSize - 1], _testData, new DistributedCacheEntryOptions
		{
			AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
		});
	}

	#endregion

	#region Concurrent Benchmarks

	/// <summary>
	/// Benchmark: Concurrent cache reads.
	/// </summary>
	[Benchmark]
	public async Task ConcurrentReads10()
	{
		var tasks = new Task<byte[]?>[10];
		for (int i = 0; i < 10; i++)
		{
			var key = _keys[i % _keys.Length];
			tasks[i] = _adaptiveCache.GetAsync(key, CancellationToken.None);
		}
		_ = await Task.WhenAll(tasks);
	}

	/// <summary>
	/// Benchmark: Mixed read/write operations.
	/// </summary>
	[Benchmark]
	public async Task MixedOperations()
	{
		var tasks = new List<Task>(20);

		// 80% reads, 20% writes
		for (int i = 0; i < 16; i++)
		{
			var key = _keys[i % _keys.Length];
			tasks.Add(_adaptiveCache.GetAsync(key, CancellationToken.None));
		}

		for (int i = 0; i < 4; i++)
		{
			var key = $"write-key-{i}";
			tasks.Add(_adaptiveCache.SetAsync(key, _testData, new DistributedCacheEntryOptions
			{
				AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
			}, CancellationToken.None));
		}

		await Task.WhenAll(tasks);
	}

	#endregion

	#region Synchronous Operation Benchmarks

	/// <summary>
	/// Benchmark: Sync get (IDistributedCache interface).
	/// </summary>
	[Benchmark]
	public byte[]? SyncGet()
	{
		return _adaptiveCache.Get(_keys[0]);
	}

	/// <summary>
	/// Benchmark: Sync set (IDistributedCache interface).
	/// </summary>
	[Benchmark]
	public void SyncSet()
	{
		var key = $"sync-key-{Guid.NewGuid():N}";
		_adaptiveCache.Set(key, _testData, new DistributedCacheEntryOptions
		{
			AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
		});
	}

	#endregion
}

/// <summary>
/// Test system load monitor for benchmarks.
/// </summary>
internal sealed class TestSystemLoadMonitor : ISystemLoadMonitor
{
	private double _load = 0.5;

	public void SetLoad(double load) => _load = load;

	public Task<double> GetCurrentLoadAsync() => Task.FromResult(_load);
}
