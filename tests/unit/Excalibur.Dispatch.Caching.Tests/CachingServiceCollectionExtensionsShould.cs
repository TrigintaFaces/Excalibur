// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Caching;

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
		services.AddSingleton(A.Fake<Excalibur.Dispatch.Abstractions.Serialization.IJsonSerializer>());

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
		services.AddSingleton(A.Fake<Excalibur.Dispatch.Abstractions.Serialization.IJsonSerializer>());

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
		services.AddSingleton(A.Fake<Excalibur.Dispatch.Abstractions.Serialization.IJsonSerializer>());

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
		services.AddSingleton(A.Fake<Excalibur.Dispatch.Abstractions.Serialization.IJsonSerializer>());

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
		services.AddSingleton(A.Fake<Excalibur.Dispatch.Abstractions.Serialization.IJsonSerializer>());

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
		services.AddSingleton(A.Fake<Excalibur.Dispatch.Abstractions.Serialization.IJsonSerializer>());

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
		services.AddSingleton(A.Fake<Excalibur.Dispatch.Abstractions.Serialization.IJsonSerializer>());

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
		services.AddSingleton(A.Fake<Excalibur.Dispatch.Abstractions.Serialization.IJsonSerializer>());

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
		services.AddSingleton(A.Fake<Excalibur.Dispatch.Abstractions.Serialization.IJsonSerializer>());

		// Act
		services.AddDispatchCaching();

		// Assert — verify middleware descriptors are registered (can't resolve without all CachingMiddleware deps)
		services.ShouldContain(sd => sd.ServiceType == typeof(IDispatchMiddleware));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenRedisCacheConfigureIsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => services.AddDispatchRedisCaching(null!));
	}
}
