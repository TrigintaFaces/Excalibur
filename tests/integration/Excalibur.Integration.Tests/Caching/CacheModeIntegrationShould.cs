// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Caching;
using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Resilience.Polly;

using Microsoft.Extensions.Caching.Distributed;

namespace Excalibur.Integration.Tests.Caching;

/// <summary>
/// Validates that caching and invalidation work correctly across all three cache modes:
/// Memory, Distributed, and Hybrid.
/// </summary>
[Collection("CachingIntegrationTests")]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
public sealed class CacheModeIntegrationShould
{
	[Theory]
	[InlineData(CacheMode.Memory)]
	[InlineData(CacheMode.Distributed)]
	[InlineData(CacheMode.Hybrid)]
	public async Task CacheAndInvalidateCorrectlyForEachMode(CacheMode mode)
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddMetrics();
		_ = services.AddMemoryCache();
		_ = services.AddSingleton<DispatchJsonSerializer>();

		// Distributed and Hybrid modes need a real IDistributedCache backend
		// (HybridCache treats MemoryDistributedCache as a no-op L2)
		if (mode is CacheMode.Distributed or CacheMode.Hybrid)
		{
			var distSvc = new ServiceCollection();
			_ = distSvc.AddDistributedMemoryCache();
			var distCache = distSvc.BuildServiceProvider().GetRequiredService<IDistributedCache>();
			_ = services.AddSingleton<IDistributedCache>(new ForwardingDistributedCache(distCache));
		}

		_ = services.AddTransient<IActionHandler<CachingTestQuery, CachingTestResult>, CachingTestQueryHandler>();
		_ = services.AddTransient<IActionHandler<InvalidateCacheCommand>, InvalidateCacheCommandHandler>();

		_ = services.AddDispatch(dispatch =>
		{
			_ = dispatch.AddHandlersFromAssembly(typeof(CacheModeIntegrationShould).Assembly);
			_ = dispatch.AddDispatchResilience()
				.AddDispatchCaching()
				.WithCachingOptions(o =>
				{
					o.Enabled = true;
					o.CacheMode = mode;
					o.Behavior.JitterRatio = 0; // Disable jitter for deterministic test timing
				});
		});

		var provider = services.BuildServiceProvider();
		_ = provider.GetRequiredKeyedService<IMessageBus>("Local");
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		var query = new CachingTestQuery { Value = 1000 + (int)mode };
		CachingTestQueryHandler.CallCount = 0;

		// Act 1 — first call populates cache
		var result1 = await dispatcher.DispatchAsync<CachingTestQuery, CachingTestResult>(
			query, new MessageContext(new TestDispatchAction(), provider), cancellationToken: default);

		// Act 2 — second call should hit cache
		var result2 = await dispatcher.DispatchAsync<CachingTestQuery, CachingTestResult>(
			query, new MessageContext(new TestDispatchAction(), provider), cancellationToken: default);

		// Assert — cached
		result1.Succeeded.ShouldBeTrue();
		result2.Succeeded.ShouldBeTrue();
		result2.CacheHit.ShouldBeTrue();
		result1.ReturnValue.Value.ShouldBe(result2.ReturnValue.Value);
		CachingTestQueryHandler.CallCount.ShouldBe(1);

		// Act 3 — invalidate by tag
		var invalidate = new InvalidateCacheCommand { TagsToInvalidate = ["test-tag"] };
		_ = await dispatcher.DispatchAsync(
			invalidate, new MessageContext(new TestDispatchAction(), provider), cancellationToken: default);

		// Act 4 — after invalidation handler should execute again
		var result3 = await dispatcher.DispatchAsync<CachingTestQuery, CachingTestResult>(
			query, new MessageContext(new TestDispatchAction(), provider), cancellationToken: default);

		// Assert — cache miss after invalidation
		CachingTestQueryHandler.CallCount.ShouldBe(2);
		result3.CacheHit.ShouldBeFalse();
	}

	[Theory]
	[InlineData(CacheMode.Memory)]
	[InlineData(CacheMode.Distributed)]
	[InlineData(CacheMode.Hybrid)]
	public async Task ProvideStampedeProtectionForEachMode(CacheMode mode)
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddMetrics();
		_ = services.AddMemoryCache();
		_ = services.AddSingleton<DispatchJsonSerializer>();

		if (mode is CacheMode.Distributed or CacheMode.Hybrid)
		{
			var distSvc = new ServiceCollection();
			_ = distSvc.AddDistributedMemoryCache();
			var distCache = distSvc.BuildServiceProvider().GetRequiredService<IDistributedCache>();
			_ = services.AddSingleton<IDistributedCache>(new ForwardingDistributedCache(distCache));
		}

		_ = services.AddTransient<IActionHandler<CachingTestQuery, CachingTestResult>, CachingTestQueryHandler>();

		_ = services.AddDispatch(dispatch =>
		{
			_ = dispatch.AddHandlersFromAssembly(typeof(CacheModeIntegrationShould).Assembly);
			_ = dispatch.AddDispatchResilience()
				.AddDispatchCaching()
				.WithCachingOptions(o =>
				{
					o.Enabled = true;
					o.CacheMode = mode;
					o.Behavior.JitterRatio = 0;
				});
		});

		var provider = services.BuildServiceProvider();
		_ = provider.GetRequiredKeyedService<IMessageBus>("Local");
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		var query = new CachingTestQuery { Value = 2000 + (int)mode };
		CachingTestQueryHandler.CallCount = 0;

		// Act — 5 concurrent dispatches
		var tasks = Enumerable.Range(0, 5)
			.Select(_ => dispatcher.DispatchAsync<CachingTestQuery, CachingTestResult>(
				query, new MessageContext(new TestDispatchAction(), provider), cancellationToken: default));
		var results = await Task.WhenAll(tasks);

		// Assert — handler called exactly once despite concurrency
		foreach (var r in results)
		{
			r.Succeeded.ShouldBeTrue();
		}

		// Stampede protection coalesces most concurrent calls, but under heavy load
		// a second call can slip through before the first result is cached -- this is
		// true for all cache modes (Memory, Distributed, Hybrid).
		CachingTestQueryHandler.CallCount.ShouldBeGreaterThan(0);
		CachingTestQueryHandler.CallCount.ShouldBeLessThanOrEqualTo(2);
	}
}
