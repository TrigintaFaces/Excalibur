// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable IL2026, IL3050 // Suppress for test - RequiresUnreferencedCode/RequiresDynamicCode

using System.Diagnostics.Metrics;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Caching;
using Excalibur.Dispatch.Resilience;

using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

using StackExchange.Redis;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Caching.Tests;

/// <summary>
/// yi59t5 regression lock: <see cref="CachingMiddleware"/> must treat a distributed-cache <b>backend</b>
/// outage as fail-open (cache error ≠ application outage) when <c>Resilience.EnableFallback</c> is true,
/// honor explicit fail-closed when it is false, skip the cache entirely while the circuit breaker is open,
/// and NEVER let the fail-open scope swallow a result-handling fault.
/// </summary>
/// <remarks>
/// Authored by FrontendDeveloper (implementer) at PM direction (msg 17566); flagged author=impl and
/// requested independent Tests review/augment (msg 17570). Non-vacuity: each test's RED mutant is documented
/// inline and was proven by a cp-backup mutate-restore of the committed middleware (never a git checkout of a
/// shared file — commit-surface-before-parallel-edits).
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Feature", "Caching")]
public sealed class CachingMiddlewareFailOpenShould : IDisposable
{
	private readonly IMeterFactory _meterFactory = new TestMeterFactory();
	private readonly HybridCache _fakeCache = A.Fake<HybridCache>();
	private readonly ICacheKeyBuilder _fakeKeyBuilder = A.Fake<ICacheKeyBuilder>();
	private readonly IServiceProvider _fakeServices = A.Fake<IServiceProvider>();

	public CachingMiddlewareFailOpenShould()
		// A non-null key drives the message into the cache path (GetOrCreateAsync), so the throws below are
		// actually exercised — NOT bypassed via a null "do not cache" key. This is what makes the lock non-vacuous.
		=> A.CallTo(() => _fakeKeyBuilder.CreateKey(A<IDispatchAction>._, A<IMessageContext>._)).Returns("fail-open-key");

	public void Dispose()
	{
		if (_meterFactory is IDisposable d) d.Dispose();
	}

	private CachingMiddleware CreateMiddleware(CacheOptions options, ICircuitBreakerPolicy? breaker = null)
	{
		var logger = A.Fake<ILogger<CachingMiddleware>>();
		A.CallTo(() => logger.IsEnabled(A<LogLevel>._)).Returns(true);
		return new CachingMiddleware(
			_meterFactory, _fakeCache, _fakeKeyBuilder, _fakeServices,
			MsOptions.Create(options), logger, cacheCircuitBreaker: breaker);
	}

	// Throws on EVERY GetOrCreateAsync invocation regardless of generic instantiation — matching by method
	// name avoids the generic-arg matcher trap (a mock keyed to the wrong <TState,T> never fires, silently
	// turning the test vacuous).
	private void MakeCacheBackendThrow(Exception ex) =>
		A.CallTo(_fakeCache).Where(call => call.Method.Name == nameof(HybridCache.GetOrCreateAsync)).Throws(ex);

	[Fact]
	public async Task FallOpenToHandler_WhenCacheBackendThrows_AndEnableFallbackTrue()
	{
		// RED mutant: remove the `catch (Exception ex) when (ex is not OCE && EnableFallback)` clause (pre-fix
		// yi59t5 caught ONLY OperationCanceledException) -> the RedisConnectionException propagates and this test
		// throws instead of returning the handler result.
		var options = new CacheOptions { Enabled = true, CacheMode = CacheMode.Distributed };
		options.Resilience.EnableFallback = true; // default; explicit for intent
		var middleware = CreateMiddleware(options);

		MakeCacheBackendThrow(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "cache backend down"));

		var message = new FailOpenCacheableMessage();
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>());

		var expectedResult = MessageResult.Success();
		var handlerRan = false;
		DispatchRequestDelegate next = (_, _, _) =>
		{
			handlerRan = true;
			return new ValueTask<IMessageResult>(expectedResult);
		};

		// Act — a fast-erroring cache backend must fall open to the handler, not fail the dispatch.
		var result = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		handlerRan.ShouldBeTrue("a cache-backend error must fall open to direct handler execution");
		result.ShouldBe(expectedResult);
	}

	[Fact]
	public async Task PropagateBackendError_WhenEnableFallbackFalse()
	{
		// Guards the EnableFallback gate: RED mutant = drop the `&& EnableFallback` condition (always fall open)
		// -> the exception would be swallowed and the handler result returned, so this throw assertion fails.
		var options = new CacheOptions { Enabled = true, CacheMode = CacheMode.Distributed };
		options.Resilience.EnableFallback = false; // explicit fail-closed
		var middleware = CreateMiddleware(options);

		MakeCacheBackendThrow(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "cache backend down"));

		var message = new FailOpenCacheableMessage();
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>());

		DispatchRequestDelegate next = (_, _, _) => new ValueTask<IMessageResult>(MessageResult.Success());

		// Act & Assert — with fallback explicitly disabled, the backend error must propagate (fail-closed).
#pragma warning disable CA2012 // ValueTask used correctly in async lambda
		await Should.ThrowAsync<RedisConnectionException>(
			async () => await middleware.InvokeAsync(message, context, next, CancellationToken.None));
#pragma warning restore CA2012
	}

	[Fact]
	public async Task SkipCacheEntirely_WhenBreakerOpen()
	{
		// RED mutant: remove the `if (IsCacheBreakerOpen()) return nextDelegate(...)` short-circuit (pre-fix had
		// no breaker gating) -> GetOrCreateAsync IS called and MustNotHaveHappened fails.
		var options = new CacheOptions { Enabled = true, CacheMode = CacheMode.Distributed };
		// CircuitBreaker.Enabled defaults true; an open breaker means the backend is known-unhealthy.
		var breaker = A.Fake<ICircuitBreakerPolicy>();
		A.CallTo(() => breaker.State).Returns(CircuitState.Open);
		var middleware = CreateMiddleware(options, breaker);

		var message = new FailOpenCacheableMessage();
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>());

		var expectedResult = MessageResult.Success();
		var handlerRan = false;
		DispatchRequestDelegate next = (_, _, _) =>
		{
			handlerRan = true;
			return new ValueTask<IMessageResult>(expectedResult);
		};

		// Act — while the breaker is open, the cache is skipped and the handler runs directly (no timeout paid).
		var result = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert
		handlerRan.ShouldBeTrue("an open breaker must skip the cache and run the handler");
		result.ShouldBe(expectedResult);
		A.CallTo(_fakeCache).Where(call => call.Method.Name == nameof(HybridCache.GetOrCreateAsync))
			.MustNotHaveHappened();
	}

	private sealed class FailOpenCacheableMessage : ICacheable<string>
	{
		public string GetCacheKey() => "fail-open:1";
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
