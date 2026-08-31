// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Application.Requests.Queries;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Caching;
using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Resilience.Polly;
using Excalibur.Dispatch.Transport;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Integration.Tests.Caching;

// Public, top-level: private nested types prevent HybridCache from working, so a probe built from them
// would report a stampede for a reason that has nothing to do with the mechanism under test.
[CacheResult]
public sealed class SlowProbeQuery : QueryBase<SlowProbeResult>, IDispatchAction<SlowProbeResult>
{
	public int Value { get; init; }
}

public sealed class SlowProbeResult
{
	public int Value { get; init; }
}

public sealed class SlowProbeHandler : IActionHandler<SlowProbeQuery, SlowProbeResult>
{
	private static int _callCount;

	/// <summary>Total handler invocations; incremented atomically because callers race by design.</summary>
	public static int CallCount => Volatile.Read(ref _callCount);

	/// <summary>Resets the counter between arms.</summary>
	public static void ResetCallCount() => Volatile.Write(ref _callCount, 0);

	/// <summary>Comfortably longer than the 200 ms default cache timeout.</summary>
	public static TimeSpan Delay { get; set; } = TimeSpan.FromMilliseconds(400);

	public async Task<SlowProbeResult> HandleAsync(SlowProbeQuery message, CancellationToken cancellationToken)
	{
		_ = Interlocked.Increment(ref _callCount);
		await Task.Delay(Delay, cancellationToken).ConfigureAwait(false);
		return new SlowProbeResult { Value = message.Value * 2 };
	}
}

/// <summary>
/// Determines whether stampede protection survives a handler slower than the cache timeout.
/// </summary>
/// <remarks>
/// CachingMiddleware wraps HybridCache.GetOrCreateAsync in a CancellationTokenSource bounded by
/// Behavior.CacheTimeout, which defaults to 200 ms. HybridCache runs the value factory — our handler —
/// INSIDE that operation, so the bound applies to handler execution as well as to cache-backend latency.
/// On timeout the middleware deliberately fails open and executes the handler directly.
/// <para>
/// If that is what defeats stampede protection, a handler slower than the timeout must break it every
/// time, with no load and no timing luck required. That is what these arms measure, and the second arm
/// changes only the timeout — so a difference between them isolates the timeout as the cause rather than
/// the caching implementation, the test shape, or machine load.
/// </para>
/// </remarks>
public sealed class SlowHandlerStampedeProbe
{
	private static async Task<int> CountHandlerInvocationsAsync(TimeSpan cacheTimeout)
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddMetrics();
		_ = services.AddMemoryCache();
		_ = services.AddSingleton<DispatchJsonSerializer>();
		_ = services.AddTransient<IActionHandler<SlowProbeQuery, SlowProbeResult>, SlowProbeHandler>();

		_ = services.AddDispatch(dispatch =>
		{
			_ = dispatch.AddHandlersFromAssembly(typeof(SlowHandlerStampedeProbe).Assembly);
			_ = dispatch.UseResilience()
				.UseCaching()
				.WithCachingOptions(o =>
				{
					o.Enabled = true;
					o.UseDistributedCache = false;
					o.Behavior.DefaultExpiration = TimeSpan.FromMinutes(5);
					o.Behavior.CacheTimeout = cacheTimeout;
				});
		});

		await using var provider = services.BuildServiceProvider();
		_ = provider.GetRequiredKeyedService<IMessageBus>("Local");
		var dispatcher = provider.GetRequiredService<IDispatcher>();

		SlowProbeHandler.ResetCallCount();

		var query = new SlowProbeQuery { Value = 7 };
		var tasks = Enumerable.Range(0, 5)
			.Select(_ => dispatcher.DispatchAsync<SlowProbeQuery, SlowProbeResult>(
				query,
				new MessageContext(new TestDispatchAction(), provider),
				cancellationToken: default))
			.ToList();

		_ = await Task.WhenAll(tasks);
		return SlowProbeHandler.CallCount;
	}

	[Fact]
	public async Task CollapseConcurrentCallsEvenWhenTheHandlerOutlastsTheCacheTimeout()
	{
		// A 400 ms handler against the 200 ms default: the ordinary shape of any handler doing real I/O.
		// Caching exists to protect expensive operations, so protection lost for anything slower than the
		// timeout is protection absent exactly where it matters most.
		var invocations = await CountHandlerInvocationsAsync(TimeSpan.FromMilliseconds(200));

		invocations.ShouldBe(
			1,
			$"5 concurrent dispatches of one cacheable query invoked the handler {invocations} times with the "
			+ "default 200 ms CacheTimeout and a 400 ms handler -- stampede protection defeated by the very "
			+ "slowness it exists to absorb");
	}

	[Fact]
	public async Task CollapseConcurrentCallsWhenTheTimeoutComfortablyExceedsTheHandler()
	{
		// CONTROL. Same handler, same concurrency; only the timeout differs.
		var invocations = await CountHandlerInvocationsAsync(TimeSpan.FromSeconds(30));

		invocations.ShouldBe(
			1,
			"with a timeout far larger than the handler, single-flight must collapse all five callers into "
			+ "one invocation; if this fails too then the timeout is not the mechanism");
	}
}
