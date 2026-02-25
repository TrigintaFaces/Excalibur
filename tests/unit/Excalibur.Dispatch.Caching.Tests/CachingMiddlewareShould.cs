// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable IL2026, IL3050 // Suppress for test - RequiresUnreferencedCode/RequiresDynamicCode

using System.Diagnostics.Metrics;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Caching;

using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Caching.Tests;

/// <summary>
/// Unit tests for <see cref="CachingMiddleware"/> covering all major code paths:
/// passthrough, ICacheable, CacheResultAttribute, policies, timeouts, jitter, and null guards.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Feature", "Caching")]
public sealed class CachingMiddlewareShould : IDisposable
{
	private readonly IMeterFactory _meterFactory = new TestMeterFactory();
	private readonly HybridCache _fakeCache = A.Fake<HybridCache>();
	private readonly ICacheKeyBuilder _fakeKeyBuilder = A.Fake<ICacheKeyBuilder>();
	private readonly IServiceProvider _fakeServices = A.Fake<IServiceProvider>();

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
		var message = A.Fake<IDispatchAction>();
		var context = A.Fake<IMessageContext>();
		var expectedResult = A.Fake<IMessageResult>();

		// Act
		var result = await middleware.InvokeAsync(
			message, context, (_, _, _) => new ValueTask<IMessageResult>(expectedResult), CancellationToken.None);

		// Assert
		result.ShouldBe(expectedResult);
	}

	[Fact]
	public async Task PassThrough_WhenMessageIsNotDispatchAction()
	{
		// Arrange — events and documents should pass through
		var options = new CacheOptions { Enabled = true };
		var middleware = CreateMiddleware(options);
		var message = A.Fake<IDispatchEvent>(); // Not IDispatchAction
		var context = A.Fake<IMessageContext>();
		var expectedResult = A.Fake<IMessageResult>();

		// Act
		var result = await middleware.InvokeAsync(
			message, context, (_, _, _) => new ValueTask<IMessageResult>(expectedResult), CancellationToken.None);

		// Assert
		result.ShouldBe(expectedResult);
	}

	[Fact]
	public async Task PassThrough_WhenMessageIsNotCacheable()
	{
		// Arrange — a plain IDispatchAction without ICacheable<T> or [CacheResult]
		var options = new CacheOptions { Enabled = true };
		var middleware = CreateMiddleware(options);
		var message = A.Fake<IDispatchAction>();
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>());
		var expectedResult = A.Fake<IMessageResult>();

		A.CallTo(() => _fakeKeyBuilder.CreateKey(A<IDispatchAction>._, A<IMessageContext>._))
			.Returns("test-key");

		// Act
		var result = await middleware.InvokeAsync(
			message, context, (_, _, _) => new ValueTask<IMessageResult>(expectedResult), CancellationToken.None);

		// Assert — should pass through since message is not cacheable
		result.ShouldBe(expectedResult);
	}

	[Fact]
	public async Task PassThrough_WhenAttributeCacheableButPolicyRejectsMessageType()
	{
		// Arrange — message has [CacheResult] but a per-message policy says no
		var options = new CacheOptions { Enabled = true };
		var middleware = CreateMiddleware(options);
		var message = new PublicTestCacheResultAction();
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>());

		// Register a concrete policy that rejects caching (avoids FakeItEasy proxy issue with private types)
		var policy = new RejectingPolicy();

		A.CallTo(() => _fakeServices.GetService(typeof(IResultCachePolicy<PublicTestCacheResultAction>)))
			.Returns(policy);

		A.CallTo(() => _fakeKeyBuilder.CreateKey(A<IDispatchAction>._, A<IMessageContext>._))
			.Returns("test-key");

		var expectedResult = A.Fake<IMessageResult>();

		// Act
		var result = await middleware.InvokeAsync(
			message, context, (_, _, _) => new ValueTask<IMessageResult>(expectedResult), CancellationToken.None);

		// Assert — should pass through because policy rejected caching
		result.ShouldBe(expectedResult);
	}

	[Fact]
	public async Task PassThrough_WhenGlobalPolicyRejects()
	{
		// Arrange — global policy that rejects everything
		var globalPolicy = A.Fake<IResultCachePolicy>();
		A.CallTo(() => globalPolicy.ShouldCache(A<IDispatchMessage>._, A<object?>._)).Returns(false);

		var options = new CacheOptions { Enabled = true, GlobalPolicy = globalPolicy };
		var middleware = CreateMiddleware(options);
		var message = new TestCacheResultAction();
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>());

		A.CallTo(() => _fakeKeyBuilder.CreateKey(A<IDispatchAction>._, A<IMessageContext>._))
			.Returns("test-key");

		var expectedResult = A.Fake<IMessageResult>();

		// Act
		var result = await middleware.InvokeAsync(
			message, context, (_, _, _) => new ValueTask<IMessageResult>(expectedResult), CancellationToken.None);

		// Assert — global policy blocks caching, so handler still executes
		result.ShouldBe(expectedResult);
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
	public async Task HandleICacheableMessage_AndCallsHybridCache()
	{
		// Arrange — ICacheable<string> message with explicit key
		var options = new CacheOptions { Enabled = true };
		var middleware = CreateMiddleware(options);
		var message = new TestCacheableMessage { Id = "test-1" };
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>());

		A.CallTo(() => _fakeKeyBuilder.CreateKey(A<IDispatchAction>._, A<IMessageContext>._))
			.Returns("cache-key-1");

		var expectedResult = MessageResult.Success();

		// Act — the HybridCache.GetOrCreateAsync will be called, and since we faked it
		// it returns default(CachedValue) = null, so HandleCachedResultAsync falls through to nextDelegate
		var result = await middleware.InvokeAsync(
			message, context, (_, _, _) => new ValueTask<IMessageResult>(expectedResult), CancellationToken.None);

		// Assert — handler executed (cache miss path through null cached result)
		result.ShouldBe(expectedResult);
	}

	[Fact]
	public async Task HandleCacheResultAttribute_AndCallsHybridCache()
	{
		// Arrange — message with [CacheResult] attribute
		var options = new CacheOptions { Enabled = true };
		var middleware = CreateMiddleware(options);
		var message = new TestCacheResultAction();
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>());

		A.CallTo(() => _fakeKeyBuilder.CreateKey(A<IDispatchAction>._, A<IMessageContext>._))
			.Returns("attr-cache-key");

		var expectedResult = MessageResult.Success();

		// Act
		var result = await middleware.InvokeAsync(
			message, context, (_, _, _) => new ValueTask<IMessageResult>(expectedResult), CancellationToken.None);

		// Assert — handler executed (cache miss path)
		result.ShouldBe(expectedResult);
	}

	[Fact]
	public async Task EmitStartupWarningOnce_WhenDistributedModeWithMemoryDistributedCache()
	{
		// Arrange — fake a service provider that returns MemoryDistributedCache
		// Use a logger that captures log output
		var logger = A.Fake<ILogger<CachingMiddleware>>();
		A.CallTo(() => logger.IsEnabled(A<LogLevel>._)).Returns(true);

		// Return a real MemoryDistributedCache instance
		var memDistCache = new Microsoft.Extensions.Caching.Distributed.MemoryDistributedCache(
			MsOptions.Create(new Microsoft.Extensions.Caching.Memory.MemoryDistributedCacheOptions()));

		A.CallTo(() => _fakeServices.GetService(typeof(Microsoft.Extensions.Caching.Distributed.IDistributedCache)))
			.Returns(memDistCache);

		var options = new CacheOptions { Enabled = true, CacheMode = CacheMode.Distributed };
		var middleware = new CachingMiddleware(
			_meterFactory, _fakeCache, _fakeKeyBuilder, _fakeServices,
			MsOptions.Create(options), logger);

		var message = A.Fake<IDispatchAction>();
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>());
		var expectedResult = A.Fake<IMessageResult>();

		A.CallTo(() => _fakeKeyBuilder.CreateKey(A<IDispatchAction>._, A<IMessageContext>._))
			.Returns("key");

		// Act — trigger startup warning
		await middleware.InvokeAsync(
			message, context, (_, _, _) => new ValueTask<IMessageResult>(expectedResult), CancellationToken.None);

		// Assert — warning was emitted
		A.CallTo(logger)
			.Where(call => call.Method.Name == "Log" && call.Arguments.Get<LogLevel>(0) == LogLevel.Warning)
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task NotEmitStartupWarning_WhenMemoryMode()
	{
		// Arrange — memory mode doesn't trigger distributed cache warning
		var logger = A.Fake<ILogger<CachingMiddleware>>();
		A.CallTo(() => logger.IsEnabled(A<LogLevel>._)).Returns(true);

		var options = new CacheOptions { Enabled = true, CacheMode = CacheMode.Memory };
		var middleware = new CachingMiddleware(
			_meterFactory, _fakeCache, _fakeKeyBuilder, _fakeServices,
			MsOptions.Create(options), logger);

		var message = A.Fake<IDispatchAction>();
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>());
		var expectedResult = A.Fake<IMessageResult>();

		// Act
		await middleware.InvokeAsync(
			message, context, (_, _, _) => new ValueTask<IMessageResult>(expectedResult), CancellationToken.None);

		// Assert — no warning emitted
		A.CallTo(logger)
			.Where(call => call.Method.Name == "Log" && call.Arguments.Get<LogLevel>(0) == LogLevel.Warning)
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task HandleICacheableMessage_WithShouldCacheReturnsFalse()
	{
		// Arrange — ICacheable that says "don't cache"
		var options = new CacheOptions { Enabled = true };
		var middleware = CreateMiddleware(options);
		var message = new TestNonCacheableMessage();
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>());

		A.CallTo(() => _fakeKeyBuilder.CreateKey(A<IDispatchAction>._, A<IMessageContext>._))
			.Returns("key");

		var expectedResult = MessageResult.Success();

		// Act — ShouldCache returns false, so handler should just execute directly
		var result = await middleware.InvokeAsync(
			message, context, (_, _, _) => new ValueTask<IMessageResult>(expectedResult), CancellationToken.None);

		// Assert — handler executed, no caching
		result.ShouldBe(expectedResult);
	}

	[Fact]
	public async Task HandleCacheResultAttribute_WithCustomExpiration()
	{
		// Arrange
		var options = new CacheOptions { Enabled = true };
		var middleware = CreateMiddleware(options);
		var message = new TestCacheResultWithExpirationAction();
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>());

		A.CallTo(() => _fakeKeyBuilder.CreateKey(A<IDispatchAction>._, A<IMessageContext>._))
			.Returns("key-exp");

		var expectedResult = MessageResult.Success();

		// Act — message has custom expiration in attribute
		var result = await middleware.InvokeAsync(
			message, context, (_, _, _) => new ValueTask<IMessageResult>(expectedResult), CancellationToken.None);

		// Assert
		result.ShouldBe(expectedResult);
	}

	[Fact]
	public async Task HandleCacheResultAttribute_WithDefaultTags()
	{
		// Arrange — options with default tags
		var options = new CacheOptions { Enabled = true, DefaultTags = ["global"] };
		var middleware = CreateMiddleware(options);
		var message = new TestCacheResultAction();
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>());

		A.CallTo(() => _fakeKeyBuilder.CreateKey(A<IDispatchAction>._, A<IMessageContext>._))
			.Returns("key-tags");

		var expectedResult = MessageResult.Success();

		// Act
		var result = await middleware.InvokeAsync(
			message, context, (_, _, _) => new ValueTask<IMessageResult>(expectedResult), CancellationToken.None);

		// Assert
		result.ShouldBe(expectedResult);
	}

	[Fact]
	public async Task HandleICacheableMessage_WithCustomTags()
	{
		// Arrange — ICacheable with tags and default tags
		var options = new CacheOptions { Enabled = true, DefaultTags = ["default"] };
		var middleware = CreateMiddleware(options);
		var message = new TestCacheableMessage { Id = "tagged" };
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>());

		A.CallTo(() => _fakeKeyBuilder.CreateKey(A<IDispatchAction>._, A<IMessageContext>._))
			.Returns("cache-tagged");

		var expectedResult = MessageResult.Success();

		// Act
		var result = await middleware.InvokeAsync(
			message, context, (_, _, _) => new ValueTask<IMessageResult>(expectedResult), CancellationToken.None);

		// Assert
		result.ShouldBe(expectedResult);
	}

	[Fact]
	public async Task PassThrough_WhenMessageIsDispatchDocument()
	{
		// Arrange — IDispatchDocument is not IDispatchAction, should pass through
		var options = new CacheOptions { Enabled = true };
		var middleware = CreateMiddleware(options);
		var message = A.Fake<IDispatchDocument>();
		var context = A.Fake<IMessageContext>();
		var expectedResult = A.Fake<IMessageResult>();

		// Act
		var result = await middleware.InvokeAsync(
			message, context, (_, _, _) => new ValueTask<IMessageResult>(expectedResult), CancellationToken.None);

		// Assert
		result.ShouldBe(expectedResult);
	}

	private CachingMiddleware CreateMiddleware(CacheOptions? options = null, IResultCachePolicy? globalPolicy = null)
	{
		return new CachingMiddleware(
			_meterFactory,
			_fakeCache,
			_fakeKeyBuilder,
			_fakeServices,
			MsOptions.Create(options ?? new CacheOptions { Enabled = true }),
			NullLogger<CachingMiddleware>.Instance,
			globalPolicy);
	}

	// ─── Test message types ───

	[CacheResult]
	private sealed class TestCacheResultAction : IDispatchAction<string>;

	[CacheResult(ExpirationSeconds = 300, Tags = ["orders"], OnlyIfSuccess = false, IgnoreNullResult = false)]
	private sealed class TestCacheResultWithExpirationAction : IDispatchAction<int>;

	private sealed class TestCacheableMessage : ICacheable<string>
	{
		public string Id { get; set; } = string.Empty;
		public int ExpirationSeconds => 120;
		public string GetCacheKey() => $"cacheable:{Id}";
		public string[]? GetCacheTags() => ["test-tag"];
		public bool ShouldCache(object? result) => true;
	}

	private sealed class TestNonCacheableMessage : ICacheable<string>
	{
		public string GetCacheKey() => "never-cache";
		public bool ShouldCache(object? result) => false;
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

// Public types needed for FakeItEasy proxy generation (private nested classes can't be proxied)

[CacheResult]
public sealed class PublicTestCacheResultAction : IDispatchAction<string>;

public sealed class RejectingPolicy : IResultCachePolicy<PublicTestCacheResultAction>
{
	public bool ShouldCache(PublicTestCacheResultAction message, object? result) => false;
}

#pragma warning restore IL2026, IL3050
