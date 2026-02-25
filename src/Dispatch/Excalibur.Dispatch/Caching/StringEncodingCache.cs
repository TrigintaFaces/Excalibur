// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text;


using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Diagnostics;
using Excalibur.Dispatch.Abstractions.Messaging;

namespace Excalibur.Dispatch.Caching;

/// <summary>
/// High-performance cache for UTF-8 encoded strings to avoid repeated encoding allocations. Identified as 5-8% CPU improvement opportunity
/// in profiling.
/// </summary>
public sealed class StringEncodingCache : IDisposable
{
	private readonly ConcurrentDictionary<string, CachedEncoding> _cache;
	private readonly int _maxCacheSize;
	private readonly Timer _cleanupTimer;
	private int _accessCount;
	private int _hitCount;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="StringEncodingCache" /> class.
	/// </summary>
	/// <param name="maxCacheSize"> The maximum number of entries to cache. </param>
	public StringEncodingCache(int maxCacheSize = 1000)
	{
		_maxCacheSize = maxCacheSize;
		_cache = new ConcurrentDictionary<string, CachedEncoding>(Environment.ProcessorCount, maxCacheSize, StringComparer.Ordinal);

		// Cleanup timer to evict LRU entries — wrap in try-catch to prevent timer leak on subsequent failure
		var timer = new Timer(Cleanup, state: null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
		try
		{
			_cleanupTimer = timer;
		}
		catch
		{
			timer.Dispose();
			throw;
		}
	}

	/// <summary>
	/// Get UTF-8 encoded bytes for a string, using cache if available.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public ReadOnlySpan<byte> GetUtf8Bytes(string value)
	{
		if (string.IsNullOrEmpty(value))
		{
			return [];
		}

		_ = Interlocked.Increment(ref _accessCount);

		// Try to get from cache
		if (_cache.TryGetValue(value, out var cached))
		{
			cached.AccessCount++;
			cached.LastAccessTimestamp = ValueStopwatch.GetTimestamp();
			_ = Interlocked.Increment(ref _hitCount);
			return new ReadOnlySpan<byte>(cached.Utf8Bytes, 0, cached.Length);
		}

		// Not in cache, encode and add
		return AddToCache(value);
	}

	/// <summary>
	/// Get UTF-8 encoded bytes into a buffer.
	/// </summary>
	/// <exception cref="ArgumentException"></exception>
	public int GetUtf8Bytes(string value, Span<byte> destination)
	{
		var bytes = GetUtf8Bytes(value);
		if (bytes.Length > destination.Length)
		{
			throw new ArgumentException(ErrorMessages.DestinationBufferTooSmall);
		}

		bytes.CopyTo(destination);
		return bytes.Length;
	}

	/// <summary>
	/// Pre-populate cache with common strings.
	/// </summary>
	public void Preload(params string[] commonStrings)
	{
		ArgumentNullException.ThrowIfNull(commonStrings);
		foreach (var str in commonStrings)
		{
			if (!string.IsNullOrEmpty(str))
			{
				_ = GetUtf8Bytes(str);
			}
		}
	}

	/// <summary>
	/// Gets cache statistics including size, access count, and hit rate.
	/// </summary>
	/// <returns> The cache statistics. </returns>
	public CacheStatistics GetStatistics() =>
		new()
		{
			CacheSize = _cache.Count,
			TotalAccesses = _accessCount,
			HitRate = _accessCount > 0 ? (double)_hitCount / _accessCount : 0,
		};

	/// <summary>
	/// Clears all cached entries and resets access count.
	/// </summary>
	public void Clear()
	{
		_cache.Clear();
		_accessCount = 0;
		_hitCount = 0;
	}

	/// <summary>
	/// Disposes the cache and releases all resources.
	/// </summary>
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		_cleanupTimer?.Dispose();
		_cache.Clear();
	}

	private ReadOnlySpan<byte> AddToCache(string value)
	{
		// Check cache size
		if (_cache.Count >= _maxCacheSize)
		{
			// Don't add if cache is full
			var bytes = Encoding.UTF8.GetBytes(value);
			return bytes;
		}

		// Encode
		var encoded = Encoding.UTF8.GetBytes(value);
		var cached = new CachedEncoding(encoded, encoded.Length);

		// Try to add to cache
		if (_cache.TryAdd(value, cached))
		{
			return new ReadOnlySpan<byte>(cached.Utf8Bytes, 0, cached.Length);
		}

		// Another thread added it, get their version
		if (_cache.TryGetValue(value, out var existing))
		{
			existing.AccessCount++;
			existing.LastAccessTimestamp = ValueStopwatch.GetTimestamp();
			return new ReadOnlySpan<byte>(existing.Utf8Bytes, 0, existing.Length);
		}

		// Fallback
		return encoded;
	}

	private void Cleanup(object? state)
	{
		if (_disposed || _cache.Count < _maxCacheSize / 2)
		{
			return;
		}

		try
		{
			// Find least recently used entries
			var entries = new List<KeyValuePair<string, CachedEncoding>>(_cache);
			entries.Sort(static (a, b) =>
			{
				// Sort by access count first, then by last access
				var countCompare = a.Value.AccessCount.CompareTo(b.Value.AccessCount);
				if (countCompare != 0)
				{
					return countCompare;
				}

				return a.Value.LastAccessTimestamp.CompareTo(b.Value.LastAccessTimestamp);
			});

			// Remove bottom 25%
			var removeCount = _cache.Count / 4;
			for (var i = 0; i < removeCount && i < entries.Count; i++)
			{
				_ = _cache.TryRemove(entries[i].Key, out _);
			}
		}
		catch (InvalidOperationException)
		{
			// Expected during concurrent modification of the collection — safe to ignore
		}
	}

	/// <summary>
	/// Represents a cached UTF-8 encoding result with access metadata.
	/// </summary>
	/// <param name="utf8Bytes"> The cached UTF-8 bytes. </param>
	/// <param name="length"> The number of meaningful bytes in the cached buffer. </param>
	internal sealed class CachedEncoding(byte[] utf8Bytes, int length)
	{
		/// <summary>
		/// Gets the cached UTF-8 bytes.
		/// </summary>
		/// <value>The current <see cref="Utf8Bytes"/> value.</value>
		public byte[] Utf8Bytes { get; } = utf8Bytes;

		/// <summary>
		/// Gets the number of meaningful bytes in <see cref="Utf8Bytes" />.
		/// </summary>
		/// <value>The current <see cref="Length"/> value.</value>
		public int Length { get; } = length;

		/// <summary>
		/// Gets or sets how many times this cache entry has been accessed.
		/// </summary>
		/// <value>The current <see cref="AccessCount"/> value.</value>
		public int AccessCount { get; set; } = 1;

		/// <summary>
		/// Gets or sets the last access timestamp captured via <see cref="ValueStopwatch" />.
		/// </summary>
		/// <value>
		/// The last access timestamp captured via <see cref="ValueStopwatch" />.
		/// </value>
		public long LastAccessTimestamp { get; set; } = ValueStopwatch.GetTimestamp();
	}
}
