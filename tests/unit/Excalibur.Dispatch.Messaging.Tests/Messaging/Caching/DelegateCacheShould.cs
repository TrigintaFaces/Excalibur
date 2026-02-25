// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Caching;

namespace Excalibur.Dispatch.Tests.Messaging.Caching;

/// <summary>
/// Tests for <see cref="DelegateCache"/> static class.
/// AD-258-2: Validates both string-based and struct-based cache key support.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class DelegateCacheShould : IDisposable
{
	public DelegateCacheShould()
	{
		// Clear cache before each test to ensure isolation
		DelegateCache.Clear();
	}

	public void Dispose()
	{
		// Clear cache after each test
		DelegateCache.Clear();
		GC.SuppressFinalize(this);
	}

	#region String Key Tests

	[Fact]
	public void CacheDelegate_WithStringKey()
	{
		// Arrange
		var factoryCallCount = 0;
		Func<Func<int, string>> factory = () =>
		{
			factoryCallCount++;
			return x => x.ToString();
		};

		// Act
		var delegate1 = DelegateCache.GetOrCreate("test_key", factory);
		var delegate2 = DelegateCache.GetOrCreate("test_key", factory);

		// Assert
		delegate1.ShouldBeSameAs(delegate2);
		factoryCallCount.ShouldBe(1); // Factory should only be called once
	}

	[Fact]
	public void ReturnDifferentDelegates_ForDifferentStringKeys()
	{
		// Arrange
		Func<Func<int, string>> factory1 = () => x => x.ToString();
		Func<Func<int, string>> factory2 = () => x => x.ToString("X");

		// Act
		var delegate1 = DelegateCache.GetOrCreate("key1", factory1);
		var delegate2 = DelegateCache.GetOrCreate("key2", factory2);

		// Assert
		delegate1.ShouldNotBeSameAs(delegate2);
	}

	[Fact]
	public void ThrowArgumentNullException_WhenFactoryIsNull_StringKey()
	{
		// Arrange & Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			DelegateCache.GetOrCreate<Func<int, string>>("test", null!));
	}

	#endregion String Key Tests

	#region Struct Key Tests

	[Fact]
	public void CacheDelegate_WithStructKey()
	{
		// Arrange
		var factoryCallCount = 0;
		Func<Func<int, string>> factory = () =>
		{
			factoryCallCount++;
			return x => x.ToString();
		};
		var key = new DelegateCacheKey("transform", "test", typeof(int), typeof(string));

		// Act
		var delegate1 = DelegateCache.GetOrCreate(key, factory);
		var delegate2 = DelegateCache.GetOrCreate(key, factory);

		// Assert
		delegate1.ShouldBeSameAs(delegate2);
		factoryCallCount.ShouldBe(1); // Factory should only be called once
	}

	[Fact]
	public void ReturnCachedDelegate_ForEqualStructKeys()
	{
		// Arrange
		var factoryCallCount = 0;
		Func<Func<int, string>> factory = () =>
		{
			factoryCallCount++;
			return x => x.ToString();
		};
		var key1 = new DelegateCacheKey("transform", "test", typeof(int), typeof(string));
		var key2 = new DelegateCacheKey("transform", "test", typeof(int), typeof(string));

		// Act
		var delegate1 = DelegateCache.GetOrCreate(key1, factory);
		var delegate2 = DelegateCache.GetOrCreate(key2, factory);

		// Assert
		delegate1.ShouldBeSameAs(delegate2);
		factoryCallCount.ShouldBe(1);
	}

	[Fact]
	public void ReturnDifferentDelegates_ForDifferentStructKeys()
	{
		// Arrange
		Func<Func<int, string>> factory1 = () => x => x.ToString();
		Func<Func<long, string>> factory2 = () => x => x.ToString();
		var key1 = new DelegateCacheKey("transform", "test", typeof(int), typeof(string));
		var key2 = new DelegateCacheKey("transform", "test", typeof(long), typeof(string));

		// Act
		var delegate1 = DelegateCache.GetOrCreate(key1, factory1);
		var delegate2 = DelegateCache.GetOrCreate(key2, factory2);

		// Assert
		delegate1.ShouldNotBeSameAs(delegate2);
	}

	[Fact]
	public void ThrowArgumentNullException_WhenFactoryIsNull_StructKey()
	{
		// Arrange
		var key = new DelegateCacheKey("transform", "test", typeof(int), typeof(string));

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			DelegateCache.GetOrCreate<Func<int, string>>(key, null!));
	}

	#endregion Struct Key Tests

	#region Statistics Tests

	[Fact]
	public void TrackCacheHits()
	{
		// Arrange
		var key = new DelegateCacheKey("transform", "stats", typeof(int), typeof(string));
		Func<Func<int, string>> factory = () => x => x.ToString();

		// Act
		_ = DelegateCache.GetOrCreate(key, factory); // Miss
		_ = DelegateCache.GetOrCreate(key, factory); // Hit
		_ = DelegateCache.GetOrCreate(key, factory); // Hit
		var stats = DelegateCache.GetStatistics();

		// Assert
		stats.hits.ShouldBeGreaterThanOrEqualTo(2);
		stats.misses.ShouldBeGreaterThanOrEqualTo(1);
	}

	[Fact]
	public void TrackCacheSize()
	{
		// Arrange
		Func<Func<int, string>> factory = () => x => x.ToString();

		// Act
		_ = DelegateCache.GetOrCreate(new DelegateCacheKey("transform", "a", typeof(int), typeof(string)), factory);
		_ = DelegateCache.GetOrCreate(new DelegateCacheKey("transform", "b", typeof(int), typeof(string)), factory);
		var stats = DelegateCache.GetStatistics();

		// Assert
		stats.cacheSize.ShouldBeGreaterThanOrEqualTo(2);
	}

	[Fact]
	public void ProvideDetailedStatistics()
	{
		// Arrange
		Func<Func<int, string>> factory = () => x => x.ToString();

		// Act
		_ = DelegateCache.GetOrCreate("string_key", factory);
		_ = DelegateCache.GetOrCreate(new DelegateCacheKey("transform", "struct", typeof(int), typeof(string)), factory);
		var stats = DelegateCache.GetDetailedStatistics();

		// Assert
		stats.stringCacheSize.ShouldBeGreaterThanOrEqualTo(1);
		stats.structCacheSize.ShouldBeGreaterThanOrEqualTo(1);
	}

	#endregion Statistics Tests

	#region Clear Tests

	[Fact]
	public void ClearBothCaches()
	{
		// Arrange
		Func<Func<int, string>> factory = () => x => x.ToString();
		_ = DelegateCache.GetOrCreate("string_key", factory);
		_ = DelegateCache.GetOrCreate(new DelegateCacheKey("transform", "struct", typeof(int), typeof(string)), factory);

		// Act
		DelegateCache.Clear();
		var stats = DelegateCache.GetStatistics();

		// Assert
		stats.cacheSize.ShouldBe(0);
	}

	#endregion Clear Tests

	#region Extension Method Tests

	[Fact]
	public void GetContinuation_ShouldCacheDelegate()
	{
		// Arrange
		Func<int, string> selector = x => x.ToString();

		// Act
		var continuation1 = "test".GetContinuation(selector);
		var continuation2 = "test".GetContinuation(selector);

		// Assert
		continuation1.ShouldBeSameAs(continuation2);
	}

	[Fact]
	public void GetErrorHandler_ShouldCacheDelegate()
	{
		// Arrange
		Func<Exception, bool> predicate = ex => ex is InvalidOperationException;

		// Act
		var handler1 = "test".GetErrorHandler(predicate);
		var handler2 = "test".GetErrorHandler(predicate);

		// Assert
		handler1.ShouldBeSameAs(handler2);
	}

	[Fact]
	public void GetTransform_ShouldCacheDelegate()
	{
		// Arrange
		Func<int, string> transform = x => x.ToString();

		// Act
		var cached1 = "test".GetTransform(transform);
		var cached2 = "test".GetTransform(transform);

		// Assert
		cached1.ShouldBeSameAs(cached2);
	}

	[Fact]
	public void GetContinuation_WithDifferentTypes_ShouldReturnDifferentDelegates()
	{
		// Arrange
		Func<int, string> selector1 = x => x.ToString();
		Func<long, string> selector2 = x => x.ToString();

		// Act
		var continuation1 = "test".GetContinuation(selector1);
		var continuation2 = "test".GetContinuation(selector2);

		// Assert
		continuation1.ShouldNotBeSameAs(continuation2);
	}

	#endregion Extension Method Tests
}
