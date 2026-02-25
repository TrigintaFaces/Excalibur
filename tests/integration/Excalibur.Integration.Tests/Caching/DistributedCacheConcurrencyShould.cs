// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Caching;
using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Resilience.Polly;

using Excalibur.Application.Requests.Queries;
using Microsoft.Extensions.Caching.Distributed;

namespace Excalibur.Integration.Tests.Caching;

// Public test types for distributed cache concurrency tests (private nested types prevent HybridCache from working)
[CacheResult(Tags = ["test-tag"])]
public sealed class ConcurrencyTestQuery : QueryBase<ConcurrencyTestResult>, IDispatchAction<ConcurrencyTestResult>
{
	public int Value { get; init; }

	public override string ActivityDisplayName => "Distributed Concurrency Query";

	public override string ActivityDescription => "Query used for distributed cache concurrency tests";
}

public sealed class ConcurrencyTestResult
{
	public int Value { get; init; }
}

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes",
	Justification = "Instantiated via DI")]
public sealed class ConcurrencyTestQueryHandler : IActionHandler<ConcurrencyTestQuery, ConcurrencyTestResult>
{
	private static int _callCount;

	public static int CallCount
	{
		get => Volatile.Read(ref _callCount);
		set => Interlocked.Exchange(ref _callCount, value);
	}

	public Task<ConcurrencyTestResult> HandleAsync(ConcurrencyTestQuery message, CancellationToken cancellationToken = default)
	{
		_ = Interlocked.Increment(ref _callCount);
		var result = new ConcurrencyTestResult { Value = message.Value * 2 };
		return Task.FromResult(result);
	}
}

[SuppressMessage("Performance", "CA1812:Avoid uninstantiated internal classes",
	Justification = "Instantiated via DI")]
public sealed class ConcurrencyInvalidateCacheCommand : ICacheInvalidator, ITestCacheInvalidator, IDispatchAction
{
	public Guid Id => Guid.NewGuid();

	public string MessageId => Guid.NewGuid().ToString();

	public DateTimeOffset Timestamp => DateTimeOffset.UtcNow;

	public MessageKinds Kind => MessageKinds.Action;

	public IReadOnlyDictionary<string, object> Headers => new Dictionary<string, object>();

	public object Body => nameof(ConcurrencyInvalidateCacheCommand);

	public string MessageType => nameof(ConcurrencyInvalidateCacheCommand);

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
public sealed class ConcurrencyInvalidateCacheCommandHandler : IActionHandler<ConcurrencyInvalidateCacheCommand>
{
	public Task HandleAsync(ConcurrencyInvalidateCacheCommand action, CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>
/// Validates distributed caching with concurrent dispatches and invalidations.
/// </summary>
[Collection("CachingIntegrationTests")]
public sealed class DistributedCacheConcurrencyShould : IntegrationTestBase
{
	[Fact]
	public async Task HandleConcurrentDispatchesAndInvalidateCorrectly()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddMetrics();
		_ = services.AddMemoryCache();
		var distSvc = new ServiceCollection();
		_ = distSvc.AddDistributedMemoryCache();
		var distCache = distSvc.BuildServiceProvider().GetRequiredService<IDistributedCache>();
		_ = services.AddSingleton<IDistributedCache>(new ForwardingDistributedCache(distCache));
		_ = services.AddSingleton<IJsonSerializer, JsonMessageSerializer>();
		// Register the test handler explicitly
		_ = services.AddTransient<IActionHandler<ConcurrencyTestQuery, ConcurrencyTestResult>, ConcurrencyTestQueryHandler>();

		_ = services.AddDispatch(dispatch =>
		{
			_ = dispatch.AddHandlersFromAssembly(typeof(DistributedCacheConcurrencyShould).Assembly);
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

		var query = new ConcurrencyTestQuery { Value = 202 };
		ConcurrencyTestQueryHandler.CallCount = 0;

		// Act - dispatch query concurrently
		var firstTasks = Enumerable.Range(0, 5)
			.Select(_ => dispatcher.DispatchAsync<ConcurrencyTestQuery, ConcurrencyTestResult>(
				query,
				new MessageContext(new TestDispatchAction(), provider), cancellationToken: default));
		var firstResults =
			await Task.WhenAll(firstTasks).ConfigureAwait(true);

		foreach (var r in firstResults)
		{
			r.Succeeded.ShouldBeTrue();
		}

		// Ensure cache is warm before testing invalidation behavior.
		var warmedResult = await DispatchUntilCacheHitAsync(dispatcher, query, provider, TimeSpan.FromSeconds(5)).ConfigureAwait(true);
		warmedResult.CacheHit.ShouldBeTrue();
		var callCountBeforeInvalidation = ConcurrencyTestQueryHandler.CallCount;

		// Invalidate concurrently
		var invalidate = new ConcurrencyInvalidateCacheCommand { TagsToInvalidate = ["test-tag"] };
		var invalidationTasks = Enumerable.Range(0, 3)
			.Select(_ => dispatcher.DispatchAsync(
				invalidate,
				new MessageContext(new TestDispatchAction(), provider), cancellationToken: default));
		_ = await Task.WhenAll(invalidationTasks).ConfigureAwait(true);

		// Invalidation should force at least one recomputation once we dispatch again.
		var recomputedResult = await DispatchUntilCallCountIncreasesAsync(
			dispatcher,
			query,
			provider,
			callCountBeforeInvalidation,
			TimeSpan.FromSeconds(5)).ConfigureAwait(true);
		recomputedResult.Succeeded.ShouldBeTrue();

		var rewarmedResult = await DispatchUntilCacheHitAsync(dispatcher, query, provider, TimeSpan.FromSeconds(5)).ConfigureAwait(true);
		rewarmedResult.Succeeded.ShouldBeTrue();
		rewarmedResult.CacheHit.ShouldBeTrue();
		ConcurrencyTestQueryHandler.CallCount.ShouldBeGreaterThan(callCountBeforeInvalidation);
	}

	private static async Task<Excalibur.Dispatch.Abstractions.IMessageResult<ConcurrencyTestResult>> DispatchUntilCacheHitAsync(
		IDispatcher dispatcher,
		ConcurrencyTestQuery query,
		IServiceProvider provider,
		TimeSpan timeout)
	{
		var deadline = DateTimeOffset.UtcNow + timeout;
		Excalibur.Dispatch.Abstractions.IMessageResult<ConcurrencyTestResult>? lastResult = null;

		while (DateTimeOffset.UtcNow < deadline)
		{
			lastResult = await dispatcher.DispatchAsync<ConcurrencyTestQuery, ConcurrencyTestResult>(
				query,
				new MessageContext(new TestDispatchAction(), provider),
				cancellationToken: default).ConfigureAwait(true);

			if (lastResult.CacheHit)
			{
				return lastResult;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(50)).ConfigureAwait(true);
		}

		lastResult.ShouldNotBeNull();
		return lastResult!;
	}

	private static async Task<Excalibur.Dispatch.Abstractions.IMessageResult<ConcurrencyTestResult>> DispatchUntilCallCountIncreasesAsync(
		IDispatcher dispatcher,
		ConcurrencyTestQuery query,
		IServiceProvider provider,
		int baselineCallCount,
		TimeSpan timeout)
	{
		var deadline = DateTimeOffset.UtcNow + timeout;
		Excalibur.Dispatch.Abstractions.IMessageResult<ConcurrencyTestResult>? lastResult = null;

		while (DateTimeOffset.UtcNow < deadline)
		{
			lastResult = await dispatcher.DispatchAsync<ConcurrencyTestQuery, ConcurrencyTestResult>(
				query,
				new MessageContext(new TestDispatchAction(), provider),
				cancellationToken: default).ConfigureAwait(true);

			if (ConcurrencyTestQueryHandler.CallCount > baselineCallCount)
			{
				return lastResult;
			}

			await Task.Delay(TimeSpan.FromMilliseconds(50)).ConfigureAwait(true);
		}

		lastResult.ShouldNotBeNull();
		ConcurrencyTestQueryHandler.CallCount.ShouldBeGreaterThan(baselineCallCount);
		return lastResult!;
	}
}
