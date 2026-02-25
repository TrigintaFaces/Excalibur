// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Caching;
using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Resilience.Polly;

namespace Excalibur.Integration.Tests.Caching;

// Public test types for Hypothesis 3 testing (private nested types may cause HybridCache issues)
[CacheResult]
public sealed class CacheTestQuery : IDispatchAction<CacheTestResult>, ICacheable<CacheTestResult>
{
	public int Id { get; init; }

	public string MessageId => Guid.NewGuid().ToString();

	public DateTimeOffset Timestamp => DateTimeOffset.UtcNow;

	public MessageKinds Kind => MessageKinds.Action;

	public IReadOnlyDictionary<string, object> Headers => new Dictionary<string, object>();

	public object Body => this;

	public string MessageType => GetType().Name;

	public string GetCacheKey() => $"query-{Id}";

	// Return empty tags array to test if tags are causing distributed caching issues
	public string[] GetCacheTags() => [];
}

public sealed class CacheTestResult
{
	public int Value { get; init; }
}

public sealed class CacheTestQueryHandler : IActionHandler<CacheTestQuery, CacheTestResult>
{
	public static int CallCount { get; set; }

	public Task<CacheTestResult> HandleAsync(CacheTestQuery message, CancellationToken cancellationToken = default)
	{
		CallCount++;
		return Task.FromResult(new CacheTestResult { Value = message.Id * 2 });
	}
}

[Collection("CachingIntegrationTests")]
public sealed class CacheResultAttributeWithICacheableIntegrationShould
{
	[Theory]
	[InlineData(CacheMode.Memory)]
	[InlineData(CacheMode.Hybrid)]
	public async Task CacheResultsWithHybridAndDistributedCache(CacheMode cacheMode)
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddMetrics();
		_ = services.AddMemoryCache();
		_ = services.AddDistributedMemoryCache();
		_ = services.AddSingleton<IJsonSerializer, JsonMessageSerializer>();

		// Register the test handler explicitly
		_ = services.AddTransient<IActionHandler<CacheTestQuery, CacheTestResult>, CacheTestQueryHandler>();

		_ = services.AddDispatch(dispatch =>
		{
			_ = dispatch.AddHandlersFromAssembly(typeof(CacheResultAttributeWithICacheableIntegrationShould).Assembly);
			_ = dispatch.AddDispatchResilience();
			_ = dispatch.AddDispatchCaching()
				.WithCachingOptions(o =>
				{
					o.Enabled = true;
					o.CacheMode = cacheMode;
					o.Behavior.DefaultExpiration = TimeSpan.FromMinutes(1);
				});
		});
		var provider = services.BuildServiceProvider();
		_ = provider.GetRequiredKeyedService<IMessageBus>("Local");
		var dispatcher = provider.GetRequiredService<IDispatcher>();
		var query = new CacheTestQuery { Id = 123 };
		CacheTestQueryHandler.CallCount = 0;

		// Act - first call caches the result (fresh context)
		var testMessage1 = new TestDispatchAction();
		var context1 = new MessageContext(testMessage1, provider);
		var result1 = await dispatcher.DispatchAsync<CacheTestQuery, CacheTestResult>(query, context1, CancellationToken.None).ConfigureAwait(true);

		// Act - second call should hit cache (fresh context to avoid state pollution)
		var testMessage2 = new TestDispatchAction();
		var context2 = new MessageContext(testMessage2, provider);
		var result2 = await dispatcher.DispatchAsync<CacheTestQuery, CacheTestResult>(query, context2, CancellationToken.None).ConfigureAwait(true);

		// Assert
		result1.Succeeded.ShouldBeTrue();
		result2.Succeeded.ShouldBeTrue();
		result2.CacheHit.ShouldBeTrue();
		// HybridCache returns different instances due to serialization - compare values instead of references
		result1.ReturnValue.Value.ShouldBe(result2.ReturnValue.Value);
		CacheTestQueryHandler.CallCount.ShouldBe(1);
	}
}
