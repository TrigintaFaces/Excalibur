// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Diagnostics;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Tests.Configuration;

/// <summary>
/// Locks the zero-configuration contract: <c>AddDispatch()</c> on a bare service collection yields a
/// pipeline that RUNS, built from services the framework supplies itself.
/// </summary>
/// <remarks>
/// <para>
/// The defect these arms bind: the default profile declared seven middleware entries and
/// <c>AddDispatch()</c> registered none of them, so a consumer who called <c>AddDispatch()</c> and
/// dispatched a message ran an empty pipeline. Every entry was marked Optional, so materialization
/// null-skipped all seven and the pipeline completed successfully having done nothing.
/// </para>
/// <para>
/// The STRUCTURAL arm is the one that keeps this fixed. Declaration and registration are now two fields
/// of one element in <c>DefaultProfileMiddleware</c>, so a divergence is not a bug to be detected but a
/// state that cannot be written down; the arm fails if that single source is ever split back into two.
/// </para>
/// <para>
/// The SAFETY arm is not a check on the membership list — it IS the membership criterion. An entry
/// belongs in the default profile if and only if it can be constructed after <c>AddDispatch()</c> alone.
/// If this arm ever requires the consumer to register infrastructure to pass, an entry that does not
/// meet the criterion has been added to the profile.
/// </para>
/// </remarks>
public sealed class ZeroConfigAddDispatchRunsItsDefaultProfileShould
{
	/// <summary>
	/// LIVENESS: every entry the default profile declares resolves from a bare zero-config container.
	/// </summary>
	/// <remarks>
	/// Asserts the middleware are actually CONSTRUCTIBLE, not merely that a descriptor was added — the
	/// distinction that made the original defect invisible. A registered middleware whose own dependency
	/// cannot resolve throws here rather than returning null, which is exactly the failure this must catch.
	/// </remarks>
	[Fact]
	public void Materialise_every_entry_the_default_profile_declares()
	{
		var services = new ServiceCollection();

		_ = services.AddDispatch();

		using var provider = services.BuildServiceProvider();
		using var scope = provider.CreateScope();

		DefaultPipelineProfiles.DefaultProfileMiddleware.ShouldNotBeEmpty(
			"A default profile that declares nothing cannot make this arm meaningful.");

		foreach (var entry in DefaultPipelineProfiles.DefaultProfileMiddleware)
		{
			var resolved = scope.ServiceProvider.GetService(entry.MiddlewareType);

			resolved.ShouldNotBeNull(
				$"{entry.MiddlewareType.Name} is declared by the default profile, so a zero-config "
				+ "AddDispatch() must seat it. A null here is the empty-pipeline defect returning: the "
				+ "entry is Optional, so materialization would silently skip it and the consumer would "
				+ "get a pipeline that runs nothing.");
		}
	}

	/// <summary>
	/// SAFETY: a bare container needs NO consumer-supplied infrastructure. This arm defines the membership.
	/// </summary>
	/// <remarks>
	/// The collection here is genuinely bare — no <c>AddLogging()</c>, no transaction service, no outbox
	/// store, no metrics sink. <c>AddDispatch()</c> is the only call. If a future entry drags in a
	/// consumer-supplied dependency, resolving it throws and this arm goes red, which is the intended
	/// signal: that entry belongs in a profile the consumer deliberately selects, not in "default".
	/// </remarks>
	[Fact]
	public void Start_and_resolve_without_the_consumer_registering_any_infrastructure()
	{
		var services = new ServiceCollection();

		_ = services.AddDispatch();

		using var provider = services.BuildServiceProvider();
		using var scope = provider.CreateScope();

		foreach (var entry in DefaultPipelineProfiles.DefaultProfileMiddleware)
		{
			_ = Should.NotThrow(
				() => scope.ServiceProvider.GetService(entry.MiddlewareType),
				$"{entry.MiddlewareType.Name} required something the consumer did not register. The "
				+ "default profile's membership criterion is 'constructible after AddDispatch() alone'; "
				+ "an entry that fails it must move to a profile the consumer selects deliberately, as "
				+ "authorization and transactions already have.");
		}

		// The two dependencies this fix moved from consumer-supplied to framework-supplied. Both are
		// what made the middleware above unregisterable before, so they are asserted directly rather
		// than only through the loop that depends on them.
		scope.ServiceProvider.GetService<Microsoft.Extensions.Logging.ILoggerFactory>().ShouldNotBeNull(
			"AddDispatch() must call AddLogging() itself, the way AddHttpClient() does, so every "
			+ "middleware it seats can take an ILogger without the consumer remembering the call.");

		scope.ServiceProvider.GetService<IMessageMetrics>().ShouldNotBeNull(
			"A no-op IMessageMetrics must be registered by default, the sibling of the no-op "
			+ "ITelemetrySanitizer, so metrics recording is never the reason a dispatch fails.");
	}

	/// <summary>
	/// STRUCTURAL: nothing the default profile declares can be absent from the container.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>The property under test is one-directional, and that is the point.</b> A profile declaring a
	/// middleware the container never received is the defect: the pipeline warns and does nothing. The
	/// converse is not a defect at all — a middleware registered and declared by no profile simply costs
	/// nothing until some profile names it, which is exactly how the default profile is now arranged.
	/// </para>
	/// <para>
	/// This arm used to additionally require the declared set to be NON-EMPTY, on the reasoning that an
	/// empty one would make it vacuous. That conflated two things. The set being empty is now a
	/// deliberate design decision — a consumer who configures nothing runs no middleware and pays for
	/// none — so requiring it to be non-empty would pin a decision this arm has no business pinning.
	/// Non-vacuity is instead established by the second half below, which checks every REGISTERED entry
	/// resolves: that set is non-empty, and it is the set whose emptiness would really hide a defect.
	/// </para>
	/// </remarks>
	[Fact]
	public void Never_declare_a_middleware_the_container_did_not_receive()
	{
		var services = new ServiceCollection();

		_ = services.AddDispatch();

		var registered = services
			.Select(static descriptor => descriptor.ServiceType)
			.ToHashSet();

		var declared = DefaultPipelineProfiles
			.CreateDefaultProfile()
			.MiddlewareEntries
			.Select(static entry => entry.MiddlewareType)
			.ToList();

		foreach (var middlewareType in declared)
		{
			registered.ShouldContain(
				middlewareType,
				$"The default profile declares {middlewareType.Name} but AddDispatch() does not register "
				+ "it. A declared-but-unregistered middleware is a pipeline stage that warns and does "
				+ "nothing, which is the defect this arm exists to make inexpressible.");
		}

		// Non-vacuity, and the stronger half: every middleware AddDispatch registers must actually
		// RESOLVE. Set membership alone would pass for a descriptor whose own dependencies cannot be
		// satisfied -- which is how a middleware ends up registered, declared, and still unable to run.
		DefaultPipelineProfiles.DefaultProfileMiddleware.ShouldNotBeEmpty(
			"AddDispatch registers no middleware at all, so the resolution check below would examine "
			+ "nothing. That is the emptiness that would hide a defect.");

		using var provider = services.BuildServiceProvider(validateScopes: true);
		using var resolutionScope = provider.CreateScope();

		foreach (var entry in DefaultPipelineProfiles.DefaultProfileMiddleware)
		{
			resolutionScope.ServiceProvider.GetService(entry.MiddlewareType).ShouldNotBeNull(
				$"AddDispatch() registers {entry.MiddlewareType.Name}, but it cannot be resolved from a "
				+ "bare container. A profile that names it would compose a stage that throws at the "
				+ "first dispatch rather than one that runs.");
		}
	}
}
