// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.Metrics;
using System.Text.Json;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Configuration;
using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Caching;
using Excalibur.Dispatch.Messaging;

using Tests.Shared.Helpers;

using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using MsOptions = Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Middleware.Tests.Caching;

/// <summary>
/// Targeted tests to cover remaining uncovered lines in Excalibur.Dispatch.Caching.
/// Covers: CacheResilienceOptions defaults, CachedValueJsonConverter edge cases,
/// CachingServiceCollectionExtensions wrapper classes, CachingDispatchBuilderExtensions
/// Configure lambda, DefaultCacheKeyBuilder fallback, and LruCache timer callback.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Caching")]
public sealed class CachingCoverageTargetedShould : UnitTestBase
{
	/// <summary>
	/// Creates a ServiceCollection with all dependencies needed to resolve the wrapper middleware.
	/// </summary>
	private static ServiceCollection CreateServicesWithCachingDependencies(Action<CacheOptions>? configure = null)
	{
		var services = new ServiceCollection();
		services.AddDispatchCaching(configure ?? (opts =>
		{
			opts.Enabled = true;
			opts.CacheMode = CacheMode.Hybrid;
		}));

		// Register dependencies required by CachingMiddleware and CacheInvalidationMiddleware
		services.AddSingleton<IMeterFactory>(new TestMeterFactory());
		services.AddSingleton(A.Fake<HybridCache>());
		services.AddSingleton(A.Fake<ICacheKeyBuilder>());
		services.AddLogging(lb => lb.AddProvider(NullLoggerProvider.Instance));

		return services;
	}

	// =========================================================================
	// CacheResilienceOptions: verify all default property values are read
	// =========================================================================

	[Fact]
	public void CacheResilienceOptions_HaveExpectedDefaults()
	{
		// Arrange & Act
		var options = new CacheResilienceOptions();

		// Assert -- reading each property covers the getter with default initializer
		options.CircuitBreaker.Enabled.ShouldBeTrue();
		options.CircuitBreaker.FailureThreshold.ShouldBe(5);
		options.CircuitBreaker.FailureWindow.ShouldBe(TimeSpan.FromMinutes(1));
		options.CircuitBreaker.OpenDuration.ShouldBe(TimeSpan.FromSeconds(30));
		options.CircuitBreaker.HalfOpenTestLimit.ShouldBe(3);
		options.CircuitBreaker.HalfOpenSuccessThreshold.ShouldBe(2);
		options.TypeNameCache.MaxCacheSize.ShouldBe(10_000);
		options.TypeNameCache.CacheTtl.ShouldBe(TimeSpan.FromHours(1));
		options.EnableFallback.ShouldBeTrue();
		options.LogMetricsOnDisposal.ShouldBeTrue();
	}

	[Fact]
	public void CacheResilienceOptions_AllowSettingAllProperties()
	{
		// Arrange
		var options = new CacheResilienceOptions();

		// Act
		options.CircuitBreaker.Enabled = false;
		options.CircuitBreaker.FailureThreshold = 10;
		options.CircuitBreaker.FailureWindow = TimeSpan.FromMinutes(5);
		options.CircuitBreaker.OpenDuration = TimeSpan.FromSeconds(60);
		options.CircuitBreaker.HalfOpenTestLimit = 5;
		options.CircuitBreaker.HalfOpenSuccessThreshold = 4;
		options.TypeNameCache.MaxCacheSize = 50_000;
		options.TypeNameCache.CacheTtl = TimeSpan.FromHours(2);
		options.EnableFallback = false;
		options.LogMetricsOnDisposal = false;

		// Assert
		options.CircuitBreaker.Enabled.ShouldBeFalse();
		options.CircuitBreaker.FailureThreshold.ShouldBe(10);
		options.CircuitBreaker.FailureWindow.ShouldBe(TimeSpan.FromMinutes(5));
		options.CircuitBreaker.OpenDuration.ShouldBe(TimeSpan.FromSeconds(60));
		options.CircuitBreaker.HalfOpenTestLimit.ShouldBe(5);
		options.CircuitBreaker.HalfOpenSuccessThreshold.ShouldBe(4);
		options.TypeNameCache.MaxCacheSize.ShouldBe(50_000);
		options.TypeNameCache.CacheTtl.ShouldBe(TimeSpan.FromHours(2));
		options.EnableFallback.ShouldBeFalse();
		options.LogMetricsOnDisposal.ShouldBeFalse();
	}

	// =========================================================================
	// CachingServiceCollectionExtensions: wrapper middleware invocation
	// Lines 216-255: CachingMiddlewareWrapper and CacheInvalidationMiddlewareWrapper
	// =========================================================================

	[Fact]
	public void AddDispatchCaching_RegistersWrapperMiddleware()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddDispatchCaching(opts =>
		{
			opts.Enabled = true;
			opts.CacheMode = CacheMode.Hybrid;
		});

		// Assert - wrappers are registered as IDispatchMiddleware
		services.ShouldContain(sd => sd.ServiceType == typeof(IDispatchMiddleware));
	}

	[Fact]
	public async Task CachingMiddlewareWrapper_WhenDisabled_SkipsCaching()
	{
		// Arrange -- caching disabled so wrapper passes through to next delegate
		var services = CreateServicesWithCachingDependencies(opts =>
		{
			opts.Enabled = false;
			opts.CacheMode = CacheMode.Hybrid;
		});

		var provider = services.BuildServiceProvider();
		var middlewares = provider.GetServices<IDispatchMiddleware>().ToList();

		var cachingWrapper = middlewares.FirstOrDefault(m =>
			m.Stage == DispatchMiddlewareStage.Cache &&
			m.GetType().Name.Contains("CachingMiddlewareWrapper"));

		cachingWrapper.ShouldNotBeNull();

		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>());
		var expectedResult = A.Fake<IMessageResult>();
		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct) =>
			new(expectedResult);

		// Act -- disabled, so wrapper passes through
		var result = await cachingWrapper.InvokeAsync(message, context, Next, CancellationToken.None);

		// Assert
		result.ShouldBe(expectedResult);
	}

	[Fact]
	public async Task CacheInvalidationMiddlewareWrapper_WhenDisabled_SkipsCacheInvalidation()
	{
		// Arrange -- caching disabled
		var services = CreateServicesWithCachingDependencies(opts =>
		{
			opts.Enabled = false;
			opts.CacheMode = CacheMode.Hybrid;
		});

		var provider = services.BuildServiceProvider();
		var middlewares = provider.GetServices<IDispatchMiddleware>().ToList();

		var invalidationWrapper = middlewares.FirstOrDefault(m =>
			m.Stage == DispatchMiddlewareStage.Cache &&
			m.GetType().Name.Contains("CacheInvalidationMiddlewareWrapper"));

		invalidationWrapper.ShouldNotBeNull();

		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>());
		var expectedResult = A.Fake<IMessageResult>();
		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct) =>
			new(expectedResult);

		// Act -- disabled, so wrapper passes through
		var result = await invalidationWrapper.InvokeAsync(message, context, Next, CancellationToken.None);

		// Assert
		result.ShouldBe(expectedResult);
	}

	// =========================================================================
	// CachingServiceCollectionExtensions: wrappers Stage property and enabled path
	// Lines 219, 245: Stage => DispatchMiddlewareStage.Cache
	// Lines 228-230, 253-255: enabled path delegates to inner middleware
	// =========================================================================

	[Fact]
	public async Task CachingMiddlewareWrapper_WhenEnabled_DelegatesToCachingMiddleware()
	{
		// Arrange -- enabled, so wrapper delegates to inner CachingMiddleware
		var services = CreateServicesWithCachingDependencies();
		var provider = services.BuildServiceProvider();
		var middlewares = provider.GetServices<IDispatchMiddleware>().ToList();

		var cachingWrapper = middlewares.FirstOrDefault(m =>
			m.GetType().Name.Contains("CachingMiddlewareWrapper"));

		cachingWrapper.ShouldNotBeNull();

		// Verify Stage property (line 219)
		cachingWrapper.Stage.ShouldBe(DispatchMiddlewareStage.Cache);

		var message = A.Fake<IDispatchAction>();
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>());
		var nextResult = A.Fake<IMessageResult>();
		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct) =>
			new(nextResult);

		// Act -- enabled, delegates to inner CachingMiddleware (lines 228-230)
		var result = await cachingWrapper.InvokeAsync(message, context, Next, CancellationToken.None);

		// Assert
		result.ShouldNotBeNull();
	}

	[Fact]
	public async Task CacheInvalidationMiddlewareWrapper_WhenEnabled_DelegatesToInvalidationMiddleware()
	{
		// Arrange -- enabled
		var services = CreateServicesWithCachingDependencies();
		var provider = services.BuildServiceProvider();
		var middlewares = provider.GetServices<IDispatchMiddleware>().ToList();

		var invalidationWrapper = middlewares.FirstOrDefault(m =>
			m.GetType().Name.Contains("CacheInvalidationMiddlewareWrapper"));

		invalidationWrapper.ShouldNotBeNull();

		// Verify Stage property (line 245)
		invalidationWrapper.Stage.ShouldBe(DispatchMiddlewareStage.Cache);

		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>());
		var nextResult = A.Fake<IMessageResult>();
		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct) =>
			new(nextResult);

		// Act -- enabled, delegates to inner invalidation middleware (lines 253-255)
		var result = await invalidationWrapper.InvokeAsync(message, context, Next, CancellationToken.None);

		// Assert
		result.ShouldNotBeNull();
	}

	// =========================================================================
	// CachingServiceCollectionExtensions <>c: static factory lambdas (lines 188, 191, 206)
	// =========================================================================

	[Fact]
	public void AddDispatchCaching_ResolvesWrapperMiddlewareFromServiceProvider()
	{
		// Arrange -- resolving IDispatchMiddleware exercises the static lambda factories
		var services = CreateServicesWithCachingDependencies();

		// Act
		var provider = services.BuildServiceProvider();
		var middlewares = provider.GetServices<IDispatchMiddleware>().ToList();

		// Assert -- factory lambdas at lines 188, 191 executed successfully
		middlewares.ShouldNotBeEmpty();
		middlewares.Count(m => m.Stage == DispatchMiddlewareStage.Cache).ShouldBeGreaterThanOrEqualTo(1);
	}

	[Fact]
	public void AddDispatchCaching_RegistersDefaultResultCachePolicy()
	{
		// Arrange -- line 206: services.TryAddSingleton<IResultCachePolicy>(new DefaultResultCachePolicy(...))
		var services = CreateServicesWithCachingDependencies();
		var provider = services.BuildServiceProvider();

		// Act
		var policy = provider.GetService<IResultCachePolicy>();

		// Assert
		policy.ShouldNotBeNull();
		policy.ShouldCache(A.Fake<IDispatchMessage>(), null).ShouldBeTrue();
	}

	// =========================================================================
	// CachingDispatchBuilderExtensions: WithCachingOptions Configure<CacheOptions> lambda
	// Lines 96-104: the lambda that copies options properties
	// =========================================================================

	[Fact]
	public void WithCachingOptions_ConfigureLambda_CopiesAllProperties()
	{
		// Arrange
		var services = new ServiceCollection();
		var builder = A.Fake<IDispatchBuilder>();
		A.CallTo(() => builder.Services).Returns(services);
		var tags = new[] { "t1", "t2" };
		var timeout = TimeSpan.FromSeconds(10);
		var expiration = TimeSpan.FromMinutes(20);
		var globalPolicy = A.Fake<IResultCachePolicy>();
		var keyBuilder = A.Fake<ICacheKeyBuilder>();

		// Act -- WithCachingOptions registers a Configure<CacheOptions> callback
		builder.WithCachingOptions(options =>
		{
			options.Enabled = true;
			options.CacheMode = CacheMode.Distributed;
			options.Behavior.DefaultExpiration = expiration;
			options.DefaultTags = tags;
			options.Behavior.CacheTimeout = timeout;
			options.GlobalPolicy = globalPolicy;
			options.CacheKeyBuilder = keyBuilder;
		});

		// Resolve and invoke the IConfigureOptions<CacheOptions> to cover lines 96-104
		var provider = services.BuildServiceProvider();
		var configureOptions = provider.GetServices<IConfigureOptions<CacheOptions>>().ToList();
		configureOptions.ShouldNotBeEmpty();

		var resolved = new CacheOptions();
		foreach (var cfg in configureOptions)
		{
			cfg.Configure(resolved);
		}

		// Assert -- the lambda body at lines 96-104 copied all properties
		resolved.Enabled.ShouldBeTrue();
		resolved.CacheMode.ShouldBe(CacheMode.Distributed);
		resolved.Behavior.DefaultExpiration.ShouldBe(expiration);
		resolved.DefaultTags.ShouldBe(tags);
		resolved.Behavior.CacheTimeout.ShouldBe(timeout);
		resolved.GlobalPolicy.ShouldBe(globalPolicy);
		resolved.CacheKeyBuilder.ShouldBe(keyBuilder);
	}

	// =========================================================================
	// DefaultCacheKeyBuilder: action with ICacheable<T> where GetCacheKey returns null
	// Lines 74-75: TryGetCacheKeyFromInterface returns false when result is not string
	// =========================================================================

	[Fact]
	public void DefaultCacheKeyBuilder_WithNullCacheKey_FallsBackToSerialization()
	{
		// Arrange -- ICacheable that returns null from GetCacheKey (non-string result)
		var serializer = A.Fake<IJsonSerializer>();
		A.CallTo(() => serializer.Serialize(A<object>._, A<Type>._)).Returns("{}");
		var builder = new DefaultCacheKeyBuilder(serializer);
		var action = new CacheableReturningNullKey();
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.TenantId).Returns("t");
		A.CallTo(() => context.UserId).Returns("u");

		// Act -- GetCacheKey returns null, so TryGetCacheKeyFromInterface returns false (lines 74-75)
		var key = builder.CreateKey(action, context);

		// Assert -- should fall back to serialization
		key.ShouldNotBeNullOrWhiteSpace();
		A.CallTo(() => serializer.Serialize(action, A<Type>._)).MustHaveHappened();
	}

	// =========================================================================
	// LruCache: timer-based cleanup of expired items (line 70)
	// =========================================================================

	[Fact]
	public async Task LruCache_WithTtl_RemovesExpiredItemsViaTimer()
	{
		// Arrange -- short TTL and short cleanup interval to trigger timer callback (line 70)
		using var cache = new LruCache<string, string>(
			capacity: 10,
			defaultTtl: TimeSpan.FromMilliseconds(50),
			cleanupInterval: TimeSpan.FromMilliseconds(30));

		cache.Set("key1", "value1");
		cache.Set("key2", "value2");
		cache.Count.ShouldBe(2);

		// Act -- wait for items to expire and timer to clean up
		await global::Tests.Shared.Infrastructure.TestTiming.PauseAsync(200);

		// Assert -- expired items should have been removed by the timer callback
		cache.TryGetValue("key1", out _).ShouldBeFalse();
		cache.TryGetValue("key2", out _).ShouldBeFalse();
	}

	// =========================================================================
	// LruCache: GetOrAdd double-check path inside lock (lines 246-250)
	// =========================================================================

	[Fact]
	public void LruCache_GetOrAdd_ConcurrentInsert_DoubleCheckReturnsExisting()
	{
		// Arrange -- pre-populate cache so the double-check inside GetOrAdd lock hits
		using var cache = new LruCache<string, int>(10, defaultTtl: TimeSpan.FromMinutes(5));
		cache.Set("race-key", 42);

		// Act -- GetOrAdd should find the existing value (double-check path at lines 246-250)
		var result = cache.GetOrAdd("race-key", _ => 99, TimeSpan.FromMinutes(1));

		// Assert
		result.ShouldBe(42);
	}

	// =========================================================================
	// Test helper types
	// =========================================================================

	/// <summary>
	/// ICacheable implementation where GetCacheKey returns null to test fallback path.
	/// </summary>
	private sealed class CacheableReturningNullKey : ICacheable<string>
	{
		public string GetCacheKey() => null!;
	}
}

