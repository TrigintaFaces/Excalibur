// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Caching;
using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Resilience.Polly;
using Microsoft.Extensions.Caching.Distributed;

namespace Excalibur.Integration.Tests.Caching;

// Public test types for distributed cache tests (private nested types prevent HybridCache from working)
[CacheResult]
public sealed class DistributedTestQuery : Application.Requests.Queries.QueryBase<DistributedTestResult>, IDispatchAction<DistributedTestResult>, IDispatchAction
{
	public int Value { get; init; }

	public override string ActivityDisplayName => "Distributed Cache Query";

	public override string ActivityDescription => "Query used for distributed cache tests";
}

public sealed class DistributedTestResult
{
	public int Value { get; init; }
}

public sealed class DistributedTestQueryHandler : IActionHandler<DistributedTestQuery, DistributedTestResult>
{
	public static int CallCount { get; set; }

	public Task<DistributedTestResult> HandleAsync(DistributedTestQuery message, CancellationToken cancellationToken = default)
	{
		CallCount++;
		var result = new DistributedTestResult { Value = message.Value * 2 };
		return Task.FromResult(result);
	}
}

/// <summary>
/// Ensures distributed-only caching bypasses the in-memory layer.
/// Uses [Collection] to prevent parallel execution with other caching tests
/// that scan the same assembly and share static handler counters.
/// </summary>
[Collection("CachingIntegrationTests")]
public sealed class DistributedCacheOnlyShould
{
	[Fact]
	public async Task WriteEntriesOnlyToDistributedStore()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddMetrics();
		_ = services.AddMemoryCache();
		// Use ForwardingDistributedCache because HybridCache skips MemoryDistributedCache as L2
		var distSvc = new ServiceCollection();
		_ = distSvc.AddDistributedMemoryCache();
		var distCache = distSvc.BuildServiceProvider().GetRequiredService<IDistributedCache>();
		_ = services.AddSingleton<IDistributedCache>(new ForwardingDistributedCache(distCache));
		_ = services.AddSingleton<IJsonSerializer, JsonMessageSerializer>();
		// Register the test handler explicitly
		_ = services.AddTransient<IActionHandler<DistributedTestQuery, DistributedTestResult>, DistributedTestQueryHandler>();

		_ = services.AddDispatch(dispatch =>
		{
			_ = dispatch.AddHandlersFromAssembly(typeof(DistributedCacheOnlyShould).Assembly);
			_ = dispatch.AddDispatchResilience();
			_ = dispatch.AddDispatchCaching()
				.WithCachingOptions(static o =>
				{
					o.Enabled = true;
					o.UseDistributedCache = true;
				});
		});
		var provider = services.BuildServiceProvider();
		_ = provider.GetRequiredKeyedService<IMessageBus>("Local");
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		var query = new DistributedTestQuery { Value = 42 };

		// Act - cache the result
		DistributedTestQueryHandler.CallCount = 0;
		var result1 = await dispatcher.DispatchAsync<DistributedTestQuery, DistributedTestResult>(
			query, new MessageContext(new TestDispatchAction(), provider), CancellationToken.None).ConfigureAwait(true);

		// Second dispatch should hit cache (served from distributed L2 store)
		var result2 = await dispatcher.DispatchAsync<DistributedTestQuery, DistributedTestResult>(
			query, new MessageContext(new TestDispatchAction(), provider), CancellationToken.None).ConfigureAwait(true);

		// Assert - handler called only once (second dispatch served from distributed cache)
		result1.Succeeded.ShouldBeTrue();
		result2.Succeeded.ShouldBeTrue();
		DistributedTestQueryHandler.CallCount.ShouldBe(1);
		result1.ReturnValue.Value.ShouldBe(result2.ReturnValue.Value);
		result2.CacheHit.ShouldBeTrue();
	}
}

/// <summary>
/// Wraps an <see cref="IDistributedCache"/> so HybridCache treats it as a real L2 backend.
/// HybridCache explicitly skips <see cref="Microsoft.Extensions.Caching.Memory.MemoryDistributedCache"/> as a no-op;
/// this wrapper has a different type so HybridCache uses it as a real distributed store.
/// </summary>
internal sealed class ForwardingDistributedCache(IDistributedCache inner) : IDistributedCache
{
	public byte[]? Get(string key) => inner.Get(key);
	public Task<byte[]?> GetAsync(string key, CancellationToken token = default) => inner.GetAsync(key, token);
	public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => inner.Set(key, value, options);
	public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default) => inner.SetAsync(key, value, options, token);
	public void Refresh(string key) => inner.Refresh(key);
	public Task RefreshAsync(string key, CancellationToken token = default) => inner.RefreshAsync(key, token);
	public void Remove(string key) => inner.Remove(key);
	public Task RemoveAsync(string key, CancellationToken token = default) => inner.RemoveAsync(key, token);
}
