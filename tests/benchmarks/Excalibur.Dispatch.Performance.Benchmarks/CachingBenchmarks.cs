// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace Excalibur.Dispatch.Performance.Benchmarks;

/// <summary>
/// Benchmarks for caching operations and patterns.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net90)]
public class CachingBenchmarks
{
	private ConcurrentDictionary<string, CacheEntry> _cache = null!;
	private Dictionary<string, CacheEntry> _dictionaryCache = null!;
	private string[] _keys = null!;
	private CacheEntry[] _values = null!;
	private const int CacheSize = 1000;

	[GlobalSetup]
	public void Setup()
	{
		_cache = new ConcurrentDictionary<string, CacheEntry>();
		_dictionaryCache = new Dictionary<string, CacheEntry>();
		_keys = new string[CacheSize];
		_values = new CacheEntry[CacheSize];

		for (var i = 0; i < CacheSize; i++)
		{
			_keys[i] = $"key-{i}";
			_values[i] = new CacheEntry
			{
				Data = $"value-{i}",
				CreatedAt = DateTimeOffset.UtcNow,
				ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5),
			};

			_cache[_keys[i]] = _values[i];
			_dictionaryCache[_keys[i]] = _values[i];
		}
	}

	[Benchmark(Baseline = true)]
	public CacheEntry? ConcurrentDictionary_TryGetValue()
	{
		_ = _cache.TryGetValue("key-500", out var value);
		return value;
	}

	[Benchmark]
	public CacheEntry? Dictionary_TryGetValue()
	{
		_ = _dictionaryCache.TryGetValue("key-500", out var value);
		return value;
	}

	[Benchmark]
	public CacheEntry ConcurrentDictionary_GetOrAdd()
	{
		return _cache.GetOrAdd("new-key", _ => new CacheEntry
		{
			Data = "new-value",
			CreatedAt = DateTimeOffset.UtcNow,
			ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5),
		});
	}

	[Benchmark]
	public bool ConcurrentDictionary_TryAdd()
	{
		var key = $"temp-key-{Random.Shared.Next()}";
		var result = _cache.TryAdd(key, _values[0]);
		_ = _cache.TryRemove(key, out _);
		return result;
	}

	[Benchmark]
	public bool ConcurrentDictionary_ContainsKey()
	{
		return _cache.ContainsKey("key-500");
	}

	[Benchmark]
	public int ConcurrentDictionary_Count()
	{
		return _cache.Count;
	}

	[Benchmark]
	public CacheEntry ConcurrentDictionary_AddOrUpdate()
	{
		return _cache.AddOrUpdate(
			"key-500",
			_ => _values[0],
			(_, existing) => existing);
	}

	[Benchmark]
	public void ConcurrentDictionary_Iterate100()
	{
		var count = 0;
		foreach (var kvp in _cache)
		{
			count++;
			if (count >= 100)
			{
				break;
			}
		}
	}

	[Benchmark]
	public string GenerateCacheKey_Concatenation()
	{
		return "prefix:" + "user" + ":" + "123" + ":" + "orders";
	}

	[Benchmark]
	public string GenerateCacheKey_Interpolation()
	{
		var type = "user";
		var id = "123";
		var action = "orders";
		return $"prefix:{type}:{id}:{action}";
	}

	[Benchmark]
	public string GenerateCacheKey_StringCreate()
	{
		var type = "user";
		var id = "123";
		var action = "orders";
		return string.Create(null, stackalloc char[64], $"prefix:{type}:{id}:{action}");
	}

	[Benchmark]
	public bool CheckExpiration_DateTimeComparison()
	{
		var entry = _values[500];
		return entry.ExpiresAt < DateTimeOffset.UtcNow;
	}

	[Benchmark]
	public bool CheckExpiration_TicksComparison()
	{
		var entry = _values[500];
		return entry.ExpiresAt.Ticks < DateTimeOffset.UtcNow.Ticks;
	}

	[Benchmark]
	public CacheEntry CreateCacheEntry()
	{
		return new CacheEntry
		{
			Data = "test-data",
			CreatedAt = DateTimeOffset.UtcNow,
			ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5),
		};
	}

	[Benchmark]
	public CacheEntryStruct CreateCacheEntryStruct()
	{
		return new CacheEntryStruct
		{
			Data = "test-data",
			CreatedAt = DateTimeOffset.UtcNow,
			ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(5),
		};
	}

	public sealed class CacheEntry
	{
		public string Data { get; init; } = string.Empty;
		public DateTimeOffset CreatedAt { get; init; }
		public DateTimeOffset ExpiresAt { get; init; }
	}

	public readonly struct CacheEntryStruct
	{
		public string Data { get; init; }
		public DateTimeOffset CreatedAt { get; init; }
		public DateTimeOffset ExpiresAt { get; init; }
	}
}
