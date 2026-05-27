// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Testing.Conformance.DependencyInjection;

using FakeItEasy;

using Microsoft.Extensions.Hosting;

using Quartz;

namespace Excalibur.Tests.MinimalWiring;

/// <summary>
/// Marker type for the <c>services.AddExcalibur(x =&gt; x.AddJobs(...))</c> builder-bridge
/// terminal (S804 / <c>bd-sdhocq</c> Front-A entry A13).
/// </summary>
public sealed class AddJobsMarker { }

/// <summary>
/// S804 <c>bd-sdhocq</c> A13 regression pin for the <see cref="IExcaliburBuilder"/>
/// <c>AddJobs(...)</c> bridge. This pin follows the A10–A12 exemplar template established
/// in S803 (serverless carve-outs) and the paired-test discipline ratified in ADR-325
/// §Secondary after the S803 <c>bd-zqkbnq</c> regression lesson.
/// </summary>
/// <remarks>
/// <para>
/// <b>Bucket A — Sensible defaults.</b> The bridge forwards to the internal
/// <c>AddExcaliburJobHost(...)</c> aggregator, which registers Quartz primitives
/// (<see cref="ISchedulerFactory"/>, scheduler hosted service, job adapters) with
/// framework defaults. A bare <c>services.AddExcalibur(x =&gt; x.AddJobs())</c>
/// therefore produces a functional composition root where <see cref="IDispatcher"/>
/// resolves cleanly and the scheduler contract is available.
/// </para>
/// <para>
/// <b>ADR-078 invariant:</b> after invocation (against a container with no consumer overrides),
/// <see cref="IDispatcher"/> MUST still resolve. This is the boundary contract that
/// <c>WrapperDIComplianceShould</c>-style tests used to pin pre-S803; the MinimalWiring pin
/// is the successor surface per COMPASS msg 1654 Path 2+.
/// </para>
/// <para>
/// <b>Idempotence:</b> Quartz registrations use TryAdd semantics. A second invocation must
/// not duplicate <see cref="ISchedulerFactory"/>.
/// </para>
/// <para>
/// <b>Paired with:</b> <c>JobsExcaliburBuilderExtensionsShould</c> in
/// <c>Excalibur.Hosting.Tests</c>. That file pins the bridge's registration shape
/// against the underlying aggregator 1:1 from the package-local perspective; this pin
/// pins the bucket/isolation/idempotence/override behavior from the composition-root
/// perspective.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Hosting.Jobs")]
[Trait("Pattern", "MINIMAL-WIRING")]
public sealed class AddJobsMinimalWiringConformanceTests
	: MinimalWiringConformanceTestKit<AddJobsMarker>
{
	/// <inheritdoc />
	protected override Action<IServiceCollection> Invoke =>
		static services => services.AddExcalibur(static x => x.AddJobs());

	/// <inheritdoc />
	protected override MinimalWiringBucket Bucket => MinimalWiringBucket.SensibleDefaults;

	/// <inheritdoc />
	/// <remarks>
	/// ADR-078 boundary invariant: <c>AddExcalibur(x =&gt; x.AddJobs())</c> against a
	/// foundation-only container must leave <see cref="IDispatcher"/> resolvable.
	/// The Quartz bridge must not displace the root Dispatch wiring.
	/// </remarks>
	protected override IReadOnlyList<Type> ExpectedResolvableServices { get; } =
		new[] { typeof(IDispatcher) };

	/// <inheritdoc />
	/// <remarks>
	/// Quartz's <c>QuartzHostedService</c> is registered as a singleton and requires
	/// <see cref="IHostApplicationLifetime"/> at build-time validation. Production consumers
	/// always compose through an <see cref="IHostApplicationBuilder"/> where that service is
	/// foundational — never against a bare <see cref="IServiceCollection"/>. We therefore
	/// declare the lifetime primitive as part of the pin's foundation (mirroring reality) so
	/// the Isolation gate validates <em>our</em> bridge surface, not Quartz's hosting
	/// prerequisites.
	/// </remarks>
	protected override void AddRequiredFoundation(IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		base.AddRequiredFoundation(services);
		services.AddSingleton(A.Fake<IHostApplicationLifetime>());
	}

	/// <summary>Bucket A isolation gate — IDispatcher resolves after AddJobs with defaults.</summary>
	[Fact]
	public void Gate_Isolation() => ExecuteIsolationGate();

	// NOTE (S804-F CRUCIBLE finding, post-bd-addjobs-idempotency): Our-side idempotence IS fixed —
	// FORGE's 2026-04-19 commit (msg 2046) flipped JobHeartbeatTracker / QuartzJobAdapter /
	// QuartzGenericJobAdapter<,> to TryAdd*. The single remaining non-idempotent descriptor is
	// Quartz.QuartzHostedService, registered by the upstream `AddQuartzHostedService(...)` SDK
	// call. That registration is owned by the Quartz package and is outside Excalibur's
	// control per FORGE msg 2046. The harness has no way to distinguish "our non-idempotence"
	// from "upstream SDK non-idempotence" without an ignore-list extension on
	// MinimalWiringConformanceTestKit<T>. Gate_Idempotence stays off on this pin until either
	// (a) Quartz ships TryAdd-style hosted service registration upstream, or (b) the kit grows
	// an upstream-descriptor-ignore hook. For now the paired-test in
	// `JobsExcaliburBuilderExtensionsShould` (Excalibur.Hosting.Tests) covers the narrow
	// "ISchedulerFactory does not duplicate" invariant that matters for our bridge surface.
}
