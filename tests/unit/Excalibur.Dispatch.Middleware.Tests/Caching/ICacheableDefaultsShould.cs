// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Caching;

using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging.Abstractions;

using MsOptions = Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Middleware.Tests.Caching;

[Trait("Category", "Unit")]
public sealed class ICacheableDefaultsShould : UnitTestBase
{
	[Fact]
	public void ExpirationSeconds_ReturnsDefaultOf60()
	{
		// Arrange — CacheableWithOnlyKey only implements GetCacheKey(), relying on defaults for all else
		ICacheable<string> cacheable = new CacheableWithOnlyKey();

		// Act & Assert
		cacheable.ExpirationSeconds.ShouldBe(60);
	}

	[Fact]
	public void GetCacheTags_ReturnsNullByDefault()
	{
		// Arrange
		ICacheable<string> cacheable = new CacheableWithOnlyKey();

		// Act
		var tags = cacheable.GetCacheTags();

		// Assert
		tags.ShouldBeNull();
	}

	[Fact]
	public void ShouldCache_ReturnsTrueByDefault()
	{
		// Arrange
		ICacheable<string> cacheable = new CacheableWithOnlyKey();

		// Act
		var shouldCache = cacheable.ShouldCache("some result");

		// Assert
		shouldCache.ShouldBeTrue();
	}

	[Fact]
	public void ShouldCache_ReturnsTrueForNullResult()
	{
		// Arrange
		ICacheable<string> cacheable = new CacheableWithOnlyKey();

		// Act
		var shouldCache = cacheable.ShouldCache(null);

		// Assert
		shouldCache.ShouldBeTrue();
	}

	[Fact]
	public void GetCacheKey_ReturnsImplementedValue()
	{
		// Arrange
		ICacheable<string> cacheable = new CacheableWithOnlyKey();

		// Act
		var key = cacheable.GetCacheKey();

		// Assert
		key.ShouldBe("only-key-test");
	}

	[Fact]
	public void ExpirationSeconds_WhenOverridden_ReturnsCustomValue()
	{
		// Arrange
		ICacheable<int> cacheable = new CacheableWithCustomExpiration();

		// Act & Assert
		cacheable.ExpirationSeconds.ShouldBe(300);
	}

	[Fact]
	public void GetCacheTags_WhenOverridden_ReturnsCustomTags()
	{
		// Arrange
		ICacheable<int> cacheable = new CacheableWithCustomTags();

		// Act
		var tags = cacheable.GetCacheTags();

		// Assert
		tags.ShouldNotBeNull();
		tags.ShouldContain("custom-tag-1");
		tags.ShouldContain("custom-tag-2");
	}

	[Fact]
	public void ShouldCache_WhenOverridden_ReturnsCustomLogic()
	{
		// Arrange
		ICacheable<string> cacheable = new CacheableWithConditionalCaching();

		// Act & Assert — null results should not be cached
		cacheable.ShouldCache(null).ShouldBeFalse();
		// Non-null results should be cached
		cacheable.ShouldCache("some value").ShouldBeTrue();
	}

	[Fact]
	public async Task CachedMessageResult_WhenCreatedViaCacheHit_HasCorrectProperties()
	{
		// Arrange — exercise CachedMessageResult<T> through the middleware by triggering a cache hit
		var cache = A.Fake<HybridCache>();
		var keyBuilder = A.Fake<ICacheKeyBuilder>();
		var serviceProvider = new ServiceCollection().BuildServiceProvider();
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>());
		A.CallTo(() => keyBuilder.CreateKey(A<IDispatchAction>._, A<IMessageContext>._)).Returns("result-test-key");

		var options = new CacheOptions { Enabled = true, CacheMode = CacheMode.Hybrid };
		using var meterFactory = new TestMeterFactory();
		var middleware = new CachingMiddleware(
			meterFactory,
			cache,
			keyBuilder,
			serviceProvider,
			MsOptions.Options.Create(options),
			NullLogger<CachingMiddleware>.Instance);

		// Simulate a cache hit — CachedValue with HasExecuted=true, ShouldCache=true, non-null Value
		A.CallTo(() => cache.GetOrCreateAsync(
			A<string>._,
			A<Func<CancellationToken, ValueTask<CachedValue>>>._,
			A<Func<Func<CancellationToken, ValueTask<CachedValue>>, CancellationToken, ValueTask<CachedValue>>>._,
			A<HybridCacheEntryOptions?>._,
			A<IEnumerable<string>?>._,
			A<CancellationToken>._))
			.Returns(new ValueTask<CachedValue>(new CachedValue
			{
				HasExecuted = true,
				ShouldCache = true,
				Value = 42,
				TypeName = typeof(int).AssemblyQualifiedName
			}));

		var message = new CacheableIntAction();

		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct) =>
			new(A.Fake<IMessageResult>());

		// Act
		var result = await middleware.InvokeAsync(message, context, Next, CancellationToken.None);

		// Assert — verify all CachedMessageResult<int> properties
		result.ShouldNotBeNull();
		result.Succeeded.ShouldBeTrue();
		result.CacheHit.ShouldBeTrue();
		result.ProblemDetails.ShouldBeNull();
		result.ErrorMessage.ShouldBeNull();
		result.ValidationResult.ShouldBeNull();
		result.AuthorizationResult.ShouldBeNull();

		var typedResult = result.ShouldBeAssignableTo<IMessageResult<int>>();
		typedResult.ReturnValue.ShouldBe(42);
	}

	[Fact]
	public async Task CacheInvalidationMiddleware_WithAttributeTags_InvalidatesByAttributeTags()
	{
		// Arrange
		var hybridCache = A.Fake<HybridCache>();
		var options = MsOptions.Options.Create(new CacheOptions
		{
			Enabled = true,
			CacheMode = CacheMode.Hybrid
		});
		var middleware = new CacheInvalidationMiddleware(new TestMeterFactory(), options, hybridCache: hybridCache);
		var context = A.Fake<IMessageContext>();
		var successResult = A.Fake<IMessageResult>();
		A.CallTo(() => successResult.Succeeded).Returns(true);

		var message = new InvalidateCacheByAttributeAction();

		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct) =>
			new(successResult);

		// Act
		var result = await middleware.InvokeAsync(message, context, Next, CancellationToken.None);

		// Assert — cache invalidation should happen for the attribute-specified tags
		result.ShouldBe(successResult);
		A.CallTo(() => hybridCache.RemoveByTagAsync(
			A<IEnumerable<string>>.That.Contains("attr-tag-1"),
			A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task CacheInvalidationMiddleware_WhenNextReturnsFailed_StillProcessesInvalidation()
	{
		// Arrange
		var hybridCache = A.Fake<HybridCache>();
		var options = MsOptions.Options.Create(new CacheOptions
		{
			Enabled = true,
			CacheMode = CacheMode.Hybrid
		});
		var middleware = new CacheInvalidationMiddleware(new TestMeterFactory(), options, hybridCache: hybridCache);
		var context = A.Fake<IMessageContext>();
		var failedResult = A.Fake<IMessageResult>();
		A.CallTo(() => failedResult.Succeeded).Returns(false);

		var message = new InvalidateCacheByAttributeAction();

		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct) =>
			new(failedResult);

		// Act
		var result = await middleware.InvokeAsync(message, context, Next, CancellationToken.None);

		// Assert — invalidation should still happen even when result is failed
		result.ShouldBe(failedResult);
		A.CallTo(() => hybridCache.RemoveByTagAsync(
			A<IEnumerable<string>>.That.Contains("attr-tag-1"),
			A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	// Test helper types

	private sealed class CacheableWithOnlyKey : ICacheable<string>
	{
		// Only implements the required GetCacheKey(); all other members use default implementations
		public string GetCacheKey() => "only-key-test";
	}

	private sealed class CacheableWithCustomExpiration : ICacheable<int>
	{
		public string GetCacheKey() => "custom-expiration-key";
		public int ExpirationSeconds => 300;
	}

	private sealed class CacheableWithCustomTags : ICacheable<int>
	{
		public string GetCacheKey() => "custom-tags-key";
		public string[]? GetCacheTags() => ["custom-tag-1", "custom-tag-2"];
	}

	private sealed class CacheableWithConditionalCaching : ICacheable<string>
	{
		public string GetCacheKey() => "conditional-key";
		public bool ShouldCache(object? result) => result is not null;
	}

	[CacheResult(ExpirationSeconds = 60)]
	private sealed class CacheableIntAction : IDispatchAction<int>
	{
	}

	[InvalidateCache(Tags = ["attr-tag-1", "attr-tag-2"])]
	private sealed class InvalidateCacheByAttributeAction : IDispatchAction
	{
	}
}
