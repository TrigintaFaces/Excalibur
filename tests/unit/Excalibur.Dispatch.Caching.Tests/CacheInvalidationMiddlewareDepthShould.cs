// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.Metrics;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Caching;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Caching.Memory;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Caching.Tests;

/// <summary>
/// Depth tests for <see cref="CacheInvalidationMiddleware"/> covering
/// unsupported cache mode, memory mode without tag tracker, deduplication, and edge cases.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Feature", "Caching")]
public sealed class CacheInvalidationMiddlewareDepthShould : IDisposable
{
	private readonly IMeterFactory _meterFactory = new TestMeterFactory();
	private readonly IMemoryCache _fakeMemoryCache = A.Fake<IMemoryCache>();
	private readonly HybridCache _fakeHybridCache = A.Fake<HybridCache>();
	private readonly ICacheTagTracker _fakeTagTracker = A.Fake<ICacheTagTracker>();

	public void Dispose()
	{
		if (_meterFactory is IDisposable d) d.Dispose();
		_fakeMemoryCache.Dispose();
	}

	[Fact]
	public async Task HandleUnknownCacheMode_Gracefully_WithUnifiedInvalidation()
	{
		// Arrange -- unknown CacheMode now handled gracefully by unified path (no throw)
		var options = new CacheOptions { Enabled = true, CacheMode = (CacheMode)99 };
		var middleware = CreateMiddleware(options, hybridCache: _fakeHybridCache);

		var message = A.Fake<ICacheInvalidator>();
		A.CallTo(() => message.GetCacheTagsToInvalidate()).Returns(["tag"]);
		A.CallTo(() => message.GetCacheKeysToInvalidate()).Returns(Enumerable.Empty<string>());

		var context = A.Fake<IMessageContext>();
		var expectedResult = A.Fake<IMessageResult>();

		// Act -- unified invalidation handles all modes, no exception for unknown
		var result = await middleware.InvokeAsync(
			message, context,
			(_, _, _) => new ValueTask<IMessageResult>(expectedResult),
			CancellationToken.None);

		// Assert
		result.ShouldBe(expectedResult);
		A.CallTo(() => _fakeHybridCache.RemoveByTagAsync(
			A<IEnumerable<string>>._, A<CancellationToken>._)).MustHaveHappened();
	}

	[Fact]
	public async Task SkipTagInvalidation_WhenMemoryMode_NoTagTracker_NoHybridCache()
	{
		// Arrange -- memory mode with IMemoryCache but no tag tracker and no HybridCache
		var options = new CacheOptions { Enabled = true, CacheMode = CacheMode.Memory };
		var middleware = CreateMiddleware(options, memoryCache: _fakeMemoryCache);

		var message = A.Fake<ICacheInvalidator>();
		A.CallTo(() => message.GetCacheTagsToInvalidate()).Returns(["tag-to-skip"]);
		A.CallTo(() => message.GetCacheKeysToInvalidate()).Returns(["key-to-remove"]);

		var context = A.Fake<IMessageContext>();
		var expectedResult = A.Fake<IMessageResult>();

		// Act -- should not throw, just skip tag resolution
		var result = await middleware.InvokeAsync(
			message, context,
			(_, _, _) => new ValueTask<IMessageResult>(expectedResult),
			CancellationToken.None);

		// Assert -- keys are still removed directly
		result.ShouldBe(expectedResult);
		A.CallTo(() => _fakeMemoryCache.Remove("key-to-remove")).MustHaveHappened();
	}

	[Fact]
	public async Task DeduplicateTags_WhenSameTagFromMultipleSources()
	{
		// Arrange -- both ICacheInvalidator and default tags contain "shared-tag"
		var options = new CacheOptions { Enabled = true, CacheMode = CacheMode.Hybrid, DefaultTags = ["shared-tag"] };
		var middleware = CreateMiddleware(options, hybridCache: _fakeHybridCache);

		var message = A.Fake<ICacheInvalidator>();
		A.CallTo(() => message.GetCacheTagsToInvalidate()).Returns(["shared-tag"]);
		A.CallTo(() => message.GetCacheKeysToInvalidate()).Returns(Enumerable.Empty<string>());

		var context = A.Fake<IMessageContext>();
		var expectedResult = A.Fake<IMessageResult>();

		// Act
		await middleware.InvokeAsync(
			message, context,
			(_, _, _) => new ValueTask<IMessageResult>(expectedResult),
			CancellationToken.None);

		// Assert -- RemoveByTagAsync was called (tags are deduped via Distinct inside)
		A.CallTo(() => _fakeHybridCache.RemoveByTagAsync(
			A<IEnumerable<string>>._,
			A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task PassThrough_WhenICacheInvalidator_ReturnsEmptyTagsAndEmptyKeys_NoDefaultTags()
	{
		// Arrange
		var options = new CacheOptions { Enabled = true, CacheMode = CacheMode.Hybrid };
		var middleware = CreateMiddleware(options, hybridCache: _fakeHybridCache);

		var message = A.Fake<ICacheInvalidator>();
		A.CallTo(() => message.GetCacheTagsToInvalidate()).Returns(Enumerable.Empty<string>());
		A.CallTo(() => message.GetCacheKeysToInvalidate()).Returns(Enumerable.Empty<string>());

		var context = A.Fake<IMessageContext>();
		var expectedResult = A.Fake<IMessageResult>();

		// Act
		var result = await middleware.InvokeAsync(
			message, context,
			(_, _, _) => new ValueTask<IMessageResult>(expectedResult),
			CancellationToken.None);

		// Assert -- no invalidation performed
		result.ShouldBe(expectedResult);
		A.CallTo(() => _fakeHybridCache.RemoveByTagAsync(
			A<IEnumerable<string>>._,
			A<CancellationToken>._)).MustNotHaveHappened();
		A.CallTo(() => _fakeHybridCache.RemoveAsync(
			A<IEnumerable<string>>._,
			A<CancellationToken>._)).MustNotHaveHappened();
	}

	[Fact]
	public async Task ForwardCancellationToken_ToHybridCacheRemoveByTag()
	{
		// Arrange
		using var cts = new CancellationTokenSource();
		var options = new CacheOptions { Enabled = true, CacheMode = CacheMode.Hybrid };
		var middleware = CreateMiddleware(options, hybridCache: _fakeHybridCache);

		var message = A.Fake<ICacheInvalidator>();
		A.CallTo(() => message.GetCacheTagsToInvalidate()).Returns(["tag1"]);
		A.CallTo(() => message.GetCacheKeysToInvalidate()).Returns(Enumerable.Empty<string>());

		var context = A.Fake<IMessageContext>();

		// Act
		await middleware.InvokeAsync(
			message, context,
			(_, _, _) => new ValueTask<IMessageResult>(A.Fake<IMessageResult>()),
			cts.Token);

		// Assert -- exact token forwarded
		A.CallTo(() => _fakeHybridCache.RemoveByTagAsync(
			A<IEnumerable<string>>._,
			cts.Token)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task HandleDistributedMode_WithKeysOnly_ViaMemoryCacheFallback()
	{
		// Arrange -- distributed mode without HybridCache, keys only (no tags)
		var options = new CacheOptions { Enabled = true, CacheMode = CacheMode.Distributed };
		var fakeMemoryCache = A.Fake<IMemoryCache>();
		var middleware = CreateMiddleware(options, memoryCache: fakeMemoryCache);

		var message = A.Fake<ICacheInvalidator>();
		A.CallTo(() => message.GetCacheTagsToInvalidate()).Returns(Enumerable.Empty<string>());
		A.CallTo(() => message.GetCacheKeysToInvalidate()).Returns(["fallback-key"]);

		var context = A.Fake<IMessageContext>();
		var expectedResult = A.Fake<IMessageResult>();

		// Act
		var result = await middleware.InvokeAsync(
			message, context,
			(_, _, _) => new ValueTask<IMessageResult>(expectedResult),
			CancellationToken.None);

		// Assert
		result.ShouldBe(expectedResult);
		A.CallTo(() => fakeMemoryCache.Remove("fallback-key"))
			.MustHaveHappened();
	}

	[Fact]
	public async Task ExecuteNextDelegate_BeforeInvalidation()
	{
		// Arrange -- verify next delegate is always called first
		var options = new CacheOptions { Enabled = true, CacheMode = CacheMode.Hybrid };
		var middleware = CreateMiddleware(options, hybridCache: _fakeHybridCache);

		var delegateCalled = false;
		var message = A.Fake<ICacheInvalidator>();
		A.CallTo(() => message.GetCacheTagsToInvalidate()).Returns(["tag"]);
		A.CallTo(() => message.GetCacheKeysToInvalidate()).Returns(Enumerable.Empty<string>());

		var context = A.Fake<IMessageContext>();
		var expectedResult = A.Fake<IMessageResult>();

		// Act
		var result = await middleware.InvokeAsync(
			message, context,
			(_, _, _) =>
			{
				delegateCalled = true;
				return new ValueTask<IMessageResult>(expectedResult);
			},
			CancellationToken.None);

		// Assert
		delegateCalled.ShouldBeTrue();
		result.ShouldBe(expectedResult);
	}

	[Fact]
	public async Task ReturnNextDelegateResult_EvenWhenInvalidating()
	{
		// Arrange
		var options = new CacheOptions { Enabled = true, CacheMode = CacheMode.Hybrid };
		var middleware = CreateMiddleware(options, hybridCache: _fakeHybridCache);

		var message = A.Fake<ICacheInvalidator>();
		A.CallTo(() => message.GetCacheTagsToInvalidate()).Returns(["tag"]);
		A.CallTo(() => message.GetCacheKeysToInvalidate()).Returns(["key"]);

		var context = A.Fake<IMessageContext>();
		var expectedResult = A.Fake<IMessageResult>();

		// Act
		var result = await middleware.InvokeAsync(
			message, context,
			(_, _, _) => new ValueTask<IMessageResult>(expectedResult),
			CancellationToken.None);

		// Assert -- return value is from handler, not affected by invalidation
		result.ShouldBe(expectedResult);
	}

	private CacheInvalidationMiddleware CreateMiddleware(
		CacheOptions? options = null,
		ICacheTagTracker? tagTracker = null,
		IMemoryCache? memoryCache = null,
		HybridCache? hybridCache = null)
	{
		return new CacheInvalidationMiddleware(
			_meterFactory,
			MsOptions.Create(options ?? new CacheOptions { Enabled = true }),
			tagTracker,
			memoryCache,
			hybridCache);
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
