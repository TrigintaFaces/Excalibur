// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Testing.Conformance.DependencyInjection;

namespace Excalibur.Tests.MinimalWiring;

/// <summary>
/// Marker for the <c>IExcaliburBuilder.AddDispatch(...)</c> composition bridge.
/// See <c>ExcaliburBuilderDispatchExtensions</c>.
/// </summary>
public sealed class AddDispatchBridgeMarker { }

/// <summary>
/// Contract pin for the <c>IExcaliburBuilder.AddDispatch</c> bridge: it composes the Dispatch
/// pipeline within the Excalibur builder and produces the same service-graph state as a direct
/// <c>services.AddDispatch()</c> call, leaving <see cref="IDispatcher"/> resolvable from the
/// composition root.
/// </summary>
/// <remarks>
/// Bucket A. Per ADR-342 (registration verb standard), <c>AddDispatch</c> is the single canonical
/// registration verb on <c>IExcaliburBuilder</c>; the former <c>UseDispatch</c> dual-alias was
/// removed (greenfield, no compat shim). This pin contract-pins the public API so a future refactor
/// that accidentally breaks the bridge is caught at test time.
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
