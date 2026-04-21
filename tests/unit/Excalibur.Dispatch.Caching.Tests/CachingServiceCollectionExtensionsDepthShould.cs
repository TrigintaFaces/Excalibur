// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Caching;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Caching.Tests;

/// <summary>
/// Depth tests for <see cref="CachingServiceCollectionExtensions"/> covering
/// AddDispatchDistributedCaching, idempotent registration, and configuration propagation.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Caching")]
public sealed class CachingServiceCollectionExtensionsDepthShould
{
	[Fact]
	public void RegisterDistributedCaching_WithCustomImplementation()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddSingleton(new DispatchJsonSerializer());

		// Act
		services.AddDispatchDistributedCaching<FakeDistributedCache>();

		// Assert
		var sp = services.BuildServiceProvider();
		var options = sp.GetRequiredService<IOptions<CacheOptions>>().Value;
		options.Enabled.ShouldBeTrue();
		options.CacheMode.ShouldBe(CacheMode.Distributed);

		var distributedCache = sp.GetService<IDistributedCache>();
		distributedCache.ShouldNotBeNull();
		distributedCache.ShouldBeOfType<FakeDistributedCache>();
	}

	[Fact]
	public void RegisterDistributedCaching_WithCustomConfiguration()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddSingleton(new DispatchJsonSerializer());

		// Act
		services.AddDispatchDistributedCaching<FakeDistributedCache>(opts =>
		{
			opts.DefaultTags = ["custom-tag"];
		});

		// Assert
		var sp = services.BuildServiceProvider();
		var options = sp.GetRequiredService<IOptions<CacheOptions>>().Value;
		options.DefaultTags.ShouldContain("custom-tag");
	}

	[Fact]
	public void NotDuplicateServices_WhenCalledMultipleTimes()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddSingleton(new DispatchJsonSerializer());

		// Act -- call twice
		services.AddDispatchCaching();
		services.AddDispatchCaching();

		// Assert -- services should use TryAdd, so no duplicates
		var sp = services.BuildServiceProvider();
		var keyBuilders = sp.GetServices<ICacheKeyBuilder>().ToList();
		keyBuilders.Count.ShouldBe(1);

		var tagTrackers = sp.GetServices<ICacheTagTracker>().ToList();
		tagTrackers.Count.ShouldBe(1);
	}

	[Fact]
	public void RegisterMemoryCaching_WithCustomMemoryOptions()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddSingleton(new DispatchJsonSerializer());

		// Act
		services.AddDispatchMemoryCaching(
			configureMemory: opts => opts.SizeLimit = 1024,
			configureCaching: opts => opts.DefaultTags = ["mem-tag"]);

		// Assert
		var sp = services.BuildServiceProvider();
		var options = sp.GetRequiredService<IOptions<CacheOptions>>().Value;
		options.CacheMode.ShouldBe(CacheMode.Memory);
		options.DefaultTags.ShouldContain("mem-tag");
	}

	[Fact]
	public void RegisterHybridCaching_WithCustomHybridOptions()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddSingleton(new DispatchJsonSerializer());

		// Act
		services.AddDispatchHybridCaching(
			configureHybrid: opts => opts.MaximumPayloadBytes = 1024 * 1024);

		// Assert
		var sp = services.BuildServiceProvider();
		var hybridOptions = sp.GetRequiredService<IOptions<HybridCacheOptions>>().Value;
		hybridOptions.MaximumPayloadBytes.ShouldBe(1024 * 1024);
	}

	[Fact]
	public void RegisterCacheOptionsValidator_AsIValidateOptions()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddSingleton(new DispatchJsonSerializer());

		// Act
		services.AddDispatchCaching();

		// Assert
		var sp = services.BuildServiceProvider();
		var validators = sp.GetServices<IValidateOptions<CacheOptions>>().ToList();
		validators.ShouldContain(v => v is CacheOptionsValidator);
	}

	[Fact]
	public void RegisterHybridCache_AlwaysEvenForMemoryMode()
	{
		// Arrange -- even memory mode needs HybridCache for CachingMiddleware
		var services = new ServiceCollection();
		services.AddSingleton(new DispatchJsonSerializer());

		// Act
		services.AddDispatchMemoryCaching();

		// Assert
		var sp = services.BuildServiceProvider();
		var hybridCache = sp.GetService<HybridCache>();
		hybridCache.ShouldNotBeNull();
	}

	[Fact]
	public void RegisterDefaultPolicy_ThatAlwaysReturnsTrue()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddSingleton(new DispatchJsonSerializer());

		// Act
		services.AddDispatchCaching();

		// Assert
		var sp = services.BuildServiceProvider();
		var policy = sp.GetRequiredService<IResultCachePolicy>();
		policy.ShouldNotBeNull();

		// Default policy should cache everything
		policy.ShouldCache(A.Fake<IDispatchMessage>(), "value").ShouldBeTrue();
		policy.ShouldCache(A.Fake<IDispatchMessage>(), null).ShouldBeTrue();
	}

	[Fact]
	public void RegisterMiddleware_AsConcrete()
	{
		// Arrange
		var services = new ServiceCollection();
		services.AddSingleton(new DispatchJsonSerializer());

		// Act
		services.AddDispatchCaching();

		// S717 T.2: middleware registered as concrete type only
		// Assert -- CachingMiddleware and CacheInvalidationMiddleware registered as concrete types
		var descriptors = services.Where(sd =>
			sd.ServiceType == typeof(CachingMiddleware) ||
			sd.ServiceType == typeof(CacheInvalidationMiddleware)).ToList();
		descriptors.Count.ShouldBeGreaterThanOrEqualTo(2);
	}

	// ─── Test helpers ───

	private sealed class FakeDistributedCache : IDistributedCache
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
