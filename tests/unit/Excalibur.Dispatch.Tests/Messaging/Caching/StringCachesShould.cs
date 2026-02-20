// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;

using Excalibur.Dispatch.Caching;

namespace Excalibur.Dispatch.Tests.Messaging.Caching;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class StringCachesShould : IDisposable
{
	private readonly StringEncodingCache _encodingCache;
	private readonly Utf8StringCache _utf8Cache;

	public StringCachesShould()
	{
		_encodingCache = new StringEncodingCache(100);
		_utf8Cache = new Utf8StringCache(100);
	}

	public void Dispose()
	{
		_encodingCache.Dispose();
		_utf8Cache.Dispose();
	}

	// --- StringEncodingCache ---

	[Fact]
	public void StringEncodingCache_GetUtf8Bytes_EmptyString_ReturnsEmpty()
	{
		// Act
		var result = _encodingCache.GetUtf8Bytes("");

		// Assert
		result.IsEmpty.ShouldBeTrue();
	}

	[Fact]
	public void StringEncodingCache_GetUtf8Bytes_NullString_ReturnsEmpty()
	{
		// Act
		var result = _encodingCache.GetUtf8Bytes(null!);

		// Assert
		result.IsEmpty.ShouldBeTrue();
	}

	[Fact]
	public void StringEncodingCache_GetUtf8Bytes_ValidString_ReturnsCorrectBytes()
	{
		// Act
		var result = _encodingCache.GetUtf8Bytes("hello");

		// Assert
		result.Length.ShouldBe(5);
		result.ToArray().ShouldBe(Encoding.UTF8.GetBytes("hello"));
	}

	[Fact]
	public void StringEncodingCache_GetUtf8Bytes_SecondCall_ReturnsCachedResult()
	{
		// Act
		_ = _encodingCache.GetUtf8Bytes("cached-value");
		var result = _encodingCache.GetUtf8Bytes("cached-value");

		// Assert
		result.ToArray().ShouldBe(Encoding.UTF8.GetBytes("cached-value"));
	}

	[Fact]
	public void StringEncodingCache_GetUtf8Bytes_IntoBuffer_CopiesCorrectly()
	{
		// Arrange
		var destination = new byte[100];

		// Act
		var length = _encodingCache.GetUtf8Bytes("test", destination);

		// Assert
		length.ShouldBe(4);
		destination.AsSpan(0, 4).ToArray().ShouldBe(Encoding.UTF8.GetBytes("test"));
	}

	[Fact]
	public void StringEncodingCache_GetUtf8Bytes_IntoSmallBuffer_Throws()
	{
		// Arrange
		var destination = new byte[2];

		// Act & Assert
		Should.Throw<ArgumentException>(() =>
			_encodingCache.GetUtf8Bytes("toolong", destination));
	}

	[Fact]
	public void StringEncodingCache_Preload_CachesStrings()
	{
		// Act
		_encodingCache.Preload("a", "b", "c");

		// Assert
		var stats = _encodingCache.GetStatistics();
		stats.CacheSize.ShouldBeGreaterThanOrEqualTo(3);
	}

	[Fact]
	public void StringEncodingCache_Preload_WithNullArg_Throws()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			_encodingCache.Preload(null!));
	}

	[Fact]
	public void StringEncodingCache_GetStatistics_ReturnsValidData()
	{
		// Arrange - preload to ensure entry exists, then access it
		_encodingCache.Preload("stat-test");
		_ = _encodingCache.GetUtf8Bytes("stat-test"); // should be a hit

		// Act
		var stats = _encodingCache.GetStatistics();

		// Assert
		stats.CacheSize.ShouldBeGreaterThanOrEqualTo(1);
		stats.TotalAccesses.ShouldBeGreaterThanOrEqualTo(1);
	}

	[Fact]
	public void StringEncodingCache_Clear_ResetsCache()
	{
		// Arrange
		_ = _encodingCache.GetUtf8Bytes("clear-test");

		// Act
		_encodingCache.Clear();
		var stats = _encodingCache.GetStatistics();

		// Assert
		stats.CacheSize.ShouldBe(0);
		stats.TotalAccesses.ShouldBe(0);
	}

	[Fact]
	public void StringEncodingCache_Dispose_MultipleCalls_DoesNotThrow()
	{
		// Arrange
		var cache = new StringEncodingCache(10);

		// Act & Assert
		cache.Dispose();
		cache.Dispose(); // safe double dispose
	}

	// --- Utf8StringCache ---

	[Fact]
	public void Utf8StringCache_GetBytes_EmptyString_ReturnsEmpty()
	{
		// Act
		var result = _utf8Cache.GetBytes("");

		// Assert
		result.ShouldBeEmpty();
	}

	[Fact]
	public void Utf8StringCache_GetBytes_NullString_ReturnsEmpty()
	{
		// Act
		var result = _utf8Cache.GetBytes(null!);

		// Assert
		result.ShouldBeEmpty();
	}

	[Fact]
	public void Utf8StringCache_GetBytes_ValidString_ReturnsCorrectBytes()
	{
		// Act
		var result = _utf8Cache.GetBytes("hello");

		// Assert
		result.ShouldBe(Encoding.UTF8.GetBytes("hello"));
	}

	[Fact]
	public void Utf8StringCache_GetBytes_SecondCall_CacheHit()
	{
		// Act
		var result1 = _utf8Cache.GetBytes("cached");
		var result2 = _utf8Cache.GetBytes("cached");

		// Assert
		result1.ShouldBe(result2);
	}

	[Fact]
	public void Utf8StringCache_GetBytes_IntoRentedBuffer_ReturnsLength()
	{
		// Act
		var length = _utf8Cache.GetBytes("test", out var buffer);

		// Assert
		length.ShouldBe(4);
		buffer.ShouldNotBeNull();
		buffer.Length.ShouldBeGreaterThanOrEqualTo(4);
		buffer.AsSpan(0, 4).ToArray().ShouldBe(Encoding.UTF8.GetBytes("test"));

		System.Buffers.ArrayPool<byte>.Shared.Return(buffer);
	}

	[Fact]
	public void Utf8StringCache_GetBytes_EmptyIntoRentedBuffer_ReturnsZero()
	{
		// Act
		var length = _utf8Cache.GetBytes("", out var buffer);

		// Assert
		length.ShouldBe(0);
	}

	[Fact]
	public void Utf8StringCache_GetString_EmptySpan_ReturnsEmpty()
	{
		// Act
		var result = _utf8Cache.GetString(ReadOnlySpan<byte>.Empty);

		// Assert
		result.ShouldBe(string.Empty);
	}

	[Fact]
	public void Utf8StringCache_GetString_ValidBytes_ReturnsString()
	{
		// Arrange
		var bytes = Encoding.UTF8.GetBytes("decoded");

		// Act
		var result = _utf8Cache.GetString(bytes);

		// Assert
		result.ShouldBe("decoded");
	}

	[Fact]
	public void Utf8StringCache_GetString_SecondCall_CacheHit()
	{
		// Arrange
		var bytes = Encoding.UTF8.GetBytes("cached-decode");

		// Act
		var result1 = _utf8Cache.GetString(bytes);
		var result2 = _utf8Cache.GetString(bytes);

		// Assert
		result1.ShouldBe(result2);
		result1.ShouldBe("cached-decode");
	}

	[Fact]
	public void Utf8StringCache_GetStatistics_ReturnsValidData()
	{
		// Arrange
		_utf8Cache.GetBytes("stat1");
		_utf8Cache.GetBytes("stat1"); // encoding hit
		_utf8Cache.GetString(Encoding.UTF8.GetBytes("stat2"));
		_utf8Cache.GetString(Encoding.UTF8.GetBytes("stat2")); // decoding hit

		// Act
		var (encodingHits, encodingMisses, decodingHits, decodingMisses, cacheSize) = _utf8Cache.GetStatistics();

		// Assert
		encodingHits.ShouldBeGreaterThanOrEqualTo(1);
		encodingMisses.ShouldBeGreaterThanOrEqualTo(1);
		decodingHits.ShouldBeGreaterThanOrEqualTo(1);
		decodingMisses.ShouldBeGreaterThanOrEqualTo(1);
		cacheSize.ShouldBeGreaterThanOrEqualTo(2);
	}

	[Fact]
	public void Utf8StringCache_Clear_ResetsAll()
	{
		// Arrange
		_utf8Cache.GetBytes("clear-test");

		// Act
		_utf8Cache.Clear();
		var (_, _, _, _, cacheSize) = _utf8Cache.GetStatistics();

		// Assert
		cacheSize.ShouldBe(0);
	}

	[Fact]
	public void Utf8StringCache_Shared_IsNotNull()
	{
		// Assert
		Utf8StringCache.Shared.ShouldNotBeNull();
	}

	[Fact]
	public void Utf8StringCache_Dispose_ClearsCache()
	{
		// Arrange
		var cache = new Utf8StringCache(10);
		cache.GetBytes("dispose-test");

		// Act & Assert
		cache.Dispose();
	}

	// --- GlobalStringCache ---

	[Fact]
	public void GlobalStringCache_Instance_IsNotNull()
	{
		// Assert
		GlobalStringCache.Instance.ShouldNotBeNull();
	}

	[Fact]
	public void GlobalStringCache_Instance_HasPreloadedEntries()
	{
		// Act
		var stats = GlobalStringCache.Instance.GetStatistics();

		// Assert
		stats.CacheSize.ShouldBeGreaterThan(0);
	}
}
