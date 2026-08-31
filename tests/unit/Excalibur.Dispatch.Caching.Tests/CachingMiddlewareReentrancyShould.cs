// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable IL2026, IL3050 // Suppress for test - RequiresUnreferencedCode/RequiresDynamicCode

using System.Diagnostics.Metrics;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Caching;

using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using StackExchange.Redis;

using Tests.Shared.Infrastructure;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Caching.Tests;

/// <summary>
/// Regression lock: a cached handler that dispatches a message resolving to the SAME cache key must fail
/// fast with a diagnosable error instead of deadlocking.
/// </summary>
/// <remarks>
/// <para>
/// <b>The cycle.</b> <see cref="HybridCache"/> collapses concurrent callers of one key onto a single
/// in-flight operation and runs the value factory — here, the handler — inside it. A handler whose result
/// is cached that dispatches, directly or transitively, a message resolving to that same key therefore
/// joins its own stampede: the inner call awaits a completion only the outer call can produce, and the
/// outer cannot complete until the inner returns. That is a cycle in the wait-for graph, and it does not
/// resolve on its own.
/// </para>
/// <para>
/// <b>Why it matters now.</b> The cycle used to be survivable by accident: a per-caller deadline sat
/// around the cache call and converted it into a timeout plus a duplicate handler execution. That deadline
/// was removed — correctly, because it also bounded handler execution and destroyed stampede protection —
/// which leaves the cycle unmasked and permanent.
/// </para>
/// <para>
/// <b>The trigger is key identity, not message identity.</b> A handler does not have to dispatch itself.
/// On the <see cref="ICacheable{T}"/> path the base key is <c>GetCacheKey()</c> verbatim, so it takes only
/// two different queries whose keys happen to match — <c>$"user:{UserId}"</c> on a summary query and a
/// balance query is ordinary code, not a contrivance. The fixtures below are exactly that shape.
/// </para>
/// <para>
/// <b>What is asserted.</b> Observable behaviour only — the exception a consumer sees and the key it
/// names. Nothing here references the guard's internals, so a future fix that prevents the cycle by some
/// other mechanism still satisfies these arms.
/// </para>
/// <para>
/// Authored by TestsDeveloper, independently of the implementer, against the real
/// <see cref="DefaultCacheKeyBuilder"/> and a real in-memory <see cref="HybridCache"/>.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Feature", "Caching")]
public sealed class CachingMiddlewareReentrancyShould : IDisposable
{
	/// <summary>
	/// The shared logical key. Two DIFFERENT query types below both return it from <c>GetCacheKey()</c> —
	/// the defect's stated trigger.
	/// </summary>
	private const string SharedLogicalKey = "user:42";

	/// <summary>A logical key belonging to no other fixture, for the different-key liveness arm.</summary>
	private const string DistinctLogicalKey = "user:99";

	/// <summary>
	/// Bound on every awaited dispatch. A re-entrancy regression HANGS rather than fails, and a hanging test
	/// is indistinguishable from a wedged CI run — so each dispatch is awaited under a deadline and the arm
	/// reports a <see cref="TimeoutException"/> (regression) rather than never returning. Generous enough
	/// that a loaded agent cannot trip it: the guarded path throws before any I/O is attempted.
	/// </summary>
	private static readonly TimeSpan HangBound = TestTimeouts.Scale(TimeSpan.FromSeconds(15));

	private readonly ServiceProvider _services;
	private readonly HybridCache _cache;
	private readonly IMeterFactory _meterFactory;
	private readonly DispatchJsonSerializer _serializer = new();
	private readonly DefaultCacheKeyBuilder _keyBuilder;
	private bool _disposed;

	public CachingMiddlewareReentrancyShould()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddMetrics();
		_ = services.AddHybridCache();
		_services = services.BuildServiceProvider();
		_cache = _services.GetRequiredService<HybridCache>();
		_meterFactory = _services.GetRequiredService<IMeterFactory>();

		// The REAL key builder, so key identity is derived the way it is in production rather than stubbed
		// into agreement. Two distinct message types collide here only because their GetCacheKey() strings
		// match — which is the whole point of the defect.
		_keyBuilder = new DefaultCacheKeyBuilder(_serializer);
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

		_serializer.Dispose();
		_services.Dispose();
	}

	// SAFETY

	[Fact]
	public async Task Throw_NamingTheKey_WhenACachedHandlerDispatchesTheSameCacheKey()
	{
		// Arrange — the outer query's handler dispatches a DIFFERENT query type whose GetCacheKey() returns
		// the same logical key. Both go through the same middleware, which is what a nested dispatch does in
		// a real pipeline.
		var middleware = CreateMiddleware(_cache);

		DispatchRequestDelegate innerHandler =
			(_, _, _) => new ValueTask<IMessageResult>(MessageResult.Success<string>("balance"));

		DispatchRequestDelegate outerHandler = async (_, ctx, ct) =>
			await middleware.InvokeAsync(new AccountBalanceQuery(), ctx, innerHandler, ct);

		// Act — bounded, so a regression FAILS on the deadline instead of hanging the suite.
		var dispatch = middleware
			.InvokeAsync(new AccountSummaryQuery(), NewContext(), outerHandler, CancellationToken.None)
			.AsTask();

		var thrown = await Should.ThrowAsync<CacheReentrancyException>(
			async () => await dispatch.WaitAsync(HangBound));

		// Assert — the consumer's only route out of this is knowing WHICH key cycled. That is carried as a
		// property rather than read out of the message: prose gets reworded, the property is the contract.
		// The expected value is the identity-folded hash the middleware actually used; the public overload
		// applies the same transform, so this binds the real key without touching internals.
		var resolvedKey = _keyBuilder.CreateKey(SharedLogicalKey, tenantId: null, userId: null);
		thrown.CacheKey.ShouldBe(
			resolvedKey,
			"the error must carry the cache key that cycled — it is the only thing that tells a consumer where to look");
	}

	// LIVENESS 1

	[Fact]
	public async Task Complete_WhenACachedHandlerDispatchesADifferentCacheKey()
	{
		// A guard that rejected ANY nested dispatch from a cached handler would satisfy the safety arm
		// above while breaking every legitimate composition. This is the arm that catches that.
		var middleware = CreateMiddleware(_cache);

		var innerRan = false;
		DispatchRequestDelegate innerHandler = (_, _, _) =>
		{
			innerRan = true;
			return new ValueTask<IMessageResult>(MessageResult.Success<string>("preferences"));
		};

		var outerRan = false;
		DispatchRequestDelegate outerHandler = async (outerMessage, ctx, ct) =>
		{
			_ = outerMessage;
			outerRan = true;
			_ = await middleware.InvokeAsync(new AccountPreferencesQuery(), ctx, innerHandler, ct);
			return MessageResult.Success<string>("summary");
		};

		var result = await middleware
			.InvokeAsync(new AccountSummaryQuery(), NewContext(), outerHandler, CancellationToken.None)
			.AsTask()
			.WaitAsync(HangBound);

		_ = result.ShouldNotBeNull();
		outerRan.ShouldBeTrue("the outer handler must run");
		innerRan.ShouldBeTrue("a nested dispatch on a DIFFERENT cache key must be allowed to execute");
	}

	// LIVENESS 2

	[Fact]
	public async Task StillCache_AcrossSequentialDispatchesOfTheSameKey()
	{
		// The arm that fails if the guard does not unwind its own bookkeeping: a key left marked in flight
		// after the first dispatch turns every LATER dispatch of it into a false re-entrancy error. Same key,
		// dispatched sequentially, which is precisely the case a broken restore breaks — and this is also the
		// plain end-to-end caching contract, so it proves the guard did not cost us caching at all.
		var middleware = CreateMiddleware(_cache);

		var executions = 0;
		DispatchRequestDelegate handler = (_, _, _) =>
		{
			executions++;
			return new ValueTask<IMessageResult>(MessageResult.Success<string>("summary"));
		};

		var first = await middleware
			.InvokeAsync(new AccountSummaryQuery(), NewContext(), handler, CancellationToken.None)
			.AsTask()
			.WaitAsync(HangBound);

		var second = await middleware
			.InvokeAsync(new AccountSummaryQuery(), NewContext(), handler, CancellationToken.None)
			.AsTask()
			.WaitAsync(HangBound);

		// A third, because "restores once" and "restores every time" are different properties and only the
		// third dispatch tells them apart.
		var third = await middleware
			.InvokeAsync(new AccountSummaryQuery(), NewContext(), handler, CancellationToken.None)
			.AsTask()
			.WaitAsync(HangBound);

		_ = first.ShouldNotBeNull();
		_ = second.ShouldNotBeNull();
		_ = third.ShouldNotBeNull();
		executions.ShouldBe(
			1,
			"sequential dispatches of one key must still be served from cache — the handler runs once, and no later dispatch is rejected as re-entrant");
	}

	// LIVENESS 3

	[Fact]
	public async Task NotRejectALaterDispatch_AfterTheCacheBackendFailedOpen()
	{
		// The fail-open path returns from INSIDE the guarded region. If that exit does not unwind the
		// bookkeeping, a single backend blip poisons the key for the rest of the flow and every later
		// dispatch of it is rejected as re-entrant — a cache outage escalated into an application outage,
		// which is the exact thing fail-open exists to prevent.
		var failingCache = A.Fake<HybridCache>();
		A.CallTo(failingCache).Where(call => call.Method.Name == nameof(HybridCache.GetOrCreateAsync))
			.Throws(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "cache backend down"));

		var options = new CacheOptions { Enabled = true };
		options.Resilience.EnableFallback = true; // default; explicit because this arm depends on it
		var middleware = CreateMiddleware(failingCache, options);

		var executions = 0;
		DispatchRequestDelegate handler = (_, _, _) =>
		{
			executions++;
			return new ValueTask<IMessageResult>(MessageResult.Success<string>("summary"));
		};

		var first = await middleware
			.InvokeAsync(new AccountSummaryQuery(), NewContext(), handler, CancellationToken.None)
			.AsTask()
			.WaitAsync(HangBound);

		var second = await middleware
			.InvokeAsync(new AccountSummaryQuery(), NewContext(), handler, CancellationToken.None)
			.AsTask()
			.WaitAsync(HangBound);

		_ = first.ShouldNotBeNull();
		_ = second.ShouldNotBeNull();
		executions.ShouldBe(
			2,
			"with the backend failing, BOTH dispatches must fall open to the handler — the second must not be rejected as re-entrant");
	}

	// SAFETY 2

	[Fact]
	public async Task Surface_TheReentrancyError_WhenTheNestedDispatchIsConditional()
	{
		// The re-entrancy error is raised from inside the HybridCache value factory, so it comes back out
		// into the SAME frame's fail-open clause — which cannot, on its own, tell an application-logic fault
		// from a dead Redis. It logs "cache operation failed", falls open, and runs the handler again.
		//
		// When the nested dispatch is UNCONDITIONAL the re-run re-enters, trips the guard a second time, and
		// the error escapes from inside the catch block. It surfaces by accident. That is why this arm makes
		// the nested dispatch CONDITIONAL — the handler re-enters only on its first execution, because the
		// first execution warmed something the second one hits, which is ordinary handler code. Then the
		// fall-open re-run does NOT re-enter, nothing re-trips, and the dispatch returns a successful,
		// uncached result with the cycle entirely invisible to the consumer.
		//
		// A cache cycle that reports success is a quieter version of the bug the guard exists to make loud,
		// so the error must reach the caller on this shape too.
		var middleware = CreateMiddleware(_cache);

		var innerExecutions = 0;
		DispatchRequestDelegate innerHandler = (_, _, _) =>
		{
			innerExecutions++;
			return new ValueTask<IMessageResult>(MessageResult.Success<string>("balance"));
		};

		var outerExecutions = 0;
		DispatchRequestDelegate outerHandler = async (_, ctx, ct) =>
		{
			outerExecutions++;
			if (outerExecutions == 1)
			{
				return await middleware.InvokeAsync(new AccountBalanceQuery(), ctx, innerHandler, ct);
			}

			return MessageResult.Success<string>("summary-without-the-nested-dispatch");
		};

		var dispatch = middleware
			.InvokeAsync(new AccountSummaryQuery(), NewContext(), outerHandler, CancellationToken.None)
			.AsTask();

		// The dispatch must FAIL. Returning a result here is the defect: the caller is handed a value that
		// looks fine and never learns its handler is in a cache cycle.
		var thrown = await Should.ThrowAsync<CacheReentrancyException>(
			async () => await dispatch.WaitAsync(HangBound));

		var resolvedKey = _keyBuilder.CreateKey(SharedLogicalKey, tenantId: null, userId: null);
		thrown.CacheKey.ShouldBe(
			resolvedKey,
			"the error must still carry the key that cycled, whatever path it took to the caller");

		// A re-entrancy fault is not a backend fault, so it must not be routed through fail-open at all —
		// which means no fall-open re-run of the handler.
		outerExecutions.ShouldBe(
			1,
			"a re-entrancy fault must not be treated as a cache-backend failure and retried; the handler runs once");
		innerExecutions.ShouldBe(
			0,
			"the cycling inner dispatch must never produce a result");
	}

	// SAFETY 2

	[Fact]
	public async Task RunTheHandlerExactlyOnce_WhenTheHandlerItselfThrowsFromInsideTheCacheFactory()
	{
		// This arm asserted `executions == 2` until the fail-open clause learned to tell a handler fault from
		// a backend fault. That was not an aspiration written early -- it was the defect, locked deliberately
		// so that closing it would show up here as a failure rather than passing unnoticed.
		//
		// The old reasoning was that a handler's InvalidOperationException is indistinguishable from a cache
		// fault and so must take the same path. The distinction was available all along, just not at that
		// catch: the factory knows whether the fault came from its own body. Marking it there makes the
		// discriminator POSITIONAL (where did this come from) rather than TYPE-BASED (what is it), which is
		// why it generalises to every handler exception instead of one more excluded type.
		//
		// Caching is opt-in cross-cutting infrastructure. Switching it on must not change how many times a
		// handler body runs -- a handler that writes, charges, enqueues or emits before throwing would
		// otherwise perform that side effect twice for one dispatch.
		var middleware = CreateMiddleware(_cache);

		var executions = 0;
		DispatchRequestDelegate throwingHandler = (_, _, _) =>
		{
			executions++;
			throw new InvalidOperationException("the handler's own failure, not a cache cycle");
		};

		var thrown = await Should.ThrowAsync<InvalidOperationException>(
			async () => await middleware
				.InvokeAsync(new AccountSummaryQuery(), NewContext(), throwingHandler, CancellationToken.None)
				.AsTask()
				.WaitAsync(HangBound));

		executions.ShouldBe(
			1,
			"a handler that threw must not be re-run by the fail-open clause -- enabling caching must not "
			+ "double-execute a handler's side effects");

		// The caller must still see its OWN exception, not a wrapper: the guard is internal bookkeeping and
		// nothing about it may reach a consumer.
		thrown.Message.ShouldBe(
			"the handler's own failure, not a cache cycle",
			"the handler's original exception must propagate unchanged, not be wrapped or replaced");
	}

	// SAFETY 3

	[Fact]
	public async Task RunTheHandlerExactlyOnce_WhenTheHandlerThrowsOperationCanceledForItsOwnReasons()
	{
		// The cancellation clause is the one exception type the positional factory-fault discriminator did
		// not originally cover, and it is not a hypothetical shape: a handler that enforces its own domain
		// deadline, or awaits on a token linked to something other than the caller's, raises
		// OperationCanceledException without the caller having cancelled anything. That took the fail-open
		// path and ran the handler a SECOND time -- and a handler that charged, enqueued or emitted before
		// throwing performed that side effect twice for one dispatch.
		//
		// The two cancellations ARE distinguishable, which is what makes this closable rather than a
		// documented boundary: the cache stack cancels by cancelling the token it hands the factory, so a
		// cancellation raised while that token is UNCANCELLED came from the handler. The arm below locks the
		// other side of that discriminator, so narrowing this one cannot quietly close the legitimate case
		// fail-open exists for.
		var middleware = CreateMiddleware(_cache);

		var executions = 0;
		DispatchRequestDelegate cancellingHandler = (_, _, _) =>
		{
			executions++;
			throw new OperationCanceledException("the handler's own deadline, not the caller's cancellation");
		};

		_ = await Should.ThrowAsync<OperationCanceledException>(
			async () => await middleware
				.InvokeAsync(new AccountSummaryQuery(), NewContext(), cancellingHandler, CancellationToken.None)
				.AsTask()
				.WaitAsync(HangBound));

		executions.ShouldBe(
			1,
			"a handler that cancelled itself must not be re-run by the fail-open clause -- enabling caching "
			+ "must not double-execute a handler's side effects");
	}

	// LIVENESS 6

	[Fact]
	public async Task FallOpen_WhenTheCacheStackCancelsTheFactory()
	{
		// The case the cancellation clause exists for, and the one a naive narrowing destroys. The cache
		// stack bounds its own work from inside the shared operation, so a backend that is merely SLOW
		// surfaces as a cancellation of the token the factory was handed -- not as an error, and not as the
		// caller's cancellation. That must still fall open and run the handler.
		//
		// Written to be RED if the fix keys off exception TYPE (every OperationCanceledException is the
		// handler's) instead of the factory token's cancellation state.
		var cancellingCache = new FactoryCancellingCache();

		var options = new CacheOptions { Enabled = true };
		options.Resilience.EnableFallback = true;
		var middleware = CreateMiddleware(cancellingCache, options);

		var executions = 0;
		var expected = MessageResult.Success<string>("summary");
		DispatchRequestDelegate handler = (_, _, token) =>
		{
			// The handler honours the token it was handed, as a well-behaved handler does -- and that is what
			// makes this arm exercise the clause rather than pass beside it. Inside the factory the token is
			// the one the cache stack cancelled, so the handler raises the cancellation the clause exists to
			// absorb, BEFORE doing any work. The fail-open re-run is handed the caller's token, which is not
			// cancelled, so it runs. A handler that ignored its token would make this arm green against every
			// implementation, including a broken one.
			token.ThrowIfCancellationRequested();
			executions++;
			return new ValueTask<IMessageResult>(expected);
		};

		var result = await middleware
			.InvokeAsync(new AccountSummaryQuery(), NewContext(), handler, CancellationToken.None)
			.AsTask()
			.WaitAsync(HangBound);

		result.ShouldBe(expected, "a cache-stack cancellation must not become an application failure");
		executions.ShouldBe(
			1,
			"a cache-stack cancellation must fall open and run the handler exactly once -- not zero, which "
			+ "would be fail-closed wearing fail-open's name");
	}

	// LIVENESS 5

	[Fact]
	public async Task RunTheHandlerExactlyOnce_WhenTheBackendErrorsFast()
	{
		// The legitimate case fail-open exists for, isolated to a SINGLE dispatch so the count is exact.
		//
		// This arm is written to outlive the current shape of the fail-open clause. That clause wraps a
		// value factory which invokes the handler, so a fault raised by the factory's BODY and one raised by
		// the CACHE arrive at the same catch — and running the handler, the right answer for a dead backend,
		// is the wrong answer for a handler that just threw. The re-entrancy fault is now excluded by type;
		// the general case is not, and whatever closes it must not close this one too.
		//
		// So: a backend that errors before the factory is ever entered must still fall open, return the
		// handler's result, and execute the handler EXACTLY ONCE. Not zero — that would be fail-closed
		// wearing fail-open's name. Not twice — that would be the duplicate-execution defect arriving from
		// the other direction.
		var failingCache = A.Fake<HybridCache>();
		A.CallTo(failingCache).Where(call => call.Method.Name == nameof(HybridCache.GetOrCreateAsync))
			.Throws(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "cache backend down"));

		var options = new CacheOptions { Enabled = true };
		options.Resilience.EnableFallback = true;
		var middleware = CreateMiddleware(failingCache, options);

		var executions = 0;
		var expected = MessageResult.Success<string>("summary");
		DispatchRequestDelegate handler = (_, _, _) =>
		{
			executions++;
			return new ValueTask<IMessageResult>(expected);
		};

		var result = await middleware
			.InvokeAsync(new AccountSummaryQuery(), NewContext(), handler, CancellationToken.None)
			.AsTask()
			.WaitAsync(HangBound);

		result.ShouldBe(expected, "a cache-backend outage must not become an application outage");
		executions.ShouldBe(
			1,
			"a fast backend error must fall open and run the handler exactly once — not zero, and not twice");
	}

	// Harness

	private CachingMiddleware CreateMiddleware(HybridCache cache, CacheOptions? options = null)
		=> new(
			_meterFactory,
			cache,
			_keyBuilder,
			_services,
			MsOptions.Create(options ?? new CacheOptions { Enabled = true }),
			NullLogger<CachingMiddleware>.Instance);

	private static IMessageContext NewContext()
	{
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>());
		return context;
	}

	/// <summary>
	/// A cache whose own work is cancelled while the factory is running: it cancels the token it hands the
	/// factory and lets the resulting <see cref="OperationCanceledException"/> escape, which is how a slow
	/// backend bounded from inside the shared operation actually surfaces. The caller's token is untouched.
	/// </summary>
	private sealed class FactoryCancellingCache : HybridCache
	{
		public override async ValueTask<T> GetOrCreateAsync<TState, T>(
			string key,
			TState state,
			Func<TState, CancellationToken, ValueTask<T>> factory,
			HybridCacheEntryOptions? options = null,
			IEnumerable<string>? tags = null,
			CancellationToken cancellationToken = default)
		{
			using var cts = new CancellationTokenSource();
			await cts.CancelAsync().ConfigureAwait(false);
			return await factory(state, cts.Token).ConfigureAwait(false);
		}

		public override ValueTask SetAsync<T>(
			string key,
			T value,
			HybridCacheEntryOptions? options = null,
			IEnumerable<string>? tags = null,
			CancellationToken cancellationToken = default)
			=> ValueTask.CompletedTask;

		public override ValueTask RemoveAsync(string key, CancellationToken cancellationToken = default)
			=> ValueTask.CompletedTask;

		public override ValueTask RemoveByTagAsync(string tag, CancellationToken cancellationToken = default)
			=> ValueTask.CompletedTask;
	}

	// Fixtures: two DIFFERENT query types that collide on one cache key

	private sealed class AccountSummaryQuery : ICacheable<string>
	{
		public int ExpirationSeconds => 120;

		public string GetCacheKey() => SharedLogicalKey;

		public bool ShouldCache(object? result) => true;
	}

	private sealed class AccountBalanceQuery : ICacheable<string>
	{
		public int ExpirationSeconds => 120;

		public string GetCacheKey() => SharedLogicalKey;

		public bool ShouldCache(object? result) => true;
	}

	private sealed class AccountPreferencesQuery : ICacheable<string>
	{
		public int ExpirationSeconds => 120;

		public string GetCacheKey() => DistinctLogicalKey;

		public bool ShouldCache(object? result) => true;
	}
}

#pragma warning restore IL2026, IL3050
