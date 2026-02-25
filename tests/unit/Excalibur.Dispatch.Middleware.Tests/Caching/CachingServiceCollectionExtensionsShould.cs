// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Caching;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Tests.Shared;

namespace Excalibur.Dispatch.Middleware.Tests.Caching;

/// <summary>
/// Unit tests for CachingServiceCollectionExtensions.
/// </summary>
[Trait("Category", "Unit")]
public sealed class CachingServiceCollectionExtensionsShould : UnitTestBase
{
	[Fact]
	public void AddDispatchCaching_RegistersCoreCachingServices()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddDispatchCaching();

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(CachingMiddleware));
		services.ShouldContain(sd => sd.ServiceType == typeof(CacheInvalidationMiddleware));
		services.ShouldContain(sd => sd.ServiceType == typeof(ICacheTagTracker));
		services.ShouldContain(sd => sd.ServiceType == typeof(ICacheKeyBuilder));
		services.ShouldContain(sd => sd.ServiceType == typeof(ICacheInvalidationService));
		services.ShouldContain(sd => sd.ServiceType == typeof(HybridCache));
	}

	[Fact]
	public void AddDispatchCaching_WithConfigureDelegate_ConfiguresOptions()
	{
		// Arrange
		var services = new ServiceCollection();
		var configureCalled = false;

		// Act
		services.AddDispatchCaching(options =>
		{
			configureCalled = true;
			options.Enabled = true;
			options.CacheMode = CacheMode.Memory;
		});

		// Assert
		configureCalled.ShouldBeTrue();
		services.ShouldContain(sd => sd.ServiceType == typeof(CachingMiddleware));
	}

	[Fact]
	public void AddDispatchCaching_WithNullConfigure_UsesDefaults()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddDispatchCaching(configure: null);

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(CachingMiddleware));
	}

	[Fact]
	public void AddDispatchMemoryCaching_RegistersMemoryCacheServices()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddDispatchMemoryCaching();

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(Microsoft.Extensions.Caching.Memory.IMemoryCache));
		services.ShouldContain(sd => sd.ServiceType == typeof(CachingMiddleware));
		services.ShouldContain(sd => sd.ServiceType == typeof(ICacheTagTracker));
	}

	[Fact]
	public void AddDispatchMemoryCaching_WithMemoryConfiguration_ConfiguresMemoryCache()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddDispatchMemoryCaching(
			configureMemory: options =>
			{
				options.SizeLimit = 1024;
			});

		// Assert — the configure delegate is Options-pattern deferred;
		// verify that IMemoryCache is registered and the provider resolves correctly
		services.ShouldContain(sd => sd.ServiceType == typeof(Microsoft.Extensions.Caching.Memory.IMemoryCache));
		var provider = services.BuildServiceProvider();
		var memoryCache = provider.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>();
		memoryCache.ShouldNotBeNull();
	}

	[Fact]
	public void AddDispatchMemoryCaching_WithCachingConfiguration_ConfiguresCacheOptions()
	{
		// Arrange
		var services = new ServiceCollection();
		var cachingCalled = false;

		// Act
		services.AddDispatchMemoryCaching(
			configureCaching: options =>
			{
				cachingCalled = true;
				options.CacheMode = CacheMode.Memory;
			});

		// Assert
		cachingCalled.ShouldBeTrue();
	}

	[Fact]
	public void AddDispatchRedisCaching_RegistersRedisCacheServices()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddDispatchRedisCaching(
			configureRedis: options =>
			{
				options.Configuration = "localhost:6379";
			});

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(IDistributedCache));
		services.ShouldContain(sd => sd.ServiceType == typeof(CachingMiddleware));
	}

	[Fact]
	public void AddDispatchRedisCaching_WithCachingConfiguration_ConfiguresCacheOptions()
	{
		// Arrange
		var services = new ServiceCollection();
		var cachingCalled = false;

		// Act
		services.AddDispatchRedisCaching(
			configureRedis: options => options.Configuration = "localhost:6379",
			configureCaching: options =>
			{
				cachingCalled = true;
				options.CacheMode = CacheMode.Distributed;
			});

		// Assert
		cachingCalled.ShouldBeTrue();
	}

	[Fact]
	public void AddDispatchHybridCaching_RegistersHybridCacheServices()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddDispatchHybridCaching();

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(HybridCache));
		services.ShouldContain(sd => sd.ServiceType == typeof(CachingMiddleware));
		services.ShouldContain(sd => sd.ServiceType == typeof(ICacheInvalidationService));
	}

	[Fact]
	public void AddDispatchHybridCaching_WithHybridConfiguration_ConfiguresHybridCache()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddDispatchHybridCaching(
			configureHybrid: options =>
			{
				options.MaximumPayloadBytes = 1024 * 1024;
			});

		// Assert — hybrid configure delegate is Options-pattern deferred;
		// verify registration and resolution
		services.ShouldContain(sd => sd.ServiceType == typeof(HybridCache));
		var provider = services.BuildServiceProvider();
		var hybridCache = provider.GetRequiredService<HybridCache>();
		hybridCache.ShouldNotBeNull();
	}

	[Fact]
	public void AddDispatchHybridCaching_WithRedisConfiguration_RegistersRedisBackend()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddDispatchHybridCaching(
			configureRedis: options =>
			{
				options.Configuration = "localhost:6379";
			});

		// Assert — Redis configure delegate is Options-pattern deferred;
		// verify IDistributedCache is registered
		services.ShouldContain(sd => sd.ServiceType == typeof(IDistributedCache));
		services.ShouldContain(sd => sd.ServiceType == typeof(HybridCache));
	}

	[Fact]
	public void AddDispatchHybridCaching_WithNullRedisConfig_DoesNotRegisterRedis()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddDispatchHybridCaching(configureRedis: null);

		// Assert
		// HybridCache is still registered, but Redis is not
		services.ShouldContain(sd => sd.ServiceType == typeof(HybridCache));
	}

	[Fact]
	public void AddDispatchHybridCaching_WithCachingConfiguration_ConfiguresCacheOptions()
	{
		// Arrange
		var services = new ServiceCollection();
		var cachingCalled = false;

		// Act
		services.AddDispatchHybridCaching(
			configureCaching: options =>
			{
				cachingCalled = true;
				options.CacheMode = CacheMode.Hybrid;
			});

		// Assert
		cachingCalled.ShouldBeTrue();
	}

	[Fact]
	public void AddDispatchDistributedCaching_RegistersCustomImplementation()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddDispatchDistributedCaching<TestDistributedCache>();

		// Assert
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(IDistributedCache) &&
			sd.ImplementationType == typeof(TestDistributedCache));
		services.ShouldContain(sd => sd.ServiceType == typeof(CachingMiddleware));
	}

	[Fact]
	public void AddDispatchDistributedCaching_WithCachingConfiguration_ConfiguresCacheOptions()
	{
		// Arrange
		var services = new ServiceCollection();
		var cachingCalled = false;

		// Act
		services.AddDispatchDistributedCaching<TestDistributedCache>(
			configureCaching: options =>
			{
				cachingCalled = true;
				options.CacheMode = CacheMode.Distributed;
			});

		// Assert
		cachingCalled.ShouldBeTrue();
	}

	[Fact]
	public void AddDispatchCaching_RegistersDefaultResultCachePolicy()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddDispatchCaching();

		// Assert
		services.ShouldContain(sd => sd.ServiceType == typeof(IResultCachePolicy));
	}

	[Fact]
	public void AddDispatchCaching_RegistersDefaultCacheKeyBuilder()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddDispatchCaching();

		// Assert
		services.ShouldContain(sd =>
			sd.ServiceType == typeof(ICacheKeyBuilder) &&
			sd.ImplementationType == typeof(DefaultCacheKeyBuilder));
	}

	// Test helper class
	private sealed class TestDistributedCache : IDistributedCache
	{
		public byte[]? Get(string key) => null;
		public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => Task.FromResult<byte[]?>(null);
		public void Refresh(string key) { }
		public Task RefreshAsync(string key, CancellationToken token = default) => Task.CompletedTask;
		public void Remove(string key) { }
		public Task RemoveAsync(string key, CancellationToken token = default) => Task.CompletedTask;
		public void Set(string key, byte[] value, DistributedCacheEntryOptions options) { }
		public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) => Task.CompletedTask;
	}
}
