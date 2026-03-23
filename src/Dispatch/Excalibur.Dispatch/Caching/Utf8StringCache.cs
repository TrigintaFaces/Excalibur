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
internal sealed class Utf8StringCache : IDisposable
{
	private readonly Meter _cacheMeter;
	private readonly Counter<long> _evictionCounter;
	private readonly Counter<long> _itemsRemovedCounter;
	private readonly Histogram<double> _cleanupDurationHistogram;
	private readonly Counter<long> _encodingHitCounter;
	private readonly Counter<long> _encodingMissCounter;
	private readonly Counter<long> _decodingHitCounter;
	private readonly Counter<long> _decodingMissCounter;

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
	/// Initializes a new instance of the <see cref="Utf8StringCache" /> class.
	/// </summary>
	/// <param name="maxCacheSize"> The maximum number of entries to cache. </param>
	/// <param name="meterFactory"> Optional meter factory for DI-managed meter lifecycle. </param>
	public Utf8StringCache(int maxCacheSize = 1000, IMeterFactory? meterFactory = null)
	{
		_maxCacheSize = maxCacheSize;
		_stringToBytes = new ConcurrentDictionary<string, byte[]>(Environment.ProcessorCount, maxCacheSize, StringComparer.Ordinal);
		_bytesToString = new ConcurrentDictionary<ByteArrayKey, string>(Environment.ProcessorCount, maxCacheSize);

		_cacheMeter = meterFactory?.Create("Excalibur.Dispatch.Utf8StringCache") ?? new Meter("Excalibur.Dispatch.Utf8StringCache", "1.0.0");
		_evictionCounter = _cacheMeter.CreateCounter<long>("dispatch.utf8cache.evictions", "evictions", "Number of Utf8StringCache eviction cycles");
		_itemsRemovedCounter = _cacheMeter.CreateCounter<long>("dispatch.utf8cache.items_removed", "items", "Number of items removed during Utf8StringCache cleanup");
		_cleanupDurationHistogram = _cacheMeter.CreateHistogram<double>("dispatch.utf8cache.cleanup_duration", "ms", "Duration of Utf8StringCache cleanup operations in milliseconds");
		_encodingHitCounter = _cacheMeter.CreateCounter<long>("dispatch.utf8cache.encoding.hits", "hits", "Number of encoding cache hits");
		_encodingMissCounter = _cacheMeter.CreateCounter<long>("dispatch.utf8cache.encoding.misses", "misses", "Number of encoding cache misses");
		_decodingHitCounter = _cacheMeter.CreateCounter<long>("dispatch.utf8cache.decoding.hits", "hits", "Number of decoding cache hits");
		_decodingMissCounter = _cacheMeter.CreateCounter<long>("dispatch.utf8cache.decoding.misses", "misses", "Number of decoding cache misses");

		// Cleanup timer runs every 5 minutes
		_cleanupTimer = new Timer(_ => CleanupIfNeeded(), state: null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
	}

	/// <summary>
	/// Gets UTF-8 bytes for the given string, using cache when possible.
	/// Returns a defensive copy to prevent callers from corrupting the internal cache.
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
			_encodingHitCounter.Add(1);
			// Return a defensive copy to prevent callers from mutating the cached array
			return cached.AsSpan().ToArray();
		}

		_encodingMissCounter.Add(1);
		var bytes = Encoding.UTF8.GetBytes(value);

		// Only cache if under size limit and string is reasonable size
		if (_currentSize < _maxCacheSize && value.Length < 1024 && _stringToBytes.TryAdd(value, bytes))
		{
			_ = Interlocked.Increment(ref _currentSize);

			// Also add to reverse cache
			_ = _bytesToString.TryAdd(new ByteArrayKey(bytes), value);
		}

		// Return a defensive copy -- the cached array must not be mutated by callers
		return bytes.AsSpan().ToArray();
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
			_encodingHitCounter.Add(1);
			rentedBuffer = ArrayPool<byte>.Shared.Rent(cached.Length);
			cached.CopyTo(rentedBuffer, 0);
			return cached.Length;
		}

		_encodingMissCounter.Add(1);
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
			_decodingHitCounter.Add(1);
			return cached;
		}

		_decodingMissCounter.Add(1);
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
	/// Gets cache statistics. Hit/miss counters are tracked only via OTel instrumentation
	/// (no redundant Interlocked counters). Use OTel metric collection for hit/miss data.
	/// The returned tuple preserves the original shape for backward compatibility; hit/miss
	/// fields always return 0.
	/// </summary>
	public (long encodingHits, long encodingMisses, long decodingHits, long decodingMisses, int cacheSize) GetStatistics() =>
		(0, 0, 0, 0, _currentSize);

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
		_cacheMeter?.Dispose();
		Clear();
	}

	private void CleanupIfNeeded()
	{
		// Cleanup if forward or reverse cache exceeds 80% capacity
		if (_currentSize > _maxCacheSize * 0.8 || _bytesToString.Count > _maxCacheSize)
		{
			var itemCount = _currentSize;
			var startTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();

			_evictionCounter.Add(1);
			Clear();

			_itemsRemovedCounter.Add(itemCount);
			var elapsedMs = System.Diagnostics.Stopwatch.GetElapsedTime(startTimestamp).TotalMilliseconds;
			_cleanupDurationHistogram.Record(elapsedMs);
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
