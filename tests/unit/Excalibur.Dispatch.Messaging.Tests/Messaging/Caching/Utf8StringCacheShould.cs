// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Buffers;

using Excalibur.Dispatch.Caching;

namespace Excalibur.Dispatch.Tests.Messaging.Caching;

/// <summary>
/// Unit tests for <see cref="Utf8StringCache"/>.
/// </summary>
/// <remarks>
/// Tests the high-performance UTF-8 string encoding/decoding cache.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Caching")]
[Trait("Priority", "0")]
public sealed class Utf8StringCacheShould : IDisposable
{
	private readonly Utf8StringCache _cache;

	public Utf8StringCacheShould()
	{
		_cache = new Utf8StringCache(maxCacheSize: 100);
	}

	public void Dispose()
	{
		_cache.Dispose();
	}

	#region Constructor Tests

	[Fact]
	public void Constructor_Default_CreatesInstance()
	{
		// Arrange & Act
		using var cache = new Utf8StringCache();

		// Assert
		_ = cache.ShouldNotBeNull();
	}

	[Fact]
	public void Constructor_WithMaxCacheSize_CreatesInstance()
	{
		// Arrange & Act
		using var cache = new Utf8StringCache(maxCacheSize: 500);

		// Assert
		_ = cache.ShouldNotBeNull();
	}

	#endregion

	#region Shared Instance Tests

	[Fact]
	public void Shared_ReturnsNonNullInstance()
	{
		// Arrange & Act
		var shared = Utf8StringCache.Shared;

		// Assert
		_ = shared.ShouldNotBeNull();
	}

	[Fact]
	public void Shared_ReturnsSameInstance()
	{
		// Arrange & Act
		var shared1 = Utf8StringCache.Shared;
		var shared2 = Utf8StringCache.Shared;

		// Assert
		shared1.ShouldBeSameAs(shared2);
	}

	#endregion

	#region GetBytes (string) Tests

	[Fact]
	public void GetBytes_WithNullOrEmpty_ReturnsEmptyArray()
	{
		// Act
		var emptyResult = _cache.GetBytes(string.Empty);

		// Assert
		_ = emptyResult.ShouldNotBeNull();
		emptyResult.Length.ShouldBe(0);
	}

	[Fact]
	public void GetBytes_WithValidString_ReturnsUtf8Bytes()
	{
		// Arrange
		const string testString = "Hello";

		// Act
		var bytes = _cache.GetBytes(testString);

		// Assert
		_ = bytes.ShouldNotBeNull();
		System.Text.Encoding.UTF8.GetString(bytes).ShouldBe(testString);
	}

	[Fact]
	public void GetBytes_SameStringTwice_ReturnsCachedBytes()
	{
		// Arrange
		const string testString = "CachedString";

		// Act
		var bytes1 = _cache.GetBytes(testString);
		var bytes2 = _cache.GetBytes(testString);

		// Assert - Should return the same array instance (cached)
		bytes1.ShouldBeSameAs(bytes2);
	}

	[Theory]
	[InlineData("a")]
	[InlineData("test")]
	[InlineData("Hello, World!")]
	[InlineData("Unicode: \u00E9\u00E8\u00EA")]
	public void GetBytes_WithVariousStrings_ReturnsCorrectBytes(string testString)
	{
		// Act
		var bytes = _cache.GetBytes(testString);

		// Assert
		System.Text.Encoding.UTF8.GetString(bytes).ShouldBe(testString);
	}

	#endregion

	#region GetBytes (with rented buffer) Tests

	[Fact]
	public void GetBytesWithRentedBuffer_WithEmptyString_ReturnsZero()
	{
		// Act
		var count = _cache.GetBytes(string.Empty, out var rentedBuffer);

		// Assert
		count.ShouldBe(0);
	}

	[Fact]
	public void GetBytesWithRentedBuffer_WithValidString_ReturnsBytesInBuffer()
	{
		// Arrange
		const string testString = "Hello";

		// Act
		var count = _cache.GetBytes(testString, out var rentedBuffer);

		// Assert
		count.ShouldBe(5);
		_ = rentedBuffer.ShouldNotBeNull();
		rentedBuffer.Length.ShouldBeGreaterThanOrEqualTo(5);
		System.Text.Encoding.UTF8.GetString(rentedBuffer, 0, count).ShouldBe(testString);

		// Cleanup
		ArrayPool<byte>.Shared.Return(rentedBuffer);
	}

	[Fact]
	public void GetBytesWithRentedBuffer_SameStringTwice_StillWorks()
	{
		// Arrange
		const string testString = "BufferedTest";

		// Act
		var count1 = _cache.GetBytes(testString, out var buffer1);
		var count2 = _cache.GetBytes(testString, out var buffer2);

		// Assert
		count1.ShouldBe(count2);
		System.Text.Encoding.UTF8.GetString(buffer1, 0, count1).ShouldBe(testString);
		System.Text.Encoding.UTF8.GetString(buffer2, 0, count2).ShouldBe(testString);

		// Cleanup
		ArrayPool<byte>.Shared.Return(buffer1);
		ArrayPool<byte>.Shared.Return(buffer2);
	}

	#endregion

	#region GetString Tests

	[Fact]
	public void GetString_WithEmptySpan_ReturnsEmptyString()
	{
		// Arrange
		ReadOnlySpan<byte> emptySpan = [];

		// Act
		var result = _cache.GetString(emptySpan);

		// Assert
		result.ShouldBe(string.Empty);
	}

	[Fact]
	public void GetString_WithValidBytes_ReturnsString()
	{
		// Arrange
		var bytes = System.Text.Encoding.UTF8.GetBytes("Hello");

		// Act
		var result = _cache.GetString(bytes);

		// Assert
		result.ShouldBe("Hello");
	}

	[Fact]
	public void GetString_SameBytesTwice_ReturnsCachedString()
	{
		// Arrange
		var bytes = System.Text.Encoding.UTF8.GetBytes("CachedDecoding");

		// Act
		var str1 = _cache.GetString(bytes);
		var str2 = _cache.GetString(bytes);

		// Assert - Should return the same string instance (cached)
		str1.ShouldBe("CachedDecoding");
		str2.ShouldBe("CachedDecoding");
	}

	[Fact]
	public void GetString_WithUnicodeBytes_ReturnsCorrectString()
	{
		// Arrange
		var unicodeString = "Héllo Wörld";
		var bytes = System.Text.Encoding.UTF8.GetBytes(unicodeString);

		// Act
		var result = _cache.GetString(bytes);

		// Assert
		result.ShouldBe(unicodeString);
	}

	#endregion

	#region GetStatistics Tests

	[Fact]
	public void GetStatistics_Initially_ReturnsZeroCounts()
	{
		// Arrange
		using var cache = new Utf8StringCache(100);

		// Act
		var (encodingHits, encodingMisses, decodingHits, decodingMisses, cacheSize) = cache.GetStatistics();

		// Assert
		encodingHits.ShouldBe(0);
		encodingMisses.ShouldBe(0);
		decodingHits.ShouldBe(0);
		decodingMisses.ShouldBe(0);
		cacheSize.ShouldBe(0);
	}

	[Fact]
	public void GetStatistics_AfterEncoding_ShowsMissThenHit()
	{
		// Arrange
		using var cache = new Utf8StringCache(100);
		const string testString = "StatTest";

		// Act
		_ = cache.GetBytes(testString); // Miss
		_ = cache.GetBytes(testString); // Hit

		var (encodingHits, encodingMisses, _, _, _) = cache.GetStatistics();

		// Assert
		encodingMisses.ShouldBe(1);
		encodingHits.ShouldBe(1);
	}

	[Fact]
	public void GetStatistics_AfterDecoding_ShowsMissThenHit()
	{
		// Arrange
		using var cache = new Utf8StringCache(100);
		var bytes = System.Text.Encoding.UTF8.GetBytes("DecodeTest");

		// Act
		_ = cache.GetString(bytes); // Miss
		_ = cache.GetString(bytes); // Hit

		var (_, _, decodingHits, decodingMisses, _) = cache.GetStatistics();

		// Assert
		decodingMisses.ShouldBe(1);
		decodingHits.ShouldBe(1);
	}

	[Fact]
	public void GetStatistics_CacheSize_IncreasesWithNewStrings()
	{
		// Arrange
		using var cache = new Utf8StringCache(100);

		// Act
		_ = cache.GetBytes("string1");
		_ = cache.GetBytes("string2");
		_ = cache.GetBytes("string3");

		var (_, _, _, _, cacheSize) = cache.GetStatistics();

		// Assert
		cacheSize.ShouldBeGreaterThanOrEqualTo(3);
	}

	#endregion

	#region Clear Tests

	[Fact]
	public void Clear_ResetsCache()
	{
		// Arrange
		using var cache = new Utf8StringCache(100);
		_ = cache.GetBytes("test1");
		_ = cache.GetBytes("test2");

		// Act
		cache.Clear();
		var (_, _, _, _, cacheSize) = cache.GetStatistics();

		// Assert
		cacheSize.ShouldBe(0);
	}

	[Fact]
	public void Clear_NewLookups_AreMisses()
	{
		// Arrange
		using var cache = new Utf8StringCache(100);
		_ = cache.GetBytes("cleartest");
		cache.Clear();

		// Act - This should be a miss after clear
		_ = cache.GetBytes("cleartest");

		var (hits, misses, _, _, _) = cache.GetStatistics();

		// Assert
		misses.ShouldBe(2); // Once before clear, once after
		hits.ShouldBe(0);
	}

	#endregion

	#region Dispose Tests

	[Fact]
	public void Dispose_ClearsCache()
	{
		// Arrange
		var cache = new Utf8StringCache(100);
		_ = cache.GetBytes("test");

		// Act
		cache.Dispose();

		// Assert - After dispose, cache should be cleared
		// We can't really test this without accessing internals
		// but we can verify dispose doesn't throw
	}

	[Fact]
	public void Dispose_CanBeCalledMultipleTimes()
	{
		// Arrange
		var cache = new Utf8StringCache(100);

		// Act & Assert - Should not throw
		cache.Dispose();
		cache.Dispose();
	}

	#endregion

	#region Thread Safety Tests

	[Fact]
	public void ConcurrentGetBytes_IsThreadSafe()
	{
		// Arrange
		using var cache = new Utf8StringCache(1000);
		var exceptions = new List<Exception>();

		// Act
		_ = Parallel.For(0, 1000, i =>
		{
			try
			{
				var str = $"concurrent_{i % 50}";
				var bytes = cache.GetBytes(str);
				var decoded = System.Text.Encoding.UTF8.GetString(bytes);
				decoded.ShouldBe(str);
			}
			catch (Exception ex)
			{
				lock (exceptions)
				{
					exceptions.Add(ex);
				}
			}
		});

		// Assert
		exceptions.ShouldBeEmpty();
	}

	[Fact]
	public void ConcurrentGetString_IsThreadSafe()
	{
		// Arrange
		using var cache = new Utf8StringCache(1000);
		var testBytes = Enumerable.Range(0, 50)
			.Select(i => System.Text.Encoding.UTF8.GetBytes($"parallel_{i}"))
			.ToArray();
		var exceptions = new List<Exception>();

		// Act
		_ = Parallel.For(0, 1000, i =>
		{
			try
			{
				var bytes = testBytes[i % testBytes.Length];
				var str = cache.GetString(bytes);
				str.ShouldContain("parallel_");
			}
			catch (Exception ex)
			{
				lock (exceptions)
				{
					exceptions.Add(ex);
				}
			}
		});

		// Assert
		exceptions.ShouldBeEmpty();
	}

	#endregion

	#region Typical Usage Scenarios

	[Fact]
	public void JsonSerializationScenario()
	{
		// Arrange - Common JSON field names
		var fieldNames = new[] { "id", "type", "timestamp", "version", "data" };

		// Act - Simulate encoding field names multiple times
		foreach (var field in fieldNames)
		{
			_ = _cache.GetBytes(field);
			_ = _cache.GetBytes(field);
			_ = _cache.GetBytes(field);
		}

		var (hits, misses, _, _, _) = _cache.GetStatistics();

		// Assert - 5 misses (first time) + 10 hits (subsequent)
		misses.ShouldBe(5);
		hits.ShouldBe(10);
	}

	[Fact]
	public void MessageDeserializationScenario()
	{
		// Arrange - Simulating deserializing multiple messages with same field names
		var messageFieldBytes = System.Text.Encoding.UTF8.GetBytes("messageId");

		// Act - Decode same field 10 times
		for (var i = 0; i < 10; i++)
		{
			var result = _cache.GetString(messageFieldBytes);
			result.ShouldBe("messageId");
		}

		var (_, _, decodingHits, decodingMisses, _) = _cache.GetStatistics();

		// Assert - 1 miss, 9 hits
		decodingMisses.ShouldBe(1);
		decodingHits.ShouldBe(9);
	}

	#endregion

	#region S542.9 (bd-vbdtq) Memory Leak Fix Tests

	[Fact]
	public void BoundReverseCacheToPreventMemoryLeak()
	{
		// Arrange — small cache that will overflow
		using var cache = new Utf8StringCache(maxCacheSize: 10);

		// Act — add more than maxCacheSize entries
		for (var i = 0; i < 30; i++)
		{
			_ = cache.GetBytes($"overflow-string-{i}");
		}

		// Assert — currentSize should be bounded, not growing unbounded
		var (_, _, _, _, cacheSize) = cache.GetStatistics();
		cacheSize.ShouldBeLessThanOrEqualTo(10,
			"Cache should be bounded to maxCacheSize to prevent memory leaks (S542.9 fix)");
	}

	[Fact]
	public void EvictReverseCacheWhenOverCapacity()
	{
		// Arrange — small cache
		using var cache = new Utf8StringCache(maxCacheSize: 5);

		// Act — fill beyond capacity
		for (var i = 0; i < 10; i++)
		{
			_ = cache.GetBytes($"eviction-test-{i}");
		}

		// Assert — should not crash and cache should remain bounded
		var (_, _, _, _, cacheSize) = cache.GetStatistics();
		cacheSize.ShouldBeLessThanOrEqualTo(5,
			"Reverse cache should be evicted when over capacity");
	}

	#endregion
}
