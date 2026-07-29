// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable IL2026, IL3050 // Suppress for test - RequiresUnreferencedCode/RequiresDynamicCode

using System.Diagnostics.Metrics;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Caching;

using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Caching.Tests;

/// <summary>
/// Regression locks for the two cache-decision correctness bugs on the interface caching path
/// (<c>ICacheable&lt;T&gt;</c>). Both run against a REAL <see cref="HybridCache"/> (in-memory) so the
/// factory actually executes, the value round-trips serialize→store→deserialize, and a second identical
/// request either hits or re-executes — proving observable end-to-end behavior, not a mocked flag.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><b>7n7hsb</b> — value-type covariance gap: <c>ExtractReturnValue</c> matched only
/// <c>IMessageResult&lt;object&gt;</c>. A value-type result (<c>IMessageResult&lt;int&gt;</c>) is NOT
/// assignable to <c>IMessageResult&lt;object&gt;</c> (variance excludes value types), so an
/// int-returning handler's value was never extracted → never cached → re-executed on every request.</item>
/// <item><b>w4181o</b> — <c>ICacheable&lt;T&gt;.ShouldCache</c> was never consulted on the interface path
/// (only the registered policy drove the decision), so a result returning <c>ShouldCache=false</c> was
/// silently cached anyway.</item>
/// </list>
/// Each bug lock is paired with a positive control (a reference-type / cache-enabled message) proving the
/// harness genuinely caches — so a "handler ran N times" assertion is non-vacuous.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Feature", "Caching")]
public sealed class CachingMiddlewareCacheDecisionShould : IDisposable
{
	private const string CacheKey = "cache-decision-key";

	private readonly ServiceProvider _services;
	private readonly HybridCache _cache;
	private readonly IMeterFactory _meterFactory;
	private readonly ICacheKeyBuilder _keyBuilder = A.Fake<ICacheKeyBuilder>();
	private bool _disposed;

	public CachingMiddlewareCacheDecisionShould()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddMetrics();
		_ = services.AddHybridCache();
		_services = services.BuildServiceProvider();
		_cache = _services.GetRequiredService<HybridCache>();
		_meterFactory = _services.GetRequiredService<IMeterFactory>();

		A.CallTo(() => _keyBuilder.CreateKey(A<IDispatchAction>._, A<IMessageContext>._)).Returns(CacheKey);
	}

	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;

		if (_meterFactory is IDisposable disposableMeterFactory)
		{
			disposableMeterFactory.Dispose();
		}

		_services.Dispose();
	}

	// ─────────────────────────── 7n7hsb: value-type covariance gap ───────────────────────────

	[Fact]
	public async Task Cache_ValueTypeResult_SoSecondRequestIsServedFromCache_7n7hsb()
	{
		// Arrange — an int-returning ICacheable handler. Post-fix the value-type return is extracted and
		// cached; pre-fix ExtractReturnValue returns null → nothing cached → handler re-runs.
		var middleware = CreateMiddleware();
		var message = new IntCacheableAction();
		var calls = 0;
		DispatchRequestDelegate next = (_, _, _) =>
		{
			calls++;
			return new ValueTask<IMessageResult>(MessageResult.Success<int>(42));
		};

		// Act — two identical requests through the same cache key
		var first = await middleware.InvokeAsync(message, NewContext(), next, CancellationToken.None);
		var second = await middleware.InvokeAsync(message, NewContext(), next, CancellationToken.None);

		// Assert — value-type result must round-trip: handler executes ONCE, second request served from cache
		_ = first.ShouldNotBeNull();
		_ = second.ShouldNotBeNull();
		calls.ShouldBe(1, "an int-returning ICacheable result must be cached so the second request is a cache hit");
	}

	[Fact]
	public async Task Cache_ReferenceTypeResult_PositiveControl()
	{
		// Positive control — a string (reference-type) result already round-trips via IMessageResult<object>
		// covariance. Proves the harness genuinely caches, so the int case above is a real gap, not a
		// broken fixture.
		var middleware = CreateMiddleware();
		var message = new StringCacheableAction(shouldCache: true);
		var calls = 0;
		DispatchRequestDelegate next = (_, _, _) =>
		{
			calls++;
			return new ValueTask<IMessageResult>(MessageResult.Success<string>("value"));
		};

		_ = await middleware.InvokeAsync(message, NewContext(), next, CancellationToken.None);
		_ = await middleware.InvokeAsync(message, NewContext(), next, CancellationToken.None);

		calls.ShouldBe(1, "a reference-type ICacheable result is cached; the second request must be a cache hit");
	}

	// ─────────────────────────── w4181o: ShouldCache=false ignored ───────────────────────────

	[Fact]
	public async Task NotCache_WhenICacheableShouldCacheIsFalse_w4181o()
	{
		// Arrange — a message whose ICacheable<T>.ShouldCache returns false. Post-fix the interface decision
		// is honored → result NOT cached → handler runs again on the second request. Pre-fix only the policy
		// drove the decision (default true) → cached → second request served from cache (handler runs once).
		var middleware = CreateMiddleware();
		var message = new StringCacheableAction(shouldCache: false);
		var calls = 0;
		DispatchRequestDelegate next = (_, _, _) =>
		{
			calls++;
			return new ValueTask<IMessageResult>(MessageResult.Success<string>("value"));
		};

		// Act
		_ = await middleware.InvokeAsync(message, NewContext(), next, CancellationToken.None);
		_ = await middleware.InvokeAsync(message, NewContext(), next, CancellationToken.None);

		// Assert — ShouldCache=false suppresses caching, so BOTH requests execute the handler
		calls.ShouldBe(2, "ICacheable<T>.ShouldCache=false must suppress caching on the interface path");
	}

	private CachingMiddleware CreateMiddleware()
		=> new(
			_meterFactory,
			_cache,
			_keyBuilder,
			_services,
			MsOptions.Create(new CacheOptions { Enabled = true }),
			NullLogger<CachingMiddleware>.Instance);

	private static IMessageContext NewContext()
	{
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>());
		return context;
	}

	// ─── Test message types ───

	private sealed class IntCacheableAction : ICacheable<int>
	{
		public int ExpirationSeconds => 120;
		public string GetCacheKey() => "int-cacheable";
		public bool ShouldCache(object? result) => true;
	}

	private sealed class StringCacheableAction(bool shouldCache) : ICacheable<string>
	{
		private readonly bool _shouldCache = shouldCache;
		public int ExpirationSeconds => 120;
		public string GetCacheKey() => "string-cacheable";
		public bool ShouldCache(object? result) => _shouldCache;
	}
}

#pragma warning restore IL2026, IL3050
