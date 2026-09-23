// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

#pragma warning disable IL2026, IL3050 // Suppress for test - RequiresUnreferencedCode/RequiresDynamicCode

using System.Diagnostics.Metrics;
using System.Text.Json;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Caching;

using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Caching.Tests;

/// <summary>
/// yi59t5 fail-open <b>boundary</b> lock (independent author = TestsDeveloper, the required 4th case the
/// author=impl lock missed — PM 17603 / SA 17597): <see cref="CachingMiddleware"/> fails open on a cache
/// <b>backend</b> fault, but MUST NOT over-reach into swallowing a <b>result-handling</b> fault (a corrupt
/// cached payload that fails to deserialize) as a silent cache miss — that would mask data corruption.
/// </summary>
/// <remarks>
/// <para>
/// This complements <see cref="CachingMiddlewareFailOpenShould"/> (backend-fault fail-open + fail-closed +
/// breaker-skip). It binds the <em>other</em> edge of the fail-open boundary that SA pinned (17558/17597):
/// <c>CompleteCacheOperationAsync</c> (deserialize / poison-marker eviction / tag registration) runs
/// <b>OUTSIDE</b> the fail-open <c>try</c> in <c>ExecuteWithCacheAsync</c>, so a deserialize fault on a
/// cache hit <b>propagates</b> — it is a data/logic error, never a swallowed miss.
/// </para>
/// <para>
/// <b>Setup:</b> the backend returns a cache HIT whose <see cref="CachedValue.Value"/> is a JSON
/// <c>null</c> element with a resolvable <see cref="CachedValue.TypeName"/> — so the middleware's
/// <c>DeserializeCachedValue</c> deserializes it to <see langword="null"/> and throws the documented
/// <see cref="InvalidOperationException"/> ("Failed to deserialize cached value…"). The throw originates
/// in result-handling, after the fail-open scope.
/// </para>
/// <para>
/// <b>Non-vacuous → RED mutant (scope-guard):</b> moving the <c>CompleteCacheOperationAsync</c> call
/// INSIDE <c>ExecuteWithCacheAsync</c>'s fail-open <c>try</c> makes the
/// <c>catch (… ex is not OCE &amp;&amp; EnableFallback)</c> swallow the deserialize fault → the middleware
/// falls open to the handler and returns its result → this test's <c>Should.ThrowAsync</c> fails. (Proven
/// by a cp-backup mutate-restore of the committed middleware — never a <c>git checkout</c> of a shared,
/// uncommitted file, <c>commit-surface-before-parallel-edits</c>.)
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Feature", "Caching")]
public sealed class CachingMiddlewareFailOpenBoundaryShould : IDisposable
{
	private readonly IMeterFactory _meterFactory = new TestMeterFactory();
	private readonly HybridCache _fakeCache = A.Fake<HybridCache>();
	private readonly ICacheKeyBuilder _fakeKeyBuilder = A.Fake<ICacheKeyBuilder>();
	private readonly IServiceProvider _fakeServices = A.Fake<IServiceProvider>();

	public CachingMiddlewareFailOpenBoundaryShould()
		// Non-null key drives the message onto the cache path (so GetOrCreateAsync is actually consulted).
		=> A.CallTo(() => _fakeKeyBuilder.CreateKey(A<IDispatchAction>._, A<IMessageContext>._)).Returns("boundary-key");

	public void Dispose()
	{
		if (_meterFactory is IDisposable d) d.Dispose();
	}

	[Fact]
	public async Task PropagateDeserializeFault_OnCorruptCacheHit_NotSwallowAsMiss()
	{
		// Arrange — a cache HIT whose stored Value is JSON `null` with a resolvable TypeName, so
		// DeserializeCachedValue resolves the type, deserializes "null" → null → throws the documented
		// InvalidOperationException. (HasExecuted + ShouldCache + non-null Value route into the deserialize path.)
		var corruptHit = new CachedValue
		{
			ActionTypeName = CachingMiddleware.DescribeActionType(typeof(BoundaryCacheableMessage)),
			HasExecuted = true,
			ShouldCache = true,
			Value = JsonDocument.Parse("null").RootElement.Clone(),
			TypeName = typeof(string).FullName, // "System.String" — resolves; "null" deserializes to a null string.
		};

		// Return the corrupt hit WITHOUT invoking the factory. WithReturnType pins the generic GetOrCreateAsync
		// overload's return (ValueTask<CachedValue>), dodging the generic-arg matcher trap (a method-name-only
		// matcher cannot supply a typed return; an untyped value would never bind to the generic call).
		A.CallTo(_fakeCache)
			.Where(call => call.Method.Name == nameof(HybridCache.GetOrCreateAsync))
			.WithReturnType<ValueTask<CachedValue>>()
			.Returns(new ValueTask<CachedValue>(corruptHit));

		var options = new CacheOptions { Enabled = true, CacheMode = CacheMode.Distributed };
		options.Resilience.EnableFallback = true; // even with fallback ON, a result-handling fault must NOT fall open.
		var middleware = CreateMiddleware(options);

		var message = new BoundaryCacheableMessage();
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>()); // no "Dispatch:OriginalResult" ⇒ a cache HIT, not a fresh execution.

		var handlerRan = false;
		DispatchRequestDelegate next = (_, _, _) =>
		{
			handlerRan = true;
			return new ValueTask<IMessageResult>(MessageResult.Success());
		};

		// Act & Assert — the deserialize fault MUST propagate out of InvokeAsync (data corruption is never
		// masked as a cache miss / fall-open). RED if CompleteCacheOperationAsync is moved inside the fail-open try.
#pragma warning disable CA2012 // ValueTask used correctly in async lambda
		_ = await Should.ThrowAsync<InvalidOperationException>(
			async () => await middleware.InvokeAsync(message, context, next, CancellationToken.None));
#pragma warning restore CA2012

		// And the handler must NOT have been silently run as a "fall-open" — a result-handling fault is not a
		// backend outage; over-reaching into the handler would mask the corruption.
		handlerRan.ShouldBeFalse(
			"yi59t5 boundary: a corrupt-cache-hit deserialize fault must propagate, NOT fall open to the handler "
			+ "(which would silently mask data corruption as a cache miss).");
	}

	// yy57cu (originally S856 REVIEW_CODE BLOCKING, P0; re-bound to the per-tag version-stamp model): the
	// tag-freshness check in CompleteCacheOperationAsync (`tagTracker.GetOrCreateStampAsync`, called from
	// CheckTaggedEntryFreshnessAsync) runs OUTSIDE the yi59t5 fail-open scope — its sibling backend write
	// `EvictAsync` IS wrapped fail-open, and the asymmetry would be the same gap under the new model too. A
	// cross-cutting cache MUST fail open: a tag-store (Redis / IDistributedCache) outage must never break
	// core dispatch. A genuine cache HIT for a tagged entry, where the tag tracker's backend is down, must
	// still return a result to the caller (falling through to the handler), never propagate the tracker's
	// exception out of InvokeAsync.
	[Fact]
	public async Task FailOpenToHandlerResult_WhenTagTrackerGetOrCreateStampThrows_TagStoreOutageNeverBreaksDispatch()
	{
		var options = new CacheOptions { Enabled = true, CacheMode = CacheMode.Distributed };

		// A genuine cache HIT (no "Dispatch:OriginalResult") with a cacheable, non-null value and a
		// recorded stamp for its own tag → reaches the tag-freshness check.
		A.CallTo(_fakeCache)
			.Where(c => c.Method.Name == nameof(HybridCache.GetOrCreateAsync))
			.WithReturnType<ValueTask<CachedValue>>()
			.Returns(new ValueTask<CachedValue>(new CachedValue
			{
				ActionTypeName = CachingMiddleware.DescribeActionType(typeof(TaggedCacheableMessage)),
				HasExecuted = true,
				ShouldCache = true,
				Value = "ok",
				TagStamps = new Dictionary<string, string> { ["yy57cu-tag"] = "some-recorded-stamp" },
			}));

		// The separate tag-store backend is DOWN — GetOrCreateStampAsync throws a non-cancellation error.
		var tagTracker = A.Fake<ICacheTagTracker>();
		A.CallTo(() => tagTracker.GetOrCreateStampAsync(A<string>._, A<CancellationToken>._))
			.ThrowsAsync(new InvalidOperationException("yy57cu: tag-store backend down"));

		var middleware = CreateMiddleware(options, tagTracker);

		var message = new TaggedCacheableMessage();
		var context = A.Fake<IMessageContext>();
		var expected = A.Fake<IMessageResult>();
		// No "Dispatch:OriginalResult" ⇒ this is a genuine hit (not a fresh execution), so the tag-freshness
		// check actually runs.
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>());

		DispatchRequestDelegate next = (_, _, _) => new ValueTask<IMessageResult>(expected);

		// Act — a tag-store outage must NOT propagate out of InvokeAsync (fail-open); the entry is treated
		// as stale (fail-closed on the READ, but the REQUEST still falls open to the handler).
		var result = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		// Assert — dispatch fell through to the handler, and the freshness check was genuinely attempted (non-vacuous).
		result.ShouldBe(expected,
			"yy57cu: a tag-tracker backend failure while checking a hit's freshness must fail OPEN to the handler — "
			+ "a cross-cutting cache must never break core dispatch (parity with yi59t5's GetOrCreateAsync fail-open).");
		A.CallTo(() => tagTracker.GetOrCreateStampAsync("yy57cu-tag", A<CancellationToken>._)).MustHaveHappened();
	}

	// r7ptim: the per-tag version-stamp read-side freshness check (CheckTaggedEntryFreshnessAsync) is the
	// highest-risk piece of this design -- a stale-but-served entry is a correctness bug, and a
	// valid-but-evicted entry is a silent perf regression. Both arms are bound here, independently of the
	// fail-open test above (which only reaches the tracker-THROWS branch, before either comparison runs).

	// Liveness arm: a matching stamp means nothing invalidated the tag since this entry was written --
	// it must be served from cache, not re-executed.
	[Fact]
	public async Task ServeFromCache_WhenTaggedEntryStampMatchesCurrentTagStamp()
	{
		var options = new CacheOptions { Enabled = true, CacheMode = CacheMode.Distributed };

		A.CallTo(_fakeCache)
			.Where(c => c.Method.Name == nameof(HybridCache.GetOrCreateAsync))
			.WithReturnType<ValueTask<CachedValue>>()
			.Returns(new ValueTask<CachedValue>(new CachedValue
			{
				ActionTypeName = CachingMiddleware.DescribeActionType(typeof(TaggedCacheableMessage)),
				HasExecuted = true,
				ShouldCache = true,
				Value = "ok",
				TagStamps = new Dictionary<string, string> { ["yy57cu-tag"] = "stamp-a" },
			}));

		var tagTracker = A.Fake<ICacheTagTracker>();
		A.CallTo(() => tagTracker.GetOrCreateStampAsync("yy57cu-tag", A<CancellationToken>._)).Returns("stamp-a");

		var middleware = CreateMiddleware(options, tagTracker);

		var message = new TaggedCacheableMessage();
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>()); // genuine hit, not a fresh execution.

		var handlerRan = false;
		DispatchRequestDelegate next = (_, _, _) =>
		{
			handlerRan = true;
			return new ValueTask<IMessageResult>(A.Fake<IMessageResult>());
		};

		await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		handlerRan.ShouldBeFalse(
			"a matching version stamp means the entry has not been invalidated -- it must be served from "
			+ "cache, not re-executed (the liveness arm: a valid entry is not needlessly evicted).");
		A.CallTo(() => _fakeCache.RemoveAsync(A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
	}

	// Safety arm: a mismatched stamp means the tag was invalidated since this entry was written -- it must
	// NOT be served, and the stale entry is evicted so the next request repopulates it.
	[Fact]
	public async Task FallThroughToHandler_WhenTaggedEntryStampDoesNotMatchCurrentTagStamp()
	{
		var options = new CacheOptions { Enabled = true, CacheMode = CacheMode.Distributed };

		A.CallTo(_fakeCache)
			.Where(c => c.Method.Name == nameof(HybridCache.GetOrCreateAsync))
			.WithReturnType<ValueTask<CachedValue>>()
			.Returns(new ValueTask<CachedValue>(new CachedValue
			{
				ActionTypeName = CachingMiddleware.DescribeActionType(typeof(TaggedCacheableMessage)),
				HasExecuted = true,
				ShouldCache = true,
				Value = "ok",
				TagStamps = new Dictionary<string, string> { ["yy57cu-tag"] = "stamp-a" },
			}));

		var tagTracker = A.Fake<ICacheTagTracker>();
		// The tag was bumped since this entry was written -- its current stamp no longer matches.
		A.CallTo(() => tagTracker.GetOrCreateStampAsync("yy57cu-tag", A<CancellationToken>._)).Returns("stamp-b");

		var middleware = CreateMiddleware(options, tagTracker);

		var message = new TaggedCacheableMessage();
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>());

		var handlerRan = false;
		var expected = A.Fake<IMessageResult>();
		DispatchRequestDelegate next = (_, _, _) =>
		{
			handlerRan = true;
			return new ValueTask<IMessageResult>(expected);
		};

		var result = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		handlerRan.ShouldBeTrue(
			"a mismatched version stamp means the tag was invalidated -- the stale entry must NOT be served "
			+ "(the safety arm: a stale entry is never returned as if it were valid).");
		result.ShouldBe(expected);
		A.CallTo(() => _fakeCache.RemoveAsync("boundary-key", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	// Safety arm (the two-different-absences distinction): an entry with NO recorded stamp for its own tag
	// is malformed -- it has nothing to prove its own validity with, and must be treated as invalidated,
	// never conflated with a tag that simply has no stamp in the backend yet.
	[Fact]
	public async Task FallThroughToHandler_WhenTaggedEntryHasNoRecordedStampForItsOwnTag()
	{
		var options = new CacheOptions { Enabled = true, CacheMode = CacheMode.Distributed };

		A.CallTo(_fakeCache)
			.Where(c => c.Method.Name == nameof(HybridCache.GetOrCreateAsync))
			.WithReturnType<ValueTask<CachedValue>>()
			.Returns(new ValueTask<CachedValue>(new CachedValue
			{
				ActionTypeName = CachingMiddleware.DescribeActionType(typeof(TaggedCacheableMessage)),
				HasExecuted = true,
				ShouldCache = true,
				Value = "ok",
				TagStamps = null, // e.g. an entry serialized before this mechanism existed.
			}));

		var tagTracker = A.Fake<ICacheTagTracker>();
		var middleware = CreateMiddleware(options, tagTracker);

		var message = new TaggedCacheableMessage();
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>());

		var handlerRan = false;
		var expected = A.Fake<IMessageResult>();
		DispatchRequestDelegate next = (_, _, _) =>
		{
			handlerRan = true;
			return new ValueTask<IMessageResult>(expected);
		};

		var result = await middleware.InvokeAsync(message, context, next, CancellationToken.None);

		handlerRan.ShouldBeTrue(
			"an entry with no recorded stamp for its own tag cannot be proven valid and must be treated as "
			+ "invalidated -- never conflated with a tag that simply has no stamp in the backend yet.");
		result.ShouldBe(expected);
		// Never even needs to ask the tracker for the tag's current stamp: there is nothing to compare it to.
		A.CallTo(() => tagTracker.GetOrCreateStampAsync(A<string>._, A<CancellationToken>._)).MustNotHaveHappened();
		A.CallTo(() => _fakeCache.RemoveAsync("boundary-key", A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	private CachingMiddleware CreateMiddleware(CacheOptions options, ICacheTagTracker? tagTracker = null)
	{
		var logger = A.Fake<ILogger<CachingMiddleware>>();
		A.CallTo(() => logger.IsEnabled(A<LogLevel>._)).Returns(true);
		return new CachingMiddleware(
			_meterFactory, _fakeCache, _fakeKeyBuilder, _fakeServices,
			MsOptions.Create(options), logger, tagTracker: tagTracker);
	}

	private sealed class BoundaryCacheableMessage : ICacheable<string>
	{
		public string GetCacheKey() => "boundary:1";

		public bool ShouldCache(object? result) => true;
	}

	// Interface-cacheable WITH tags. GetCacheTags is EXPLICITLY implemented so the middleware's reflection
	// invocation on the ICacheable<T> interface slot dispatches to this body (a class-level `public` method
	// would NOT — ICacheable.GetCacheTags is a default interface method, not virtual to the class).
	private sealed class TaggedCacheableMessage : ICacheable<string>
	{
		public string GetCacheKey() => "yy57cu:1";

		string[]? ICacheable.GetCacheTags() => ["yy57cu-tag"];

		public bool ShouldCache(object? result) => true;
	}

	private sealed class TestMeterFactory : IMeterFactory
	{
		private readonly List<Meter> _meters = [];

		public Meter Create(MeterOptions options)
		{
			var meter = new Meter(options);
			_meters.Add(meter);
			return meter;
		}

		public void Dispose()
		{
			foreach (var meter in _meters) meter.Dispose();
		}
	}
}

#pragma warning restore IL2026, IL3050
