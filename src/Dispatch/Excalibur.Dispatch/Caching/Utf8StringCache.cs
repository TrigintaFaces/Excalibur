// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using System.Text;

namespace Excalibur.Dispatch.Caching;

/// <summary>
/// High-performance cache for UTF-8 string encoding/decoding operations. Implements PROF-003 optimization: String encoding cache (5-8% CPU improvement).
/// </summary>
public sealed class Utf8StringCache : IDisposable
{
	/// <summary>
	/// Shared Meter for Utf8StringCache telemetry.
	/// </summary>
	private static readonly Meter CacheMeter = new("Excalibur.Dispatch.Utf8StringCache", "1.0.0");

	/// <summary>
	/// Counter tracking the number of cache evictions (full clears due to capacity pressure).
	/// </summary>
	private static readonly Counter<long> EvictionCounter =
		CacheMeter.CreateCounter<long>("dispatch.utf8cache.evictions", "evictions", "Number of Utf8StringCache eviction cycles");

	/// <summary>
	/// Counter tracking the number of items removed during cleanup.
	/// </summary>
	private static readonly Counter<long> ItemsRemovedCounter =
		CacheMeter.CreateCounter<long>("dispatch.utf8cache.items_removed", "items", "Number of items removed during Utf8StringCache cleanup");

	/// <summary>
	/// Histogram tracking the duration of cleanup operations.
	/// </summary>
	private static readonly Histogram<double> CleanupDurationHistogram =
		CacheMeter.CreateHistogram<double>("dispatch.utf8cache.cleanup_duration", "ms", "Duration of Utf8StringCache cleanup operations in milliseconds");

	/// <summary>
	/// Counter tracking encoding cache hits.
	/// </summary>
	private static readonly Counter<long> EncodingHitCounter =
		CacheMeter.CreateCounter<long>("dispatch.utf8cache.encoding.hits", "hits", "Number of encoding cache hits");

	/// <summary>
	/// Counter tracking encoding cache misses.
	/// </summary>
	private static readonly Counter<long> EncodingMissCounter =
		CacheMeter.CreateCounter<long>("dispatch.utf8cache.encoding.misses", "misses", "Number of encoding cache misses");

	/// <summary>
	/// Counter tracking decoding cache hits.
	/// </summary>
	private static readonly Counter<long> DecodingHitCounter =
		CacheMeter.CreateCounter<long>("dispatch.utf8cache.decoding.hits", "hits", "Number of decoding cache hits");

	/// <summary>
	/// Counter tracking decoding cache misses.
	/// </summary>
	private static readonly Counter<long> DecodingMissCounter =
		CacheMeter.CreateCounter<long>("dispatch.utf8cache.decoding.misses", "misses", "Number of decoding cache misses");

	/// <summary>
	/// Shared instance for common usage.
	/// </summary>
	public static readonly Utf8StringCache Shared = new(maxCacheSize: 10000);

	private readonly ConcurrentDictionary<string, byte[]> _stringToBytes;
	private readonly ConcurrentDictionary<ByteArrayKey, string> _bytesToString;
	private readonly int _maxCacheSize;
	private readonly Timer _cleanupTimer;
	private int _currentSize;

	/// <summary>
	/// Statistics counters for encoding/decoding operations.
	/// </summary>
	private long _encodingHits;

	private long _encodingMisses;
	private long _decodingHits;
	private long _decodingMisses;

	/// <summary>
	/// Initializes a new instance of the <see cref="Utf8StringCache" /> class.
	/// </summary>
	/// <param name="maxCacheSize"> The maximum number of entries to cache. </param>
	public Utf8StringCache(int maxCacheSize = 1000)
	{
		_maxCacheSize = maxCacheSize;
		_stringToBytes = new ConcurrentDictionary<string, byte[]>(Environment.ProcessorCount, maxCacheSize, StringComparer.Ordinal);
		_bytesToString = new ConcurrentDictionary<ByteArrayKey, string>(Environment.ProcessorCount, maxCacheSize);

		// Cleanup timer runs every 5 minutes
		_cleanupTimer = new Timer(_ => CleanupIfNeeded(), state: null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
	}

	/// <summary>
	/// Gets UTF-8 bytes for the given string, using cache when possible.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public byte[] GetBytes(string value)
	{
		if (string.IsNullOrEmpty(value))
		{
			return [];
		}

		if (_stringToBytes.TryGetValue(value, out var cached))
		{
			_ = Interlocked.Increment(ref _encodingHits);
			EncodingHitCounter.Add(1);
			return cached;
		}

		_ = Interlocked.Increment(ref _encodingMisses);
		EncodingMissCounter.Add(1);
		var bytes = Encoding.UTF8.GetBytes(value);

		// Only cache if under size limit and string is reasonable size
		if (_currentSize < _maxCacheSize && value.Length < 1024 && _stringToBytes.TryAdd(value, bytes))
		{
			_ = Interlocked.Increment(ref _currentSize);

			// Also add to reverse cache
			_ = _bytesToString.TryAdd(new ByteArrayKey(bytes), value);
		}

		return bytes;
	}

	/// <summary>
	/// Gets UTF-8 bytes for the given string into a rented buffer.
	/// </summary>
	public int GetBytes(string value, out byte[] rentedBuffer)
	{
		if (string.IsNullOrEmpty(value))
		{
			rentedBuffer = [];
			return 0;
		}

		if (_stringToBytes.TryGetValue(value, out var cached))
		{
			_ = Interlocked.Increment(ref _encodingHits);
			EncodingHitCounter.Add(1);
			rentedBuffer = ArrayPool<byte>.Shared.Rent(cached.Length);
			cached.CopyTo(rentedBuffer, 0);
			return cached.Length;
		}

		_ = Interlocked.Increment(ref _encodingMisses);
		EncodingMissCounter.Add(1);
		var byteCount = Encoding.UTF8.GetByteCount(value);
		rentedBuffer = ArrayPool<byte>.Shared.Rent(byteCount);
		var actualBytes = Encoding.UTF8.GetBytes(value, rentedBuffer);

		// Cache if appropriate
		if (_currentSize < _maxCacheSize && value.Length < 1024)
		{
			var bytes = new byte[actualBytes];
			Buffer.BlockCopy(rentedBuffer, 0, bytes, 0, actualBytes);

			if (_stringToBytes.TryAdd(value, bytes))
			{
				_ = Interlocked.Increment(ref _currentSize);
				_ = _bytesToString.TryAdd(new ByteArrayKey(bytes), value);
			}
		}

		return actualBytes;
	}

	/// <summary>
	/// Gets a string from UTF-8 bytes, using cache when possible.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public string GetString(ReadOnlySpan<byte> bytes)
	{
		if (bytes.IsEmpty)
		{
			return string.Empty;
		}

		var key = new ByteArrayKey(bytes);
		if (_bytesToString.TryGetValue(key, out var cached))
		{
			_ = Interlocked.Increment(ref _decodingHits);
			DecodingHitCounter.Add(1);
			return cached;
		}

		_ = Interlocked.Increment(ref _decodingMisses);
		DecodingMissCounter.Add(1);
		var str = Encoding.UTF8.GetString(bytes);

		// Only cache if under size limit and string is reasonable size
		if (_currentSize < _maxCacheSize && str.Length < 1024)
		{
			var bytesCopy = bytes.ToArray();
			if (_bytesToString.TryAdd(new ByteArrayKey(bytesCopy), str))
			{
				_ = Interlocked.Increment(ref _currentSize);
				_ = _stringToBytes.TryAdd(str, bytesCopy);
			}
		}

		return str;
	}

	/// <summary>
	/// Gets cache statistics.
	/// </summary>
	public (long encodingHits, long encodingMisses, long decodingHits, long decodingMisses, int cacheSize) GetStatistics() =>
		(_encodingHits, _encodingMisses, _decodingHits, _decodingMisses, _currentSize);

	/// <summary>
	/// Clears the cache.
	/// </summary>
	public void Clear()
	{
		_stringToBytes.Clear();
		_bytesToString.Clear();
		_ = Interlocked.Exchange(ref _currentSize, 0);
	}

	/// <summary>
	/// Disposes the cache and releases all resources.
	/// </summary>
	public void Dispose()
	{
		_cleanupTimer?.Dispose();
		Clear();
	}

	private void CleanupIfNeeded()
	{
		// Cleanup if forward or reverse cache exceeds 80% capacity
		if (_currentSize > _maxCacheSize * 0.8 || _bytesToString.Count > _maxCacheSize)
		{
			var itemCount = _currentSize;
			var startTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();

			EvictionCounter.Add(1);
			Clear();

			ItemsRemovedCounter.Add(itemCount);
			var elapsedMs = System.Diagnostics.Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
			CleanupDurationHistogram.Record(elapsedMs);
		}
	}

	/// <summary>
	/// Key for byte array comparison in dictionary.
	/// </summary>
	private readonly struct ByteArrayKey : IEquatable<ByteArrayKey>
	{
		private readonly byte[] _bytes;
		private readonly int _hashCode;

		public ByteArrayKey(byte[] bytes)
		{
			_bytes = bytes;
			_hashCode = ComputeHashCode(bytes);
		}

		public ByteArrayKey(ReadOnlySpan<byte> bytes)
		{
			_bytes = bytes.ToArray();
			_hashCode = ComputeHashCode(_bytes);
		}

		public bool Equals(ByteArrayKey other) => _bytes.AsSpan().SequenceEqual(other._bytes);

		public override bool Equals(object? obj) => obj is ByteArrayKey other && Equals(other);

		public override int GetHashCode() => _hashCode;

		private static int ComputeHashCode(byte[] bytes)
		{
			unchecked
			{
				var hash = 17;
				for (var i = 0; i < bytes.Length; i++)
				{
					hash = (hash * 31) + bytes[i];
				}

				return hash;
			}
		}
	}
}
