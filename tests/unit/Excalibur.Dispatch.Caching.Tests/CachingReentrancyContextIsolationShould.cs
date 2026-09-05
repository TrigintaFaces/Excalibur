// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable IL2026, IL3050 // Suppress for test - RequiresUnreferencedCode/RequiresDynamicCode

using System.Diagnostics.Metrics;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Caching;
using Excalibur.Dispatch.Features;

using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using Tests.Shared.Infrastructure;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Caching.Tests;

/// <summary>
/// How the cache re-entrancy guard's in-flight set behaves across the context boundaries a real nested
/// dispatch crosses.
/// </summary>
/// <remarks>
/// <para>
/// The set rides <see cref="IMessageContext.Items"/>. A nested dispatch is handed a CHILD context whose
/// Items are a shallow per-entry copy of its parent's, so the set must reach the inner frame that way --
/// and, because the copy is into the child's own dictionary, an inner frame's write must NOT reach the
/// parent or a concurrent sibling. Those two properties used to be provided by ExecutionContext
/// copy-on-write; nothing else asserts them now.
/// </para>
/// <para>
/// <see cref="CachingMiddlewareReentrancyShould"/> covers the guard when a nested dispatch REUSES its
/// caller's context object. Both arms below cross a child-context boundary instead, which is what
/// dispatching from inside a handler actually does.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Feature", "Caching")]
public sealed class CachingReentrancyContextIsolationShould : IDisposable
{
	private const string OuterKey = "isolation:outer";
	private const string SiblingAKey = "isolation:sibling-a";
	private const string SiblingBKey = "isolation:sibling-b";

	private static readonly TimeSpan HangBound = TestTimeouts.Scale(TimeSpan.FromSeconds(15));

	private readonly ServiceProvider _services;
	private readonly HybridCache _cache;
	private readonly IMeterFactory _meterFactory;
	private readonly DispatchJsonSerializer _serializer = new();
	private readonly DefaultCacheKeyBuilder _keyBuilder;
	private bool _disposed;

	public CachingReentrancyContextIsolationShould()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddMetrics();
		_ = services.AddHybridCache();
		_services = services.BuildServiceProvider();
		_cache = _services.GetRequiredService<HybridCache>();
		_meterFactory = _services.GetRequiredService<IMeterFactory>();
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

	// SAFETY -- the set must reach the inner frame through the child context.

	[Fact]
	public async Task Throw_WhenTheNestedDispatchOnTheSameKeyRunsInAChildContext()
	{
		// The shape a handler actually produces: it dispatches, the dispatcher gives that dispatch a CHILD
		// of the ambient context, and the inner middleware frame reads the guard from THAT context. If the
		// set did not travel down the child copy, the cycle would go undetected and hang instead.
		var middleware = CreateMiddleware();

		DispatchRequestDelegate innerHandler =
			(_, _, _) => new ValueTask<IMessageResult>(MessageResult.Success<string>("inner"));

		DispatchRequestDelegate outerHandler = async (_, ctx, ct) =>
			await middleware.InvokeAsync(new OuterTwinQuery(), ctx.CreateChildContext(), innerHandler, ct);

		var dispatch = middleware
			.InvokeAsync(new OuterQuery(), NewContext(), outerHandler, CancellationToken.None)
			.AsTask();

		var thrown = await Should.ThrowAsync<CacheReentrancyException>(
			async () => await dispatch.WaitAsync(HangBound));

		thrown.CacheKey.ShouldBe(
			_keyBuilder.CreateKey(OuterKey, tenantId: null, userId: null),
			"the error must name the key that cycled, whichever context the inner frame ran in");
	}

	// SAFETY -- an inner frame's write must not travel back up into its parent.

	[Fact]
	public async Task NotLeakACompletedChildKeyBackIntoTheParentContext()
	{
		// Sibling isolation is a consequence of this property: every sibling copies from the parent, so a
		// parent polluted by the first sibling hands the second a key it never entered. Asserted here
		// sequentially, where it is deterministic -- the second nested dispatch is rejected as re-entrant
		// only if the first one's write escaped upward.
		var middleware = CreateMiddleware();

		DispatchRequestDelegate leafHandler =
			(_, _, _) => new ValueTask<IMessageResult>(MessageResult.Success<string>("leaf"));

		var secondChildRan = false;
		DispatchRequestDelegate outerHandler = async (outerMessage, ctx, ct) =>
		{
			_ = outerMessage;

			// First child runs SiblingA to completion, inside its own child context.
			_ = await middleware.InvokeAsync(new SiblingAQuery(), ctx.CreateChildContext(), leafHandler, ct);

			// Second child, same parent, dispatches SiblingA's key again. Legitimate: the first one finished.
			_ = await middleware.InvokeAsync(new SiblingATwinQuery(), ctx.CreateChildContext(), leafHandler, ct);
			secondChildRan = true;

			return MessageResult.Success<string>("outer");
		};

		var result = await middleware
			.InvokeAsync(new OuterQuery(), NewContext(), outerHandler, CancellationToken.None)
			.AsTask()
			.WaitAsync(HangBound);

		_ = result.ShouldNotBeNull();
		secondChildRan.ShouldBeTrue(
			"a child's in-flight key must not survive in the parent after that child completed -- if it did, "
			+ "every later sibling would inherit it and be rejected as re-entrant");
	}

	// LIVENESS -- two children in flight at the same time must not see each other.

	[Fact]
	public async Task NotLetOneConcurrentChildObserveTheOtherInFlightKey()
	{
		// Both children are parked inside their middleware frames simultaneously, so each key is in flight
		// while the other reads. They hold DIFFERENT keys, so neither may be rejected -- and one shared
		// dictionary would either cross-contaminate them or corrupt under concurrent mutation.
		var middleware = CreateMiddleware();
		using var arrived = new CountdownEvent(2);

		async ValueTask<IMessageResult> ParkAsync(CancellationToken ct)
		{
			_ = arrived.Signal();
			await Task.Run(() => arrived.Wait(HangBound), ct).ConfigureAwait(false);
			return MessageResult.Success<string>("child");
		}

		var parent = NewContext();
		var a = middleware
			.InvokeAsync(new SiblingAQuery(), parent.CreateChildContext(), (_, _, ct) => ParkAsync(ct), CancellationToken.None)
			.AsTask();
		var b = middleware
			.InvokeAsync(new SiblingBQuery(), parent.CreateChildContext(), (_, _, ct) => ParkAsync(ct), CancellationToken.None)
			.AsTask();

		var results = await Task.WhenAll(a, b).WaitAsync(HangBound);

		results.Length.ShouldBe(2);
		_ = results[0].ShouldNotBeNull();
		_ = results[1].ShouldNotBeNull();
	}

	private CachingMiddleware CreateMiddleware()
		=> new(
			_meterFactory,
			_cache,
			_keyBuilder,
			_services,
			MsOptions.Create(new CacheOptions { Enabled = true }),
			NullLogger<CachingMiddleware>.Instance);

	private static IMessageContext NewContext()
	{
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.Items).Returns(new Dictionary<string, object>(StringComparer.Ordinal));
		A.CallTo(() => context.Features).Returns(new Dictionary<Type, object>());
		return context;
	}

	private sealed class OuterQuery : ICacheable<string>
	{
		public int ExpirationSeconds => 120;

		public string GetCacheKey() => OuterKey;

		public bool ShouldCache(object? result) => true;
	}

	/// <summary>A different type that resolves to the SAME key as <see cref="OuterQuery"/>.</summary>
	private sealed class OuterTwinQuery : ICacheable<string>
	{
		public int ExpirationSeconds => 120;

		public string GetCacheKey() => OuterKey;

		public bool ShouldCache(object? result) => true;
	}

	private sealed class SiblingAQuery : ICacheable<string>
	{
		public int ExpirationSeconds => 120;

		public string GetCacheKey() => SiblingAKey;

		public bool ShouldCache(object? result) => true;
	}

	/// <summary>A different type that resolves to the SAME key as <see cref="SiblingAQuery"/>.</summary>
	private sealed class SiblingATwinQuery : ICacheable<string>
	{
		public int ExpirationSeconds => 120;

		public string GetCacheKey() => SiblingAKey;

		public bool ShouldCache(object? result) => true;
	}

	private sealed class SiblingBQuery : ICacheable<string>
	{
		public int ExpirationSeconds => 120;

		public string GetCacheKey() => SiblingBKey;

		public bool ShouldCache(object? result) => true;
	}
}

#pragma warning restore IL2026, IL3050
