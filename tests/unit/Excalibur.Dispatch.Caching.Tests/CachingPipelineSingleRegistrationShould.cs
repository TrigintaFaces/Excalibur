// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Configuration;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Caching.Tests;

/// <summary>
/// The caching middleware must appear in the pipeline exactly once, however many times registration is
/// called.
/// </summary>
/// <remarks>
/// <para>
/// A second caching stage DEADLOCKS the pipeline, and does so without any handler recursion or consumer
/// code. The first stage's value factory calls the rest of the chain, which reaches the second stage,
/// which asks the cache for the very key whose creation is in flight; HybridCache collapses both onto one
/// in-flight operation, so the inner call waits on a result only the outer call can produce. The
/// framework's own advertised composition — <c>UseCaching().WithCachingOptions(...)</c> — reaches that
/// state, because <c>WithCachingOptions</c> routes through <c>UseCaching</c> and <c>UseMiddleware</c>
/// appends unconditionally.
/// </para>
/// <para>
/// This was survivable only while a per-caller deadline sat around the cache call: it converted the
/// deadlock into a timeout and a duplicate execution. That deadline has been removed — correctly, because
/// it also bounded handler execution and destroyed stampede protection — so the idempotency guard is now
/// the only thing standing between this composition and a permanent hang.
/// </para>
/// <para>
/// The guard is a registration-time marker rather than anything visible at dispatch time, so nothing else
/// in the suite fails if it is deleted: every unit test stays green and only an integration test hangs.
/// These arms exist so that removing it is detected here, cheaply, instead of by a wedged CI run.
/// </para>
/// </remarks>
public sealed class CachingPipelineSingleRegistrationShould
{
	/// <summary>
	/// Counts pipeline stages of <typeparamref name="TMiddleware"/> by reading the builder's own middleware
	/// list — the list <c>UseMiddleware</c> appends to and the pipeline is built from.
	/// </summary>
	/// <remarks>
	/// Counting DI descriptors instead would be wrong and would look right: several registration paths add
	/// a descriptor for the same wrapper, so a single <c>UseCaching()</c> yields three of them. The number
	/// that decides whether the pipeline deadlocks is the number of PIPELINE STAGES, which is this list.
	/// The field lookup asserts before counting, so renaming the field fails the test loudly instead of
	/// silently reporting zero stages and passing every duplicate check.
	/// </remarks>
	private static int CountStages<TMiddleware>(IDispatchBuilder builder)
	{
		var field = builder.GetType().GetField(
			"_globalMiddleware",
			System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

		field.ShouldNotBeNull(
			"DispatchBuilder._globalMiddleware is the list UseMiddleware appends to; if it has been renamed "
			+ "this test must be updated rather than left to report zero stages and pass vacuously");

		var middleware = field!.GetValue(builder) as List<Type>;
		middleware.ShouldNotBeNull("the middleware list must be readable for this assertion to mean anything");

		return middleware!.Count(t => t == typeof(TMiddleware));
	}

	private static IDispatchBuilder Configure(Action<IDispatchBuilder> configure)
	{
		IDispatchBuilder? captured = null;
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddDispatch(dispatch =>
		{
			captured = dispatch;
			configure(dispatch);
		});

		captured.ShouldNotBeNull("AddDispatch must invoke its configuration callback");
		return captured!;
	}

	[Fact]
	public void RegisterOneCachingStageForTheAdvertisedCompositionOfUseCachingAndWithCachingOptions()
	{
		// SAFETY. This is the exact composition the documentation teaches and every caching test file uses.
		// Two stages here is a permanent hang, not a slow path.
		var builder = Configure(dispatch => dispatch.UseCaching().WithCachingOptions(o => o.Enabled = true));

		CountStages<CachingServiceCollectionExtensions.CachingMiddlewareWrapper>(builder).ShouldBe(
			1,
			"UseCaching() followed by WithCachingOptions(...) is the advertised composition, and "
			+ "WithCachingOptions routes through UseCaching -- so without an idempotency guard the caching "
			+ "middleware is appended twice, and a second stage waits forever on the in-flight cache entry "
			+ "the first stage is creating");

		CountStages<CachingServiceCollectionExtensions.CacheInvalidationMiddlewareWrapper>(builder).ShouldBe(
			1,
			"the invalidation stage is registered alongside the caching stage and must be guarded with it");
	}

	[Fact]
	public void RegisterOneCachingStageWhenRegistrationIsRepeated()
	{
		// The same invariant stated directly, so it holds for any repetition and not merely for the one
		// composition above -- a consumer calling UseCaching() twice must not deadlock either.
		var builder = Configure(dispatch => dispatch.UseCaching().UseCaching().UseCaching());

		CountStages<CachingServiceCollectionExtensions.CachingMiddlewareWrapper>(builder)
			.ShouldBe(1, "registration is documented as idempotent");
	}

	[Fact]
	public void StillRegisterTheCachingStageWhenRegisteredOnce()
	{
		// LIVENESS. "Exactly one" has to exclude zero as well as two. Without this arm, a guard that
		// suppressed registration entirely would satisfy every assertion above while leaving caching inert
		// -- a cache that never runs is trivially free of stampedes.
		var builder = Configure(dispatch => dispatch.UseCaching());

		CountStages<CachingServiceCollectionExtensions.CachingMiddlewareWrapper>(builder).ShouldBe(
			1,
			"a single UseCaching() must actually put the middleware in the pipeline; a guard that registered "
			+ "nothing would pass every duplicate-suppression check and silently disable caching");
	}

	[Fact]
	public void RegisterOneCachingStageWhenOnlyWithCachingOptionsIsCalled()
	{
		// WithCachingOptions documents that it also enables caching, so it must register the stage on its
		// own -- and still only once.
		var builder = Configure(dispatch => dispatch.WithCachingOptions(o => o.Enabled = true));

		CountStages<CachingServiceCollectionExtensions.CachingMiddlewareWrapper>(builder).ShouldBe(
			1,
			"WithCachingOptions documents that configuring caching also enables it, so it must register the "
			+ "pipeline stage by itself");
	}
}
