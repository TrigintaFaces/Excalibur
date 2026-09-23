// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Caching;
using Excalibur.Dispatch.Serialization;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Caching.Tests;

[Trait("Category", "Unit")]
[Trait("Component", "Caching")]
public sealed class CachingServiceCollectionExtensionsShould
{
	[Fact]
	public void RegisterCachingServices_WithDefaults()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddSingleton(new DispatchJsonSerializer());

		// Act
		services.AddDispatchCaching();

		// Assert
		var sp = services.BuildServiceProvider();
		sp.GetService<ICacheKeyBuilder>().ShouldNotBeNull();
		sp.GetService<ICacheTagTracker>().ShouldNotBeNull();
		sp.GetService<IResultCachePolicy>().ShouldNotBeNull();
		sp.GetService<ICacheInvalidationService>().ShouldNotBeNull();
	}

	[Fact]
	public void RegisterCachingServices_WithCustomConfiguration()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddSingleton(new DispatchJsonSerializer());

		// Act
		services.AddDispatchCaching(opts =>
		{
			opts.Enabled = true;
			opts.CacheMode = CacheMode.Memory;
		});

		// Assert
		var sp = services.BuildServiceProvider();
		var options = sp.GetRequiredService<IOptions<CacheOptions>>().Value;
		options.Enabled.ShouldBeTrue();
		options.CacheMode.ShouldBe(CacheMode.Memory);
	}

	[Fact]
	public void RegisterMemoryCaching()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddSingleton(new DispatchJsonSerializer());

		// Act
		services.AddDispatchMemoryCaching();

		// Assert
		var sp = services.BuildServiceProvider();
		var options = sp.GetRequiredService<IOptions<CacheOptions>>().Value;
		options.Enabled.ShouldBeTrue();
		options.CacheMode.ShouldBe(CacheMode.Memory);
	}

	[Fact]
	public void RegisterHybridCaching_WithEnabledAndDistributedCache()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddSingleton(new DispatchJsonSerializer());

		// Act
		services.AddDispatchHybridCaching();

		// Assert
		var sp = services.BuildServiceProvider();
		var options = sp.GetRequiredService<IOptions<CacheOptions>>().Value;
		options.Enabled.ShouldBeTrue();
		// UseDistributedCache is true for Hybrid mode (property copier resolves CacheMode via UseDistributedCache setter)
		options.UseDistributedCache.ShouldBeTrue();
	}

	[Fact]
	public void RegisterValidator()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddSingleton(new DispatchJsonSerializer());

		// Act
		services.AddDispatchCaching();

		// Assert
		var sp = services.BuildServiceProvider();
		var validators = sp.GetServices<IValidateOptions<CacheOptions>>();
		validators.ShouldNotBeEmpty();
	}

	[Fact]
	public void RegisterDefaultCacheKeyBuilder()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddSingleton(new DispatchJsonSerializer());

		// Act
		services.AddDispatchCaching();

		// Assert
		var sp = services.BuildServiceProvider();
		var keyBuilder = sp.GetService<ICacheKeyBuilder>();
		keyBuilder.ShouldNotBeNull();
		keyBuilder.ShouldBeOfType<DefaultCacheKeyBuilder>();
	}

	[Fact]
	public void RegisterDefaultResultCachePolicy()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddSingleton(new DispatchJsonSerializer());

		// Act
		services.AddDispatchCaching();

		// Assert
		var sp = services.BuildServiceProvider();
		var policy = sp.GetService<IResultCachePolicy>();
		policy.ShouldNotBeNull();
		// Default policy should always return true
		policy.ShouldCache(A.Fake<IDispatchMessage>(), null).ShouldBeTrue();
	}

	[Fact]
	public void RegisterInMemoryCacheTagTracker()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddSingleton(new DispatchJsonSerializer());

		// Act
		services.AddDispatchCaching();

		// Assert
		var sp = services.BuildServiceProvider();
		var tracker = sp.GetService<ICacheTagTracker>();
		tracker.ShouldNotBeNull();
		tracker.ShouldBeOfType<InMemoryCacheTagTracker>();
	}

	[Fact]
	public void RegisterMiddlewareWrappers_InServiceDescriptors()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddSingleton(new DispatchJsonSerializer());

		// Act
		services.AddDispatchCaching();

		// S717 T.2: middleware registered as concrete type only
		// Assert — verify middleware concrete types are registered (can't resolve without all CachingMiddleware deps)
		services.ShouldContain(sd => sd.ServiceType == typeof(CachingMiddleware));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenRedisCacheConfigureIsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => services.AddDispatchRedisCaching((Action<RedisCacheOptions>)null!));
	}

	[Fact]
	public void DisposeATypeRegisteredDistributedCacheBackend_WithTheContainer()
	{
		// 454edu T3: AddStackExchangeRedisCache (the shipped Redis backend) registers via
		// ServiceDescriptor.Singleton<IDistributedCache, RedisCacheImpl>() -- a concrete TYPE, not an
		// instance or factory. Before this fix, ResolveOriginalDistributedCache built that instance with
		// ActivatorUtilities.CreateInstance, which the container never tracks -- a disposable backend
		// (RedisCache owns a ConnectionMultiplexer) leaked for the container's entire lifetime. This arm
		// reproduces the exact registration shape with a fake disposable backend and asserts the container
		// disposes it, without relying on a real Redis connection.
		var services = new ServiceCollection();
		services.AddSingleton(new DispatchJsonSerializer());
		_ = services.AddSingleton<IDistributedCache, DisposableTypeRegisteredCache>();

		services.AddDispatchCaching(o => o.CacheMode = CacheMode.Distributed);

		var provider = services.BuildServiceProvider();
		var resolved = provider.GetRequiredService<IDistributedCache>();
		resolved.ShouldBeOfType<TimeoutDistributedCache>("the decorator must still wrap the type-registered backend");

		var backend = provider.GetRequiredService<DisposableTypeRegisteredCache>();
		backend.Disposed.ShouldBeFalse("not yet disposed -- the container has not been disposed yet");

		provider.Dispose();

		backend.Disposed.ShouldBeTrue(
			"the type-registered backend must be resolved through the container (not ActivatorUtilities."
			+ "CreateInstance) so the container disposes it along with everything else it owns");
	}

	/// <summary>A minimal disposable <see cref="IDistributedCache"/> registered by TYPE, as the shipped Redis backend is.</summary>
	private sealed class DisposableTypeRegisteredCache : IDistributedCache, IDisposable
	{
		public bool Disposed { get; private set; }

		public byte[]? Get(string key) => null;

		public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => Task.FromResult<byte[]?>(null);

		public void Refresh(string key)
		{
		}

		public Task RefreshAsync(string key, CancellationToken token = default) => Task.CompletedTask;

		public void Remove(string key)
		{
		}

		public Task RemoveAsync(string key, CancellationToken token = default) => Task.CompletedTask;

		public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
		{
		}

		public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) => Task.CompletedTask;

		public void Dispose() => Disposed = true;
	}
}
