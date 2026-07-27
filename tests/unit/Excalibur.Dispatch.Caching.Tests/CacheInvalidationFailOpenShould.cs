// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.Metrics;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Caching;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Caching.Tests;

/// <summary>
/// Author≠impl regression lock (TestsDeveloper) for the <see cref="CacheInvalidationMiddleware"/>
/// fail-open + cancellation contract: cache invalidation is a cross-cutting concern that must never break
/// the core operation, and caller-requested cancellation is cooperative control-flow — not a "failure"
/// that triggers <c>InvalidateOnFailure</c>. Binds the split-catch behaviour: caller-cancel is re-thrown
/// WITHOUT invalidating; any other handler failure runs best-effort invalidation then re-throws the
/// ORIGINAL exception; and an invalidation error is swallowed so the core result still flows.
/// </summary>
/// <remarks>
/// <b>RED mutants:</b> collapse the caller-cancel <c>catch … when(cancellationToken.IsCancellationRequested)</c>
/// into the general <c>catch</c> ⇒ a cancelled handler wrongly invalidates (test (a) RED). Remove the
/// fail-open <c>catch</c> in <c>InvalidateForMessageAsync</c> ⇒ an invalidation error escapes and breaks the
/// core op (test (c) RED). Drop the best-effort invalidate on the failure path ⇒ test (b) RED.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Feature", "Caching")]
public sealed class CacheInvalidationFailOpenShould : IDisposable
{
	private readonly IMeterFactory _meterFactory = new TestMeterFactory();
	private readonly ICacheKeyBuilder _keyBuilder = A.Fake<ICacheKeyBuilder>();

	public CacheInvalidationFailOpenShould() =>
		A.CallTo(() => _keyBuilder.CreateKey(A<string>._, A<string?>._, A<string?>._))
			.ReturnsLazily((string logicalKey, string? tenantId, string? userId) => $"sk:{logicalKey}");

	public void Dispose()
	{
		if (_meterFactory is IDisposable d) d.Dispose();
	}

	// (a) Caller-requested cancellation propagates and does NOT trigger invalidate-on-failure.
	[Fact]
	public async Task CallerCancellation_PropagatesWithoutInvalidating()
	{
		var middleware = CreateMiddleware();
		var message = new FailInvalidatingMessage();
		using var cts = new CancellationTokenSource();
		await cts.CancelAsync();

		_ = await Should.ThrowAsync<OperationCanceledException>(async () => await middleware.InvokeAsync(
			message,
			A.Fake<IMessageContext>(),
			(_, _, ct) => throw new OperationCanceledException(ct),
			cts.Token));

		message.TagsFetched.ShouldBeFalse(
			"caller-requested cancellation is control-flow, not a failure — invalidation must be skipped.");
	}

	// (b) A real handler failure runs best-effort invalidation, then re-throws the ORIGINAL exception.
	[Fact]
	public async Task HandlerFailure_InvalidatesThenRethrowsOriginal()
	{
		var middleware = CreateMiddleware();
		var message = new FailInvalidatingMessage();
		var boom = new InvalidOperationException("handler boom");

		var thrown = await Should.ThrowAsync<InvalidOperationException>(async () => await middleware.InvokeAsync(
			message,
			A.Fake<IMessageContext>(),
			(_, _, _) => throw boom,
			CancellationToken.None));

		thrown.ShouldBeSameAs(boom, "the ORIGINAL handler exception must surface, never a masking invalidation error.");
		message.TagsFetched.ShouldBeTrue("InvalidateOnFailure=true must run best-effort invalidation on a real failure.");
	}

	// (c) An error raised inside invalidation is fail-open: the core result still flows, no throw.
	[Fact]
	public async Task InvalidationError_IsFailOpen_CoreResultStillFlows()
	{
		var middleware = CreateMiddleware();
		var message = new ThrowingInvalidatorMessage();
		var expected = A.Fake<IMessageResult>();

		var result = await middleware.InvokeAsync(
			message,
			A.Fake<IMessageContext>(),
			(_, _, _) => new ValueTask<IMessageResult>(expected),
			CancellationToken.None);

		result.ShouldBeSameAs(expected,
			"invalidation is cross-cutting and must never break the core operation — its error is swallowed.");
	}

	private CacheInvalidationMiddleware CreateMiddleware() =>
		new(_meterFactory, MsOptions.Create(new CacheOptions { Enabled = true }), _keyBuilder, tagTracker: null, memoryCache: null, hybridCache: null);

	// Opts into invalidate-on-failure and records whether invalidation actually ran (its tags were fetched).
	[InvalidateCache(InvalidateOnFailure = true, Tags = ["orders"])]
	private sealed class FailInvalidatingMessage : IDispatchMessage, ICacheInvalidator
	{
		public bool TagsFetched { get; private set; }

		public IEnumerable<string> GetCacheTagsToInvalidate()
		{
			TagsFetched = true;
			return ["orders"];
		}

		public IEnumerable<string> GetCacheKeysToInvalidate() => [];
	}

	// Raises an error from within the invalidation path to prove fail-open behaviour on the success path.
	[InvalidateCache(Tags = ["orders"])]
	private sealed class ThrowingInvalidatorMessage : IDispatchMessage, ICacheInvalidator
	{
		public IEnumerable<string> GetCacheTagsToInvalidate() => throw new InvalidOperationException("invalidation boom");

		public IEnumerable<string> GetCacheKeysToInvalidate() => [];
	}

	private sealed class TestMeterFactory : IMeterFactory
	{
		public Meter Create(MeterOptions options) => new(options);

		public void Dispose()
		{
		}
	}
}
