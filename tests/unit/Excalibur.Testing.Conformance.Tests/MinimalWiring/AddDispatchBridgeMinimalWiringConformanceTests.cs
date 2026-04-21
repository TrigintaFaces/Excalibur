// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Testing.Conformance.DependencyInjection;

namespace Excalibur.Tests.MinimalWiring;

/// <summary>
/// Marker for S792 Workstream A1 — <c>IExcaliburBuilder.AddDispatch(...)</c> forwarding alias
/// to <c>UseDispatch</c>. See <c>ExcaliburBuilderDispatchExtensions</c>.
/// </summary>
public sealed class AddDispatchBridgeMarker { }

/// <summary>
/// Contract pin for Sprint 792 Workstream A1 (<c>bd-t63bns</c>, commit <c>e7b191058</c>):
/// the <c>IExcaliburBuilder.AddDispatch</c> bridge forwards to <c>UseDispatch</c> and
/// produces the same service-graph state as a direct <c>AddDispatch</c> call, leaving
/// <see cref="IDispatcher"/> resolvable from the composition root.
/// </summary>
/// <remarks>
/// Bucket A. Filed as <c>bd-elyhe9</c> S793 follow-up (Workstream D3) after the S792
/// FORGE pre-PR checklist miss on COMPASS msg 1423 §10.4. Bridge is a pure forwarding
/// alias; this pin contract-pins the public API so a future refactor that accidentally
/// breaks the forwarding is caught at test time.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Pattern", "MINIMAL-WIRING")]
public sealed class AddDispatchBridgeMinimalWiringConformanceTests
	: MinimalWiringConformanceTestKit<AddDispatchBridgeMarker>
{
	/// <inheritdoc />
	protected override Action<IServiceCollection> Invoke =>
		static services => services.AddExcalibur(static x => x.AddDispatch());

	/// <inheritdoc />
	protected override IReadOnlyList<Type> ExpectedResolvableServices => new[]
	{
		typeof(IDispatcher),
	};

	/// <summary>Bucket A isolation gate — bridge forwards and leaves IDispatcher resolvable.</summary>
	[Fact]
	public void Gate_Isolation() => ExecuteIsolationGate();

	/// <summary>
	/// Bucket A idempotence gate — positive assertion post S794-D1 framework fix
	/// (commit <c>ed975a746</c>, bd <c>ffecs4</c>).
	/// </summary>
	/// <remarks>
	/// <c>AddDispatch</c> now guards builder-mode re-entry and uses <c>TryAdd*</c> semantics
	/// for <c>PipelineProfileRegistry</c>, <c>TransportBindingRegistry</c>,
	/// <c>IMiddlewareApplicabilityStrategy</c>, <c>PipelineProfileSynthesizer</c>, and the
	/// remaining Bucket-1 registrations (per COMPASS msg 1473 §D-table). Second-invocation
	/// through the bridge (<c>AddExcalibur(x =&gt; x.AddDispatch())</c>) no longer drifts.
	/// Pin flipped inverted→positive per S791/S792/S793 lifecycle (CRUCIBLE-owned).
	/// </remarks>
	[Fact]
	public void Gate_Idempotence() => ExecuteIdempotenceGate();
}
