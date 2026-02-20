// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.Metrics;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Caching;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Caching.Tests;

/// <summary>
/// Unit tests for <see cref="CacheInvalidationMiddleware"/> covering all code paths
/// including all three cache modes, ICacheInvalidator, InvalidateCacheAttribute, and default tags.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Feature", "Caching")]
public sealed class CacheInvalidationMiddlewareShould : IDisposable
{
	private static readonly string[] SingleTag = ["tag"];
	private static readonly string[] SingleMemTag = ["mem-tag"];
	private static readonly string[] SingleMemKey = ["mem-key"];
	private static readonly string[] SingleDistTag = ["dist-tag"];
	private static readonly string[] SingleDTag = ["d-tag"];
	private static readonly string[] SingleDDirectKey = ["d-direct-key"];

	private readonly IMeterFactory _meterFactory = new TestMeterFactory();
	private readonly IMemoryCache _fakeMemoryCache = A.Fake<IMemoryCache>();
	private readonly IDistributedCache _fakeDistributedCache = A.Fake<IDistributedCache>();
	private readonly HybridCache _fakeHybridCache = A.Fake<HybridCache>();
	private readonly ICacheTagTracker _fakeTagTracker = A.Fake<ICacheTagTracker>();

	public void Dispose()
	{
		if (_meterFactory is IDisposable d) d.Dispose();
	}

	[Fact]
	public void HaveCacheStage()
	{
		var middleware = CreateMiddleware();
		middleware.Stage.ShouldBe(DispatchMiddlewareStage.Cache);
	}

	[Fact]
	public async Task PassThrough_WhenCachingDisabled()
	{
		// Arrange
		var options = new CacheOptions { Enabled = false };
		var middleware = CreateMiddleware(options);
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		var expectedResult = A.Fake<IMessageResult>();

		// Act
		var result = await middleware.InvokeAsync(
			message, context, (_, _, _) => new ValueTask<IMessageResult>(expectedResult), CancellationToken.None);

		// Assert
		result.ShouldBe(expectedResult);
	}

	[Fact]
	public async Task PassThrough_WhenNoTagsOrKeys()
	{
		// Arrange — a plain message with no invalidation tags or keys
		var options = new CacheOptions { Enabled = true };
		var middleware = CreateMiddleware(options);
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		var expectedResult = A.Fake<IMessageResult>();

		// Act
		var result = await middleware.InvokeAsync(
			message, context, (_, _, _) => new ValueTask<IMessageResult>(expectedResult), CancellationToken.None);

		// Assert
		result.ShouldBe(expectedResult);
	}

	[Fact]
	public async Task InvalidateByTags_WhenICacheInvalidatorReturnsTagsAndHybridCacheAvailable()
	{
		// Arrange
		var options = new CacheOptions { Enabled = true, CacheMode = CacheMode.Hybrid };
		var middleware = CreateMiddleware(options, hybridCache: _fakeHybridCache);

		var message = A.Fake<ICacheInvalidator>();
		A.CallTo(() => message.GetCacheTagsToInvalidate()).Returns(["tag1", "tag2"]);
		A.CallTo(() => message.GetCacheKeysToInvalidate()).Returns(Enumerable.Empty<string>());

		var context = A.Fake<IMessageContext>();
		var expectedResult = A.Fake<IMessageResult>();

		// Act
		var result = await middleware.InvokeAsync(
			message, context, (_, _, _) => new ValueTask<IMessageResult>(expectedResult), CancellationToken.None);

		// Assert
		result.ShouldBe(expectedResult);
		A.CallTo(() => _fakeHybridCache.RemoveByTagAsync(
			A<IEnumerable<string>>.That.Contains("tag1"),
			A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task InvalidateByKeys_WhenICacheInvalidatorReturnsKeysAndHybridCacheAvailable()
	{
		// Arrange
		var options = new CacheOptions { Enabled = true, CacheMode = CacheMode.Hybrid };
		var middleware = CreateMiddleware(options, hybridCache: _fakeHybridCache);

		var message = A.Fake<ICacheInvalidator>();
		A.CallTo(() => message.GetCacheTagsToInvalidate()).Returns(Enumerable.Empty<string>());
		A.CallTo(() => message.GetCacheKeysToInvalidate()).Returns(["key1", "key2"]);

		var context = A.Fake<IMessageContext>();
		var expectedResult = A.Fake<IMessageResult>();

		// Act
		var result = await middleware.InvokeAsync(
			message, context, (_, _, _) => new ValueTask<IMessageResult>(expectedResult), CancellationToken.None);

		// Assert
		result.ShouldBe(expectedResult);
		A.CallTo(() => _fakeHybridCache.RemoveAsync(
			A<IEnumerable<string>>.That.Contains("key1"),
			A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task InvalidateByAttribute_WhenInvalidateCacheAttributePresent()
	{
		// Arrange
		var options = new CacheOptions { Enabled = true, CacheMode = CacheMode.Hybrid };
		var middleware = CreateMiddleware(options, hybridCache: _fakeHybridCache);

		var message = new TestInvalidateByAttributeMessage();
		var context = A.Fake<IMessageContext>();
		var expectedResult = A.Fake<IMessageResult>();

		// Act
		var result = await middleware.InvokeAsync(
			message, context, (_, _, _) => new ValueTask<IMessageResult>(expectedResult), CancellationToken.None);

		// Assert
		result.ShouldBe(expectedResult);
		A.CallTo(() => _fakeHybridCache.RemoveByTagAsync(
			A<IEnumerable<string>>.That.Contains("orders"),
			A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task IncludeDefaultTags_WhenConfigured()
	{
		// Arrange
		var options = new CacheOptions { Enabled = true, CacheMode = CacheMode.Hybrid, DefaultTags = ["default-tag"] };
		var middleware = CreateMiddleware(options, hybridCache: _fakeHybridCache);

		// Use a message that doesn't implement ICacheInvalidator but has no attribute either
		// DefaultTags alone make hasTags=true, triggering invalidation
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		var expectedResult = A.Fake<IMessageResult>();

		// Act
		var result = await middleware.InvokeAsync(
			message, context, (_, _, _) => new ValueTask<IMessageResult>(expectedResult), CancellationToken.None);

		// Assert
		result.ShouldBe(expectedResult);
		A.CallTo(() => _fakeHybridCache.RemoveByTagAsync(
			A<IEnumerable<string>>.That.Contains("default-tag"),
			A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task InvalidateMemoryCache_ViaHybridCache_WhenMemoryMode()
	{
		// Arrange
		var options = new CacheOptions { Enabled = true, CacheMode = CacheMode.Memory };
		var middleware = CreateMiddleware(options, hybridCache: _fakeHybridCache);

		var message = A.Fake<ICacheInvalidator>();
		A.CallTo(() => message.GetCacheTagsToInvalidate()).Returns(SingleMemTag);
		A.CallTo(() => message.GetCacheKeysToInvalidate()).Returns(SingleMemKey);

		var context = A.Fake<IMessageContext>();
		var expectedResult = A.Fake<IMessageResult>();

		// Act
		await middleware.InvokeAsync(
			message, context, (_, _, _) => new ValueTask<IMessageResult>(expectedResult), CancellationToken.None);

		// Assert — Memory mode uses HybridCache when available
		A.CallTo(() => _fakeHybridCache.RemoveByTagAsync(
			A<IEnumerable<string>>.That.Contains("mem-tag"),
			A<CancellationToken>._)).MustHaveHappenedOnceExactly();
		A.CallTo(() => _fakeHybridCache.RemoveAsync(
			A<IEnumerable<string>>.That.Contains("mem-key"),
			A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task InvalidateMemoryCache_ViaMemoryCacheAndTagTracker_WhenNoHybridCache()
	{
		// Arrange
		var options = new CacheOptions { Enabled = true, CacheMode = CacheMode.Memory };
		var middleware = CreateMiddleware(options, memoryCache: _fakeMemoryCache, tagTracker: _fakeTagTracker);

		A.CallTo(() => _fakeTagTracker.GetKeysByTagsAsync(A<string[]>._, A<CancellationToken>._))
			.Returns(new HashSet<string>(["cached-key-1", "cached-key-2"]));

		var message = A.Fake<ICacheInvalidator>();
		A.CallTo(() => message.GetCacheTagsToInvalidate()).Returns(["tag-a"]);
		A.CallTo(() => message.GetCacheKeysToInvalidate()).Returns(["direct-key"]);

		var context = A.Fake<IMessageContext>();
		var expectedResult = A.Fake<IMessageResult>();

		// Act
		await middleware.InvokeAsync(
			message, context, (_, _, _) => new ValueTask<IMessageResult>(expectedResult), CancellationToken.None);

		// Assert — tag tracker resolves keys, then memory cache removes them
		A.CallTo(() => _fakeTagTracker.GetKeysByTagsAsync(A<string[]>._, A<CancellationToken>._)).MustHaveHappened();
		A.CallTo(() => _fakeMemoryCache.Remove("cached-key-1")).MustHaveHappened();
		A.CallTo(() => _fakeMemoryCache.Remove("cached-key-2")).MustHaveHappened();
		A.CallTo(() => _fakeMemoryCache.Remove("direct-key")).MustHaveHappened();
	}

	[Fact]
	public async Task InvalidateDistributedCache_ViaHybridCache()
	{
		// Arrange
		var options = new CacheOptions { Enabled = true, CacheMode = CacheMode.Distributed };
		var middleware = CreateMiddleware(options, hybridCache: _fakeHybridCache);

		var message = A.Fake<ICacheInvalidator>();
		A.CallTo(() => message.GetCacheTagsToInvalidate()).Returns(SingleDistTag);
		A.CallTo(() => message.GetCacheKeysToInvalidate()).Returns(Enumerable.Empty<string>());

		var context = A.Fake<IMessageContext>();
		var expectedResult = A.Fake<IMessageResult>();

		// Act
		await middleware.InvokeAsync(
			message, context, (_, _, _) => new ValueTask<IMessageResult>(expectedResult), CancellationToken.None);

		// Assert — Distributed mode also uses HybridCache
		A.CallTo(() => _fakeHybridCache.RemoveByTagAsync(
			A<IEnumerable<string>>.That.Contains("dist-tag"),
			A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task InvalidateDistributedCache_ViaDistributedCacheDirectly_WhenNoHybridCache()
	{
		// Arrange
		var options = new CacheOptions { Enabled = true, CacheMode = CacheMode.Distributed };
		var middleware = CreateMiddleware(options, distributedCache: _fakeDistributedCache, tagTracker: _fakeTagTracker);

		A.CallTo(() => _fakeTagTracker.GetKeysByTagsAsync(A<string[]>._, A<CancellationToken>._))
			.Returns(new HashSet<string>(["dist-key-1"]));

		var message = A.Fake<ICacheInvalidator>();
		A.CallTo(() => message.GetCacheTagsToInvalidate()).Returns(SingleDTag);
		A.CallTo(() => message.GetCacheKeysToInvalidate()).Returns(SingleDDirectKey);

		var context = A.Fake<IMessageContext>();
		var expectedResult = A.Fake<IMessageResult>();

		// Act
		await middleware.InvokeAsync(
			message, context, (_, _, _) => new ValueTask<IMessageResult>(expectedResult), CancellationToken.None);

		// Assert
		A.CallTo(() => _fakeDistributedCache.RemoveAsync("dist-key-1", A<CancellationToken>._)).MustHaveHappened();
		A.CallTo(() => _fakeDistributedCache.RemoveAsync("d-direct-key", A<CancellationToken>._)).MustHaveHappened();
	}

	[Fact]
	public async Task ThrowOnNullMessage()
	{
		var middleware = CreateMiddleware();
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await middleware.InvokeAsync(null!, A.Fake<IMessageContext>(),
				(_, _, _) => new ValueTask<IMessageResult>(A.Fake<IMessageResult>()), CancellationToken.None));
	}

	[Fact]
	public async Task ThrowOnNullContext()
	{
		var middleware = CreateMiddleware();
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await middleware.InvokeAsync(A.Fake<IDispatchMessage>(), null!,
				(_, _, _) => new ValueTask<IMessageResult>(A.Fake<IMessageResult>()), CancellationToken.None));
	}

	[Fact]
	public async Task ThrowOnNullNextDelegate()
	{
		var middleware = CreateMiddleware();
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await middleware.InvokeAsync(A.Fake<IDispatchMessage>(), A.Fake<IMessageContext>(),
				null!, CancellationToken.None));
	}

	[Fact]
	public async Task NoOp_WhenMemoryMode_NoHybridCache_NoMemoryCache()
	{
		// Arrange — memory mode with no HybridCache and no IMemoryCache
		var options = new CacheOptions { Enabled = true, CacheMode = CacheMode.Memory };
		var middleware = CreateMiddleware(options);

		var message = A.Fake<ICacheInvalidator>();
		A.CallTo(() => message.GetCacheTagsToInvalidate()).Returns(SingleTag);
		A.CallTo(() => message.GetCacheKeysToInvalidate()).Returns(Enumerable.Empty<string>());

		var context = A.Fake<IMessageContext>();
		var expectedResult = A.Fake<IMessageResult>();

		// Act — should not throw
		var result = await middleware.InvokeAsync(
			message, context, (_, _, _) => new ValueTask<IMessageResult>(expectedResult), CancellationToken.None);

		// Assert
		result.ShouldBe(expectedResult);
	}

	[Fact]
	public async Task NoOp_WhenDistributedMode_NoHybridCache_NoDistributedCache()
	{
		// Arrange
		var options = new CacheOptions { Enabled = true, CacheMode = CacheMode.Distributed };
		var middleware = CreateMiddleware(options);

		var message = A.Fake<ICacheInvalidator>();
		A.CallTo(() => message.GetCacheTagsToInvalidate()).Returns(SingleTag);
		A.CallTo(() => message.GetCacheKeysToInvalidate()).Returns(Enumerable.Empty<string>());

		var context = A.Fake<IMessageContext>();
		var expectedResult = A.Fake<IMessageResult>();

		// Act — should not throw
		var result = await middleware.InvokeAsync(
			message, context, (_, _, _) => new ValueTask<IMessageResult>(expectedResult), CancellationToken.None);

		// Assert
		result.ShouldBe(expectedResult);
	}

	[Fact]
	public async Task NoOp_WhenHybridMode_NoHybridCache()
	{
		// Arrange
		var options = new CacheOptions { Enabled = true, CacheMode = CacheMode.Hybrid };
		var middleware = CreateMiddleware(options);

		var message = A.Fake<ICacheInvalidator>();
		A.CallTo(() => message.GetCacheTagsToInvalidate()).Returns(SingleTag);
		A.CallTo(() => message.GetCacheKeysToInvalidate()).Returns(Enumerable.Empty<string>());

		var context = A.Fake<IMessageContext>();
		var expectedResult = A.Fake<IMessageResult>();

		// Act — should not throw (exits early)
		var result = await middleware.InvokeAsync(
			message, context, (_, _, _) => new ValueTask<IMessageResult>(expectedResult), CancellationToken.None);

		// Assert
		result.ShouldBe(expectedResult);
	}

	[Fact]
	public async Task CombineInvalidatorTagsAndAttributeTags()
	{
		// Arrange
		var options = new CacheOptions { Enabled = true, CacheMode = CacheMode.Hybrid, DefaultTags = ["default"] };
		var middleware = CreateMiddleware(options, hybridCache: _fakeHybridCache);

		var message = new TestInvalidatorWithAttribute();
		var context = A.Fake<IMessageContext>();
		var expectedResult = A.Fake<IMessageResult>();

		// Act
		await middleware.InvokeAsync(
			message, context, (_, _, _) => new ValueTask<IMessageResult>(expectedResult), CancellationToken.None);

		// Assert — tags from ICacheInvalidator + [InvalidateCache] + default tags should all be combined
		A.CallTo(() => _fakeHybridCache.RemoveByTagAsync(
			A<IEnumerable<string>>._,
			A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	private CacheInvalidationMiddleware CreateMiddleware(
		CacheOptions? options = null,
		ICacheTagTracker? tagTracker = null,
		IMemoryCache? memoryCache = null,
		IDistributedCache? distributedCache = null,
		HybridCache? hybridCache = null)
	{
		return new CacheInvalidationMiddleware(
			_meterFactory,
			MsOptions.Create(options ?? new CacheOptions { Enabled = true }),
			tagTracker,
			memoryCache,
			distributedCache,
			hybridCache);
	}

	// Test helpers

	[InvalidateCache(Tags = ["orders", "products"])]
	private sealed class TestInvalidateByAttributeMessage : IDispatchMessage;

	[InvalidateCache(Tags = ["attr-tag"])]
	private sealed class TestInvalidatorWithAttribute : ICacheInvalidator
	{
		public IEnumerable<string> GetCacheTagsToInvalidate() => ["invalidator-tag"];
		public IEnumerable<string> GetCacheKeysToInvalidate() => [];
	}

	private sealed class TestMeterFactory : IMeterFactory
	{
		private readonly List<Meter> _meters = [];

		public Meter Create(MeterOptions options)
		{
			var meter = new Meter(options);
			_meters.Add(meter);
			return meter;
		}

		public void Dispose()
		{
			foreach (var meter in _meters) meter.Dispose();
		}
	}
}
