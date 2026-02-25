// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA2213 // Disposable fields should be disposed -- TestMeterFactory is test-scoped

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Caching;

using FakeItEasy;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

using Tests.Shared;

namespace Excalibur.Dispatch.Middleware.Tests.Caching;

[Trait("Category", "Unit")]
public sealed class CacheInvalidationMiddlewareShould : UnitTestBase
{
	private readonly TestMeterFactory _meterFactory;
	private readonly IOptions<CacheOptions> _options;
	private readonly IMessageContext _context;
	private readonly CancellationToken _ct = CancellationToken.None;
	private readonly IMessageResult _successResult;

	public CacheInvalidationMiddlewareShould()
	{
		_meterFactory = new TestMeterFactory();
		_options = Microsoft.Extensions.Options.Options.Create(new CacheOptions { Enabled = true, CacheMode = CacheMode.Hybrid });
		_context = A.Fake<IMessageContext>();
		_successResult = A.Fake<IMessageResult>();
		A.CallTo(() => _successResult.Succeeded).Returns(true);
	}

	[Fact]
	public async Task InvokeAsync_WhenDisabled_SkipsInvalidation()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new CacheOptions { Enabled = false });
		var middleware = new CacheInvalidationMiddleware(_meterFactory, options);
		var message = A.Fake<IDispatchMessage>();
		var called = false;

		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct)
		{
			called = true;
			return new ValueTask<IMessageResult>(_successResult);
		}

		// Act
		var result = await middleware.InvokeAsync(message, _context, Next, _ct);

		// Assert
		called.ShouldBeTrue();
		result.ShouldBe(_successResult);
	}

	[Fact]
	public async Task InvokeAsync_WhenMessageIsNotInvalidator_AndNoAttribute_SkipsInvalidation()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new CacheOptions { Enabled = true, CacheMode = CacheMode.Hybrid });
		var middleware = new CacheInvalidationMiddleware(_meterFactory, options);
		var message = A.Fake<IDispatchMessage>();

		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct) =>
			new(_successResult);

		// Act
		var result = await middleware.InvokeAsync(message, _context, Next, _ct);

		// Assert
		result.ShouldBe(_successResult);
	}

	[Fact]
	public async Task InvokeAsync_WhenMessageImplementsICacheInvalidator_InvalidatesByTags()
	{
		// Arrange
		var hybridCache = A.Fake<HybridCache>();
		var options = Microsoft.Extensions.Options.Options.Create(new CacheOptions { Enabled = true, CacheMode = CacheMode.Hybrid });
		var middleware = new CacheInvalidationMiddleware(_meterFactory, options, hybridCache: hybridCache);

		var message = A.Fake<TestCacheInvalidatorMessage>();
		A.CallTo(() => ((ICacheInvalidator)message).GetCacheTagsToInvalidate()).Returns(["tag1", "tag2"]);
		A.CallTo(() => ((ICacheInvalidator)message).GetCacheKeysToInvalidate()).Returns([]);

		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct) =>
			new(_successResult);

		// Act
		await middleware.InvokeAsync(message, _context, Next, _ct);

		// Assert
		A.CallTo(() => hybridCache.RemoveByTagAsync(
			A<IEnumerable<string>>.That.Contains("tag1"),
			_ct)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task InvokeAsync_WhenMessageImplementsICacheInvalidator_InvalidatesByKeys()
	{
		// Arrange
		var hybridCache = A.Fake<HybridCache>();
		var options = Microsoft.Extensions.Options.Options.Create(new CacheOptions { Enabled = true, CacheMode = CacheMode.Hybrid });
		var middleware = new CacheInvalidationMiddleware(_meterFactory, options, hybridCache: hybridCache);

		var message = A.Fake<TestCacheInvalidatorMessage>();
		A.CallTo(() => ((ICacheInvalidator)message).GetCacheTagsToInvalidate()).Returns([]);
		A.CallTo(() => ((ICacheInvalidator)message).GetCacheKeysToInvalidate()).Returns(["key1", "key2"]);

		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct) =>
			new(_successResult);

		// Act
		await middleware.InvokeAsync(message, _context, Next, _ct);

		// Assert
		A.CallTo(() => hybridCache.RemoveAsync(
			A<IEnumerable<string>>.That.Contains("key1"),
			_ct)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task InvokeAsync_WithDefaultTags_InvalidatesDefaultTags()
	{
		// Arrange
		var hybridCache = A.Fake<HybridCache>();
		var options = Microsoft.Extensions.Options.Options.Create(new CacheOptions
		{
			Enabled = true,
			CacheMode = CacheMode.Hybrid,
			DefaultTags = ["default-tag"]
		});
		var middleware = new CacheInvalidationMiddleware(_meterFactory, options, hybridCache: hybridCache);
		var message = A.Fake<IDispatchMessage>();

		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct) =>
			new(_successResult);

		// Act
		await middleware.InvokeAsync(message, _context, Next, _ct);

		// Assert
		A.CallTo(() => hybridCache.RemoveByTagAsync(
			A<IEnumerable<string>>.That.Contains("default-tag"),
			_ct)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task InvokeAsync_MemoryMode_InvalidatesViaHybridCache()
	{
		// Arrange
		var hybridCache = A.Fake<HybridCache>();
		var options = Microsoft.Extensions.Options.Options.Create(new CacheOptions
		{
			Enabled = true,
			CacheMode = CacheMode.Memory,
			DefaultTags = ["mem-tag"]
		});
		var middleware = new CacheInvalidationMiddleware(_meterFactory, options, hybridCache: hybridCache);
		var message = A.Fake<IDispatchMessage>();

		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct) =>
			new(_successResult);

		// Act
		await middleware.InvokeAsync(message, _context, Next, _ct);

		// Assert
		A.CallTo(() => hybridCache.RemoveByTagAsync(
			A<IEnumerable<string>>.That.Contains("mem-tag"),
			_ct)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task InvokeAsync_DistributedMode_InvalidatesViaHybridCache()
	{
		// Arrange
		var hybridCache = A.Fake<HybridCache>();
		var options = Microsoft.Extensions.Options.Options.Create(new CacheOptions
		{
			Enabled = true,
			CacheMode = CacheMode.Distributed,
			DefaultTags = ["dist-tag"]
		});
		var middleware = new CacheInvalidationMiddleware(_meterFactory, options, hybridCache: hybridCache);
		var message = A.Fake<IDispatchMessage>();

		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct) =>
			new(_successResult);

		// Act
		await middleware.InvokeAsync(message, _context, Next, _ct);

		// Assert
		A.CallTo(() => hybridCache.RemoveByTagAsync(
			A<IEnumerable<string>>.That.Contains("dist-tag"),
			_ct)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task InvokeAsync_MemoryMode_FallsBackToMemoryCache_WhenHybridCacheIsNull()
	{
		// Arrange
		var memoryCache = A.Fake<IMemoryCache>();
		var tagTracker = A.Fake<ICacheTagTracker>();
		A.CallTo(() => tagTracker.GetKeysByTagsAsync(A<string[]>._, _ct))
			.Returns(new HashSet<string> { "cached-key-1" });

		var options = Microsoft.Extensions.Options.Options.Create(new CacheOptions
		{
			Enabled = true,
			CacheMode = CacheMode.Memory,
			DefaultTags = ["fallback-tag"]
		});
		var middleware = new CacheInvalidationMiddleware(_meterFactory, options, tagTracker: tagTracker, memoryCache: memoryCache);
		var message = A.Fake<IDispatchMessage>();

		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct) =>
			new(_successResult);

		// Act
		await middleware.InvokeAsync(message, _context, Next, _ct);

		// Assert
		A.CallTo(() => memoryCache.Remove("cached-key-1")).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task InvokeAsync_DistributedMode_FallsBackToDistributedCache_WhenHybridCacheIsNull()
	{
		// Arrange
		var distributedCache = A.Fake<IDistributedCache>();
		var tagTracker = A.Fake<ICacheTagTracker>();
		A.CallTo(() => tagTracker.GetKeysByTagsAsync(A<string[]>._, _ct))
			.Returns(new HashSet<string> { "cached-key-1" });

		var options = Microsoft.Extensions.Options.Options.Create(new CacheOptions
		{
			Enabled = true,
			CacheMode = CacheMode.Distributed,
			DefaultTags = ["fallback-tag"]
		});
		var middleware = new CacheInvalidationMiddleware(_meterFactory, options, tagTracker: tagTracker, distributedCache: distributedCache);
		var message = A.Fake<IDispatchMessage>();

		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct) =>
			new(_successResult);

		// Act
		await middleware.InvokeAsync(message, _context, Next, _ct);

		// Assert
		A.CallTo(() => distributedCache.RemoveAsync("cached-key-1", _ct)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task InvokeAsync_MemoryMode_FallbackWithKeys_RemovesFromMemoryCache()
	{
		// Arrange
		var memoryCache = A.Fake<IMemoryCache>();
		var options = Microsoft.Extensions.Options.Options.Create(new CacheOptions
		{
			Enabled = true,
			CacheMode = CacheMode.Memory,
		});
		var middleware = new CacheInvalidationMiddleware(_meterFactory, options, memoryCache: memoryCache);

		var message = A.Fake<TestCacheInvalidatorMessage>();
		A.CallTo(() => ((ICacheInvalidator)message).GetCacheTagsToInvalidate()).Returns([]);
		A.CallTo(() => ((ICacheInvalidator)message).GetCacheKeysToInvalidate()).Returns(["key1"]);

		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct) =>
			new(_successResult);

		// Act
		await middleware.InvokeAsync(message, _context, Next, _ct);

		// Assert
		A.CallTo(() => memoryCache.Remove("key1")).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task InvokeAsync_DistributedMode_FallbackWithKeys_RemovesFromDistributedCache()
	{
		// Arrange
		var distributedCache = A.Fake<IDistributedCache>();
		var options = Microsoft.Extensions.Options.Options.Create(new CacheOptions
		{
			Enabled = true,
			CacheMode = CacheMode.Distributed,
		});
		var middleware = new CacheInvalidationMiddleware(_meterFactory, options, distributedCache: distributedCache);

		var message = A.Fake<TestCacheInvalidatorMessage>();
		A.CallTo(() => ((ICacheInvalidator)message).GetCacheTagsToInvalidate()).Returns([]);
		A.CallTo(() => ((ICacheInvalidator)message).GetCacheKeysToInvalidate()).Returns(["key1"]);

		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct) =>
			new(_successResult);

		// Act
		await middleware.InvokeAsync(message, _context, Next, _ct);

		// Assert
		A.CallTo(() => distributedCache.RemoveAsync("key1", _ct)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public void Stage_ReturnsCache()
	{
		// Arrange
		var middleware = new CacheInvalidationMiddleware(_meterFactory, _options);

		// Assert
		middleware.Stage.ShouldBe(DispatchMiddlewareStage.Cache);
	}

	[Fact]
	public async Task InvokeAsync_ThrowsOnNullMessage()
	{
		// Arrange
		var middleware = new CacheInvalidationMiddleware(_meterFactory, _options);

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await middleware.InvokeAsync(null!, _context, (_, _, _) => new ValueTask<IMessageResult>(_successResult), _ct));
	}

	[Fact]
	public async Task InvokeAsync_ThrowsOnNullContext()
	{
		// Arrange
		var middleware = new CacheInvalidationMiddleware(_meterFactory, _options);
		var message = A.Fake<IDispatchMessage>();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await middleware.InvokeAsync(message, null!, (_, _, _) => new ValueTask<IMessageResult>(_successResult), _ct));
	}

	[Fact]
	public async Task InvokeAsync_ThrowsOnNullDelegate()
	{
		// Arrange
		var middleware = new CacheInvalidationMiddleware(_meterFactory, _options);
		var message = A.Fake<IDispatchMessage>();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await middleware.InvokeAsync(message, _context, null!, _ct));
	}

	[Fact]
	public async Task InvokeAsync_UnsupportedCacheMode_ThrowsInvalidOperationException()
	{
		// Arrange
		var options = Microsoft.Extensions.Options.Options.Create(new CacheOptions
		{
			Enabled = true,
			CacheMode = (CacheMode)99, // Invalid mode
			DefaultTags = ["tag"]
		});
		var middleware = new CacheInvalidationMiddleware(_meterFactory, options);
		var message = A.Fake<IDispatchMessage>();

		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct) =>
			new(_successResult);

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(async () =>
			await middleware.InvokeAsync(message, _context, Next, _ct));
	}

	[Fact]
	public async Task InvokeAsync_WithInvalidateCacheAttribute_InvalidatesByAttributeTags()
	{
		// Arrange
		var hybridCache = A.Fake<HybridCache>();
		var options = Microsoft.Extensions.Options.Options.Create(new CacheOptions
		{
			Enabled = true,
			CacheMode = CacheMode.Hybrid
		});
		var middleware = new CacheInvalidationMiddleware(_meterFactory, options, hybridCache: hybridCache);
		var message = new TestInvalidateCacheMessage();

		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct) =>
			new(_successResult);

		// Act
		await middleware.InvokeAsync(message, _context, Next, _ct);

		// Assert — tags from [InvalidateCache] attribute should be invalidated
		A.CallTo(() => hybridCache.RemoveByTagAsync(
			A<IEnumerable<string>>.That.Contains("attr-invalidate-tag"),
			_ct)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task InvokeAsync_MemoryMode_WithNullMemoryCache_ReturnsGracefully()
	{
		// Arrange — memory mode with no hybridCache and no memoryCache
		var options = Microsoft.Extensions.Options.Options.Create(new CacheOptions
		{
			Enabled = true,
			CacheMode = CacheMode.Memory,
			DefaultTags = ["tag"]
		});
		var middleware = new CacheInvalidationMiddleware(_meterFactory, options);
		var message = A.Fake<IDispatchMessage>();

		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct) =>
			new(_successResult);

		// Act — should not throw, just return gracefully
		var result = await middleware.InvokeAsync(message, _context, Next, _ct);

		// Assert
		result.ShouldBe(_successResult);
	}

	[Fact]
	public async Task InvokeAsync_DistributedMode_WithNullDistributedCache_ReturnsGracefully()
	{
		// Arrange — distributed mode with no hybridCache and no distributedCache
		var options = Microsoft.Extensions.Options.Options.Create(new CacheOptions
		{
			Enabled = true,
			CacheMode = CacheMode.Distributed,
			DefaultTags = ["tag"]
		});
		var middleware = new CacheInvalidationMiddleware(_meterFactory, options);
		var message = A.Fake<IDispatchMessage>();

		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct) =>
			new(_successResult);

		// Act — should not throw
		var result = await middleware.InvokeAsync(message, _context, Next, _ct);

		// Assert
		result.ShouldBe(_successResult);
	}

	[Fact]
	public async Task InvokeAsync_HybridMode_WithNullHybridCache_ReturnsGracefully()
	{
		// Arrange — hybrid mode with no hybridCache
		var options = Microsoft.Extensions.Options.Options.Create(new CacheOptions
		{
			Enabled = true,
			CacheMode = CacheMode.Hybrid,
			DefaultTags = ["tag"]
		});
		var middleware = new CacheInvalidationMiddleware(_meterFactory, options);
		var message = A.Fake<IDispatchMessage>();

		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct) =>
			new(_successResult);

		// Act — should not throw
		var result = await middleware.InvokeAsync(message, _context, Next, _ct);

		// Assert
		result.ShouldBe(_successResult);
	}

	[Fact]
	public async Task InvokeAsync_MemoryMode_WithTagsButNoTagTracker_SkipsTagInvalidation()
	{
		// Arrange — memory mode fallback with memoryCache but no tag tracker
		var memoryCache = A.Fake<IMemoryCache>();
		var options = Microsoft.Extensions.Options.Options.Create(new CacheOptions
		{
			Enabled = true,
			CacheMode = CacheMode.Memory,
			DefaultTags = ["some-tag"]
		});
		var middleware = new CacheInvalidationMiddleware(_meterFactory, options, memoryCache: memoryCache);
		var message = A.Fake<IDispatchMessage>();

		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct) =>
			new(_successResult);

		// Act — tags present but no tag tracker, should not call memoryCache.Remove
		var result = await middleware.InvokeAsync(message, _context, Next, _ct);

		// Assert
		result.ShouldBe(_successResult);
		A.CallTo(() => memoryCache.Remove(A<object>._)).MustNotHaveHappened();
	}

	[Fact]
	public async Task InvokeAsync_DistributedMode_WithTagsButNoTagTracker_SkipsTagInvalidation()
	{
		// Arrange — distributed mode fallback with distributedCache but no tag tracker
		var distributedCache = A.Fake<IDistributedCache>();
		var options = Microsoft.Extensions.Options.Options.Create(new CacheOptions
		{
			Enabled = true,
			CacheMode = CacheMode.Distributed,
			DefaultTags = ["some-tag"]
		});
		var middleware = new CacheInvalidationMiddleware(_meterFactory, options, distributedCache: distributedCache);
		var message = A.Fake<IDispatchMessage>();

		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct) =>
			new(_successResult);

		// Act — tags present but no tag tracker
		var result = await middleware.InvokeAsync(message, _context, Next, _ct);

		// Assert
		result.ShouldBe(_successResult);
		A.CallTo(() => distributedCache.RemoveAsync(A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
	}

	[Fact]
	public async Task InvokeAsync_WithCombinedInvalidatorAndAttribute_MergesAllTags()
	{
		// Arrange — message implements ICacheInvalidator AND has [InvalidateCache] attribute
		var hybridCache = A.Fake<HybridCache>();
		var options = Microsoft.Extensions.Options.Options.Create(new CacheOptions
		{
			Enabled = true,
			CacheMode = CacheMode.Hybrid,
			DefaultTags = ["default-tag"]
		});
		var middleware = new CacheInvalidationMiddleware(_meterFactory, options, hybridCache: hybridCache);

		var message = A.Fake<TestCacheInvalidatorWithAttributeMessage>();
		A.CallTo(() => ((ICacheInvalidator)message).GetCacheTagsToInvalidate()).Returns(["invalidator-tag"]);
		A.CallTo(() => ((ICacheInvalidator)message).GetCacheKeysToInvalidate()).Returns([]);

		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct) =>
			new(_successResult);

		// Act
		await middleware.InvokeAsync(message, _context, Next, _ct);

		// Assert — should have all three tag sources merged
		A.CallTo(() => hybridCache.RemoveByTagAsync(
			A<IEnumerable<string>>.That.Matches(tags =>
				tags.Contains("invalidator-tag") &&
				tags.Contains("combined-attr-tag") &&
				tags.Contains("default-tag")),
			_ct)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task InvokeAsync_WithMemoryModeAndHybridCache_InvalidatesKeysByRemoveAsync()
	{
		// Arrange — memory mode with hybridCache and keys to invalidate
		var hybridCache = A.Fake<HybridCache>();
		var options = Microsoft.Extensions.Options.Options.Create(new CacheOptions
		{
			Enabled = true,
			CacheMode = CacheMode.Memory
		});
		var middleware = new CacheInvalidationMiddleware(_meterFactory, options, hybridCache: hybridCache);

		var message = A.Fake<TestCacheInvalidatorMessage>();
		A.CallTo(() => ((ICacheInvalidator)message).GetCacheTagsToInvalidate()).Returns([]);
		A.CallTo(() => ((ICacheInvalidator)message).GetCacheKeysToInvalidate()).Returns(["key-a", "key-b"]);

		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct) =>
			new(_successResult);

		// Act
		await middleware.InvokeAsync(message, _context, Next, _ct);

		// Assert — keys should be invalidated via HybridCache.RemoveAsync
		A.CallTo(() => hybridCache.RemoveAsync(
			A<IEnumerable<string>>.That.Contains("key-a"),
			_ct)).MustHaveHappenedOnceExactly();
	}

}

// Test helper: message with [InvalidateCache] attribute
[InvalidateCache(Tags = ["attr-invalidate-tag"])]
#pragma warning disable CA1034
public sealed class TestInvalidateCacheMessage : IDispatchMessage
{
}

// Test helper: message that implements ICacheInvalidator AND has [InvalidateCache] attribute
[InvalidateCache(Tags = ["combined-attr-tag"])]
public abstract class TestCacheInvalidatorWithAttributeMessage : IDispatchMessage, ICacheInvalidator
{
	public abstract IEnumerable<string> GetCacheTagsToInvalidate();
	public abstract IEnumerable<string> GetCacheKeysToInvalidate();
}
#pragma warning restore CA1034

// Test helper: a message that implements both IDispatchMessage and ICacheInvalidator
#pragma warning disable CA1034
public abstract class TestCacheInvalidatorMessage : IDispatchMessage, ICacheInvalidator
{
	public abstract IEnumerable<string> GetCacheTagsToInvalidate();
	public abstract IEnumerable<string> GetCacheKeysToInvalidate();
}
#pragma warning restore CA1034
