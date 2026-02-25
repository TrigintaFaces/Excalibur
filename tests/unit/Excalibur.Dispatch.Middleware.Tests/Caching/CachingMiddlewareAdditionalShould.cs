// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA2213 // Disposable fields should be disposed -- TestMeterFactory is test-scoped

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Caching;

using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using MsOptions = Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Middleware.Tests.Caching;

/// <summary>
/// Additional unit tests for <see cref="CachingMiddleware"/> covering edge cases
/// and uncovered code paths: GlobalPolicy from options, ShouldCacheBasedOnPolicy branches,
/// startup warnings for Hybrid mode, ICacheable ShouldCache=false, and TargetException handling.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Caching")]
public sealed class CachingMiddlewareAdditionalShould : UnitTestBase
{
	private readonly TestMeterFactory _meterFactory;
	private readonly HybridCache _cache;
	private readonly ICacheKeyBuilder _keyBuilder;
	private readonly IServiceProvider _services;
	private readonly ILogger<CachingMiddleware> _logger;
	private readonly IMessageContext _context;
	private readonly CancellationToken _ct = CancellationToken.None;

	public CachingMiddlewareAdditionalShould()
	{
		_meterFactory = new TestMeterFactory();
		_cache = A.Fake<HybridCache>();
		_keyBuilder = A.Fake<ICacheKeyBuilder>();
		_services = new ServiceCollection().BuildServiceProvider();
		_logger = NullLogger<CachingMiddleware>.Instance;
		_context = A.Fake<IMessageContext>();

		A.CallTo(() => _context.Items).Returns(new Dictionary<string, object>());
		A.CallTo(() => _keyBuilder.CreateKey(A<IDispatchAction>._, A<IMessageContext>._))
			.Returns("test-cache-key");
	}

	private CachingMiddleware CreateMiddleware(CacheOptions? opts = null, IResultCachePolicy? globalPolicy = null)
	{
		var options = opts ?? new CacheOptions { Enabled = true, CacheMode = CacheMode.Hybrid };
		return new CachingMiddleware(
			_meterFactory,
			_cache,
			_keyBuilder,
			_services,
			MsOptions.Options.Create(options),
			_logger,
			globalPolicy);
	}

	[Fact]
	public async Task InvokeAsync_WhenGlobalPolicyIsSetViaOptions_UsesItForICacheable()
	{
		// Arrange -- global policy set via CacheOptions.GlobalPolicy rather than constructor parameter
		// For ICacheable messages, the middleware calls ShouldCache(message, null) which checks global policy
		var globalPolicy = A.Fake<IResultCachePolicy>();
		A.CallTo(() => globalPolicy.ShouldCache(A<IDispatchMessage>._, A<object?>._)).Returns(false);

		var options = new CacheOptions
		{
			Enabled = true,
			CacheMode = CacheMode.Hybrid,
			GlobalPolicy = globalPolicy
		};

		// Pass null for the globalPolicy constructor parameter to test the fallback to options.GlobalPolicy
		var middleware = new CachingMiddleware(
			_meterFactory,
			_cache,
			_keyBuilder,
			_services,
			MsOptions.Options.Create(options),
			_logger,
			globalPolicy: null);

		var message = new CacheableTestQuery();
		var expected = A.Fake<IMessageResult>();

		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct) =>
			new(expected);

		// Act
		var result = await middleware.InvokeAsync(message, _context, Next, _ct);

		// Assert -- global policy from options says false, so cache is skipped for ICacheable
		result.ShouldBe(expected);
		A.CallTo(() => _cache.GetOrCreateAsync(
			A<string>._,
			A<Func<CancellationToken, ValueTask<CachedValue>>>._,
			A<Func<Func<CancellationToken, ValueTask<CachedValue>>, CancellationToken, ValueTask<CachedValue>>>._,
			A<HybridCacheEntryOptions?>._,
			A<IEnumerable<string>?>._,
			A<CancellationToken>._)).MustNotHaveHappened();
	}

	[Fact]
	public async Task InvokeAsync_WithHybridMode_EmitsStartupWarningForMemoryDistributedCache()
	{
		// Arrange -- Hybrid mode (not just Distributed) should also trigger the warning
		var serviceCollection = new ServiceCollection();
		serviceCollection.AddDistributedMemoryCache();
		var serviceProvider = serviceCollection.BuildServiceProvider();

		var testLogger = new TestLogger<CachingMiddleware>();
		var options = new CacheOptions { Enabled = true, CacheMode = CacheMode.Hybrid };
		var middleware = new CachingMiddleware(
			_meterFactory,
			_cache,
			_keyBuilder,
			serviceProvider,
			MsOptions.Options.Create(options),
			testLogger);

		var message = new AttributeCacheableAction();

		A.CallTo(() => _cache.GetOrCreateAsync(
			A<string>._,
			A<Func<CancellationToken, ValueTask<CachedValue>>>._,
			A<Func<Func<CancellationToken, ValueTask<CachedValue>>, CancellationToken, ValueTask<CachedValue>>>._,
			A<HybridCacheEntryOptions?>._,
			A<IEnumerable<string>?>._,
			A<CancellationToken>._))
			.Returns(new ValueTask<CachedValue>(new CachedValue()));

		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct) =>
			new(A.Fake<IMessageResult>());

		// Act
		await middleware.InvokeAsync(message, _context, Next, _ct);

		// Assert -- Hybrid mode should also emit warning about MemoryDistributedCache
		testLogger.LogEntries.ShouldContain(entry =>
			entry.LogLevel == LogLevel.Warning &&
			entry.Message.Contains("MemoryDistributedCache"));
	}

	[Fact]
	public async Task InvokeAsync_WhenNoDistributedCacheRegistered_DoesNotThrow()
	{
		// Arrange -- empty service provider (no IDistributedCache registered)
		var serviceProvider = new ServiceCollection().BuildServiceProvider();
		var testLogger = new TestLogger<CachingMiddleware>();
		var options = new CacheOptions { Enabled = true, CacheMode = CacheMode.Distributed };
		var middleware = new CachingMiddleware(
			_meterFactory,
			_cache,
			_keyBuilder,
			serviceProvider,
			MsOptions.Options.Create(options),
			testLogger);

		var message = new AttributeCacheableAction();

		A.CallTo(() => _cache.GetOrCreateAsync(
			A<string>._,
			A<Func<CancellationToken, ValueTask<CachedValue>>>._,
			A<Func<Func<CancellationToken, ValueTask<CachedValue>>, CancellationToken, ValueTask<CachedValue>>>._,
			A<HybridCacheEntryOptions?>._,
			A<IEnumerable<string>?>._,
			A<CancellationToken>._))
			.Returns(new ValueTask<CachedValue>(new CachedValue()));

		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct) =>
			new(A.Fake<IMessageResult>());

		// Act -- should not throw
		await middleware.InvokeAsync(message, _context, Next, _ct);

		// Assert -- no MemoryDistributedCache warning because no distributed cache registered at all
		testLogger.LogEntries.ShouldNotContain(entry =>
			entry.LogLevel == LogLevel.Warning &&
			entry.Message.Contains("MemoryDistributedCache"));
	}

	[Fact]
	public async Task InvokeAsync_WithICacheableAndGlobalPolicyReturningTrue_UsesCaching()
	{
		// Arrange -- ICacheable with global policy returning true
		var globalPolicy = A.Fake<IResultCachePolicy>();
		A.CallTo(() => globalPolicy.ShouldCache(A<IDispatchMessage>._, A<object?>._)).Returns(true);

		var middleware = CreateMiddleware(globalPolicy: globalPolicy);
		var message = new CacheableTestQuery();

		A.CallTo(() => _cache.GetOrCreateAsync(
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
				Value = "cached",
				TypeName = typeof(string).AssemblyQualifiedName
			}));

		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct) =>
			new(A.Fake<IMessageResult>());

		// Act
		var result = await middleware.InvokeAsync(message, _context, Next, _ct);

		// Assert
		result.ShouldNotBeNull();
		A.CallTo(() => _cache.GetOrCreateAsync(
			A<string>._,
			A<Func<CancellationToken, ValueTask<CachedValue>>>._,
			A<Func<Func<CancellationToken, ValueTask<CachedValue>>, CancellationToken, ValueTask<CachedValue>>>._,
			A<HybridCacheEntryOptions?>._,
			A<IEnumerable<string>?>._,
			A<CancellationToken>._)).MustHaveHappened();
	}

	[Fact]
	public async Task InvokeAsync_WithICacheableAndCustomExpirationSeconds_UsesCustomExpiration()
	{
		// Arrange
		var middleware = CreateMiddleware(new CacheOptions
		{
			Enabled = true,
			CacheMode = CacheMode.Hybrid,
			Behavior = { JitterRatio = 0 }, // disable jitter for predictable test
		});
		var message = new CacheableWithCustomExpiration();

		HybridCacheEntryOptions? capturedOptions = null;
		A.CallTo(() => _cache.GetOrCreateAsync(
			A<string>._,
			A<Func<CancellationToken, ValueTask<CachedValue>>>._,
			A<Func<Func<CancellationToken, ValueTask<CachedValue>>, CancellationToken, ValueTask<CachedValue>>>._,
			A<HybridCacheEntryOptions?>._,
			A<IEnumerable<string>?>._,
			A<CancellationToken>._))
			.Invokes(call => capturedOptions = call.GetArgument<HybridCacheEntryOptions?>(3))
			.Returns(new ValueTask<CachedValue>(new CachedValue()));

		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct) =>
			new(A.Fake<IMessageResult>());

		// Act
		await middleware.InvokeAsync(message, _context, Next, _ct);

		// Assert -- should use 300 seconds from ICacheable
		capturedOptions.ShouldNotBeNull();
		capturedOptions.Expiration.ShouldNotBeNull();
		capturedOptions.Expiration.Value.TotalSeconds.ShouldBe(300);
	}

	[Fact]
	public async Task InvokeAsync_WithAttributeExpirationZero_UsesDefaultExpiration()
	{
		// Arrange -- attribute with ExpirationSeconds = 0 should fall back to DefaultExpiration
		var middleware = CreateMiddleware(new CacheOptions
		{
			Enabled = true,
			CacheMode = CacheMode.Hybrid,
			Behavior = { DefaultExpiration = TimeSpan.FromMinutes(15), JitterRatio = 0 }, // disable jitter for predictable test
		});
		var message = new AttributeCacheableWithZeroExpiration();

		HybridCacheEntryOptions? capturedOptions = null;
		A.CallTo(() => _cache.GetOrCreateAsync(
			A<string>._,
			A<Func<CancellationToken, ValueTask<CachedValue>>>._,
			A<Func<Func<CancellationToken, ValueTask<CachedValue>>, CancellationToken, ValueTask<CachedValue>>>._,
			A<HybridCacheEntryOptions?>._,
			A<IEnumerable<string>?>._,
			A<CancellationToken>._))
			.Invokes(call => capturedOptions = call.GetArgument<HybridCacheEntryOptions?>(3))
			.Returns(new ValueTask<CachedValue>(new CachedValue()));

		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct) =>
			new(A.Fake<IMessageResult>());

		// Act
		await middleware.InvokeAsync(message, _context, Next, _ct);

		// Assert -- should use default 15 minutes
		capturedOptions.ShouldNotBeNull();
		capturedOptions.Expiration.ShouldNotBeNull();
		capturedOptions.Expiration.Value.TotalMinutes.ShouldBe(15);
	}

	[Fact]
	public async Task InvokeAsync_WhenICacheableExpirationZero_UsesDefaultExpiration()
	{
		// Arrange -- ICacheable with ExpirationSeconds = 0 should fall back to DefaultExpiration
		var middleware = CreateMiddleware(new CacheOptions
		{
			Enabled = true,
			CacheMode = CacheMode.Hybrid,
			Behavior = { DefaultExpiration = TimeSpan.FromMinutes(20), JitterRatio = 0 },
		});
		var message = new CacheableWithZeroExpiration();

		HybridCacheEntryOptions? capturedOptions = null;
		A.CallTo(() => _cache.GetOrCreateAsync(
			A<string>._,
			A<Func<CancellationToken, ValueTask<CachedValue>>>._,
			A<Func<Func<CancellationToken, ValueTask<CachedValue>>, CancellationToken, ValueTask<CachedValue>>>._,
			A<HybridCacheEntryOptions?>._,
			A<IEnumerable<string>?>._,
			A<CancellationToken>._))
			.Invokes(call => capturedOptions = call.GetArgument<HybridCacheEntryOptions?>(3))
			.Returns(new ValueTask<CachedValue>(new CachedValue()));

		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct) =>
			new(A.Fake<IMessageResult>());

		// Act
		await middleware.InvokeAsync(message, _context, Next, _ct);

		// Assert
		capturedOptions.ShouldNotBeNull();
		capturedOptions.Expiration.ShouldNotBeNull();
		capturedOptions.Expiration.Value.TotalMinutes.ShouldBe(20);
	}

	[Fact]
	public async Task InvokeAsync_WithMemoryCacheMode_SetsNoneFlag()
	{
		// Arrange
		var middleware = CreateMiddleware(new CacheOptions
		{
			Enabled = true,
			CacheMode = CacheMode.Memory
		});
		var message = new AttributeCacheableAction();

		HybridCacheEntryOptions? capturedOptions = null;
		A.CallTo(() => _cache.GetOrCreateAsync(
			A<string>._,
			A<Func<CancellationToken, ValueTask<CachedValue>>>._,
			A<Func<Func<CancellationToken, ValueTask<CachedValue>>, CancellationToken, ValueTask<CachedValue>>>._,
			A<HybridCacheEntryOptions?>._,
			A<IEnumerable<string>?>._,
			A<CancellationToken>._))
			.Invokes(call => capturedOptions = call.GetArgument<HybridCacheEntryOptions?>(3))
			.Returns(new ValueTask<CachedValue>(new CachedValue()));

		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct) =>
			new(A.Fake<IMessageResult>());

		// Act
		await middleware.InvokeAsync(message, _context, Next, _ct);

		// Assert -- memory mode should not disable local cache
		capturedOptions.ShouldNotBeNull();
		capturedOptions.Flags.ShouldBe(HybridCacheEntryFlags.None);
	}

	[Fact]
	public async Task InvokeAsync_WithNegativeJitterRatio_SkipsJitter()
	{
		// Arrange -- negative jitter ratio should be treated as no jitter
		// Use ExpirationSeconds = 0 attribute so DefaultExpiration is used
		var middleware = CreateMiddleware(new CacheOptions
		{
			Enabled = true,
			CacheMode = CacheMode.Hybrid,
			Behavior = { JitterRatio = -0.5, DefaultExpiration = TimeSpan.FromMinutes(5) },
		});
		var message = new AttributeCacheableWithZeroExpiration();

		HybridCacheEntryOptions? capturedOptions = null;
		A.CallTo(() => _cache.GetOrCreateAsync(
			A<string>._,
			A<Func<CancellationToken, ValueTask<CachedValue>>>._,
			A<Func<Func<CancellationToken, ValueTask<CachedValue>>, CancellationToken, ValueTask<CachedValue>>>._,
			A<HybridCacheEntryOptions?>._,
			A<IEnumerable<string>?>._,
			A<CancellationToken>._))
			.Invokes(call => capturedOptions = call.GetArgument<HybridCacheEntryOptions?>(3))
			.Returns(new ValueTask<CachedValue>(new CachedValue()));

		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct) =>
			new(A.Fake<IMessageResult>());

		// Act
		await middleware.InvokeAsync(message, _context, Next, _ct);

		// Assert -- expiration should be exactly 5 minutes (no jitter applied)
		capturedOptions.ShouldNotBeNull();
		capturedOptions.Expiration.ShouldNotBeNull();
		capturedOptions.Expiration.Value.TotalMinutes.ShouldBe(5);
	}

	[Fact]
	public async Task InvokeAsync_WithICacheableDistributedMode_SetsDisableLocalCacheFlag()
	{
		// Arrange -- ICacheable path with Distributed mode
		var middleware = CreateMiddleware(new CacheOptions
		{
			Enabled = true,
			CacheMode = CacheMode.Distributed
		});
		var message = new CacheableTestQuery();

		HybridCacheEntryOptions? capturedOptions = null;
		A.CallTo(() => _cache.GetOrCreateAsync(
			A<string>._,
			A<Func<CancellationToken, ValueTask<CachedValue>>>._,
			A<Func<Func<CancellationToken, ValueTask<CachedValue>>, CancellationToken, ValueTask<CachedValue>>>._,
			A<HybridCacheEntryOptions?>._,
			A<IEnumerable<string>?>._,
			A<CancellationToken>._))
			.Invokes(call => capturedOptions = call.GetArgument<HybridCacheEntryOptions?>(3))
			.Returns(new ValueTask<CachedValue>(new CachedValue()));

		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct) =>
			new(A.Fake<IMessageResult>());

		// Act
		await middleware.InvokeAsync(message, _context, Next, _ct);

		// Assert -- distributed mode should set DisableLocalCache
		capturedOptions.ShouldNotBeNull();
		capturedOptions.Flags.ShouldBe(HybridCacheEntryFlags.DisableLocalCache);
	}

	[Fact]
	public async Task InvokeAsync_WithICacheableHybridMode_DoesNotSetDisableLocalCacheFlag()
	{
		// Arrange -- ICacheable path with Hybrid mode
		var middleware = CreateMiddleware(new CacheOptions
		{
			Enabled = true,
			CacheMode = CacheMode.Hybrid
		});
		var message = new CacheableTestQuery();

		HybridCacheEntryOptions? capturedOptions = null;
		A.CallTo(() => _cache.GetOrCreateAsync(
			A<string>._,
			A<Func<CancellationToken, ValueTask<CachedValue>>>._,
			A<Func<Func<CancellationToken, ValueTask<CachedValue>>, CancellationToken, ValueTask<CachedValue>>>._,
			A<HybridCacheEntryOptions?>._,
			A<IEnumerable<string>?>._,
			A<CancellationToken>._))
			.Invokes(call => capturedOptions = call.GetArgument<HybridCacheEntryOptions?>(3))
			.Returns(new ValueTask<CachedValue>(new CachedValue()));

		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct) =>
			new(A.Fake<IMessageResult>());

		// Act
		await middleware.InvokeAsync(message, _context, Next, _ct);

		// Assert
		capturedOptions.ShouldNotBeNull();
		capturedOptions.Flags.ShouldBe(HybridCacheEntryFlags.None);
	}

	// -- Test helper types --

	[CacheResult(ExpirationSeconds = 60)]
	private sealed class AttributeCacheableAction : IDispatchAction<string>
	{
	}

	[CacheResult(ExpirationSeconds = 0)]
	private sealed class AttributeCacheableWithZeroExpiration : IDispatchAction<string>
	{
	}

	private sealed class CacheableTestQuery : ICacheable<string>
	{
		public string GetCacheKey() => "test-query-key";
		public string[]? GetCacheTags() => ["test-tag"];
	}

	private sealed class CacheableWithCustomExpiration : ICacheable<string>
	{
		public int ExpirationSeconds => 300;
		public string GetCacheKey() => "custom-exp-key";
	}

	private sealed class CacheableWithZeroExpiration : ICacheable<string>
	{
		public int ExpirationSeconds => 0;
		public string GetCacheKey() => "zero-exp-key";
	}

	// Simple test logger to capture log entries
	private sealed class TestLogger<T> : ILogger<T>
	{
		public List<LogEntry> LogEntries { get; } = [];

		public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

		public bool IsEnabled(LogLevel logLevel) => true;

		public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
		{
			LogEntries.Add(new LogEntry
			{
				LogLevel = logLevel,
				Message = formatter(state, exception)
			});
		}
	}

	internal sealed class LogEntry
	{
		public LogLevel LogLevel { get; set; }
		public string Message { get; set; } = "";
	}
}
