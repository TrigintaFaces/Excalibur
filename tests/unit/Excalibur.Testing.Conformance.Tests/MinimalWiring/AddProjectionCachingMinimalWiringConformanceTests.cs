// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Caching.Projections;
using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Caching;
using Excalibur.Testing.Conformance.DependencyInjection;

using FakeItEasy;

namespace Excalibur.Tests.MinimalWiring;

/// <summary>
/// Marker type for the <c>services.AddExcalibur(x =&gt; x.AddEventSourcing(es =&gt; es.AddProjectionCaching()))</c>
/// builder-bridge terminal (S804 / <c>bd-sdhocq</c> Front-A entry A15).
/// </summary>
public sealed class AddProjectionCachingMarker { }

/// <summary>
/// S804 <c>bd-sdhocq</c> A15 regression pin for the <see cref="Excalibur.EventSourcing.DependencyInjection.IEventSourcingBuilder"/>
/// <c>AddProjectionCaching()</c> bridge. This pin follows the A10–A12 exemplar template
/// established in S803 (serverless carve-outs) and the paired-test discipline ratified
/// in ADR-325 §Secondary after the S803 <c>bd-zqkbnq</c> regression lesson.
/// </summary>
/// <remarks>
/// <para>
/// <b>Bucket A — Sensible defaults.</b> The bridge forwards to the internal
/// <c>AddExcaliburProjectionCaching()</c> aggregator, which TryAdd-registers
/// <see cref="IProjectionCacheInvalidator"/> with the default implementation
/// (<see cref="ProjectionCacheInvalidator"/>) as a Singleton. A bare
/// <c>services.AddExcalibur(x =&gt; x.AddEventSourcing(es =&gt; es.AddProjectionCaching()))</c>
/// therefore wires the projection-invalidation primitive with sensible defaults while
/// leaving <see cref="IDispatcher"/> resolvable via the root composition.
/// </para>
/// <para>
/// <b>ADR-078 invariant:</b> after invocation, <see cref="IDispatcher"/> MUST still resolve.
/// This is the boundary contract that <c>WrapperDIComplianceShould</c>-style tests used
/// to pin pre-S803; the MinimalWiring pin is the successor surface per COMPASS msg 1654
/// Path 2+.
/// </para>
/// <para>
/// <b>Canonical-path policy:</b> Projection caching is a sub-concern of event sourcing;
/// the bridge attaches at <see cref="Excalibur.EventSourcing.DependencyInjection.IEventSourcingBuilder"/>
/// rather than the root <c>IExcaliburBuilder</c>, per ADR-321.
/// </para>
/// <para>
/// <b>Paired with:</b> <c>EventSourcingBuilderProjectionCachingExtensionsShould</c> in
/// <c>Excalibur.Caching.Tests</c>. That file pins the bridge's registration shape
/// against the underlying aggregator 1:1 from the package-local perspective; this pin
/// pins the bucket/isolation/idempotence behavior from the composition-root perspective.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
[Trait("Pattern", "MINIMAL-WIRING")]
public sealed class AddProjectionCachingMinimalWiringConformanceTests
	: MinimalWiringConformanceTestKit<AddProjectionCachingMarker>
{
	/// <inheritdoc />
	protected override Action<IServiceCollection> Invoke =>
		static services => services.AddExcalibur(static x => x.AddEventSourcing(static es => es.AddProjectionCaching()));

	/// <inheritdoc />
	protected override MinimalWiringBucket Bucket => MinimalWiringBucket.SensibleDefaults;

	/// <inheritdoc />
	/// <remarks>
	/// ADR-078 boundary invariant: post-invocation,
	/// <see cref="IDispatcher"/> MUST resolve cleanly. The projection-caching bridge
	/// must not displace root Dispatch wiring. Additionally, the Singleton
	/// <see cref="IProjectionCacheInvalidator"/> registered by the underlying
	/// aggregator must be resolvable — confirming bridge-to-aggregator forwarding.
	/// </remarks>
	protected override IReadOnlyList<Type> ExpectedResolvableServices { get; } =
		new[] { typeof(IDispatcher), typeof(IProjectionCacheInvalidator) };

	/// <inheritdoc />
	/// <remarks>
	/// <see cref="ProjectionCacheInvalidator"/> requires
	/// <see cref="ICacheInvalidationService"/>, which is normally supplied by
	/// <c>AddDispatchCaching()</c> as a required prerequisite per the bridge's own
	/// documentation. Production consumers always compose it alongside — we mirror
	/// that prerequisite in foundation so the Isolation gate validates <em>this</em>
	/// bridge's wiring rather than the sibling caching contract.
	/// </remarks>
	protected override void AddRequiredFoundation(IServiceCollection services)
	{
		ArgumentNullException.ThrowIfNull(services);

		base.AddRequiredFoundation(services);
		services.AddSingleton(A.Fake<ICacheInvalidationService>());
	}

	/// <summary>Bucket A isolation gate — IDispatcher + IProjectionCacheInvalidator resolve after AddProjectionCaching with defaults.</summary>
	[Fact]
	public void Gate_Isolation() => ExecuteIsolationGate();

	/// <summary>Idempotence gate — second invocation is a no-op (TryAdd semantics).</summary>
	[Fact]
	public void Gate_Idempotence() => ExecuteIdempotenceGate();
}
