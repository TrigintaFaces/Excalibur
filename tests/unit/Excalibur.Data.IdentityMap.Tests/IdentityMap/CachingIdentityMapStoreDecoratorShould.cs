// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.IdentityMap.Diagnostics;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;

namespace Excalibur.Data.IdentityMap.Tests.IdentityMap;

[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Data.IdentityMap")]
public sealed class CachingIdentityMapStoreDecoratorShould : IDisposable
{
	private readonly IIdentityMapStore _inner;
	private readonly MemoryDistributedCache _cache;
	private readonly CachingIdentityMapStoreDecorator _sut;

	public CachingIdentityMapStoreDecoratorShould()
	{
		// Set up a real in-memory store as inner
		var services = new ServiceCollection();
		services.AddInMemoryIdentityMap();
		_inner = services.BuildServiceProvider().GetRequiredService<IIdentityMapStore>();

		// Use MemoryDistributedCache (real impl, in-process)
		_cache = new MemoryDistributedCache(
			Options.Create(new MemoryDistributedCacheOptions()));

		var cacheOptions = new IdentityMapCacheOptions
		{
			AbsoluteExpiration = TimeSpan.FromMinutes(30),
			SlidingExpiration = TimeSpan.FromMinutes(5),
		};

		_sut = new CachingIdentityMapStoreDecorator(_inner, _cache, cacheOptions);
	}

	public void Dispose()
	{
		// MemoryDistributedCache doesn't implement IDisposable
	}

	#region ResolveAsync Caching

	[Fact]
	public async Task ResolveAsync_CacheResultAfterFirstLookup()
	{
		// Arrange - bind via decorator
		await _sut.BindAsync("SAP", "EXT-001", "Order", "AGG-001", CancellationToken.None);

		// Act - resolve (should hit cache on second call)
		var result1 = await _sut.ResolveAsync("SAP", "EXT-001", "Order", CancellationToken.None);
		var result2 = await _sut.ResolveAsync("SAP", "EXT-001", "Order", CancellationToken.None);

		// Assert
		result1.ShouldBe("AGG-001");
		result2.ShouldBe("AGG-001");
	}

	[Fact]
	public async Task ResolveAsync_ReturnNull_WhenNotMapped()
	{
		var result = await _sut.ResolveAsync("SAP", "NONE", "Order", CancellationToken.None);

		result.ShouldBeNull();
	}

	#endregion

	#region BindAsync Caching

	[Fact]
	public async Task BindAsync_UpdateCache()
	{
		await _sut.BindAsync("SAP", "EXT-001", "Order", "AGG-001", CancellationToken.None);

		// Verify cache has the value
		var cached = await _cache.GetStringAsync("idmap:SAP:Order:EXT-001");
		cached.ShouldBe("AGG-001");
	}

	[Fact]
	public async Task BindAsync_OverwriteCache_OnUpdate()
	{
		await _sut.BindAsync("SAP", "EXT-001", "Order", "AGG-001", CancellationToken.None);
		await _sut.BindAsync("SAP", "EXT-001", "Order", "AGG-002", CancellationToken.None);

		var cached = await _cache.GetStringAsync("idmap:SAP:Order:EXT-001");
		cached.ShouldBe("AGG-002");
	}

	#endregion

	#region UnbindAsync Cache Invalidation

	[Fact]
	public async Task UnbindAsync_RemoveFromCache()
	{
		await _sut.BindAsync("SAP", "EXT-001", "Order", "AGG-001", CancellationToken.None);

		var removed = await _sut.UnbindAsync("SAP", "EXT-001", "Order", CancellationToken.None);

		removed.ShouldBeTrue();
		var cached = await _cache.GetStringAsync("idmap:SAP:Order:EXT-001");
		cached.ShouldBeNull();
	}

	[Fact]
	public async Task UnbindAsync_NotRemoveCache_WhenNothingToRemove()
	{
		var removed = await _sut.UnbindAsync("SAP", "NONE", "Order", CancellationToken.None);

		removed.ShouldBeFalse();
	}

	#endregion

	#region TryBindAsync Caching

	[Fact]
	public async Task TryBindAsync_CacheNewBinding()
	{
		var result = await _sut.TryBindAsync("SAP", "EXT-001", "Order", "AGG-001", CancellationToken.None);

		result.WasCreated.ShouldBeTrue();
		var cached = await _cache.GetStringAsync("idmap:SAP:Order:EXT-001");
		cached.ShouldBe("AGG-001");
	}

	[Fact]
	public async Task TryBindAsync_CacheExistingBinding()
	{
		await _sut.BindAsync("SAP", "EXT-001", "Order", "AGG-001", CancellationToken.None);

		var result = await _sut.TryBindAsync("SAP", "EXT-001", "Order", "AGG-NEW", CancellationToken.None);

		result.WasCreated.ShouldBeFalse();
		result.AggregateId.ShouldBe("AGG-001");
	}

	#endregion

	#region ResolveBatchAsync Caching

	[Fact]
	public async Task ResolveBatchAsync_UseCacheForHits()
	{
		// Bind two, one via cache
		await _sut.BindAsync("SAP", "EXT-001", "Order", "AGG-001", CancellationToken.None);
		await _sut.BindAsync("SAP", "EXT-002", "Order", "AGG-002", CancellationToken.None);

		var result = await _sut.ResolveBatchAsync(
			"SAP", ["EXT-001", "EXT-002", "EXT-003"], "Order", CancellationToken.None);

		result.Count.ShouldBe(2);
		result["EXT-001"].ShouldBe("AGG-001");
		result["EXT-002"].ShouldBe("AGG-002");
	}

	#endregion

	#region Constructor Validation

	[Fact]
	public void ThrowOnNullInner()
	{
		Should.Throw<ArgumentNullException>(() =>
			new CachingIdentityMapStoreDecorator(null!, _cache, new IdentityMapCacheOptions()));
	}

	[Fact]
	public void ThrowOnNullCache()
	{
		Should.Throw<ArgumentNullException>(() =>
			new CachingIdentityMapStoreDecorator(_inner, null!, new IdentityMapCacheOptions()));
	}

	[Fact]
	public void ThrowOnNullOptions()
	{
		Should.Throw<ArgumentNullException>(() =>
			new CachingIdentityMapStoreDecorator(_inner, _cache, null!));
	}

	#endregion

	#region IdentityMapCacheOptions

	[Fact]
	public void CacheOptions_HaveCorrectDefaults()
	{
		var options = new IdentityMapCacheOptions();

		options.AbsoluteExpiration.ShouldBe(TimeSpan.FromHours(1));
		options.SlidingExpiration.ShouldBe(TimeSpan.FromMinutes(10));
	}

	#endregion
}
