// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;

using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Caching;
using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Resilience.Polly;

using Excalibur.Application.Requests.Queries;

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
	public static int CallCount { get; set; }

	public Task<ConcurrencyTestResult> HandleAsync(ConcurrencyTestQuery message, CancellationToken cancellationToken = default)
	{
		CallCount++;
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
		_ = services.AddDistributedMemoryCache();
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
			r.CacheHit.ShouldBe(firstResults[0] != r || r.CacheHit);
		}

		ConcurrencyTestQueryHandler.CallCount.ShouldBe(1);

		// Invalidate concurrently
		var invalidate = new ConcurrencyInvalidateCacheCommand { TagsToInvalidate = ["test-tag"] };
		var invalidationTasks = Enumerable.Range(0, 3)
			.Select(_ => dispatcher.DispatchAsync(
				invalidate,
				new MessageContext(new TestDispatchAction(), provider), cancellationToken: default));
		_ = await Task.WhenAll(invalidationTasks).ConfigureAwait(true);

		// Act - dispatch again concurrently after invalidation
		var secondTasks = Enumerable.Range(0, 5)
			.Select(_ => dispatcher.DispatchAsync<ConcurrencyTestQuery, ConcurrencyTestResult>(
				query,
				new MessageContext(new TestDispatchAction(), provider), cancellationToken: default));
		var secondResults =
			await Task.WhenAll(secondTasks).ConfigureAwait(true);

		foreach (var r in secondResults)
		{
			r.Succeeded.ShouldBeTrue();
			r.CacheHit.ShouldBe(secondResults[0] != r || r.CacheHit);
		}

		// Assert - handler should have executed exactly twice
		ConcurrencyTestQueryHandler.CallCount.ShouldBe(2);
	}
}
