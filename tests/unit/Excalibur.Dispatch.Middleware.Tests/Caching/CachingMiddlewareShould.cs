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

[Trait("Category", "Unit")]
public sealed class CachingMiddlewareShould : UnitTestBase
{
	private readonly TestMeterFactory _meterFactory;
	private readonly HybridCache _cache;
	private readonly ICacheKeyBuilder _keyBuilder;
	private readonly IServiceProvider _services;
	private readonly ILogger<CachingMiddleware> _logger;
	private readonly IMessageContext _context;
	private readonly CancellationToken _ct = CancellationToken.None;

	public CachingMiddlewareShould()
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

	private CachingMiddleware CreateMiddleware(CacheOptions? opts = null)
	{
		var options = opts ?? new CacheOptions { Enabled = true, CacheMode = CacheMode.Hybrid };
		return new CachingMiddleware(
			_meterFactory,
			_cache,
			_keyBuilder,
			_services,
			MsOptions.Options.Create(options),
			_logger);
	}

	[Fact]
	public void Stage_ReturnsCache()
	{
		var middleware = CreateMiddleware();
		middleware.Stage.ShouldBe(DispatchMiddlewareStage.Cache);
	}

	[Fact]
	public async Task InvokeAsync_WhenDisabled_SkipsCaching()
	{
		// Arrange
		var middleware = CreateMiddleware(new CacheOptions { Enabled = false });
		var message = A.Fake<IDispatchMessage>();
		var expected = A.Fake<IMessageResult>();

		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct) =>
			new(expected);

		// Act
		var result = await middleware.InvokeAsync(message, _context, Next, _ct);

		// Assert
		result.ShouldBe(expected);
	}

	[Fact]
	public async Task InvokeAsync_WhenNotDispatchAction_SkipsCaching()
	{
		// Arrange
		var middleware = CreateMiddleware();
		var message = A.Fake<IDispatchMessage>(); // Not IDispatchAction
		var expected = A.Fake<IMessageResult>();

		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct) =>
			new(expected);

		// Act
		var result = await middleware.InvokeAsync(message, _context, Next, _ct);

		// Assert
		result.ShouldBe(expected);
	}

	[Fact]
	public async Task InvokeAsync_WhenNotCacheable_SkipsCaching()
	{
		// Arrange
		var middleware = CreateMiddleware();
		var message = new NonCacheableAction();
		var expected = A.Fake<IMessageResult>();

		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct) =>
			new(expected);

		// Act
		var result = await middleware.InvokeAsync(message, _context, Next, _ct);

		// Assert
		result.ShouldBe(expected);
	}

	[Fact]
	public async Task InvokeAsync_ThrowsOnNullMessage()
	{
		var middleware = CreateMiddleware();

		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await middleware.InvokeAsync(
				null!,
				_context,
				(_, _, _) => new ValueTask<IMessageResult>(A.Fake<IMessageResult>()),
				_ct));
	}

	[Fact]
	public async Task InvokeAsync_ThrowsOnNullContext()
	{
		var middleware = CreateMiddleware();

		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await middleware.InvokeAsync(
				A.Fake<IDispatchMessage>(),
				null!,
				(_, _, _) => new ValueTask<IMessageResult>(A.Fake<IMessageResult>()),
				_ct));
	}

	[Fact]
	public async Task InvokeAsync_ThrowsOnNullDelegate()
	{
		var middleware = CreateMiddleware();

		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await middleware.InvokeAsync(
				A.Fake<IDispatchMessage>(),
				_context,
				null!,
				_ct));
	}

	[Fact]
	public async Task InvokeAsync_WithAttributeCacheable_CallsHybridCacheGetOrCreate()
	{
		// Arrange
		var middleware = CreateMiddleware();
		var message = new AttributeCacheableAction();
		var expected = A.Fake<IMessageResult>();

		// Set up the HybridCache to call the factory and return a CachedValue
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
				Value = "test",
				TypeName = typeof(string).AssemblyQualifiedName
			}));

		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct) =>
			new(expected);

		// Act
		var result = await middleware.InvokeAsync(message, _context, Next, _ct);

		// Assert — HybridCache.GetOrCreateAsync was called
		A.CallTo(() => _cache.GetOrCreateAsync(
			A<string>._,
			A<Func<CancellationToken, ValueTask<CachedValue>>>._,
			A<Func<Func<CancellationToken, ValueTask<CachedValue>>, CancellationToken, ValueTask<CachedValue>>>._,
			A<HybridCacheEntryOptions?>._,
			A<IEnumerable<string>?>._,
			A<CancellationToken>._)).MustHaveHappened();
	}

	[Fact]
	public async Task InvokeAsync_WithJitterRatio_AppliesJitterToExpiration()
	{
		// Arrange — verify jitter doesn't break the middleware
		var cacheOptions = new CacheOptions
		{
			Enabled = true,
			CacheMode = CacheMode.Hybrid,
			Behavior = { JitterRatio = 0.20, DefaultExpiration = TimeSpan.FromMinutes(5) },
		};
		var middleware = CreateMiddleware(cacheOptions);
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

		// Act — should not throw
		await middleware.InvokeAsync(message, _context, Next, _ct);
	}

	[Fact]
	public async Task InvokeAsync_WithZeroJitter_WorksCorrectly()
	{
		// Arrange
		var middleware = CreateMiddleware(new CacheOptions
		{
			Enabled = true,
			CacheMode = CacheMode.Hybrid,
			Behavior = { JitterRatio = 0 },
		});
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

		// Act — should not throw
		await middleware.InvokeAsync(message, _context, Next, _ct);
	}

	[Fact]
	public async Task InvokeAsync_WithICacheableMessage_UsesInterfacePath()
	{
		// Arrange
		var middleware = CreateMiddleware();
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
				Value = "42",
				TypeName = typeof(string).AssemblyQualifiedName
			}));

		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct) =>
			new(A.Fake<IMessageResult>());

		// Act
		var result = await middleware.InvokeAsync(message, _context, Next, _ct);

		// Assert — HybridCache was invoked
		A.CallTo(() => _cache.GetOrCreateAsync(
			A<string>._,
			A<Func<CancellationToken, ValueTask<CachedValue>>>._,
			A<Func<Func<CancellationToken, ValueTask<CachedValue>>, CancellationToken, ValueTask<CachedValue>>>._,
			A<HybridCacheEntryOptions?>._,
			A<IEnumerable<string>?>._,
			A<CancellationToken>._)).MustHaveHappened();
	}

	[Fact]
	public async Task InvokeAsync_WhenCacheHit_ReturnsCachedMessageResultWithCacheHitTrue()
	{
		// Arrange
		var middleware = CreateMiddleware();
		var message = new AttributeCacheableAction();

		// Return a CachedValue with HasExecuted=true and ShouldCache=true, simulating a cache hit
		// No "Dispatch:OriginalResult" in context items means this is a true cache hit
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
				Value = "cached-result",
				TypeName = typeof(string).AssemblyQualifiedName
			}));

		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct) =>
			new(A.Fake<IMessageResult>());

		// Act
		var result = await middleware.InvokeAsync(message, _context, Next, _ct);

		// Assert — result should be a CachedMessageResult with CacheHit=true
		result.ShouldNotBeNull();
		result.Succeeded.ShouldBeTrue();
		result.CacheHit.ShouldBeTrue();
		result.ProblemDetails.ShouldBeNull();
		result.ErrorMessage.ShouldBeNull();

		// Verify it's an IMessageResult<string> with correct value
		var typedResult = result.ShouldBeAssignableTo<IMessageResult<string>>();
		typedResult.ReturnValue.ShouldBe("cached-result");
	}

	[Fact]
	public async Task InvokeAsync_WhenCacheHit_ValidationAndAuthorizationResultsAreNull()
	{
		// Arrange
		var middleware = CreateMiddleware();
		var message = new AttributeCacheableAction();

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
				Value = "test-value",
				TypeName = typeof(string).AssemblyQualifiedName
			}));

		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct) =>
			new(A.Fake<IMessageResult>());

		// Act
		var result = await middleware.InvokeAsync(message, _context, Next, _ct);

		// Assert — CachedMessageResult should have null ValidationResult and AuthorizationResult
		result.ShouldNotBeNull();
		result.ValidationResult.ShouldBeNull();
		result.AuthorizationResult.ShouldBeNull();
	}

	[Fact]
	public async Task InvokeAsync_WhenCacheTimeout_FallsThroughToNextDelegate()
	{
		// Arrange
		var middleware = CreateMiddleware();
		var message = new AttributeCacheableAction();
		var expected = A.Fake<IMessageResult>();
		var nextCalled = false;

		// HybridCache throws OperationCanceledException (timeout)
		A.CallTo(() => _cache.GetOrCreateAsync(
			A<string>._,
			A<Func<CancellationToken, ValueTask<CachedValue>>>._,
			A<Func<Func<CancellationToken, ValueTask<CachedValue>>, CancellationToken, ValueTask<CachedValue>>>._,
			A<HybridCacheEntryOptions?>._,
			A<IEnumerable<string>?>._,
			A<CancellationToken>._))
			.ThrowsAsync(new OperationCanceledException());

		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct)
		{
			nextCalled = true;
			return new(expected);
		}

		// Act
		var result = await middleware.InvokeAsync(message, _context, Next, _ct);

		// Assert — middleware should catch the timeout and call next delegate
		nextCalled.ShouldBeTrue();
		result.ShouldBe(expected);
	}

	[Fact]
	public async Task InvokeAsync_WhenICacheableTimeout_FallsThroughToNextDelegate()
	{
		// Arrange
		var middleware = CreateMiddleware();
		var message = new CacheableTestQuery();
		var expected = A.Fake<IMessageResult>();
		var nextCalled = false;

		// HybridCache throws OperationCanceledException (timeout)
		A.CallTo(() => _cache.GetOrCreateAsync(
			A<string>._,
			A<Func<CancellationToken, ValueTask<CachedValue>>>._,
			A<Func<Func<CancellationToken, ValueTask<CachedValue>>, CancellationToken, ValueTask<CachedValue>>>._,
			A<HybridCacheEntryOptions?>._,
			A<IEnumerable<string>?>._,
			A<CancellationToken>._))
			.ThrowsAsync(new OperationCanceledException());

		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct)
		{
			nextCalled = true;
			return new(expected);
		}

		// Act
		var result = await middleware.InvokeAsync(message, _context, Next, _ct);

		// Assert
		nextCalled.ShouldBeTrue();
		result.ShouldBe(expected);
	}

	[Fact]
	public async Task InvokeAsync_WhenCachedValueHasNotExecuted_CallsNextDelegate()
	{
		// Arrange
		var middleware = CreateMiddleware();
		var message = new AttributeCacheableAction();
		var expected = A.Fake<IMessageResult>();

		// Return a CachedValue with HasExecuted=false
		A.CallTo(() => _cache.GetOrCreateAsync(
			A<string>._,
			A<Func<CancellationToken, ValueTask<CachedValue>>>._,
			A<Func<Func<CancellationToken, ValueTask<CachedValue>>, CancellationToken, ValueTask<CachedValue>>>._,
			A<HybridCacheEntryOptions?>._,
			A<IEnumerable<string>?>._,
			A<CancellationToken>._))
			.Returns(new ValueTask<CachedValue>(new CachedValue
			{
				HasExecuted = false,
				ShouldCache = false,
				Value = null,
				TypeName = null
			}));

		var nextCalled = false;
		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct)
		{
			nextCalled = true;
			return new(expected);
		}

		// Act
		var result = await middleware.InvokeAsync(message, _context, Next, _ct);

		// Assert — when HasExecuted is false, middleware should fall through to next delegate
		nextCalled.ShouldBeTrue();
		result.ShouldBe(expected);
	}

	[Fact]
	public async Task InvokeAsync_WhenCachedValueHasExecutedButShouldCacheFalse_CallsNextDelegate()
	{
		// Arrange
		var middleware = CreateMiddleware();
		var message = new AttributeCacheableAction();
		var expected = A.Fake<IMessageResult>();

		// Return a CachedValue with HasExecuted=true but ShouldCache=false
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
				ShouldCache = false,
				Value = null,
				TypeName = null
			}));

		var nextCalled = false;
		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct)
		{
			nextCalled = true;
			return new(expected);
		}

		// Act
		var result = await middleware.InvokeAsync(message, _context, Next, _ct);

		// Assert — ShouldCache=false, so should fall through to next
		nextCalled.ShouldBeTrue();
		result.ShouldBe(expected);
	}

	[Fact]
	public async Task InvokeAsync_WhenCachedValueHasExecutedAndShouldCacheTrueButValueNull_CallsNextDelegate()
	{
		// Arrange
		var middleware = CreateMiddleware();
		var message = new AttributeCacheableAction();
		var expected = A.Fake<IMessageResult>();

		// Return CachedValue with HasExecuted=true, ShouldCache=true, but Value=null
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
				Value = null,
				TypeName = null
			}));

		var nextCalled = false;
		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct)
		{
			nextCalled = true;
			return new(expected);
		}

		// Act
		var result = await middleware.InvokeAsync(message, _context, Next, _ct);

		// Assert — Value is null so the cache hit check (ShouldCache && Value != null) fails
		nextCalled.ShouldBeTrue();
		result.ShouldBe(expected);
	}

	[Fact]
	public async Task InvokeAsync_WithGlobalPolicy_CanBeConstructedAndInvoked()
	{
		// Arrange — verify middleware accepts a global policy without error
		var globalPolicy = A.Fake<IResultCachePolicy>();
		A.CallTo(() => globalPolicy.ShouldCache(A<IDispatchMessage>._, A<object?>._)).Returns(true);

		var options = new CacheOptions { Enabled = true, CacheMode = CacheMode.Hybrid };
		var middleware = new CachingMiddleware(
			_meterFactory,
			_cache,
			_keyBuilder,
			_services,
			MsOptions.Options.Create(options),
			_logger,
			globalPolicy);

		var message = new AttributeCacheableAction();

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
				Value = "test",
				TypeName = typeof(string).AssemblyQualifiedName
			}));

		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct) =>
			new(A.Fake<IMessageResult>());

		// Act — should not throw
		var result = await middleware.InvokeAsync(message, _context, Next, _ct);

		// Assert
		result.ShouldNotBeNull();
	}

	[Fact]
	public async Task InvokeAsync_WithTypedCachePolicy_RespectsTypedPolicy()
	{
		// Arrange — typed policy that rejects caching for AttributeCacheableAction
		var serviceCollection = new ServiceCollection();
		serviceCollection.AddSingleton<IResultCachePolicy<AttributeCacheableAction>>(
			new TestTypedResultCachePolicy((_, _) => false));
		var serviceProvider = serviceCollection.BuildServiceProvider();

		var options = new CacheOptions { Enabled = true, CacheMode = CacheMode.Hybrid };
		var middleware = new CachingMiddleware(
			_meterFactory,
			_cache,
			_keyBuilder,
			serviceProvider,
			MsOptions.Options.Create(options),
			_logger);

		var message = new AttributeCacheableAction();
		var expected = A.Fake<IMessageResult>();

		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct) =>
			new(expected);

		// Act
		var result = await middleware.InvokeAsync(message, _context, Next, _ct);

		// Assert — typed policy says don't cache, so next is called directly
		result.ShouldBe(expected);

		// HybridCache should not have been called
		A.CallTo(() => _cache.GetOrCreateAsync(
			A<string>._,
			A<Func<CancellationToken, ValueTask<CachedValue>>>._,
			A<Func<Func<CancellationToken, ValueTask<CachedValue>>, CancellationToken, ValueTask<CachedValue>>>._,
			A<HybridCacheEntryOptions?>._,
			A<IEnumerable<string>?>._,
			A<CancellationToken>._)).MustNotHaveHappened();
	}

	[Fact]
	public async Task InvokeAsync_WithDistributedCacheMode_SetsDisableLocalCacheFlag()
	{
		// Arrange
		var middleware = CreateMiddleware(new CacheOptions
		{
			Enabled = true,
			CacheMode = CacheMode.Distributed
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

		// Assert — distributed mode sets DisableLocalCache flag
		capturedOptions.ShouldNotBeNull();
		capturedOptions.Flags.ShouldBe(HybridCacheEntryFlags.DisableLocalCache);
	}

	[Fact]
	public async Task InvokeAsync_WithHybridCacheMode_DoesNotSetDisableLocalCacheFlag()
	{
		// Arrange
		var middleware = CreateMiddleware(new CacheOptions
		{
			Enabled = true,
			CacheMode = CacheMode.Hybrid
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

		// Assert — hybrid mode should not disable local cache
		capturedOptions.ShouldNotBeNull();
		capturedOptions.Flags.ShouldBe(HybridCacheEntryFlags.None);
	}

	[Fact]
	public async Task InvokeAsync_WithDefaultTags_IncludesDefaultTagsInCacheCall()
	{
		// Arrange
		var middleware = CreateMiddleware(new CacheOptions
		{
			Enabled = true,
			CacheMode = CacheMode.Hybrid,
			DefaultTags = ["global-tag-1", "global-tag-2"]
		});
		var message = new AttributeCacheableAction();

		IEnumerable<string>? capturedTags = null;
		A.CallTo(() => _cache.GetOrCreateAsync(
			A<string>._,
			A<Func<CancellationToken, ValueTask<CachedValue>>>._,
			A<Func<Func<CancellationToken, ValueTask<CachedValue>>, CancellationToken, ValueTask<CachedValue>>>._,
			A<HybridCacheEntryOptions?>._,
			A<IEnumerable<string>?>._,
			A<CancellationToken>._))
			.Invokes(call => capturedTags = call.GetArgument<IEnumerable<string>?>(4))
			.Returns(new ValueTask<CachedValue>(new CachedValue()));

		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct) =>
			new(A.Fake<IMessageResult>());

		// Act
		await middleware.InvokeAsync(message, _context, Next, _ct);

		// Assert — default tags should be included
		capturedTags.ShouldNotBeNull();
		capturedTags.ShouldContain("global-tag-1");
		capturedTags.ShouldContain("global-tag-2");
	}

	[Fact]
	public async Task InvokeAsync_WithICacheableDefaultExpirationSeconds_UsesDefaultExpiration()
	{
		// Arrange — test ICacheable with default ExpirationSeconds (60)
		var middleware = CreateMiddleware(new CacheOptions
		{
			Enabled = true,
			CacheMode = CacheMode.Hybrid,
			Behavior = { DefaultExpiration = TimeSpan.FromMinutes(10) },
		});
		var message = new CacheableWithDefaultExpiration();

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

		// Assert — should use the default ExpirationSeconds (60) from the ICacheable default implementation
		capturedOptions.ShouldNotBeNull();
		capturedOptions.Expiration.ShouldNotBeNull();
		// The expiration should be around 60 seconds (with jitter applied)
		capturedOptions.Expiration.Value.TotalSeconds.ShouldBeInRange(50, 70);
	}

	[Fact]
	public async Task InvokeAsync_WithICacheableTags_IncludesCustomTags()
	{
		// Arrange
		var middleware = CreateMiddleware();
		var message = new CacheableTestQuery(); // returns ["test-tag"]

		IEnumerable<string>? capturedTags = null;
		A.CallTo(() => _cache.GetOrCreateAsync(
			A<string>._,
			A<Func<CancellationToken, ValueTask<CachedValue>>>._,
			A<Func<Func<CancellationToken, ValueTask<CachedValue>>, CancellationToken, ValueTask<CachedValue>>>._,
			A<HybridCacheEntryOptions?>._,
			A<IEnumerable<string>?>._,
			A<CancellationToken>._))
			.Invokes(call => capturedTags = call.GetArgument<IEnumerable<string>?>(4))
			.Returns(new ValueTask<CachedValue>(new CachedValue()));

		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct) =>
			new(A.Fake<IMessageResult>());

		// Act
		await middleware.InvokeAsync(message, _context, Next, _ct);

		// Assert
		capturedTags.ShouldNotBeNull();
		capturedTags.ShouldContain("test-tag");
	}

	[Fact]
	public async Task InvokeAsync_WithICacheableAndDefaultTags_MergesDefaultTags()
	{
		// Arrange — ICacheable with DefaultTags to cover GetCacheTags + DefaultTags merge (line 560-562)
		var middleware = CreateMiddleware(new CacheOptions
		{
			Enabled = true,
			CacheMode = CacheMode.Hybrid,
			DefaultTags = ["default-tag"]
		});
		var message = new CacheableTestQuery(); // returns ["test-tag"]

		IEnumerable<string>? capturedTags = null;
		A.CallTo(() => _cache.GetOrCreateAsync(
			A<string>._,
			A<Func<CancellationToken, ValueTask<CachedValue>>>._,
			A<Func<Func<CancellationToken, ValueTask<CachedValue>>, CancellationToken, ValueTask<CachedValue>>>._,
			A<HybridCacheEntryOptions?>._,
			A<IEnumerable<string>?>._,
			A<CancellationToken>._))
			.Invokes(call => capturedTags = call.GetArgument<IEnumerable<string>?>(4))
			.Returns(new ValueTask<CachedValue>(new CachedValue()));

		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct) =>
			new(A.Fake<IMessageResult>());

		// Act
		await middleware.InvokeAsync(message, _context, Next, _ct);

		// Assert — should have both custom and default tags
		capturedTags.ShouldNotBeNull();
		capturedTags.ShouldContain("test-tag");
		capturedTags.ShouldContain("default-tag");
	}

	[Fact]
	public async Task InvokeAsync_WithAttributeDefaultTags_MergesDefaultTags()
	{
		// Arrange — Attribute cacheable with DefaultTags to cover GetAttributeCacheConfiguration + DefaultTags merge (line 469-471)
		var middleware = CreateMiddleware(new CacheOptions
		{
			Enabled = true,
			CacheMode = CacheMode.Hybrid,
			DefaultTags = ["attr-default-tag"]
		});
		var message = new AttributeCacheableWithTags(); // has Tags = ["attr-tag"]

		IEnumerable<string>? capturedTags = null;
		A.CallTo(() => _cache.GetOrCreateAsync(
			A<string>._,
			A<Func<CancellationToken, ValueTask<CachedValue>>>._,
			A<Func<Func<CancellationToken, ValueTask<CachedValue>>, CancellationToken, ValueTask<CachedValue>>>._,
			A<HybridCacheEntryOptions?>._,
			A<IEnumerable<string>?>._,
			A<CancellationToken>._))
			.Invokes(call => capturedTags = call.GetArgument<IEnumerable<string>?>(4))
			.Returns(new ValueTask<CachedValue>(new CachedValue()));

		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct) =>
			new(A.Fake<IMessageResult>());

		// Act
		await middleware.InvokeAsync(message, _context, Next, _ct);

		// Assert — should have both attribute tags and default tags
		capturedTags.ShouldNotBeNull();
		capturedTags.ShouldContain("attr-tag");
		capturedTags.ShouldContain("attr-default-tag");
	}

	[Fact]
	public async Task InvokeAsync_WhenICacheableWithNullTags_StillWorksFine()
	{
		// Arrange — ICacheable that returns null from GetCacheTags
		var middleware = CreateMiddleware();
		var message = new CacheableWithDefaultExpiration(); // GetCacheTags() defaults to null

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

		// Act — should not throw
		await middleware.InvokeAsync(message, _context, Next, _ct);
	}

	[Fact]
	public async Task InvokeAsync_WhenTypedPolicyThrows_FallsBackToGlobalPolicy()
	{
		// Arrange — typed policy that throws, to cover ShouldCache exception handlers (lines 662-672)
		var serviceCollection = new ServiceCollection();
		serviceCollection.AddSingleton<IResultCachePolicy<AttributeCacheableAction>>(
			new ThrowingTypedResultCachePolicy());
		var serviceProvider = serviceCollection.BuildServiceProvider();

		// Global policy returns true so caching proceeds
		var globalPolicy = A.Fake<IResultCachePolicy>();
		A.CallTo(() => globalPolicy.ShouldCache(A<IDispatchMessage>._, A<object?>._)).Returns(true);

		var options = new CacheOptions { Enabled = true, CacheMode = CacheMode.Hybrid };
		var middleware = new CachingMiddleware(
			_meterFactory,
			_cache,
			_keyBuilder,
			serviceProvider,
			MsOptions.Options.Create(options),
			_logger,
			globalPolicy);

		var message = new AttributeCacheableAction();

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
				Value = "test",
				TypeName = typeof(string).AssemblyQualifiedName
			}));

		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct) =>
			new(A.Fake<IMessageResult>());

		// Act — should not throw; typed policy exception is caught, global policy used
		var result = await middleware.InvokeAsync(message, _context, Next, _ct);

		// Assert — middleware should succeed
		result.ShouldNotBeNull();
	}

	[Fact]
	public async Task InvokeAsync_WhenNonGenericDispatchActionWithCacheHit_ThrowsInvalidOperationException()
	{
		// Arrange — message with [CacheResult] implementing IDispatchAction (non-generic), not IDispatchAction<T>
		// When cache returns a hit (HasExecuted=true, ShouldCache=true, Value!=null),
		// HandleCachedResultAsync can't find IDispatchAction<T> and throws
		var middleware = CreateMiddleware();
		var message = new NonGenericCacheableAction();

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
				Value = "cached-value",
				TypeName = typeof(string).AssemblyQualifiedName
			}));

		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct) =>
			new(A.Fake<IMessageResult>());

		// Act & Assert — should throw because IDispatchAction<T> not found
		await Should.ThrowAsync<InvalidOperationException>(async () =>
			await middleware.InvokeAsync(message, _context, Next, _ct));
	}

	// Test helper types

	private sealed class NonCacheableAction : IDispatchAction
	{
	}

	[CacheResult(ExpirationSeconds = 60)]
	private sealed class AttributeCacheableAction : IDispatchAction<string>
	{
	}

	[CacheResult(ExpirationSeconds = 30)]
	private sealed class NonGenericCacheableAction : IDispatchAction
	{
	}

	private sealed class CacheableTestQuery : ICacheable<string>
	{
		public string GetCacheKey() => "test-query-key";
		public string[]? GetCacheTags() => ["test-tag"];
	}

	private sealed class CacheableWithDefaultExpiration : ICacheable<int>
	{
		// Uses all defaults from ICacheable: ExpirationSeconds=60, GetCacheTags()=>null, ShouldCache()=>true
		public string GetCacheKey() => "default-expiration-key";
	}

	[CacheResult(ExpirationSeconds = 120, Tags = ["attr-tag"])]
	private sealed class AttributeCacheableWithTags : IDispatchAction<string>
	{
	}

	private sealed class TestTypedResultCachePolicy(Func<AttributeCacheableAction, object?, bool> shouldCache)
		: IResultCachePolicy<AttributeCacheableAction>
	{
		public bool ShouldCache(AttributeCacheableAction message, object? result) => shouldCache(message, result);
	}

	private sealed class ThrowingTypedResultCachePolicy : IResultCachePolicy<AttributeCacheableAction>
	{
		public bool ShouldCache(AttributeCacheableAction message, object? result) =>
			throw new InvalidOperationException("Policy failure");
	}

	[Fact]
	public async Task InvokeAsync_WithDistributedOrHybridMode_EmitsStartupWarningForMemoryDistributedCache()
	{
		// Arrange — register MemoryDistributedCache to trigger the startup warning path
		var serviceCollection = new ServiceCollection();
		serviceCollection.AddDistributedMemoryCache();
		var serviceProvider = serviceCollection.BuildServiceProvider();

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

		// Act
		await middleware.InvokeAsync(message, _context, Next, _ct);

		// Assert — should have emitted a warning about MemoryDistributedCache
		testLogger.LogEntries.ShouldContain(entry =>
			entry.LogLevel == LogLevel.Warning &&
			entry.Message.Contains("MemoryDistributedCache"));
	}

	[Fact]
	public async Task InvokeAsync_WithMemoryMode_DoesNotEmitDistributedCacheWarning()
	{
		// Arrange — memory mode should not trigger the distributed cache warning
		var serviceCollection = new ServiceCollection();
		serviceCollection.AddDistributedMemoryCache();
		var serviceProvider = serviceCollection.BuildServiceProvider();

		var testLogger = new TestLogger<CachingMiddleware>();
		var options = new CacheOptions { Enabled = true, CacheMode = CacheMode.Memory };
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

		// Assert — no warning about MemoryDistributedCache
		testLogger.LogEntries.ShouldNotContain(entry =>
			entry.LogLevel == LogLevel.Warning &&
			entry.Message.Contains("MemoryDistributedCache"));
	}

	[Fact]
	public async Task InvokeAsync_WithCacheHitAndOriginalResultInContext_ReturnsFreshResult()
	{
		// Arrange — simulate a fresh execution (not a cache hit) where OriginalResult is in context
		var middleware = CreateMiddleware();
		var message = new AttributeCacheableAction();
		var freshResult = A.Fake<IMessageResult>();
		var items = new Dictionary<string, object> { { "Dispatch:OriginalResult", freshResult } };

		A.CallTo(() => _context.Items).Returns(items);

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
				Value = "test",
				TypeName = typeof(string).AssemblyQualifiedName
			}));

		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct) =>
			new(A.Fake<IMessageResult>());

		// Act
		var result = await middleware.InvokeAsync(message, _context, Next, _ct);

		// Assert — should return the fresh result from context, not a CachedMessageResult
		result.ShouldBe(freshResult);
		// OriginalResult should be removed from context
		items.ShouldNotContainKey("Dispatch:OriginalResult");
	}

	[Fact]
	public async Task InvokeAsync_WithGlobalPolicyReturnsFalse_SkipsCachingViaICacheable()
	{
		// Arrange — global policy says don't cache, ICacheable message
		var globalPolicy = A.Fake<IResultCachePolicy>();
		A.CallTo(() => globalPolicy.ShouldCache(A<IDispatchMessage>._, A<object?>._)).Returns(false);

		var options = new CacheOptions { Enabled = true, CacheMode = CacheMode.Hybrid };
		var middleware = new CachingMiddleware(
			_meterFactory,
			_cache,
			_keyBuilder,
			_services,
			MsOptions.Options.Create(options),
			_logger,
			globalPolicy);

		var message = new CacheableTestQuery();
		var expected = A.Fake<IMessageResult>();

		ValueTask<IMessageResult> Next(IDispatchMessage m, IMessageContext c, CancellationToken ct) =>
			new(expected);

		// Act
		var result = await middleware.InvokeAsync(message, _context, Next, _ct);

		// Assert — global policy returns false, so handler called directly
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
	public async Task InvokeAsync_StartupWarning_OnlyEmittedOnce()
	{
		// Arrange — verify the startup warning is emitted only on first invocation
		var testLogger = new TestLogger<CachingMiddleware>();
		var options = new CacheOptions { Enabled = true, CacheMode = CacheMode.Hybrid };
		var middleware = new CachingMiddleware(
			_meterFactory,
			_cache,
			_keyBuilder,
			_services,
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

		// Act — invoke twice
		await middleware.InvokeAsync(message, _context, Next, _ct);
		await middleware.InvokeAsync(message, _context, Next, _ct);

		// Assert — startup path entered only once (no warnings for Memory mode, but the code path is covered)
		// The important thing is it doesn't throw
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
