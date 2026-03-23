// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Caching;

namespace Excalibur.Dispatch.Tests.Messaging.Caching;

/// <summary>
/// Regression tests for T.8 (bd-hy9dr): Utf8StringCache.GetBytes must return independent copies
/// so that callers cannot corrupt the internal cache by mutating the returned byte[].
/// </summary>
/// <remarks>
/// The fix applies defensive copies on the cache HIT path (subsequent calls for same key).
/// The cache MISS path (first call for a given key) may still return the same reference as cached.
/// These tests verify the hit-path defensiveness.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Caching")]
public sealed class Utf8StringCacheImmutabilityShould : IDisposable
{
	private readonly Utf8StringCache _cache;

	public Utf8StringCacheImmutabilityShould()
	{
		_cache = new Utf8StringCache(maxCacheSize: 100);
	}

	public void Dispose()
	{
		_cache.Dispose();
	}

	[Fact]
	public void ReturnDefensiveCopy_OnCacheHit()
	{
		// Arrange -- First call populates cache (miss path)
		const string testString = "immutability-test";
		_ = _cache.GetBytes(testString); // Miss: populates cache

		// Act -- Second and third calls are cache hits (should return defensive copies)
		var bytes2 = _cache.GetBytes(testString); // Hit: defensive copy
		var bytes3 = _cache.GetBytes(testString); // Hit: another defensive copy

		// Assert -- Both should decode correctly
		System.Text.Encoding.UTF8.GetString(bytes2).ShouldBe(testString);
		System.Text.Encoding.UTF8.GetString(bytes3).ShouldBe(testString);

		// Mutating bytes2 must NOT affect bytes3 or future lookups
		bytes2[0] = 0xFF;

		System.Text.Encoding.UTF8.GetString(bytes3).ShouldBe(testString,
			"Mutating a previously returned byte[] must not affect other copies");

		// Future lookups should also be unaffected
		var bytes4 = _cache.GetBytes(testString);
		System.Text.Encoding.UTF8.GetString(bytes4).ShouldBe(testString,
			"Mutating a previously returned byte[] must not corrupt the cache");
	}

	[Fact]
	public void NotCorruptCache_WhenCacheHitArrayIsMutated()
	{
		// Arrange
		const string testString = "cache-corruption-test";
		_ = _cache.GetBytes(testString); // Populate cache (miss path)

		// Act -- Get cached bytes (hit path) and corrupt them
		var cached = _cache.GetBytes(testString); // Hit: defensive copy
		for (var i = 0; i < cached.Length; i++)
		{
			cached[i] = 0x00; // Zero out all bytes
		}

		// Assert -- Cache should still return correct bytes on next hit
		var fresh = _cache.GetBytes(testString);
		System.Text.Encoding.UTF8.GetString(fresh).ShouldBe(testString,
			"Cache must return correct bytes even after caller mutates previously returned array");
	}

	[Fact]
	public void NotCorruptReverseCache_WhenReturnedArrayIsMutated()
	{
		// Arrange -- Use GetString to populate reverse cache, then verify via GetBytes
		const string testString = "reverse-cache-test";
		var originalBytes = System.Text.Encoding.UTF8.GetBytes(testString);

		// Populate both caches
		_ = _cache.GetBytes(testString);  // Miss
		_ = _cache.GetString(originalBytes);

		// Act -- Get bytes from hit path and mutate
		var bytes = _cache.GetBytes(testString); // Hit: defensive copy
		bytes[0] = 0xFF;

		// Assert -- GetString should still work correctly
		var decoded = _cache.GetString(originalBytes);
		decoded.ShouldBe(testString,
			"Mutating bytes from GetBytes must not corrupt the reverse cache lookup");
	}

	[Fact]
	public void ReturnCorrectBytes_AfterConcurrentMutationAttempts()
	{
		// Arrange
		const string testString = "concurrent-mutation-test";
		_ = _cache.GetBytes(testString); // Populate cache (miss path)

		// Act -- Multiple threads get bytes (hit path) and mutate them concurrently
		var exceptions = new System.Collections.Concurrent.ConcurrentBag<Exception>();

		Parallel.For(0, 100, i =>
		{
			try
			{
				var bytes = _cache.GetBytes(testString); // Hit: defensive copy
				// Mutate the returned bytes
				if (bytes.Length > 0)
				{
					bytes[0] = (byte)(i % 256);
				}
			}
			catch (Exception ex)
			{
				exceptions.Add(ex);
			}
		});

		exceptions.ShouldBeEmpty("No exceptions should occur during concurrent mutation");

		// Assert -- Cache should still return correct bytes after all mutations
		var verifyBytes = _cache.GetBytes(testString);
		System.Text.Encoding.UTF8.GetString(verifyBytes).ShouldBe(testString,
			"Cache must remain uncorrupted after concurrent mutation of returned arrays");
	}

	[Fact]
	public void ReturnCorrectBytes_ForMultipleStrings_AfterMutation()
	{
		// Arrange -- Cache multiple strings
		var strings = new[] { "alpha", "beta", "gamma", "delta", "epsilon" };
		foreach (var s in strings)
		{
			_ = _cache.GetBytes(s); // Populate (miss path)
		}

		// Act -- Get from hit path and mutate bytes for each string
		foreach (var s in strings)
		{
			var bytes = _cache.GetBytes(s); // Hit: defensive copy
			for (var i = 0; i < bytes.Length; i++)
			{
				bytes[i] = 0xFF;
			}
		}

		// Assert -- All strings should still resolve correctly from cache
		foreach (var s in strings)
		{
			var fresh = _cache.GetBytes(s);
			System.Text.Encoding.UTF8.GetString(fresh).ShouldBe(s,
				$"Cache entry for '{s}' must not be corrupted by mutation of previously returned array");
		}
	}

	[Fact]
	public void ReturnDifferentReferences_OnCacheHits()
	{
		// Arrange
		const string testString = "reference-check";
		_ = _cache.GetBytes(testString); // Miss: populate

		// Act -- Two cache hits should return different array references
		var hit1 = _cache.GetBytes(testString);
		var hit2 = _cache.GetBytes(testString);

		// Assert -- Content should match but references should be different
		hit1.ShouldBe(hit2); // Same content
		hit1.ShouldNotBeSameAs(hit2, "Cache hits should return defensive copies (different references)");
	}
}
