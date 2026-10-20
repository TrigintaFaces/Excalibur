// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Caching;
using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Resilience.Polly;

using Excalibur.Application.Requests.Queries;

namespace Excalibur.Integration.Tests.Caching;

// Public test types for caching integration tests (private nested types prevent HybridCache from working)
[CacheResult(Tags = ["test-tag"])]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
public sealed class CachingTestQuery : QueryBase<CachingTestResult>, IDispatchAction<CachingTestResult>
{
	public int Value { get; init; }

	public override string ActivityDisplayName => "Test Query";

	public override string ActivityDescription => "A test query for caching";
}

[Trait("Category", "Integration")]
[Trait("Component", "Core")]
public sealed class CachingTestResult
{
	public int Value { get; init; }

	public string Timestamp { get; init; } = DateTimeOffset.UtcNow.ToString("O");
}

[Trait("Category", "Integration")]
[Trait("Component", "Core")]
public sealed class CachingTestQueryHandler(CachingHandlerCallTracker<CachingTestQueryHandler> tracker) : IActionHandler<CachingTestQuery, CachingTestResult>
{
	public async Task<CachingTestResult> HandleAsync(
		CachingTestQuery message,
		CancellationToken cancellationToken = default)
	{
		tracker.Record();
		if (tracker.Gate is { } gate)
		{
			gate.Entered.TrySetResult();
			await gate.Release.Task.WaitAsync(cancellationToken);
		}
		var result = new CachingTestResult { Value = message.Value * 2 };
		return result;
	}
}

public sealed class CachingHandlerCallTracker<THandler>
{
	private int _callCount;

	public CachingHandlerGate? Gate { get; set; }

	public int CallCount => Volatile.Read(ref _callCount);

	public void Record() => Interlocked.Increment(ref _callCount);
}

public sealed class CachingHandlerGate
{
	public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

	public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
}

// Public test types for policy and middleware tests (private nested types prevent HybridCache from working)
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
public sealed class TestResult
{
	public int Value { get; init; }
}

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes",
	Justification = "Instantiated via DI")]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
public sealed class ConditionalCachePolicy : IResultCachePolicy<CachingTestQuery>
{
	public bool ShouldCache(CachingTestQuery message, object? result) =>
		// Only cache queries with Value >= 100
		message.Value >= 100;

	public TimeSpan GetCacheDuration(CachingTestQuery message) => TimeSpan.FromMinutes(5);
}

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes",
	Justification = "Instantiated via DI")]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
public sealed class TestMiddlewareCallTracker
{
	private int _callCount;

	public int CallCount => Volatile.Read(ref _callCount);

	public void Record() => Interlocked.Increment(ref _callCount);
}

/// <summary>
/// Counts through a Singleton <see cref="TestMiddlewareCallTracker"/>, never on itself: the pipeline resolves
/// a middleware registered with <c>UseMiddleware&lt;T&gt;</c> per dispatch, so the instance a test can
/// resolve from the root provider is not one the pipeline ever invokes.
/// </summary>
public sealed class TestTrackingMiddleware(TestMiddlewareCallTracker tracker) : IDispatchMiddleware
{
	public int CallCount => tracker.CallCount;

	public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.PreProcessing;

	public async ValueTask<IMessageResult> InvokeAsync(
		IDispatchMessage message,
		IMessageContext context,
		DispatchRequestDelegate nextDelegate,
		CancellationToken cancellationToken)
	{
		tracker.Record();
		return await nextDelegate(message, context, cancellationToken);
	}
}

[CacheResult(OnlyIfSuccess = true)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
public sealed class OnlyIfSuccessQuery : QueryBase<TestResult>, IDispatchAction<TestResult>
{
	public int Value { get; init; }

	public override string ActivityDisplayName => "OnlyIfSuccess Query";

	public override string ActivityDescription => "Query with OnlyIfSuccess";
}

[SuppressMessage("Performance", "CA1812", Justification = "Instantiated via DI")]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
public sealed class OnlyIfSuccessQueryHandler(CachingHandlerCallTracker<OnlyIfSuccessQueryHandler> tracker) : IActionHandler<OnlyIfSuccessQuery, TestResult>
{
	public Task<TestResult> HandleAsync(OnlyIfSuccessQuery message, CancellationToken cancellationToken = default)
	{
		tracker.Record();
		var result = new TestResult { Value = message.Value };
		return Task.FromResult(result);
	}
}

[CacheResult(IgnoreNullResult = true)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
public sealed class NullResultQuery : QueryBase<TestResult>, IDispatchAction<TestResult>
{
	public int Value { get; init; }

	public override string ActivityDisplayName => "Null Result Query";

	public override string ActivityDescription => "Query returning null";
}

[SuppressMessage("Performance", "CA1812", Justification = "Instantiated via DI")]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
public sealed class NullResultQueryHandler(CachingHandlerCallTracker<NullResultQueryHandler> tracker) : IActionHandler<NullResultQuery, TestResult>
{
	public Task<TestResult> HandleAsync(NullResultQuery message, CancellationToken cancellationToken = default)
	{
		tracker.Record();
		return Task.FromResult<TestResult>(null!);
	}
}

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes",
	Justification = "Instantiated via DI")]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
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
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
public sealed class InvalidateCacheCommandHandler : IActionHandler<InvalidateCacheCommand>
{
	public Task HandleAsync(InvalidateCacheCommand action, CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>
/// Integration tests for the caching functionality.
/// </summary>
[Collection("CachingIntegrationTests")]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
public sealed class CachingIntegrationShould : IntegrationTestBase
{
	[Fact]
	[Trait("Pattern", "Regression")]
	public async Task KeepHandlerCountsIndependentAcrossProviders()
	{
		var firstServices = new ServiceCollection();
		var secondServices = new ServiceCollection();
		firstServices.AddSingleton(typeof(CachingHandlerCallTracker<>));
		firstServices.AddTransient<CachingTestQueryHandler>();
		firstServices.AddTransient<OnlyIfSuccessQueryHandler>();
		firstServices.AddTransient<NullResultQueryHandler>();
		secondServices.AddSingleton(typeof(CachingHandlerCallTracker<>));
		secondServices.AddTransient<CachingTestQueryHandler>();
		secondServices.AddTransient<OnlyIfSuccessQueryHandler>();
		secondServices.AddTransient<NullResultQueryHandler>();
		await using var firstProvider = firstServices.BuildServiceProvider();
		await using var secondProvider = secondServices.BuildServiceProvider();
		var firstTracker = firstProvider.GetRequiredService<CachingHandlerCallTracker<CachingTestQueryHandler>>();
		var secondTracker = secondProvider.GetRequiredService<CachingHandlerCallTracker<CachingTestQueryHandler>>();
		firstTracker.Gate = new CachingHandlerGate();
		secondTracker.Gate = new CachingHandlerGate();
		var tasks = new[]
		{
			firstProvider.GetRequiredService<CachingTestQueryHandler>().HandleAsync(new CachingTestQuery { Value = 11 }),
			secondProvider.GetRequiredService<CachingTestQueryHandler>().HandleAsync(new CachingTestQuery { Value = 22 }),
		};
		try
		{
			await Task.WhenAll(firstTracker.Gate.Entered.Task, secondTracker.Gate.Entered.Task)
				.WaitAsync(TimeSpan.FromSeconds(30));
			tasks.All(static task => !task.IsCompleted).ShouldBeTrue();
		}
		finally
		{
			firstTracker.Gate.Release.TrySetResult();
			secondTracker.Gate.Release.TrySetResult();
			await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(30));
		}
		var results = await Task.WhenAll(tasks);
		results[0].Value.ShouldBe(22);
		results[1].Value.ShouldBe(44);
		await Task.WhenAll(
			firstProvider.GetRequiredService<OnlyIfSuccessQueryHandler>().HandleAsync(new OnlyIfSuccessQuery { Value = 11 }),
			secondProvider.GetRequiredService<OnlyIfSuccessQueryHandler>().HandleAsync(new OnlyIfSuccessQuery { Value = 22 }),
			firstProvider.GetRequiredService<NullResultQueryHandler>().HandleAsync(new NullResultQuery { Value = 11 }),
			secondProvider.GetRequiredService<NullResultQueryHandler>().HandleAsync(new NullResultQuery { Value = 22 }));
		firstProvider.GetRequiredService<CachingHandlerCallTracker<OnlyIfSuccessQueryHandler>>().CallCount.ShouldBe(1);
		secondProvider.GetRequiredService<CachingHandlerCallTracker<OnlyIfSuccessQueryHandler>>().CallCount.ShouldBe(1);
		firstProvider.GetRequiredService<CachingHandlerCallTracker<NullResultQueryHandler>>().CallCount.ShouldBe(1);
		secondProvider.GetRequiredService<CachingHandlerCallTracker<NullResultQueryHandler>>().CallCount.ShouldBe(1);
		firstProvider.GetRequiredService<CachingHandlerCallTracker<CachingTestQueryHandler>>().CallCount.ShouldBe(1, "the first provider owns only its handler invocation");
		secondProvider.GetRequiredService<CachingHandlerCallTracker<CachingTestQueryHandler>>().CallCount.ShouldBe(1, "the second provider owns only its handler invocation");
	}

	// The window ExpireEntriesAfterDefaultExpiration measures against. UNCHANGED value (300 ms), merely
	// hoisted so that test's inconclusive guard can name the exact window it is measuring against.
	// Lengthening it is deliberately NOT the fix: that would make the arm pass more often without fixing
	// the case where load exceeds whatever new value is chosen — a rarer flake, not a fixed one.
	private static readonly TimeSpan DefaultExpirationWindow = TimeSpan.FromMilliseconds(300);

	// The caching middleware JITTERS every entry's TTL by ±JitterRatio, so the configured 300 ms is a
	// midpoint, NOT a floor — the shortest TTL an entry can actually receive is 270 ms. Pinned here to
	// the product's own default (0.10) so behaviour is UNCHANGED and the floor below is exact rather
	// than inherited from a default that could drift. Deliberately not set to 0: that would LENGTHEN
	// the worst-case window from 270 ms to 300 ms, which is the fix this bead forbids.
	private const double ExpirationJitterRatio = 0.10;

	// The conservative FLOOR of the jittered window. The guard must compare the (upper-bounded) elapsed
	// against the SHORTEST life the entry could have been given, not the midpoint — otherwise an entry
	// handed a 270 ms TTL that legitimately expires at 280 ms slips under a 300 ms threshold and the arm
	// false-fails exactly as before. Upper-bounded burn vs lower-bounded window is what makes the
	// inconclusive verdict honest in both directions.
	private static readonly TimeSpan ShortestPossibleEntryLifetime =
		DefaultExpirationWindow * (1.0 - ExpirationJitterRatio);

	[Fact]
	public async Task CacheQueryResultsEndToEnd()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddSingleton(typeof(CachingHandlerCallTracker<>));

		// Add required services
		_ = services.AddLogging();
		_ = services.AddMetrics();
		_ = services.AddMemoryCache();
		_ = services.AddSingleton<IMemoryCache, MemoryCache>();
		_ = services.AddSingleton<DispatchJsonSerializer>();

		// Register the test handler explicitly
		_ = services.AddTransient<IActionHandler<CachingTestQuery, CachingTestResult>, CachingTestQueryHandler>();

		// Add dispatch and caching
		_ = services.AddDispatch(dispatch =>
		{
			_ = dispatch.AddHandlersFromAssembly(typeof(CachingIntegrationShould).Assembly);
			_ = dispatch.UseResilience();
			_ = dispatch.UseCaching()
				.WithCachingOptions(opt =>
				{
					opt.Enabled = true;
					opt.UseDistributedCache = false;
					opt.Behavior.DefaultExpiration = TimeSpan.FromMinutes(5);
				});
		});
		await using var provider = services.BuildServiceProvider();
		// Ensure the local bus is registered
		_ = provider.GetRequiredKeyedService<IMessageBus>("Local");
		var dispatcher = provider.GetRequiredService<IDispatcher>();
		var memoryCache = provider.GetRequiredService<IMemoryCache>();
		var contextAccessor = provider.GetRequiredService<IMessageContextAccessor>();

		var query = new CachingTestQuery { Value = 123 };


		// Act - First call should execute handler
		var testMessage = new TestDispatchAction();
		var context = new MessageContext(testMessage, provider);
		var result1 = await dispatcher.DispatchAsync<CachingTestQuery, CachingTestResult>(query, context, CancellationToken.None)
			;

		// Act - Second call should return cached result
		var result2 = await dispatcher.DispatchAsync<CachingTestQuery, CachingTestResult>(query, context, CancellationToken.None)
			;

		// Assert
		_ = result1.ShouldNotBeNull();
		_ = result2.ShouldNotBeNull();
		result1.Succeeded.ShouldBeTrue();
		result2.Succeeded.ShouldBeTrue();
		// HybridCache returns different instances due to serialization - compare values instead of references
		result1.ReturnValue.Value.ShouldBe(result2.ReturnValue.Value);

		// Verify handler was called only once
		provider.GetRequiredService<CachingHandlerCallTracker<CachingTestQueryHandler>>().CallCount.ShouldBe(1);
	}

	[Fact]
	public async Task InvalidateCacheWithAttribute()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddSingleton(typeof(CachingHandlerCallTracker<>));

		_ = services.AddLogging();
		_ = services.AddMetrics();
		_ = services.AddMemoryCache();
		_ = services.AddSingleton<DispatchJsonSerializer>();

		// Register the test handlers explicitly
		_ = services.AddTransient<IActionHandler<CachingTestQuery, CachingTestResult>, CachingTestQueryHandler>();
		_ = services.AddTransient<IActionHandler<InvalidateCacheCommand>, InvalidateCacheCommandHandler>();

		_ = services.AddDispatch(dispatch =>
		{
			_ = dispatch.AddHandlersFromAssembly(typeof(CachingIntegrationShould).Assembly);
			_ = dispatch.UseResilience()
				.UseCaching()
				.WithCachingOptions(o =>
				{
					o.UseDistributedCache = false;
					o.Enabled = true;
				});
		});
		await using var provider = services.BuildServiceProvider();
		// Ensure the local bus is registered
		_ = provider.GetRequiredKeyedService<IMessageBus>("Local");
		var dispatcher = provider.GetRequiredService<IDispatcher>();
		var testMessage = new TestDispatchAction();
		var context = new MessageContext(testMessage, provider);

		var query = new CachingTestQuery { Value = 456 };


		// Act - First call caches result
		var result1 = await dispatcher.DispatchAsync<CachingTestQuery, CachingTestResult>(query, context, CancellationToken.None)
			;


		// Act - Second call should use cache
		var result2 = await dispatcher.DispatchAsync<CachingTestQuery, CachingTestResult>(query, context, CancellationToken.None)
			;

		// Assert HybridCache returns different instances due to serialization - compare values instead of references
		result1.ReturnValue.Value.ShouldBe(result2.ReturnValue.Value);
		provider.GetRequiredService<CachingHandlerCallTracker<CachingTestQueryHandler>>().CallCount.ShouldBe(1); // Handler not called again
	}

	[Fact]
	public async Task RespectCachePolicyDecisions()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddSingleton(typeof(CachingHandlerCallTracker<>));

		_ = services.AddLogging();
		_ = services.AddMetrics();
		_ = services.AddMemoryCache();
		_ = services.AddSingleton<DispatchJsonSerializer>();

		// Register the test handler explicitly
		_ = services.AddTransient<IActionHandler<CachingTestQuery, CachingTestResult>, CachingTestQueryHandler>();
		_ = services.AddTransient<ConditionalCachePolicy>();

		_ = services.AddDispatch(dispatch =>
		{
			_ = dispatch.AddHandlersFromAssembly(typeof(CachingIntegrationShould).Assembly);
			_ = dispatch.UseResilience()
				.UseCaching()
				.WithCachingOptions(static o =>
				{
					o.Enabled = true;
					o.UseDistributedCache = false;
				})
				.WithResultCachePolicy<CachingTestQuery, ConditionalCachePolicy>();
		});
		await using var provider = services.BuildServiceProvider();
		// Ensure the local bus is registered
		_ = provider.GetRequiredKeyedService<IMessageBus>("Local");
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		// Act - Query with MessageId < 100 should not be cached
		var query1 = new CachingTestQuery { Value = 50 };
		var result1a = await dispatcher.DispatchAsync<CachingTestQuery, CachingTestResult>(query1, new MessageContext(new TestDispatchAction(), provider), cancellationToken: default);
		var result1b = await dispatcher.DispatchAsync<CachingTestQuery, CachingTestResult>(query1, new MessageContext(new TestDispatchAction(), provider), cancellationToken: default);

		// Act - Query with MessageId >= 100 should be cached
		var query2 = new CachingTestQuery { Value = 150 };
		var result2a = await dispatcher.DispatchAsync<CachingTestQuery, CachingTestResult>(query2, new MessageContext(new TestDispatchAction(), provider), cancellationToken: default);
		var result2b = await dispatcher.DispatchAsync<CachingTestQuery, CachingTestResult>(query2, new MessageContext(new TestDispatchAction(), provider), cancellationToken: default);

		// Assert - handler should be called 3 times: 2 for query1 (not cached), 1 for query2 (cached on second call)
		provider.GetRequiredService<CachingHandlerCallTracker<CachingTestQueryHandler>>().CallCount.ShouldBe(3);

		// Non-cached: both calls execute handler, returning fresh results
		result1a.ReturnValue.Value.ShouldBe(100); // 50 * 2
		result1b.ReturnValue.Value.ShouldBe(100); // 50 * 2

		// Cached: second call returns same value as first without executing handler
		result2a.ReturnValue.Value.ShouldBe(300); // 150 * 2
		result2b.ReturnValue.Value.ShouldBe(300); // 150 * 2
		result2b.Disposition.ShouldBe(MessageDisposition.ServedFromCache);
	}

	[Fact]
	public async Task HandleCachingWithMultipleMiddleware()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddSingleton(typeof(CachingHandlerCallTracker<>));

		_ = services.AddLogging();
		_ = services.AddMetrics();
		_ = services.AddMemoryCache();
		_ = services.AddSingleton<DispatchJsonSerializer>();

		// Register the test handlers explicitly
		_ = services.AddTransient<IActionHandler<CachingTestQuery, CachingTestResult>, CachingTestQueryHandler>();
		_ = services.AddTransient<IActionHandler<InvalidateCacheCommand>, InvalidateCacheCommandHandler>();

		// Register as singleton so we can retrieve the instance for assertions
		_ = services.AddSingleton<TestMiddlewareCallTracker>();

		_ = services.AddDispatch(dispatch =>
		{
			_ = dispatch.AddHandlersFromAssembly(typeof(CachingIntegrationShould).Assembly);
			_ = dispatch.UseMiddleware<TestTrackingMiddleware>()
				.UseResilience()
				.UseCaching()
				.WithCachingOptions(o =>
				{
					o.Enabled = true;
					o.UseDistributedCache = false;
				});
		});
		await using var provider = services.BuildServiceProvider();
		// Ensure the local bus is registered
		_ = provider.GetRequiredKeyedService<IMessageBus>("Local");
		var dispatcher = provider.GetRequiredService<IDispatcher>();
		var trackingMiddleware = provider.GetRequiredService<TestMiddlewareCallTracker>();

		var query = new CachingTestQuery { Value = 789 };

		// Act
		_ = await dispatcher.DispatchAsync<CachingTestQuery, CachingTestResult>(query, new MessageContext(new TestDispatchAction(), provider), CancellationToken.None)
			;
		_ = await dispatcher.DispatchAsync<CachingTestQuery, CachingTestResult>(query, new MessageContext(new TestDispatchAction(), provider), CancellationToken.None)
			; // Cached

		// Assert
		trackingMiddleware.CallCount.ShouldBe(2); // Middleware called twice
		provider.GetRequiredService<CachingHandlerCallTracker<CachingTestQueryHandler>>().CallCount.ShouldBe(1); // Handler called once (cached)
	}

	[Fact]
	public async Task ExpireEntriesAfterDefaultExpiration()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddSingleton(typeof(CachingHandlerCallTracker<>));

		_ = services.AddLogging();
		_ = services.AddMetrics();
		_ = services.AddMemoryCache();
		_ = services.AddSingleton<DispatchJsonSerializer>();

		// Register the test handlers explicitly
		_ = services.AddTransient<IActionHandler<CachingTestQuery, CachingTestResult>, CachingTestQueryHandler>();
		_ = services.AddTransient<IActionHandler<InvalidateCacheCommand>, InvalidateCacheCommandHandler>();

		_ = services.AddDispatch(dispatch =>
		{
			_ = dispatch.AddHandlersFromAssembly(typeof(CachingIntegrationShould).Assembly);
			_ = dispatch.UseResilience()
				.UseCaching()
				.WithCachingOptions(o =>
				{
					o.Enabled = true;
					o.UseDistributedCache = false;
					o.Behavior.DefaultExpiration = DefaultExpirationWindow;
					o.Behavior.JitterRatio = ExpirationJitterRatio;
				});
		});
		await using var provider = services.BuildServiceProvider();
		_ = provider.GetRequiredKeyedService<IMessageBus>("Local");
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		var query = new CachingTestQuery { Value = 321 };

		// Act - first call caches the result

		// Mark BEFORE the first dispatch. The entry's expiry is stamped when the caching middleware WRITES
		// the entry *during* that first round trip, i.e. at or after this mark — so elapsed-from-here is a
		// conservative UPPER bound on how much of the entry's life has burned by the time the cached-hit arm
		// below is evaluated. Bounding it in that direction is what keeps the inconclusive guard honest: it
		// can only ever over-estimate the burn, never under-estimate it into a false "the arm discriminated".
		var sinceEntryCached = Stopwatch.StartNew();

		var result1 = await dispatcher.DispatchAsync<CachingTestQuery, CachingTestResult>(query, new MessageContext(new TestDispatchAction(), provider), CancellationToken.None)
			;
		var result2 = await dispatcher.DispatchAsync<CachingTestQuery, CachingTestResult>(query, new MessageContext(new TestDispatchAction(), provider), CancellationToken.None)
			;
		var elapsedCacheToObservation = sinceEntryCached.Elapsed;

		// Assert cached. This pair is a SAFETY arm: it asserts the entry has NOT yet expired, and it is the
		// DISCRIMINATOR for the expiry arm below — without it, a cache that never serves a hit at all would
		// sail through the re-dispatch poll on its first attempt and the whole test would pass vacuously, so
		// it must stay. But a safety arm is only meaningful if its observation demonstrably landed INSIDE the
		// window it asserts. Two full dispatcher round trips under CI load can exceed a 300 ms expiration, in
		// which case the entry legitimately expired, a miss on the second dispatch is CORRECT behaviour, and
		// this arm would be reporting a defect that does not exist. When the measurement cannot tell those
		// apart, say so instead of accusing the product.
		var servedFromCache = provider.GetRequiredService<CachingHandlerCallTracker<CachingTestQueryHandler>>().CallCount == 1 && result2.Disposition == MessageDisposition.ServedFromCache;
		if (!servedFromCache && elapsedCacheToObservation >= ShortestPossibleEntryLifetime)
		{
			Assert.Fail(
				$"INCONCLUSIVE — this SAFETY arm could not run, and this is NOT a product-defect report. The "
				+ $"two back-to-back dispatches took {elapsedCacheToObservation.TotalMilliseconds:F0} ms, "
				+ $"which already reaches the {ShortestPossibleEntryLifetime.TotalMilliseconds:F0} ms floor "
				+ $"of the jittered {DefaultExpirationWindow.TotalMilliseconds:F0} ms expiration, so a cache "
				+ $"MISS here is equally explained by an entry that legitimately expired "
				+ $"under load and by a cache that never served the entry at all. The arm cannot discriminate; "
				+ $"re-run on a less loaded host. Deliberately NOT fixed by lengthening the expiration — that "
				+ $"would only make this rarer, not correct. (handler calls: "
				+ $"{provider.GetRequiredService<CachingHandlerCallTracker<CachingTestQueryHandler>>().CallCount}, result2.Disposition: {result2.Disposition})");
		}

		provider.GetRequiredService<CachingHandlerCallTracker<CachingTestQueryHandler>>().CallCount.ShouldBe(
			1,
			$"the second dispatch must be served from cache, so the handler runs exactly once (measured "
			+ $"cache-write→observation elapsed: {elapsedCacheToObservation.TotalMilliseconds:F0} ms — "
			+ $"inside the {ShortestPossibleEntryLifetime.TotalMilliseconds:F0} ms floor of the jittered "
			+ $"{DefaultExpirationWindow.TotalMilliseconds:F0} ms expiration, so this arm DID "
			+ $"discriminate)");
		result2.Disposition.ShouldBe(
			MessageDisposition.ServedFromCache,
			$"the entry is still inside the {ShortestPossibleEntryLifetime.TotalMilliseconds:F0} ms floor of "
			+ $"its jittered {DefaultExpirationWindow.TotalMilliseconds:F0} ms window "
			+ $"(measured elapsed: {elapsedCacheToObservation.TotalMilliseconds:F0} ms), so the second "
			+ $"dispatch must report a cache hit");

		// Act - after expiration the handler should eventually run again.
		var result3 = await DispatchUntilHandlerRunsAgainAsync(
			dispatcher,
			query,
			provider,
			baselineCallCount: 1,
			TimeSpan.FromSeconds(5));

		// Assert
		provider.GetRequiredService<CachingHandlerCallTracker<CachingTestQueryHandler>>().CallCount.ShouldBe(2);
		result3.Disposition.ShouldBe(MessageDisposition.Handled);
		result3.ReturnValue.Timestamp.ShouldNotBe(result1.ReturnValue.Timestamp);
	}

	[Fact]
	public async Task NotCacheWhenValidationFailsWithOnlyIfSuccess()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddSingleton(typeof(CachingHandlerCallTracker<>));

		_ = services.AddLogging();
		_ = services.AddMetrics();
		_ = services.AddMemoryCache();
		_ = services.AddSingleton<DispatchJsonSerializer>();

		// Register the test handler explicitly
		_ = services.AddTransient<IActionHandler<OnlyIfSuccessQuery, TestResult>, OnlyIfSuccessQueryHandler>();

		_ = services.AddDispatch(dispatch =>
		{
			_ = dispatch.AddHandlersFromAssembly(typeof(CachingIntegrationShould).Assembly);
			_ = dispatch.UseResilience()
				.UseCaching()
				.WithCachingOptions(static o =>
				{
					o.Enabled = true;
					o.UseDistributedCache = false;
				});
		});
		await using var provider = services.BuildServiceProvider();
		_ = provider.GetRequiredKeyedService<IMessageBus>("Local");
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		var query = new OnlyIfSuccessQuery { Value = 99 };

		var testMessage1 = new TestDispatchAction();
		var context1 = new MessageContext(testMessage1, provider);
		// Set validation result via extension method (Items dictionary) so caching middleware can read it
		Excalibur.Dispatch.MessageContextExtensions.ValidationResult(context1, SerializableValidationResult.Failed("bad"));
		var testMessage2 = new TestDispatchAction();
		var context2 = new MessageContext(testMessage2, provider);
		Excalibur.Dispatch.MessageContextExtensions.ValidationResult(context2, SerializableValidationResult.Failed("bad"));

		_ = await dispatcher.DispatchAsync<OnlyIfSuccessQuery, TestResult>(query, context1, cancellationToken: default);
		_ = await dispatcher.DispatchAsync<OnlyIfSuccessQuery, TestResult>(query, context2, cancellationToken: default);

		// Assert
		provider.GetRequiredService<CachingHandlerCallTracker<OnlyIfSuccessQueryHandler>>().CallCount.ShouldBe(2);
	}

	[Fact]
	public async Task IgnoreNullResultSkipsCaching()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddSingleton(typeof(CachingHandlerCallTracker<>));

		_ = services.AddLogging();
		_ = services.AddMetrics();
		_ = services.AddMemoryCache();
		_ = services.AddSingleton<DispatchJsonSerializer>();

		// Register the test handler explicitly
		_ = services.AddTransient<IActionHandler<NullResultQuery, TestResult>, NullResultQueryHandler>();
		_ = services.AddDispatch(dispatch =>
		{
			_ = dispatch.AddHandlersFromAssembly(typeof(CachingIntegrationShould).Assembly);

			_ = dispatch.UseResilience()
				.UseCaching()
				.WithCachingOptions(static o =>
				{
					o.Enabled = true;
					o.UseDistributedCache = false;
				});
		});
		await using var provider = services.BuildServiceProvider();
		_ = provider.GetRequiredKeyedService<IMessageBus>("Local");
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		var query = new NullResultQuery { Value = 5 };

		var result1 = await dispatcher.DispatchAsync<NullResultQuery, TestResult>(query, new MessageContext(new TestDispatchAction(), provider), cancellationToken: default);
		var result2 = await dispatcher.DispatchAsync<NullResultQuery, TestResult>(query, new MessageContext(new TestDispatchAction(), provider), cancellationToken: default);

		// A THIRD dispatch is the real proof. The subject of this test is "a null result is never cached",
		// and a cache miss on every subsequent dispatch demonstrates that directly — an implementation that
		// cached the null would serve a hit here.
		var result3 = await dispatcher.DispatchAsync<NullResultQuery, TestResult>(query, new MessageContext(new TestDispatchAction(), provider), cancellationToken: default);

		// Assert
		result1.Disposition.ShouldBe(MessageDisposition.Handled);
		result2.Disposition.ShouldBe(MessageDisposition.Handled);
		result3.Disposition.ShouldBe(
			MessageDisposition.Handled,
			"a null result must never be cached, no matter how many times it is requested");

		// AT LEAST once per dispatch, not EXACTLY. The previous `ShouldBe(2)` was not a statement about
		// caching at all — it asserted that nothing anywhere invokes the handler out of band, and that is
		// false by design: HybridCache's stampede protection runs
		// DefaultHybridCache.StampedeState.BackgroundFetchAsync, which calls the value factory (and so the
		// handler) on a BACKGROUND task, independent of any awaited dispatch. Captured stack, from a
		// reproduction:
		//
		//     DefaultHybridCache.StampedeState`2.BackgroundFetchAsync()
		//       -> CachingMiddleware.HandleAttributeCacheableAsync
		//         -> CreateAttributeCacheValueAsync -> HandlerInvoker -> NullResultQueryHandler
		//
		// Whether that background fetch lands before this assertion is a race, which is exactly why the
		// count was observed as 2, 3 and 4 intermittently — including with the test running alone. Pinning
		// an exact count here does not test caching; it tests scheduler timing, and fails ~1 run in 4.
		//
		// The lower bound still carries the load-bearing claim: the null result did NOT short-circuit
		// dispatch, so the handler really was consulted for each call rather than served from cache.
		provider.GetRequiredService<CachingHandlerCallTracker<NullResultQueryHandler>>().CallCount.ShouldBeGreaterThanOrEqualTo(
			3,
			"a non-cached result must reach the handler on every dispatch");
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	[Trait("Pattern", "Regression")]
	public async Task CollapseConcurrentCallsOnlyWhenCachingIsEnabled(bool cachingEnabled)
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddSingleton(typeof(CachingHandlerCallTracker<>));

		_ = services.AddLogging();
		_ = services.AddMetrics();
		_ = services.AddMemoryCache();
		_ = services.AddSingleton<DispatchJsonSerializer>();

		// Register the test handlers explicitly
		_ = services.AddTransient<IActionHandler<CachingTestQuery, CachingTestResult>, CachingTestQueryHandler>();
		_ = services.AddTransient<IActionHandler<InvalidateCacheCommand>, InvalidateCacheCommandHandler>();

		_ = services.AddDispatch(dispatch =>
		{
			_ = dispatch.AddHandlersFromAssembly(typeof(CachingIntegrationShould).Assembly);
			_ = dispatch.UseResilience()
				.UseCaching()
				.WithCachingOptions(o =>
				{
					o.Enabled = cachingEnabled;
					o.UseDistributedCache = false;

					// Long enough that expiry cannot participate in the outcome. The subject here is
					// single-flight COLLAPSE, not lifetime: at 1 second this asserted "collapsed" and
					// "did not expire" at once, so under load -- where the dispatches below stagger --
					// the entry could lapse mid-flight and a follower would legitimately re-enter the
					// handler. That reports as a stampede failure and is not one.
					o.Behavior.DefaultExpiration = TimeSpan.FromMinutes(5);
				});
		});
		await using var provider = services.BuildServiceProvider();
		_ = provider.GetRequiredKeyedService<IMessageBus>("Local");
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		var query = new CachingTestQuery { Value = 654 };
		var tracker = provider.GetRequiredService<CachingHandlerCallTracker<CachingTestQueryHandler>>();
		var gate = new CachingHandlerGate();
		tracker.Gate = gate;
		// Every actual handler entry increments before waiting. The gate holds work open without
		// coalescing it; disabling caching below must expose all five invocations.
		var tasks = new List<Task<IMessageResult<CachingTestResult>>>();
		try
		{
			for (var index = 0; index < 5; index++)
			{
				tasks.Add(dispatcher.DispatchAsync<CachingTestQuery, CachingTestResult>(
					query, new MessageContext(new TestDispatchAction(), provider), cancellationToken: default));
			}
			await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(30));
			tasks.All(static task => !task.IsCompleted).ShouldBeTrue();
		}
		finally
		{
			gate.Release.TrySetResult();
			await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(30));
		}
		var results = await Task.WhenAll(tasks);

		foreach (var r in results)
		{
			// ErrorMessage is included because this assertion has failed intermittently ("False should be
			// True") while discarding the one field that explains why.
			r.Succeeded.ShouldBeTrue(
				"a concurrent dispatch must not fail; ErrorMessage: " + (r.ErrorMessage ?? "(none)"));
			r.ReturnValue.Value.ShouldBe(1308);
		}

		// Exactly one dispatch reaches the handler; the rest are collapsed by single-flight. Asserting the
		// count is what actually tests stampede protection.
		//
		// The previous per-result cache assertion was `r.CacheHit.ShouldBe(results[0] != r || r.CacheHit)`,
		// which compares a value against an expression containing that same value: for results[0] it reduces
		// to `r.CacheHit == r.CacheHit`, true for either outcome, and for the others it demanded a cache hit
		// that a collapsed follower does not necessarily report. It could not fail in the interesting
		// direction, so it was removed rather than kept as decoration.
		tracker.CallCount.ShouldBe(cachingEnabled ? 1 : 5);
		var laterResult = await dispatcher.DispatchAsync<CachingTestQuery, CachingTestResult>(
			query, new MessageContext(new TestDispatchAction(), provider), cancellationToken: default);
		laterResult.Succeeded.ShouldBeTrue(laterResult.ErrorMessage);
		laterResult.ReturnValue.Value.ShouldBe(1308);
		tracker.CallCount.ShouldBe(cachingEnabled ? 1 : 6);
		laterResult.Disposition.ShouldBe(cachingEnabled ? MessageDisposition.ServedFromCache : MessageDisposition.Handled);
	}

	[Fact]
	public async Task CacheUsingDistributedCacheAndInvalidateCorrectly()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddSingleton(typeof(CachingHandlerCallTracker<>));

		_ = services.AddLogging();
		_ = services.AddMetrics();
		_ = services.AddMemoryCache();
		_ = services.AddSingleton<IMemoryCache, MemoryCache>();
		// Use ForwardingDistributedCache because HybridCache skips MemoryDistributedCache as L2
		var distSvc = new ServiceCollection();
		_ = distSvc.AddDistributedMemoryCache();
		// Held and disposed with the test: the cache instance must outlive resolution (it backs the
		// ForwardingDistributedCache registered below) but must NOT outlive the test.
		await using var distProvider = distSvc.BuildServiceProvider();
		var distCache = distProvider.GetRequiredService<Microsoft.Extensions.Caching.Distributed.IDistributedCache>();
		_ = services.AddSingleton<Microsoft.Extensions.Caching.Distributed.IDistributedCache>(new ForwardingDistributedCache(distCache));
		_ = services.AddSingleton<DispatchJsonSerializer>();

		// Register the test handlers explicitly
		_ = services.AddTransient<IActionHandler<CachingTestQuery, CachingTestResult>, CachingTestQueryHandler>();
		_ = services.AddTransient<IActionHandler<InvalidateCacheCommand>, InvalidateCacheCommandHandler>();

		_ = services.AddDispatch(dispatch =>
		{
			_ = dispatch.AddHandlersFromAssembly(typeof(CachingIntegrationShould).Assembly);
			_ = dispatch.UseResilience()
				.UseCaching()
				.WithCachingOptions(o =>
				{
					o.Enabled = true;
					o.UseDistributedCache = true;
				});
		});
		await using var provider = services.BuildServiceProvider();
		_ = provider.GetRequiredKeyedService<IMessageBus>("Local");
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		var query = new CachingTestQuery { Value = 808 };
		var context = new MessageContext(new TestDispatchAction(), provider);

		// Act - first call caches the result
		var result1 = await dispatcher.DispatchAsync<CachingTestQuery, CachingTestResult>(
			query, context, CancellationToken.None);
		var result2 = await dispatcher.DispatchAsync<CachingTestQuery, CachingTestResult>(
			query, context, CancellationToken.None);

		// Assert cached
		result1.Succeeded.ShouldBeTrue();
		result2.Disposition.ShouldBe(MessageDisposition.ServedFromCache);
		provider.GetRequiredService<CachingHandlerCallTracker<CachingTestQueryHandler>>().CallCount.ShouldBe(1);

		// Invalidate
		var invalidate = new InvalidateCacheCommand { TagsToInvalidate = ["test-tag"] };
		_ = await dispatcher
			.DispatchAsync(invalidate, context, cancellationToken: default)
			;

		// Act - after invalidation handler should run again
		// Use polling helper because HybridCache tag invalidation may be eventually consistent
		var result3 = await DispatchUntilHandlerRunsAgainAsync(
			dispatcher, query, provider, baselineCallCount: 1, timeout: TimeSpan.FromSeconds(10));

		// Assert - handler should be called twice: first call + third call after invalidation
		provider.GetRequiredService<CachingHandlerCallTracker<CachingTestQueryHandler>>().CallCount.ShouldBe(2);
		result3.Disposition.ShouldBe(MessageDisposition.Handled);
	}

	private static async Task<IMessageResult<CachingTestResult>> DispatchUntilHandlerRunsAgainAsync(
		IDispatcher dispatcher,
		CachingTestQuery query,
		IServiceProvider provider,
		int baselineCallCount,
		TimeSpan timeout)
	{
		var deadline = DateTimeOffset.UtcNow + timeout;
		IMessageResult<CachingTestResult>? lastResult = null;

		while (DateTimeOffset.UtcNow < deadline)
		{
			lastResult = await dispatcher.DispatchAsync<CachingTestQuery, CachingTestResult>(
				query,
				new MessageContext(new TestDispatchAction(), provider),
				CancellationToken.None);

			if (provider.GetRequiredService<CachingHandlerCallTracker<CachingTestQueryHandler>>().CallCount > baselineCallCount)
			{
				return lastResult;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(50));
		}

		lastResult.ShouldNotBeNull();
		provider.GetRequiredService<CachingHandlerCallTracker<CachingTestQueryHandler>>().CallCount.ShouldBeGreaterThan(baselineCallCount);
		return lastResult!;
	}
}
