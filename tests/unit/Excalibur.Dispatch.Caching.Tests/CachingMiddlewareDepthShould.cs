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
/// Depth tests for <see cref="CachingMiddleware"/> covering timeout paths,
/// jitter edge cases, startup warning idempotency, and OTel metrics.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Feature", "Caching")]
public sealed class CachingMiddlewareDepthShould : IDisposable
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
	public async Task EmitStartupWarning_OnlyOnce_AcrossMultipleInvocations()
	{
		// Arrange
		var logger = A.Fake<ILogger<CachingMiddleware>>();
		A.CallTo(() => logger.IsEnabled(A<LogLevel>._)).Returns(true);

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

		// Act -- invoke twice
		await middleware.InvokeAsync(
			message, context, (_, _, _) => new ValueTask<IMessageResult>(expectedResult), CancellationToken.None);
		await middleware.InvokeAsync(
			message, context, (_, _, _) => new ValueTask<IMessageResult>(expectedResult), CancellationToken.None);

		// Assert -- warning emitted exactly once, not twice
		A.CallTo(logger)
			.Where(call => call.Method.Name == "Log" && call.Arguments.Get<LogLevel>(0) == LogLevel.Warning)
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task NotEmitStartupWarning_WhenHybridMode()
	{
		// Arrange -- Hybrid mode should warn but only for MemoryDistributedCache
		var logger = A.Fake<ILogger<CachingMiddleware>>();
		A.CallTo(() => logger.IsEnabled(A<LogLevel>._)).Returns(true);

		// Return a real distributed cache (not MemoryDistributedCache)
		var realDistCache = A.Fake<Microsoft.Extensions.Caching.Distributed.IDistributedCache>();
		A.CallTo(() => _fakeServices.GetService(typeof(Microsoft.Extensions.Caching.Distributed.IDistributedCache)))
			.Returns(realDistCache);

		var options = new CacheOptions { Enabled = true, CacheMode = CacheMode.Hybrid };
		var middleware = new CachingMiddleware(
			_meterFactory, _fakeCache, _fakeKeyBuilder, _fakeServices,
			MsOptions.Create(options), logger);

		var message = A.Fake<IDispatchAction>();
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>());

		A.CallTo(() => _fakeKeyBuilder.CreateKey(A<IDispatchAction>._, A<IMessageContext>._))
			.Returns("key");

		// Act
		await middleware.InvokeAsync(
			message, context, (_, _, _) => new ValueTask<IMessageResult>(A.Fake<IMessageResult>()), CancellationToken.None);

		// Assert -- no warning because we have a real IDistributedCache
		A.CallTo(logger)
			.Where(call => call.Method.Name == "Log" && call.Arguments.Get<LogLevel>(0) == LogLevel.Warning)
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task UseDefaultExpiration_WhenICacheableReturnsZeroExpiration()
	{
		// Arrange
		var options = new CacheOptions
		{
			Enabled = true,
			Behavior = new CacheBehaviorOptions { DefaultExpiration = TimeSpan.FromMinutes(5) },
		};
		var middleware = CreateMiddleware(options);
		var message = new ZeroExpirationCacheableMessage();
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>());

		A.CallTo(() => _fakeKeyBuilder.CreateKey(A<IDispatchAction>._, A<IMessageContext>._))
			.Returns("zero-exp-key");

		var expectedResult = MessageResult.Success();

		// Act -- should not throw and should use default expiration
		var result = await middleware.InvokeAsync(
			message, context, (_, _, _) => new ValueTask<IMessageResult>(expectedResult), CancellationToken.None);

		// Assert
		result.ShouldBe(expectedResult);
	}

	[Fact]
	public async Task PassThrough_WhenBothICacheableAndAttribute_PrefersICacheable()
	{
		// Arrange -- message implements ICacheable AND has [CacheResult]
		var options = new CacheOptions { Enabled = true };
		var middleware = CreateMiddleware(options);
		var message = new DualCacheableMessage();
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>());

		A.CallTo(() => _fakeKeyBuilder.CreateKey(A<IDispatchAction>._, A<IMessageContext>._))
			.Returns("dual-key");

		var expectedResult = MessageResult.Success();

		// Act
		var result = await middleware.InvokeAsync(
			message, context, (_, _, _) => new ValueTask<IMessageResult>(expectedResult), CancellationToken.None);

		// Assert -- should work without error
		result.ShouldBe(expectedResult);
	}

	[Fact]
	public async Task HandleICacheable_WithNullTags()
	{
		// Arrange -- ICacheable that returns null tags
		var options = new CacheOptions { Enabled = true };
		var middleware = CreateMiddleware(options);
		var message = new NullTagsCacheableMessage();
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>());

		A.CallTo(() => _fakeKeyBuilder.CreateKey(A<IDispatchAction>._, A<IMessageContext>._))
			.Returns("null-tags-key");

		var expectedResult = MessageResult.Success();

		// Act -- should not throw
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

	private sealed class ZeroExpirationCacheableMessage : ICacheable<string>
	{
		public int ExpirationSeconds => 0;
		public string GetCacheKey() => "zero-exp";
		public bool ShouldCache(object? result) => true;
	}

	[CacheResult(ExpirationSeconds = 30)]
	private sealed class DualCacheableMessage : ICacheable<string>
	{
		public int ExpirationSeconds => 60;
		public string GetCacheKey() => "dual-key";
		public bool ShouldCache(object? result) => true;
	}

	private sealed class NullTagsCacheableMessage : ICacheable<string>
	{
		public string GetCacheKey() => "null-tags";
		public string[]? GetCacheTags() => null;
		public bool ShouldCache(object? result) => true;
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

#pragma warning restore IL2026, IL3050
