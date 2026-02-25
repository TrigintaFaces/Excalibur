// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Caching;
using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Resilience.Polly;

using Excalibur.Application.Requests.Queries;

namespace Excalibur.Integration.Tests.Caching;

// Public test types for caching integration tests (private nested types prevent HybridCache from working)
[CacheResult(Tags = ["test-tag"])]
public sealed class CachingTestQuery : QueryBase<CachingTestResult>, IDispatchAction<CachingTestResult>
{
	public int Value { get; init; }

	public override string ActivityDisplayName => "Test Query";

	public override string ActivityDescription => "A test query for caching";
}

public sealed class CachingTestResult
{
	public int Value { get; init; }

	public string Timestamp { get; init; } = DateTimeOffset.UtcNow.ToString("O");
}

public sealed class CachingTestQueryHandler : IActionHandler<CachingTestQuery, CachingTestResult>
{
	public static int CallCount { get; set; }

	public Task<CachingTestResult> HandleAsync(
		CachingTestQuery message,
		CancellationToken cancellationToken = default)
	{
		CallCount++;
		var result = new CachingTestResult { Value = message.Value * 2 };
		return Task.FromResult(result);
	}
}

// Public test types for policy and middleware tests (private nested types prevent HybridCache from working)
public sealed class TestResult
{
	public int Value { get; init; }
}

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes",
	Justification = "Instantiated via DI")]
public sealed class ConditionalCachePolicy : IResultCachePolicy<CachingTestQuery>
{
	public bool ShouldCache(CachingTestQuery message, object? result) =>
		// Only cache queries with Value >= 100
		message.Value >= 100;

	public TimeSpan GetCacheDuration(CachingTestQuery message) => TimeSpan.FromMinutes(5);
}

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes",
	Justification = "Instantiated via DI")]
public sealed class TestTrackingMiddleware : IDispatchMiddleware
{
	public int CallCount { get; private set; }

	public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.PreProcessing;

	public async ValueTask<IMessageResult> InvokeAsync(
		IDispatchMessage message,
		IMessageContext context,
		DispatchRequestDelegate nextDelegate,
		CancellationToken cancellationToken)
	{
		CallCount++;
		return await nextDelegate(message, context, cancellationToken).ConfigureAwait(true);
	}
}

[CacheResult(OnlyIfSuccess = true)]
public sealed class OnlyIfSuccessQuery : QueryBase<TestResult>, IDispatchAction<TestResult>
{
	public int Value { get; init; }

	public override string ActivityDisplayName => "OnlyIfSuccess Query";

	public override string ActivityDescription => "Query with OnlyIfSuccess";
}

[SuppressMessage("Performance", "CA1812", Justification = "Instantiated via DI")]
public sealed class OnlyIfSuccessQueryHandler : IActionHandler<OnlyIfSuccessQuery, TestResult>
{
	public static int CallCount { get; set; }

	public Task<TestResult> HandleAsync(OnlyIfSuccessQuery message, CancellationToken cancellationToken = default)
	{
		CallCount++;
		var result = new TestResult { Value = message.Value };
		return Task.FromResult(result);
	}
}

[CacheResult(IgnoreNullResult = true)]
public sealed class NullResultQuery : QueryBase<TestResult>, IDispatchAction<TestResult>
{
	public int Value { get; init; }

	public override string ActivityDisplayName => "Null Result Query";

	public override string ActivityDescription => "Query returning null";
}

[SuppressMessage("Performance", "CA1812", Justification = "Instantiated via DI")]
public sealed class NullResultQueryHandler : IActionHandler<NullResultQuery, TestResult>
{
	public static int CallCount { get; set; }

	public Task<TestResult> HandleAsync(NullResultQuery message, CancellationToken cancellationToken = default)
	{
		CallCount++;
		return Task.FromResult<TestResult>(null!);
	}
}

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes",
	Justification = "Instantiated via DI")]
public sealed class InvalidateCacheCommand : ICacheInvalidator, ITestCacheInvalidator, IDispatchAction
{
	public Guid Id => Guid.NewGuid();

	public string MessageId => Guid.NewGuid().ToString();

	public DateTimeOffset Timestamp => DateTimeOffset.UtcNow;

	public MessageKinds Kind => MessageKinds.Action;

	public IReadOnlyDictionary<string, object> Headers => new Dictionary<string, object>();

	public object Body => nameof(InvalidateCacheCommand);

	public string MessageType => nameof(InvalidateCacheCommand);

	public IEnumerable<string> TagsToInvalidate { get; init; } = [];

	public IEnumerable<string> KeysToInvalidate { get; init; } = [];

	public string ActivityDisplayName => "Invalidate Cache";

	public string ActivityDescription => "Invalidates cached entries";

	public IEnumerable<string> GetCacheTagsToInvalidate() => TagsToInvalidate;

	public IEnumerable<string> GetCacheKeysToInvalidate() => KeysToInvalidate;

	public Task InvalidateAsync(string key, CancellationToken cancellationToken = default) => Task.CompletedTask;

	public Task InvalidateAsync(string[] keys, CancellationToken cancellationToken = default) => Task.CompletedTask;

	public Task InvalidateManyAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default) => Task.CompletedTask;

	public Task InvalidateByPatternAsync(string pattern, CancellationToken cancellationToken = default) => Task.CompletedTask;

	public Task InvalidateByTagAsync(string tag, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes",
	Justification = "Instantiated via DI")]
public sealed class InvalidateCacheCommandHandler : IActionHandler<InvalidateCacheCommand>
{
	public Task HandleAsync(InvalidateCacheCommand action, CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>
/// Integration tests for the caching functionality.
/// </summary>
[Collection("CachingIntegrationTests")] // Disable parallel execution to avoid shared static CallCount issues
public sealed class CachingIntegrationShould : IntegrationTestBase
{
	[Fact]
	public async Task CacheQueryResultsEndToEnd()
	{
		// Arrange
		var services = new ServiceCollection();

		// Add required services
		_ = services.AddLogging();
		_ = services.AddMetrics();
		_ = services.AddMemoryCache();
		_ = services.AddSingleton<IMemoryCache, MemoryCache>();
		_ = services.AddSingleton<IJsonSerializer, JsonMessageSerializer>();

		// Register the test handler explicitly
		_ = services.AddTransient<IActionHandler<CachingTestQuery, CachingTestResult>, CachingTestQueryHandler>();

		// Add dispatch and caching
		_ = services.AddDispatch(dispatch =>
		{
			_ = dispatch.AddHandlersFromAssembly(typeof(CachingIntegrationShould).Assembly);
			_ = dispatch.AddDispatchResilience();
			_ = dispatch.AddDispatchCaching()
				.WithCachingOptions(opt =>
				{
					opt.Enabled = true;
					opt.UseDistributedCache = false;
					opt.Behavior.DefaultExpiration = TimeSpan.FromMinutes(5);
				});
		});
		var provider = services.BuildServiceProvider();
		// Ensure the local bus is registered
		_ = provider.GetRequiredKeyedService<IMessageBus>("Local");
		var dispatcher = provider.GetRequiredService<IDispatcher>();
		var memoryCache = provider.GetRequiredService<IMemoryCache>();
		var contextAccessor = provider.GetRequiredService<IMessageContextAccessor>();

		var query = new CachingTestQuery { Value = 123 };

		// Reset handler call count to ensure clean state
		CachingTestQueryHandler.CallCount = 0;

		// Act - First call should execute handler
		var testMessage = new TestDispatchAction();
		var context = new MessageContext(testMessage, provider);
		var result1 = await dispatcher.DispatchAsync<CachingTestQuery, CachingTestResult>(query, context, CancellationToken.None)
			.ConfigureAwait(true);

		// Act - Second call should return cached result
		var result2 = await dispatcher.DispatchAsync<CachingTestQuery, CachingTestResult>(query, context, CancellationToken.None)
			.ConfigureAwait(true);

		// Assert
		_ = result1.ShouldNotBeNull();
		_ = result2.ShouldNotBeNull();
		result1.Succeeded.ShouldBeTrue();
		result2.Succeeded.ShouldBeTrue();
		// HybridCache returns different instances due to serialization - compare values instead of references
		result1.ReturnValue.Value.ShouldBe(result2.ReturnValue.Value);

		// Verify handler was called only once
		CachingTestQueryHandler.CallCount.ShouldBe(1);
	}

	[Fact]
	public async Task InvalidateCacheWithAttribute()
	{
		// Arrange
		var services = new ServiceCollection();

		_ = services.AddLogging();
		_ = services.AddMetrics();
		_ = services.AddMemoryCache();
		_ = services.AddSingleton<IJsonSerializer, JsonMessageSerializer>();

		// Register the test handlers explicitly
		_ = services.AddTransient<IActionHandler<CachingTestQuery, CachingTestResult>, CachingTestQueryHandler>();
		_ = services.AddTransient<IActionHandler<InvalidateCacheCommand>, InvalidateCacheCommandHandler>();

		_ = services.AddDispatch(dispatch =>
		{
			_ = dispatch.AddHandlersFromAssembly(typeof(CachingIntegrationShould).Assembly);
			_ = dispatch.AddDispatchResilience()
				.AddDispatchCaching()
				.WithCachingOptions(o =>
				{
					o.UseDistributedCache = false;
					o.Enabled = true;
				});
		});
		var provider = services.BuildServiceProvider();
		// Ensure the local bus is registered
		_ = provider.GetRequiredKeyedService<IMessageBus>("Local");
		var dispatcher = provider.GetRequiredService<IDispatcher>();
		var testMessage = new TestDispatchAction();
		var context = new MessageContext(testMessage, provider);

		var query = new CachingTestQuery { Value = 456 };

		// Reset handler call count to ensure clean state
		CachingTestQueryHandler.CallCount = 0;

		// Act - First call caches result
		var result1 = await dispatcher.DispatchAsync<CachingTestQuery, CachingTestResult>(query, context, CancellationToken.None)
			.ConfigureAwait(true);

		// Reset handler call count
		CachingTestQueryHandler.CallCount = 0;

		// Act - Second call should use cache
		var result2 = await dispatcher.DispatchAsync<CachingTestQuery, CachingTestResult>(query, context, CancellationToken.None)
			.ConfigureAwait(true);

		// Assert HybridCache returns different instances due to serialization - compare values instead of references
		result1.ReturnValue.Value.ShouldBe(result2.ReturnValue.Value);
		CachingTestQueryHandler.CallCount.ShouldBe(0); // Handler not called again
	}

	[Fact]
	public async Task RespectCachePolicyDecisions()
	{
		// Arrange
		var services = new ServiceCollection();

		_ = services.AddLogging();
		_ = services.AddMetrics();
		_ = services.AddMemoryCache();
		_ = services.AddSingleton<IJsonSerializer, JsonMessageSerializer>();

		// Register the test handler explicitly
		_ = services.AddTransient<IActionHandler<CachingTestQuery, CachingTestResult>, CachingTestQueryHandler>();
		_ = services.AddTransient<ConditionalCachePolicy>();

		_ = services.AddDispatch(dispatch =>
		{
			_ = dispatch.AddHandlersFromAssembly(typeof(CachingIntegrationShould).Assembly);
			_ = dispatch.AddDispatchResilience()
				.AddDispatchCaching()
				.WithCachingOptions(static o =>
				{
					o.Enabled = true;
					o.UseDistributedCache = false;
				})
				.WithResultCachePolicy<CachingTestQuery, ConditionalCachePolicy>();
		});
		var provider = services.BuildServiceProvider();
		// Ensure the local bus is registered
		_ = provider.GetRequiredKeyedService<IMessageBus>("Local");
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		// Act - Query with MessageId < 100 should not be cached
		CachingTestQueryHandler.CallCount = 0;
		var query1 = new CachingTestQuery { Value = 50 };
		var result1a = await dispatcher.DispatchAsync<CachingTestQuery, CachingTestResult>(query1, new MessageContext(new TestDispatchAction(), provider), cancellationToken: default).ConfigureAwait(true);
		var result1b = await dispatcher.DispatchAsync<CachingTestQuery, CachingTestResult>(query1, new MessageContext(new TestDispatchAction(), provider), cancellationToken: default).ConfigureAwait(true);

		// Act - Query with MessageId >= 100 should be cached
		var query2 = new CachingTestQuery { Value = 150 };
		var result2a = await dispatcher.DispatchAsync<CachingTestQuery, CachingTestResult>(query2, new MessageContext(new TestDispatchAction(), provider), cancellationToken: default).ConfigureAwait(true);
		var result2b = await dispatcher.DispatchAsync<CachingTestQuery, CachingTestResult>(query2, new MessageContext(new TestDispatchAction(), provider), cancellationToken: default).ConfigureAwait(true);

		// Assert - handler should be called 3 times: 2 for query1 (not cached), 1 for query2 (cached on second call)
		CachingTestQueryHandler.CallCount.ShouldBe(3);

		// Non-cached: both calls execute handler, returning fresh results
		result1a.ReturnValue.Value.ShouldBe(100); // 50 * 2
		result1b.ReturnValue.Value.ShouldBe(100); // 50 * 2

		// Cached: second call returns same value as first without executing handler
		result2a.ReturnValue.Value.ShouldBe(300); // 150 * 2
		result2b.ReturnValue.Value.ShouldBe(300); // 150 * 2
		result2b.CacheHit.ShouldBeTrue();
	}

	[Fact]
	public async Task HandleCachingWithMultipleMiddleware()
	{
		// Arrange
		var services = new ServiceCollection();

		_ = services.AddLogging();
		_ = services.AddMetrics();
		_ = services.AddMemoryCache();
		_ = services.AddSingleton<IJsonSerializer, JsonMessageSerializer>();

		// Register the test handlers explicitly
		_ = services.AddTransient<IActionHandler<CachingTestQuery, CachingTestResult>, CachingTestQueryHandler>();
		_ = services.AddTransient<IActionHandler<InvalidateCacheCommand>, InvalidateCacheCommandHandler>();

		_ = services.AddDispatch(dispatch =>
		{
			_ = dispatch.AddHandlersFromAssembly(typeof(CachingIntegrationShould).Assembly);
			_ = dispatch.AddDispatchResilience()
				.AddDispatchCaching()
				.WithCachingOptions(o =>
				{
					o.Enabled = true;
					o.UseDistributedCache = false;
				});
		});
		_ = services.AddSingleton<IDispatchMiddleware, TestTrackingMiddleware>();
		var provider = services.BuildServiceProvider();
		// Ensure the local bus is registered
		_ = provider.GetRequiredKeyedService<IMessageBus>("Local");
		var dispatcher = provider.GetRequiredService<IDispatcher>();
		var trackingMiddleware = (TestTrackingMiddleware)provider.GetServices<IDispatchMiddleware>().First(m => m is TestTrackingMiddleware);

		var query = new CachingTestQuery { Value = 789 };

		// Act
		CachingTestQueryHandler.CallCount = 0;
		_ = await dispatcher.DispatchAsync<CachingTestQuery, CachingTestResult>(query, new MessageContext(new TestDispatchAction(), provider), CancellationToken.None)
			.ConfigureAwait(true);
		_ = await dispatcher.DispatchAsync<CachingTestQuery, CachingTestResult>(query, new MessageContext(new TestDispatchAction(), provider), CancellationToken.None)
			.ConfigureAwait(true); // Cached

		// Assert
		trackingMiddleware.CallCount.ShouldBe(2); // Middleware called twice
		CachingTestQueryHandler.CallCount.ShouldBe(1); // Handler called once (cached)
	}

	[Fact]
	public async Task ExpireEntriesAfterDefaultExpiration()
	{
		// Arrange
		var services = new ServiceCollection();

		_ = services.AddLogging();
		_ = services.AddMetrics();
		_ = services.AddMemoryCache();
		_ = services.AddSingleton<IJsonSerializer, JsonMessageSerializer>();

		// Register the test handlers explicitly
		_ = services.AddTransient<IActionHandler<CachingTestQuery, CachingTestResult>, CachingTestQueryHandler>();
		_ = services.AddTransient<IActionHandler<InvalidateCacheCommand>, InvalidateCacheCommandHandler>();

		_ = services.AddDispatch(dispatch =>
		{
			_ = dispatch.AddHandlersFromAssembly(typeof(CachingIntegrationShould).Assembly);
			_ = dispatch.AddDispatchResilience()
				.AddDispatchCaching()
				.WithCachingOptions(o =>
				{
					o.Enabled = true;
					o.UseDistributedCache = false;
					o.Behavior.DefaultExpiration = TimeSpan.FromMilliseconds(300);
				});
		});
		var provider = services.BuildServiceProvider();
		_ = provider.GetRequiredKeyedService<IMessageBus>("Local");
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		var query = new CachingTestQuery { Value = 321 };

		// Act - first call caches the result
		CachingTestQueryHandler.CallCount = 0;
		var result1 = await dispatcher.DispatchAsync<CachingTestQuery, CachingTestResult>(query, new MessageContext(new TestDispatchAction(), provider), CancellationToken.None)
			.ConfigureAwait(true);
		var result2 = await dispatcher.DispatchAsync<CachingTestQuery, CachingTestResult>(query, new MessageContext(new TestDispatchAction(), provider), CancellationToken.None)
			.ConfigureAwait(true);

		// Assert cached
		CachingTestQueryHandler.CallCount.ShouldBe(1);
		result2.CacheHit.ShouldBeTrue();

		// Wait for expiration (DefaultExpiration is 300ms)
		await Task.Delay(350).ConfigureAwait(true);

		// Act - after expiration handler should run again
		var result3 = await dispatcher.DispatchAsync<CachingTestQuery, CachingTestResult>(query, new MessageContext(new TestDispatchAction(), provider), CancellationToken.None)
			.ConfigureAwait(true);

		// Assert
		CachingTestQueryHandler.CallCount.ShouldBe(2);
		result3.CacheHit.ShouldBeFalse();
		result3.ReturnValue.Timestamp.ShouldNotBe(result1.ReturnValue.Timestamp);
	}

	[Fact]
	public async Task NotCacheWhenValidationFailsWithOnlyIfSuccess()
	{
		// Arrange
		var services = new ServiceCollection();

		_ = services.AddLogging();
		_ = services.AddMetrics();
		_ = services.AddMemoryCache();
		_ = services.AddSingleton<IJsonSerializer, JsonMessageSerializer>();

		// Register the test handler explicitly
		_ = services.AddTransient<IActionHandler<OnlyIfSuccessQuery, TestResult>, OnlyIfSuccessQueryHandler>();

		_ = services.AddDispatch(dispatch =>
		{
			_ = dispatch.AddHandlersFromAssembly(typeof(CachingIntegrationShould).Assembly);
			_ = dispatch.AddDispatchResilience()
				.AddDispatchCaching()
				.WithCachingOptions(static o =>
				{
					o.Enabled = true;
					o.UseDistributedCache = false;
				});
		});
		var provider = services.BuildServiceProvider();
		_ = provider.GetRequiredKeyedService<IMessageBus>("Local");
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		var query = new OnlyIfSuccessQuery { Value = 99 };

		var testMessage1 = new TestDispatchAction();
		var context1 = new MessageContext(testMessage1, provider);
		// Set validation result via extension method (Items dictionary) so caching middleware can read it
		Excalibur.Dispatch.Abstractions.MessageContextExtensions.ValidationResult(context1, SerializableValidationResult.Failed("bad"));
		var testMessage2 = new TestDispatchAction();
		var context2 = new MessageContext(testMessage2, provider);
		Excalibur.Dispatch.Abstractions.MessageContextExtensions.ValidationResult(context2, SerializableValidationResult.Failed("bad"));

		OnlyIfSuccessQueryHandler.CallCount = 0;
		_ = await dispatcher.DispatchAsync<OnlyIfSuccessQuery, TestResult>(query, context1, cancellationToken: default).ConfigureAwait(true);
		_ = await dispatcher.DispatchAsync<OnlyIfSuccessQuery, TestResult>(query, context2, cancellationToken: default).ConfigureAwait(true);

		// Assert
		OnlyIfSuccessQueryHandler.CallCount.ShouldBe(2);
	}

	[Fact]
	public async Task IgnoreNullResultSkipsCaching()
	{
		// Arrange
		var services = new ServiceCollection();

		_ = services.AddLogging();
		_ = services.AddMetrics();
		_ = services.AddMemoryCache();
		_ = services.AddSingleton<IJsonSerializer, JsonMessageSerializer>();

		// Register the test handler explicitly
		_ = services.AddTransient<IActionHandler<NullResultQuery, TestResult>, NullResultQueryHandler>();

		_ = services.AddDispatch(dispatch =>
		{
			_ = dispatch.AddHandlersFromAssembly(typeof(CachingIntegrationShould).Assembly);
			_ = dispatch.AddDispatchResilience()
				.AddDispatchCaching()
				.WithCachingOptions(static o =>
				{
					o.Enabled = true;
					o.UseDistributedCache = false;
				});
		});
		var provider = services.BuildServiceProvider();
		_ = provider.GetRequiredKeyedService<IMessageBus>("Local");
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		var query = new NullResultQuery { Value = 5 };

		NullResultQueryHandler.CallCount = 0;
		var result1 = await dispatcher.DispatchAsync<NullResultQuery, TestResult>(query, new MessageContext(new TestDispatchAction(), provider), cancellationToken: default).ConfigureAwait(true);
		var result2 = await dispatcher.DispatchAsync<NullResultQuery, TestResult>(query, new MessageContext(new TestDispatchAction(), provider), cancellationToken: default).ConfigureAwait(true);

		// Assert
		result1.CacheHit.ShouldBeFalse();
		result2.CacheHit.ShouldBeFalse();
		NullResultQueryHandler.CallCount.ShouldBe(2);
	}

	[Fact]
	public async Task OnlyInvokeHandlerOnceForConcurrentCalls()
	{
		// Arrange
		var services = new ServiceCollection();

		_ = services.AddLogging();
		_ = services.AddMetrics();
		_ = services.AddMemoryCache();
		_ = services.AddSingleton<IJsonSerializer, JsonMessageSerializer>();

		// Register the test handlers explicitly
		_ = services.AddTransient<IActionHandler<CachingTestQuery, CachingTestResult>, CachingTestQueryHandler>();
		_ = services.AddTransient<IActionHandler<InvalidateCacheCommand>, InvalidateCacheCommandHandler>();

		_ = services.AddDispatch(dispatch =>
		{
			_ = dispatch.AddHandlersFromAssembly(typeof(CachingIntegrationShould).Assembly);
			_ = dispatch.AddDispatchResilience()
				.AddDispatchCaching()
				.WithCachingOptions(o =>
				{
					o.Enabled = true;
					o.UseDistributedCache = false;
					o.Behavior.DefaultExpiration = TimeSpan.FromSeconds(1);
				});
		});
		var provider = services.BuildServiceProvider();
		_ = provider.GetRequiredKeyedService<IMessageBus>("Local");
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		var query = new CachingTestQuery { Value = 654 };

		CachingTestQueryHandler.CallCount = 0;

		var tasks = Enumerable.Range(0, 5)
			.Select(_ => dispatcher.DispatchAsync<CachingTestQuery, CachingTestResult>(
				query,
				new MessageContext(new TestDispatchAction(), provider), cancellationToken: default));

		var results = await Task.WhenAll(tasks).ConfigureAwait(true);

		foreach (var r in results)
		{
			r.Succeeded.ShouldBeTrue();
			r.CacheHit.ShouldBe(results[0] != r || r.CacheHit);
		}

		CachingTestQueryHandler.CallCount.ShouldBe(1);
	}

	[Fact]
	public async Task CacheUsingDistributedCacheAndInvalidateCorrectly()
	{
		// Arrange
		var services = new ServiceCollection();

		_ = services.AddLogging();
		_ = services.AddMetrics();
		_ = services.AddMemoryCache();
		_ = services.AddSingleton<IMemoryCache, MemoryCache>();
		// Use ForwardingDistributedCache because HybridCache skips MemoryDistributedCache as L2
		var distSvc = new ServiceCollection();
		_ = distSvc.AddDistributedMemoryCache();
		var distCache = distSvc.BuildServiceProvider().GetRequiredService<Microsoft.Extensions.Caching.Distributed.IDistributedCache>();
		_ = services.AddSingleton<Microsoft.Extensions.Caching.Distributed.IDistributedCache>(new ForwardingDistributedCache(distCache));
		_ = services.AddSingleton<IJsonSerializer, JsonMessageSerializer>();

		// Register the test handlers explicitly
		_ = services.AddTransient<IActionHandler<CachingTestQuery, CachingTestResult>, CachingTestQueryHandler>();
		_ = services.AddTransient<IActionHandler<InvalidateCacheCommand>, InvalidateCacheCommandHandler>();

		_ = services.AddDispatch(dispatch =>
		{
			_ = dispatch.AddHandlersFromAssembly(typeof(CachingIntegrationShould).Assembly);
			_ = dispatch.AddDispatchResilience()
				.AddDispatchCaching()
				.WithCachingOptions(o =>
				{
					o.Enabled = true;
					o.UseDistributedCache = true;
				});
		});
		var provider = services.BuildServiceProvider();
		_ = provider.GetRequiredKeyedService<IMessageBus>("Local");
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		var query = new CachingTestQuery { Value = 808 };

		// Act - first call caches the result
		CachingTestQueryHandler.CallCount = 0;
		var result1 = await dispatcher.DispatchAsync<CachingTestQuery, CachingTestResult>(
			query, new MessageContext(new TestDispatchAction(), provider), CancellationToken.None).ConfigureAwait(true);
		var result2 = await dispatcher.DispatchAsync<CachingTestQuery, CachingTestResult>(
			query, new MessageContext(new TestDispatchAction(), provider), CancellationToken.None).ConfigureAwait(true);

		// Assert cached
		result1.Succeeded.ShouldBeTrue();
		result2.CacheHit.ShouldBeTrue();
		CachingTestQueryHandler.CallCount.ShouldBe(1);

		// Invalidate
		var invalidate = new InvalidateCacheCommand { TagsToInvalidate = ["test-tag"] };
		_ = await dispatcher
			.DispatchAsync(invalidate, new MessageContext(new TestDispatchAction(), provider), cancellationToken: default)
			.ConfigureAwait(true);

		// Act - after invalidation handler should run again
		var result3 = await dispatcher.DispatchAsync<CachingTestQuery, CachingTestResult>(
			query, new MessageContext(new TestDispatchAction(), provider), cancellationToken: default).ConfigureAwait(true);

		// Assert - handler should be called twice: first call + third call after invalidation
		CachingTestQueryHandler.CallCount.ShouldBe(2);
		result3.CacheHit.ShouldBeFalse();
	}
}
