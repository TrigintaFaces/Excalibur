// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.Metrics;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Caching;

using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Caching.Tests;

/// <summary>
/// Behavior lock for the w5iuyn <see cref="InvalidateCacheAttribute.InvalidateOnFailure"/> public-API flag on
/// <see cref="CacheInvalidationMiddleware"/>. Binds the new contract on the error path:
/// <list type="bullet">
/// <item>Default (<c>InvalidateOnFailure</c> unset/false): a throwing handler propagates its exception and the
/// cache is left UNTOUCHED — invalidation runs only after the handler returns successfully.</item>
/// <item><c>InvalidateOnFailure = true</c>: invalidation ALSO runs when the handler throws, but the handler's
/// ORIGINAL exception is always the one surfaced — invalidation is fail-open and never masks it, even when the
/// invalidation operation itself throws.</item>
/// </list>
/// Each case is RED on a credible mutation of the gate / catch-scope, so the lock is non-vacuous.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Feature", "Caching")]
public sealed class CacheInvalidationOnFailureShould : IDisposable
{
	private readonly IMeterFactory _meterFactory = new TestMeterFactory();
	private readonly HybridCache _fakeHybridCache = A.Fake<HybridCache>();
	private readonly ICacheKeyBuilder _keyBuilder = A.Fake<ICacheKeyBuilder>();

	public CacheInvalidationOnFailureShould()
	{
		A.CallTo(() => _keyBuilder.CreateKey(A<string>._, A<string?>._, A<string?>._))
			.ReturnsLazily((string logicalKey, string? tenantId, string? userId) => $"sk:{logicalKey}");
	}

	public void Dispose()
	{
		if (_meterFactory is IDisposable d) d.Dispose();
	}

	/// <summary>
	/// Default contract: a handler that throws propagates its exception and leaves the cache UNTOUCHED.
	/// RED on the mutant that drops the <c>InvalidateOnFailure != true</c> gate (i.e. always runs the
	/// catch-then-invalidate path) — that mutant would invalidate on the default error path too.
	/// </summary>
	[Fact]
	public async Task NotInvalidate_OnHandlerThrow_ByDefault()
	{
		// Arrange — attribute present (so there ARE tags to invalidate) but InvalidateOnFailure left default (false).
		var options = new CacheOptions { Enabled = true, CacheMode = CacheMode.Hybrid };
		var middleware = CreateMiddleware(options, hybridCache: _fakeHybridCache);
		var message = new DefaultInvalidateMessage();
		var context = A.Fake<IMessageContext>();
		var boom = new HandlerBoomException();

		// Act — handler throws; the original exception must surface.
		var thrown = await Should.ThrowAsync<HandlerBoomException>(async () =>
			await middleware.InvokeAsync(
				message, context,
				(_, _, _) => throw boom,
				CancellationToken.None));

		// Assert — original exception surfaced, AND nothing was invalidated (cache untouched on the default error path).
		thrown.ShouldBeSameAs(boom);
		A.CallTo(() => _fakeHybridCache.RemoveByTagAsync(
			A<IEnumerable<string>>._, A<CancellationToken>._)).MustNotHaveHappened();
	}

	/// <summary>
	/// Default contract, success path: invalidation runs after the handler returns successfully.
	/// Anchors that the "no invalidation on throw" result above is NOT vacuous (the same message DOES invalidate
	/// when the handler succeeds).
	/// </summary>
	[Fact]
	public async Task Invalidate_OnHandlerSuccess_ByDefault()
	{
		// Arrange
		var options = new CacheOptions { Enabled = true, CacheMode = CacheMode.Hybrid };
		var middleware = CreateMiddleware(options, hybridCache: _fakeHybridCache);
		var message = new DefaultInvalidateMessage();
		var context = A.Fake<IMessageContext>();
		var expectedResult = A.Fake<IMessageResult>();

		// Act
		var result = await middleware.InvokeAsync(
			message, context, (_, _, _) => new ValueTask<IMessageResult>(expectedResult), CancellationToken.None);

		// Assert — result returned and invalidation ran on the success path.
		result.ShouldBe(expectedResult);
		A.CallTo(() => _fakeHybridCache.RemoveByTagAsync(
			A<IEnumerable<string>>.That.Contains("orders"), A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	/// <summary>
	/// Opt-in contract: with <c>InvalidateOnFailure = true</c>, invalidation runs even when the handler throws,
	/// and the handler's ORIGINAL exception is surfaced. RED on the mutant that re-throws WITHOUT calling
	/// invalidation in the catch block (no <c>RemoveByTagAsync</c>), or that inverts the gate.
	/// </summary>
	[Fact]
	public async Task Invalidate_OnHandlerThrow_WhenInvalidateOnFailureTrue()
	{
		// Arrange
		var options = new CacheOptions { Enabled = true, CacheMode = CacheMode.Hybrid };
		var middleware = CreateMiddleware(options, hybridCache: _fakeHybridCache);
		var message = new InvalidateOnFailureMessage();
		var context = A.Fake<IMessageContext>();
		var boom = new HandlerBoomException();

		// Act — handler throws; original exception must still surface.
		var thrown = await Should.ThrowAsync<HandlerBoomException>(async () =>
			await middleware.InvokeAsync(
				message, context,
				(_, _, _) => throw boom,
				CancellationToken.None));

		// Assert — original handler exception surfaced AND invalidation ran on the error path.
		thrown.ShouldBeSameAs(boom);
		A.CallTo(() => _fakeHybridCache.RemoveByTagAsync(
			A<IEnumerable<string>>.That.Contains("orders"), A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	/// <summary>
	/// Fail-open boundary (AC-w5-3): with <c>InvalidateOnFailure = true</c>, when BOTH the handler throws AND the
	/// invalidation operation itself throws, the surfaced exception is still the handler's ORIGINAL exception —
	/// invalidation never masks it. RED on the mutant that removes the fail-open catch in
	/// <c>InvalidateForMessageAsync</c> (the invalidation exception would then propagate from the catch block and
	/// mask the handler's original throw).
	/// </summary>
	[Fact]
	public async Task SurfaceOriginalHandlerException_NotInvalidationException_WhenInvalidateOnFailureTrue()
	{
		// Arrange — invalidation itself faults.
		var options = new CacheOptions { Enabled = true, CacheMode = CacheMode.Hybrid };
		var middleware = CreateMiddleware(options, hybridCache: _fakeHybridCache);
		var message = new InvalidateOnFailureMessage();
		var context = A.Fake<IMessageContext>();
		var boom = new HandlerBoomException();

		A.CallTo(() => _fakeHybridCache.RemoveByTagAsync(A<IEnumerable<string>>._, A<CancellationToken>._))
			.Throws(new InvalidOperationException("invalidation backend down"));

		// Act
		var thrown = await Should.ThrowAsync<HandlerBoomException>(async () =>
			await middleware.InvokeAsync(
				message, context,
				(_, _, _) => throw boom,
				CancellationToken.None));

		// Assert — the HANDLER exception is surfaced, NOT the invalidation exception (fail-open never masks).
		thrown.ShouldBeSameAs(boom);
		A.CallTo(() => _fakeHybridCache.RemoveByTagAsync(
			A<IEnumerable<string>>._, A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	/// <summary>
	/// Opt-in contract, success path: with <c>InvalidateOnFailure = true</c> a successful handler still returns its
	/// result and invalidation runs once (the flag changes the error path, not the happy path).
	/// </summary>
	[Fact]
	public async Task Invalidate_OnHandlerSuccess_WhenInvalidateOnFailureTrue()
	{
		// Arrange
		var options = new CacheOptions { Enabled = true, CacheMode = CacheMode.Hybrid };
		var middleware = CreateMiddleware(options, hybridCache: _fakeHybridCache);
		var message = new InvalidateOnFailureMessage();
		var context = A.Fake<IMessageContext>();
		var expectedResult = A.Fake<IMessageResult>();

		// Act
		var result = await middleware.InvokeAsync(
			message, context, (_, _, _) => new ValueTask<IMessageResult>(expectedResult), CancellationToken.None);

		// Assert
		result.ShouldBe(expectedResult);
		A.CallTo(() => _fakeHybridCache.RemoveByTagAsync(
			A<IEnumerable<string>>.That.Contains("orders"), A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	private CacheInvalidationMiddleware CreateMiddleware(
		CacheOptions? options = null,
		HybridCache? hybridCache = null)
	{
		return new CacheInvalidationMiddleware(
			_meterFactory,
			MsOptions.Create(options ?? new CacheOptions { Enabled = true }),
			_keyBuilder,
			tagTracker: null,
			memoryCache: null,
			hybridCache: hybridCache);
	}

	// Test helpers

	[InvalidateCache(Tags = ["orders"])]
	private sealed class DefaultInvalidateMessage : IDispatchMessage;

	[InvalidateCache(Tags = ["orders"], InvalidateOnFailure = true)]
	private sealed class InvalidateOnFailureMessage : IDispatchMessage;

	private sealed class HandlerBoomException : Exception
	{
		public HandlerBoomException() : base("handler boom") { }
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
