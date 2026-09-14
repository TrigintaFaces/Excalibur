// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

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
	/// STRUCTURAL: what the default profile DECLARES equals what <c>AddDispatch()</c> REGISTERS.
	/// </summary>
	/// <remarks>
	/// Computed from the single source, so re-introducing a declaration with no registration fails here.
	/// This is the arm that makes the divergence inexpressible rather than merely warned about.
	/// </remarks>
	[Fact]
	public void Declare_exactly_the_set_that_AddDispatch_registers()
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

		declared.ShouldNotBeEmpty(
			"An empty declared set would make this arm vacuous — it would pass against a profile that "
			+ "declares nothing, which is the very state the liveness arm exists to reject.");

		foreach (var middlewareType in declared)
		{
			registered.ShouldContain(
				middlewareType,
				$"The default profile declares {middlewareType.Name} but AddDispatch() does not register "
				+ "it. Both are read from DefaultProfileMiddleware, so this can only fail if that single "
				+ "source has been split back into two lists that can disagree.");
		}
	}
}
