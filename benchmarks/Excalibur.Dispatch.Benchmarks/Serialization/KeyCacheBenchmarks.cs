// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

using Excalibur.Dispatch.Compliance;

namespace Excalibur.Dispatch.Benchmarks.Serialization;

/// <summary>
/// Benchmarks for key metadata cache performance.
/// Measures cache hit/miss latency and memory allocation.
/// </summary>
/// <remarks>
/// <para>
/// Performance Targets:
/// </para>
/// <list type="bullet">
///   <item>Cache hit: &lt; 1μs</item>
///   <item>Cache miss with factory: depends on factory</item>
///   <item>Cache set: &lt; 5μs</item>
///   <item>Zero allocations on cache hit</item>
/// </list>
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.HostProcess)]
#pragma warning disable CA1001 // BenchmarkDotNet manages lifecycle via [GlobalCleanup]
public class KeyCacheBenchmarks
#pragma warning restore CA1001
{
	private KeyCache _cache = null!;
	private KeyMetadata _testMetadata = null!;
	private string[] _keyIds = null!;

	/// <summary>
	/// Initialize cache and test data.
	/// </summary>
	[GlobalSetup]
	public void GlobalSetup()
	{
		var options = new KeyCacheOptions
		{
			DefaultTtl = TimeSpan.FromMinutes(5),
			MaxEntries = 1000,
			UseSlidingExpiration = true,
		};

		_cache = new KeyCache(options, NullEncryptionTelemetry.Instance);

		_testMetadata = new KeyMetadata
		{
			KeyId = "test-key-1",
			Version = 1,
			Algorithm = EncryptionAlgorithm.Aes256Gcm,
			Status = KeyStatus.Active,
			CreatedAt = DateTimeOffset.UtcNow,
		};

		// Pre-populate cache with 100 keys
		_keyIds = new string[100];
		for (int i = 0; i < 100; i++)
		{
			var keyId = $"key-{i:D4}";
			_keyIds[i] = keyId;
			_cache.Set(new KeyMetadata
			{
				KeyId = keyId,
				Version = 1,
				Algorithm = EncryptionAlgorithm.Aes256Gcm,
				Status = KeyStatus.Active,
				CreatedAt = DateTimeOffset.UtcNow,
			});
		}
	}

	/// <summary>
	/// Cleanup cache resources.
	/// </summary>
	[GlobalCleanup]
	public void GlobalCleanup()
	{
		_cache?.Dispose();
	}

	/// <summary>
	/// Benchmark: Cache hit for known key.
	/// Target: &lt; 1μs, zero allocations.
	/// </summary>
	[Benchmark(Baseline = true)]
	public KeyMetadata? CacheHit()
	{
		return _cache.TryGet("key-0050");
	}

	/// <summary>
	/// Benchmark: Cache miss for unknown key.
	/// Target: &lt; 1μs, minimal allocations.
	/// </summary>
	[Benchmark]
	public KeyMetadata? CacheMiss()
	{
		return _cache.TryGet("nonexistent-key");
	}

	/// <summary>
	/// Benchmark: Set key metadata.
	/// Target: &lt; 5μs.
	/// </summary>
	[Benchmark]
	public void CacheSet()
	{
		_cache.Set(_testMetadata);
	}

	/// <summary>
	/// Benchmark: Set key with custom TTL.
	/// Target: &lt; 5μs.
	/// </summary>
	[Benchmark]
	public void CacheSetWithTtl()
	{
		_cache.Set(_testMetadata, TimeSpan.FromMinutes(10));
	}

	/// <summary>
	/// Benchmark: GetOrAdd with cache hit (no factory invocation).
	/// Target: Near-instant, zero allocations.
	/// </summary>
	[Benchmark]
	public KeyMetadata? GetOrAddHit()
	{
		return _cache.GetOrAddAsync(
			"key-0050",
			(keyId, ct) => Task.FromResult<KeyMetadata?>(null), CancellationToken.None).GetAwaiter().GetResult();
	}

	/// <summary>
	/// Benchmark: GetOrAdd with cache miss (factory invocation).
	/// </summary>
	[Benchmark]
	public KeyMetadata? GetOrAddMiss()
	{
		// Use a unique key each time to ensure miss
		var keyId = $"miss-{Guid.NewGuid():N}";
		return _cache.GetOrAddAsync(
			keyId,
			(id, ct) => Task.FromResult<KeyMetadata?>(new KeyMetadata
			{
				KeyId = id,
				Version = 1,
				Algorithm = EncryptionAlgorithm.Aes256Gcm,
				Status = KeyStatus.Active,
				CreatedAt = DateTimeOffset.UtcNow,
			}), CancellationToken.None).GetAwaiter().GetResult();
	}

	/// <summary>
	/// Benchmark: Remove key from cache.
	/// Target: &lt; 1μs.
	/// </summary>
	[Benchmark]
	public void CacheRemove()
	{
		_cache.Remove("key-0099");
		// Re-add so subsequent iterations work
		_cache.Set(new KeyMetadata
		{
			KeyId = "key-0099",
			Version = 1,
			Algorithm = EncryptionAlgorithm.Aes256Gcm,
			Status = KeyStatus.Active,
			CreatedAt = DateTimeOffset.UtcNow,
		});
	}

	/// <summary>
	/// Benchmark: Sequential reads across multiple keys (simulates real access patterns).
	/// </summary>
	[Benchmark]
	public int SequentialReads()
	{
		int hits = 0;
		for (int i = 0; i < 100; i++)
		{
			if (_cache.TryGet(_keyIds[i]) is not null)
			{
				hits++;
			}
		}

		return hits;
	}
}
