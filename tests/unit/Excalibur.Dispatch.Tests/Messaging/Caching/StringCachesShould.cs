// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;

using Excalibur.Dispatch.Caching;

namespace Excalibur.Dispatch.Tests.Messaging.Caching;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class StringCachesShould : IDisposable
{
	private readonly Utf8StringCache _utf8Cache;

	public StringCachesShould()
	{
		_utf8Cache = new Utf8StringCache(100);
	}

	public void Dispose()
	{
		_utf8Cache.Dispose();
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
	public void Utf8StringCache_GetStatistics_ReturnsCacheSize()
	{
		// Arrange -- T.9 (Sprint 688): Interlocked counters removed; hit/miss now OTel-only.
		// GetStatistics() still returns the tuple shape but hit/miss fields are always 0.
		_utf8Cache.GetBytes("stat1");
		_utf8Cache.GetBytes("stat1"); // encoding hit (tracked via OTel, not Interlocked)
		_utf8Cache.GetString(Encoding.UTF8.GetBytes("stat2"));
		_utf8Cache.GetString(Encoding.UTF8.GetBytes("stat2")); // decoding hit

		// Act
		var (encodingHits, encodingMisses, decodingHits, decodingMisses, cacheSize) = _utf8Cache.GetStatistics();

		// Assert -- hit/miss counters return 0 after T.9 removal; only cacheSize is meaningful
		encodingHits.ShouldBe(0);
		encodingMisses.ShouldBe(0);
		decodingHits.ShouldBe(0);
		decodingMisses.ShouldBe(0);
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
}
